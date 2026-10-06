using System;
using UnityEngine;

namespace Vaanam.Core
{
    /// <summary>
    /// Everything we remember between sessions, saved as one small JSON string in
    /// PlayerPrefs. No accounts, no cloud: the parent dashboard reads this.
    ///
    /// Design rule: nothing here can be "lost". Stars only go up. There are no
    /// streaks to break.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        private const string Key = "vaanam.save.v1";

        // Progress (only ever grows)
        public int TotalStars;
        public int TotalRings;
        public int SessionsPlayed;
        public float TotalMinutes;
        public int HighestLevelReached;   // 1-based
        public string LastPlayedDate = "";

        // Settings the parent can change
        public int Language = (int)Core.Language.English;
        public int SessionMinutes = 15;    // 10, 15 or 20
        public bool VoiceEnabled = true;
        public bool MusicEnabled = true;
        public string CameraName = "";

        private static SaveData _current;
        public static SaveData Current => _current ?? (_current = Load());

        public static SaveData Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var d = JsonUtility.FromJson<SaveData>(json);
                    if (d != null) return d;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Vaanam] Save data unreadable, starting fresh: " + e.Message); }
            return new SaveData();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void RecordSession(int stars, int rings, float minutes, int levelReached)
        {
            TotalStars += stars;
            TotalRings += rings;
            TotalMinutes += minutes;
            SessionsPlayed += 1;
            HighestLevelReached = Mathf.Max(HighestLevelReached, levelReached);
            LastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd");
            Save();
        }

        /// <summary>Resets progress counters only. Settings are kept.</summary>
        public void ResetProgress()
        {
            TotalStars = 0; TotalRings = 0; SessionsPlayed = 0; TotalMinutes = 0f;
            HighestLevelReached = 0; LastPlayedDate = "";
            Save();
        }
    }
}
