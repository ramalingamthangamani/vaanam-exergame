using System;
using UnityEngine;
using Vaanam.Input;

namespace Vaanam.Flight
{
    /// <summary>
    /// Forgiving arcade flight down a wide corridor.
    ///
    /// Why a corridor and not free 3D flight: a 5 year old cannot plan a turn.
    /// The bird always flies forward (+Z). Tilting moves it left/right, raising
    /// the arms makes it climb, folding the arms makes it dive. Levels are then
    /// simply things placed ahead along Z, and two children fly side by side in
    /// the same corridor without ever losing each other.
    ///
    /// Attach to a GameObject with a kinematic Rigidbody and a trigger Collider.
    /// Pickups and obstacles are triggers that look for this component.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour
    {
        /// <summary>0..1 multiplier applied to every bird: GameFlow uses it to slow the world gently.</summary>
        public static float GlobalSpeed = 1f;

        [SerializeField] private int playerIndex;

        [Header("Speed")]
        [SerializeField] private float forwardSpeed = 14f;
        [SerializeField] private float lateralSpeed = 9f;
        [SerializeField] private float lateralSmoothing = 4f;
        [SerializeField] private float corridorHalfWidth = 10f;

        [Header("Vertical")]
        [SerializeField] private float climbSpeed = 6f;
        [SerializeField] private float sinkSpeed = 1.2f;        // wings open, arms level
        [SerializeField] private float foldFallSpeed = 7f;      // wings folded = dive
        [SerializeField] private float verticalSmoothing = 3f;
        [SerializeField] private float minHeight = 1.5f;
        [SerializeField] private float maxHeight = 16f;

        [Header("Gestures")]
        [SerializeField] private float jumpImpulse = 9f;
        [SerializeField] private float flapBoost = 3.5f;
        [SerializeField] private float impulseGravity = 14f;
        [SerializeField] private float duckDrop = 1.2f;

        public int PlayerIndex => playerIndex;
        public FlightInput Input { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool IsDucking { get; private set; }
        /// <summary>True for a short while after a jump, so jump-walls know the child jumped.</summary>
        public bool IsJumping => Time.time - _lastJumpTime < 0.9f;
        public float ForwardSpeed => forwardSpeed;
        public float CorridorHalfWidth => corridorHalfWidth;
        public float MinHeight => minHeight;
        public float MaxHeight => maxHeight;
        /// <summary>Temporary slow-down after bumping something, 0..1.</summary>
        public float BumpSlow { get; private set; } = 1f;

        public event Action OnJump;
        public event Action OnFlap;
        public event Action OnDuckStart;

        private Rigidbody _rb;
        private float _vx, _vy, _impulse;
        private float _lastJumpTime = -10f;
        private bool _active;

        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                gameObject.SetActive(value);
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _active = gameObject.activeSelf;
        }

        public void SetForwardSpeed(float speed) => forwardSpeed = speed;

        /// <summary>Place the bird at the corridor start. x offset lets two birds start apart.</summary>
        public void ResetTo(Vector3 position)
        {
            // Player 2 starts inactive, so Awake may not have run yet.
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.position = position;
            transform.position = position;
            _vx = _vy = _impulse = 0f;
            BumpSlow = 1f;
        }

        /// <summary>A jump triggered by something other than the body, e.g. the voice command "jump".</summary>
        public void ExternalJump()
        {
            _impulse = jumpImpulse;
            _lastJumpTime = Time.time;
            OnJump?.Invoke();
        }

        public void Bump(float strength = 0.5f)
        {
            BumpSlow = Mathf.Min(BumpSlow, 1f - strength);
            _impulse = Mathf.Min(_impulse, -2f);
        }

        private void FixedUpdate()
        {
            FlightInput input = PoseProvider.Instance != null && playerIndex < PoseProvider.Instance.Players.Length
                ? PoseProvider.Instance.Players[playerIndex]
                : FlightInput.Untracked;
            Input = input;

            float dt = Time.fixedDeltaTime;

            // If tracking drops, glide straight instead of stopping dead.
            float roll = input.IsTracked ? input.Roll : 0f;
            bool wingsOpen = !input.IsTracked || input.WingsOpen;
            float lift = input.IsTracked ? input.Lift : 0f;

            // --- gestures -----------------------------------------------------------
            if (input.Jump) { _impulse = jumpImpulse; _lastJumpTime = Time.time; OnJump?.Invoke(); }
            if (input.Flap) { _impulse = Mathf.Max(_impulse, 0f) + flapBoost; OnFlap?.Invoke(); }
            bool duck = input.IsTracked && input.Duck;
            if (duck && !IsDucking) OnDuckStart?.Invoke();
            IsDucking = duck;

            // --- lateral ------------------------------------------------------------
            float targetVx = roll * lateralSpeed;
            _vx = Mathf.Lerp(_vx, targetVx, lateralSmoothing * dt);

            // --- vertical -----------------------------------------------------------
            float targetVy;
            if (!wingsOpen) targetVy = -foldFallSpeed;
            else if (lift > 0.05f) targetVy = Mathf.Lerp(-sinkSpeed, climbSpeed, lift);
            else targetVy = -sinkSpeed;
            if (duck) targetVy -= duckDrop * 2f;
            _vy = Mathf.Lerp(_vy, targetVy, verticalSmoothing * dt);

            // Impulses (jump / flap / bump) decay like a little gravity.
            if (Mathf.Abs(_impulse) > 0.01f) _impulse = Mathf.MoveTowards(_impulse, 0f, impulseGravity * dt);

            BumpSlow = Mathf.MoveTowards(BumpSlow, 1f, 0.6f * dt);
            float speed = forwardSpeed * GlobalSpeed * BumpSlow;

            Vector3 velocity = new Vector3(_vx, _vy + _impulse, speed);
            Vector3 next = _rb.position + velocity * dt;
            next.x = Mathf.Clamp(next.x, -corridorHalfWidth, corridorHalfWidth);
            next.y = Mathf.Clamp(next.y, minHeight, maxHeight);
            if (next.x <= -corridorHalfWidth || next.x >= corridorHalfWidth) _vx = 0f;
            if (next.y <= minHeight && _vy < 0f) _vy = 0f;

            Velocity = (next - _rb.position) / dt;
            _rb.MovePosition(next);
        }
    }
}
