using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Drives time of day: sun/moon direction, sky gradient, fog, sky-light darkening for the voxel shader,
    /// star visibility and the directional light. Time01: 0 = sunrise, 0.25 = noon, 0.5 = sunset, 0.75 = midnight.
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        public float DayLengthSeconds = 600f;
        public float Time01 = 0.03f;
        public bool FastForward;
        public bool Running = true;
        public Light Sun;

        /// <summary>When set (camera under water) fog is overridden with a short blue fog.</summary>
        public bool Underwater;
        public float ViewDistance = 128f;

        public float Daylight { get; private set; }
        public float SkyDarken { get; private set; }
        public Color HorizonColor { get; private set; }
        public Color SkyTint { get; private set; }

        const float Tilt = 0.35f;
        static readonly Vector3 SunAxis = new Vector3(0f, -Mathf.Sin(Tilt), Mathf.Cos(Tilt));

        static readonly int IdSkyDarken = Shader.PropertyToID("_MineSkyDarken");
        static readonly int IdMinBrightness = Shader.PropertyToID("_MineMinBrightness");
        static readonly int IdSkyTint = Shader.PropertyToID("_MineSkyTint");
        static readonly int IdSkyTop = Shader.PropertyToID("_MineSkyTop");
        static readonly int IdSkyHorizon = Shader.PropertyToID("_MineSkyHorizon");
        static readonly int IdSunset = Shader.PropertyToID("_MineSunsetColor");
        static readonly int IdSunDir = Shader.PropertyToID("_MineSunDir");
        static readonly int IdSunAxis = Shader.PropertyToID("_MineSunAxis");
        static readonly int IdStars = Shader.PropertyToID("_MineStars");
        static readonly int IdStarAngle = Shader.PropertyToID("_MineStarAngle");
        static readonly int IdCloudColor = Shader.PropertyToID("_MineCloudColor");

        static readonly Color DayTop = new Color(0.33f, 0.56f, 1.0f);
        static readonly Color DayHorizon = new Color(0.70f, 0.83f, 1.0f);
        static readonly Color NightTop = new Color(0.004f, 0.006f, 0.022f);
        static readonly Color NightHorizon = new Color(0.03f, 0.04f, 0.085f);
        static readonly Color SunsetColor = new Color(1.0f, 0.46f, 0.16f);
        static readonly Color WaterFog = new Color(0.07f, 0.18f, 0.42f);

        public string ClockText
        {
            get
            {
                float hours = (Time01 * 24f + 6f) % 24f;
                int h = (int)hours;
                int m = (int)((hours - h) * 60f);
                return h.ToString("00") + ":" + m.ToString("00");
            }
        }

        void Update()
        {
            if (Running)
            {
                float speed = FastForward ? 45f : 1f;
                Time01 = Mathf.Repeat(Time01 + Time.deltaTime * speed / DayLengthSeconds, 1f);
            }
            Apply();
        }

        static Color ToShader(Color c, bool linear)
        {
            return linear ? c.linear : c;
        }

        public Vector3 SunDirection
        {
            get
            {
                float a = Time01 * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(a), Mathf.Sin(a) * Mathf.Cos(Tilt), Mathf.Sin(a) * Mathf.Sin(Tilt)).normalized;
            }
        }

        public void Apply()
        {
            Vector3 sunDir = SunDirection;
            float h = sunDir.y;
            float day = MathUtil.SmoothStep(-0.2f, 0.25f, h);
            Daylight = day;
            SkyDarken = (1f - day) * 10.5f;

            // Sunset strength: sun close to the horizon.
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(h + 0.03f) / 0.3f);

            Color top = Color.Lerp(NightTop, DayTop, day);
            Color horizon = Color.Lerp(NightHorizon, DayHorizon, day);
            horizon = Color.Lerp(horizon, Color.Lerp(horizon, SunsetColor, 0.55f), sunset * 0.6f);
            HorizonColor = horizon;

            Color tint = Color.Lerp(new Color(0.55f, 0.62f, 0.95f), Color.white, day);
            tint = Color.Lerp(tint, new Color(1f, 0.84f, 0.7f), sunset * 0.45f * day);
            SkyTint = tint;

            // Global shader colours are not converted automatically, but fog colour is: convert by hand so the
            // sky and the fogged terrain meet at exactly the same colour on the horizon.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Shader.SetGlobalFloat(IdSkyDarken, SkyDarken);
            Shader.SetGlobalFloat(IdMinBrightness, 0.02f);
            Shader.SetGlobalColor(IdSkyTint, tint);
            Shader.SetGlobalColor(IdSkyTop, ToShader(top, linear));
            Shader.SetGlobalColor(IdSkyHorizon, ToShader(horizon, linear));
            Color sunsetShader = ToShader(SunsetColor, linear);
            sunsetShader.a = sunset * 0.9f;
            Shader.SetGlobalColor(IdSunset, sunsetShader);
            Shader.SetGlobalVector(IdSunDir, sunDir);
            Shader.SetGlobalVector(IdSunAxis, SunAxis);
            Shader.SetGlobalFloat(IdStars, Mathf.Clamp01(1f - day * 1.6f));
            Shader.SetGlobalFloat(IdStarAngle, Time01 * Mathf.PI * 2f);

            Color cloud = Color.Lerp(new Color(0.09f, 0.1f, 0.15f), Color.white, day);
            cloud = Color.Lerp(cloud, new Color(1f, 0.75f, 0.6f), sunset * 0.4f);
            Shader.SetGlobalColor(IdCloudColor, ToShader(cloud, linear));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            if (Underwater)
            {
                RenderSettings.fogColor = WaterFog * Mathf.Lerp(0.15f, 1f, day);
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.fogEndDistance = 20f;
            }
            else
            {
                RenderSettings.fogColor = horizon;
                RenderSettings.fogStartDistance = ViewDistance * 0.5f;
                RenderSettings.fogEndDistance = ViewDistance * 0.92f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.08f, 0.09f, 0.14f), new Color(0.6f, 0.62f, 0.66f), day);

            Camera cam = Camera.main;
            if (cam != null) cam.backgroundColor = horizon;

            if (Sun != null)
            {
                bool isDay = h > -0.05f;
                Vector3 lightDir = isDay ? sunDir : -sunDir;
                Sun.transform.rotation = Quaternion.LookRotation(-lightDir, Vector3.up);
                Sun.intensity = isDay ? Mathf.Lerp(0.2f, 1.1f, day) : 0.15f;
                Sun.color = Color.Lerp(new Color(0.6f, 0.7f, 1f), Color.Lerp(Color.white, new Color(1f, 0.7f, 0.45f), sunset), day);
            }
        }
    }
}
