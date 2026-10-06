using UnityEngine;

namespace Vaanam.World
{
    /// <summary>
    /// Decorative things beside the corridor, built from primitives per theme.
    /// Pure decoration: no colliders. Trees, hills and houses for the fields;
    /// floating puffs and islands for the cloud kingdom; sugarcane, pots, kolam
    /// and kites for Pongal.
    /// </summary>
    public static class Scenery
    {
        public static void Populate(Transform parent, float z, float segmentLength, SceneryTheme theme, float corridorHalfWidth, System.Random rnd)
        {
            var palette = VaanamPalette.Get();
            int count = 2 + rnd.Next(3);
            for (int i = 0; i < count; i++)
            {
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                float x = side * (corridorHalfWidth + 8f + (float)rnd.NextDouble() * 40f);
                float zz = z + (float)rnd.NextDouble() * segmentLength;
                Vector3 pos = new Vector3(x, 0f, zz);
                switch (theme)
                {
                    case SceneryTheme.Fields: Fields(parent, pos, rnd, palette); break;
                    case SceneryTheme.Clouds: Clouds(parent, pos, rnd, palette); break;
                    case SceneryTheme.Pongal: Pongal(parent, pos, rnd, palette); break;
                }
            }
            // Distant hills on both sides for every theme, so there is always a horizon.
            if (rnd.NextDouble() < 0.5)
            {
                float side = rnd.Next(2) == 0 ? -1f : 1f;
                float s = 40f + (float)rnd.NextDouble() * 60f;
                MeshBuilders.Prim(PrimitiveType.Sphere, "Hill", parent, new Vector3(side * (corridorHalfWidth + 70f + (float)rnd.NextDouble() * 60f), -s * 0.35f, z + (float)rnd.NextDouble() * segmentLength), new Vector3(s, s * 0.55f, s), palette.Hill);
            }
        }

        private static void Fields(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            double r = rnd.NextDouble();
            if (r < 0.45) Tree(parent, pos, rnd, p);
            else if (r < 0.65) Palm(parent, pos, rnd, p);
            else if (r < 0.85) House(parent, pos, rnd, p, kolam: false);
            else MeshBuilders.Prim(PrimitiveType.Cylinder, "Pond", parent, pos + Vector3.up * 0.02f, new Vector3(8f + (float)rnd.NextDouble() * 6f, 0.02f, 6f + (float)rnd.NextDouble() * 4f), p.Water);
        }

        private static void Clouds(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            float y = 3f + (float)rnd.NextDouble() * 18f;
            var root = new GameObject("CloudDeco").transform; root.SetParent(parent, false); root.position = pos + Vector3.up * y;
            if (rnd.NextDouble() < 0.3)
            {
                // Floating island: a grassy disc with a tiny tree.
                MeshBuilders.Prim(PrimitiveType.Cylinder, "Island", root, Vector3.zero, new Vector3(7f, 0.6f, 7f), p.Ground);
                MeshBuilders.Prim(PrimitiveType.Sphere, "IslandBottom", root, Vector3.down * 1.4f, new Vector3(6f, 2.8f, 6f), p.Trunk);
                Tree(root, Vector3.up * 0.6f, rnd, p);
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 o = new Vector3((float)rnd.NextDouble() * 5f - 2.5f, (float)rnd.NextDouble() * 1.5f - 0.75f, (float)rnd.NextDouble() * 2.5f - 1.25f);
                    MeshBuilders.Prim(PrimitiveType.Sphere, "Puff", root, o, Vector3.one * (2.2f + (float)rnd.NextDouble() * 2f), p.Cloud);
                }
            }
        }

        private static void Pongal(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            double r = rnd.NextDouble();
            if (r < 0.35) Sugarcane(parent, pos, rnd, p);
            else if (r < 0.6) House(parent, pos, rnd, p, kolam: true);
            else if (r < 0.8) Pot(parent, pos, p);
            else Palm(parent, pos, rnd, p);
        }

        public static void Tree(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            float h = 3f + (float)rnd.NextDouble() * 3f;
            var root = new GameObject("Tree").transform; root.SetParent(parent, false); root.position = pos;
            MeshBuilders.Prim(PrimitiveType.Cylinder, "Trunk", root, Vector3.up * (h / 2f), new Vector3(0.6f, h / 2f, 0.6f), p.Trunk);
            MeshBuilders.Prim(PrimitiveType.Sphere, "Leaves", root, Vector3.up * (h + 1.2f), Vector3.one * (3f + (float)rnd.NextDouble() * 2f), p.Leaves);
        }

