// ============================================================================
//  MediaPipePoseSource
// ============================================================================
//  Turns the webcam picture into BodyPoses using MediaPipe Pose Landmarker.
//
//  This file is compiled only when the define VAANAM_MEDIAPIPE is active. The
//  Vaanam.asmdef turns that define on automatically whenever the package
//  "com.github.homuler.mediapipe" is installed (see "versionDefines" in the
//  asmdef). If the package is missing the game still compiles and runs on the
//  keyboard source, so nothing is ever blocked.
//
//  How it works, in plain words:
//   1. Open the webcam with Unity's WebCamTexture (frames arrive on the GPU
//      and in system memory).
//   2. Every frame, copy the pixels into one of a few spare Texture2Ds, flipping
//      the rows so the picture is the right way up for MediaPipe (Unity stores
//      images bottom-up, MediaPipe wants top-down).
//   3. Hand that texture to the Pose Landmarker in LIVE_STREAM mode. MediaPipe
//      runs on its own thread and calls OnResult when it is done.
//   4. OnResult converts the 33 landmarks into our tiny BodyPose struct
//      (7 points), mirrors x so it matches what the child sees, sorts people
//      left-to-right, and stores the result under a lock.
//   5. The game thread reads the latest result through TryGetPose.
//
//  Privacy: frames stay inside this process. Nothing is saved or sent anywhere.
// ============================================================================
#if VAANAM_MEDIAPIPE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Vaanam.Input
{
    public class MediaPipePoseSource : MonoBehaviour, IPoseSource
    {
        // Landmark indices from the MediaPipe pose model (33 points).
        private const int NOSE = 0, L_SHOULDER = 11, R_SHOULDER = 12,
                          L_WRIST = 15, R_WRIST = 16, L_HIP = 23, R_HIP = 24;

        [Header("Model")]
        [Tooltip("File inside Assets/StreamingAssets. 'lite' is fast enough for kids and runs on any laptop.")]
        [SerializeField] private string modelFileName = "pose_landmarker_lite.task";
        [SerializeField, Range(1, 2)] private int maxPoses = 2;
        [SerializeField, Range(0.1f, 0.9f)] private float minDetectionConfidence = 0.5f;
        [SerializeField, Range(0.1f, 0.9f)] private float minPresenceConfidence = 0.5f;
        [SerializeField, Range(0.1f, 0.9f)] private float minTrackingConfidence = 0.5f;

        [Header("Camera")]
        [Tooltip("Part of the camera name to prefer. Empty = first camera. Saved by the parent panel.")]
        [SerializeField] private string preferredCameraName = "";
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;
        [Tooltip("Show the child a mirror image, like looking into a mirror. Keep on.")]
        [SerializeField] private bool mirror = true;
        [Tooltip("Flip rows before handing the picture to MediaPipe. The plugin already reads Unity textures the right way up, so this is normally OFF. Auto-corrected at runtime if wrong.")]
        [SerializeField] private bool flipVertically = false;

        [Header("Tracking")]
        [Tooltip("How long a person may vanish before we say they are gone.")]
        [SerializeField] private float staleSeconds = 0.5f;
        [Tooltip("A second person must be at least this fraction as large (shoulder width) as the biggest one, so people in the background are ignored.")]
        [SerializeField, Range(0.3f, 1f)] private float minRelativeSize = 0.6f;

        public string Name => "MediaPipe Pose (" + _cameraName + ")";
        public bool IsReady => _running;
        public int PoseCount => IsFresh ? _publicCount : 0;
        public Texture PreviewTexture => _webcam;
        public bool PreviewIsMirrored => mirror;
        public string LastError { get; private set; }
        public float InferenceFps { get; private set; }

        private WebCamTexture _webcam;
        private string _cameraName = "no camera";
        private Texture2D[] _ring;
        private int _ringIndex;
        private Color32[] _pixels;
        private Color32[] _flipped;

        private PoseLandmarker _landmarker;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastTimestamp = -1;
        private bool _running;

        // Written by the MediaPipe thread, read by the game thread. Always under _lock.
        private readonly object _lock = new object();
        private readonly BodyPose[] _incoming = new BodyPose[2];
        private int _incomingCount;
        private long _incomingSerial;

        // Game-thread copies.
        private readonly BodyPose[] _public = new BodyPose[2];
        private int _publicCount;
        private long _publicSerial = -1;
        private float _lastFreshTime = -999f;
        private int _upsideDownFrames;
        private float _fpsAccum; private int _fpsCount; private float _fpsTimer;

        private bool IsFresh => Time.unscaledTime - _lastFreshTime < staleSeconds;

        private void Start()
        {
            // Parent panel can save a preferred camera; honour it.
            string saved = PlayerPrefs.GetString("vaanam.camera", "");
            if (!string.IsNullOrEmpty(saved)) preferredCameraName = saved;
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            // ---- 1. Open the webcam -------------------------------------------------
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                LastError = "No webcam found";
                Debug.LogWarning("[Vaanam] " + LastError + ". Keyboard controls will be used.");
                yield break;
            }

            string deviceName = devices[0].name;
            foreach (var d in devices)
            {
                if (!string.IsNullOrEmpty(preferredCameraName) &&
                    d.name.IndexOf(preferredCameraName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    deviceName = d.name; break;
                }
            }
            _cameraName = deviceName;

            _webcam = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
            _webcam.Play();

            // WebCamTexture reports a 16x16 placeholder until the first real frame arrives.
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_webcam.width <= 16 && Time.realtimeSinceStartup < deadline) yield return null;
            if (_webcam.width <= 16)
            {
                LastError = "Webcam did not start: " + deviceName;
                Debug.LogWarning("[Vaanam] " + LastError);
                _webcam.Stop(); _webcam = null;
                yield break;
            }

            int w = _webcam.width, h = _webcam.height;
            _pixels = new Color32[w * h];
            _flipped = new Color32[w * h];
            // A small ring of textures: MediaPipe may still be reading the previous
            // frame while we write the next one, so never reuse the newest buffer.
            _ring = new Texture2D[6];
            for (int i = 0; i < _ring.Length; i++) _ring[i] = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // ---- 2. Load the model and create the landmarker -----------------------
            string path = Path.Combine(Application.streamingAssetsPath, modelFileName);
            if (!File.Exists(path))
            {
                LastError = "Model missing: " + path;
                Debug.LogError("[Vaanam] " + LastError + "\nRun the menu Vaanam > Setup > Copy MediaPipe Model, or copy pose_landmarker_lite.bytes from the plugin's PackageResources into StreamingAssets with this name.");
                yield break;
            }

            try
            {
                byte[] model = File.ReadAllBytes(path);
                var options = new PoseLandmarkerOptions(
                    new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: model),
                    runningMode: RunningMode.LIVE_STREAM,
                    numPoses: maxPoses,
                    minPoseDetectionConfidence: minDetectionConfidence,
                    minPosePresenceConfidence: minPresenceConfidence,
                    minTrackingConfidence: minTrackingConfidence,
                    outputSegmentationMasks: false,
                    resultCallback: OnResult);
                _landmarker = PoseLandmarker.CreateFromOptions(options);
            }
            catch (Exception e)
            {
                LastError = "MediaPipe failed to start: " + e.Message;
                Debug.LogError("[Vaanam] " + LastError + "\n" + e);
                yield break;
            }

            _running = true;
            Debug.Log($"[Vaanam] MediaPipe pose tracking running on '{deviceName}' at {w}x{h}.");
        }

        private void Update()
        {
            if (!_running || _webcam == null || !_webcam.didUpdateThisFrame) return;

            // ---- 3. Copy the frame and send it to MediaPipe ------------------------
            _webcam.GetPixels32(_pixels);
            int w = _webcam.width, h = _webcam.height;
            Color32[] src = _pixels;
            if (flipVertically)
            {
                for (int y = 0; y < h; y++)
                    Array.Copy(_pixels, y * w, _flipped, (h - 1 - y) * w, w);
                src = _flipped;
            }

            Texture2D tex = _ring[_ringIndex];
            _ringIndex = (_ringIndex + 1) % _ring.Length;
            tex.SetPixels32(src);   // writes the CPU copy MediaPipe reads; no Apply() needed

            long ts = _clock.ElapsedMilliseconds;
            if (ts <= _lastTimestamp) ts = _lastTimestamp + 1;   // MediaPipe requires strictly increasing time
            _lastTimestamp = ts;

            try
            {
                var image = new Image(ImageFormat.Types.Format.Srgba, tex);
                _landmarker.DetectAsync(image, ts);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Vaanam] DetectAsync failed: " + e.Message);
            }

            // ---- 5. Pull the newest result onto the game thread --------------------
            lock (_lock)
            {
                if (_incomingSerial != _publicSerial)
                {
                    _publicSerial = _incomingSerial;
                    _publicCount = _incomingCount;
                    for (int i = 0; i < _publicCount; i++) _public[i] = _incoming[i];
                    _lastFreshTime = Time.unscaledTime;
                    _fpsCount++;
                }
            }

            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f) { InferenceFps = _fpsCount / _fpsTimer; _fpsTimer = 0f; _fpsCount = 0; }

            AutoCorrectOrientation();
        }

        /// <summary>
        /// If people consistently appear upside down (nose below hips) the rows are
        /// flipped the wrong way for this camera. Fix it once instead of asking the
        /// developer to guess.
        /// </summary>
        private void AutoCorrectOrientation()
        {
            if (!IsFresh || _publicCount == 0) { _upsideDownFrames = 0; return; }
            if (_public[0].NoseHeight < -0.3f) _upsideDownFrames++; else _upsideDownFrames = 0;
            if (_upsideDownFrames > 30)
            {
                flipVertically = !flipVertically;
                _upsideDownFrames = 0;
                Debug.Log("[Vaanam] Camera image looked upside down; flipped it. flipVertically=" + flipVertically);
            }
        }

        // ---- 4. Runs on MediaPipe's thread. Only touch plain data here. ------------
        private void OnResult(PoseLandmarkerResult result, Image image, long timestampMs)
        {
            var poses = result.poseLandmarks;
            int count = 0;
            Span<BodyPose> temp = stackalloc BodyPose[2];

            if (poses != null)
            {
                // People far from the camera (someone walking past in the background)
                // have narrow shoulders in the picture. Only players close to the
                // screen count: keep the biggest person and anyone at least 60% as big.
                float biggest = 0f;
                for (int p = 0; p < poses.Count; p++)
                {
                    var lm = poses[p].landmarks;
                    if (lm == null || lm.Count <= R_HIP) continue;
                    biggest = Mathf.Max(biggest, Convert(lm).ShoulderWidth);
                }

                for (int p = 0; p < poses.Count && count < 2; p++)
                {
                    var lm = poses[p].landmarks;
                    if (lm == null || lm.Count <= R_HIP) continue;
                    BodyPose pose = Convert(lm);
                    if (pose.ShoulderWidth < biggest * minRelativeSize) continue;
                    temp[count++] = pose;
                }
            }

            // Leftmost person on screen is player 0.
            if (count == 2 && temp[0].BodyX > temp[1].BodyX)
            {
                var t = temp[0]; temp[0] = temp[1]; temp[1] = t;
            }

            lock (_lock)
            {
                _incomingCount = count;
                for (int i = 0; i < count; i++) _incoming[i] = temp[i];
                _incomingSerial++;
            }
        }

        private BodyPose Convert(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> lm)
        {
            // MediaPipe "left" is the PERSON's left. In a mirror the person's left
            // hand appears on the RIGHT of the screen. BodyPose is in screen space,
            // so with mirroring we swap sides and flip x.
            int lsh = mirror ? R_SHOULDER : L_SHOULDER, rsh = mirror ? L_SHOULDER : R_SHOULDER;
            int lwr = mirror ? R_WRIST : L_WRIST, rwr = mirror ? L_WRIST : R_WRIST;
            int lhp = mirror ? R_HIP : L_HIP, rhp = mirror ? L_HIP : R_HIP;

            float vis = 0f;
            int[] used = { NOSE, lsh, rsh, lwr, rwr, lhp, rhp };
            foreach (int i in used) vis += lm[i].visibility ?? 1f;

            return new BodyPose
            {
                Nose = P(lm[NOSE]),
                LeftShoulder = P(lm[lsh]), RightShoulder = P(lm[rsh]),
                LeftWrist = P(lm[lwr]), RightWrist = P(lm[rwr]),
                LeftHip = P(lm[lhp]), RightHip = P(lm[rhp]),
                Confidence = vis / used.Length
            };
        }

        private Vector2 P(Mediapipe.Tasks.Components.Containers.NormalizedLandmark l)
            => new Vector2(mirror ? 1f - l.x : l.x, l.y);

        public bool TryGetPose(int index, out BodyPose pose)
        {
            if (!IsFresh || index < 0 || index >= _publicCount) { pose = default; return false; }
            pose = _public[index];
            return true;
        }

        /// <summary>Switch camera at runtime (parent panel). Restarts tracking.</summary>
        public void SetCamera(string namePart)
        {
            preferredCameraName = namePart ?? "";
            PlayerPrefs.SetString("vaanam.camera", preferredCameraName);
            Shutdown();
            StartCoroutine(Initialize());
        }

        private void Shutdown()
        {
            _running = false;
            try { _landmarker?.Close(); } catch (Exception e) { Debug.LogWarning(e.Message); }
            _landmarker = null;
            if (_webcam != null) { _webcam.Stop(); Destroy(_webcam); _webcam = null; }
            if (_ring != null) foreach (var t in _ring) if (t != null) Destroy(t);
            _ring = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
#endif
