using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaanam.Input
{
    /// <summary>
    /// Single place the game asks: what is each player doing right now?
    ///
    /// Picks the best available IPoseSource (webcam over keyboard), smooths the
    /// raw numbers, detects gestures (jump, duck, flap) and turns everything into
    /// one FlightInput per player. Gameplay reads <see cref="Players"/> or
    /// <see cref="Current"/> (player 0) and nothing else.
    ///
    /// Put one of these in the scene with a KeyboardPoseSource (and a
    /// MediaPipePoseSource) on the same GameObject.
    ///
    /// Design rule: forgiving. Deadzones are wide, thresholds are low, and the
    /// first "raise your hands" moment calibrates the thresholds to this child.
    /// </summary>
    public class PoseProvider : MonoBehaviour
    {
        public static PoseProvider Instance { get; private set; }

        [Header("Steering")]
        [Tooltip("Arm roll in degrees that counts as full bank.")]
        [SerializeField] private float maxRollDegrees = 35f;
        [Tooltip("Degrees of roll ignored around center so slight wobble does nothing.")]
        [SerializeField] private float rollDeadzoneDegrees = 5f;
        [Tooltip("Body lean (shoulder widths) ignored around center.")]
        [SerializeField] private float leanDeadzone = 0.08f;
        [Tooltip("Body lean (shoulder widths) that counts as full bank.")]
        [SerializeField] private float leanMax = 0.35f;
        [Tooltip("How much body lean adds to arm roll. 0 = arms only.")]
        [SerializeField, Range(0f, 1f)] private float leanWeight = 0.6f;

        [Header("Wings and lift (defaults, overridden by calibration)")]
        [Tooltip("ArmSpreadRatio above which wings count as open.")]
        [SerializeField] private float wingsOpenRatio = 2.0f;
        [Tooltip("ArmsHeight (in shoulder widths) that gives full lift.")]
        [SerializeField] private float fullLiftHeight = 0.9f;

        [Header("Gestures")]
        [Tooltip("Upward hip speed (shoulder widths per second) that counts as a jump.")]
        [SerializeField] private float jumpHipSpeed = 1.6f;
        [SerializeField] private float jumpCooldown = 0.8f;
        [Tooltip("Duck when nose height drops below this fraction of normal.")]
        [SerializeField, Range(0.4f, 0.95f)] private float duckRatio = 0.72f;
        [Tooltip("Upward arm speed (lift units per second) that counts as a flap.")]
        [SerializeField] private float flapLiftSpeed = 2.5f;
        [SerializeField] private float flapCooldown = 0.45f;

        [Header("Tracking")]
        [SerializeField, Range(1, 2)] private int maxPlayers = 2;
        [Tooltip("Seconds we keep the last input after the camera loses someone, before saying 'lost'.")]
        [SerializeField] private float lostGraceSeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float minConfidence = 0.45f;

        [Header("Smoothing")]
        [SerializeField] private float filterMinCutoff = 1.2f;
        [SerializeField] private float filterBeta = 0.08f;

        [Header("Debug")]
        [SerializeField] private bool showDebugOverlay = false;

        /// <summary>Input for every player slot. Untracked slots have IsTracked = false.</summary>
        public FlightInput[] Players { get; private set; }
        /// <summary>Shortcut for player 0.</summary>
        public FlightInput Current => Players[0];
        /// <summary>How many players are tracked right now.</summary>
        public int TrackedCount { get; private set; }
        public int MaxPlayers => maxPlayers;
        public IPoseSource ActiveSource { get; private set; }
        public bool UsingCamera => ActiveSource != null && !(ActiveSource is KeyboardPoseSource);

        /// <summary>Fired with the player index when a person appears / disappears.</summary>
        public event Action<int> PlayerFound;
        public event Action<int> PlayerLost;

        private class PlayerState
        {
            public OneEuroFilter Roll, Lift, Spread, Lean;
            public bool Tracked;
            public float LastSeen = -999f;
            public BodyPose LastPose;
            public bool HasPrev;
            public float PrevHipY, PrevLift, HipVel;
            public float JumpCd, FlapCd;
            public float NoseBaseline = -1f;
            public bool Ducking;
            public bool WingsOpen = true;
            // Per-child calibration. -1 = not calibrated, use defaults.
            public float OpenRatio = -1f;
            public float LiftHeight = -1f;

            public void ResetFilters()
            {
                Roll.Reset(); Lift.Reset(); Spread.Reset(); Lean.Reset();
                HasPrev = false; HipVel = 0f; NoseBaseline = -1f; Ducking = false;
            }
        }

        private IPoseSource[] _sources;
        private KeyboardPoseSource _keyboard;
        private PlayerState[] _states;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _sources = GetComponents<IPoseSource>();
            _keyboard = GetComponent<KeyboardPoseSource>();

            Players = new FlightInput[maxPlayers];
            _states = new PlayerState[maxPlayers];
            for (int i = 0; i < maxPlayers; i++)
            {
                Players[i] = FlightInput.Untracked;
                _states[i] = new PlayerState
                {
                    Roll = new OneEuroFilter(filterMinCutoff, filterBeta),
                    Lift = new OneEuroFilter(filterMinCutoff, filterBeta),
                    Spread = new OneEuroFilter(filterMinCutoff, filterBeta),
                    Lean = new OneEuroFilter(filterMinCutoff, filterBeta),
                };
            }
        }

        private void Update()
        {
            ActiveSource = PickSource();
            float dt = Time.unscaledDeltaTime;   // tracking must not slow down when the game pauses
            int tracked = 0;

            for (int i = 0; i < maxPlayers; i++)
            {
                PlayerState s = _states[i];
                BodyPose pose = default;
                bool got = ActiveSource != null && ActiveSource.TryGetPose(i, out pose)
                           && pose.IsPlausible && pose.Confidence >= minConfidence;

                if (got)
                {
                    if (!s.Tracked) { s.Tracked = true; s.ResetFilters(); PlayerFound?.Invoke(i); }
                    s.LastSeen = Time.unscaledTime;
                    s.LastPose = pose;
                    Players[i] = Interpret(s, pose, dt);
                    tracked++;
                }
                else if (s.Tracked && Time.unscaledTime - s.LastSeen < lostGraceSeconds)
                {
                    // Brief dropout: keep flying with the last input, but no new gestures.
                    FlightInput keep = Players[i];
                    keep.Jump = false; keep.Flap = false;
                    Players[i] = keep;
                    tracked++;
                }
                else
                {
                    if (s.Tracked) { s.Tracked = false; PlayerLost?.Invoke(i); }
                    Players[i] = FlightInput.Untracked;
                }
            }
            TrackedCount = tracked;

            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                showDebugOverlay = !showDebugOverlay;
        }

        private FlightInput Interpret(PlayerState s, BodyPose pose, float dt)
        {
            float roll = s.Roll.Filter(pose.ArmRollDegrees, dt);
            float liftRaw = s.Lift.Filter(pose.ArmsHeight, dt);
            float spread = s.Spread.Filter(pose.ArmSpreadRatio, dt);
            float lean = s.Lean.Filter(pose.LeanRatio, dt);

            // --- steering: arms + body lean, both with a generous deadzone ---
            float rollNorm = 0f;
            if (Mathf.Abs(roll) > rollDeadzoneDegrees)
                rollNorm = Mathf.Sign(roll) * Mathf.InverseLerp(rollDeadzoneDegrees, maxRollDegrees, Mathf.Abs(roll));
            float leanNorm = 0f;
            if (Mathf.Abs(lean) > leanDeadzone)
                leanNorm = Mathf.Sign(lean) * Mathf.InverseLerp(leanDeadzone, leanMax, Mathf.Abs(lean));

            // --- wings open, with hysteresis so they do not flicker at the edge ---
            float openRatio = s.OpenRatio > 0f ? s.OpenRatio : wingsOpenRatio;
            if (s.WingsOpen) s.WingsOpen = spread >= openRatio - 0.2f;
            else s.WingsOpen = spread >= openRatio + 0.1f;

            // --- lift ---
            float liftFull = s.LiftHeight > 0f ? s.LiftHeight : fullLiftHeight;
            float lift = Mathf.Clamp01(liftRaw / liftFull);

            // --- gestures ---
            bool jump = false, flap = false;
            float sw = Mathf.Max(pose.ShoulderWidth, 0.02f);
            float hipY = pose.HipCenter.y / sw;              // in shoulder widths, y down
            if (s.HasPrev && dt > 0f)
            {
                float v = (s.PrevHipY - hipY) / dt;           // positive = moving up
                s.HipVel = Mathf.Lerp(s.HipVel, v, 0.5f);
                if (s.JumpCd <= 0f && s.HipVel > jumpHipSpeed) { jump = true; s.JumpCd = jumpCooldown; }

                float liftVel = (lift - s.PrevLift) / dt;
                if (s.FlapCd <= 0f && liftVel > flapLiftSpeed && lift > 0.25f) { flap = true; s.FlapCd = flapCooldown; }
            }
            s.PrevHipY = hipY; s.PrevLift = lift; s.HasPrev = true;
            s.JumpCd -= dt; s.FlapCd -= dt;

            // Duck: compare nose height to a slowly learned "standing" baseline.
            float nose = pose.NoseHeight;
            if (s.NoseBaseline < 0f) s.NoseBaseline = nose;
            if (!s.Ducking) s.NoseBaseline = Mathf.Lerp(s.NoseBaseline, nose, 0.5f * dt);
            if (s.Ducking) s.Ducking = nose < (duckRatio + 0.1f) * s.NoseBaseline;
            else s.Ducking = nose < duckRatio * s.NoseBaseline;

            return new FlightInput
            {
                Roll = Mathf.Clamp(rollNorm + leanNorm * leanWeight, -1f, 1f),
                Lift = lift,
                WingsOpen = s.WingsOpen,
                IsTracked = true,
                Lean = Mathf.Clamp(leanNorm, -1f, 1f),
                Jump = jump,
                Duck = s.Ducking,
                Flap = flap,
                BodyX = pose.BodyX
            };
        }

        /// <summary>
        /// Prefer a camera source that is ready. The keyboard still works at any
        /// time: if the camera sees nobody and someone is pressing keys, use it.
        /// </summary>
        private IPoseSource PickSource()
        {
            IPoseSource camera = null;
            foreach (var s in _sources)
            {
                if (!s.IsReady || s is KeyboardPoseSource) continue;
                camera = s; break;
            }
            if (camera == null) return _keyboard;
            if (camera.PoseCount == 0 && _keyboard != null && _keyboard.HasRecentActivity) return _keyboard;
            return camera;
        }

        // ------------------------------------------------------------------ calibration

        /// <summary>
        /// Learn this child's "arms stretched out, raised" pose. Called by the
        /// tutorial when the child holds the pose. Makes thresholds generous:
        /// wings count as open at 70% of their real spread.
        /// </summary>
        public void CalibrateFromCurrentPose(int player)
        {
            if (player < 0 || player >= maxPlayers || !_states[player].Tracked) return;
            BodyPose p = _states[player].LastPose;
            PlayerState s = _states[player];
            if (p.ArmSpreadRatio > 1.6f) s.OpenRatio = Mathf.Max(1.5f, p.ArmSpreadRatio * 0.7f);
            if (p.ArmsHeight > 0.3f) s.LiftHeight = Mathf.Max(0.4f, p.ArmsHeight * 0.8f);
            s.NoseBaseline = p.NoseHeight;
            Debug.Log($"[Vaanam] Calibrated player {player}: openRatio={s.OpenRatio:0.00} liftHeight={s.LiftHeight:0.00}");
        }

        public void ResetCalibration()
        {
            foreach (var s in _states) { s.OpenRatio = -1f; s.LiftHeight = -1f; s.NoseBaseline = -1f; }
        }

        public bool TryGetLastPose(int player, out BodyPose pose)
        {
            if (player >= 0 && player < maxPlayers && _states[player].Tracked) { pose = _states[player].LastPose; return true; }
            pose = default; return false;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            string src = ActiveSource != null ? ActiveSource.Name : "none";
            GUI.Label(new Rect(10, 10, 600, 20), $"Source: {src}   Tracked players: {TrackedCount}   (F1 hides this)");
            for (int i = 0; i < maxPlayers; i++)
            {
                var f = Players[i];
                GUI.Label(new Rect(10, 30 + i * 20, 700, 20),
                    $"P{i + 1} tracked:{f.IsTracked}  roll:{f.Roll:+0.00;-0.00}  lift:{f.Lift:0.00}  wings:{(f.WingsOpen ? "OPEN" : "closed")}  duck:{f.Duck}  lean:{f.Lean:+0.00;-0.00}  x:{f.BodyX:0.00}");
            }
        }
    }
}
