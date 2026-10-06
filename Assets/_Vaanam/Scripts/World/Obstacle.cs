using UnityEngine;
using Vaanam.Core;
using Vaanam.Flight;

namespace Vaanam.World
{
    /// <summary>
    /// Soft obstacles. Nothing here ever ends the game: bumping into something
    /// slows the bird for a moment, plays a soft thud and the voice gives a hint.
    ///
    ///  Cloud     a puffy cluster floating in the corridor. Fly around it.
    ///  DuckBar   a bar across the corridor at flying height. Duck under it
    ///            (crouch), or simply fly lower.
    ///  JumpWall  a wall rising from the ground. Jump (hop in place), or fly higher.
    ///
    /// Each obstacle calls out its hint as the child approaches, so Phase 4
    /// (jump / duck) teaches itself.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        public enum Kind { Cloud, DuckBar, JumpWall }

        public Kind kind;
        private float _height;       // bar height or wall top
        private bool _warned;
        private FlightController[] _players;
        private Transform _wobble;
        private float _wobbleT;
        private readonly System.Collections.Generic.HashSet<int> _handled = new System.Collections.Generic.HashSet<int>();

        public static Obstacle Create(Kind kind, Transform parent, Vector3 position, float corridorHalfWidth, FlightController[] players)
        {
            var palette = VaanamPalette.Get();
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ob = go.AddComponent<Obstacle>();
            ob.kind = kind;
            ob._players = players;

            switch (kind)
            {
                case Kind.Cloud:
                {
                    var puff = new GameObject("Puffs").transform; puff.SetParent(go.transform, false); ob._wobble = puff;
                    var rnd = new System.Random(position.GetHashCode());
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 p = new Vector3((float)rnd.NextDouble() * 4f - 2f, (float)rnd.NextDouble() * 1.6f - 0.8f, (float)rnd.NextDouble() * 2f - 1f);
                        float s = 1.8f + (float)rnd.NextDouble() * 1.6f;
                        MeshBuilders.Prim(PrimitiveType.Sphere, "Puff", puff, p, Vector3.one * s, palette.Cloud);
                    }
                    var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 2.4f;
                    break;
                }
                case Kind.DuckBar:
                {
                    ob._height = position.y;
                    float w = corridorHalfWidth * 2f + 4f;
                    MeshBuilders.Prim(PrimitiveType.Cube, "Bar", go.transform, Vector3.zero, new Vector3(w, 0.7f, 0.7f), palette.DuckBar);
                    // Stripes so it reads as "a thing to avoid"
                    for (int i = -3; i <= 3; i++)
                        MeshBuilders.Prim(PrimitiveType.Cube, "Stripe", go.transform, new Vector3(i * w / 7f, 0f, 0f), new Vector3(0.9f, 0.8f, 0.8f), palette.Kolam);
                    foreach (float side in new[] { -1f, 1f })
                        MeshBuilders.Prim(PrimitiveType.Cylinder, "Post", go.transform, new Vector3(side * (w / 2f), -position.y / 2f, 0f), new Vector3(0.5f, position.y / 2f, 0.5f), palette.DuckBar);
                    var col = go.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(corridorHalfWidth * 2f, 2.6f, 1.6f);
                    break;
                }
                case Kind.JumpWall:
                {
                    ob._height = position.y;                      // position.y is the wall TOP
                    float w = corridorHalfWidth * 2f + 4f;
                    float h = position.y;
                    var wall = MeshBuilders.Prim(PrimitiveType.Cube, "Wall", go.transform, new Vector3(0f, -h / 2f, 0f), new Vector3(w, h, 1f), palette.JumpWall);
                    for (int i = -2; i <= 2; i++)
                        MeshBuilders.Prim(PrimitiveType.Cube, "Brick", go.transform, new Vector3(i * w / 5f, -0.4f, 0f), new Vector3(1.2f, 0.7f, 1.1f), palette.Kolam);
                    var col = go.AddComponent<BoxCollider>(); col.isTrigger = true;
                    col.center = new Vector3(0f, -h / 2f, 0f); col.size = new Vector3(corridorHalfWidth * 2f, h, 1.6f);
                    break;
                }
            }
            return ob;
        }

        private void Update()
        {
            if (_wobble != null)
            {
                _wobbleT += Time.deltaTime;
                _wobble.localPosition = new Vector3(0f, Mathf.Sin(_wobbleT * 1.3f) * 0.3f, 0f);
            }

            // Approach hint ~2 seconds before arrival.
            if (!_warned && _players != null)
            {
                foreach (var p in _players)
                {
                    if (p == null || !p.Active) continue;
                    float dz = transform.position.z - p.transform.position.z;
                    if (dz > 0f && dz < p.ForwardSpeed * 2.2f)
                    {
                        _warned = true;
                        string key = kind == Kind.Cloud ? "cloud" : kind == Kind.DuckBar ? "duck" : "jump";
                        AudioDirector.Instance?.Say(key);
                        if (kind != Kind.Cloud) UI.HudController.Instance?.FlashIcon(key);
                        break;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var bird = other.GetComponentInParent<FlightController>();
            if (bird == null || _handled.Contains(bird.PlayerIndex)) return;
            _handled.Add(bird.PlayerIndex);

            bool success;
            switch (kind)
            {
                case Kind.DuckBar: success = bird.IsDucking || bird.transform.position.y < _height - 1.2f; break;
                case Kind.JumpWall: success = bird.IsJumping || bird.transform.position.y > _height + 0.3f; break;
                default: success = false; break;
            }

            if (success)
            {
                AudioDirector.Instance?.Play(AudioDirector.Sfx.Whoosh);
                AudioDirector.Instance?.Say(Random.value < 0.5f ? "super" : "great");
                Effects.Burst(bird.transform.position, Color.white, 12);
            }
            else
            {
                bird.Bump(kind == Kind.Cloud ? 0.45f : 0.35f);
                AudioDirector.Instance?.Play(AudioDirector.Sfx.Bump);
                if (kind == Kind.Cloud) AudioDirector.Instance?.Say("cloud");
                Effects.Burst(bird.transform.position, new Color(1f, 1f, 1f, 0.6f), 10);
            }
        }
    }
}
