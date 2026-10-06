using System.Collections.Generic;
using UnityEngine;

namespace Vaanam.World
{
    /// <summary>
    /// All the materials the procedural world uses, in one asset, so an artist
    /// (or you) can recolour the whole game from one Inspector.
    ///
    /// The scene builder creates this asset at Assets/_Vaanam/Resources/VaanamPalette
    /// together with URP materials. If it is missing, <see cref="Get"/> builds a
    /// temporary palette in memory so the game still runs.
    /// </summary>
    [CreateAssetMenu(menuName = "Vaanam/Palette")]
    public class VaanamPalette : ScriptableObject
    {
        [Header("Birds")]
        public Material[] birdPrimary;
        public Material[] birdSecondary;
        public Material beak, eye, eyeWhite;

        [Header("Pickups")]
        public Material ringGold, ringPassed, star, kolam;

        [Header("Obstacles")]
        public Material cloud, duckBar, jumpWall;

        [Header("World")]
        public Material ground, groundDark, hill, trunk, leaves, water, house, roof, pot, sugarcane, sun, perch;
        public Material[] kites;

        [Header("Effects")]
        public Material particle;

        private static VaanamPalette _instance;

        public static VaanamPalette Get()
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<VaanamPalette>("VaanamPalette");
            if (_instance == null)
            {
                Debug.LogWarning("[Vaanam] VaanamPalette asset not found in Resources; using runtime fallback. Run Vaanam > Build Main Scene.");
                _instance = CreateInstance<VaanamPalette>();
            }
            _instance.FillMissing();
            return _instance;
        }

        public Material BirdPrimary(int i) => Pick(birdPrimary, i);
        public Material BirdSecondary(int i) => Pick(birdSecondary, i);
        public Material Kite(int i) => Pick(kites, i);
        public Material Beak => beak; public Material Eye => eye; public Material EyeWhite => eyeWhite;
        public Material RingGold => ringGold; public Material RingPassed => ringPassed; public Material Star => star; public Material Kolam => kolam;
        public Material Cloud => cloud; public Material DuckBar => duckBar; public Material JumpWall => jumpWall;
        public Material Ground => ground; public Material GroundDark => groundDark; public Material Hill => hill;
        public Material Trunk => trunk; public Material Leaves => leaves; public Material Water => water;
        public Material House => house; public Material Roof => roof; public Material Pot => pot; public Material Sugarcane => sugarcane;
        public Material Sun => sun; public Material Perch => perch; public Material Particle => particle;

        private static Material Pick(Material[] arr, int i) => arr == null || arr.Length == 0 ? null : arr[Mathf.Abs(i) % arr.Length];

        /// <summary>Default colours. Used by the scene builder AND by the runtime fallback.</summary>
        public static readonly Dictionary<string, Color> DefaultColors = new Dictionary<string, Color>
        {
            { "BirdOrange", new Color(1.00f, 0.52f, 0.12f) }, { "BirdOrangeLight", new Color(1.00f, 0.86f, 0.45f) },
            { "BirdBlue", new Color(0.18f, 0.60f, 0.95f) }, { "BirdBlueLight", new Color(0.70f, 0.90f, 1.00f) },
            { "Beak", new Color(1.00f, 0.80f, 0.20f) }, { "Eye", new Color(0.08f, 0.08f, 0.1f) }, { "EyeWhite", Color.white },
            { "RingGold", new Color(1.00f, 0.78f, 0.15f) }, { "RingPassed", new Color(0.55f, 1.00f, 0.60f) },
            { "Star", new Color(1.00f, 0.92f, 0.25f) }, { "Kolam", new Color(0.98f, 0.98f, 0.95f) },
            { "Cloud", new Color(0.97f, 0.97f, 1.00f) }, { "DuckBar", new Color(0.95f, 0.35f, 0.45f) }, { "JumpWall", new Color(0.55f, 0.40f, 0.75f) },
            { "Ground", new Color(0.45f, 0.75f, 0.35f) }, { "GroundDark", new Color(0.35f, 0.62f, 0.30f) }, { "Hill", new Color(0.40f, 0.68f, 0.45f) },
            { "Trunk", new Color(0.45f, 0.30f, 0.18f) }, { "Leaves", new Color(0.20f, 0.60f, 0.30f) }, { "Water", new Color(0.35f, 0.70f, 0.95f) },
            { "House", new Color(0.98f, 0.90f, 0.75f) }, { "Roof", new Color(0.85f, 0.35f, 0.25f) }, { "Pot", new Color(0.75f, 0.45f, 0.30f) },
            { "Sugarcane", new Color(0.55f, 0.75f, 0.30f) }, { "Sun", new Color(1.00f, 0.85f, 0.40f) }, { "Perch", new Color(0.50f, 0.35f, 0.22f) },
            { "Kite1", new Color(0.95f, 0.25f, 0.35f) }, { "Kite2", new Color(0.25f, 0.75f, 0.45f) }, { "Kite3", new Color(0.95f, 0.65f, 0.15f) },
            { "Particle", Color.white },
        };

        /// <summary>Find a URP-friendly shader at runtime. Falls back gracefully.</summary>
        public static Shader LitShader()
        {
            return Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Legacy Shaders/Diffuse");
        }

        public static Material MakeMaterial(string name, Color color, bool emissive = false)
        {
            var m = new Material(LitShader()) { name = name, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.6f);
            }
            return m;
        }

        private void FillMissing()
        {
            Material F(Material m, string key, bool emissive = false) => m != null ? m : MakeMaterial(key, DefaultColors[key], emissive);
            if (birdPrimary == null || birdPrimary.Length == 0) birdPrimary = new[] { MakeMaterial("BirdOrange", DefaultColors["BirdOrange"]), MakeMaterial("BirdBlue", DefaultColors["BirdBlue"]) };
            if (birdSecondary == null || birdSecondary.Length == 0) birdSecondary = new[] { MakeMaterial("BirdOrangeLight", DefaultColors["BirdOrangeLight"]), MakeMaterial("BirdBlueLight", DefaultColors["BirdBlueLight"]) };
            beak = F(beak, "Beak"); eye = F(eye, "Eye"); eyeWhite = F(eyeWhite, "EyeWhite");
            ringGold = F(ringGold, "RingGold", true); ringPassed = F(ringPassed, "RingPassed", true); star = F(star, "Star", true); kolam = F(kolam, "Kolam", true);
            cloud = F(cloud, "Cloud"); duckBar = F(duckBar, "DuckBar"); jumpWall = F(jumpWall, "JumpWall");
            ground = F(ground, "Ground"); groundDark = F(groundDark, "GroundDark"); hill = F(hill, "Hill"); trunk = F(trunk, "Trunk"); leaves = F(leaves, "Leaves");
            water = F(water, "Water"); house = F(house, "House"); roof = F(roof, "Roof"); pot = F(pot, "Pot"); sugarcane = F(sugarcane, "Sugarcane");
            sun = F(sun, "Sun", true); perch = F(perch, "Perch");
            if (kites == null || kites.Length == 0) kites = new[] { MakeMaterial("Kite1", DefaultColors["Kite1"]), MakeMaterial("Kite2", DefaultColors["Kite2"]), MakeMaterial("Kite3", DefaultColors["Kite3"]) };
            if (particle == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                particle = new Material(sh) { name = "Particle" };
            }
        }
    }
}
