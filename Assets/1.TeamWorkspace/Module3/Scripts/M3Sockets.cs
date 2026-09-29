using UnityEngine;

/// <summary>
/// Finds the named Socket_* transforms that Module 3's prop and furniture prefabs carry
/// (Module3Props.Socket). Sockets are how art says where things attach, so code looks them up by
/// name and never by a position it measured itself.
/// </summary>
public static class M3Sockets
{
    /// <summary>The first transform called <paramref name="name"/> at or below <paramref name="root"/>, or null.</summary>
    public static Transform Find(Transform root, string name)
    {
        if (root == null) return null;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
