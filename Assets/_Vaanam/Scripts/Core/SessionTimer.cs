using UnityEngine;
using UnityEngine.Events;

namespace Vaanam.Core
{
    /// <summary>
    /// Ends the play session gently after a fixed time. This is a core design
    /// feature, not an afterthought: the game decides when to stop so the child
    /// does not have to. The sun in the HUD moves across the sky with
    /// <see cref="Progress01"/>, so the child can SEE time passing without numbers.
    ///
    /// The timer only runs while <see cref="Running"/> is true (i.e. during real
    /// play), so standing around in the tutorial does not eat into flying time.
    /// </summary>
    public class SessionTimer : MonoBehaviour
    {
        [Tooltip("Default length. Overridden by the parent setting in SaveData at start.")]
        [SerializeField] private float sessionMinutes = 15f;
        [SerializeField] private float warningMinutesBeforeEnd = 2f;

        public UnityEvent OnWarning;
        public UnityEvent OnSessionEnd;

        public float Remaining { get; private set; }
        public float Total { get; private set; }
        public bool Ended { get; private set; }
        public bool Running { get; set; }

        /// <summary>0 at the start of the session, 1 at the end.</summary>
        public float Progress01 => Total > 0f ? 1f - Mathf.Clamp01(Remaining / Total) : 0f;
        public float ElapsedMinutes => (Total - Remaining) / 60f;
        public bool InWarning => _warned;

        private bool _warned;

        private void Start()
        {
            sessionMinutes = Mathf.Clamp(SaveData.Current.SessionMinutes, 5, 30);
            Restart();
        }

        public void Restart()
        {
            Total = sessionMinutes * 60f;
            Remaining = Total;
            Ended = false;
            _warned = false;
            Running = false;
        }

        private void Update()
        {
            if (Ended || !Running) return;
            Remaining -= Time.unscaledDeltaTime;   // pausing the world must not pause the session

            if (!_warned && Remaining <= warningMinutesBeforeEnd * 60f)
            {
                _warned = true;
                OnWarning?.Invoke();
            }
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                Ended = true;
                Running = false;
                OnSessionEnd?.Invoke();
            }
        }
    }
}
