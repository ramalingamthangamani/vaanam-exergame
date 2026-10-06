namespace Vaanam.Input
{
    /// <summary>
    /// Anything that can produce a BodyPose each frame: the webcam tracker,
    /// a keyboard simulator, or a recorded replay for testing.
    /// </summary>
    public interface IPoseSource
    {
        /// <summary>Human readable name shown in the debug overlay.</summary>
        string Name { get; }

        /// <summary>True if the source is running and can deliver poses.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Returns true and fills <paramref name="pose"/> if a pose is available this frame.
        /// Returns false if the player is not visible or the source is not ready.
        /// </summary>
        bool TryGetPose(out BodyPose pose);
    }
}
