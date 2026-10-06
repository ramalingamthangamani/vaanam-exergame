using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vaanam.Core;

namespace Vaanam.UI
{
    /// <summary>
    /// The calm ending. The sky has already turned to dusk; this overlay fades
    /// in with "Good flying! Time to rest", the stars collected, and ONE
    /// real-world task ("Drink a glass of water", "Draw a kolam"...). It stays
    /// up until a parent starts a new session. There is deliberately no
    /// "play again" button a child could press.
    /// </summary>
    public class SunsetScreen : MonoBehaviour
    {
        private static readonly string[] Tasks = { "task_water", "task_kolam", "task_hug", "task_stretch", "task_window", "task_sing" };

        private RectTransform _root;
        private CanvasGroup _group;
        private Image _title, _task, _taskIcon, _seeYou, _nowDo;
        private Text _titleFb, _taskFb, _seeYouFb, _nowDoFb, _starsText;
        private string _taskKey = "task_water";

        public bool IsShowing => _group != null && _group.alpha > 0.5f;

        private void Awake()
        {
            _root = UiFactory.Rect("SunsetRoot", transform);
            UiFactory.Stretch(_root);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;

            var bg = UiFactory.Image("Gradient", _root, UiFactory.GradientSprite(new Color(0.95f, 0.55f, 0.35f, 0.92f), new Color(0.25f, 0.15f, 0.4f, 0.96f)), Color.white);
            UiFactory.Stretch((RectTransform)bg.transform);

            // A big setting sun peeking over the bottom edge, behind everything else.
            var sun = UiFactory.Image("BigSun", _root, L10n.Icon("sun") ?? Input.PoseMirrorView.DotSprite(), new Color(1f, 0.75f, 0.35f, 0.45f));
            sun.preserveAspect = true;
            UiFactory.Place((RectTransform)sun.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(900f, 900f));

            var titleRt = UiFactory.PhraseImage("Title", _root, "sunset", new Vector2(1500f, 180f), out _title, out _titleFb);
            UiFactory.Place(titleRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1500f, 180f));

            // Stars collected this session.
            var starRow = UiFactory.Rect("StarRow", _root);
            UiFactory.Place(starRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(500f, 140f));
            var starIcon = UiFactory.Image("Star", starRow, L10n.Icon("star") ?? Input.PoseMirrorView.DotSprite(), new Color(1f, 0.9f, 0.3f));
            starIcon.preserveAspect = true;
            UiFactory.Place((RectTransform)starIcon.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            _starsText = UiFactory.Text("Count", starRow, "0", 110, Color.white);
            UiFactory.Place((RectTransform)_starsText.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(300f, 140f));

            var nowRt = UiFactory.PhraseImage("NowDo", _root, "now_do_this", new Vector2(900f, 90f), out _nowDo, out _nowDoFb);
            UiFactory.Place(nowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(900f, 90f));

            _taskIcon = UiFactory.Image("TaskIcon", _root, null, Color.white);
            _taskIcon.preserveAspect = true;
            UiFactory.Place((RectTransform)_taskIcon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(230f, 230f));

            var taskRt = UiFactory.PhraseImage("Task", _root, _taskKey, new Vector2(1400f, 150f), out _task, out _taskFb);
            UiFactory.Place(taskRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(1400f, 150f));

            var seeRt = UiFactory.PhraseImage("SeeYou", _root, "see_you", new Vector2(800f, 80f), out _seeYou, out _seeYouFb);
            UiFactory.Place(seeRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 80f));

            L10n.Changed += Refresh;
        }

        private void OnDestroy() => L10n.Changed -= Refresh;

        private void Refresh()
        {
            UiFactory.SetPhrase(_title, _titleFb, "sunset");
            UiFactory.SetPhrase(_nowDo, _nowDoFb, "now_do_this");
            UiFactory.SetPhrase(_task, _taskFb, _taskKey);
            UiFactory.SetPhrase(_seeYou, _seeYouFb, "see_you");
        }

        public void Show(int stars)
        {
            _taskKey = Tasks[Random.Range(0, Tasks.Length)];
            _starsText.text = stars.ToString();
            Sprite icon = L10n.Icon(_taskKey);
            _taskIcon.sprite = icon; _taskIcon.enabled = icon != null;
            Refresh();
            StopAllCoroutines();
            StartCoroutine(FadeTo(1f, 3f));
            StartCoroutine(Speak());
        }

        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeTo(0f, 1f));
        }

        private IEnumerator Speak()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            AudioDirector.Instance?.Say("sunset", important: true);
            yield return new WaitForSecondsRealtime(4f);
            AudioDirector.Instance?.Say("now_do_this", important: true);
            yield return new WaitForSecondsRealtime(2.5f);
            AudioDirector.Instance?.Say(_taskKey, important: true);
            yield return new WaitForSecondsRealtime(5f);
            AudioDirector.Instance?.Say("see_you", important: true);
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            float start = _group.alpha, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            _group.alpha = target;
        }
    }
}
