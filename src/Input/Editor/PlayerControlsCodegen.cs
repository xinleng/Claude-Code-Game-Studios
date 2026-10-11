using System.IO;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Editor;

namespace Dynasty.Input.Editor
{
    /// <summary>
    /// Regenerates the <c>PlayerControls</c> C# wrapper from <c>PlayerControls.inputactions</c>.
    /// The Input System importer's built-in "Generate C# Class" option resolves the wrapper path from
    /// the asset path, which for a local <c>file:</c> package is <c>Packages/com.dynasty.game/…</c> — a
    /// directory that does not exist on disk, so the importer would create a stray folder there. This
    /// tool writes to the package's resolved physical folder instead, using the package's own
    /// generator. Run it after every edit to the actions asset (an EditMode test fails if the wrapper
    /// drifts from the asset).
    /// </summary>
    /// <example>
    /// <code>
    /// // Menu: Tools ▸ Dynasty ▸ Regenerate PlayerControls Wrapper
    /// // Batch: unity run . -- -executeMethod Dynasty.Input.Editor.PlayerControlsCodegen.Generate
    /// </code>
    /// </example>
    public static class PlayerControlsCodegen
    {
        /// <summary>Asset path of the single Input Actions asset (ADR-0004 rule 7).</summary>
        public const string AssetPath = "Packages/com.dynasty.game/Input/PlayerControls.inputactions";

        /// <summary>Generates <c>PlayerControls.cs</c> next to the actions asset on disk.</summary>
        [MenuItem("Tools/Dynasty/Regenerate PlayerControls Wrapper")]
        public static void Generate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (asset == null)
            {
                throw new FileNotFoundException("PlayerControls actions asset not found", AssetPath);
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(AssetPath);
            string physicalDir = Path.Combine(package.resolvedPath, "Input");
            string wrapperPath = Path.Combine(physicalDir, "PlayerControls.cs");
            var options = new InputActionCodeGenerator.Options
            {
                className = "PlayerControls",
                namespaceName = "Dynasty.Input",
                sourceAssetPath = AssetPath,
            };

            bool written = InputActionCodeGenerator.GenerateWrapperCode(wrapperPath, asset, options);
            UnityEngine.Debug.Log("PlayerControlsCodegen: " + (written ? "wrote " : "unchanged ") + wrapperPath);
            AssetDatabase.Refresh();
        }
    }
}
