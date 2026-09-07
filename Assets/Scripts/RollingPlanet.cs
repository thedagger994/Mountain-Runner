using UnityEngine;

public class RollingPlanet : MonoBehaviour
{
    [SerializeField] private Vector3 autoRollAxis = Vector3.right;
    [SerializeField] private float autoRollSpeed = 20f;
    [SerializeField] private Vector3 steerAxis = Vector3.up;
    [SerializeField] private float steerSpeed = 90f;
    [SerializeField] private float maxSteerAngle = 75f;
    [SerializeField] private float steerReturnSpeed = 22f; // eases the turn back to centre when input is released; 0 disables
    [SerializeField] private float steerSmoothing = 8f;

    private float steerInput;
    private float smoothedSteer;
    private float steerAngle;   // accumulated player yaw, clamped to +/- maxSteerAngle
    private float appliedSteer; // yaw already written to the transform
    private float slopeModifier = 1f;

    public void SetSteerInput(float input)
    {
        steerInput = Mathf.Clamp(input, -1f, 1f);
    }

    public Vector3 RollAxis => autoRollAxis.sqrMagnitude > 1e-6f ? autoRollAxis.normalized : Vector3.right;

    // Base (authored) roll speed. MountainField only needs its sign, so this stays
    // independent of the run's difficulty ramp.
    public float AutoRollSpeed => autoRollSpeed;

    public float CurrentRollSpeed => autoRollSpeed * SpeedScale * slopeModifier;

    // Set by the boulder from the slope it is sitting on: >1 running downhill, <1 uphill.
    public void SetSpeedModifier(float value) => slopeModifier = Mathf.Clamp(value, 0.3f, 2f);

    // 0 on the flat, rising toward 1 downhill, for cosmetic followers such as the
    // camera's field-of-view kick.
    public float SpeedBoost01 => Mathf.InverseLerp(1f, 1.6f, slopeModifier);

    // Current steer as -1..1 of the allowed range, for cosmetic followers like the camera.
    public float SteerAngle01 =>
        EffectiveMaxSteer > 0f ? Mathf.Clamp(steerAngle / EffectiveMaxSteer, -1f, 1f) : 0f;

    // The Steering upgrade widens the turn limit. Read live rather than folded in at
    // Awake, so a purchase made from the pause menu takes effect straight away.
    private float EffectiveMaxSteer => maxSteerAngle + PlayerProgress.SteerRangeBonus;

    private static float SpeedScale => GameManager.Instance != null ? GameManager.Instance.SpeedMultiplier : 1f;

    private void Update()
    {
        transform.Rotate(autoRollAxis.normalized, CurrentRollSpeed * Time.deltaTime, Space.World);

        // The player steers a bounded yaw rather than an open-ended spin: integrate the
        // (smoothed) input into steerAngle, clamp it, then rotate by only the delta since
        // last frame so a full turn-around is impossible but normal weaving still works.
        smoothedSteer = Mathf.Lerp(smoothedSteer, steerInput, steerSmoothing * Time.deltaTime);
        steerAngle += smoothedSteer * steerSpeed * Time.deltaTime;
        if (steerReturnSpeed > 0f && Mathf.Abs(smoothedSteer) < 0.05f)
            steerAngle = Mathf.MoveTowards(steerAngle, 0f, steerReturnSpeed * Time.deltaTime);
        steerAngle = Mathf.Clamp(steerAngle, -EffectiveMaxSteer, EffectiveMaxSteer);

        transform.Rotate(steerAxis.normalized, steerAngle - appliedSteer, Space.World);
        appliedSteer = steerAngle;

        steerInput = 0f;
    }
}
