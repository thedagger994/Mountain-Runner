using UnityEngine;

// Synthesises the game's audio at runtime so the project needs no sound assets: a swept
// noise "whoosh" for near misses, a pitched blip that rises with the combo, a low thud
// for impacts, and a looping wind bed whose volume and pitch track the run's speed.
//
// The wind loop is generated slightly long and then has its tail cross-faded into its
// head before being trimmed, which hides the seam that raw looped noise would produce.
public class GameAudio : MonoBehaviour
{
    [SerializeField] private bool enableAudio = true;
    [SerializeField] private float masterVolume = 0.6f;
    [SerializeField] private float windVolume = 0.22f;

    private const int SampleRate = 44100;

    private AudioSource sfxSource;
    private AudioSource comboSource;
    private AudioSource windSource;
    private AudioClip whooshClip;
    private AudioClip thudClip;
    private AudioClip comboClip;

    private void Awake()
    {
        if (!enableAudio)
        {
            enabled = false;
            return;
        }

        whooshClip = BuildWhoosh();
        thudClip = BuildThud();
        comboClip = BuildCombo();

        sfxSource = CreateSource();
        comboSource = CreateSource();
        windSource = CreateSource();

        windSource.clip = BuildWind();
        windSource.loop = true;
        windSource.volume = 0f;
        windSource.Play();
    }

    private void Start()
    {
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

    private void Update()
    {
        if (windSource == null) return;

        float difficulty = GameManager.Instance != null ? GameManager.Instance.Difficulty01 : 0f;
        windSource.volume = windVolume * masterVolume * (0.35f + 0.65f * difficulty);
        windSource.pitch = 0.85f + 0.4f * difficulty;
    }

    private AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void OnNearMiss(float combo)
    {
        sfxSource.PlayOneShot(whooshClip, 0.45f * masterVolume);
        comboSource.pitch = Mathf.Clamp(0.95f + (combo - 1f) * 0.12f, 0.9f, 2.2f);
        comboSource.PlayOneShot(comboClip, 0.3f * masterVolume);
    }

    private void OnImpact(float severity)
    {
        sfxSource.PlayOneShot(thudClip, Mathf.Clamp01(0.15f + severity * 0.7f) * masterVolume);
    }

    private static AudioClip BuildWhoosh()
    {
        int n = Mathf.RoundToInt(SampleRate * 0.32f);
        var data = new float[n];
        var rnd = new System.Random(101);
        float low = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            // Cutoff sweeps open then shut, which is what makes it read as passing by.
            float cutoff = Mathf.Lerp(0.03f, 0.5f, Mathf.Sin(t * Mathf.PI));
            low += (noise - low) * cutoff;

            float envelope = Mathf.Sin(t * Mathf.PI);
            data[i] = low * envelope * envelope;
        }

        return Finish("Whoosh", data, 0.9f);
    }

    private static AudioClip BuildThud()
    {
        int n = Mathf.RoundToInt(SampleRate * 0.45f);
        var data = new float[n];
        var rnd = new System.Random(202);
        float phase = 0f;
        float low = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float frequency = Mathf.Lerp(160f, 42f, Mathf.Sqrt(t));
            phase += frequency / SampleRate * 2f * Mathf.PI;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.25f;

            float body = Mathf.Sin(phase) * Mathf.Exp(-5f * t);
            float crack = low * Mathf.Exp(-40f * t) * 0.6f;
            data[i] = body * 0.85f + crack;
        }

        return Finish("Thud", data, 0.95f);
    }

    private static AudioClip BuildCombo()
    {
        int n = Mathf.RoundToInt(SampleRate * 0.18f);
        var data = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            phase += 780f / SampleRate * 2f * Mathf.PI;
            data[i] = (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.25f) * Mathf.Exp(-16f * t);
        }

        return Finish("ComboBlip", data, 0.8f);
    }

    private static AudioClip BuildWind()
    {
        int n = Mathf.RoundToInt(SampleRate * 3f);
        int fade = SampleRate / 4;
        var data = new float[n];
        var rnd = new System.Random(303);
        float a = 0f, b = 0f;

        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            a += (noise - a) * 0.05f;
            b += (a - b) * 0.05f;
            data[i] = b * 0.8f + a * 0.25f;
        }
        Normalize(data, 0.85f);

        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
        }

        int length = n - fade;
        var loop = new float[length];
        System.Array.Copy(data, loop, length);

        var clip = AudioClip.Create("Wind", length, 1, SampleRate, false);
        clip.SetData(loop, 0);
        return clip;
    }

    private static AudioClip Finish(string name, float[] data, float peak)
    {
        Normalize(data, peak);
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
        if (max < 1e-5f) return;

        float scale = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= scale;
    }
}
