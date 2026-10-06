using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vaanam.Core
{
    public enum Language { English = 0, Tamil = 1 }

    /// <summary>
    /// Tamil + English in one place.
    ///
    /// Why sprites and not text components: Unity's built-in text (UGUI and
    /// TextMeshPro) does not shape Indic scripts, so Tamil vowel signs such as
    /// கொ render in the wrong order. The phrases below are pre-rendered to PNG by
    /// Tools/render-text-sprites.ps1 using Windows' own text engine, which shapes
    /// Tamil correctly. The game shows those sprites. The strings here are the
    /// source of truth for that script, for voice lines, and for debug output.
    ///
    /// Child-facing UI uses very few words on purpose. Pictures and voice first.
    /// </summary>
    public static class L10n
    {
        private static Language _current = Language.English;
        public static Language Current
        {
            get => _current;
            set { if (_current == value) return; _current = value; Changed?.Invoke(); }
        }
        public static event Action Changed;

        public static string Code(Language l) => l == Language.Tamil ? "ta" : "en";
        public static string Code() => Code(_current);

        /// <summary>key -> (English, Tamil). Keep keys lowercase_with_underscores.</summary>
        public static readonly Dictionary<string, (string en, string ta)> Phrases = new Dictionary<string, (string, string)>
        {
            // Onboarding: the first 30 seconds
            { "title",        ("VAANAM", "வானம்") },
            { "stand_here",   ("Stand in front of the camera", "கேமரா முன்னால் நில்லுங்கள்") },
            { "i_see_you",    ("I see you!", "உன்னைப் பார்த்தேன்!") },
            { "raise_hands",  ("Raise your hands", "கைகளை உயர்த்துங்கள்") },
            { "hold_it",      ("Hold it...", "அப்படியே வை...") },
            { "fly",          ("Fly!", "பறக்கலாம்!") },
            { "tilt_to_turn", ("Tilt to turn", "சாய்ந்து திரும்பு") },
            { "flap_to_rise", ("Flap to go up", "அடித்து மேலே போ") },

            // Play
            { "great",        ("Great!", "அருமை!") },
            { "wow",          ("Wow!", "ஆஹா!") },
            { "super",        ("Super!", "சூப்பர்!") },
            { "ring",         ("Through the ring!", "வளையம் வழியே!") },
            { "star",         ("A star!", "நட்சத்திரம்!") },
            { "duck",         ("Duck!", "குனி!") },
            { "jump",         ("Jump!", "குதி!") },
            { "cloud",        ("Whoa, a cloud!", "அடடா, மேகம்!") },
            { "two_players",  ("Two friends flying!", "இரண்டு நண்பர்கள் பறக்கிறார்கள்!") },
            { "where_are_you",("Where did you go?", "எங்கே போனாய்?") },
            { "come_back",    ("Come back and fly", "திரும்பி வந்து பற") },
            { "level_done",   ("Level complete!", "நிலை முடிந்தது!") },
            { "next_sky",     ("Next sky...", "அடுத்த வானம்...") },

            // Session end
            { "almost_time",  ("The sun is setting soon", "சூரியன் மறையப் போகிறது") },
            { "sunset",       ("Good flying! Time to rest", "நன்றாகப் பறந்தாய்! ஓய்வு நேரம்") },
            { "now_do_this",  ("Now, go do this:", "இப்போது இதைச் செய்:") },
            { "task_water",   ("Drink a glass of water", "ஒரு டம்ளர் தண்ணீர் குடி") },
            { "task_kolam",   ("Draw a kolam", "ஒரு கோலம் போடு") },
            { "task_hug",     ("Give amma or appa a hug", "அம்மா அப்பாவைக் கட்டிப்பிடி") },
            { "task_stretch", ("Stretch like a bird", "பறவை போல் நீட்டு") },
            { "task_window",  ("Look at the real sky outside", "வெளியே உண்மையான வானத்தைப் பார்") },
            { "task_sing",    ("Sing a song for someone", "யாருக்காவது ஒரு பாட்டு பாடு") },
            { "see_you",      ("See you tomorrow!", "நாளை பார்க்கலாம்!") },

            // Labels
            { "stars",        ("Stars", "நட்சத்திரங்கள்") },
            { "rings",        ("Rings", "வளையங்கள்") },
            { "no_camera",    ("No camera? Press W to fly", "கேமரா இல்லையா? W அழுத்து") },
            { "level_1",      ("Sunrise Fields", "விடியல் வயல்") },
            { "level_2",      ("Cloud Kingdom", "மேக ராஜ்யம்") },
            { "level_3",      ("Pongal Sky", "பொங்கல் வானம்") },
        };

        public static string Text(string key) => Text(key, _current);

        public static string Text(string key, Language lang)
        {
            if (!Phrases.TryGetValue(key, out var p)) return key;
            return lang == Language.Tamil ? p.ta : p.en;
        }

        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, AudioClip> _voiceCache = new Dictionary<string, AudioClip>();

        /// <summary>Pre-rendered phrase picture for the current language, English as fallback.</summary>
        public static Sprite TextSprite(string key)
        {
            string path = $"UI/Text/{Code()}/{key}";
            if (_spriteCache.TryGetValue(path, out var s) && s != null) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null && _current != Language.English) s = Resources.Load<Sprite>($"UI/Text/en/{key}");
            _spriteCache[path] = s;
            return s;
        }

        /// <summary>Voice line for the current language, English as fallback, null if none.</summary>
        public static AudioClip Voice(string key)
        {
            string path = $"Voice/{Code()}/{key}";
            if (_voiceCache.TryGetValue(path, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>(path);
            if (c == null && _current != Language.English) c = Resources.Load<AudioClip>($"Voice/en/{key}");
            _voiceCache[path] = c;
            return c;
        }

        public static Sprite Icon(string name) => Resources.Load<Sprite>("UI/Icons/" + name);
    }
}
