using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Makes a villager someone you can talk to: point at them and pull the trigger, or reach out
/// and squeeze the grip. Either opens <see cref="M3PersonPanel"/> beside them.
///
/// Every villager gets this, sick or well, and it looks the same on all of them - section 5's
/// rule that nothing may mark a sick character applies to the way you start a conversation too.
/// </summary>
public class M3VillagerTarget : MonoBehaviour
{
    public int personId;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>() ?? gameObject.AddComponent<XRSimpleInteractable>();
        interactable.selectMode = InteractableSelectMode.Multiple;
        interactable.selectEntered.AddListener(_ => Talk());
    }

    // Previous trigger state per interactor, so a press is caught as an edge of "held". XRI's own
    // ReadWasPerformedThisFrame depends on when the Input System updates relative to this Update,
    // and a press was simply missed with the trigger pulled while pointing right at the villager.
    private readonly System.Collections.Generic.Dictionary<NearFarInteractor, bool> wasHeld =
        new System.Collections.Generic.Dictionary<NearFarInteractor, bool>();

    private void Update()
    {
        // The ray's select is the grip; pointing with the trigger is what people try first, so a
        // trigger pull while the ray is on this villager opens the conversation as well.
        var hovering = interactable.interactorsHovering;
        for (int i = 0; i < hovering.Count; i++)
        {
            if (!(hovering[i] is NearFarInteractor nf)) continue;
            bool held = nf.uiPressInput.ReadIsPerformed();
            wasHeld.TryGetValue(nf, out bool before);
            wasHeld[nf] = held;
            if (held && !before) Talk();
        }
    }

    private void Talk()
    {
        var panel = M3PersonPanel.Instance;
        if (panel == null) return;
        if (panel.IsOpen && panel.PersonId == personId) return;
        panel.Open(personId, transform);
    }
}
