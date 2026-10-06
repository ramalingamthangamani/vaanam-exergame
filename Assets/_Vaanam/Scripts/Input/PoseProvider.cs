using UnityEngine;

namespace Vaanam.Input
{
    /// <summary>
    /// Single place the game asks: what is the player doing right now?
    /// Picks the best available IPoseSource, smooths it, and turns it into FlightInput.
    ///
    /// Put one of these in the scene with a KeyboardPoseSource (and later a
    /// MediaPipePoseSource) on the same GameObject.
    /// </summary>
    public class PoseProvider : MonoBehaviour
    {
        public static PoseProvider Instance { get; private set; }

        [Header("Calibration (per player, tune in the calibration screen)")]
        [Tooltip("Arm roll in degrees that counts as full bank.")]
        [SerializeField] private float maxRollDegrees = 35f;
        [Tooltip("Degrees of roll ignored around center so slight wobble does nothing.")]
        [SerializeField] private float rollDeadzoneDegrees = 4f;
        [Tooltip("ArmSpreadRatio above which wings count as open.")]
        [SerializeField] private float wingsOpenRatio = 2.2f;
        [Tooltip("ArmsHeight (in shoulder widths) that gives full lift.")]
        [SerializeField] private float fullLiftHeight = 0.9f;

        [Header("Smoothing")]
        [SerializeField] private float filterMinCutoff = 1.0f;
        [SerializeField] private float filterBeta = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool showDebugOverlay = true;

        public FlightInput Current { get; private set; }
        public BodyPose LastPose { get; private set; }
        public IPoseSource ActiveSource { get; private set; }

        private IPoseSource[] _sources;
        private OneEuroFilter _rollFilter, _liftFilter, _spreadFilter;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _sources = GetComponents<IPoseSource>();
            _rollFilter = new OneEuroFilter(filterMinCutoff, filterBeta);
            _liftFilter = new OneEuroFilter(filterMinCutoff, filterBeta);
            _spreadFilter = new OneEuroFilter(filterMinCutoff, filterBeta);
        }

        private void Update()
        {
            ActiveSource = PickSource();
            if (ActiveSource == null || !ActiveSource.TryGetPose(out BodyPose pose))
            {
                Current = new FlightInput { IsTracked = false };
                return;
            }

            LastPose = pose;
            float dt = Time.deltaTime;

            float roll = _rollFilter.Filter(pose.ArmRollDegrees, dt);
            float lift = _liftFilter.Filter(pose.ArmsHeight, dt);
            float spread = _spreadFilter.Filter(pose.ArmSpreadRatio, dt);

            float rollNorm = 0f;
            if (Mathf.Abs(roll) > rollDeadzoneDegrees)
            {
                float sign = Mathf.Sign(roll);
                rollNorm = sign * Mathf.InverseLerp(rollDeadzoneDegrees, maxRollDegrees, Mathf.Abs(roll));
            }

            Current = new FlightInput
            {
                Roll = Mathf.Clamp(rollNorm, -1f, 1f),
                Lift = Mathf.Clamp01(lift / fullLiftHeight),
                WingsOpen = spread >= wingsOpenRatio,
                IsTracked = pose.Confidence > 0.5f
            };
        }

        /// <summary>
        /// Prefer the first ready source that is NOT the keyboard, so the webcam
        /// wins when it works and the keyboard silently takes over when it does not.
        /// </summary>
        private IPoseSource PickSource()
        {
            IPoseSource fallback = null;
            foreach (var s in _sources)
            {
                if (!s.IsReady) continue;
                if (s is KeyboardPoseSource) { fallback = s; continue; }
                return s;
            }
            return fallback;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            var f = Current;
            string src = ActiveSource != null ? ActiveSource.Name : "none";
            GUI.Label(new Rect(10, 10, 400, 20), "Source: " + src + "   Tracked: " + f.IsTracked);
            GUI.Label(new Rect(10, 30, 400, 20),
                "Roll: " + f.Roll.ToString("+0.00;-0.00") +
                "   Lift: " + f.Lift.ToString("0.00") +
                "   Wings: " + (f.WingsOpen ? "OPEN" : "closed"));
        }
    }
}
