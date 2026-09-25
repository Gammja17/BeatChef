using UnityEditor;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// Resources/Songs의 오디오는 온셋 분석(clip.GetData)이 가능하도록
    /// Decompress On Load + 로드 시 프리로드로 강제한다.
    /// </summary>
    public class SongAudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Songs")) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
        }
    }
}
