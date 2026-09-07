using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Owns the run: the clock, the difficulty ramp, scoring, shard income, and every global
// time effect.
//
// Scoring is a risk/reward loop. Points accrue from survival scaled by speed and combo;
// skimming a tree without touching it raises the combo, and hitting one ends the run
// unless a Shield upgrade absorbs it. Terrain is never lethal - bumping over a ridge only
// dents the combo.
//
// This is also the single authority over Time.timeScale, so pause, impact slow-motion and
// the game-over crawl can never fight each other.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Run pacing")]
    [SerializeField] private float rampDuration = 150f;
    [SerializeField] private float maxSpeedMultiplier = 2.6f;

    [Header("Scoring")]
    [SerializeField] private float pointsPerSecond = 12f;
    [SerializeField] private float nearMissBonus = 60f;
    [SerializeField] private float comboStep = 0.4f;
    [SerializeField] private float maxCombo = 8f;
    [SerializeField] private float comboHoldTime = 2.5f;
    [SerializeField] private float comboDecayRate = 1.2f;

    [Header("Shards")]
    [SerializeField] private float shardsPerThousandPoints = 12f;
    [SerializeField] private float shardsPerNearMiss = 0.6f;

    [Header("Impact feel")]
    [SerializeField] private float nearMissTimeScale = 0.6f;
    [SerializeField] private float nearMissSlowMoTime = 0.08f;
    [SerializeField] private float slowMoCooldown = 1.4f;
    [SerializeField] private float impactCooldown = 0.6f;
    [SerializeField] private float impactComboCost = 1.2f;
    [SerializeField] private float shieldTimeScale = 0.4f;
    [SerializeField] private float shieldSlowMoTime = 0.2f;
    [SerializeField] private float shieldGraceTime = 1.2f;
    [SerializeField] private float deathTimeScale = 0.25f;
    [SerializeField] private float deathSlowMoTime = 0.25f;
    [SerializeField] private float timeBlendRate = 7f;
    [SerializeField] private float gameOverTimeScale = 0.18f;

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    public float ElapsedTime { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsRunning { get; private set; } = true;
    public bool IsGameOver => !IsRunning;
    public float Difficulty01 { get; private set; }
    public float SpeedMultiplier => Mathf.Lerp(1f, maxSpeedMultiplier, Difficulty01);
    public float Score { get; private set; }
    public float Combo { get; private set; } = 1f;
    public float BestScore { get; private set; }
    public int RunShards { get; private set; }
    public int ShieldsRemaining => Mathf.Max(0, PlayerProgress.ShieldCharges - shieldsUsed);
    public bool IsInvulnerable => Time.unscaledTime < invulnerableUntil;

    public event System.Action<bool> PauseChanged;
    public event System.Action<float> NearMissed;
    // Severity 0..1: light terrain bumps sit near the bottom, a fatal hit is 1.
    public event System.Action<float> Impacted;
    public event System.Action GameOver;

    private const string BestScoreKey = "MountainRunner.BestScore";

    private float comboTimer;
    private float slowMoEndTime;
    private float slowMoScale = 1f;
    private float lastSlowMoTime = -99f;
    private float lastImpactTime = -99f;
    private float invulnerableUntil;
    private float shardFraction;
    private int shieldsUsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        BestScore = PlayerPrefs.GetFloat(BestScoreKey, 0f);

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    private void OnDestroy()
    {
        SaveBest();
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit() => SaveBest();

    private void Update()
    {
        HandleHotkeys();
        if (IsPaused) return;

        UpdateTimeScale();
        if (!IsRunning) return;

        float dt = Time.deltaTime;
        ElapsedTime += dt;
        Difficulty01 = rampDuration > 0f ? Mathf.Clamp01(ElapsedTime / rampDuration) : 1f;

        comboTimer -= dt;
        if (comboTimer <= 0f)
            Combo = Mathf.MoveTowards(Combo, 1f, comboDecayRate * dt);

        float gained = pointsPerSecond * SpeedMultiplier * Combo * PlayerProgress.ScoreMultiplier * dt;
        Score += gained;
        AccrueShards(gained * shardsPerThousandPoints / 1000f);

        if (Score > BestScore) BestScore = Score;
    }

    private void HandleHotkeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (!IsRunning)
        {
            if (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame)
                RestartLevel();
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
            TogglePause();
        if (IsPaused && keyboard.rKey.wasPressedThisFrame)
            RestartLevel();
    }

    // Shards are earned as a fraction of points, so the whole-number balance only ticks
    // up once the accumulated fraction crosses 1.
    private void AccrueShards(float amount)
    {
        shardFraction += amount * PlayerProgress.FortuneMultiplier;
        if (shardFraction < 1f) return;

        int whole = Mathf.FloorToInt(shardFraction);
        shardFraction -= whole;
        RunShards += whole;
    }

    private void UpdateTimeScale()
    {
        float target = 1f;
        if (!IsRunning) target = gameOverTimeScale;
        else if (Time.unscaledTime < slowMoEndTime) target = slowMoScale;

        Time.timeScale = Mathf.MoveTowards(Time.timeScale, target, timeBlendRate * Time.unscaledDeltaTime);
    }

    private void RequestSlowMo(float scale, float duration, bool respectCooldown)
    {
        if (duration <= 0f) return;
        if (respectCooldown && Time.unscaledTime < lastSlowMoTime + slowMoCooldown) return;

        slowMoScale = scale;
        slowMoEndTime = Time.unscaledTime + duration;
        lastSlowMoTime = Time.unscaledTime;
    }

    public void RegisterNearMiss()
    {
        if (!IsRunning || IsPaused) return;

        Combo = Mathf.Min(Combo + comboStep, maxCombo);
        Score += nearMissBonus * Combo;
        AccrueShards(shardsPerNearMiss);
        comboTimer = comboHoldTime;
        RequestSlowMo(nearMissTimeScale, nearMissSlowMoTime, respectCooldown: true);
        NearMissed?.Invoke(Combo);
    }

    // A non-fatal knock against the terrain. It costs combo progress rather than wiping
    // it, never slows time, and is rate limited so a rough stretch reads as a single bump
    // instead of a stack of them.
    public void RegisterImpact(float severity)
    {
        if (!IsRunning || IsPaused) return;
        if (Time.unscaledTime < lastImpactTime + impactCooldown) return;

        lastImpactTime = Time.unscaledTime;
        severity = Mathf.Clamp01(severity);

        Combo = Mathf.Max(1f, Combo - impactComboCost * severity);
        comboTimer = Mathf.Min(comboTimer, comboHoldTime * 0.5f);
        Impacted?.Invoke(severity);
    }

    // Hitting a tree. A Shield upgrade absorbs the first hits of a run and grants a short
    // grace period, so the same tree cannot immediately claim a second charge.
    public void TriggerGameOver()
    {
        if (!IsRunning || IsPaused) return;
        if (Time.unscaledTime < invulnerableUntil) return;

        if (ShieldsRemaining > 0)
        {
            shieldsUsed++;
            invulnerableUntil = Time.unscaledTime + shieldGraceTime;
            Combo = 1f;
            comboTimer = 0f;
            RequestSlowMo(shieldTimeScale, shieldSlowMoTime, respectCooldown: false);
            Impacted?.Invoke(0.7f);
            return;
        }

        IsRunning = false;
        Combo = 1f;
        RequestSlowMo(deathTimeScale, deathSlowMoTime, respectCooldown: false);
        Impacted?.Invoke(1f);

        PlayerProgress.AddCurrency(RunShards);
        SaveBest();
        GameOver?.Invoke();
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        slowMoEndTime = 0f;
        PauseChanged?.Invoke(false);
    }

    public void RestartLevel()
    {
        SaveBest();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        SaveBest();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SaveBest()
    {
        PlayerPrefs.SetFloat(BestScoreKey, BestScore);
        PlayerPrefs.Save();
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }
}
