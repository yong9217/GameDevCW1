using UnityEditor;
using UnityEngine;

// Import settings for the files in Assets/Resources/Audio, applied automatically:
//  - music_* tracks stream from disk (long files, so they are not fully loaded into memory);
//  - everything else is a short effect, decompressed on load so it plays without delay.
public class GameAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Audio/")) return;

        var importer = (AudioImporter)assetImporter;
        string file = System.IO.Path.GetFileNameWithoutExtension(path);
        var settings = importer.defaultSampleSettings;
        if (file.StartsWith("music_"))
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .6f;
            importer.loadInBackground = true;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .7f;
        }
        importer.defaultSampleSettings = settings;
    }
}
