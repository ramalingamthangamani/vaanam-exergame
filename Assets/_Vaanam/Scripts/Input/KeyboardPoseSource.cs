using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaanam.Input
{
    /// <summary>
    /// Fakes a body pose from the keyboard so you can build and test the game
    /// without a webcam. Also the permanent fallback for players with no camera.
    ///
    ///   A / D  or  Left / Right : tilt arms (roll)
    ///   W / Up                  : flap (raise arms)
    ///   Space (hold)            : arms stretched out
    /// </summary>
    public class KeyboardPoseSource : MonoBehaviour, IPoseSource
    {
        [SerializeField] private float rollSpeed = 120f;      // degrees per second
        [SerializeField] private float maxRoll = 45f;
        [SerializeField] private float flapSpeed = 4f;

        private float _roll;
        private float _armsHeight;

        public string Name => "Keyboard";
        public bool IsReady => true;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float rollInput = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rollInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rollInput += 1f;

            if (Mathf.Approximately(rollInput, 0f))
                _roll = Mathf.MoveTowards(_roll, 0f, rollSpeed * Time.deltaTime);
            else
                _roll = Mathf.Clamp(_roll + rollInput * rollSpeed * Time.deltaTime, -maxRoll, maxRoll);

            float targetHeight = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1.2f : 0f;
            _armsHeight = Mathf.MoveTowards(_armsHeight, targetHeight, flapSpeed * Time.deltaTime);
        }

        public bool TryGetPose(out BodyPose pose)
        {
            // Build a plausible normalized skeleton from the simulated values.
            const float shoulderW = 0.2f;
            Vector2 shoulderCenter = new Vector2(0.5f, 0.45f);
            Vector2 left = shoulderCenter + new Vector2(-shoulderW / 2f, 0f);
            Vector2 right = shoulderCenter + new Vector2(shoulderW / 2f, 0f);

            bool armsOut = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            float spread = armsOut ? 3f * shoulderW : 1f * shoulderW;

            float rollRad = _roll * Mathf.Deg2Rad;
            Vector2 halfArm = new Vector2(Mathf.Cos(rollRad), -Mathf.Sin(rollRad)) * (spread / 2f);
            Vector2 lift = new Vector2(0f, -_armsHeight * shoulderW);

            pose = new BodyPose
            {
                Nose = shoulderCenter + new Vector2(0f, -0.15f),
                LeftShoulder = left,
                RightShoulder = right,
                LeftWrist = shoulderCenter - halfArm + lift,
                RightWrist = shoulderCenter + halfArm + lift,
                LeftHip = left + new Vector2(0f, 0.3f),
                RightHip = right + new Vector2(0f, 0.3f),
                Confidence = 1f
            };
            return true;
        }
    }
}
