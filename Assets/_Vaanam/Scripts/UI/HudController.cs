using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vaanam.Core;
using Vaanam.Input;

namespace Vaanam.UI
{
    /// <summary>
    /// Heads-up display for children: a star jar, a ring counter, a sun that
    /// travels across the top of the screen as the session passes, and the
    /// mirror in the corner. No menus, no numbers except the two counts.
    /// Everything is built in code in Awake.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public static HudController Instance { get; private set; }

        [SerializeField] private SessionTimer timer;
        [SerializeField] private Vector2 mirrorSize = new Vector2(360f, 270f);

        private Text _starText, _ringText;
        private RectTransform _starBox, _ringBox, _sun, _sunTrack;
        private Image _sunImage, _flash, _keyboardHint;
        private Text _keyboardFallback;
        private CanvasGroup _group;
        private RawImage _mirror;
        private int _lastStars, _lastRings;

        private void Awake()
        {
            Instance = this;
            UiFactory.EnsureEventSystem();
            if (timer == null) timer = FindFirstObjectByType<SessionTimer>();
            Build();
        }

        private void Build()
        {
            var root = UiFactory.Rect("HUD", transform);
            UiFactory.Stretch(root);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            // --- Star jar, top-left ---
            _starBox = Counter(root, "StarBox", new Vector2(0f, 1f), new Vector2(40f, -40f), "star", new Color(1f, 0.9f, 0.3f), out _starText);
            // --- Rings, top-right ---
            _ringBox = Counter(root, "RingBox", new Vector2(1f, 1f), new Vector2(-40f, -40f), "ring", new Color(1f, 0.75f, 0.2f), out _ringText);

            // --- Sun track, top-center ---
            _sunTrack = UiFactory.Rect("SunTrack", root);
            UiFactory.Place(_sunTrack, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 140f));
            var horizon = UiFactory.Image("Horizon", _sunTrack, UiFactory.RoundedSprite(), new Color(1f, 1f, 1f, 0.25f));
            UiFactory.Place((RectTransform)horizon.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 6f));
            _sunImage = UiFactory.Image("Sun", _sunTrack, L10n.Icon("sun") ?? PoseMirrorView.DotSprite(), new Color(1f, 0.95f, 0.6f));
            _sunImage.preserveAspect = true;
            _sun = (RectTransform)_sunImage.transform;
            UiFactory.Place(_sun, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));

            // --- Mirror, bottom-right ---
            var mirrorBg = UiFactory.Image("MirrorFrame", root, UiFactory.RoundedSprite(), new Color(1f, 1f, 1f, 0.9f));
            UiFactory.Place((RectTransform)mirrorBg.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), mirrorSize + new Vector2(16f, 16f));
            var mirrorRt = UiFactory.Rect("Mirror", mirrorBg.transform);
            UiFactory.Place(mirrorRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, mirrorSize);
            _mirror = mirrorRt.gameObject.AddComponent<RawImage>();
            _mirror.raycastTarget = false;
            mirrorRt.gameObject.AddComponent<PoseMirrorView>();
            mirrorBg.gameObject.AddComponent<MirrorFrameFader>().Init(mirrorBg);

            // --- Keyboard hint, bottom-left (only when there is no camera) ---
            var hintRt = UiFactory.PhraseImage("KeyboardHint", root, "no_camera", new Vector2(700f, 70f), out _keyboardHint, out _keyboardFallback);
            UiFactory.Place(hintRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(700f, 70f));

            // --- Big centre flash for duck / jump prompts ---
            _flash = UiFactory.Image("Flash", root, null, Color.white);
            _flash.preserveAspect = true;
            UiFactory.Place((RectTransform)_flash.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(380f, 380f));
            _flash.enabled = false;
        }

        private RectTransform Counter(Transform parent, string name, Vector2 anchor, Vector2 pos, string icon, Color tint, out Text text)
        {
            var box = UiFactory.Rect(name, parent);
            UiFactory.Place(box, anchor, anchor, pos, new Vector2(300f, 130f));
            var bg = UiFactory.Image("Bg", box, UiFactory.RoundedSprite(), new Color(0.08f, 0.12f, 0.25f, 0.5f));
            UiFactory.Stretch((RectTransform)bg.transform);
            var ic = UiFactory.Image("Icon", box, L10n.Icon(icon) ?? PoseMirrorView.DotSprite(), tint);
            ic.preserveAspect = true;
            UiFactory.Place((RectTransform)ic.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 0f), new Vector2(100f, 100f));
            text = UiFactory.Text("Count", box, "0", 84, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)text.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(160f, 120f));
            return box;
        }

        private void Start()
        {
            if (GameSession.Instance != null) GameSession.Instance.Changed += OnScoreChanged;
            OnScoreChanged();
        }

        private void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.Changed -= OnScoreChanged;
        }

        private void OnScoreChanged()
        {
            var s = GameSession.Instance;
            if (s == null) return;
            _starText.text = s.Stars.ToString();
            _ringText.text = s.Rings.ToString();
            if (s.Stars != _lastStars) StartCoroutine(Pop(_starBox));
            if (s.Rings != _lastRings) StartCoroutine(Pop(_ringBox));
            _lastStars = s.Stars; _lastRings = s.Rings;
        }

        private IEnumerator Pop(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.18f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void Update()
        {
            // The sun walks from left to right along an arc as the session passes.
            float p = timer != null ? timer.Progress01 : 0f;
            float w = _sunTrack.rect.width;
            float x = Mathf.Lerp(40f, w - 40f, p);
            float y = 20f + Mathf.Sin(p * Mathf.PI) * 80f;
            _sun.anchoredPosition = new Vector2(x, y);
            _sunImage.color = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.3f), Mathf.InverseLerp(0.7f, 1f, p));

            bool camera = PoseProvider.Instance != null && PoseProvider.Instance.UsingCamera;
            _keyboardHint.gameObject.SetActive(!camera);
        }

        /// <summary>Show a big icon (duck / jump) for a moment in the middle of the screen.</summary>
        public void FlashIcon(string iconKey)
        {
            Sprite s = L10n.Icon(iconKey);
            if (s == null) return;
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine), s);
        }

        private IEnumerator FlashRoutine(Sprite s)
        {
            _flash.sprite = s; _flash.enabled = true;
            float t = 0f;
            while (t < 1.6f)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 0.2f ? t / 0.2f : (t > 1.2f ? 1f - (t - 1.2f) / 0.4f : 1f);
                _flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                _flash.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.06f);
                yield return null;
            }
            _flash.enabled = false;
        }

        public void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
        }

        /// <summary>Dims the mirror frame when no camera is used so it does not distract.</summary>
        private class MirrorFrameFader : MonoBehaviour
        {
            private Image _frame;
            public void Init(Image frame) => _frame = frame;
            private void Update()
            {
                bool camera = PoseProvider.Instance != null && PoseProvider.Instance.UsingCamera;
                float target = camera ? 0.9f : 0f;
                var c = _frame.color; c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 2f); _frame.color = c;
            }
        }
    }
}
