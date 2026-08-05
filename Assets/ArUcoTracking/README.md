# ArUco tracking for the HoloLens garden

The runtime searches for OpenCV `DICT_4X4_50`, marker ID `0`, with an 80 mm
black-square side. It uses the HoloLens locatable Photo/Video camera and pure
managed C# so the detector can be compiled by IL2CPP for ARM64 without a native
OpenCV plug-in.

## Runtime flow

1. A UWP/HoloLens build automatically creates `ArUco Plant Anchor` after the
   scene loads.
2. `ArucoPhotoCaptureSource` captures BGRA frames without holograms.
3. `ArucoMarkerDetector` finds and decodes marker ID 0.
4. `ArucoPlantAnchor` combines the marker corners with the frame projection and
   camera-to-world matrices.
5. The first available content root is anchored in this order:
   `Garden Content`, `Plant_AR_Root`, `Sunflower Simulation`, `Plant`.
6. If the marker is temporarily lost, the last stable pose is kept.

## Unity verification

Use `Tools > ArUco > Validate Project And Detector`. The check is read-only and
confirms the WebCam capability, the managed detector, and the runtime content
target. It does not modify the scene or project settings.

## Printing

Print `Marker_4X4_50_ID0_80mm.svg` at 100% / Actual Size. Do not use Fit to Page.
Measure the black square after printing: it must be 80 mm on each side. Keep the
white quiet zone and place the marker flat at the desired plant origin.

## Device requirement

Camera pose can only be validated in a HoloLens/UWP build. The Unity Editor can
compile and run the synthetic detector self-test, but it cannot prove the final
physical alignment. The app must have the Windows `WebCam` capability; this
project already declares it.

References:

- Microsoft locatable camera: https://learn.microsoft.com/windows/mixed-reality/develop/unity/locatable-camera-in-unity
- OpenCV ArUco detection: https://docs.opencv.org/4.x/d5/dae/tutorial_aruco_detection.html
