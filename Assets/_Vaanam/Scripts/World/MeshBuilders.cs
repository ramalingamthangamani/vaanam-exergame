using System.Collections.Generic;
using UnityEngine;

namespace Vaanam.World
{
    /// <summary>
    /// Builds the few shapes Unity has no primitive for: a ring (torus), a
    /// five-point star, a kite diamond and a cone. Meshes are cached so each is
    /// generated once per run.
    /// </summary>
    public static class MeshBuilders
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh Torus(float radius = 3f, float tube = 0.3f, int segments = 36, int sides = 12)
        {
            string key = $"torus_{radius}_{tube}_{segments}_{sides}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                    verts.Add(center + n * tube); norms.Add(n); uvs.Add(new Vector2(i / (float)segments, j / (float)sides));
                }
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            return Store(key, verts, norms, uvs, tris);
        }

        public static Mesh Star(float outer = 1f, float inner = 0.45f, float depth = 0.25f, int points = 5)
        {
            string key = $"star_{outer}_{inner}_{depth}_{points}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var ring = new List<Vector2>();
            for (int i = 0; i < points * 2; i++)
            {
                float a = i / (float)(points * 2) * Mathf.PI * 2f + Mathf.PI / 2f;
                float r = i % 2 == 0 ? outer : inner;
                ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return Extrude(key, ring, depth);
        }

        public static Mesh Kite(float width = 1f, float height = 1.4f, float depth = 0.05f)
        {
            string key = $"kite_{width}_{height}_{depth}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var ring = new List<Vector2> { new Vector2(0f, height * 0.6f), new Vector2(width * 0.5f, 0f), new Vector2(0f, -height * 0.4f), new Vector2(-width * 0.5f, 0f) };
            return Extrude(key, ring, depth);
        }

        public static Mesh Cone(float radius = 1f, float height = 2f, int segments = 16)
        {
            string key = $"cone_{radius}_{height}_{segments}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Vector3 tip = new Vector3(0f, height, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 n = Vector3.Cross(p1 - tip, p0 - tip).normalized;
                int b = verts.Count;
                verts.Add(tip); verts.Add(p1); verts.Add(p0);
                norms.Add(n); norms.Add(n); norms.Add(n);
                uvs.Add(Vector2.one); uvs.Add(Vector2.zero); uvs.Add(Vector2.right);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                // base
                int c = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
                norms.Add(Vector3.down); norms.Add(Vector3.down); norms.Add(Vector3.down);
                uvs.Add(Vector2.zero); uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
                tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
            }
            return Store(key, verts, norms, uvs, tris);
        }

        /// <summary>Extrudes a convex-ish 2D outline (fan triangulated) along Z.</summary>
        private static Mesh Extrude(string key, List<Vector2> outline, float depth)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int n = outline.Count; float hz = depth * 0.5f;
            // front and back caps as fans from the centroid
            foreach (float z in new[] { -hz, hz })
            {
                Vector3 normal = z < 0 ? Vector3.back : Vector3.forward;
                int c = verts.Count;
                verts.Add(new Vector3(0f, 0f, z)); norms.Add(normal); uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < n; i++) { verts.Add(new Vector3(outline[i].x, outline[i].y, z)); norms.Add(normal); uvs.Add(outline[i] * 0.5f + Vector2.one * 0.5f); }
                for (int i = 0; i < n; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % n;
                    if (z < 0) { tris.Add(c); tris.Add(a); tris.Add(b); } else { tris.Add(c); tris.Add(b); tris.Add(a); }
                }
            }
            // sides
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = outline[i], p1 = outline[(i + 1) % n];
                Vector2 e = p1 - p0; Vector3 normal = new Vector3(e.y, -e.x, 0f).normalized;
                int b = verts.Count;
                verts.Add(new Vector3(p0.x, p0.y, -hz)); verts.Add(new Vector3(p1.x, p1.y, -hz));
                verts.Add(new Vector3(p1.x, p1.y, hz)); verts.Add(new Vector3(p0.x, p0.y, hz));
                for (int k = 0; k < 4; k++) { norms.Add(normal); uvs.Add(Vector2.zero); }
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            return Store(key, verts, norms, uvs, tris);
        }

        private static Mesh Store(string key, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Creates a GameObject with a mesh renderer for the given mesh. No collider.</summary>
        public static GameObject MeshObject(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Creates a primitive without its collider, parented and placed.</summary>
        public static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
