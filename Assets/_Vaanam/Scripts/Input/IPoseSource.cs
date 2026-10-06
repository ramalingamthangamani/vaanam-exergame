using UnityEngine;

namespace Vaanam.Input
{
    /// <summary>
    /// Anything that can produce BodyPoses each frame: the webcam tracker,
    /// a keyboard simulator, or a recorded replay for testing.
    /// One source may see several people (two-player co-op), so poses are indexed.
    /// Poses must be sorted left-to-right across the picture.
    /// </summary>
    public interface IPoseSource
    {
        /// <summary>Human readable name shown in the debug overlay.</summary>
        string Name { get; }

        /// <summary>True if the source is running and can deliver poses.</summary>
        bool IsReady { get; }

        /// <summary>How many people were seen in the latest frame (0 if nobody).</summary>
        int PoseCount { get; }

        /// <summary>
        /// Returns true and fills <paramref name="pose"/> if the person at
        /// <paramref name="index"/> (0 = leftmost on screen) is visible this frame.
        /// </summary>
        bool TryGetPose(int index, out BodyPose pose);

        /// <summary>
        /// Live camera picture for the on-screen mirror. Null when there is no camera.
        /// </summary>
        Texture PreviewTexture { get; }

        /// <summary>True if the preview must be flipped horizontally to look like a mirror.</summary>
        bool PreviewIsMirrored { get; }
    }
}
