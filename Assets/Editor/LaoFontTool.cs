using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Regenerates the Lao TMP font asset with full coverage of the Lao block.
///
/// Why this exists: the committed "Lao_SomVang SDF_Custom" asset was baked from a handful of specific
/// strings and holds only 47 of the 87 Lao glyphs in the source typeface. One of the missing glyphs is
/// U+0EBD, which ປ່ຽນ (Change) needs - a CMPE-validated 5 ປ measure. Rather than approximate a
/// letterform, this rebuilds the asset from Lao_SomVang.ttf so every glyph is the designed one.
///
/// Output goes to Resources so Module2HUD can load it at runtime (the HUD is created by code, so it
/// has no inspector field to wire).
///
///   Unity -batchmode -quit -projectPath . -executeMethod LaoFontTool.Generate
/// or the menu: AEDES / Regenerate Lao font asset
/// </summary>
public static class LaoFontTool
{
    const string SourcePath = "Assets/1.TeamWorkspace/Team Assets/TextMesh Pro/Fonts/Lao/Lao_SomVang.ttf";
    const string OutputPath = "Assets/Resources/Fonts/Lao_SomVang Full SDF.asset";

    [MenuItem("AEDES/Regenerate Lao font asset")]
    public static void Generate()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (font == null)
        {
            Debug.LogError($"[LaoFontTool] Source font not found at {SourcePath}");
            EditorApplication.Exit(1);
            return;
        }

        // Built dynamic so TryAddCharacters can rasterise into the atlas, then frozen to Static so the
        // player build carries the atlas and needs no runtime access to the .ttf.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (fontAsset == null)
        {
            Debug.LogError("[LaoFontTool] CreateFontAsset returned null.");
            EditorApplication.Exit(1);
            return;
        }

        string characters = BuildCharacterSet();
        fontAsset.TryAddCharacters(characters, out string missing, true);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutputPath));
        AssetDatabase.DeleteAsset(OutputPath);
        AssetDatabase.CreateAsset(fontAsset, OutputPath);

        // Atlas texture and material have to live inside the asset or they are lost on reload.
        if (fontAsset.atlasTextures != null)
        {
            foreach (var texture in fontAsset.atlasTextures)
            {
                if (texture == null) continue;
                texture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, fontAsset);
            }
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int lao = 0, latin = 0;
        foreach (var kvp in fontAsset.characterLookupTable)
        {
            if (kvp.Key >= 0x0E80 && kvp.Key <= 0x0EFF) lao++;
            else if (kvp.Key >= 0x20 && kvp.Key <= 0x7E) latin++;
        }

        Debug.Log($"[LaoFontTool] {OutputPath}: {fontAsset.characterLookupTable.Count} glyphs " +
                  $"(Lao {lao}, Latin {latin}). U+0EBD present: {fontAsset.characterLookupTable.ContainsKey(0x0EBD)}. " +
                  $"Missing: '{missing}'");

        EditorApplication.Exit(0);
    }

    // Everything the Lao block defines, plus Basic Latin for the English gloss and digits.
    static string BuildCharacterSet()
    {
        var sb = new StringBuilder();
        for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);
        for (int c = 0x0E80; c <= 0x0EFF; c++) sb.Append((char)c);
        return sb.ToString();
    }
}
