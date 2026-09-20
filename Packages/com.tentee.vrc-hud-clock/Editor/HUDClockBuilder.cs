#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace TenteEEEE.HUDClock.Editor
{
    public static class HUDClockBuilder
    {
        private const string PackageAssetRoot = "Packages/com.tentee.vrc-hud-clock";
        private const string LegacyAssetRoot = "Assets/TenteEEEE/HUDClock";
        private static string AssetRoot =>
            AssetDatabase.IsValidFolder(PackageAssetRoot) ? PackageAssetRoot : LegacyAssetRoot;
        private static string IconRoot => AssetRoot + "/Icons";
        private static string MenuIconPath => IconRoot + "/HUDClock_MenuIcon.png";
        private static string DisplayIconPath => IconRoot + "/HUDClock_DisplayIcon.png";
        private static string PositionIconPath => IconRoot + "/HUDClock_PositionIcon.png";
        private static string SideIconPath => IconRoot + "/HUDClock_SideIcon.png";
        private static string RotationIconPath => IconRoot + "/HUDClock_RotationIcon.png";
        private static string OpacityIconPath => IconRoot + "/HUDClock_OpacityIcon.png";
        private const string FacingPath = "Anchor/Side/Offset/PosX/PosY/Facing";
        private const string YawPath = FacingPath + "/Yaw";
        private const string PitchPath = YawPath + "/Pitch";
        private const string FacePath = PitchPath + "/Face";
        private const string DisplayPath = FacePath + "/Display";
        private const string EventDisplayPath = FacePath + "/EventDisplay";
        private const string SidePath = "Anchor/Side";
        private const string PosXPath = "Anchor/Side/Offset/PosX";
        private const string PosYPath = "Anchor/Side/Offset/PosX/PosY";

        private const string IsLocalParameter = "IsLocal";
        private const string ClockParameter = "HUDClock";
        private const string EventParameter = "HUDClockEvent";
        private const string PosXParameter = "HUDClockPosX";
        private const string PosYParameter = "HUDClockPosY";
        private const string OfsXParameter = "HUDClockOfsX";
        private const string OfsYParameter = "HUDClockOfsY";
        private const string RotYawParameter = "HUDClockRotYaw";
        private const string RotPitchParameter = "HUDClockRotPitch";
        private const string RotYawOfsParameter = "HUDClockRotYawOfs";
        private const string RotPitchOfsParameter = "HUDClockRotPitchOfs";
        private const string SideParameter = "HUDClockSide";
        private const string AlphaParameter = "HUDClockAlpha";

        // Tuning values: default placement in the Head constraint's local space.
        public const float DisplayOffsetLeft = -0.11f;
        public const float DisplayOffsetDown = -0.10f;
        public const float DisplayOffsetForward = 0.42f;
        public const float DisplayPitch = -4.0f;
        // Negative on the left makes the outside edge approach the viewer, like a
        // shallow wraparound display. The right clip mirrors this sign.
        public const float DisplayYaw = -8.0f;

        // Tuning values: runtime adjustment ranges.
        public const float PositionXRange = 0.12f;
        public const float PositionYRange = 0.10f;
        public const float RotationYawRange = 24.0f;
        public const float RotationPitchRange = 18.0f;
        public const float AlphaRange = 1.0f;

        // Tuning values: joystick drift integrates one guarded Add on each tick.
        public const float DriftTickSeconds = 0.1f;
        public const float DriftFullTravelSeconds = 4.0f;
        public const float DriftDeadzone = 0.25f;
        public const float DriftLimit = 0.995f;
        // The blend trees span -1..1, so a full traverse is 2.0 units.
        public static float DriftStep => 2.0f * DriftTickSeconds / DriftFullTravelSeconds;

        // One digit cell is one unit tall in the shader's layout, so the physical height of a
        // cell is the quad width divided by the layout width. Pinning the cell height and
        // deriving the quad width keeps the digits the same size on screen when the layout
        // changes; the plate just gets a little wider.
        public const float DigitCellHeightMeters = 0.01308f;   // == the previous 0.09 / 6.88
        public const float ShaderLayoutWidth = 7.20f;
        public const float QuadWidthMeters = DigitCellHeightMeters * ShaderLayoutWidth;

        internal static bool GeneratedAssetDependenciesAvailable(out string error)
        {
            if (Shader.Find("TenteEEEE/HUD Clock") == null)
            {
                error = "Shader 'TenteEEEE/HUD Clock' is missing.";
                return false;
            }

            string[] iconPaths =
            {
                MenuIconPath,
                DisplayIconPath,
                PositionIconPath,
                SideIconPath,
                RotationIconPath,
                OpacityIconPath
            };
            foreach (string path in iconPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                {
                    error = "Menu icon is missing: " + path;
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal sealed class GeneratedAssets
        {
            public Mesh Quad;
            public Material Material;
            public Material EventMaterial;
            public AnimatorController Controller;
        }

        internal static GeneratedAssets BuildGeneratedAssets(HUDClockComponent settings)
        {
            Mesh quad = CreateOrUpdateQuadMesh();
            Material material = CreateOrUpdateMaterial(settings);
            Material eventMaterial = settings.eventRowEnabled ? CreateOrUpdateEventMaterial(settings) : null;

            AnimationClip onClip = CreateActiveClip("HUDClock_On", 1.0f);
            AnimationClip offClip = CreateActiveClip("HUDClock_Off", 0.0f);
            AnimationClip eventOnClip = settings.eventRowEnabled
                ? CreateActiveClip("HUDClock_Event_On", EventDisplayPath, 1.0f)
                : null;
            AnimationClip eventOffClip = settings.eventRowEnabled
                ? CreateActiveClip("HUDClock_Event_Off", EventDisplayPath, 0.0f)
                : null;
            AnimationClip posXLow = CreateFloatClip(
                "HUDClock_PosX_Low", PosXPath, "m_LocalPosition.x", -PositionXRange);
            AnimationClip posXHigh = CreateFloatClip(
                "HUDClock_PosX_High", PosXPath, "m_LocalPosition.x", PositionXRange);
            AnimationClip posYLow = CreateFloatClip(
                "HUDClock_PosY_Low", PosYPath, "m_LocalPosition.y", -PositionYRange);
            AnimationClip posYHigh = CreateFloatClip(
                "HUDClock_PosY_High", PosYPath, "m_LocalPosition.y", PositionYRange);
            AnimationClip yawLow = CreateRotationClip(
                "HUDClock_Yaw_Low",
                YawPath,
                Quaternion.Euler(0.0f, -RotationYawRange, 0.0f));
            AnimationClip yawHigh = CreateRotationClip(
                "HUDClock_Yaw_High",
                YawPath,
                Quaternion.Euler(0.0f, RotationYawRange, 0.0f));
            AnimationClip pitchLow = CreateRotationClip(
                "HUDClock_Pitch_Low",
                PitchPath,
                Quaternion.Euler(-RotationPitchRange, 0.0f, 0.0f));
            AnimationClip pitchHigh = CreateRotationClip(
                "HUDClock_Pitch_High",
                PitchPath,
                Quaternion.Euler(RotationPitchRange, 0.0f, 0.0f));
            AnimationClip sideLeft = CreateTransformPoseClip(
                "HUDClock_Side_Left",
                SidePath,
                new Vector3(DisplayOffsetLeft, DisplayOffsetDown, DisplayOffsetForward),
                Quaternion.Euler(DisplayPitch, DisplayYaw, 0.0f),
                1.0f);
            AnimationClip sideRight = CreateTransformPoseClip(
                "HUDClock_Side_Right",
                SidePath,
                new Vector3(-DisplayOffsetLeft, DisplayOffsetDown, DisplayOffsetForward),
                Quaternion.Euler(DisplayPitch, -DisplayYaw, 0.0f),
                -1.0f);
            // Tick exists only to give exitTime a real one-second clock in the drift layers.
            AnimationClip tick = CreateFloatClip(
                "HUDClock_Tick", "Tick", "m_LocalPosition.x", 0.0f);
            AnimationClip alphaLow = CreateAlphaClip(
                "HUDClock_Alpha_Low", 0.0f, settings.eventRowEnabled);
            AnimationClip alphaHigh = CreateAlphaClip(
                "HUDClock_Alpha_High", AlphaRange, settings.eventRowEnabled);

            AnimatorController controller = CreateController(
                onClip,
                offClip,
                posXLow,
                posXHigh,
                posYLow,
                posYHigh,
                yawLow,
                yawHigh,
                pitchLow,
                pitchHigh,
                sideLeft,
                sideRight,
                tick,
                alphaLow,
                alphaHigh,
                eventOnClip,
                eventOffClip,
                settings.eventRowEnabled,
                settings.startEventEnabled);

            return new GeneratedAssets
            {
                Quad = quad,
                Material = material,
                EventMaterial = eventMaterial,
                Controller = controller
            };
        }

        // Alpha clips write material._Opacity on Display, and also on EventDisplay when the
        // event row is enabled - both renderers must fade together under HUDClockAlpha.
        private static AnimationClip CreateAlphaClip(string name, float value, bool includeEventRow)
        {
            AnimationClip clip = PrepareClip(name);
            SetFloatCurve(clip, DisplayPath, typeof(Renderer), "material._Opacity", value);
            if (includeEventRow)
                SetFloatCurve(clip, EventDisplayPath, typeof(Renderer), "material._Opacity", value);
            return clip;
        }

        private static Mesh CreateOrUpdateQuadMesh()
        {
            Mesh mesh = new Mesh { name = "HUDClockQuad" };

            float height = QuadWidthMeters / ShaderLayoutWidth;
            float halfWidth = QuadWidthMeters * 0.5f;
            float halfHeight = height * 0.5f;

            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0.0f),
                new Vector3(halfWidth, -halfHeight, 0.0f),
                new Vector3(halfWidth, halfHeight, 0.0f),
                new Vector3(-halfWidth, halfHeight, 0.0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);
            return mesh;
        }

        private static Shader FindHudClockShader()
        {
            Shader shader = Shader.Find("TenteEEEE/HUD Clock");
            if (shader == null)
                throw new InvalidOperationException("Shader 'TenteEEEE/HUD Clock' was not found.");
            return shader;
        }

        // Shared by both the clock and event materials, so every style property is set
        // through exactly one list. Hand-duplicating this list between the two materials is
        // how a property silently drifts out of sync between them.
        private static void ApplyStyleProperties(Material material, HUDClockComponent settings)
        {
            material.SetColor("_Color", settings.digitColor);
            material.SetColor("_ColonColor", settings.colonColor);
            material.SetColor("_BackColor", settings.backColor);
            material.SetFloat("_BackRounding", settings.backRounding);
            material.SetFloat("_BackPadding", settings.backPadding);
            material.SetFloat("_SegThickness", settings.segThickness);
            material.SetFloat("_SegGap", settings.segGap);
            material.SetFloat("_Slant", settings.slant);
            material.SetFloat("_ColonSize", settings.colonSize);
            material.SetFloat("_ColonSpread", settings.colonSpread);
            material.SetFloat("_OneSerif", settings.oneSerif ? 1.0f : 0.0f);
            material.SetFloat("_ShowSeconds", settings.showSeconds ? 1.0f : 0.0f);
            material.SetFloat("_BlinkColon", settings.blinkColon ? 1.0f : 0.0f);
            material.SetFloat("_GlowStrength", settings.glowStrength);
            material.SetFloat("_DimSegments", settings.dimSegments);
            material.SetFloat("_MeshStrength", settings.meshStrength);
            material.SetFloat("_MeshPitch", settings.meshPitch);
            material.SetFloat("_MeshWireWidth", settings.meshWireWidth);
            material.SetFloat("_HideInMirror", settings.hideInMirror ? 1.0f : 0.0f);
            material.SetFloat("_HideInCamera", settings.hideInCamera ? 1.0f : 0.0f);
        }

        private static Material CreateOrUpdateMaterial(HUDClockComponent settings)
        {
            Material material = new Material(FindHudClockShader()) { name = "HUDClock" };
            ApplyStyleProperties(material, settings);
            material.SetFloat("_RowMode", 0.0f);
            material.SetFloat("_Opacity", 1.0f);
            return material;
        }

        private static Material CreateOrUpdateEventMaterial(HUDClockComponent settings)
        {
            Material material = new Material(FindHudClockShader()) { name = "HUDClockEvent" };
            ApplyStyleProperties(material, settings);
            material.SetFloat("_RowMode", 1.0f);
            material.SetColor("_EventColor", settings.eventColor);
            material.SetColor("_EventWarnColor", settings.eventWarnColor);
            material.SetColor("_EventIntervalColor", settings.eventIntervalColor);
            material.SetFloat("_Opacity", 1.0f);
            return material;
        }

        private static AnimationClip CreateActiveClip(string name, float activeValue)
        {
            return CreateActiveClip(name, DisplayPath, activeValue);
        }

        private static AnimationClip CreateActiveClip(string name, string relativePath, float activeValue)
        {
            AnimationClip clip = PrepareClip(name);
            SetFloatCurve(clip, relativePath, typeof(GameObject), "m_IsActive", activeValue);
            return clip;
        }

        private static AnimationClip CreateFloatClip(
            string name,
            string relativePath,
            string propertyName,
            float value,
            Type componentType = null)
        {
            AnimationClip clip = PrepareClip(name);
            SetFloatCurve(clip, relativePath, componentType ?? typeof(Transform), propertyName, value);
            return clip;
        }

        private static AnimationClip CreateRotationClip(
            string name,
            string relativePath,
            Quaternion rotation)
        {
            AnimationClip clip = PrepareClip(name);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.x", rotation.x);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.y", rotation.y);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.z", rotation.z);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.w", rotation.w);
            return clip;
        }

        private static AnimationClip CreateTransformPoseClip(
            string name,
            string relativePath,
            Vector3 position,
            Quaternion rotation,
            float horizontalMotionScale)
        {
            AnimationClip clip = PrepareClip(name);

            // Both side states write every pose channel so WD-OFF cannot retain a value
            // from whichever side was active previously.
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalPosition.x", position.x);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalPosition.y", position.y);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalPosition.z", position.z);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.x", rotation.x);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.y", rotation.y);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.z", rotation.z);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalRotation.w", rotation.w);

            // Side is above every user-adjustable offset. Mirroring it makes the stored X
            // displacement change sides together with the base pose. Facing receives the
            // same mirror a second time before the adjustable rotations, so both the clock
            // face and the rotation controls retain the same handedness on either side.
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalScale.x", horizontalMotionScale);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalScale.y", 1.0f);
            SetFloatCurve(clip, relativePath, typeof(Transform), "m_LocalScale.z", 1.0f);
            // Apply the readability counter-mirror after Yaw/Pitch. With it before Yaw the
            // two mirrors cancelled too early and the saved yaw did not reflect with Side.
            SetFloatCurve(clip, FacePath, typeof(Transform), "m_LocalScale.x", horizontalMotionScale);
            SetFloatCurve(clip, FacePath, typeof(Transform), "m_LocalScale.y", 1.0f);
            SetFloatCurve(clip, FacePath, typeof(Transform), "m_LocalScale.z", 1.0f);
            return clip;
        }

        private static AnimationClip PrepareClip(string name)
        {
            AnimationClip clip = new AnimationClip { name = name };
            clip.frameRate = 60.0f;
            return clip;
        }

        private static void SetFloatCurve(
            AnimationClip clip,
            string relativePath,
            Type componentType,
            string propertyName,
            float value)
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0.0f, value),
                new Keyframe(1.0f, value));
            curve.preWrapMode = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(relativePath, componentType, propertyName),
                curve);
        }

        private static AnimatorController CreateController(
            AnimationClip onClip,
            AnimationClip offClip,
            AnimationClip posXLow,
            AnimationClip posXHigh,
            AnimationClip posYLow,
            AnimationClip posYHigh,
            AnimationClip yawLow,
            AnimationClip yawHigh,
            AnimationClip pitchLow,
            AnimationClip pitchHigh,
            AnimationClip sideLeft,
            AnimationClip sideRight,
            AnimationClip tick,
            AnimationClip alphaLow,
            AnimationClip alphaHigh,
            AnimationClip eventOnClip,
            AnimationClip eventOffClip,
            bool includeEventRow,
            bool startEventEnabled)
        {
            AnimatorController controller = new AnimatorController { name = "HUDClock_FX" };
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            controller.parameters = Array.Empty<AnimatorControllerParameter>();

            AddParameter(controller, IsLocalParameter, AnimatorControllerParameterType.Bool, false, 0.0f);
            AddParameter(controller, ClockParameter, AnimatorControllerParameterType.Bool, true, 0.0f);
            if (includeEventRow)
                AddParameter(controller, EventParameter, AnimatorControllerParameterType.Bool, startEventEnabled, 0.0f);
            AddParameter(controller, PosXParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, PosYParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, OfsXParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, OfsYParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, RotYawParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, RotPitchParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, RotYawOfsParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, RotPitchOfsParameter, AnimatorControllerParameterType.Float, false, 0.0f);
            AddParameter(controller, SideParameter, AnimatorControllerParameterType.Bool, false, 0.0f);
            AddParameter(controller, AlphaParameter, AnimatorControllerParameterType.Float, false, 1.0f);

            AddClockLayer(controller, onClip, offClip);
            if (includeEventRow)
                AddEventLayer(controller, eventOnClip, eventOffClip);
            AddDriftLayer(controller, "HUDClock_DriftX", PosXParameter, OfsXParameter, tick);
            AddDriftLayer(controller, "HUDClock_DriftY", PosYParameter, OfsYParameter, tick);
            AddDriftLayer(controller, "HUDClock_DriftYaw", RotYawParameter, RotYawOfsParameter, tick);
            AddDriftLayer(controller, "HUDClock_DriftPitch", RotPitchParameter, RotPitchOfsParameter, tick);
            AddSideLayer(controller, sideLeft, sideRight);
            AddAdjustmentLayer(controller, "HUDClock_PosX", OfsXParameter, posXLow, posXHigh, -1.0f, 1.0f);
            AddAdjustmentLayer(controller, "HUDClock_PosY", OfsYParameter, posYLow, posYHigh, -1.0f, 1.0f);
            AddAdjustmentLayer(controller, "HUDClock_Yaw", RotYawOfsParameter, yawLow, yawHigh, -1.0f, 1.0f);
            AddAdjustmentLayer(controller, "HUDClock_Pitch", RotPitchOfsParameter, pitchLow, pitchHigh, -1.0f, 1.0f);
            AddAdjustmentLayer(controller, "HUDClock_Alpha", AlphaParameter, alphaLow, alphaHigh, 0.0f, 1.0f);

            return controller;
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            bool defaultBool,
            float defaultFloat)
        {
            controller.AddParameter(name, type);
            // controller.parameters returns a fresh array each time, so mutating an
            // element in place is discarded. The array has to be assigned back.
            AnimatorControllerParameter[] parameters = controller.parameters;
            parameters[parameters.Length - 1].defaultBool = defaultBool;
            parameters[parameters.Length - 1].defaultFloat = defaultFloat;
            controller.parameters = parameters;
        }

        private static void AddClockLayer(
            AnimatorController controller,
            AnimationClip onClip,
            AnimationClip offClip)
        {
            AnimatorStateMachine stateMachine = CreateStateMachine(controller, "HUDClock");
            AnimatorState off = stateMachine.AddState("Off", new Vector3(220.0f, 80.0f));
            AnimatorState on = stateMachine.AddState("On", new Vector3(500.0f, 80.0f));
            off.motion = offClip;
            on.motion = onClip;
            off.writeDefaultValues = false;
            on.writeDefaultValues = false;
            stateMachine.defaultState = off;

            AnimatorStateTransition offToOn = off.AddTransition(on);
            ConfigureImmediateTransition(offToOn);
            offToOn.AddCondition(AnimatorConditionMode.If, 0.0f, IsLocalParameter);
            offToOn.AddCondition(AnimatorConditionMode.If, 0.0f, ClockParameter);

            AnimatorStateTransition onToOffLocal = on.AddTransition(off);
            ConfigureImmediateTransition(onToOffLocal);
            onToOffLocal.AddCondition(AnimatorConditionMode.IfNot, 0.0f, IsLocalParameter);

            AnimatorStateTransition onToOffToggle = on.AddTransition(off);
            ConfigureImmediateTransition(onToOffToggle);
            onToOffToggle.AddCondition(AnimatorConditionMode.IfNot, 0.0f, ClockParameter);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "HUDClock",
                defaultWeight = 1.0f,
                stateMachine = stateMachine
            });
        }

        private static void AddEventLayer(
            AnimatorController controller,
            AnimationClip onClip,
            AnimationClip offClip)
        {
            AnimatorStateMachine stateMachine = CreateStateMachine(controller, "HUDClock_Event");
            AnimatorState off = stateMachine.AddState("Off", new Vector3(220.0f, 80.0f));
            AnimatorState on = stateMachine.AddState("On", new Vector3(500.0f, 80.0f));
            off.motion = offClip;
            on.motion = onClip;
            off.writeDefaultValues = false;
            on.writeDefaultValues = false;
            stateMachine.defaultState = off;

            AnimatorStateTransition offToOn = off.AddTransition(on);
            ConfigureImmediateTransition(offToOn);
            offToOn.AddCondition(AnimatorConditionMode.If, 0.0f, IsLocalParameter);
            offToOn.AddCondition(AnimatorConditionMode.If, 0.0f, EventParameter);

            AnimatorStateTransition onToOffLocal = on.AddTransition(off);
            ConfigureImmediateTransition(onToOffLocal);
            onToOffLocal.AddCondition(AnimatorConditionMode.IfNot, 0.0f, IsLocalParameter);

            AnimatorStateTransition onToOffToggle = on.AddTransition(off);
            ConfigureImmediateTransition(onToOffToggle);
            onToOffToggle.AddCondition(AnimatorConditionMode.IfNot, 0.0f, EventParameter);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "HUDClock_Event",
                defaultWeight = 1.0f,
                stateMachine = stateMachine
            });
        }

        private static void AddDriftLayer(
            AnimatorController controller,
            string layerName,
            string inputParameter,
            string accumulatorParameter,
            AnimationClip tickClip)
        {
            AnimatorStateMachine stateMachine = CreateStateMachine(controller, layerName);
            AnimatorState idle = stateMachine.AddState("Idle", new Vector3(180.0f, 80.0f));
            AnimatorState plusA = stateMachine.AddState("PlusA", new Vector3(400.0f, 20.0f));
            AnimatorState plusB = stateMachine.AddState("PlusB", new Vector3(620.0f, 20.0f));
            AnimatorState minusA = stateMachine.AddState("MinusA", new Vector3(400.0f, 140.0f));
            AnimatorState minusB = stateMachine.AddState("MinusB", new Vector3(620.0f, 140.0f));
            AnimatorState[] states = { idle, plusA, plusB, minusA, minusB };
            foreach (AnimatorState state in states)
            {
                state.motion = tickClip;
                state.writeDefaultValues = false;
            }

            AddDriftDriver(plusA, accumulatorParameter, DriftStep);
            AddDriftDriver(plusB, accumulatorParameter, DriftStep);
            AddDriftDriver(minusA, accumulatorParameter, -DriftStep);
            AddDriftDriver(minusB, accumulatorParameter, -DriftStep);
            stateMachine.defaultState = idle;

            float positiveThreshold = DriftDeadzone;
            float negativeThreshold = -DriftDeadzone;
            float positiveLimit = DriftLimit;
            float negativeLimit = -DriftLimit;

            AnimatorStateTransition idleToPlus = idle.AddTransition(plusA);
            ConfigureImmediateTransition(idleToPlus);
            idleToPlus.AddCondition(AnimatorConditionMode.If, 0.0f, IsLocalParameter);
            idleToPlus.AddCondition(AnimatorConditionMode.Greater, positiveThreshold, inputParameter);
            idleToPlus.AddCondition(AnimatorConditionMode.Less, positiveLimit, accumulatorParameter);

            AnimatorStateTransition idleToMinus = idle.AddTransition(minusA);
            ConfigureImmediateTransition(idleToMinus);
            idleToMinus.AddCondition(AnimatorConditionMode.If, 0.0f, IsLocalParameter);
            idleToMinus.AddCondition(AnimatorConditionMode.Less, negativeThreshold, inputParameter);
            idleToMinus.AddCondition(AnimatorConditionMode.Greater, negativeLimit, accumulatorParameter);

            AddDriftRunningTransitions(plusA, idle, plusB, inputParameter, accumulatorParameter,
                AnimatorConditionMode.Less, positiveThreshold, AnimatorConditionMode.Greater, positiveLimit);
            AddDriftRunningTransitions(plusB, idle, plusA, inputParameter, accumulatorParameter,
                AnimatorConditionMode.Less, positiveThreshold, AnimatorConditionMode.Greater, positiveLimit);
            AddDriftRunningTransitions(minusA, idle, minusB, inputParameter, accumulatorParameter,
                AnimatorConditionMode.Greater, negativeThreshold, AnimatorConditionMode.Less, negativeLimit);
            AddDriftRunningTransitions(minusB, idle, minusA, inputParameter, accumulatorParameter,
                AnimatorConditionMode.Greater, negativeThreshold, AnimatorConditionMode.Less, negativeLimit);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1.0f,
                stateMachine = stateMachine
            });
        }

        private static void AddDriftDriver(
            AnimatorState state,
            string accumulatorParameter,
            float value)
        {
            VRCAvatarParameterDriver driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;
            driver.parameters.Add(new VRCAvatarParameterDriver.Parameter
            {
                type = VRCAvatarParameterDriver.ChangeType.Add,
                name = accumulatorParameter,
                value = value
            });
        }

        private static void AddDriftRunningTransitions(
            AnimatorState running,
            AnimatorState idle,
            AnimatorState nextTick,
            string inputParameter,
            string accumulatorParameter,
            AnimatorConditionMode inputMode,
            float inputThreshold,
            AnimatorConditionMode accumulatorMode,
            float accumulatorThreshold)
        {
            AnimatorStateTransition inputStop = running.AddTransition(idle);
            ConfigureImmediateTransition(inputStop);
            inputStop.AddCondition(inputMode, inputThreshold, inputParameter);

            AnimatorStateTransition limitStop = running.AddTransition(idle);
            ConfigureImmediateTransition(limitStop);
            limitStop.AddCondition(accumulatorMode, accumulatorThreshold, accumulatorParameter);

            AnimatorStateTransition localStop = running.AddTransition(idle);
            ConfigureImmediateTransition(localStop);
            localStop.AddCondition(AnimatorConditionMode.IfNot, 0.0f, IsLocalParameter);

            AnimatorStateTransition tick = running.AddTransition(nextTick);
            tick.hasExitTime = true;
            tick.exitTime = DriftTickSeconds;
            tick.duration = 0.0f;
            tick.offset = 0.0f;
        }

        private static void AddSideLayer(
            AnimatorController controller,
            AnimationClip leftClip,
            AnimationClip rightClip)
        {
            AnimatorStateMachine stateMachine = CreateStateMachine(controller, "HUDClock_Side");
            AnimatorState left = stateMachine.AddState("Left", new Vector3(260.0f, 80.0f));
            AnimatorState right = stateMachine.AddState("Right", new Vector3(520.0f, 80.0f));
            left.motion = leftClip;
            right.motion = rightClip;
            left.writeDefaultValues = false;
            right.writeDefaultValues = false;
            stateMachine.defaultState = left;

            AnimatorStateTransition leftToRight = left.AddTransition(right);
            ConfigureImmediateTransition(leftToRight);
            leftToRight.AddCondition(AnimatorConditionMode.If, 0.0f, SideParameter);

            AnimatorStateTransition rightToLeft = right.AddTransition(left);
            ConfigureImmediateTransition(rightToLeft);
            rightToLeft.AddCondition(AnimatorConditionMode.IfNot, 0.0f, SideParameter);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "HUDClock_Side",
                defaultWeight = 1.0f,
                stateMachine = stateMachine
            });
        }

        private static void AddAdjustmentLayer(
            AnimatorController controller,
            string layerName,
            string parameterName,
            AnimationClip lowClip,
            AnimationClip highClip,
            float lowThreshold,
            float highThreshold)
        {
            AnimatorStateMachine stateMachine = CreateStateMachine(controller, layerName);
            BlendTree blendTree = new BlendTree
            {
                name = layerName + "_BlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameterName,
                useAutomaticThresholds = false
            };
            blendTree.AddChild(lowClip, lowThreshold);
            blendTree.AddChild(highClip, highThreshold);

            AnimatorState state = stateMachine.AddState("Adjust", new Vector3(320.0f, 80.0f));
            state.motion = blendTree;
            state.writeDefaultValues = false;
            stateMachine.defaultState = state;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1.0f,
                stateMachine = stateMachine
            });
        }

        private static AnimatorStateMachine CreateStateMachine(AnimatorController controller, string name)
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine { name = name };
            return stateMachine;
        }

        private static void ConfigureImmediateTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = 0.0f;
            transition.offset = 0.0f;
        }

        internal static void PopulateHierarchy(
            GameObject root, GeneratedAssets assets, HUDClockComponent settings)
        {
            root.name = "HUDClock";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            ModularAvatarMergeAnimator mergeAnimator = EnsureComponent<ModularAvatarMergeAnimator>(root);
            mergeAnimator.animator = assets.Controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
            // Follow whatever Write Defaults setting the avatar's own FX layers use.
            // Leaving this false merges WD-OFF layers into a WD-ON avatar, and that
            // mismatch is a known cause of blendshape misbehaviour (eyelids drifting
            // shut over repeated blinks). Every layer here is a single-state layer or
            // a pair of states that animate the same property, so it behaves
            // correctly under either setting.
            mergeAnimator.matchAvatarWriteDefaults = true;

            ModularAvatarParameters parameters = EnsureComponent<ModularAvatarParameters>(root);
            parameters.parameters = new List<ParameterConfig>
            {
                Parameter(ClockParameter, ParameterSyncType.Bool, 1.0f, true),
                Parameter(PosXParameter, ParameterSyncType.Float, 0.0f, false),
                Parameter(PosYParameter, ParameterSyncType.Float, 0.0f, false),
                Parameter(OfsXParameter, ParameterSyncType.Float, 0.0f, true),
                Parameter(OfsYParameter, ParameterSyncType.Float, 0.0f, true),
                Parameter(RotYawParameter, ParameterSyncType.Float, 0.0f, false),
                Parameter(RotPitchParameter, ParameterSyncType.Float, 0.0f, false),
                Parameter(RotYawOfsParameter, ParameterSyncType.Float, 0.0f, true),
                Parameter(RotPitchOfsParameter, ParameterSyncType.Float, 0.0f, true),
                Parameter(SideParameter, ParameterSyncType.Bool, 0.0f, true),
                Parameter(AlphaParameter, ParameterSyncType.Float, 1.0f, true)
            };
            if (settings.eventRowEnabled)
                parameters.parameters.Insert(1,
                    Parameter(EventParameter, ParameterSyncType.Bool, 1.0f, true));

            ModularAvatarMenuItem rootMenuItem = EnsureComponent<ModularAvatarMenuItem>(root);
            ConfigureMenuItem(rootMenuItem, "HUD Clock", PortableControlType.SubMenu);
            rootMenuItem.PortableControl.Icon = LoadMenuIcon(MenuIconPath);
            rootMenuItem.MenuSource = SubmenuSource.Children;
            rootMenuItem.PortableControl.SubParameters = ImmutableList<string>.Empty;

            ModularAvatarMenuInstaller menuInstaller = EnsureComponent<ModularAvatarMenuInstaller>(root);
            menuInstaller.menuToAppend = null;
            menuInstaller.installTargetMenu = null;

            // The one avatar-specific thing the HUD needs is the Head bone. MA Bone Proxy
            // resolves it from the humanoid rig at build time, so this generated hierarchy
            // needs no per-avatar bone wiring.
            GameObject headAnchor = EnsureChild(root.transform, "HeadAnchor", out _);
            headAnchor.transform.localPosition = Vector3.zero;
            headAnchor.transform.localRotation = Quaternion.identity;
            headAnchor.transform.localScale = Vector3.one;
            ModularAvatarBoneProxy boneProxy = EnsureComponent<ModularAvatarBoneProxy>(headAnchor);
            boneProxy.boneReference = HumanBodyBones.Head;
            boneProxy.subPath = string.Empty;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            boneProxy.matchScale = false;

            // Anchor deliberately stays at the avatar root and only follows HeadAnchor
            // through a constraint. Parenting it under the Head bone instead would make the
            // display inherit that bone's scale, which differs from avatar to avatar - and
            // re-tuning the size per avatar is exactly what this layout avoids.
            GameObject anchorObject = EnsureChild(root.transform, "Anchor", out _);
            anchorObject.transform.localPosition = Vector3.zero;
            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.transform.localScale = Vector3.one;
            VRCParentConstraint constraint = EnsureComponent<VRCParentConstraint>(anchorObject);
            VRCConstraintSource source = new VRCConstraintSource
            {
                SourceTransform = headAnchor.transform,
                Weight = 1.0f,
                ParentPositionOffset = Vector3.zero,
                ParentRotationOffset = Vector3.zero
            };

            // Sources is a verified keyable list; replacing index zero keeps a rebuild
            // idempotent instead of appending a duplicate source every time.
            if (constraint.Sources.Count == 0)
                constraint.Sources.Add(source);
            else
                constraint.Sources[0] = source;

            constraint.GlobalWeight = 1.0f;
            constraint.IsActive = true;
            constraint.Locked = true;

            GameObject sideObject = EnsureChild(anchorObject.transform, "Side", out _);
            sideObject.transform.localPosition = Vector3.zero;
            sideObject.transform.localRotation = Quaternion.identity;
            sideObject.transform.localScale = Vector3.one;
            GameObject offsetObject = EnsureChild(sideObject.transform, "Offset", out _);
            offsetObject.transform.localPosition = Vector3.zero;
            offsetObject.transform.localRotation = Quaternion.identity;
            offsetObject.transform.localScale = Vector3.one;

            GameObject posXObject = EnsureChild(offsetObject.transform, "PosX", out _);
            GameObject posYObject = EnsureChild(posXObject.transform, "PosY", out _);
            GameObject facingObject = EnsureChild(posYObject.transform, "Facing", out _);
            GameObject yawObject = EnsureChild(facingObject.transform, "Yaw", out _);
            GameObject pitchObject = EnsureChild(yawObject.transform, "Pitch", out _);
            GameObject faceObject = EnsureChild(pitchObject.transform, "Face", out _);
            facingObject.transform.localPosition = Vector3.zero;
            facingObject.transform.localRotation = Quaternion.identity;
            facingObject.transform.localScale = Vector3.one;
            yawObject.transform.localPosition = Vector3.zero;
            yawObject.transform.localRotation = Quaternion.identity;
            yawObject.transform.localScale = Vector3.one;
            pitchObject.transform.localPosition = Vector3.zero;
            pitchObject.transform.localRotation = Quaternion.identity;
            pitchObject.transform.localScale = Vector3.one;
            faceObject.transform.localPosition = Vector3.zero;
            faceObject.transform.localRotation = Quaternion.identity;
            faceObject.transform.localScale = Vector3.one;
            GameObject displayObject = EnsureChild(faceObject.transform, "Display", out _);
            MeshFilter meshFilter = EnsureComponent<MeshFilter>(displayObject);
            meshFilter.sharedMesh = assets.Quad;
            MeshRenderer meshRenderer = EnsureComponent<MeshRenderer>(displayObject);
            meshRenderer.sharedMaterial = assets.Material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            displayObject.SetActive(false);

            Transform staleNestedEventDisplay = displayObject.transform.Find("EventDisplay");
            if (staleNestedEventDisplay != null)
                UnityEngine.Object.DestroyImmediate(staleNestedEventDisplay.gameObject);

            if (assets.EventMaterial != null)
            {
                GameObject eventDisplayObject = EnsureChild(faceObject.transform, "EventDisplay", out _);
                float eventOffsetY = -(1.0f + settings.eventRowGap) * DigitCellHeightMeters;
                eventDisplayObject.transform.localPosition = new Vector3(0.0f, eventOffsetY, 0.0f);
                eventDisplayObject.transform.localRotation = Quaternion.identity;
                eventDisplayObject.transform.localScale = Vector3.one;
                MeshFilter eventMeshFilter = EnsureComponent<MeshFilter>(eventDisplayObject);
                eventMeshFilter.sharedMesh = assets.Quad;
                MeshRenderer eventMeshRenderer = EnsureComponent<MeshRenderer>(eventDisplayObject);
                eventMeshRenderer.sharedMaterial = assets.EventMaterial;
                eventMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                eventMeshRenderer.receiveShadows = false;
                eventMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                eventMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                eventDisplayObject.SetActive(false);
            }
            else
            {
                // Idempotent rebuild: remove a stale EventDisplay left over from a previous
                // build where the event row was enabled.
                Transform staleEventDisplay = faceObject.transform.Find("EventDisplay");
                if (staleEventDisplay != null)
                    UnityEngine.Object.DestroyImmediate(staleEventDisplay.gameObject);
            }

            GameObject tickObject = EnsureTick(root);

            ModularAvatarMenuItem displayMenu =
                CreateMenuEntry(root.transform, "時計", PortableControlType.Toggle, ClockParameter);
            displayMenu.PortableControl.Icon = LoadMenuIcon(DisplayIconPath);

            ModularAvatarMenuItem eventMenu = null;
            if (settings.eventRowEnabled)
            {
                eventMenu = CreateMenuEntry(root.transform, "イベント", PortableControlType.Toggle, EventParameter);
                eventMenu.PortableControl.Icon = LoadMenuIcon(DisplayIconPath);
            }

            ModularAvatarMenuItem positionMenu =
                CreateMenuEntry(root.transform, "位置", PortableControlType.TwoAxisPuppet, null,
                    PosXParameter, PosYParameter);
            positionMenu.PortableControl.Icon = LoadMenuIcon(PositionIconPath);

            ModularAvatarMenuItem rotationMenu =
                CreateMenuEntry(root.transform, "回転", PortableControlType.TwoAxisPuppet, null,
                    RotYawParameter, RotPitchParameter);
            rotationMenu.PortableControl.Icon = LoadMenuIcon(RotationIconPath);

            ModularAvatarMenuItem sideMenu =
                CreateMenuEntry(root.transform, "左右", PortableControlType.Toggle, SideParameter);
            sideMenu.PortableControl.Icon = LoadMenuIcon(SideIconPath);

            ModularAvatarMenuItem opacityMenu =
                CreateMenuEntry(root.transform, "透過度", PortableControlType.RadialPuppet, null,
                    AlphaParameter);
            opacityMenu.PortableControl.Icon = LoadMenuIcon(OpacityIconPath);

            headAnchor.transform.SetSiblingIndex(0);
            tickObject.transform.SetSiblingIndex(1);
            anchorObject.transform.SetSiblingIndex(2);
            displayMenu.transform.SetSiblingIndex(3);
            int menuIndex = 4;
            if (eventMenu != null)
                eventMenu.transform.SetSiblingIndex(menuIndex++);
            positionMenu.transform.SetSiblingIndex(menuIndex++);
            rotationMenu.transform.SetSiblingIndex(menuIndex++);
            sideMenu.transform.SetSiblingIndex(menuIndex++);
            opacityMenu.transform.SetSiblingIndex(menuIndex);
        }

        // Tick has no visual role; its harmless curve makes normalized exitTime equal real
        // seconds for the drift ping-pong states instead of leaving a zero-length clip.
        private static GameObject EnsureTick(GameObject root)
        {
            GameObject tick = EnsureChild(root.transform, "Tick", out _);
            tick.transform.localPosition = Vector3.zero;
            tick.transform.localRotation = Quaternion.identity;
            tick.transform.localScale = Vector3.one;
            return tick;
        }

        private static GameObject EnsureChild(Transform parent, string name, out bool created)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                created = false;
                return child.gameObject;
            }

            GameObject childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            created = true;
            return childObject;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        // MA's ParameterAssignerPass only auto-creates an expression parameter from a
        // MenuItem's MAIN parameter, never from a puppet's sub-parameters, and
        // ParameterSyncType.NotSynced does not register the parameter at all. So the
        // parameters are declared explicitly with their real type plus localOnly, which
        // MA maps to networkSynced = false - registered and saveable, zero sync cost.
        private static ParameterConfig Parameter(
            string name,
            ParameterSyncType syncType,
            float defaultValue,
            bool saved)
        {
            return new ParameterConfig
            {
                nameOrPrefix = name,
                syncType = syncType,
                localOnly = true,
                saved = saved,
                defaultValue = defaultValue,
                hasExplicitDefaultValue = true
            };
        }

        private static void ConfigureMenuItem(
            ModularAvatarMenuItem menuItem,
            string label,
            PortableControlType type)
        {
            menuItem.label = label;
            menuItem.isSynced = false;
            menuItem.isSaved = true;
            menuItem.isDefault = false;
            menuItem.automaticValue = false;
            menuItem.PortableControl.Type = type;
            menuItem.PortableControl.Value = 1.0f;
        }

        private static ModularAvatarMenuItem CreateMenuEntry(
            Transform parent,
            string name,
            PortableControlType type,
            string parameter,
            params string[] subParameters)
        {
            GameObject entryObject = EnsureChild(parent, name, out _);
            ModularAvatarMenuItem menuItem = EnsureComponent<ModularAvatarMenuItem>(entryObject);
            ConfigureMenuItem(menuItem, name, type);
            menuItem.PortableControl.Parameter = parameter ?? string.Empty;
            menuItem.PortableControl.SubParameters =
                subParameters == null
                    ? ImmutableList<string>.Empty
                    : ImmutableList.Create(subParameters);
            return menuItem;
        }

        private static Texture2D LoadMenuIcon(string path)
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon == null)
                throw new InvalidOperationException("HUD Clock menu icon is missing: " + path);

            return icon;
        }

    }
}

#endif
