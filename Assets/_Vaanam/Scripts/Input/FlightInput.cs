namespace Vaanam.Input
{
    /// <summary>
    /// What the flight controller actually consumes, for ONE player. Produced by
    /// PoseProvider from a BodyPose. Game code should depend on this, never on
    /// raw landmarks.
    /// </summary>
    public struct FlightInput
    {
        /// <summary>-1 (bank left) .. +1 (bank right). Already combines arm tilt and body lean.</summary>
        public float Roll;

        /// <summary>0 .. 1, how much lift the player is generating by raising arms.</summary>
        public float Lift;

        /// <summary>True when both arms are stretched out, i.e. wings open.</summary>
        public bool WingsOpen;

        /// <summary>False when the tracker lost the player this frame.</summary>
        public bool IsTracked;

        /// <summary>-1 .. +1 body lean only (shoulders vs hips). Useful for visuals.</summary>
        public float Lean;

        /// <summary>True for exactly one frame when the player jumped (hips shot up).</summary>
        public bool Jump;

        /// <summary>True while the player is crouching / ducking.</summary>
        public bool Duck;

        /// <summary>True for exactly one frame when the arms were swept up quickly.</summary>
        public bool Flap;

        /// <summary>Where the player stands across the camera picture, 0 left .. 1 right.</summary>
        public float BodyX;

        public static FlightInput Untracked => new FlightInput { IsTracked = false, WingsOpen = true, BodyX = 0.5f };
    }
}
