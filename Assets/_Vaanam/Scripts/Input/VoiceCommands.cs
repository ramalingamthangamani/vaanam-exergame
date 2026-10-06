using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace Vaanam.Input
{
    /// <summary>
    /// Simple voice actions using the Windows built-in keyword recognizer.
    /// Nothing is recorded or uploaded; Windows listens for a handful of words.
    ///
    /// Why keywords and not free speech: a 5 year old shouting "FLY!" is exactly
    /// the kind of input a keyword recognizer handles well, and it works offline.
    /// Tamil words are included as transliterations; Windows' English recognizer
    /// catches some of them ("paRa" = fly) but this is best-effort.
    ///
    /// Any system without speech support simply disables this component.
    /// </summary>
    public class VoiceCommands : MonoBehaviour
    {
        public enum Command { Go, Jump, Duck, Stop }

        [SerializeField] private bool enableVoice = true;

        /// <summary>Fired on the main thread when a word is heard.</summary>
        public event Action<Command> OnCommand;

        public bool IsListening { get; private set; }
        public string LastHeard { get; private set; } = "";

        private readonly Dictionary<string, Command> _phrases = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase)
        {
            { "fly", Command.Go }, { "go", Command.Go }, { "start", Command.Go }, { "para", Command.Go }, { "poo", Command.Go },
            { "jump", Command.Jump }, { "up", Command.Jump }, { "hop", Command.Jump }, { "kuthi", Command.Jump },
            { "duck", Command.Duck }, { "down", Command.Duck }, { "kuni", Command.Duck },
            { "stop", Command.Stop }, { "pause", Command.Stop }, { "nillu", Command.Stop },
        };

        private readonly Queue<Command> _pending = new Queue<Command>();
        private readonly object _lock = new object();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer _recognizer;
#endif

        private void Start()
        {
            if (!enableVoice) return;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var keys = new List<string>(_phrases.Keys);
                _recognizer = new KeywordRecognizer(keys.ToArray(), ConfidenceLevel.Low);
                _recognizer.OnPhraseRecognized += OnPhrase;
                _recognizer.Start();
                IsListening = true;
                Debug.Log("[Vaanam] Voice commands listening: " + string.Join(", ", keys));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Vaanam] Voice commands unavailable: " + e.Message);
                IsListening = false;
            }
#else
            Debug.Log("[Vaanam] Voice commands are only available on Windows.");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void OnPhrase(PhraseRecognizedEventArgs args)
        {
            if (_phrases.TryGetValue(args.text, out Command cmd))
            {
                lock (_lock) { _pending.Enqueue(cmd); LastHeard = args.text; }
            }
        }
#endif

        private void Update()
        {
            // Deliver on the main thread so listeners can touch Unity objects.
            lock (_lock)
            {
                while (_pending.Count > 0) OnCommand?.Invoke(_pending.Dequeue());
            }
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer != null)
            {
                try { if (_recognizer.IsRunning) _recognizer.Stop(); _recognizer.Dispose(); } catch { }
                _recognizer = null;
            }
#endif
        }
    }
}
