using UnityEngine;

/// <summary>
/// Keeps the HoloLens scene directly previewable on macOS without changing
/// the UWP/XR runtime. The existing WASD controller also supports Up/Down.
/// </summary>
public static class GardnerDesktopPreview
{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Configure()
    {
        if (Object.FindObjectOfType<GardnerSimController>(true) == null)
        {
            return;
        }

        Camera desktopCamera = null;
        Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidate = cameras[index];
            if (IsInsideInteractionRig(candidate.transform))
            {
                candidate.enabled = false;
                SetAudioListener(candidate, false);
                continue;
            }

            if (desktopCamera == null
                || candidate.name.Contains("disabled in XR"))
            {
                desktopCamera = candidate;
            }
        }

        if (desktopCamera == null)
        {
            Debug.LogError(
                "[Gardner sim] Nu există o cameră pentru Play Mode pe Mac.");
            return;
        }

        desktopCamera.enabled = true;
        desktopCamera.tag = "MainCamera";
        desktopCamera.nearClipPlane = 0.05f;
        SetAudioListener(desktopCamera, true);

        WASDCameraController movement =
            desktopCamera.GetComponent<WASDCameraController>();
        if (movement == null)
        {
            movement =
                desktopCamera.gameObject.AddComponent<WASDCameraController>();
        }
        movement.enabled = true;

        Debug.Log(
            "[Gardner sim] Previzualizare desktop activă: "
            + "WASD, săgeată sus/jos și click dreapta pentru privire.");
    }

    private static bool IsInsideInteractionRig(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (current.name == "MRInteractionSetup")
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private static void SetAudioListener(Camera camera, bool enabled)
    {
        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = enabled;
        }
    }
#endif
}
