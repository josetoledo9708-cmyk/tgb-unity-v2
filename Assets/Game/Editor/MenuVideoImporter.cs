using UnityEditor;

namespace Game.Editor
{
    /// <summary>Transcodifica los videos bajo Resources/Menu/ a bitrate bajo y 720p para reducir el
    /// tamaño en el build (el .ogv fuente es pesado). No requiere herramientas externas.</summary>
    public sealed class MenuVideoImporter : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (assetImporter is not VideoClipImporter vci) return;
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Menu/")) return;

            var s = vci.defaultTargetSettings;
            s.enableTranscoding = true;
            s.codec = VideoCodec.Auto;
            s.bitrateMode = VideoBitrateMode.Low;
            s.spatialQuality = VideoSpatialQuality.MediumSpatialQuality;
            s.resizeMode = VideoResizeMode.CustomSize;
            s.customWidth = 1280;
            s.customHeight = 720;
            vci.defaultTargetSettings = s;
        }
    }
}
