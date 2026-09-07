using UnityEngine;
using UnityEngine.Rendering;

// Generates the planet as one continuous displaced sphere, so the mountain range wraps
// the whole world and every peak is joined to its neighbours at the trough.
//
// The sphere's poles are placed on the roll axis (world X) rather than on world up. The
// play area sits on the resulting "equator" - the great circle that scrolls past the
// player - which keeps the mesh resolution even along the direction of travel instead of
// piling every segment into a singularity right under the boulder.
//
// Height comes from ridged fractal noise sampled from the 3D direction vector, which is
// seamless on a sphere by construction (a noise field indexed by latitude/longitude
// would tear at the wrap). Raising the ridge value to a power sharpens the peaks and
// flattens the valleys, which is what connects the range at the troughs.
//
// The same height function is public as SampleHeight so obstacles can be planted exactly
// on the surface without raycasting against the collider.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlanetTerrain : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float radius = 13f;
    [SerializeField] private int segments = 180;
    [SerializeField] private int rings = 90;
    [SerializeField] private float mountainHeight = 4.5f;

    // Noise is sampled from the unit direction vector, so one lattice cell spans roughly
    // radius / frequency world units of arc. At radius 13 the base octave gives ridges
    // about five units apart, and the finest octave stays near the mesh's quad size.
    [Header("Noise")]
    [SerializeField] private float baseFrequency = 2.4f;
    [SerializeField] private int octaves = 4;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private float gain = 0.5f;
    [SerializeField] private float ridgeSharpness = 2f;
    [SerializeField] private int seed = 2024;

    [Header("Altitude colouring")]
    [SerializeField] private float snowStart = 0.62f;
    [SerializeField] private Color lowColor = new Color(0.05f, 0.06f, 0.10f);
    [SerializeField] private Color midColor = new Color(0.40f, 0.43f, 0.52f);
    [SerializeField] private Color highColor = new Color(0.92f, 0.94f, 1.00f);

    [Header("Progression")]
    [SerializeField] private bool driftBiome = true;
    [SerializeField] private float biomeHueShift = 0.3f;
    [SerializeField] private bool driveFog = true;
    [SerializeField] private float fogDensityGain = 0.45f;

    private const int GradientSteps = 128;

    private Mesh mesh;
    private Texture2D gradientTexture;
    private Color baseFogColor;
    private float baseFogDensity;
    private float builtDifficulty = -1f;
    private bool built;

    public float Radius => radius;
    public float MountainHeight => mountainHeight;

    // Radial distance of the surface along a unit direction, in planet-local space.
    public float SampleHeight(Vector3 localDirection) => radius + Displacement(localDirection.normalized);

    public Vector3 SurfacePoint(Vector3 localDirection)
    {
        Vector3 dir = localDirection.normalized;
        return dir * (radius + Displacement(dir));
    }

    private void Awake() => EnsureBuilt();

    // Safe to call from another component's Awake, so build order does not matter.
    public void EnsureBuilt()
    {
        if (built) return;
        built = true;

        baseFogColor = RenderSettings.fogColor;
        baseFogDensity = RenderSettings.fogDensity;

        BuildMesh();
        BuildMaterial();
    }

    private void LateUpdate() => UpdateBiome();

    private void BuildMesh()
    {
        int seg = Mathf.Max(segments, 8);
        int ring = Mathf.Max(rings, 4);

        int vertexCount = (ring + 1) * seg;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        float heightRef = Mathf.Max(mountainHeight, 0.01f);

        for (int r = 0; r <= ring; r++)
        {
            float phi = Mathf.PI * r / ring;
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            for (int s = 0; s < seg; s++)
            {
                float theta = 2f * Mathf.PI * s / seg;
                var dir = new Vector3(cosPhi, sinPhi * Mathf.Cos(theta), sinPhi * Mathf.Sin(theta));

                float displacement = Displacement(dir);
                int i = r * seg + s;
                vertices[i] = dir * (radius + displacement);
                uvs[i] = new Vector2(0.5f, Mathf.Clamp01(displacement / heightRef));
            }
        }

        var triangles = new int[(ring * seg * 2 - seg * 2) * 3];
        int t = 0;
        for (int r = 0; r < ring; r++)
        {
            for (int s = 0; s < seg; s++)
            {
                int s1 = (s + 1) % seg;
                int a = r * seg + s;
                int b = r * seg + s1;
                int c = (r + 1) * seg + s;
                int d = (r + 1) * seg + s1;

                // Both pole rings collapse to a point, so one triangle of each quad there
                // is degenerate and is skipped.
                if (r > 0) { triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; }
                if (r < ring - 1) { triangles[t++] = b; triangles[t++] = c; triangles[t++] = d; }
            }
        }

        mesh = new Mesh { name = "PlanetTerrain" };
        if (vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        var collider = GetComponent<MeshCollider>();
        if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }

    private void BuildMaterial()
    {
        gradientTexture = new Texture2D(1, GradientSteps, TextureFormat.RGBA32, false)
        {
            name = "TerrainAltitudeRamp",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        FillGradient(0f);

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
        {
            name = "PlanetTerrainMaterial"
        };
        material.SetTexture("_BaseMap", gradientTexture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);
        GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void FillGradient(float hueShift)
    {
        Color low = ShiftHue(lowColor, hueShift);
        Color mid = ShiftHue(midColor, hueShift);
        Color high = ShiftHue(highColor, hueShift);

        float cut = Mathf.Clamp01(snowStart);
        for (int y = 0; y < GradientSteps; y++)
        {
            float t = y / (float)(GradientSteps - 1);
            Color c = t < cut
                ? Color.Lerp(low, mid, Mathf.InverseLerp(0f, cut, t))
                : Color.Lerp(mid, high, Mathf.InverseLerp(cut, 1f, t));
            gradientTexture.SetPixel(0, y, c);
        }
        gradientTexture.Apply();
    }

    // Rotates hue while leaving brightness alone, so the ramp keeps its dark -> grey ->
    // white structure and only its tint travels as the run escalates.
    private static Color ShiftHue(Color color, float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return color;

        Color.RGBToHSV(color, out float h, out float s, out float v);
        h = Mathf.Repeat(h + amount, 1f);
        s = Mathf.Clamp01(s * (1f + amount * 2f));
        return Color.HSVToRGB(h, s, v);
    }

    private void UpdateBiome()
    {
        if (gradientTexture == null) return;
        if (!driftBiome && !driveFog) return;

        float difficulty = GameManager.Instance != null ? GameManager.Instance.Difficulty01 : 0f;
        if (Mathf.Abs(difficulty - builtDifficulty) < 0.02f) return;
        builtDifficulty = difficulty;

        float shift = biomeHueShift * difficulty;
        if (driftBiome) FillGradient(shift);

        if (driveFog)
        {
            RenderSettings.fogColor = ShiftHue(baseFogColor, shift);
            RenderSettings.fogDensity = baseFogDensity * (1f + fogDensityGain * difficulty);
        }
    }

    private float Displacement(Vector3 dir)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float sum = 0f;
        float norm = 0f;

        for (int o = 0; o < Mathf.Max(octaves, 1); o++)
        {
            float n = ValueNoise(dir * frequency, seed + o * 131);
            n = 1f - Mathf.Abs(n * 2f - 1f);   // ridge transform: creases where the noise crosses 0.5
            sum += n * amplitude;
            norm += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        float t = norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
        return Mathf.Pow(t, ridgeSharpness) * mountainHeight;
    }

    private static float ValueNoise(Vector3 p, int seed)
    {
        int xi = Mathf.FloorToInt(p.x);
        int yi = Mathf.FloorToInt(p.y);
        int zi = Mathf.FloorToInt(p.z);
        float xf = p.x - xi;
        float yf = p.y - yi;
        float zf = p.z - zi;

        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);
        float w = zf * zf * (3f - 2f * zf);

        float x00 = Mathf.Lerp(Hash(xi, yi, zi, seed), Hash(xi + 1, yi, zi, seed), u);
        float x10 = Mathf.Lerp(Hash(xi, yi + 1, zi, seed), Hash(xi + 1, yi + 1, zi, seed), u);
        float x01 = Mathf.Lerp(Hash(xi, yi, zi + 1, seed), Hash(xi + 1, yi, zi + 1, seed), u);
        float x11 = Mathf.Lerp(Hash(xi, yi + 1, zi + 1, seed), Hash(xi + 1, yi + 1, zi + 1, seed), u);

        return Mathf.Lerp(Mathf.Lerp(x00, x10, v), Mathf.Lerp(x01, x11, v), w);
    }

    private static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFFFF) / 2147483647f;
        }
    }
}
