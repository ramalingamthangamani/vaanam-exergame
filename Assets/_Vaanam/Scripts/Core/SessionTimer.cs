using UnityEngine;
using UnityEngine.Events;

namespace Vaanam.Core
{
    /// <summary>
    /// Ends the play session gently after a fixed time. This is a core design
    /// feature, not an afterthought: the game decides when to stop so the child
    /// does not have to. Hook the events up to the sunset screen later.
    /// </summary>
    public class SessionTimer : MonoBehaviour
    {
        [SerializeField] private float sessionMinutes = 15f;
        [SerializeField] private float warningMinutesBeforeEnd = 2f;

        public UnityEvent OnWarning;
        public UnityEvent OnSessionEnd;

        public float Remaining { get; private set; }
        public bool Ended { get; private set; }

        private bool _warned;

        private void Start() => Remaining = sessionMinutes * 60f;

        private void Update()
        {
            if (Ended) return;
            Remaining -= Time.deltaTime;

            if (!_warned && Remaining <= warningMinutesBeforeEnd * 60f)
            {
                _warned = true;
                OnWarning?.Invoke();
            }
            if (Remaining <= 0f)
            {
                Ended = true;
                OnSessionEnd?.Invoke();
            }
        }
    }
}
