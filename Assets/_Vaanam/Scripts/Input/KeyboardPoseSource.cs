using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaanam.Input
{
    /// <summary>
    /// Fakes body poses from the keyboard so you can build and test the game
    /// without a webcam. Also the permanent fallback for players with no camera.
    ///
    ///   Player 1                        Player 2
    ///   A / D        tilt arms          Left / Right
    ///   W            raise arms (lift)  Up
    ///   S            duck               Down
    ///   Left Shift   jump               Right Shift
    ///   Space (hold) fold wings         Enter (hold)
    ///   Press 2 to add the second keyboard player, 1 to remove.
    ///
    /// Note: the keyboard only drives a fake skeleton. All the game logic
    /// (jump detection, duck, calibration) runs in PoseProvider exactly as it
    /// does for the webcam, so the keyboard tests the real code path.
    /// </summary>
    public class KeyboardPoseSource : MonoBehaviour, IPoseSource
    {
        [SerializeField] private float rollSpeed = 120f;      // degrees per second
        [SerializeField] private float maxRoll = 40f;
        [SerializeField] private float armSpeed = 5f;         // shoulder widths per second

        private class Sim
        {
            public float Roll;
            public float ArmsHeight;   // in shoulder widths, 0 = level with shoulders
            public float Duck;         // 0..1
            public float Fold;         // 0..1, 1 = wings folded
            public float JumpT = -1f;  // seconds since jump started, -1 = none
        }

        private readonly Sim[] _sims = { new Sim(), new Sim() };
        private int _players = 1;

        public string Name => "Keyboard";
        public bool IsReady => true;
        public int PoseCount => _players;
        public Texture PreviewTexture => null;
        public bool PreviewIsMirrored => false;

        /// <summary>True for a couple of seconds after any control key was touched.
        /// PoseProvider uses this so the keyboard works even while a camera is active.</summary>
        public bool HasRecentActivity => Time.unscaledTime - _lastActivity < 2f;
        private float _lastActivity = -999f;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit2Key.wasPressedThisFrame) _players = 2;
            if (kb.digit1Key.wasPressedThisFrame) _players = 1;

            if (kb.aKey.isPressed || kb.dKey.isPressed || kb.wKey.isPressed || kb.sKey.isPressed ||
                kb.leftShiftKey.isPressed || kb.spaceKey.isPressed || kb.leftArrowKey.isPressed ||
                kb.rightArrowKey.isPressed || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed ||
                kb.rightShiftKey.isPressed || kb.enterKey.isPressed)
                _lastActivity = Time.unscaledTime;

            Drive(_sims[0],
                left: kb.aKey.isPressed, right: kb.dKey.isPressed,
                up: kb.wKey.isPressed, duck: kb.sKey.isPressed,
                jump: kb.leftShiftKey.wasPressedThisFrame, fold: kb.spaceKey.isPressed);

            Drive(_sims[1],
                left: kb.leftArrowKey.isPressed, right: kb.rightArrowKey.isPressed,
                up: kb.upArrowKey.isPressed, duck: kb.downArrowKey.isPressed,
                jump: kb.rightShiftKey.wasPressedThisFrame, fold: kb.enterKey.isPressed);
        }

        private void Drive(Sim s, bool left, bool right, bool up, bool duck, bool jump, bool fold)
        {
            float dt = Time.deltaTime;
            float rollInput = (right ? 1f : 0f) - (left ? 1f : 0f);
            if (Mathf.Approximately(rollInput, 0f))
                s.Roll = Mathf.MoveTowards(s.Roll, 0f, rollSpeed * dt);
            else
                s.Roll = Mathf.Clamp(s.Roll + rollInput * rollSpeed * dt, -maxRoll, maxRoll);

            s.ArmsHeight = Mathf.MoveTowards(s.ArmsHeight, up ? 1.2f : 0f, armSpeed * dt);
            s.Duck = Mathf.MoveTowards(s.Duck, duck ? 1f : 0f, 6f * dt);
            s.Fold = Mathf.MoveTowards(s.Fold, fold ? 1f : 0f, 8f * dt);

            if (jump && s.JumpT < 0f) s.JumpT = 0f;
            if (s.JumpT >= 0f)
            {
                s.JumpT += dt;
                if (s.JumpT > 0.5f) s.JumpT = -1f;
            }
        }

        public bool TryGetPose(int index, out BodyPose pose)
        {
            if (index < 0 || index >= _players) { pose = default; return false; }
            Sim s = _sims[index];

            // Build a plausible normalized skeleton from the simulated values.
            const float shoulderW = 0.18f;
            float centerX = _players == 1 ? 0.5f : (index == 0 ? 0.3f : 0.7f);

            // Jump: the whole body rises quickly then comes back (half sine over 0.5 s).
            float jumpRise = s.JumpT >= 0f ? Mathf.Sin(s.JumpT / 0.5f * Mathf.PI) * 0.7f * shoulderW : 0f;
            // Duck: the body compresses, nose and shoulders drop toward the hips.
            float duckDrop = s.Duck * 0.10f;

            Vector2 hipCenter = new Vector2(centerX, 0.75f - jumpRise);
            Vector2 shoulderCenter = hipCenter + new Vector2(0f, -0.30f + duckDrop);
            Vector2 left = shoulderCenter + new Vector2(-shoulderW / 2f, 0f);
            Vector2 right = shoulderCenter + new Vector2(shoulderW / 2f, 0f);

            float spread = Mathf.Lerp(3f, 1f, s.Fold) * shoulderW;
            float rollRad = s.Roll * Mathf.Deg2Rad;
            Vector2 halfArm = new Vector2(Mathf.Cos(rollRad), -Mathf.Sin(rollRad)) * (spread / 2f);
            Vector2 lift = new Vector2(0f, -s.ArmsHeight * shoulderW);

            pose = new BodyPose
            {
                Nose = shoulderCenter + new Vector2(0f, -0.14f + duckDrop),
                LeftShoulder = left,
                RightShoulder = right,
                LeftWrist = shoulderCenter - halfArm + lift,
                RightWrist = shoulderCenter + halfArm + lift,
                LeftHip = hipCenter + new Vector2(-shoulderW / 2f, 0f),
                RightHip = hipCenter + new Vector2(shoulderW / 2f, 0f),
                Confidence = 1f
            };
            return true;
        }
    }
}
