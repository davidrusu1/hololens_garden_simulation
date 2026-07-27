# HoloLens 2 build

The maple simulation is configured for HoloLens 2 with:

- Unity 2022.3 LTS and Unity OpenXR 1.14.3
- Universal Windows Platform (UWP)
- ARM64 and IL2CPP
- Direct3D 11
- OpenXR initialized on startup
- single-pass instanced rendering and 16-bit depth submission
- hand tracking, Microsoft hand interaction, eye gaze, and motion-controller profiles
- a transparent holographic camera background
- the `Assets/Main.unity` scene as the only enabled build scene
- the Performant URP quality profile for Windows Store Apps

## Build on Windows

1. Install Unity 2022.3.62f1 with **Universal Windows Platform Build Support** and **Windows Build Support (IL2CPP)**.
2. Open this project and run **Tools > Plant Simulation > Apply HoloLens 2 Settings**.
3. Open **File > Build Settings**, select **Universal Windows Platform**, and switch the platform.
4. Use **D3D Project**, **ARM64**, **Latest installed SDK**, minimum platform **10.0.10240.0**, and the latest installed Visual Studio.
5. Build to a new `Builds/HoloLens2` folder.
6. Open the generated solution in Visual Studio 2022, select **Release**, **ARM64**, and **Device**, then deploy to the paired HoloLens 2.

The Unity UWP build and device deployment require Windows; macOS can edit and validate the Unity project but cannot produce the HoloLens UWP solution.
