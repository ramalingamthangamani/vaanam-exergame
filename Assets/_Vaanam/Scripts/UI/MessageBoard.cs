using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vaanam.Core;

namespace Vaanam.UI
{
    /// <summary>
    /// The one big message in the middle of the screen: a picture of a phrase
    /// (Tamil or English) with an optional icon above it. Pops in, optionally
    /// hides itself after a few seconds. There is never more than one message.
    /// </summary>
    public class MessageBoard : MonoBehaviour
    {
        [SerializeField] private Vector2 phraseSize = new Vector2(1400f, 200f);
        [SerializeField] private Vector2 iconSize = new Vector2(320f, 320f);

        private RectTransform _root;
        private Image _phrase, _icon, _bg;
        private Text _fallback;
        private CanvasGroup _group;
        private Coroutine _hideRoutine;
        private string _currentKey;

        public bool IsShowing => _group != null && _group.alpha > 0.01f;

        private void Awake()
        {
            _root = UiFactory.Rect("MessageRoot", transform);
            UiFactory.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1500f, 620f));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f; _group.blocksRaycasts = false;

            _bg = UiFactory.Image("Pill", _root, UiFactory.RoundedSprite(), new Color(0.08f, 0.12f, 0.25f, 0.55f));
            UiFactory.Place((RectTransform)_bg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(phraseSize.x + 80f, phraseSize.y + 40f));

            var phraseRt = UiFactory.PhraseImage("Phrase", _root, "", phraseSize, out _phrase, out _fallback);
            UiFactory.Place(phraseRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), phraseSize);

            _icon = UiFactory.Image("Icon", _root, null, Color.white);
            _icon.preserveAspect = true;
            UiFactory.Place((RectTransform)_icon.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, phraseSize.y + 60f), iconSize);

            L10n.Changed += Refresh;
        }

        private void OnDestroy() => L10n.Changed -= Refresh;

        private void Refresh()
        {
            if (!string.IsNullOrEmpty(_currentKey)) UiFactory.SetPhrase(_phrase, _fallback, _currentKey);
        }

        /// <summary>Show a phrase by L10n key. seconds = 0 keeps it until Hide().</summary>
        public void Show(string key, string icon = null, float seconds = 0f)
        {
            _currentKey = key;
            UiFactory.SetPhrase(_phrase, _fallback, key);
            Sprite ic = string.IsNullOrEmpty(icon) ? null : L10n.Icon(icon);
            _icon.sprite = ic; _icon.enabled = ic != null;
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(Animate(seconds));
        }

        public void Hide()
        {
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(FadeOut());
        }

        private IEnumerator Animate(float seconds)
        {
            float t = 0f;
            _group.alpha = 1f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.35f;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;        // little overshoot
                _root.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, k)) * s;
                yield return null;
            }
            _root.localScale = Vector3.one;
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(seconds);
                yield return FadeOut();
            }
        }

        private IEnumerator FadeOut()
        {
            float start = _group.alpha;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, 0f, t / 0.3f);
                yield return null;
            }
            _group.alpha = 0f;
            _currentKey = null;
        }
    }
}
