using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds and drives the whole HUD from code, so the scene stays free of a fragile
// hand-authored Canvas hierarchy. Reads state from GameManager.Instance, reads the steer
// meter straight off RollingPlanet, and reacts to the manager events for the combo pulse
// and the impact flash. It also owns the upgrade shop, which the pause and game-over
// panels open.
public class GameHUD : MonoBehaviour
{
    private const float SteerTrackHalfWidth = 167f;

    private Text timerText;
    private Text scoreText;
    private Text bestText;
    private Text comboText;
    private Text speedText;
    private Text shardText;
    private Text shieldText;
    private Image crashFlash;
    private RectTransform steerKnob;
    private Image steerKnobImage;
    private GameObject pausePanel;
    private GameObject gameOverPanel;
    private Text gameOverScoreText;
    private Text gameOverBestText;
    private Text gameOverShardText;

    private readonly ShopPanel shop = new ShopPanel();
    private Font font;
    private RollingPlanet planet;
    private float comboPulse;
    private float flashAmount;

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        planet = FindAnyObjectByType<RollingPlanet>();

        EnsureEventSystem();
        BuildCanvas();

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.NearMissed += OnNearMiss;
            gm.Impacted += OnImpact;
        }
    }

    private void OnDestroy()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.NearMissed -= OnNearMiss;
            gm.Impacted -= OnImpact;
        }
    }

    private void OnNearMiss(float combo) => comboPulse = 1f;

    private void OnImpact(float severity) => flashAmount = Mathf.Max(flashAmount, Mathf.Clamp01(severity));

    private void Update()
    {
        // Everything here runs on unscaled time so the HUD stays lively during the impact
        // slow-motion instead of crawling with it.
        float udt = Time.unscaledDeltaTime;
        comboPulse = Mathf.MoveTowards(comboPulse, 0f, 3f * udt);
        flashAmount = Mathf.MoveTowards(flashAmount, 0f, 1.6f * udt);

        if (crashFlash != null)
            crashFlash.color = new Color(1f, 0.28f, 0.22f, flashAmount * 0.32f);

        if (steerKnob != null && planet != null)
        {
            float steer = planet.SteerAngle01;
            steerKnob.anchoredPosition = new Vector2(steer * SteerTrackHalfWidth, 0f);
            steerKnobImage.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.28f), Mathf.Abs(steer));
        }

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        timerText.text = FormatTime(gm.ElapsedTime);
        scoreText.text = Mathf.FloorToInt(gm.Score).ToString("N0");
        bestText.text = "BEST  " + Mathf.FloorToInt(gm.BestScore).ToString("N0");
        speedText.text = "SPEED  x" + gm.SpeedMultiplier.ToString("0.00");
        shardText.text = "SHARDS  " + (PlayerProgress.Currency + gm.RunShards);

        bool hasShield = gm.ShieldsRemaining > 0;
        shieldText.enabled = hasShield;
        if (hasShield) shieldText.text = "SHIELD  x" + gm.ShieldsRemaining;

        bool showCombo = gm.Combo > 1.05f;
        comboText.enabled = showCombo;
        if (showCombo)
        {
            comboText.text = "COMBO  x" + gm.Combo.ToString("0.0");
            comboText.rectTransform.localScale = Vector3.one * (1f + comboPulse * 0.3f);
            comboText.color = Color.Lerp(UIFactory.Accent, Color.white, comboPulse);
        }

        if (pausePanel != null && pausePanel.activeSelf != gm.IsPaused)
            pausePanel.SetActive(gm.IsPaused);

        if (gameOverPanel != null && gameOverPanel.activeSelf != gm.IsGameOver)
        {
            gameOverPanel.SetActive(gm.IsGameOver);
            if (gm.IsGameOver)
            {
                gameOverScoreText.text = Mathf.FloorToInt(gm.Score).ToString("N0");
                gameOverBestText.text = "BEST  " + Mathf.FloorToInt(gm.BestScore).ToString("N0")
                                        + "     TIME  " + FormatTime(gm.ElapsedTime);
                gameOverShardText.text = "+" + gm.RunShards + " SHARDS EARNED";
            }
        }

        // The shop is only reachable from pause or game over, so if the run is live again
        // it has no business being open.
        if (shop.IsOpen && !gm.IsPaused && !gm.IsGameOver) shop.SetOpen(false);
    }

    private static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return string.Format("{0:00}:{1:00.0}", minutes, rest);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        if (module.actionsAsset == null) module.AssignDefaultActions();
    }

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("HUD Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        Transform root = canvasGO.transform;

        scoreText = UIFactory.MakeText(root, font, "Score", "0", 58, TextAnchor.UpperLeft);
        UIFactory.Anchor(scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -22f), new Vector2(560f, 74f));
        UIFactory.AddOutline(scoreText.gameObject);

        bestText = UIFactory.MakeText(root, font, "Best", "BEST  0", 22, TextAnchor.UpperLeft);
        bestText.color = new Color(1f, 1f, 1f, 0.55f);
        UIFactory.Anchor(bestText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -94f), new Vector2(400f, 32f));

        comboText = UIFactory.MakeText(root, font, "Combo", "", 32, TextAnchor.UpperLeft);
        comboText.color = UIFactory.Accent;
        UIFactory.Anchor(comboText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -128f), new Vector2(400f, 44f));
        UIFactory.AddOutline(comboText.gameObject);
        comboText.enabled = false;

        shieldText = UIFactory.MakeText(root, font, "Shield", "", 26, TextAnchor.UpperLeft);
        shieldText.color = new Color(0.55f, 0.85f, 1f);
        UIFactory.Anchor(shieldText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -176f), new Vector2(400f, 40f));
        UIFactory.AddOutline(shieldText.gameObject);
        shieldText.enabled = false;

        timerText = UIFactory.MakeText(root, font, "Timer", "00:00.0", 44, TextAnchor.UpperCenter);
        UIFactory.Anchor(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(500f, 70f));
        UIFactory.AddOutline(timerText.gameObject);

        speedText = UIFactory.MakeText(root, font, "Speed", "SPEED  x1.00", 28, TextAnchor.UpperRight);
        speedText.color = new Color(1f, 1f, 1f, 0.8f);
        UIFactory.Anchor(speedText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(400f, 44f));
        UIFactory.AddOutline(speedText.gameObject);

        shardText = UIFactory.MakeText(root, font, "Shards", "SHARDS  0", 26, TextAnchor.UpperRight);
        shardText.color = UIFactory.Accent;
        UIFactory.Anchor(shardText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -76f), new Vector2(400f, 40f));
        UIFactory.AddOutline(shardText.gameObject);

        BuildSteerMeter(root);

        Text hint = UIFactory.MakeText(root, font, "Hint", "Esc  -  Pause", 22, TextAnchor.LowerRight);
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        UIFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(300f, 40f));

        crashFlash = UIFactory.MakePanel(root, "Crash Flash", new Color(1f, 0.28f, 0.22f, 0f));
        crashFlash.raycastTarget = false;
        UIFactory.Stretch(crashFlash.rectTransform);

        BuildPausePanel(root);
        BuildGameOverPanel(root);

        // Built last so it renders above the pause and game-over screens that open it.
        shop.Build(root, font);
    }

    private void BuildSteerMeter(Transform parent)
    {
        Image track = UIFactory.MakePanel(parent, "Steer Track", new Color(1f, 1f, 1f, 0.16f));
        track.raycastTarget = false;
        UIFactory.Anchor(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(360f, 6f));

        steerKnobImage = UIFactory.MakePanel(track.transform, "Steer Knob", Color.white);
        steerKnobImage.raycastTarget = false;
        steerKnob = steerKnobImage.rectTransform;
        steerKnob.anchorMin = steerKnob.anchorMax = steerKnob.pivot = new Vector2(0.5f, 0.5f);
        steerKnob.sizeDelta = new Vector2(24f, 24f);
        steerKnob.anchoredPosition = Vector2.zero;
    }

    private void BuildPausePanel(Transform parent)
    {
        Image bg = UIFactory.MakePanel(parent, "Pause Panel", new Color(0.02f, 0.03f, 0.06f, 0.72f));
        pausePanel = bg.gameObject;
        UIFactory.Stretch(bg.rectTransform);

        Text title = UIFactory.MakeText(pausePanel.transform, font, "Title", "PAUSED", 72, TextAnchor.MiddleCenter);
        UIFactory.Place(title.rectTransform, new Vector2(0f, 180f), new Vector2(600f, 100f));

        Vector2 size = new Vector2(340f, 64f);
        Text unused;
        UIFactory.MakeButton(pausePanel.transform, font, "Resume", size, new Vector2(0f, 60f),
                             OnResumeClicked, out unused);
        UIFactory.MakeButton(pausePanel.transform, font, "Upgrades", size, new Vector2(0f, -20f),
                             OnShopClicked, out unused);
        UIFactory.MakeButton(pausePanel.transform, font, "Restart", size, new Vector2(0f, -100f),
                             OnRestartClicked, out unused);
        UIFactory.MakeButton(pausePanel.transform, font, "Quit", size, new Vector2(0f, -180f),
                             OnQuitClicked, out unused);

        pausePanel.SetActive(false);
    }

    private void BuildGameOverPanel(Transform parent)
    {
        Image bg = UIFactory.MakePanel(parent, "Game Over Panel", new Color(0.06f, 0.02f, 0.03f, 0.78f));
        gameOverPanel = bg.gameObject;
        UIFactory.Stretch(bg.rectTransform);

        Text title = UIFactory.MakeText(gameOverPanel.transform, font, "Title", "WIPEOUT", 78, TextAnchor.MiddleCenter);
        title.color = new Color(1f, 0.42f, 0.32f);
        UIFactory.Place(title.rectTransform, new Vector2(0f, 250f), new Vector2(800f, 110f));
        UIFactory.AddOutline(title.gameObject);

        gameOverScoreText = UIFactory.MakeText(gameOverPanel.transform, font, "Final Score", "0", 92,
                                               TextAnchor.MiddleCenter);
        UIFactory.Place(gameOverScoreText.rectTransform, new Vector2(0f, 155f), new Vector2(800f, 120f));
        UIFactory.AddOutline(gameOverScoreText.gameObject);

        gameOverBestText = UIFactory.MakeText(gameOverPanel.transform, font, "Final Best", "", 26,
                                              TextAnchor.MiddleCenter);
        gameOverBestText.color = new Color(1f, 1f, 1f, 0.65f);
        UIFactory.Place(gameOverBestText.rectTransform, new Vector2(0f, 90f), new Vector2(800f, 40f));

        gameOverShardText = UIFactory.MakeText(gameOverPanel.transform, font, "Earned", "", 30,
                                               TextAnchor.MiddleCenter);
        gameOverShardText.color = UIFactory.Accent;
        UIFactory.Place(gameOverShardText.rectTransform, new Vector2(0f, 45f), new Vector2(800f, 44f));
        UIFactory.AddOutline(gameOverShardText.gameObject);

        Vector2 size = new Vector2(340f, 64f);
        Text unused;
        UIFactory.MakeButton(gameOverPanel.transform, font, "Retry", size, new Vector2(0f, -30f),
                             OnRestartClicked, out unused);
        UIFactory.MakeButton(gameOverPanel.transform, font, "Upgrades", size, new Vector2(0f, -110f),
                             OnShopClicked, out unused);
        UIFactory.MakeButton(gameOverPanel.transform, font, "Quit", size, new Vector2(0f, -190f),
                             OnQuitClicked, out unused);

        Text hint = UIFactory.MakeText(gameOverPanel.transform, font, "Retry Hint", "R  /  Enter  -  Retry", 22,
                                       TextAnchor.MiddleCenter);
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        UIFactory.Place(hint.rectTransform, new Vector2(0f, -250f), new Vector2(600f, 40f));

        gameOverPanel.SetActive(false);
    }

    private void OnResumeClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Resume();
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartLevel();
    }

    private void OnQuitClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.QuitGame();
    }

    private void OnShopClicked() => shop.SetOpen(true);
}
