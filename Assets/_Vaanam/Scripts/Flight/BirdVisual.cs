using UnityEngine;
using Vaanam.World;

namespace Vaanam.Flight
{
    /// <summary>
    /// A cheerful bird built from primitives at runtime, so the game has a
    /// character before any art exists. Its wings copy the child's arms
    /// directly: arms up, wings up. That one-to-one mirroring is what makes a
    /// child feel "I am inside the game" within seconds.
    ///
    /// Replace later by dropping a real model under <see cref="visualRoot"/> and
    /// assigning its wing bones to leftWing / rightWing.
    /// </summary>
    [RequireComponent(typeof(FlightController))]
    public class BirdVisual : MonoBehaviour
    {
        [Header("Rig (auto-built if empty)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform body;

        [Header("Look")]
        [SerializeField] private int colorIndex;
        [SerializeField] private float maxBankDegrees = 45f;
        [SerializeField] private float yawFollowDegrees = 12f;
        [SerializeField] private float wingUpDegrees = 55f;
        [SerializeField] private float wingDownDegrees = -20f;
        [SerializeField] private float wingSmoothing = 10f;
        [SerializeField] private float bankSmoothing = 5f;

        private FlightController _flight;
        private float _bank, _wing, _fold, _squash = 1f, _bob;
        private ParticleSystem _puff;

        private void Awake()
        {
            _flight = GetComponent<FlightController>();
            if (visualRoot == null) Build();
            _flight.OnFlap += () => Puff(6);
            _flight.OnJump += () => Puff(12);
        }

        /// <summary>Which player colour this bird wears. 0 = orange parrot, 1 = blue kingfisher.</summary>
        public void SetColorIndex(int index)
        {
            colorIndex = index;
            if (visualRoot != null) { Destroy(visualRoot.gameObject); visualRoot = null; Build(); }
        }

        private void Build()
        {
            var palette = VaanamPalette.Get();
            Material primary = palette.BirdPrimary(colorIndex);
            Material secondary = palette.BirdSecondary(colorIndex);

            visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(transform, false);

            body = Prim(PrimitiveType.Sphere, "Body", visualRoot, Vector3.zero, new Vector3(1f, 0.85f, 1.5f), primary);
            Prim(PrimitiveType.Sphere, "Belly", body, new Vector3(0f, -0.22f, 0.15f), new Vector3(0.75f, 0.6f, 0.8f), secondary);
            var head = Prim(PrimitiveType.Sphere, "Head", visualRoot, new Vector3(0f, 0.45f, 0.75f), Vector3.one * 0.62f, primary);
            Prim(PrimitiveType.Cube, "Beak", head, new Vector3(0f, -0.05f, 0.5f), new Vector3(0.22f, 0.16f, 0.45f), palette.Beak);
            foreach (float side in new[] { -1f, 1f })
            {
                var eyeWhite = Prim(PrimitiveType.Sphere, "Eye", head, new Vector3(0.22f * side, 0.12f, 0.36f), Vector3.one * 0.22f, palette.EyeWhite);
                Prim(PrimitiveType.Sphere, "Pupil", eyeWhite, new Vector3(0f, 0f, 0.3f), Vector3.one * 0.55f, palette.Eye);
            }
            Prim(PrimitiveType.Cube, "Tail", visualRoot, new Vector3(0f, 0.1f, -0.95f), new Vector3(0.5f, 0.08f, 0.7f), secondary)
                .localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Crest", head, new Vector3(0f, 0.35f, -0.05f), new Vector3(0.18f, 0.3f, 0.18f), palette.Beak);

            leftWing = WingPivot("LeftWing", -1f, primary, secondary);
            rightWing = WingPivot("RightWing", 1f, primary, secondary);

            // Little puff of sparkles for flaps and jumps.
            var puffGo = new GameObject("Puff");
            puffGo.transform.SetParent(visualRoot, false);
            puffGo.transform.localPosition = new Vector3(0f, 0f, -0.8f);
            _puff = puffGo.AddComponent<ParticleSystem>();
            var main = _puff.main; main.startLifetime = 0.6f; main.startSpeed = 2.5f; main.startSize = 0.18f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, secondary.color); main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.loop = false;
            var em = _puff.emission; em.rateOverTime = 0f;
            var sh = _puff.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var r = _puff.GetComponent<ParticleSystemRenderer>(); r.material = palette.Particle;
        }

        private Transform WingPivot(string name, float side, Material primary, Material secondary)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(visualRoot, false);
            pivot.localPosition = new Vector3(0.45f * side, 0.15f, 0.1f);
            var wing = Prim(PrimitiveType.Cube, "WingMesh", pivot, new Vector3(1.1f * side, 0f, 0f), new Vector3(2.2f, 0.08f, 0.9f), primary);
            Prim(PrimitiveType.Cube, "WingTip", wing, new Vector3(0.35f * side, 0f, -0.25f), new Vector3(0.4f, 1.2f, 0.6f), secondary);
            return pivot;
        }

        private static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>(); rend.sharedMaterial = mat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        private void Puff(int count)
        {
            if (_puff != null) _puff.Emit(count);
        }

        private void Update()
        {
            var input = _flight.Input;
            float dt = Time.deltaTime;

            // Bank and yaw follow the roll input.
            float roll = input.IsTracked ? input.Roll : 0f;
            _bank = Mathf.Lerp(_bank, -roll * maxBankDegrees, bankSmoothing * dt);
            float yaw = roll * yawFollowDegrees;

            // Wings mirror the arms: lift 0 = level, lift 1 = high. Folded = swept back.
            float targetWing = input.IsTracked ? Mathf.Lerp(wingDownDegrees * 0.3f, wingUpDegrees, input.Lift) : 5f;
            bool open = !input.IsTracked || input.WingsOpen;
            _wing = Mathf.Lerp(_wing, targetWing, wingSmoothing * dt);
            _fold = Mathf.Lerp(_fold, open ? 0f : 1f, 8f * dt);

            // Squash when ducking, stretch when jumping.
            float targetSquash = _flight.IsDucking ? 0.6f : (_flight.IsJumping ? 1.15f : 1f);
            _squash = Mathf.Lerp(_squash, targetSquash, 10f * dt);

            // A gentle bob so the bird never looks frozen.
            _bob += dt * (open ? 2.5f : 6f);
            float bobY = Mathf.Sin(_bob) * 0.06f;

            visualRoot.localRotation = Quaternion.Euler(0f, yaw, _bank);
            visualRoot.localPosition = new Vector3(0f, bobY, 0f);
            visualRoot.localScale = new Vector3(1f, _squash, 1f);

            float wingZ = _wing * (1f - _fold) + -60f * _fold;
            float wingY = 70f * _fold;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, -wingY, wingZ);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, wingY, -wingZ);
        }
    }
}
