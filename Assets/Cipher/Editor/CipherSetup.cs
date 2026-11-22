using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cipher.EditorTools
{
    /// <summary>Настройки проекта под iPhone (iOS + ARKit).</summary>
    public static class CipherSetup
    {
        [MenuItem("Cipher/1. Настроить проект для iPhone", priority = 1)]
        public static void ConfigureIOS()
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.companyName = "MIREA";
            PlayerSettings.productName = "Cipher";
            PlayerSettings.SetApplicationIdentifier(ios, "com.mirea.cipher");
            PlayerSettings.iOS.cameraUsageDescription = "Cipher использует камеру для дополненной реальности";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);

            // AR-игра на телефоне — в вертикальной ориентации
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // HDR в URP мешает AR-фону на мобильных
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp && urp.supportsHDR)
                { urp.supportsHDR = false; EditorUtility.SetDirty(urp); }
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset def && def.supportsHDR)
            { def.supportsHDR = false; EditorUtility.SetDirty(def); }

            CheckBackgroundFeature();
            AssetDatabase.SaveAssets();
            Debug.Log("[Cipher] Проект настроен для iPhone: Bundle ID com.mirea.cipher, iOS 15+, вертикальная ориентация, описание камеры.");
        }

        /// <summary>Проверяет, что в URP-рендерерах добавлен AR Background Renderer Feature (без него на iPhone чёрный экран).</summary>
        static void CheckBackgroundFeature()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                bool has = data.rendererFeatures.Any(f => f && f.GetType().Name == "ARBackgroundRendererFeature");
                if (has) Debug.Log($"[Cipher] {path}: AR Background Renderer Feature — есть.");
                else Debug.LogWarning($"[Cipher] {path}: нет AR Background Renderer Feature! Выделите файл и нажмите Add Renderer Feature → AR Background Renderer Feature.");
            }
        }
    }
}
