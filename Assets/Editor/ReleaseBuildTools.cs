using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AliceMirrorfall.Editor
{
    /// <summary>Generates the product icon and fails a build early when release essentials are missing.</summary>
    public sealed class ReleaseBuildTools : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Alice Mirrorfall/Generate Release Branding")]
        public static void GenerateBranding()
        {
            const string folder = "Assets/Generated";
            const string iconPath = folder + "/AliceMirrorfallIcon.png";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Generated");

            var icon = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var violet = new Color(.08f, .035f, .18f);
            var gold = new Color(.965f, .835f, .43f);
            var rose = new Color(.9f, .25f, .46f);
            for (int y = 0; y < 512; y++)
            {
                Color rowColor = Color.Lerp(violet, new Color(.22f, .1f, .36f), y / 512f);
                for (int x = 0; x < 512; x++)
                {
                    float dx = x - 256f;
                    float dy = y - 256f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    Color color = rowColor;
                    if (radius < 186) color = rose;
                    if (radius < 128) color = gold;
                    if (radius < 76) color = violet;
                    icon.SetPixel(x, y, color);
                }
            }
            File.WriteAllBytes(iconPath, icon.EncodeToPNG());
            Object.DestroyImmediate(icon);
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            PlayerSettings.productName = "Alice Mirrorfall";
            PlayerSettings.companyName = "Wonderland Atelier";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, new[] { texture });
            AssetDatabase.SaveAssets();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorBuildSettings.scenes.Length == 0)
                throw new BuildFailedException("At least one enabled scene is required.");
            GenerateBranding();
        }
    }
}
