using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(TenteEEEE.HUDClock.Editor.HUDClockBuildPlugin))]

namespace TenteEEEE.HUDClock.Editor
{
    public sealed class HUDClockBuildPlugin : Plugin<HUDClockBuildPlugin>
    {
        public override string QualifiedName { get { return "tenteeeee.hud-clock"; } }
        public override string DisplayName { get { return "HUD Clock"; } }

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run("Generate HUD Clock", Generate);
        }

        private static void Generate(BuildContext context)
        {
            var allComponents = context.AvatarRootObject
                .GetComponentsInChildren<HUDClockComponent>(true);
            var components = allComponents
                .Where(item => item != null && item.isActiveAndEnabled)
                .ToArray();
            if (components.Length == 0)
            {
                Cleanup(allComponents, context.AvatarRootTransform);
                return;
            }

            if (components.Length > 1)
            {
                Debug.LogWarning(
                    "[HUD Clock] HUD Clock コンポーネントが複数あるため、先頭の1個だけを使用します。",
                    components[0]);
            }

            HUDClockComponent settings = components[0];
            Animator animator = context.AvatarRootObject.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning(
                    "[HUD Clock] ヒューマノイドリグではないため Head ボーンを解決できません。HUD Clock をスキップしました。",
                    settings);
                Cleanup(allComponents, context.AvatarRootTransform);
                return;
            }

            if (!HUDClockBuilder.GeneratedAssetDependenciesAvailable(out string dependencyError))
            {
                Debug.LogWarning(
                    "[HUD Clock] " + dependencyError + " HUD Clock をスキップしました。",
                    settings);
                Cleanup(allComponents, context.AvatarRootTransform);
                return;
            }

            HUDClockBuilder.GeneratedAssets assets = HUDClockBuilder.BuildGeneratedAssets(settings);
            context.AssetSaver.SaveAsset(assets.Quad);
            context.AssetSaver.SaveAsset(assets.Material);
            if (assets.EventMaterial != null)
                context.AssetSaver.SaveAsset(assets.EventMaterial);

            GameObject root = new GameObject("HUDClock");
            root.transform.SetParent(context.AvatarRootObject.transform, false);
            HUDClockBuilder.PopulateHierarchy(root, assets, settings);
            ApplySettings(root, settings);

            Cleanup(allComponents, context.AvatarRootTransform);
        }

        private static void ApplySettings(GameObject root, HUDClockComponent settings)
        {
            Transform offset = root.transform.Find("Anchor/Side/Offset");
            if (offset != null)
            {
                offset.localPosition = settings.offsetPosition;
                offset.localRotation = Quaternion.Euler(settings.offsetRotation);
                offset.localScale = Vector3.one * settings.offsetScale;
            }

            ModularAvatarParameters parameters = root.GetComponent<ModularAvatarParameters>();
            SetParameterDefault(parameters, "HUDClock", settings.startEnabled ? 1.0f : 0.0f);
            SetParameterDefault(parameters, "HUDClockEvent", settings.startEventEnabled ? 1.0f : 0.0f);
            SetParameterDefault(parameters, "HUDClockSide", settings.startOnRightSide ? 1.0f : 0.0f);
            SetParameterDefault(parameters, "HUDClockAlpha", settings.startOpacity);
        }

        private static void SetParameterDefault(
            ModularAvatarParameters parameters, string name, float value)
        {
            if (parameters == null || parameters.parameters == null)
                return;

            for (int i = 0; i < parameters.parameters.Count; i++)
            {
                ParameterConfig parameter = parameters.parameters[i];
                if (parameter.nameOrPrefix != name)
                    continue;

                parameter.defaultValue = value;
                parameter.hasExplicitDefaultValue = true;
                parameters.parameters[i] = parameter;
                return;
            }
        }

        private static void Cleanup(
            IEnumerable<HUDClockComponent> components, Transform avatarRoot)
        {
            foreach (HUDClockComponent item in components)
            {
                if (item == null)
                    continue;

                GameObject gameObject = item.gameObject;
                Object.DestroyImmediate(item);
                if (gameObject != null && gameObject.transform != avatarRoot &&
                    gameObject.name == "HUDClock" && gameObject.transform.childCount == 0 &&
                    gameObject.GetComponents<Component>().Length == 1)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
