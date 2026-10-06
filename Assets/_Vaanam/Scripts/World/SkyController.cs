using UnityEngine;

namespace Vaanam.World
{
    /// <summary>
    /// Drives the procedural skybox, the sun light and the fog. Each level sets
    /// its own daytime colours; the session end slides everything into a warm
    /// sunset and then dusk. All changes are smooth so nothing ever "pops".
    /// </summary>
    public class SkyController : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [Tooltip("A material using the Skybox/Procedural shader. Created by the scene builder.")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private float blendSpeed = 0.6f;
        [SerializeField] private float fogStart = 80f;
        [SerializeField] private float fogEnd = 320f;

        private Material _sky;
        private Color _skyTint, _ground, _fog, _sunColor, _tSkyTint, _tGround, _tFog, _tSunColor;
        private float _elevation, _tElevation, _exposure = 1.2f, _tExposure = 1.2f, _thickness = 1f, _tThickness = 1f;
        private float _sunset;    // 0 = day, 1 = full dusk
        private LevelDefinition _level;

        private void Awake()
        {
            if (sun == null) sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            Shader sh = skyboxMaterial != null ? skyboxMaterial.shader : Shader.Find("Skybox/Procedural");
            _sky = skyboxMaterial != null ? new Material(skyboxMaterial) : (sh != null ? new Material(sh) : null);
            if (_sky != null)
            {
                RenderSettings.skybox = _sky;
                _sky.SetFloat("_SunSize", 0.06f);
                _sky.SetFloat("_SunSizeConvergence", 4f);
            }
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            _skyTint = _tSkyTint = new Color(0.35f, 0.65f, 1f);
            _ground = _tGround = new Color(0.85f, 0.93f, 1f);
            _fog = _tFog = _tGround;
            _sunColor = _tSunColor = Color.white;
            _elevation = _tElevation = 50f;
            Apply();
        }

        public void ApplyLevel(LevelDefinition level)
        {
            _level = level;
            RefreshTargets();
        }

        /// <summary>0 = normal day, 0.5 = golden sunset, 1 = calm dusk.</summary>
        public void SetSunset(float progress)
        {
            _sunset = Mathf.Clamp01(progress);
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            Color top = _level != null ? _level.skyTop : new Color(0.35f, 0.65f, 1f);
            Color horizon = _level != null ? _level.skyHorizon : new Color(0.85f, 0.93f, 1f);
            Color sunC = _level != null ? _level.sunColor : Color.white;
            Color fog = _level != null ? _level.fogColor : horizon;
            float elev = _level != null ? _level.sunElevation : 50f;

            // Sunset blend: first golden, then purple dusk.
            Color goldenTop = new Color(0.95f, 0.55f, 0.35f), goldenHorizon = new Color(1f, 0.75f, 0.45f);
            Color duskTop = new Color(0.25f, 0.18f, 0.45f), duskHorizon = new Color(0.75f, 0.45f, 0.55f);
            float g = Mathf.Clamp01(_sunset * 2f), d = Mathf.Clamp01(_sunset * 2f - 1f);
            top = Color.Lerp(Color.Lerp(top, goldenTop, g), duskTop, d);
            horizon = Color.Lerp(Color.Lerp(horizon, goldenHorizon, g), duskHorizon, d);
            fog = Color.Lerp(Color.Lerp(fog, goldenHorizon, g), duskHorizon, d);
            sunC = Color.Lerp(Color.Lerp(sunC, new Color(1f, 0.7f, 0.45f), g), new Color(0.7f, 0.5f, 0.7f), d);
            elev = Mathf.Lerp(Mathf.Lerp(elev, 10f, g), 2f, d);

            _tSkyTint = top; _tGround = horizon; _tFog = fog; _tSunColor = sunC; _tElevation = elev;
            _tExposure = Mathf.Lerp(1.25f, 0.7f, d);
            _tThickness = Mathf.Lerp(1f, 1.6f, g);
        }

        private void Update()
        {
            float k = blendSpeed * Time.unscaledDeltaTime;
            _skyTint = Color.Lerp(_skyTint, _tSkyTint, k);
            _ground = Color.Lerp(_ground, _tGround, k);
            _fog = Color.Lerp(_fog, _tFog, k);
            _sunColor = Color.Lerp(_sunColor, _tSunColor, k);
            _elevation = Mathf.Lerp(_elevation, _tElevation, k);
            _exposure = Mathf.Lerp(_exposure, _tExposure, k);
            _thickness = Mathf.Lerp(_thickness, _tThickness, k);
            Apply();
        }

        private void Apply()
        {
            if (_sky != null)
            {
                _sky.SetColor("_SkyTint", _skyTint);
                _sky.SetColor("_GroundColor", _ground);
                _sky.SetFloat("_Exposure", _exposure);
                _sky.SetFloat("_AtmosphereThickness", _thickness);
            }
            if (sun != null)
            {
                sun.color = _sunColor;
                sun.intensity = Mathf.Lerp(0.6f, 1.3f, Mathf.InverseLerp(0f, 50f, _elevation));
                sun.transform.rotation = Quaternion.Euler(_elevation, 35f, 0f);
            }
            RenderSettings.fogColor = _fog;
            RenderSettings.ambientSkyColor = _skyTint * 0.9f;
            RenderSettings.ambientEquatorColor = _ground * 0.8f;
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.4f, 0.3f);
        }
    }
}
