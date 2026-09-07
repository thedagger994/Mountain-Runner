using System.Collections.Generic;
using UnityEngine;

// Swaps the boulder's smooth primitive sphere for a lumpy, flat-shaded rock. A perfectly
// smooth ball gives no visual cue that it is spinning, so the physics tumbling reads as
// nothing; facets make every rotation obvious. The collider stays a clean sphere, which
// keeps the physics stable and predictable.
[RequireComponent(typeof(MeshFilter))]
public class BoulderVisual : MonoBehaviour
{
    [SerializeField] private int segments = 14;
    [SerializeField] private int rings = 9;
    [SerializeField] private float radius = 0.5f;
    [SerializeField] private float lumpiness = 0.16f;
    [SerializeField] private float noiseScale = 2.2f;
    [SerializeField] private int seed = 7;

    private void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = BuildRock();
    }

    private Mesh BuildRock()
    {
        int seg = Mathf.Max(segments, 4);
        int ring = Mathf.Max(rings, 3);

        var rand = new System.Random(seed);
        float offsetX = (float)rand.NextDouble() * 128f;
        float offsetY = (float)rand.NextDouble() * 128f;

        var smooth = new Vector3[(ring + 1) * seg];
        for (int r = 0; r <= ring; r++)
        {
            float phi = Mathf.PI * r / ring;
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            for (int s = 0; s < seg; s++)
            {
                float theta = 2f * Mathf.PI * s / seg;
                var dir = new Vector3(sinPhi * Mathf.Cos(theta), cosPhi, sinPhi * Mathf.Sin(theta));
                float lump = 1f + Lump(dir, offsetX, offsetY) * lumpiness;
                smooth[r * seg + s] = dir * (radius * lump);
            }
        }

        var indices = new List<int>(ring * seg * 6);
        for (int r = 0; r < ring; r++)
        {
            for (int s = 0; s < seg; s++)
            {
                int s1 = (s + 1) % seg;
                int a = r * seg + s;
                int b = r * seg + s1;
                int c = (r + 1) * seg + s;
                int d = (r + 1) * seg + s1;

                // The pole rings collapse to a single point, so one triangle of each quad
                // there is degenerate; skipping it keeps the flat-shaded normals valid.
                if (r > 0) { indices.Add(a); indices.Add(b); indices.Add(c); }
                if (r < ring - 1) { indices.Add(b); indices.Add(d); indices.Add(c); }
            }
        }

        // Flat shading: give every triangle its own three vertices so RecalculateNormals
        // produces one hard normal per face instead of smoothing across the sphere.
        var vertices = new Vector3[indices.Count];
        var triangles = new int[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            vertices[i] = smooth[indices[i]];
            triangles[i] = i;
        }

        var mesh = new Mesh { name = "BoulderRock" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float Lump(Vector3 dir, float offsetX, float offsetY)
    {
        float a = Mathf.PerlinNoise(offsetX + (dir.x + 1f) * noiseScale, offsetY + (dir.y + 1f) * noiseScale);
        float b = Mathf.PerlinNoise(offsetY + (dir.y + 1f) * noiseScale, offsetX + (dir.z + 1f) * noiseScale);
        return a + b - 1f;
    }
}
