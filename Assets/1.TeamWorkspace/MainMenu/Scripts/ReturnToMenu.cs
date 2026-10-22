using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The way out of a module and back to <see cref="AppFlow.MenuScene"/>, in two forms:
///   - press the left controller's MENU button twice (the second within <see cref="ArmSeconds"/>).
///     That is the button a Quest player already reaches for to "go to the menu". Twice, because
///     one stray press must not throw away a child's progress; the first press says what the
///     second will do;
///   - hold B / Y for <see cref="HoldSeconds"/>. B is used because the modules only ever tap it
///     (Module 1's "press anything to fly again", Module 3's "no"); none of them holds it.
/// No module on main reads the menu button.
///
/// It installs itself into every scene through <see cref="RuntimeInitializeOnLoadMethod"/> and
/// survives scene loads, so neither module scene nor any module script has to be touched to gain
/// it. That is deliberate: the module scenes are tens of thousands of lines of YAML and every edit
/// to one is a merge conflict waiting for whoever is working on the other branch.
/// </summary>
public class ReturnToMenu : MonoBehaviour
{
    public const float HoldSeconds = 1.5f;
    public const float ArmSeconds = 3f;
    const float ShowAfterSeconds = 0.3f;

    static ReturnToMenu instance;

    readonly XRButtons input = new XRButtons();
    Transform cam;
    RectTransform root;
    CanvasGroup group;
    Image track, fill;
    TextMeshProUGUI label;
    bool leaving;
    float armedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("[Return To Menu]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ReturnToMenu>();
    }

    void Awake()
    {
        BuildIndicator();
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Installed after the first scene has loaded, so its sceneLoaded has already gone by.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    // This object outlives every scene, so per-scene state is reset on each load; in particular
    // the "already leaving" latch, or only the first trip back per app launch would work.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        leaving = false;
        armedUntil = 0f;
    }

    void Update()
    {
        // Nothing to go back to from the menu itself.
        if (AppFlow.InMenu || leaving)
        {
            group.alpha = 0f;
            return;
        }

        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        input.Update();
        float now = Time.unscaledTime;

        if (input.MenuDown)
        {
            if (now < armedUntil) { Leave(); return; }
            armedUntil = now + ArmSeconds;
            input.Rumble();
        }
        if (input.SecondaryHeld >= HoldSeconds) { Leave(); return; }

        // A quick tap of B is an answer in Module 3 ("no"), so the bar only shows once the press is
        // clearly a hold, rather than flashing up on every tap.
        string text = null;
        bool bar = false;
        if (input.SecondaryHeld > ShowAfterSeconds) { text = ReturnToMenuText.Holding; bar = true; }
        else if (now < armedUntil) text = ReturnToMenuText.PressAgain;

        group.alpha = Mathf.MoveTowards(group.alpha, text != null ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (text != null) label.text = text;
        track.enabled = fill.enabled = bar;
        fill.fillAmount = Mathf.Clamp01(input.SecondaryHeld / HoldSeconds);
        // Text in the middle on its own; above the bar when there is one.
        label.rectTransform.offsetMin = new Vector2(20f, bar ? 44f : 10f);

        // Sits below the line of sight so it never covers what the player is doing.
        root.position = cam.position + cam.rotation * new Vector3(0f, -0.32f, 0.9f);
        root.rotation = cam.rotation;
    }

    void Leave()
    {
        leaving = true;
        input.Rumble(0.6f, 0.15f);
        AppFlow.ReturnToMenu();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    void BuildIndicator()
    {
        int layer = LayerMask.NameToLayer("UI");
        if (layer < 0) layer = 5;

        var go = new GameObject("Return Indicator") { layer = layer };
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;

        root = (RectTransform)go.transform;
        root.sizeDelta = new Vector2(620f, 130f);
        root.localScale = Vector3.one * 0.001f;
        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var bg = Add<Image>("Panel", root);
        bg.color = new Color(0.02f, 0.06f, 0.09f, 0.82f);
        Stretch(bg.rectTransform);
        OnTop(bg);

        label = Add<TextMeshProUGUI>("Label", root);
        label.fontSize = 30;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        Stretch(label.rectTransform);
        label.rectTransform.offsetMax = new Vector2(-20f, -10f);
        // TMP draws with its own material, so it needs its own copy to ignore depth.
        label.fontMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);

        track = Add<Image>("Track", root);
        track.color = new Color(1f, 1f, 1f, 0.18f);
        Bar(track.rectTransform);
        OnTop(track);

        fill = Add<Image>("Fill", root);
        fill.color = new Color32(0x3F, 0xCB, 0x4B, 0xFF);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        Bar(fill.rectTransform);
        OnTop(fill);
    }

    /// <summary>
    /// The prompt floats 0.9 m ahead, and in Module 1 a wall or a flower is often nearer than that.
    /// Drawn without a depth test it stays readable instead of disappearing into the scenery.
    /// </summary>
    static void OnTop(Graphic g)
    {
        var m = new Material(Graphic.defaultGraphicMaterial);
        m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        g.material = m;
    }

    static T Add<T>(string name, Transform parent) where T : Component
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        var c = go.AddComponent<T>();
        if (c is Graphic g) g.raycastTarget = false;
        return c;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Bar(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(24f, 22f);
        rt.offsetMax = new Vector2(-24f, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 14f);
    }
}

public static class ReturnToMenuText
{
    public const string PressAgain = "Press  MENU  again to go back to the module menu";
    public const string Holding = "Keep holding  B  to go back to the module menu";
}
