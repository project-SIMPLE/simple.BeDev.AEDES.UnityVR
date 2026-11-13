using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

/// <summary>
/// The XR rig's body capsule, with a ceiling on its height.
///
/// The stock manipulator makes the capsule exactly as tall as the wearer's head is high. That
/// leaves no room to move under a beam or an eave for anyone tall, so the capsule is capped a
/// little below head height; anyone shorter than the cap is unaffected. (The village doorways
/// are 2.1 m clear, so this is not what lets people through the door - a first version capped
/// it at 1.3 m to squeeze under a 1.5 m doorway and the head simply pushed through the wall
/// above it, which is why the houses were scaled up instead.)
/// </summary>
public class M3CappedBodyManipulator : CharacterControllerBodyManipulator
{
    /// <summary>Tallest the capsule may be, in metres.</summary>
    public float maxCapsuleHeight = 1.6f;

    public override CollisionFlags MoveBody(Vector3 motion)
    {
        if (linkedBody == null || characterController == null)
            return CollisionFlags.None;

        var ground = linkedBody.GetBodyGroundLocalPosition();
        float height = Mathf.Min(linkedBody.xrOrigin.CameraInOriginSpaceHeight - ground.y, maxCapsuleHeight);
        characterController.height = height;
        characterController.center = new Vector3(ground.x, ground.y + height * 0.5f + characterController.skinWidth, ground.z);

        if (characterController.enabled)
            return characterController.Move(motion);

        linkedBody.originTransform.position += motion;
        return CollisionFlags.None;
    }
}
