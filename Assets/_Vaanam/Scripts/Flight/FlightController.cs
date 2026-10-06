using UnityEngine;
using Vaanam.Input;

namespace Vaanam.Flight
{
    /// <summary>
    /// Minimal arcade flight. Always moves forward; roll banks and turns;
    /// lift raises the player; the player sinks slowly with wings open and
    /// falls faster with wings closed.
    /// Deliberately forgiving: a child flapping badly must still fly.
    ///
    /// Attach to a GameObject with a Rigidbody and a Collider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float forwardSpeed = 12f;
        [SerializeField] private float turnRateDegPerSec = 60f;

        [Header("Vertical")]
        [SerializeField] private float climbSpeed = 6f;
        [SerializeField] private float sinkSpeed = 2f;        // wings open, no lift
        [SerializeField] private float fallSpeed = 5f;        // wings closed
        [SerializeField] private float minHeight = 1f;
        [SerializeField] private float maxHeight = 40f;

        [Header("Visuals")]
        [SerializeField] private Transform visualModel;        // child mesh that banks
        [SerializeField] private float maxBankDegrees = 50f;
        [SerializeField] private float bankSmoothing = 6f;

        private Rigidbody _rb;
        private float _heading;   // degrees around Y
        private float _bank;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _heading = transform.eulerAngles.y;
        }

        private void FixedUpdate()
        {
            FlightInput input = PoseProvider.Instance != null
                ? PoseProvider.Instance.Current
                : new FlightInput { WingsOpen = true, IsTracked = true };

            // If tracking drops, glide straight instead of stopping dead.
            float roll = input.IsTracked ? input.Roll : 0f;
            bool wingsOpen = !input.IsTracked || input.WingsOpen;
            float lift = input.IsTracked ? input.Lift : 0f;

            _heading += roll * turnRateDegPerSec * Time.fixedDeltaTime;
            Quaternion rot = Quaternion.Euler(0f, _heading, 0f);

            float vertical;
            if (!wingsOpen) vertical = -fallSpeed;
            else if (lift > 0.05f) vertical = Mathf.Lerp(-sinkSpeed, climbSpeed, lift);
            else vertical = -sinkSpeed;

            Vector3 velocity = rot * Vector3.forward * forwardSpeed + Vector3.up * vertical;
            Vector3 next = _rb.position + velocity * Time.fixedDeltaTime;
            next.y = Mathf.Clamp(next.y, minHeight, maxHeight);

            _rb.MovePosition(next);
            _rb.MoveRotation(rot);

            if (visualModel != null)
            {
                _bank = Mathf.Lerp(_bank, -roll * maxBankDegrees, bankSmoothing * Time.fixedDeltaTime);
                visualModel.localRotation = Quaternion.Euler(0f, 0f, _bank);
            }
        }
    }
}
