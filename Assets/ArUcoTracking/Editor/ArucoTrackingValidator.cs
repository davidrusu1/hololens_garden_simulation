using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ICI.ArucoTracking.Editor
{
    public static class ArucoTrackingValidator
    {
        private const int TestWidth = 640;
        private const int TestHeight = 480;
        private const int MarkerSidePixels = 240;

        [MenuItem("Tools/ArUco/Validate Project And Detector")]
        public static void ValidateProjectAndDetector()
        {
            bool webcamEnabled = PlayerSettings.WSA.GetCapability(
                PlayerSettings.WSACapability.WebCam);
            bool detectorWorks = RunSyntheticDetectorTest(out string detail);

            GameObject content = GameObject.Find("Garden Content")
                ?? GameObject.Find("Plant_AR_Root")
                ?? GameObject.Find("Sunflower Simulation")
                ?? GameObject.Find("Plant");

            string message =
                "ArUco validation\n"
                + $"- WebCam capability: {(webcamEnabled ? "OK" : "MISSING")}\n"
                + $"- DICT_4X4_50 / ID 0 detector: {(detectorWorks ? "OK" : "FAILED")} ({detail})\n"
                + $"- Runtime content target: {(content != null ? content.name : "MISSING")}\n"
                + "- HoloLens runtime bootstrap: enabled for UWP builds";

            if (webcamEnabled && detectorWorks && content != null)
            {
                Debug.Log("[ArUco] " + message);
            }
            else
            {
                Debug.LogError("[ArUco] " + message);
            }
        }

        [MenuItem("Tools/ArUco/Run Detector Self-Test")]
        public static void RunDetectorSelfTest()
        {
            bool success = RunSyntheticDetectorTest(out string detail);
            if (success)
            {
                Debug.Log("[ArUco] Detector self-test passed: " + detail);
            }
            else
            {
                Debug.LogError("[ArUco] Detector self-test failed: " + detail);
            }
        }

        private static bool RunSyntheticDetectorTest(out string detail)
        {
            float minimumConfidence = 1f;
            for (int rotation = 0; rotation < 4; rotation++)
            {
                byte[] frame = CreateSyntheticMarkerFrame(rotation);
                var detector = new ArucoMarkerDetector();
                bool detected = detector.TryDetect(
                    frame,
                    TestWidth,
                    TestHeight,
                    false,
                    out ArucoDetection result);
                if (!detected)
                {
                    detail = $"marker not found at {rotation * 90} degrees";
                    return false;
                }

                bool correctId = result.MarkerId
                    == ArucoMarkerDetector.MarkerId;
                bool hasCorners = result.Corners != null
                    && result.Corners.Length == 4;
                if (!correctId || !hasCorners)
                {
                    detail = $"invalid result at {rotation * 90} degrees";
                    return false;
                }

                minimumConfidence = Mathf.Min(
                    minimumConfidence,
                    result.Confidence);
            }

            detail = $"4 rotations passed, minimum confidence={minimumConfidence:F2}";
            return true;
        }

        private static byte[] CreateSyntheticMarkerFrame(int clockwiseRotations)
        {
            byte[] frame = new byte[TestWidth * TestHeight * 4];
            for (int pixel = 0; pixel < TestWidth * TestHeight; pixel++)
            {
                int offset = pixel * 4;
                frame[offset] = 255;
                frame[offset + 1] = 255;
                frame[offset + 2] = 255;
                frame[offset + 3] = 255;
            }

            int cellSize = MarkerSidePixels / ArucoMarkerDetector.MarkerCells;
            int startX = (TestWidth - MarkerSidePixels) / 2;
            int startY = (TestHeight - MarkerSidePixels) / 2;
            string[] pattern =
            {
                "000000",
                "010110",
                "001010",
                "000110",
                "000100",
                "000000"
            };

            for (int row = 0; row < pattern.Length; row++)
            {
                for (int column = 0; column < pattern[row].Length; column++)
                {
                    GetRotatedCell(
                        pattern,
                        row,
                        column,
                        clockwiseRotations,
                        out int sourceRow,
                        out int sourceColumn);
                    if (pattern[sourceRow][sourceColumn] != '0')
                    {
                        continue;
                    }

                    FillRectangle(
                        frame,
                        startX + column * cellSize,
                        startY + row * cellSize,
                        cellSize,
                        cellSize,
                        0);
                }
            }

            return frame;
        }

        private static void GetRotatedCell(
            IReadOnlyList<string> pattern,
            int destinationRow,
            int destinationColumn,
            int clockwiseRotations,
            out int sourceRow,
            out int sourceColumn)
        {
            int size = pattern.Count;
            sourceRow = destinationRow;
            sourceColumn = destinationColumn;
            for (int rotation = 0; rotation < clockwiseRotations; rotation++)
            {
                int previousRow = sourceRow;
                sourceRow = size - 1 - sourceColumn;
                sourceColumn = previousRow;
            }
        }

        private static void FillRectangle(
            IList<byte> frame,
            int x,
            int y,
            int width,
            int height,
            byte value)
        {
            for (int row = y; row < y + height; row++)
            {
                for (int column = x; column < x + width; column++)
                {
                    int offset = (row * TestWidth + column) * 4;
                    frame[offset] = value;
                    frame[offset + 1] = value;
                    frame[offset + 2] = value;
                    frame[offset + 3] = 255;
                }
            }
        }
    }
}
