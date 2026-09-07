using System.Collections.Generic;
using UnityEngine;

// A procedural fir: a faceted trunk prism topped by stacked cone skirts. Trunk and
// foliage share one mesh and one material - their vertices simply sample different bands
// of the shared two-tone ramp texture through uv.y, which avoids needing submeshes or a
// vertex-colour shader.
//
// Collision is a plain CapsuleCollider rather than the mesh: it is cheap, needs no
// cooking when the tree is rebuilt on recycle, and being slightly slimmer than the
// foliage makes near misses feel fair.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tree : MonoBehaviour
{
    private const float TrunkUV = 0.08f;
    private const float FoliageUV = 0.85f;

    private Mesh mesh;
    private CapsuleCollider capsule;
    private float height;

    public float Height => height;

    public void Build(int seed, float treeHeight, float width, int sides, int layers)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "TreeMesh" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        height = Mathf.Max(treeHeight, 0.5f);
        sides = Mathf.Max(sides, 3);
        layers = Mathf.Max(layers, 1);

        var rand = new System.Random(seed);
        float lean = (float)rand.NextDouble() * 0.06f;

        var vertices = new List<Vector3>(sides * (layers + 2) + layers);
        var uvs = new List<Vector2>(vertices.Capacity);
        var triangles = new List<int>(sides * (layers + 1) * 6);

        float trunkHeight = height * 0.34f;
        float trunkRadius = width * 0.16f;
        AddPrism(vertices, uvs, triangles, sides, trunkRadius, 0f, trunkHeight, TrunkUV);

        for (int i = 0; i < layers; i++)
        {
            float frac = layers > 1 ? i / (float)(layers - 1) : 0f;
            float baseY = height * Mathf.Lerp(0.22f, 0.58f, frac);
            float apexY = i == layers - 1 ? height : baseY + height * 0.42f;
            float radius = width * (1f - 0.5f * frac) * (1f + lean);
            AddCone(vertices, uvs, triangles, sides, radius, baseY, apexY, FoliageUV);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = width * 0.5f;
        capsule.height = Mathf.Max(height, capsule.radius * 2f);
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
    }

    private static void AddPrism(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
                                 int sides, float radius, float bottom, float top, float uv)
    {
        int start = vertices.Count;
        for (int s = 0; s < sides; s++)
        {
            float angle = 2f * Mathf.PI * s / sides;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, bottom, z));
            vertices.Add(new Vector3(x, top, z));
            uvs.Add(new Vector2(0.5f, uv));
            uvs.Add(new Vector2(0.5f, uv));
        }

        for (int s = 0; s < sides; s++)
        {
            int a = start + s * 2;
            int c = a + 1;
            int b = start + ((s + 1) % sides) * 2;
            int d = b + 1;
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }
    }

    private static void AddCone(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
                                int sides, float radius, float bottom, float apexY, float uv)
    {
        int start = vertices.Count;
        for (int s = 0; s < sides; s++)
        {
            float angle = 2f * Mathf.PI * s / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, bottom, Mathf.Sin(angle) * radius));
            uvs.Add(new Vector2(0.5f, uv));
        }

        int apex = vertices.Count;
        vertices.Add(new Vector3(0f, apexY, 0f));
        uvs.Add(new Vector2(0.5f, uv));

        for (int s = 0; s < sides; s++)
        {
            int a = start + s;
            int b = start + (s + 1) % sides;
            triangles.Add(a); triangles.Add(apex); triangles.Add(b);
        }
    }
}
