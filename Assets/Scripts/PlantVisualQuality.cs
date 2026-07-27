using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class PlantVisualQuality
{
    private readonly struct RendererState
    {
        public RendererState(Renderer renderer)
        {
            Enabled = renderer.enabled;
            ShadowCastingMode = renderer.shadowCastingMode;
            ReceiveShadows = renderer.receiveShadows;
            LightProbeUsage = renderer.lightProbeUsage;
            ReflectionProbeUsage = renderer.reflectionProbeUsage;
            MotionVectorGenerationMode =
                renderer.motionVectorGenerationMode;
        }

        public bool Enabled { get; }
        public ShadowCastingMode ShadowCastingMode { get; }
        public bool ReceiveShadows { get; }
        public LightProbeUsage LightProbeUsage { get; }
        public ReflectionProbeUsage ReflectionProbeUsage { get; }
        public MotionVectorGenerationMode MotionVectorGenerationMode { get; }
    }

    private readonly struct CameraState
    {
        public CameraState(Camera camera)
        {
            AllowHdr = camera.allowHDR;
            AllowMsaa = camera.allowMSAA;
        }

        public bool AllowHdr { get; }
        public bool AllowMsaa { get; }
    }

    private static readonly Dictionary<Renderer, RendererState>
        RendererStates = new Dictionary<Renderer, RendererState>();
    private static readonly Dictionary<Camera, CameraState>
        CameraStates = new Dictionary<Camera, CameraState>();
    private static readonly Dictionary<Light, LightShadows>
        LightStates = new Dictionary<Light, LightShadows>();
    private static readonly Dictionary<Volume, bool>
        VolumeStates = new Dictionary<Volume, bool>();

    private static bool globalStateCaptured;
    private static ShadowQuality originalShadows;
    private static float originalShadowDistance;
    private static int originalPixelLightCount;
    private static int originalAntiAliasing;
    private static AnisotropicFiltering originalAnisotropicFiltering;
    private static bool originalRealtimeReflectionProbes;
    private static bool originalSoftParticles;
    private static bool originalFog;

    public static bool LiteModeEnabled { get; private set; }

    public static void SetLiteMode(bool enabled, GameObject plantRoot)
    {
        LiteModeEnabled = enabled;
        if (enabled)
        {
            CaptureGlobalState();
            ApplyLiteGlobalSettings();
        }
        else
        {
            RestoreGlobalState();
        }

        ApplySceneVisuals(plantRoot);
    }

    public static void ApplyRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        if (LiteModeEnabled)
        {
            if (!RendererStates.ContainsKey(renderer))
            {
                RendererStates.Add(renderer, new RendererState(renderer));
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            if (renderer.gameObject.name == "Maple Leaf Veins")
            {
                renderer.enabled = false;
            }

            return;
        }

        if (RendererStates.TryGetValue(
                renderer,
                out RendererState originalState))
        {
            RestoreRenderer(renderer, originalState);
            RendererStates.Remove(renderer);
        }
    }

    private static void ApplySceneVisuals(GameObject plantRoot)
    {
        if (plantRoot != null)
        {
            Renderer[] renderers =
                plantRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                ApplyRenderer(renderers[index]);
            }
        }

        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int index = 0; index < cameras.Length; index++)
        {
            ApplyCamera(cameras[index]);
        }

        Light[] lights = Object.FindObjectsOfType<Light>(true);
        for (int index = 0; index < lights.Length; index++)
        {
            ApplyLight(lights[index]);
        }

        Volume[] volumes = Object.FindObjectsOfType<Volume>(true);
        for (int index = 0; index < volumes.Length; index++)
        {
            ApplyVolume(volumes[index]);
        }

        if (!LiteModeEnabled)
        {
            RestoreRemainingRenderers();
        }
    }

    private static void ApplyCamera(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (LiteModeEnabled)
        {
            if (!CameraStates.ContainsKey(camera))
            {
                CameraStates.Add(camera, new CameraState(camera));
            }

            camera.allowHDR = false;
            camera.allowMSAA = false;
            return;
        }

        if (CameraStates.TryGetValue(camera, out CameraState originalState))
        {
            camera.allowHDR = originalState.AllowHdr;
            camera.allowMSAA = originalState.AllowMsaa;
            CameraStates.Remove(camera);
        }
    }

    private static void ApplyLight(Light lightSource)
    {
        if (lightSource == null)
        {
            return;
        }

        if (LiteModeEnabled)
        {
            if (!LightStates.ContainsKey(lightSource))
            {
                LightStates.Add(lightSource, lightSource.shadows);
            }

            lightSource.shadows = LightShadows.None;
            return;
        }

        if (LightStates.TryGetValue(
                lightSource,
                out LightShadows originalShadowsValue))
        {
            lightSource.shadows = originalShadowsValue;
            LightStates.Remove(lightSource);
        }
    }

    private static void ApplyVolume(Volume volume)
    {
        if (volume == null)
        {
            return;
        }

        if (LiteModeEnabled)
        {
            if (!VolumeStates.ContainsKey(volume))
            {
                VolumeStates.Add(volume, volume.enabled);
            }

            volume.enabled = false;
            return;
        }

        if (VolumeStates.TryGetValue(volume, out bool originallyEnabled))
        {
            volume.enabled = originallyEnabled;
            VolumeStates.Remove(volume);
        }
    }

    private static void CaptureGlobalState()
    {
        if (globalStateCaptured)
        {
            return;
        }

        globalStateCaptured = true;
        originalShadows = QualitySettings.shadows;
        originalShadowDistance = QualitySettings.shadowDistance;
        originalPixelLightCount = QualitySettings.pixelLightCount;
        originalAntiAliasing = QualitySettings.antiAliasing;
        originalAnisotropicFiltering =
            QualitySettings.anisotropicFiltering;
        originalRealtimeReflectionProbes =
            QualitySettings.realtimeReflectionProbes;
        originalSoftParticles = QualitySettings.softParticles;
        originalFog = RenderSettings.fog;
    }

    private static void ApplyLiteGlobalSettings()
    {
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowDistance = 0f;
        QualitySettings.pixelLightCount = 0;
        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering =
            AnisotropicFiltering.Disable;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        RenderSettings.fog = false;
    }

    private static void RestoreGlobalState()
    {
        if (!globalStateCaptured)
        {
            return;
        }

        QualitySettings.shadows = originalShadows;
        QualitySettings.shadowDistance = originalShadowDistance;
        QualitySettings.pixelLightCount = originalPixelLightCount;
        QualitySettings.antiAliasing = originalAntiAliasing;
        QualitySettings.anisotropicFiltering =
            originalAnisotropicFiltering;
        QualitySettings.realtimeReflectionProbes =
            originalRealtimeReflectionProbes;
        QualitySettings.softParticles = originalSoftParticles;
        RenderSettings.fog = originalFog;
    }

    private static void RestoreRemainingRenderers()
    {
        var renderers = new List<Renderer>(RendererStates.Keys);
        for (int index = 0; index < renderers.Count; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer != null
                && RendererStates.TryGetValue(
                    renderer,
                    out RendererState originalState))
            {
                RestoreRenderer(renderer, originalState);
            }

            RendererStates.Remove(renderer);
        }
    }

    private static void RestoreRenderer(
        Renderer renderer,
        RendererState originalState)
    {
        renderer.enabled = originalState.Enabled;
        renderer.shadowCastingMode = originalState.ShadowCastingMode;
        renderer.receiveShadows = originalState.ReceiveShadows;
        renderer.lightProbeUsage = originalState.LightProbeUsage;
        renderer.reflectionProbeUsage =
            originalState.ReflectionProbeUsage;
        renderer.motionVectorGenerationMode =
            originalState.MotionVectorGenerationMode;
    }
}
