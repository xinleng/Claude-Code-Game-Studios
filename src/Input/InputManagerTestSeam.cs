using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dynasty.Input
{
    /// <summary>
    /// Test seam (ADR-0010 rule 5) reporting InputManager / PlayerControls state as strings, ints and
    /// bools so the test assemblies never reference <c>Unity.InputSystem</c> (ADR-0004 asmdef gate).
    /// Internal: visible to the test assemblies only.
    /// </summary>
    internal static class InputManagerTestSeam
    {
        /// <summary>Asset path of the authored actions asset.</summary>
        internal const string ActionsAssetPath = "Packages/com.dynasty.game/Input/PlayerControls.inputactions";

        /// <summary>"Name|Type|ExpectedControlType" for each action in <paramref name="mapName"/>.</summary>
        internal static string[] DescribeActions(InputManager manager, string mapName)
        {
            return manager.Controls.asset.FindActionMap(mapName, throwIfNotFound: true).actions
                .Select(a => a.name + "|" + a.type + "|" + a.expectedControlType)
                .ToArray();
        }

        /// <summary>"path|groups" for each non-composite binding of <paramref name="actionName"/>.</summary>
        internal static string[] DescribeBindings(InputManager manager, string mapName, string actionName)
        {
            InputAction action = manager.Controls.asset.FindActionMap(mapName, true).FindAction(actionName, true);
            return action.bindings.Where(b => !b.isComposite).Select(b => b.path + "|" + b.groups).ToArray();
        }

        /// <summary>Names of the control schemes on the controls instance.</summary>
        internal static string[] ControlSchemes(InputManager manager)
        {
            return manager.Controls.controlSchemes.Select(s => s.name).ToArray();
        }

        /// <summary>Whether the named map is enabled.</summary>
        internal static bool IsMapEnabled(InputManager manager, string mapName)
        {
            return manager.Controls.asset.FindActionMap(mapName, true).enabled;
        }

        /// <summary>The controls instance as an opaque reference (identity checks only).</summary>
        internal static object ControlsIdentity(InputManager manager)
        {
            return manager.Controls;
        }

        /// <summary>Live in-memory InputActionAssets named "PlayerControls" (each wrapper construction creates one).</summary>
        internal static int CountPlayerControlsAssets()
        {
            return Resources.FindObjectsOfTypeAll<InputActionAsset>().Count(a => a.name == "PlayerControls");
        }

        /// <summary>True if <c>InputSystem.settings.updateMode</c> is Dynamic Update.</summary>
        internal static bool IsDynamicUpdateMode()
        {
            return InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        }

        /// <summary>Sets the global update mode (tests must restore it in teardown).</summary>
        internal static void SetDynamicUpdateMode(bool dynamic)
        {
            InputSystem.settings.updateMode = dynamic
                ? InputSettings.UpdateMode.ProcessEventsInDynamicUpdate
                : InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Differences between the generated wrapper's embedded actions and the authored
        /// <c>.inputactions</c> asset (maps, actions, bindings, schemes); empty when in sync. Catches a
        /// forgotten <c>PlayerControlsCodegen.Generate</c> after editing the asset.
        /// </summary>
        internal static string WrapperVsAssetDiff()
        {
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (authored == null)
            {
                return "authored asset not found at " + ActionsAssetPath;
            }

            var wrapper = new PlayerControls();
            try
            {
                string a = Describe(authored);
                string w = Describe(wrapper.asset);
                return a == w ? string.Empty : "authored:\n" + a + "\nwrapper:\n" + w;
            }
            finally
            {
                // The generated Dispose() calls Object.Destroy, which is illegal in edit mode.
                Object.DestroyImmediate(wrapper.asset);
            }
        }

        private static string Describe(InputActionAsset asset)
        {
            var sb = new StringBuilder();
            foreach (InputActionMap map in asset.actionMaps)
            {
                sb.Append("map ").Append(map.name).Append('\n');
                foreach (InputAction action in map.actions)
                {
                    sb.Append(" action ").Append(action.name).Append(' ').Append(action.type).Append(' ').Append(action.expectedControlType).Append('\n');
                }

                foreach (InputBinding binding in map.bindings)
                {
                    sb.Append(" binding ").Append(binding.action).Append(' ').Append(binding.name).Append(' ').Append(binding.path)
                        .Append(' ').Append(binding.groups).Append(' ').Append(binding.isComposite).Append(binding.isPartOfComposite).Append('\n');
                }
            }

            foreach (InputControlScheme scheme in asset.controlSchemes)
            {
                sb.Append("scheme ").Append(scheme.name).Append(' ').Append(scheme.bindingGroup).Append('\n');
            }

            return sb.ToString();
        }
#endif
    }
}
