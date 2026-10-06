using System;
using UnityEngine;

namespace Vaanam.Core
{
    /// <summary>
    /// Makes every sound in the game from math, so the project has no audio
    /// dependencies and nothing to license. Replace any clip with a real
    /// recording later by dropping it into AudioDirector's inspector slots.
    ///
    /// Musical choice: everything uses the pentatonic scale of raga Mohanam
    /// (C D E G A). Any two notes from it sound pleasant together, so random
    /// sparkles never clash with the background music.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 44100;

        // Mohanam / major pentatonic frequencies, starting at C4.
        private static readonly float[] Scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f, 659.25f, 783.99f, 880.00f };

        private static AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Env(float t, float attack, float decay, float total)
        {
            if (t < attack) return t / attack;
            float rel = (t - attack) / Mathf.Max(0.001f, total - attack);
            return Mathf.Exp(-rel * decay);
        }

        private static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        private static float Tri(float f, float t) => 2f * Mathf.Abs(2f * (f * t - Mathf.Floor(f * t + 0.5f))) - 1f;

        /// <summary>Soft bell: flying through a ring.</summary>
        public static AudioClip Chime(int note = 5)
        {
            float f = Scale[Mathf.Clamp(note, 0, Scale.Length - 1)];
            return Make("Chime", 0.9f, t => Env(t, 0.005f, 5f, 0.9f) * (0.5f * Sine(f, t) + 0.25f * Sine(f * 2f, t) + 0.12f * Sine(f * 3f, t)));
        }

        /// <summary>Quick upward three-note sparkle: picking up a star.</summary>
        public static AudioClip Sparkle()
        {
            return Make("Sparkle", 0.5f, t =>
            {
                int idx = Mathf.Min(2, (int)(t / 0.09f));
                float f = Scale[5 + idx * 2];
                float lt = t - idx * 0.09f;
                return 0.45f * Env(lt, 0.004f, 6f, 0.3f) * (Sine(f, t) + 0.4f * Sine(f * 2f, t));
            });
        }

        /// <summary>Breathy whoosh for a wing flap, from shaped noise.</summary>
        public static AudioClip Whoosh()
        {
            var rnd = new System.Random(7);
            float last = 0f;
            return Make("Whoosh", 0.45f, t =>
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, white, 0.12f);          // crude low-pass
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                return 0.35f * env * env * last * 3f;
            });
        }

        /// <summary>Soft low thud for bumping a cloud. Never harsh.</summary>
        public static AudioClip Bump()
        {
            return Make("Bump", 0.35f, t => 0.5f * Env(t, 0.003f, 8f, 0.35f) * Sine(110f - 60f * t, t));
        }

        /// <summary>Rising sweep for a jump.</summary>
        public static AudioClip Jump()
        {
            return Make("Jump", 0.35f, t => 0.35f * Env(t, 0.005f, 4f, 0.35f) * Tri(300f + 500f * t, t));
        }

        /// <summary>Short falling blip for a duck.</summary>
        public static AudioClip Duck()
        {
            return Make("Duck", 0.25f, t => 0.3f * Env(t, 0.005f, 6f, 0.25f) * Sine(500f - 300f * t, t));
        }

        /// <summary>Happy ascending arpeggio for finishing a level.</summary>
        public static AudioClip Fanfare()
        {
            int[] notes = { 0, 2, 3, 4, 5, 7 };
            const float step = 0.13f;
            return Make("Fanfare", notes.Length * step + 1.2f, t =>
            {
                float v = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float lt = t - i * step;
                    if (lt < 0f) continue;
                    float f = Scale[notes[i]];
                    v += 0.28f * Env(lt, 0.005f, i == notes.Length - 1 ? 2f : 5f, 1.4f) * (Sine(f, t) + 0.3f * Sine(f * 2f, t));
                }
                return v;
            });
        }

        /// <summary>Two gentle notes when the game first sees the child.</summary>
        public static AudioClip Hello()
        {
            return Make("Hello", 0.8f, t =>
            {
                float a = Env(t, 0.01f, 4f, 0.8f) * Sine(Scale[2], t) * 0.35f;
                float b = t > 0.18f ? Env(t - 0.18f, 0.01f, 4f, 0.6f) * Sine(Scale[4], t) * 0.35f : 0f;
                return a + b;
            });
        }

        /// <summary>
        /// A looping background pad with a slow bell melody. 16 bars-ish, seamless.
        /// Day: brighter and quicker. Sunset: slower, lower, softer.
        /// </summary>
        public static AudioClip MusicLoop(bool sunset)
        {
            float length = sunset ? 24f : 16f;
            float bpm = sunset ? 52f : 72f;
            float beat = 60f / bpm;
            int[][] chords = sunset
                ? new[] { new[] { 0, 2, 4 }, new[] { 4, 7, 9 }, new[] { 3, 5, 7 }, new[] { 0, 3, 5 } }
                : new[] { new[] { 0, 2, 4 }, new[] { 3, 5, 7 }, new[] { 4, 7, 9 }, new[] { 0, 2, 5 } };
            int[] melody = sunset
                ? new[] { 4, -1, 2, -1, 0, -1, -1, -1, 3, -1, 2, -1, 0, -1, -1, -1 }
                : new[] { 5, -1, 7, 5, 4, -1, 2, -1, 3, -1, 5, -1, 4, -1, -1, -1 };
            float chordLen = length / chords.Length;
            float melStep = length / melody.Length;
            float octave = sunset ? 0.5f : 1f;
            var rnd = new System.Random(3);

            return Make(sunset ? "MusicSunset" : "MusicDay", length, t =>
            {
                float v = 0f;
                // Pad: three notes per chord, soft attack, crossfaded at boundaries so the loop is seamless.
                int ci = (int)(t / chordLen) % chords.Length;
                float ct = (t % chordLen) / chordLen;
                float fade = Mathf.Sin(ct * Mathf.PI);                 // 0 at edges, 1 in the middle
                foreach (int n in chords[ci])
                {
                    float f = Scale[n] * octave;
                    v += 0.06f * fade * (Sine(f, t) + 0.5f * Tri(f * 0.5f, t) * 0.4f + 0.3f * Sine(f * 2.0f + 0.5f, t));
                }
                int pi = (ci + 1) % chords.Length;                      // next chord fading in
                foreach (int n in chords[pi])
                    v += 0.06f * Mathf.Max(0f, ct - 0.5f) * 2f * (1f - fade) * Sine(Scale[n] * octave, t);

                // Bell melody.
                int mi = (int)(t / melStep) % melody.Length;
                if (melody[mi] >= 0)
                {
                    float lt = t - mi * melStep;
                    float f = Scale[melody[mi]] * (sunset ? 1f : 2f);
                    v += 0.10f * Env(lt, 0.01f, 3f, melStep) * (Sine(f, t) + 0.4f * Sine(f * 2f, t));
                }

                // Very slow breathing on the whole thing.
                v *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / length * 2f);
                return v;
            });
        }
    }
}
