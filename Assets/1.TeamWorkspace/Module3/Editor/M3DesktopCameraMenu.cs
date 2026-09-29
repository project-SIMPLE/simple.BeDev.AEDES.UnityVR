using UnityEditor;

/// <summary>
/// AEDES > Module 3 > Use Desktop Camera: play the scene with the plain camera and the keyboard
/// driver instead of the XR rig - for working on the scene without a headset. Off by default, so
/// the scene plays the way it ships.
/// </summary>
public static class M3DesktopCameraMenu
{
    private const string Path = "AEDES/Module 3/Use Desktop Camera";

    [MenuItem(Path)]
    private static void Toggle() =>
        EditorPrefs.SetBool(M3XRPlayer.DesktopCameraPref, !EditorPrefs.GetBool(M3XRPlayer.DesktopCameraPref, false));

    [MenuItem(Path, true)]
    private static bool Validate()
    {
        Menu.SetChecked(Path, EditorPrefs.GetBool(M3XRPlayer.DesktopCameraPref, false));
        return true;
    }
}
