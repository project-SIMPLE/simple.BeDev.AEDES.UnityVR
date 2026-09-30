using UnityEngine;

/// <summary>
/// The daylight over the Module 3 neighbourhood - sun, sky, ambient and haze - driven by the turn
/// clock, so that time visibly passes.
///
/// A turn is three days of the calendar and three minutes of play, and every one of them used to be
/// the same noon: the sun stood at one fixed angle from Day 0 to Day 54. Indoors was worse. The
/// scene's ambient was in Skybox mode and M3Surroundings swaps in the Fantasy sky at runtime, so the
/// bright blue sky was the rooms' only fill and every wall, floor and ceiling came out cyan. This
/// owns all of that now:
///   - the sun's elevation, azimuth, colour and intensity (its shadow settings are left alone),
///   - the ambient as a low, warm trilight (sky / horizon / ground), which URP's Lit multiplies into
///     every surface the sun does not reach - the cream of the house walls indoors,
///   - the haze colour, and the skybox's exposure, tint and turn, on a runtime copy of the material
///     so the asset is never dirtied,
///   - and, through M3InteriorLight and M3LightShafts, the light that comes in at the openings.
/// Morning at the start of a turn, bright midday, warm late afternoon in the last stretch. At the
/// next turn's start it sweeps back to morning in under a second, so "three days pass" is felt; the
/// handover screen holds the clock, so the morning then holds with it. Never below the horizon:
/// there is no night in the module.
///
/// A trilight ambient is read from RenderSettings every frame by the shaders, so it needs no
/// DynamicGI.UpdateEnvironment, and that call is never made here. Added at runtime by
/// M3NeighbourhoodBuilder, like M3DistanceCulling, so the scene file has nothing to merge.
/// </summary>
public class M3TimeOfDay : MonoBehaviour
{
    /// <summary>One moment of the day: everything this class sets, at one point of the turn.</summary>
    [System.Serializable]
    public struct Moment
    {
        [Tooltip("Sun height above the horizon, degrees. Never 0: there is no night in the module.")]
        public float elevation;
        [Tooltip("Where the sun stands, as the light's Y rotation. 150 is the angle the scene was built with.")]
        public float azimuth;
        public Color sun;
        public float sunIntensity;
        [Tooltip("Trilight ambient: what upward-facing surfaces get in the shade.")]
        public Color ambientSky;
        [Tooltip("Trilight ambient: what walls get in the shade.")]
        public Color ambientEquator;
        [Tooltip("Trilight ambient: what ceilings and undersides get. Never black.")]
        public Color ambientGround;
        public Color haze;
        public float skyExposure;
        [Tooltip("Skybox tint; 0.5 grey is neutral on the panoramic shader.")]
        public Color skyTint;

        public static Moment Lerp(Moment a, Moment b, float t) => new Moment
        {
            elevation = Mathf.Lerp(a.elevation, b.elevation, t),
            azimuth = Mathf.Lerp(a.azimuth, b.azimuth, t),
            sun = Color.Lerp(a.sun, b.sun, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
            ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
            ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
            haze = Color.Lerp(a.haze, b.haze, t),
            skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t),
            skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
        };
    }

    // The sun crosses the lane on a diagonal: it rises beyond the x = 0 end on the paddy side of the
    // near row and sets beyond the far end on the paddy side of the far row, so over a turn each row
    // gets direct sun through its own openings for part of the day, and the house shadows swing
    // right across the lane. The ambient is kept only a little warm: indoors that is what makes the
    // walls cream rather than blue, outdoors the shade must still read as shade.
    [Header("Start of the turn")]
    [SerializeField] private Moment morning = new Moment
    {
        elevation = 30f, azimuth = 62f, sun = new Color(1f, 0.93f, 0.82f), sunIntensity = 1.0f,
        ambientSky = new Color(0.56f, 0.59f, 0.64f), ambientEquator = new Color(0.50f, 0.46f, 0.40f),
        ambientGround = new Color(0.28f, 0.25f, 0.22f), haze = new Color(0.78f, 0.83f, 0.88f),
        skyExposure = 0.95f, skyTint = new Color(0.5f, 0.5f, 0.5f),
    };

    [Header("Middle of the turn")]
    [SerializeField] private Moment midday = new Moment
    {
        elevation = 60f, azimuth = 150f, sun = new Color(1f, 0.98f, 0.94f), sunIntensity = 1.15f,
        ambientSky = new Color(0.62f, 0.64f, 0.68f), ambientEquator = new Color(0.56f, 0.52f, 0.46f),
        ambientGround = new Color(0.32f, 0.29f, 0.25f), haze = new Color(0.76f, 0.83f, 0.90f),
        skyExposure = 1.05f, skyTint = new Color(0.5f, 0.5f, 0.5f),
    };

