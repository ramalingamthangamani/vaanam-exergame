using System;
using UnityEngine;

namespace Vaanam.Core
{
    /// <summary>
    /// Score for the CURRENT play session. Shared between both players because
    /// the game is co-operative: two friends fill one star jar together.
    /// Nothing here ever goes down.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public int Stars { get; private set; }
        public int Rings { get; private set; }
        public int LevelIndex { get; set; }          // 0-based, current level
        public int LevelsCompleted { get; private set; }
        public float StartedAt { get; private set; }

        /// <summary>Fired whenever a number changes. The HUD listens.</summary>
        public event Action Changed;
        /// <summary>Fired with the world position so effects can play there. (what, position, playerIndex)</summary>
        public event Action<string, Vector3, int> Collected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            StartedAt = Time.unscaledTime;
        }

        public void AddStar(Vector3 at, int player)
        {
            Stars++;
            Changed?.Invoke();
            Collected?.Invoke("star", at, player);
        }

        public void AddRing(Vector3 at, int player)
        {
            Rings++;
            Changed?.Invoke();
            Collected?.Invoke("ring", at, player);
        }

        public void LevelCompleted()
        {
            LevelsCompleted++;
            Changed?.Invoke();
        }

        public void ResetSession()
        {
            Stars = 0; Rings = 0; LevelIndex = 0; LevelsCompleted = 0;
            StartedAt = Time.unscaledTime;
            Changed?.Invoke();
        }

        /// <summary>Write this session into the permanent save file.</summary>
        public void Commit(float minutesPlayed)
        {
            SaveData.Current.RecordSession(Stars, Rings, minutesPlayed, LevelIndex + 1);
        }
    }
}
