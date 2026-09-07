using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's boulder. It is a real Rigidbody that rides the planet's displaced terrain
// and gets shoved around by the slopes rolling underneath it. The player fights that with
// "steering": the input yaws the whole planet (repositioning the incoming trees) and also
// nudges the boulder directly for close-quarters dodging.
//
// A tether keeps the boulder in the play zone: stiff along the roll-travel axis (world Z)
// so it can never drift off the front or back of the cap, and soft across the track
// (world X) so slope hits genuinely move it and the player has to correct. Gravity is
// applied toward the planet centre so the boulder hugs the surface wherever it ends up.
//
// The terrain contact normal is also what drives the run's speed: the component of that
// normal along the travel direction says whether the boulder is facing up or down a
// slope, and that scales the planet's roll. Trees are the only lethal thing out there -
// a larger trigger sphere added at runtime scores the ones that are dodged cleanly.
[RequireComponent(typeof(Rigidbody))]
public class BoulderController : MonoBehaviour
{
    [SerializeField] private RollingPlanet planet;
    [SerializeField] private bool invert;

    [Header("Physics")]
    [SerializeField] private float gravityAccel = 26f;
    [SerializeField] private float steerAssistAccel = 8f;
    [SerializeField] private float steerAssistSpeedGain = 0.4f;
    [SerializeField] private float linearDamping = 0.5f;
    [SerializeField] private float angularDamping = 1.5f;

    [Header("Play-zone tether")]
    [SerializeField] private float depthStiffness = 55f;
    [SerializeField] private float depthDamping = 12f;
    [SerializeField] private float lateralStiffness = 5f;
    [SerializeField] private float lateralDamping = 3f;
    [SerializeField] private float maxStray = 16f;

    [Header("Slope drive")]
    [SerializeField] private float slopeSpeedInfluence = 1f;
    [SerializeField] private float slopeResponse = 3f;

    [Header("Scoring sense")]
    [SerializeField] private float nearMissRadius = 1.9f;
    [SerializeField] private float terrainImpactThreshold = 14f;
    [SerializeField] private float maxTerrainSeverity = 0.35f;

    private readonly HashSet<Tree> nearby = new HashSet<Tree>();
    private readonly HashSet<Tree> touched = new HashSet<Tree>();

    private Rigidbody body;
    private Vector3 spawnPosition;
    private Vector3 groundNormal = Vector3.up;
    private bool grounded;
    private float smoothedSlope;
    private float steer;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;


        // Configure the authored solid collider before adding the sensor, so the sensor
        // never gets picked up as the physical one.
        var solid = GetComponent<Collider>();
        if (solid != null)
        {
            solid.sharedMaterial = new PhysicsMaterial("BoulderPhysics")
            {
                dynamicFriction = 0.25f,
                staticFriction = 0.25f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        var sensor = gameObject.AddComponent<SphereCollider>();
        sensor.isTrigger = true;
        sensor.radius = nearMissRadius;

        if (planet == null) planet = FindAnyObjectByType<RollingPlanet>();
        spawnPosition = transform.position;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        steer = gm != null && gm.IsGameOver ? 0f : ReadHorizontal();
        if (invert) steer = -steer;
        if (planet != null) planet.SetSteerInput(steer);
    }

    private void FixedUpdate()
    {
        Vector3 center = planet != null ? planet.transform.position : Vector3.zero;

        Vector3 toCenter = center - body.position;
        body.AddForce(toCenter.normalized * gravityAccel, ForceMode.Acceleration);

        Vector3 offset = body.position - center;
        Vector3 velocity = body.linearVelocity;
        float lateralForce = -offset.x * lateralStiffness - velocity.x * lateralDamping;
        float depthForce = -offset.z * depthStiffness - velocity.z * depthDamping;
        body.AddForce(new Vector3(lateralForce, 0f, depthForce), ForceMode.Acceleration);

        // Keep the boulder as responsive at full speed as it is at the start of a run.
        float difficulty = GameManager.Instance != null ? GameManager.Instance.Difficulty01 : 0f;
        float assist = steerAssistAccel * PlayerProgress.GripMultiplier
                       * (1f + steerAssistSpeedGain * difficulty);
        body.AddForce(Vector3.right * (steer * assist), ForceMode.Acceleration);

        UpdateSlopeDrive();

        Vector3 flatOffset = new Vector3(offset.x, 0f, offset.z);
        if (flatOffset.magnitude > maxStray || body.position.y < center.y)
            Respawn();
    }

    // The surface scrolls toward -Z under the boulder, so the boulder is effectively
    // running toward +Z. A ground normal leaning toward +Z means the slope falls away
    // ahead (downhill, speed up); leaning toward -Z means it climbs (uphill, slow down).
    private void UpdateSlopeDrive()
    {
        float targetSlope = grounded ? Vector3.Dot(groundNormal, Vector3.forward) : 0f;
        grounded = false;

        smoothedSlope = Mathf.Lerp(smoothedSlope, targetSlope, slopeResponse * Time.fixedDeltaTime);
        if (planet != null) planet.SetSpeedModifier(1f + smoothedSlope * slopeSpeedInfluence);
    }

    private void OnTriggerEnter(Collider other)
    {
        Tree tree = other.GetComponent<Tree>();
        if (tree != null) nearby.Add(tree);
    }

    private void OnTriggerExit(Collider other)
    {
        Tree tree = other.GetComponent<Tree>();
        if (tree == null) return;

        bool hit = touched.Remove(tree);
        if (nearby.Remove(tree) && !hit && GameManager.Instance != null)
            GameManager.Instance.RegisterNearMiss();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Tree tree = collision.collider.GetComponent<Tree>();
        if (tree != null)
        {
            touched.Add(tree);
            if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
            return;
        }

        // Terrain is never fatal. Only a genuinely heavy landing registers at all, and its
        // severity ramps up from zero at the threshold, so ordinary rolling over ridges
        // produces no feedback rather than a full crash reaction.
        float impact = collision.impulse.magnitude;
        if (impact < terrainImpactThreshold || GameManager.Instance == null) return;

        float severity = Mathf.InverseLerp(terrainImpactThreshold, terrainImpactThreshold * 3f, impact);
        GameManager.Instance.RegisterImpact(severity * maxTerrainSeverity);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.GetComponent<Tree>() != null) return;

        Vector3 sum = Vector3.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++) sum += collision.GetContact(i).normal;

        if (sum.sqrMagnitude < 1e-6f) return;
        groundNormal = sum.normalized;
        grounded = true;
    }

    private void Respawn()
    {
        body.position = spawnPosition;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        nearby.Clear();
        touched.Clear();
    }

    private float ReadHorizontal()
    {
        float value = 0f;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value += 1f;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && Mathf.Abs(value) < 0.01f)
            value = gamepad.leftStick.x.ReadValue();

        return Mathf.Clamp(value, -1f, 1f);
    }
}
