using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "Hold B to leave" - the way out of a module and back to <see cref="AppFlow.MenuScene"/>.
///
/// It installs itself into every scene through <see cref="RuntimeInitializeOnLoadMethod"/> and
/// survives scene loads, so neither module scene nor any module script has to be touched to gain
/// it. That is deliberate: <c>Module_1_MainScene.unity</c> is ~38k lines of YAML and every edit to
/// it is a merge conflict waiting for whoever is working on the other branch.
///
/// The hold is long enough (<see cref="HoldSeconds"/>) that nobody leaves a module by fumbling a
/// button, and a progress bar appears while holding so it is discoverable rather than a secret.
/// B / Y is used because the modules only ever tap it (Module 1's "press anything to fly again",
/// Module 3's "no"); none of them holds it.
/// </summary>
public class ReturnToMenu : MonoBehaviour
{
    public const float HoldSeconds = 1.5f;
    const float ShowAfterSeconds = 0.3f;

    static ReturnToMenu instance;

    readonly XRButtons input = new XRButtons();
    Transform cam;
    RectTransform root;
    CanvasGroup group;
    Image fill;
    TextMeshProUGUI label;
    bool leaving;

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
    }

    // This object outlives every scene, so the "already leaving" latch has to be cleared on each
    // load; otherwise only the first trip back to the menu per app launch would work.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        leaving = false;
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
        float held = Mathf.Clamp01(input.SecondaryHeld / HoldSeconds);

        // A quick tap of B is an answer in Module 3 ("no"), so the bar only shows once the press is
        // clearly a hold, rather than flashing up on every tap.
        bool show = input.SecondaryHeld > ShowAfterSeconds;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        fill.fillAmount = held;
        label.text = ReturnToMenuText.Holding;

        // Sits below the line of sight so it never covers what the player is doing.
        root.position = cam.position + cam.rotation * new Vector3(0f, -0.26f, 0.9f);
        root.rotation = cam.rotation;

        if (input.SecondaryHeld >= HoldSeconds)
        {
            leaving = true;
            input.Rumble(0.6f, 0.15f);
            AppFlow.ReturnToMenu();
        }
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

        var go = new GameObject("Hold Indicator") { layer = layer };
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        root = (RectTransform)go.transform;
        root.sizeDelta = new Vector2(560f, 120f);
        root.localScale = Vector3.one * 0.001f;
        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var bg = Add<Image>("Panel", root);
        bg.color = new Color(0.02f, 0.06f, 0.09f, 0.78f);
        Stretch(bg.rectTransform);

        label = Add<TextMeshProUGUI>("Label", root);
        label.fontSize = 30;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        var lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(16f, 0f); lr.offsetMax = new Vector2(-16f, -14f);
        lr.sizeDelta = new Vector2(lr.sizeDelta.x, 46f);

        var track = Add<Image>("Track", root);
        track.color = new Color(1f, 1f, 1f, 0.18f);
        Bar(track.rectTransform);

        fill = Add<Image>("Fill", root);
        fill.color = new Color32(0x3F, 0xCB, 0x4B, 0xFF);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        Bar(fill.rectTransform);
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
    public const string Holding = "Keep holding  B  to go back to the menu";
}
