using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tells its owner when the ray or a fingertip is on this piece of UI. The person card uses it to
/// put an icon's words in the caption line only while someone is pointing at that icon.
/// </summary>
public class M3Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action<bool> Changed;

    public static M3Hover On(Component target, Action<bool> changed)
    {
        var hover = target.GetComponent<M3Hover>() ?? target.gameObject.AddComponent<M3Hover>();
        hover.Changed = changed;
        return hover;
    }

    public void OnPointerEnter(PointerEventData e) => Changed?.Invoke(true);
    public void OnPointerExit(PointerEventData e) => Changed?.Invoke(false);
    private void OnDisable() => Changed?.Invoke(false);
}
