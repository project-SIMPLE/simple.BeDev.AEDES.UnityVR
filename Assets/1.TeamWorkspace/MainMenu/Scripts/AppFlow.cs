using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The one place that knows which scenes exist and how to get between them.
///
/// Everything here loads scenes by <b>path</b>, never by build index or bare name:
///   - build indices shift every time someone reorders the build list (SaveManager used to wipe
///     the save at index 0, and M2Manager restarts by index);
///   - names are not unique. Module 1's game scene is called "Main Scene", and so are two SIMPLE
///     template scenes, so <c>LoadScene("Main Scene")</c> means whichever of them comes first in
///     the build list.
///
/// A scene still has to be in File > Build Profiles > Scene List to be loadable at runtime, so
/// <see cref="IsAvailable"/> checks that and <see cref="Load"/> says so in the log instead of
/// throwing deep inside SceneManager.
/// </summary>
public static class AppFlow
{
    public const string MenuScene = "Assets/1.TeamWorkspace/MainMenu/Scene/Module Select.unity";

    /// <summary>
    /// Module 1 is entered through the team's own start screen, not straight into the game: it
    /// holds the how-to-play slides and its SaveManager starts the run's clock. The game scene
    /// ("Main Scene") returns there when time runs out.
    /// </summary>
    public const string Module1Scene = "Assets/1.TeamWorkspace/Module1/Scene/Startup Menu_New.unity";
    public const string Module1GameScene = "Assets/1.TeamWorkspace/Module1/Scene/Main Scene.unity";
    public const string Module2Scene = "Assets/1.TeamWorkspace/Module2/Scene/Module_2_MainScene.unity";
    /// <summary>
    /// The team's Module 3 (branches Module-3-Demo / M3_Demo_BackUP): check villagers' symptoms and
    /// send them to hospital or home. Not on main yet, so the menu hides its card until the scene is
    /// in the build list. That branch also holds an unrelated scene with the same name,
    /// Module3/Scenes/Module_3_Demo.unity, which is one more reason to load by path.
    /// </summary>
    public const string Module3Scene = "Assets/1.TeamWorkspace/Module3_System/Scene/Module_3_Demo.unity";
    public const string Module2TutorialScene = "Assets/1.TeamWorkspace/Module2/Scene/Tutorial_M2.unity";
    /// <summary>SIMPLE's GAMA connection screen. Only needed when the headset drives a simulation.</summary>
    public const string IpMenuScene = "Assets/Scenes/Scenes by Simple/Menu/IP Menu.unity";

    public static bool InMenu => SceneManager.GetActiveScene().path == MenuScene;

    public static bool IsAvailable(string scenePath)
    {
        return !string.IsNullOrEmpty(scenePath) && SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0;
    }

    public static void Load(string scenePath)
    {
        if (!IsAvailable(scenePath))
        {
            Debug.LogError($"[AppFlow] Scene '{scenePath}' is not in the build list - add it under " +
                           "File > Build Profiles > Scene List. Staying put.");
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(scenePath);
    }

    public static void ReturnToMenu()
    {
        Load(MenuScene);
    }
}
