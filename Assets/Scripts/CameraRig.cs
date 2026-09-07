using UnityEngine;

// All of the camera's feel work: it banks into turns, widens its field of view and
// develops a low rumble as the run speeds up, and shakes on impacts. The camera's
// authored local transform is the neutral pose; everything here is layered on top of it.
//
// Runs on unscaled time so shakes still read clearly during impact slow-motion.
public class CameraRig : MonoBehaviour
{
    [SerializeField] private RollingPlanet planet;
    [SerializeField] private Camera targetCamera;

    [Header("Steering")]
    [SerializeField] private float bankAngle = 7f;
    [SerializeField] private float swayDistance = 0.7f;
    [SerializeField] private float responsiveness = 5f;

    [Header("Speed")]
    [SerializeField] private float speedFovBoost = 10f;
    [SerializeField] private float slopeFovBoost = 7f;
    [SerializeField] private float speedRumble = 0.035f;
    [SerializeField] private float speedPullBack = 0.5f;

    [Header("Shake")]
    [SerializeField] private float crashShake = 0.45f;
    [SerializeField] private float nearMissShake = 0.09f;
    [SerializeField] private float shakeDecay = 1.8f;
    [SerializeField] private float shakeFrequency = 22f;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float baseFieldOfView = 60f;
    private float steer;
    private float shake;

    private void Awake()
    {
        homePosition = transform.localPosition;
        homeRotation = transform.localRotation;

        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera != null) baseFieldOfView = targetCamera.fieldOfView;
        if (planet == null) planet = FindAnyObjectByType<RollingPlanet>();
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

    private void OnNearMiss(float combo) => AddShake(nearMissShake);

    private void OnImpact(float severity) => AddShake(crashShake * Mathf.Clamp01(severity));

    public void AddShake(float strength) => shake = Mathf.Max(shake, strength);

    private void LateUpdate()
    {
        float udt = Time.unscaledDeltaTime;
        float difficulty = GameManager.Instance != null ? GameManager.Instance.Difficulty01 : 0f;

        float target = planet != null ? planet.SteerAngle01 : 0f;
        steer = Mathf.Lerp(steer, target, responsiveness * udt);
        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * udt);

        // Smooth (Perlin) shake rather than per-frame random, so it reads as a camera
        // being knocked about instead of pixel noise.
        float amount = shake + speedRumble * difficulty;
        float t = Time.unscaledTime * shakeFrequency;
        Vector3 jitter = amount * new Vector3(
            Mathf.PerlinNoise(t, 0.37f) - 0.5f,
            Mathf.PerlinNoise(0.71f, t) - 0.5f,
            0f) * 2f;

        transform.localRotation = homeRotation
                                  * Quaternion.AngleAxis(-steer * bankAngle + jitter.x * 12f, Vector3.forward);
        transform.localPosition = homePosition
                                  + Vector3.right * (steer * swayDistance + jitter.x)
                                  + Vector3.up * jitter.y
                                  + Vector3.back * (speedPullBack * difficulty);

        if (targetCamera != null)
        {
            float boost = planet != null ? planet.SpeedBoost01 : 0f;
            float fov = baseFieldOfView + speedFovBoost * difficulty + slopeFovBoost * boost;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, fov, 2.5f * udt);
        }
    }
}
