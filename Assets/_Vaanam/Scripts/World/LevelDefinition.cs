using UnityEngine;

namespace Vaanam.World
{
    public enum SceneryTheme { Fields, Clouds, Pongal }

    /// <summary>
    /// Everything that makes one level different from another, as an asset you
    /// can tune in the Inspector without touching code. The scene builder creates
    /// three of these; add a fourth by right-clicking in the Project window:
    /// Create > Vaanam > Level.
    ///
    /// Levels are short (2 to 3 minutes) because arms held out tire fast.
    /// </summary>
    [CreateAssetMenu(menuName = "Vaanam/Level")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("L10n key for the level name picture, e.g. level_1")]
        public string nameKey = "level_1";
        public SceneryTheme theme = SceneryTheme.Fields;

        [Header("Pace")]
        [Tooltip("How long the level lasts in seconds. 120 to 180 is right for small children.")]
        public float durationSeconds = 150f;
        public float forwardSpeed = 14f;

        [Header("What appears (per 30 m segment)")]
        [Range(0f, 1f)] public float ringChance = 0.8f;
        [Range(0f, 1f)] public float starChance = 0.9f;
        [Range(0, 5)] public int starsPerCluster = 3;
        [Range(0f, 1f)] public float cloudChance = 0f;
        [Range(0f, 1f)] public float duckBarChance = 0f;
        [Range(0f, 1f)] public float jumpWallChance = 0f;
        [Range(0f, 1f)] public float kiteChance = 0f;
        [Tooltip("Use kolam-decorated rings (Pongal theme).")]
        public bool kolamRings = false;

        [Header("Sky")]
        public Color skyTop = new Color(0.35f, 0.65f, 1.0f);
        public Color skyHorizon = new Color(0.85f, 0.93f, 1.0f);
        public Color sunColor = new Color(1.0f, 0.96f, 0.85f);
        [Range(0f, 90f)] public float sunElevation = 50f;
        public Color fogColor = new Color(0.85f, 0.93f, 1.0f);
        public Color groundTint = Color.white;
    }
}
