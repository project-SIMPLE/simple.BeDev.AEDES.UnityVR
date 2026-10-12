using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for Module 1's sounds in Team Assets/Sound/Module1, so they survive a reimport.
/// The long ambience streams; the other loops stay compressed in memory; one-shots decompress on load
/// so they start without a hitch. Everything is preloaded with the scene.
/// </summary>
public class Module1AudioImport : AssetPostprocessor
{
    const string Folder = "Assets/1.TeamWorkspace/Team Assets/Sound/Module1/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (AudioImporter)assetImporter;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        bool ambience = name.StartsWith("m1_amb_");
        bool loop = ambience || name.Contains("dragonfly") || name.Contains("sip_") || name.Contains("drink_") || name.Contains("heartbeat");

        var s = importer.defaultSampleSettings;
        s.loadType = ambience ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
        importer.loadInBackground = ambience;
    }
}
