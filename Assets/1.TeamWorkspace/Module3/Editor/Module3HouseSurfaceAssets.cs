using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;

/// <summary>
/// Generates the assets behind the Module 3 house surface: a small seamless detail texture, and two
/// materials that are the Lao house's own palette materials switched to the "AEDES/Module 3/House Lit"
/// shader with that texture.
///
/// Why not just edit the house's materials: PF_Lao_House_One_FloorV2 and M_Gradient(_Light) are shared
/// by Modules 1 and 2, and the house's UVs only pick a colour out of a palette (each face samples one
/// flat swatch), so an ordinary detail map has nothing to grip. The shader projects the texture from
/// world space instead, and M3HouseSurface swaps these materials onto the houses Module 3 builds.
///
/// The results are committed (under Assets/Resources so a build always includes them and their shader
/// variants); this menu item exists so they can be regenerated rather than hand-edited:
///   Unity -batchmode -quit -projectPath . -executeMethod Module3HouseSurfaceAssets.Generate
/// </summary>
public static class Module3HouseSurfaceAssets
{
    private const string Folder = "Assets/Resources/Module3";
    private const string TexturePath = Folder + "/T_M3_HouseDetail.png";
    private const string ShaderName = "AEDES/Module 3/House Lit";
    private const int Size = 256;

    /// <summary>The house material each replacement is a copy of, and the asset it becomes.</summary>
    private static readonly (string source, string asset)[] Materials =
    {
        ("M_Gradient_Light", Folder + "/M_M3_House_Light.mat"),   // walls and roof
        ("M_Gradient", Folder + "/M_M3_House_Door.mat"),          // door leaves
    };

    [MenuItem("AEDES/Module 3/Generate House Surface Assets")]
    public static void Generate()
    {
        WriteTexture();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"Shader '{ShaderName}' not found - has it imported?"); return; }

        foreach (var (source, asset) in Materials)
        {
            var original = FindMaterial(source);
            if (original == null) { Debug.LogError($"House material '{source}' not found."); continue; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(asset);
            bool isNew = material == null;
            if (isNew) material = new Material(shader);
            material.CopyPropertiesFromMaterial(original);
            material.shaderKeywords = original.shaderKeywords;
            // The copy replaces the property sheet with the original's, which lacks the detail map;
            // assigning the shader again is what adds it, and SetTexture ignores unknown properties.
            material.shader = shader;
            if (!material.HasProperty("_M3DetailMap")) { Debug.LogError($"{asset} has no _M3DetailMap property."); continue; }
            material.SetTexture("_M3DetailMap", texture);
            if (isNew) AssetDatabase.CreateAsset(material, asset);
            else EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Module 3: house surface assets generated in " + Folder);
    }

    private static Material FindMaterial(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.name == name && m.shader.name == "Universal Render Pipeline/Lit") return m;
        }
        return null;
    }

    /// <summary>
    /// R: grain and blotches. G: streaks that run down the v axis, which the shader turns into rain
    /// streaks on walls and runs down the slope on a roof. Both are centred on 0.5, so on average
    /// they leave the palette colour alone. Every octave is periodic, so the texture tiles.
    /// </summary>
    private static void WriteTexture()
    {
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float2 uv = float2(x + 0.5f, y + 0.5f) / Size;
                float grain = 0.55f * Periodic(uv, 64, 64, 0f) + 0.30f * Periodic(uv, 16, 16, 31f) + 0.15f * Periodic(uv, 4, 4, 77f);
                float streak = 0.60f * Periodic(uv, 40, 3, 13f) + 0.40f * Periodic(uv, 17, 2, 53f);
                pixels[y * Size + x] = new Color32(ToByte(grain), ToByte(streak), 128, 255);
            }
        }

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);

        // Data, not colour: linear, tiled, mipmapped so it fades to the average at a distance.
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static float Periodic(float2 uv, int cyclesX, int cyclesY, float offset)
    {
        var cycles = float2(cyclesX, cyclesY);
        return noise.pnoise(uv * cycles + offset, cycles);
    }

    private static byte ToByte(float signed) => (byte)(saturate(0.5f + 0.5f * signed * 1.3f) * 255f + 0.5f);
}
