namespace Vaanam.Input
{
    /// <summary>
    /// What the flight controller actually consumes. Produced by PoseProvider
    /// from a BodyPose. Game code should depend on this, never on raw landmarks.
    /// </summary>
    public struct FlightInput
    {
        /// <summary>-1 (bank left) .. +1 (bank right)</summary>
        public float Roll;

        /// <summary>0 .. 1, how much lift the player is generating by raising arms.</summary>
        public float Lift;

        /// <summary>True when both arms are stretched out, i.e. wings open.</summary>
        public bool WingsOpen;

        /// <summary>False when the tracker lost the player this frame.</summary>
        public bool IsTracked;
    }
}
