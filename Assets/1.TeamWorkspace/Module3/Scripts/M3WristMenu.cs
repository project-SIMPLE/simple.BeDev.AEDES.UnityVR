using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Module 2's wrist menu, carrying Module 3's buttons.
///
/// The Pilot's rig is Module 2's player (VR_Player_Module_2), so Module 3 controls exactly like
/// Module 2: the left stick walks, the right stick turns, the grip grabs - here, reaches out to a
/// villager - and the right index finger pokes buttons. Module 2 keeps its tools on a watch on the
/// left wrist: turn the wrist to look at it and a small menu appears, poke a button to take the
/// swatter or the cream. Module 3 keeps the watch and its look-at-wrist rule and gives it its own
/// buttons: "Ready" at the start of a turn, "End turn" during one (then "Yes" / "No" to confirm),
/// with the turn and its clock above them where Module 2 shows its score and time. The same answers
/// are on A, B and the left menu button (M3XRPlayer), so nobody is stuck if they never find the watch.
///
/// Built from the rig at runtime by <see cref="Attach"/>: Module 2's own UIHand (which would also
/// spawn its swatter) comes off the watch and this takes its place, reading the same canvas and the
/// same layer mask. The prefab itself is not touched.
/// </summary>
public class M3WristMenu : MonoBehaviour
{
    private GameObject menu;
    private LayerMask headMask;
    private Button primary, secondary;
    private TextMeshProUGUI primaryLabel, secondaryLabel, turnText, clockText;

    /// <summary>Takes over the watch on <paramref name="rig"/>; null, with a warning, if the rig has none.</summary>
    public static M3WristMenu Attach(GameObject rig)
    {
        var module2 = rig.GetComponentInChildren<UIHand>(true);
        if (module2 == null)
        {
            Debug.LogWarning("Module 3: the rig has no wrist watch (UIHand), so Ready and End turn are on the buttons only.");
            return null;
        }
        var watch = module2.gameObject;
        var canvas = module2.uiHand;
        var mask = module2.layerMaskHand;
        // Its Awake has already run (it only notes itself as the current watch); off it goes.
        Destroy(module2);

        var wrist = watch.AddComponent<M3WristMenu>();
        wrist.Init(canvas, mask);
        return wrist;
    }

    private void Init(GameObject canvas, LayerMask mask)
    {
        menu = canvas;
        headMask = mask;

        // Module 2's two tool buttons become Ready / End turn and Yes / No. Their look stays - the
        // rounded backing, the press affordance - only the tool icon goes and a label takes its place.
        var buttons = canvas.GetComponentsInChildren<Button>(true);
        // In the order they sit in the grid, left to right: the first is Ready / End turn / Yes, the second No.
        System.Array.Sort(buttons, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        if (buttons.Length > 0) primary = Relabel(buttons[0], out primaryLabel);
        if (buttons.Length > 1) secondary = Relabel(buttons[1], out secondaryLabel);
        if (primary != null) primary.onClick.AddListener(() => { if (M3XRPlayer.Instance != null) M3XRPlayer.Instance.PressPrimary(); });
        if (secondary != null) secondary.onClick.AddListener(() => { if (M3XRPlayer.Instance != null) M3XRPlayer.Instance.PressSecondary(); });

        // Module 2's score and time lines show the turn and its clock.
        foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == "Score") turnText = Adopt(text);
            else if (text.name == "Time") clockText = Adopt(text);
        }
        menu.SetActive(false);
    }

    private static Button Relabel(Button button, out TextMeshProUGUI label)
    {
        // Module 2 wired these to its tool scripts through the scene; on a prefab instance those calls
        // have no target. A fresh event, with only ours on it.
        button.onClick = new Button.ButtonClickedEvent();
        foreach (var image in button.GetComponentsInChildren<Image>(true))
            if (image.name == "LOGO") image.gameObject.SetActive(false);

        var rect = (RectTransform)button.transform;
        // The button's size comes from the grid it sits in, which has not laid out yet when the rig is
        // spawned: read the cell size, not the rect (whose serialized size is something else entirely).
        var grid = button.GetComponentInParent<GridLayoutGroup>(true);
        float height = grid != null ? grid.cellSize.y : rect.rect.height;
        // Sized to the disc: "Ready" on one line, "End turn" on two (see Show), both well inside the
        // button. A fixed size - TMP's auto-sizing shrank a two-line label to a whisper.
        label = M3Ui.Label(rect, "Label", height * 0.2f, M3Ui.Text, TextAlignmentOptions.Center);
        label.rectTransform.offsetMin = new Vector2(height * 0.1f, height * 0.1f);
        label.rectTransform.offsetMax = new Vector2(-height * 0.1f, -height * 0.1f);
        return button;
    }

    private static TextMeshProUGUI Adopt(TextMeshProUGUI text)
    {
        if (M3Ui.Font != null) text.font = M3Ui.Font;
        foreach (var image in text.GetComponentsInChildren<Image>(true)) image.gameObject.SetActive(false);   // the score and clock icons
        text.text = "";
        return text;
    }

    private void Update()
    {
        // Module 2's rule, kept as it was: the menu shows while the watch face points at the head (the
        // rig's camera carries a collider on the Camera layer for exactly this).
        bool shown = Physics.Raycast(transform.position, transform.up, 10f, headMask);
        if (menu.activeSelf != shown) menu.SetActive(shown);
        if (!shown) return;

        var s = M3Session.Instance;
        var xr = M3XRPlayer.Instance;
        bool running = s != null && s.Session != null && s.Running;
        string first = null, second = null;
        if (running && s.WaitingForPilot) first = M3Ui.L("m3.ui.wristReady");
        else if (running && xr != null && xr.ConfirmingEndTurn) { first = M3Ui.L("m3.ui.wristYes"); second = M3Ui.L("m3.ui.wristNo"); }
        else if (running) first = M3Ui.L("m3.ui.wristEndTurn");
        Show(primary, primaryLabel, first);
        Show(secondary, secondaryLabel, second);

        if (turnText != null) turnText.text = running ? M3Ui.L("m3.ui.wristTurn", s.Session.TurnIndex + 1) : "";
        if (clockText != null) clockText.text = running ? Clock(s.ClockHeld ? s.TurnSecondsTotal : s.TurnSecondsRemaining) : "";
    }

    private static void Show(Button button, TextMeshProUGUI label, string text)
    {
        if (button == null) return;
        bool on = text != null;
        if (button.gameObject.activeSelf != on) button.gameObject.SetActive(on);
        // A two-word label ("End turn") goes on two lines, so it fills the disc instead of shrinking to fit one.
        if (on && label != null) { string lines = text.Replace(' ', '\n'); if (label.text != lines) label.text = lines; }
    }

    private static string Clock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{whole / 60}:{whole % 60:00}";
    }
}
