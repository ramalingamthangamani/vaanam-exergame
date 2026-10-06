using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vaanam.Core;
using Vaanam.Input;

namespace Vaanam.UI
{
    /// <summary>
    /// The parent corner. Opens by holding P for 1.5 seconds or pressing Escape,
    /// which a small child will not do by accident. Shows what the child has
    /// done (sessions, minutes, stars) and the few settings that matter:
    /// language, session length, voice, music, camera. English only, in plain
    /// text, because this screen is for adults.
    /// </summary>
    public class ParentPanel : MonoBehaviour
    {
        [SerializeField] private float holdSeconds = 1.5f;

        private RectTransform _root;
        private CanvasGroup _group;
        private Text _stats, _langLabel, _lengthLabel, _voiceLabel, _musicLabel, _cameraLabel, _resetLabel;
        private float _hold;
        private float _resetArmedUntil;
        private int _cameraIndex;

        public bool IsOpen => _group != null && _group.alpha > 0.5f;

        private void Awake()
        {
            Build();
            SetOpen(false, instant: true);
        }

        private void Build()
        {
            _root = UiFactory.Rect("ParentRoot", transform);
            UiFactory.Stretch(_root);
            _group = _root.gameObject.AddComponent<CanvasGroup>();

            var dim = UiFactory.Image("Dim", _root, UiFactory.WhiteSprite(), new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch((RectTransform)dim.transform);
            dim.raycastTarget = true;

            var panel = UiFactory.Image("Panel", _root, UiFactory.RoundedSprite(), new Color(0.12f, 0.14f, 0.2f, 0.97f));
            UiFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 860f));
            panel.raycastTarget = true;
            Transform p = panel.transform;

            var title = UiFactory.Text("Title", p, "Parent corner", 48, Color.white);
            UiFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 60f));

            _stats = UiFactory.Text("Stats", p, "", 28, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft, FontStyle.Normal);
            UiFactory.Place((RectTransform)_stats.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900f, 150f));

            float y = -270f; const float rowH = 78f;
            _langLabel = Row(p, ref y, rowH, "Language", "", () => { var s = SaveData.Current; s.Language = s.Language == 0 ? 1 : 0; L10n.Current = (Language)s.Language; s.Save(); RefreshLabels(); });
            _lengthLabel = Row(p, ref y, rowH, "Session length", "", () => { var s = SaveData.Current; s.SessionMinutes = s.SessionMinutes >= 20 ? 10 : s.SessionMinutes + 5; s.Save(); RefreshLabels(); });
            _voiceLabel = Row(p, ref y, rowH, "Voice", "", () => { var s = SaveData.Current; s.VoiceEnabled = !s.VoiceEnabled; s.Save(); RefreshLabels(); });
            _musicLabel = Row(p, ref y, rowH, "Music", "", () => { var s = SaveData.Current; s.MusicEnabled = !s.MusicEnabled; s.Save(); RefreshLabels(); });
            _cameraLabel = Row(p, ref y, rowH, "Camera", "", NextCamera);

            y -= 20f;
            var newSession = UiFactory.Button("NewSession", p, "Start a new session now", () => { GameFlow.Instance?.NewSession(); SetOpen(false); }, new Vector2(620f, 64f), new Color(0.2f, 0.65f, 0.4f), 28);
            UiFactory.Place((RectTransform)newSession.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 64f));
            y -= 80f;
            var reset = UiFactory.Button("Reset", p, "Reset progress", ResetProgress, new Vector2(620f, 64f), new Color(0.55f, 0.25f, 0.3f), 28);
            UiFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 64f));
            _resetLabel = reset.GetComponentInChildren<Text>();
            y -= 80f;
            var close = UiFactory.Button("Close", p, "Close", () => SetOpen(false), new Vector2(300f, 64f), new Color(0.3f, 0.35f, 0.45f), 28);
            UiFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(300f, 64f));

            var hint = UiFactory.Text("Hint", p, "Hold P or press Esc to open this panel. Keyboard: A/D tilt, W flap, S duck, Shift jump, Space fold. F1 debug.", 20, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UiFactory.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(940f, 60f));
        }

        private Text Row(Transform parent, ref float y, float h, string label, string value, UnityEngine.Events.UnityAction onClick)
        {
            var row = UiFactory.Rect(label, parent);
            UiFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, h - 10f));
            var l = UiFactory.Text("Label", row, label, 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            UiFactory.Place((RectTransform)l.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(400f, h));
            var b = UiFactory.Button("Value", row, value, onClick, new Vector2(440f, h - 14f), new Color(0.25f, 0.45f, 0.75f), 28);
            UiFactory.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(440f, h - 14f));
            y -= h;
            return b.GetComponentInChildren<Text>();
        }

        private void RefreshLabels()
        {
            var s = SaveData.Current;
            _langLabel.text = s.Language == (int)Language.Tamil ? "Tamil (தமிழ்)" : "English";
            _lengthLabel.text = s.SessionMinutes + " minutes";
            _voiceLabel.text = s.VoiceEnabled ? "On" : "Off";
            _musicLabel.text = s.MusicEnabled ? "On" : "Off";
            var devices = WebCamTexture.devices;
            string cam = devices.Length == 0 ? "No camera found" : (string.IsNullOrEmpty(s.CameraName) ? devices[0].name : s.CameraName);
            _cameraLabel.text = devices.Length == 0 ? cam : "▸ " + cam;
            _stats.text =
                $"Sessions played: {s.SessionsPlayed}      Total minutes: {Mathf.RoundToInt(s.TotalMinutes)}\n" +
                $"Stars collected: {s.TotalStars}      Rings: {s.TotalRings}\n" +
                $"Highest level: {s.HighestLevelReached}      Last played: {(string.IsNullOrEmpty(s.LastPlayedDate) ? "-" : s.LastPlayedDate)}\n" +
                $"Camera frames never leave this computer. No ads, no purchases.";
            if (_resetLabel != null && Time.unscaledTime > _resetArmedUntil) _resetLabel.text = "Reset progress";
        }

        private void NextCamera()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0) return;
            _cameraIndex = (_cameraIndex + 1) % devices.Length;
            var s = SaveData.Current; s.CameraName = devices[_cameraIndex].name; s.Save();
#if VAANAM_MEDIAPIPE
            var src = FindFirstObjectByType<MediaPipePoseSource>();
            if (src != null) src.SetCamera(s.CameraName);
#endif
            RefreshLabels();
        }

        private void ResetProgress()
        {
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 4f;
                _resetLabel.text = "Tap again to confirm";
                return;
            }
            SaveData.Current.ResetProgress();
            _resetArmedUntil = 0f;
            RefreshLabels();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) SetOpen(!IsOpen);
            if (kb.pKey.isPressed)
            {
                _hold += Time.unscaledDeltaTime;
                if (_hold >= holdSeconds) { _hold = -999f; SetOpen(!IsOpen); }
            }
            else _hold = 0f;
            if (IsOpen && Time.unscaledTime > _resetArmedUntil && _resetLabel != null && _resetLabel.text != "Reset progress") _resetLabel.text = "Reset progress";
        }

        public void SetOpen(bool open, bool instant = false)
        {
            if (open) RefreshLabels();
            _group.alpha = open ? 1f : 0f;
            _group.blocksRaycasts = open;
            _group.interactable = open;
            Cursor.visible = open;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
