using System.Collections;
using UnityEngine;
using Vaanam.Core;
using Vaanam.Flight;

namespace Vaanam.World
{
    /// <summary>
    /// A golden ring to fly through. Big, forgiving trigger; turns green and
    /// chimes when passed. In the Pongal level it is dressed as a kolam with
    /// white dots around it.
    /// </summary>
    public class Ring : MonoBehaviour
    {
        public const float Radius = 3.2f;

        private Renderer _torus;
        private bool _passed;
        private float _spin;

        public static Ring Create(Transform parent, Vector3 position, bool kolam)
        {
            var palette = VaanamPalette.Get();
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var ring = go.AddComponent<Ring>();
            var torus = MeshBuilders.MeshObject("Torus", MeshBuilders.Torus(Radius, 0.28f), kolam ? palette.Kolam : palette.RingGold, go.transform, Vector3.zero);
            ring._torus = torus.GetComponent<Renderer>();

            if (kolam)
            {
                // Kolam dots: small white spheres in two circles around the ring.
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    MeshBuilders.Prim(PrimitiveType.Sphere, "Dot", go.transform, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Radius + 0.7f), Vector3.one * 0.22f, palette.Kolam);
                    if (i % 2 == 0)
                        MeshBuilders.Prim(PrimitiveType.Sphere, "Dot", go.transform, new Vector3(Mathf.Cos(a + 0.26f), Mathf.Sin(a + 0.26f), 0f) * (Radius + 1.3f), Vector3.one * 0.16f, palette.RingGold);
                }
            }

            // Trigger is a thin box filling the ring: passing anywhere inside counts.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Radius * 2f, Radius * 2f, 1.2f);
            return ring;
        }

        private void Update()
        {
            _spin += Time.deltaTime * (_passed ? 120f : 25f);
            transform.localRotation = Quaternion.Euler(0f, 0f, _spin);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_passed) return;
            var bird = other.GetComponentInParent<FlightController>();
            if (bird == null) return;
            _passed = true;

            _torus.sharedMaterial = VaanamPalette.Get().RingPassed;
            GameSession.Instance?.AddRing(transform.position, bird.PlayerIndex);
            AudioDirector.Instance?.Play(AudioDirector.Sfx.Chime, 1f, Random.Range(0.95f, 1.1f));
            Effects.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 40);
            StartCoroutine(Pulse());
        }

        private IEnumerator Pulse()
        {
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float s = 1f + Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI) * 0.25f;
                transform.localScale = Vector3.one * s;
                yield return null;
            }
            transform.localScale = Vector3.one;
        }
    }
}
