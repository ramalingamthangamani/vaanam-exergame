using UnityEngine;

namespace Vaanam.World
{
    /// <summary>
    /// Tiny particle helper: one shared ParticleSystem that can burst anywhere
    /// in any colour. Enough for stars, rings and celebrations.
    /// </summary>
    public static class Effects
    {
        private static ParticleSystem _burst;
        private static ParticleSystem _confetti;

        private static ParticleSystem Create(string name, float size, float speed, float life, bool gravity)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.loop = false;
            main.maxParticles = 600; main.gravityModifier = gravity ? 0.6f : 0f;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            ps.GetComponent<ParticleSystemRenderer>().material = VaanamPalette.Get().Particle;
            return ps;
        }

        /// <summary>Sparkle burst for pickups.</summary>
        public static void Burst(Vector3 position, Color color, int count = 24)
        {
            if (_burst == null) _burst = Create("FX Burst", 0.3f, 5f, 0.8f, false);
            var p = new ParticleSystem.EmitParams { position = position, startColor = color };
            _burst.Emit(p, count);
        }

        /// <summary>Slow falling confetti for celebrations.</summary>
        public static void Confetti(Vector3 position, int count = 150)
        {
            if (_confetti == null) _confetti = Create("FX Confetti", 0.35f, 7f, 2.5f, true);
            Color[] colors = { new Color(1f, 0.4f, 0.4f), new Color(1f, 0.85f, 0.3f), new Color(0.4f, 0.9f, 0.5f), new Color(0.4f, 0.7f, 1f), new Color(0.9f, 0.5f, 1f) };
            for (int i = 0; i < count; i++)
            {
                var p = new ParticleSystem.EmitParams { position = position + Random.insideUnitSphere * 1.5f, startColor = colors[i % colors.Length] };
                _confetti.Emit(p, 1);
            }
        }
    }
}
