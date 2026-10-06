// ============================================================================
//  MediaPipePoseSource  (YOUR FIRST BIG LEARNING TASK)
// ============================================================================
//  This file is compiled only after you add the scripting define symbol
//  VAANAM_MEDIAPIPE in  Edit > Project Settings > Player > Scripting Define Symbols.
//  Until then the game runs on the keyboard source, so nothing is blocked.
//
//  Goal: fill BodyPose from MediaPipe Pose Landmarker and return it in TryGetPose.
//
//  Steps:
//   1. In Package Manager open "MediaPipe Unity Plugin" and import the sample
//      "Solutions". Run the Pose Landmark Detection sample to confirm the webcam
//      works. Read its PoseLandmarkerRunner.cs. That script is your template.
//   2. Download pose_landmarker_lite.task from the MediaPipe models page and put it
//      in Assets/StreamingAssets/ (create the folder). "lite" is fast enough for kids.
//   3. Copy the parts of PoseLandmarkerRunner that: start the WebCamTexture, build
//      PoseLandmarkerOptions with RunningMode.LIVE_STREAM, and receive results in a
//      callback. Store the latest result in a field. The callback runs off the main
//      thread, so copy only plain data and use the lock below.
//   4. In TryGetPose map landmark indices to BodyPose:
//        0 nose   11 L shoulder  12 R shoulder
//        15 L wrist  16 R wrist   23 L hip  24 R hip
//      MediaPipe "left" is the PERSON's left, which appears on the RIGHT of a
//      mirrored webcam image. Decide once whether you mirror and stay consistent.
//      Set Confidence from the average "visibility" of those 7 landmarks.
//   5. Set IsReady = true once the first result arrives.
//   6. Add this component next to KeyboardPoseSource on the PoseProvider object.
//      PoseProvider will automatically prefer it over the keyboard.
//   7. Also add "Mediapipe.Runtime" to the references list in Vaanam.asmdef.
// ============================================================================
#if VAANAM_MEDIAPIPE
using UnityEngine;

namespace Vaanam.Input
{
    public class MediaPipePoseSource : MonoBehaviour, IPoseSource
    {
        public string Name => "MediaPipe Pose";
        public bool IsReady { get; private set; }

        private BodyPose _latest;
        private bool _hasLatest;
        private readonly object _lock = new object();

        private void Start()
        {
            // TODO step 3: start webcam + Pose Landmarker in LIVE_STREAM mode.
        }

        // TODO step 3: result callback -> convert -> lock(_lock) { _latest = ...; _hasLatest = true; IsReady = true; }

        public bool TryGetPose(out BodyPose pose)
        {
            lock (_lock)
            {
                pose = _latest;
                return _hasLatest;
            }
        }

        private void OnDestroy()
        {
            // TODO: stop webcam and dispose the landmarker.
        }
    }
}
#endif
