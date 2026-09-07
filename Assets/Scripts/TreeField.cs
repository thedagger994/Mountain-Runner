using System.Collections.Generic;
using UnityEngine;

// Scatters the run's obstacles - trees - across the planet and recycles them endlessly.
//
// Trees are parented to the planet, so its roll carries them through the play area on
// its own. Progress is the signed angle of a tree around the roll axis measured from the
// fixed world apex where the player sits (0 = under the boulder). The roll moves that
// angle at a rate whose sign follows RollingPlanet.AutoRollSpeed; a tree is planted
// spawnLead degrees upstream and recycled once it is despawnTrail degrees downstream,
// both measured along the travel direction so this works whichever way the planet rolls.
// Recycling happens on the far side of the planet, hidden by its own bulge.
//
// Lateral placement is rejection-sampled: a candidate is rejected if it lands within
// minLateralGap of another tree in the same travel "row", which guarantees the player
// always has a reachable lane instead of an unbroken wall.
//
// Each tree is planted at the exact surface height reported by PlanetTerrain, so they sit
// on the mountains rather than floating over the troughs.
public class TreeField : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private Transform planet;
    [SerializeField] private PlanetTerrain terrain;
    [SerializeField] private Vector3 rollAxis = Vector3.right;
    [SerializeField] private int treeCount = 26;
    [SerializeField] private float spawnLead = 140f;
    [SerializeField] private float despawnTrail = 160f;

    [Header("Spacing")]
    [SerializeField] private float lateralSpread = 34f;
    [SerializeField] private float minLateralGap = 18f;
    [SerializeField] private float minRowGap = 14f;
    [SerializeField] private float spawnDepthJitter = 50f;
    [SerializeField] private float initialApexClear = 20f;

    [Header("Shape")]
    [SerializeField] private Vector2 heightRange = new Vector2(1.6f, 3.1f);
    [SerializeField] private Vector2 widthRange = new Vector2(0.55f, 1.0f);
    [SerializeField] private int sides = 7;
    [SerializeField] private int layers = 3;
    [SerializeField] private float sinkDepth = 0.15f;
    [SerializeField] private int seed = 991;

    [Header("Colour")]
    [SerializeField] private Color trunkColor = new Color(0.26f, 0.17f, 0.11f);
    [SerializeField] private Color foliageLowColor = new Color(0.08f, 0.22f, 0.16f);
    [SerializeField] private Color foliageHighColor = new Color(0.16f, 0.36f, 0.24f);

    private readonly List<Tree> trees = new List<Tree>();
    private float[] lateralDeg;
    private System.Random rng;
    private Material sharedMaterial;

    private float travelSign = 1f;
    private float spawnAngle;
    private float despawnAngle;

    private void Reset()
    {
        planet = transform;
        terrain = GetComponent<PlanetTerrain>();
    }

    private void Awake()
    {
        if (planet == null) planet = transform;
        if (terrain == null) terrain = planet.GetComponent<PlanetTerrain>();
        if (terrain != null) terrain.EnsureBuilt();

        var roller = planet.GetComponent<RollingPlanet>();
        if (roller != null)
        {
            rollAxis = roller.RollAxis;
            if (roller.AutoRollSpeed != 0f) travelSign = Mathf.Sign(roller.AutoRollSpeed);
        }

        spawnAngle = -travelSign * spawnLead;
        despawnAngle = travelSign * despawnTrail;

        rng = new System.Random(seed);
        sharedMaterial = BuildMaterial();
        lateralDeg = new float[treeCount];

        for (int i = 0; i < treeCount; i++)
        {
            CreateTree(i);
            float depth = Mathf.Lerp(spawnAngle, despawnAngle, (i + 0.5f) / treeCount);
            // Never plant a tree straight onto the player at startup.
            if (Mathf.Abs(depth) < initialApexClear) depth = spawnAngle;
            PlaceTree(i, depth, BestLateral(i, depth), freshShape: true);
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < trees.Count; i++)
        {
            if (travelSign * (CurrentAngle(trees[i].transform) - despawnAngle) >= 0f)
            {
                float depth = spawnAngle - travelSign * (float)rng.NextDouble() * spawnDepthJitter;
                PlaceTree(i, depth, BestLateral(i, depth), freshShape: true);
            }
        }
    }

    private void CreateTree(int index)
    {
        var go = new GameObject("Tree_" + index);
        go.transform.SetParent(planet, false);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = sharedMaterial;
        trees.Add(go.AddComponent<Tree>());
    }

    private void PlaceTree(int index, float depth, float lateral, bool freshShape)
    {
        Tree tree = trees[index];
        lateralDeg[index] = lateral;

        if (freshShape)
        {
            float height = Mathf.Lerp(heightRange.x, heightRange.y, (float)rng.NextDouble());
            float width = Mathf.Lerp(widthRange.x, widthRange.y, (float)rng.NextDouble());
            tree.Build(rng.Next(), height, width, sides, layers);
        }

        // Surface direction: start at the apex (world up), tilt "lateral" degrees across
        // the track, then swing "depth" degrees along the roll direction.
        Vector3 axisW = rollAxis.sqrMagnitude > 1e-6f ? rollAxis.normalized : Vector3.right;
        Vector3 sideAxisW = Vector3.Cross(axisW, Vector3.up).normalized;
        Vector3 worldDir = Quaternion.AngleAxis(depth, axisW)
                           * (Quaternion.AngleAxis(lateral, sideAxisW) * Vector3.up);

        Vector3 localDir = (Quaternion.Inverse(planet.rotation) * worldDir).normalized;
        float surface = terrain != null ? terrain.SampleHeight(localDir) : localDir.magnitude;

        tree.transform.localPosition = localDir * (surface - sinkDepth);
        tree.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDir)
                                       * Quaternion.AngleAxis((float)rng.NextDouble() * 360f, Vector3.up);
    }

    private float BestLateral(int index, float depth)
    {
        float best = 0f;
        float bestGap = float.NegativeInfinity;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float candidate = ((float)rng.NextDouble() * 2f - 1f) * lateralSpread;
            float gap = NearestLateralGap(index, depth, candidate);
            if (gap >= minLateralGap) return candidate;
            if (gap > bestGap) { bestGap = gap; best = candidate; }
        }
        return best;
    }

    private float NearestLateralGap(int index, float depth, float lateral)
    {
        float nearest = float.PositiveInfinity;
        for (int j = 0; j < trees.Count; j++)
        {
            if (j == index) continue;
            float otherDepth = CurrentAngle(trees[j].transform);
            if (Mathf.Abs(Mathf.DeltaAngle(otherDepth, depth)) > minRowGap) continue;
            nearest = Mathf.Min(nearest, Mathf.Abs(lateral - lateralDeg[j]));
        }
        return nearest;
    }

    private float CurrentAngle(Transform child)
    {
        Vector3 axisW = rollAxis.sqrMagnitude > 1e-6f ? rollAxis.normalized : Vector3.right;
        Vector3 planar = Vector3.ProjectOnPlane(child.position - planet.position, axisW);
        if (planar.sqrMagnitude < 1e-6f) return 0f;
        return Vector3.SignedAngle(Vector3.up, planar.normalized, axisW);
    }

    // A two-band ramp: the lower rows are trunk brown, the upper rows foliage green, with
    // a hard step between them so uv.y picks one or the other cleanly.
    private Material BuildMaterial()
    {
        const int steps = 32;
        var tex = new Texture2D(1, steps, TextureFormat.RGBA32, false)
        {
            name = "TreeRamp",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        for (int y = 0; y < steps; y++)
        {
            float t = y / (float)(steps - 1);
            Color c = t < 0.4f
                ? trunkColor
                : Color.Lerp(foliageLowColor, foliageHighColor, Mathf.InverseLerp(0.4f, 1f, t));
            tex.SetPixel(0, y, c);
        }
        tex.Apply();

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TreeMaterial" };
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.08f);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }
}
