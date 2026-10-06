using UnityEngine;
using UnityEngine.UI;

namespace Vaanam.Input
{
    /// <summary>
    /// The little "mirror" in the corner: shows the webcam picture with the
    /// tracked skeleton drawn on top. This is the Phase 1 proof that the webcam
    /// opens, the body is found and the landmarks make sense. It also tells the
    /// child "the game sees you", which matters more than any instruction text.
    ///
    /// Attach to a RawImage. Everything else (dots and bones) is created in code.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class PoseMirrorView : MonoBehaviour
    {
        [SerializeField] private Color[] playerColors = { new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.7f, 1f) };
        [SerializeField] private float dotSize = 18f;
        [SerializeField] private float boneWidth = 6f;
        [SerializeField] private bool hideWhenKeyboard = true;

        private RawImage _image;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Skeleton[] _skeletons;
        private static Sprite _dotSprite;

        private class Skeleton
        {
            public RectTransform[] Dots = new RectTransform[7];
            public RectTransform[] Bones = new RectTransform[6];
            public GameObject Root;
        }

        // Pairs of joint indices that get a bone between them.
        // 0 nose, 1 Lsh, 2 Rsh, 3 Lwr, 4 Rwr, 5 Lhip, 6 Rhip
        private static readonly int[,] BonePairs = { { 1, 2 }, { 1, 3 }, { 2, 4 }, { 1, 5 }, { 2, 6 }, { 5, 6 } };

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _rect = GetComponent<RectTransform>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _image.color = Color.white;
        }

        private void Start()
        {
            int n = PoseProvider.Instance != null ? PoseProvider.Instance.MaxPlayers : 2;
            _skeletons = new Skeleton[n];
            for (int i = 0; i < n; i++) _skeletons[i] = BuildSkeleton(i);
        }

        private Skeleton BuildSkeleton(int player)
        {
            var sk = new Skeleton();
            sk.Root = new GameObject("Skeleton" + player, typeof(RectTransform));
            sk.Root.transform.SetParent(transform, false);
            var rt = (RectTransform)sk.Root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

            Color c = playerColors[player % playerColors.Length];
            for (int b = 0; b < sk.Bones.Length; b++)
            {
                var go = new GameObject("Bone", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(sk.Root.transform, false);
                var img = go.GetComponent<Image>(); img.color = new Color(c.r, c.g, c.b, 0.85f); img.raycastTarget = false;
                var r = (RectTransform)go.transform; r.pivot = new Vector2(0f, 0.5f); r.anchorMin = r.anchorMax = Vector2.zero;
                sk.Bones[b] = r;
            }
            for (int d = 0; d < sk.Dots.Length; d++)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(sk.Root.transform, false);
                var img = go.GetComponent<Image>(); img.sprite = DotSprite(); img.color = c; img.raycastTarget = false;
                var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = Vector2.zero;
                r.sizeDelta = Vector2.one * (d == 0 ? dotSize * 1.4f : dotSize);
                sk.Dots[d] = r;
            }
            return sk;
        }

        private void LateUpdate()
        {
            var provider = PoseProvider.Instance;
            if (provider == null || _skeletons == null) return;

            IPoseSource src = provider.ActiveSource;
            bool camera = src != null && src.PreviewTexture != null;
            _image.texture = camera ? src.PreviewTexture : null;
            _image.uvRect = camera && src.PreviewIsMirrored ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            _image.color = camera ? Color.white : new Color(0f, 0f, 0f, 0.35f);

            bool visible = camera || !hideWhenKeyboard;
            _group.alpha = visible ? 1f : 0f;
            if (!visible) return;

            Vector2 size = _rect.rect.size;
            for (int i = 0; i < _skeletons.Length; i++)
            {
                bool has = provider.TryGetLastPose(i, out BodyPose pose) && provider.Players[i].IsTracked;
                _skeletons[i].Root.SetActive(has);
                if (!has) continue;
                Draw(_skeletons[i], pose, size);
            }
        }

        private void Draw(Skeleton sk, BodyPose p, Vector2 size)
        {
            // Pose x,y are 0..1 with y down. UI anchoredPosition is from bottom-left, y up.
            Vector2[] pts =
            {
                ToUi(p.Nose, size), ToUi(p.LeftShoulder, size), ToUi(p.RightShoulder, size),
                ToUi(p.LeftWrist, size), ToUi(p.RightWrist, size), ToUi(p.LeftHip, size), ToUi(p.RightHip, size)
            };
            for (int d = 0; d < 7; d++) sk.Dots[d].anchoredPosition = pts[d];
            for (int b = 0; b < sk.Bones.Length; b++)
            {
                Vector2 a = pts[BonePairs[b, 0]], c = pts[BonePairs[b, 1]];
                Vector2 dir = c - a;
                var r = sk.Bones[b];
                r.anchoredPosition = a;
                r.sizeDelta = new Vector2(dir.magnitude, boneWidth);
                r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        private static Vector2 ToUi(Vector2 n, Vector2 size) => new Vector2(n.x * size.x, (1f - n.y) * size.y);

        /// <summary>A soft white circle, made in code so we need no art file.</summary>
        public static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01((1f - d) * 6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return _dotSprite;
        }
    }
}