        public static void Palm(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            float h = 6f + (float)rnd.NextDouble() * 4f;
            var root = new GameObject("Palm").transform; root.SetParent(parent, false); root.position = pos;
            MeshBuilders.Prim(PrimitiveType.Cylinder, "Trunk", root, Vector3.up * (h / 2f), new Vector3(0.45f, h / 2f, 0.45f), p.Trunk);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f;
                var leaf = MeshBuilders.Prim(PrimitiveType.Cube, "Frond", root, Vector3.up * h, new Vector3(0.8f, 0.08f, 3.5f), p.Leaves);
                leaf.localRotation = Quaternion.Euler(-25f, a, 0f);
                leaf.localPosition += leaf.forward * 1.5f;
            }
        }

        public static void House(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p, bool kolam)
        {
            var root = new GameObject("House").transform; root.SetParent(parent, false); root.position = pos;
            root.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            MeshBuilders.Prim(PrimitiveType.Cube, "Walls", root, Vector3.up * 1.6f, new Vector3(5f, 3.2f, 5f), p.House);
            var roof = MeshBuilders.MeshObject("Roof", MeshBuilders.Cone(4.2f, 2.4f, 4), p.Roof, root, Vector3.up * 3.2f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            if (kolam)
            {
                // A kolam drawn in front of the door: concentric white rings with dots.
                Vector3 front = Vector3.forward * 4.5f + Vector3.up * 0.03f;
                var k1 = MeshBuilders.MeshObject("Kolam", MeshBuilders.Torus(1.4f, 0.06f, 24, 6), p.Kolam, root, front);
                k1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var k2 = MeshBuilders.MeshObject("Kolam2", MeshBuilders.Torus(0.7f, 0.06f, 16, 6), p.Kolam, root, front);
                k2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    MeshBuilders.Prim(PrimitiveType.Sphere, "Dot", root, front + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.05f, Vector3.one * 0.14f, p.Kolam);
                }
            }
        }

        public static void Sugarcane(Transform parent, Vector3 pos, System.Random rnd, VaanamPalette p)
        {
            var root = new GameObject("Sugarcane").transform; root.SetParent(parent, false); root.position = pos;
            int n = 5 + rnd.Next(6);
            for (int i = 0; i < n; i++)
            {
                float h = 3.5f + (float)rnd.NextDouble() * 1.5f;
                Vector3 o = new Vector3((float)rnd.NextDouble() * 4f - 2f, h / 2f, (float)rnd.NextDouble() * 4f - 2f);
                MeshBuilders.Prim(PrimitiveType.Cylinder, "Cane", root, o, new Vector3(0.18f, h / 2f, 0.18f), p.Sugarcane, Quaternion.Euler((float)rnd.NextDouble() * 8f - 4f, 0f, (float)rnd.NextDouble() * 8f - 4f));
                MeshBuilders.Prim(PrimitiveType.Cube, "Leaf", root, o + Vector3.up * (h / 2f), new Vector3(0.15f, 1.6f, 0.05f), p.Leaves, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 30f));
            }
        }

        public static void Pot(Transform parent, Vector3 pos, VaanamPalette p)
        {
            var root = new GameObject("PongalPot").transform; root.SetParent(parent, false); root.position = pos;
            // Three bricks, a pot, and a boiling-over ring of white.
            MeshBuilders.Prim(PrimitiveType.Cube, "Brick", root, new Vector3(-0.8f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.9f), p.Roof);
            MeshBuilders.Prim(PrimitiveType.Cube, "Brick", root, new Vector3(0.8f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.9f), p.Roof);
            MeshBuilders.Prim(PrimitiveType.Cube, "Brick", root, new Vector3(0f, 0.4f, 0.8f), new Vector3(0.9f, 0.8f, 0.9f), p.Roof);
            MeshBuilders.Prim(PrimitiveType.Sphere, "Pot", root, Vector3.up * 1.8f, new Vector3(2.4f, 2.2f, 2.4f), p.Pot);
            MeshBuilders.Prim(PrimitiveType.Cylinder, "Neck", root, Vector3.up * 2.9f, new Vector3(1.6f, 0.2f, 1.6f), p.Pot);
            var foam = MeshBuilders.MeshObject("Foam", MeshBuilders.Torus(0.8f, 0.25f, 16, 8), p.Kolam, root, Vector3.up * 3.15f);
            foam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Sugarcane tied to the pot, as on Pongal day.
            MeshBuilders.Prim(PrimitiveType.Cylinder, "Cane", root, new Vector3(1.3f, 2.5f, 0f), new Vector3(0.15f, 2.5f, 0.15f), p.Sugarcane, Quaternion.Euler(0f, 0f, -12f));
        }

        /// <summary>A kite on a string that sways in the wind. Decorative only.</summary>
        public static Transform Kite(Transform parent, Vector3 pos, int colorIndex, VaanamPalette p)
        {
            var root = new GameObject("Kite").transform; root.SetParent(parent, false); root.position = pos;
            var kite = MeshBuilders.MeshObject("Diamond", MeshBuilders.Kite(2.2f, 3f, 0.05f), p.Kite(colorIndex), root, Vector3.zero);
            kite.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            MeshBuilders.Prim(PrimitiveType.Cube, "Tail", root, new Vector3(0f, -2.4f, -0.3f), new Vector3(0.12f, 2.4f, 0.02f), p.Kolam, Quaternion.Euler(15f, 0f, 0f));
            MeshBuilders.Prim(PrimitiveType.Cube, "String", root, new Vector3(0f, -pos.y / 2f - 1f, -1f), new Vector3(0.03f, pos.y / 2f, 0.03f), p.Kolam);
            root.gameObject.AddComponent<Sway>();
            return root;
        }

        /// <summary>Little sway animation for kites.</summary>
        public class Sway : MonoBehaviour
        {
            private float _t = 0f;
            private Vector3 _origin;
            private void Start() { _origin = transform.position; _t = Random.value * 10f; }
            private void Update()
            {
                _t += Time.deltaTime;
                transform.position = _origin + new Vector3(Mathf.Sin(_t * 0.8f) * 1.2f, Mathf.Sin(_t * 1.3f) * 0.6f, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 0.8f) * 12f);
            }
        }
    }
}
