using UnityEngine;

namespace Vaanam.Input
{
    /// <summary>
    /// The small set of body points the game cares about.
    /// All positions are normalized to the camera image: x and y in 0..1,
    /// origin at top-left, exactly like MediaPipe gives them.
    /// Keeping this struct tiny means the rest of the game never has to know
    /// which tracking library produced the data.
    /// </summary>
    public struct BodyPose
    {
        public Vector2 Nose;
        public Vector2 LeftShoulder;
        public Vector2 RightShoulder;
        public Vector2 LeftWrist;
        public Vector2 RightWrist;
        public Vector2 LeftHip;
        public Vector2 RightHip;

        /// <summary>0..1 confidence that this frame is usable.</summary>
        public float Confidence;

        public Vector2 ShoulderCenter => (LeftShoulder + RightShoulder) * 0.5f;
        public Vector2 HipCenter => (LeftHip + RightHip) * 0.5f;
        public float ShoulderWidth => Vector2.Distance(LeftShoulder, RightShoulder);

        /// <summary>
        /// Roll in degrees. Positive when the right wrist is higher than the left.
        /// Based on the wrist-to-wrist line so it works at any camera distance.
        /// </summary>
        public float ArmRollDegrees
        {
            get
            {
                Vector2 d = RightWrist - LeftWrist;
                // Image y grows downward, so flip it.
                return Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// How far the arms are spread, relative to shoulder width.
        /// Around 2.5 to 3.5 means fully stretched out. Below 1.5 means arms down.
        /// </summary>
        public float ArmSpreadRatio
        {
            get
            {
                float w = ShoulderWidth;
                if (w < 0.001f) return 0f;
                return Vector2.Distance(LeftWrist, RightWrist) / w;
            }
        }

        /// <summary>
        /// Average wrist height relative to the shoulders, in shoulder widths.
        /// Positive means wrists are above the shoulders.
        /// </summary>
        public float ArmsHeight
        {
            get
            {
                float w = ShoulderWidth;
                if (w < 0.001f) return 0f;
                float avgWristY = (LeftWrist.y + RightWrist.y) * 0.5f;
                return (ShoulderCenter.y - avgWristY) / w;
            }
        }
    }
}