    [Header("End of the turn")]
    [SerializeField] private Moment afternoon = new Moment
    {
        elevation = 24f, azimuth = 238f, sun = new Color(1f, 0.80f, 0.58f), sunIntensity = 1.0f,
        ambientSky = new Color(0.60f, 0.57f, 0.55f), ambientEquator = new Color(0.56f, 0.46f, 0.36f),
        ambientGround = new Color(0.30f, 0.25f, 0.20f), haze = new Color(0.88f, 0.78f, 0.64f),
        skyExposure = 0.85f, skyTint = new Color(0.56f, 0.49f, 0.42f),
    };

    [Header("Handover")]
    [Tooltip("Seconds the sun takes to sweep back from the end of one turn to the morning of the next.")]
    [SerializeField] private float sweepSeconds = 0.8f;

    [Tooltip("Turn the sky panorama with the sun, so the clouds are not nailed in place all day.")]
    [SerializeField] private bool skyTurnsWithSun = true;

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");

    public static M3TimeOfDay Instance { get; private set; }

    /// <summary>How far through the day the scene is showing: 0 morning, 0.5 midday, 1 late afternoon.</summary>
    public float Progress => shown;
    /// <summary>0 until midday, rising to 1 at the end of the turn: how golden the light indoors should be.</summary>
    public float Warmth => Mathf.Clamp01((shown - 0.5f) * 2f);
    public Moment Now { get; private set; }
    public Light Sun => sun;
    /// <summary>Which way the sunlight travels (down and away from the sun).</summary>
    public Vector3 SunDirection => sun != null ? sun.transform.forward : Vector3.down;

    private Light sun;
    private Material skyAsset, skyInstance;
    private float shown = -1f;

    private void Awake()
    {
        Instance = this;
        // The two things daylight does indoors. Each is its own component so it can be tuned or
        // switched off on its own.
        if (GetComponent<M3InteriorLight>() == null) gameObject.AddComponent<M3InteriorLight>();
        if (GetComponent<M3LightShafts>() == null) gameObject.AddComponent<M3LightShafts>();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
    }

    private void LateUpdate()
    {
        if (sun == null) sun = FindSun();

        float target = TargetProgress();
        if (shown < 0f) shown = target;
        else
        {
            // Forward, the clock only ever moves a fraction of a percent a frame. A jump backwards
            // is the next turn starting: sweep the sun back over the handover rather than snapping.
            float rate = target < shown ? 1f / Mathf.Max(sweepSeconds, 0.05f) : 1f;
            shown = Mathf.MoveTowards(shown, target, rate * Time.deltaTime);
        }

        Now = shown < 0.5f ? Moment.Lerp(morning, midday, shown * 2f)
                           : Moment.Lerp(midday, afternoon, (shown - 0.5f) * 2f);
        Apply(Now);
    }

    /// <summary>Where the turn clock says the day is. Frozen with the clock while a handover is up.</summary>
    private float TargetProgress()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null || !s.Running || s.TurnSecondsTotal <= 0f) return Mathf.Max(shown, 0f);
        return Mathf.Clamp01(1f - s.TurnSecondsRemaining / s.TurnSecondsTotal);
    }

    private void Apply(Moment m)
    {
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(m.elevation, m.azimuth, 0f);
            sun.color = m.sun;
            sun.intensity = m.sunIntensity;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = m.ambientSky;
        RenderSettings.ambientEquatorColor = m.ambientEquator;
        RenderSettings.ambientGroundColor = m.ambientGround;
        RenderSettings.fogColor = m.haze;

        ApplySky(m);
    }

    /// <summary>
    /// Exposure and tint go on a copy of whatever skybox is set, never on the asset: M2Manager writes
    /// straight to its skybox material and leaves the asset changed on disk after every play.
    /// M3Surroundings assigns the skybox when it builds, possibly after this has already run, so the
    /// copy is remade whenever the material underneath changes.
    /// </summary>
    private void ApplySky(Moment m)
    {
        var current = RenderSettings.skybox;
        if (current == null) return;
        if (current != skyInstance)
        {
            if (skyInstance != null) Destroy(skyInstance);
            skyAsset = current;
            skyInstance = new Material(current) { name = current.name + " (time of day)" };
            RenderSettings.skybox = skyInstance;
        }
        if (skyInstance.HasProperty(ExposureId)) skyInstance.SetFloat(ExposureId, m.skyExposure);
        if (skyInstance.HasProperty(TintId)) skyInstance.SetColor(TintId, m.skyTint);
        if (skyTurnsWithSun && skyInstance.HasProperty(RotationId))
            skyInstance.SetFloat(RotationId, Mathf.Repeat(m.azimuth - midday.azimuth, 360f));
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;
        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (skyInstance != null)
        {
            if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = skyAsset;
            Destroy(skyInstance);
        }
    }
}
