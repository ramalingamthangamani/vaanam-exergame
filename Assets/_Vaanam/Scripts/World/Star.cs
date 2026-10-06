using System.Collections;
using UnityEngine;
using Vaanam.Core;
using Vaanam.Flight;

namespace Vaanam.World
{
    /// <summary>
    /// A collectible star. Spins, bobs, and pops with a sparkle when touched.
    /// Trigger radius is generous on purpose: near misses count.
    /// </summary>
    public class Star : MonoBehaviour
    {
        private float _phase;
        private bool _taken;
        private Transform _mesh;

        public static Star Create(Transform parent, Vector3 position, int pitchIndex = 0)
        {
            var palette = VaanamPalette.Get();
            var go = new GameObject("Star");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var star = go.AddComponent<Star>();
            star._phase = Random.value * 10f;
            star._pitchIndex = pitchIndex;
            var mesh = MeshBuilders.MeshObject("StarMesh", MeshBuilders.Star(0.9f, 0.42f, 0.3f), palette.Star, go.transform, Vector3.zero);
            star._mesh = mesh.transform;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.7f;
            return star;
        }

        private int _pitchIndex;

        private void Update()
        {
            if (_taken) return;
            _phase += Time.deltaTime;
            _mesh.localRotation = Quaternion.Euler(0f, _phase * 90f, 0f);
            _mesh.localPosition = new Vector3(0f, Mathf.Sin(_phase * 2f) * 0.25f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            var bird = other.GetComponentInParent<FlightController>();
            if (bird == null) return;
            _taken = true;

            GameSession.Instance?.AddStar(transform.position, bird.PlayerIndex);
            AudioDirector.Instance?.Play(AudioDirector.Sfx.Sparkle, 0.9f, 1f + 0.08f * _pitchIndex);
            Effects.Burst(transform.position, new Color(1f, 0.95f, 0.5f), 18);
            StartCoroutine(PopAndDie());
        }

        private IEnumerator PopAndDie()
        {
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float s = 1f + t * 4f;
                _mesh.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t / 0.25f) * s;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
