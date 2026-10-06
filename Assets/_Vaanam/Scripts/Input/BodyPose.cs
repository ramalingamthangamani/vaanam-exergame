using UnityEngine;

namespace Vaanam.Input
{
    /// <summary>
    /// The small set of body points the game cares about.
    ///
    /// Coordinates are normalized to the camera image: x and y in 0..1, origin at
    /// the TOP-LEFT, y growing DOWN, exactly like MediaPipe gives them.
    ///
    /// IMPORTANT CONVENTION: the pose is in SCREEN (mirror) space. "LeftWrist" is
    /// the wrist that appears on the LEFT of the mirrored webcam picture, which is
    /// the child's RIGHT hand. We do this so that when the child tilts to the
    /// right of the screen, the bird banks to the right of the screen. Tracking
    /// sources are responsible for mirroring before filling this struct.
    ///
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

        /// <summary>Where the body is across the picture, 0 = left edge, 1 = right edge.</summary>
        public float BodyX => HipCenter.x;

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

        /// <summary>
        /// Sideways lean of the upper body, in shoulder widths.
        /// Positive when the shoulders are to the right of the hips (leaning right).
        /// A second, very natural steering signal: small children steer with their
        /// whole body, not just their arms.
        /// </summary>
        public float LeanRatio
        {
            get
            {
                float w = ShoulderWidth;
                if (w < 0.001f) return 0f;
                return (ShoulderCenter.x - HipCenter.x) / w;
            }
        }

        /// <summary>
        /// Height of the nose above the hips, in shoulder widths. Drops sharply
        /// when the child ducks or crouches, which is how we detect "duck".
        /// </summary>
        public float NoseHeight
        {
            get
            {
                float w = ShoulderWidth;
                if (w < 0.001f) return 0f;
                return (HipCenter.y - Nose.y) / w;
            }
        }

        /// <summary>True when the landmarks look like a real standing person.</summary>
        public bool IsPlausible => ShoulderWidth > 0.02f && Confidence > 0.3f;
    }
}
