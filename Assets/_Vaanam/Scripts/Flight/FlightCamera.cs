using UnityEngine;

namespace Vaanam.Flight
{
    /// <summary>
    /// Third-person chase camera that follows the midpoint of all active birds.
    /// With two players it pulls back so both stay on screen. Soft smoothing
    /// keeps the picture calm even when a child jiggles.
    /// </summary>
    public class FlightCamera : MonoBehaviour
    {
        [SerializeField] private FlightController[] players;
        [SerializeField] private Vector3 offset = new Vector3(0f, 3.5f, -11f);
        [SerializeField] private float lookAhead = 9f;
        [SerializeField] private float positionSmoothing = 4f;
        [SerializeField] private float rotationSmoothing = 4f;
        [SerializeField] private float baseFov = 62f;
        [SerializeField] private float twoPlayerPullBack = 4f;

        private Camera _cam;
        private Vector3 _vel;

        public void SetPlayers(FlightController[] p) => players = p;

        private void Awake() => _cam = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (players == null || players.Length == 0) return;

            Vector3 sum = Vector3.zero; int n = 0; float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in players)
            {
                if (p == null || !p.Active) continue;
                sum += p.transform.position; n++;
                minX = Mathf.Min(minX, p.transform.position.x); maxX = Mathf.Max(maxX, p.transform.position.x);
            }
            if (n == 0) return;
            Vector3 mid = sum / n;
            float spread = n > 1 ? Mathf.Clamp01((maxX - minX) / 16f) : 0f;

            Vector3 target = mid + offset + Vector3.back * (spread * twoPlayerPullBack);
            target.x *= 0.6f;                       // the camera follows sideways only a little, so the corridor stays readable
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, target, positionSmoothing * dt);

            Vector3 lookAt = new Vector3(mid.x * 0.5f, mid.y, mid.z + lookAhead);
            Quaternion rot = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSmoothing * dt);

            if (_cam != null) _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, baseFov + spread * 6f, 2f * dt);
        }
    }
}
