using System;
using System.Collections.Generic;
using UnityEngine;

namespace ICI.ArucoTracking
{
    /// <summary>
    /// Result produced by the dependency-free ArUco detector.
    /// Corners are returned in canonical marker order: top-left, top-right,
    /// bottom-right, bottom-left.
    /// </summary>
    public readonly struct ArucoDetection
    {
        public ArucoDetection(int markerId, Vector2[] corners, float confidence)
        {
            MarkerId = markerId;
            Corners = corners;
            Confidence = confidence;
        }

        public int MarkerId { get; }
        public Vector2[] Corners { get; }
        public float Confidence { get; }
    }

    /// <summary>
    /// Small, allocation-conscious detector dedicated to DICT_4X4_50 marker 0.
    /// It intentionally uses managed C# only so the same code can be compiled by
    /// IL2CPP for HoloLens 2 ARM64 without an OpenCV native plug-in.
    /// </summary>
    public sealed class ArucoMarkerDetector
    {
        public const int MarkerId = 0;
        public const int MarkerCells = 6;

        // OpenCV DICT_4X4_50 marker 0, read row-major with white cells as 1:
        // 1011 / 0101 / 0011 / 0010.
        private const ushort ExpectedCode = 0xB532;
        private const int MaximumWorkingDimension = 512;
        private const int MaximumBitErrors = 1;
        private const int MaximumBorderErrors = 2;

        private byte[] grayscale;
        private byte[] blackMask;
        private byte[] visited;
        private int[] integral;
        private int[] floodQueue;
        private int workingWidth;
        private int workingHeight;

        public bool TryDetect(
            IReadOnlyList<byte> bgra,
            int sourceWidth,
            int sourceHeight,
            bool reversePixelOrder,
            out ArucoDetection detection)
        {
            detection = default;
            if (bgra == null
                || sourceWidth < 48
                || sourceHeight < 48
                || bgra.Count < sourceWidth * sourceHeight * 4)
            {
                return false;
            }

            int downsample = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    Mathf.Max(sourceWidth, sourceHeight)
                    / (float)MaximumWorkingDimension));
            workingWidth = sourceWidth / downsample;
            workingHeight = sourceHeight / downsample;
            int pixelCount = workingWidth * workingHeight;
            EnsureCapacity(pixelCount, (workingWidth + 1) * (workingHeight + 1));
            Array.Clear(visited, 0, pixelCount);

            BuildGrayscale(
                bgra,
                sourceWidth,
                sourceHeight,
                downsample,
                reversePixelOrder);
            BuildAdaptiveThreshold();

            float bestScore = float.NegativeInfinity;
            Vector2[] bestCorners = null;
            int minimumSide = Mathf.Max(14, Mathf.Min(workingWidth, workingHeight) / 28);
            int maximumSide = Mathf.RoundToInt(
                Mathf.Min(workingWidth, workingHeight) * 0.9f);

            for (int y = 1; y < workingHeight - 1; y++)
            {
                int row = y * workingWidth;
                for (int x = 1; x < workingWidth - 1; x++)
                {
                    int index = row + x;
                    if (blackMask[index] == 0 || visited[index] != 0)
                    {
                        continue;
                    }

                    ComponentBounds component = FloodComponent(index);
                    int componentWidth = component.maxX - component.minX + 1;
                    int componentHeight = component.maxY - component.minY + 1;
                    if (componentWidth < minimumSide
                        || componentHeight < minimumSide
                        || componentWidth > maximumSide
                        || componentHeight > maximumSide)
                    {
                        continue;
                    }

                    float aspect = componentWidth / (float)componentHeight;
                    float fill = component.area
                        / (float)(componentWidth * componentHeight);
                    if (aspect < 0.48f
                        || aspect > 2.08f
                        || fill < 0.12f
                        || fill > 0.94f)
                    {
                        continue;
                    }

                    Vector2[] corners = component.GetCorners();
                    if (!IsPlausibleQuadrilateral(corners, minimumSide))
                    {
                        continue;
                    }

                    if (!TryDecode(corners, out int bitErrors, out int borderErrors))
                    {
                        continue;
                    }

                    float perimeter = 0f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        perimeter += Vector2.Distance(
                            corners[corner],
                            corners[(corner + 1) & 3]);
                    }

                    float score = perimeter
                        - bitErrors * 80f
                        - borderErrors * 35f;
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestCorners = corners;
                }
            }

            if (bestCorners == null)
            {
                return false;
            }

            for (int index = 0; index < bestCorners.Length; index++)
            {
                bestCorners[index] *= downsample;
            }

            float confidence = Mathf.Clamp01(0.72f + bestScore / 2400f);
            detection = new ArucoDetection(MarkerId, bestCorners, confidence);
            return true;
        }

        private void EnsureCapacity(int pixelCount, int integralCount)
        {
            if (grayscale == null || grayscale.Length < pixelCount)
            {
                grayscale = new byte[pixelCount];
                blackMask = new byte[pixelCount];
                visited = new byte[pixelCount];
                floodQueue = new int[pixelCount];
            }

            if (integral == null || integral.Length < integralCount)
            {
                integral = new int[integralCount];
            }
        }

        private void BuildGrayscale(
            IReadOnlyList<byte> bgra,
            int sourceWidth,
            int sourceHeight,
            int downsample,
            bool reversePixelOrder)
        {
            int sourcePixelCount = sourceWidth * sourceHeight;
            for (int y = 0; y < workingHeight; y++)
            {
                int sourceY = Mathf.Min(sourceHeight - 1, y * downsample);
                int destinationRow = y * workingWidth;
                int sourceRow = sourceY * sourceWidth;
                for (int x = 0; x < workingWidth; x++)
                {
                    int sourcePixel = sourceRow
                        + Mathf.Min(sourceWidth - 1, x * downsample);
                    if (reversePixelOrder)
                    {
                        sourcePixel = sourcePixelCount - 1 - sourcePixel;
                    }

                    int byteIndex = sourcePixel * 4;
                    int blue = bgra[byteIndex];
                    int green = bgra[byteIndex + 1];
                    int red = bgra[byteIndex + 2];
                    grayscale[destinationRow + x] = (byte)(
                        (red * 77 + green * 150 + blue * 29) >> 8);
                }
            }
        }

        private void BuildAdaptiveThreshold()
        {
            int integralWidth = workingWidth + 1;
            Array.Clear(
                integral,
                0,
                integralWidth * (workingHeight + 1));

            for (int y = 0; y < workingHeight; y++)
            {
                int rowSum = 0;
                int sourceRow = y * workingWidth;
                int destinationRow = (y + 1) * integralWidth;
                int previousRow = y * integralWidth;
                for (int x = 0; x < workingWidth; x++)
                {
                    rowSum += grayscale[sourceRow + x];
                    integral[destinationRow + x + 1] =
                        integral[previousRow + x + 1] + rowSum;
                }
            }

            int radius = Mathf.Clamp(
                Mathf.Min(workingWidth, workingHeight) / 42,
                5,
                18);
            const int thresholdOffset = 7;
            int pixelCount = workingWidth * workingHeight;
            Array.Clear(blackMask, 0, pixelCount);

            for (int y = 1; y < workingHeight - 1; y++)
            {
                int y0 = Mathf.Max(0, y - radius);
                int y1 = Mathf.Min(workingHeight - 1, y + radius);
                int row = y * workingWidth;
                for (int x = 1; x < workingWidth - 1; x++)
                {
                    int x0 = Mathf.Max(0, x - radius);
                    int x1 = Mathf.Min(workingWidth - 1, x + radius);
                    int area = (x1 - x0 + 1) * (y1 - y0 + 1);
                    int sum = RectangleSum(x0, y0, x1, y1, integralWidth);
                    int localMean = sum / Mathf.Max(1, area);
                    blackMask[row + x] = grayscale[row + x]
                        < localMean - thresholdOffset
                        ? (byte)1
                        : (byte)0;
                }
            }
        }

        private int RectangleSum(
            int x0,
            int y0,
            int x1,
            int y1,
            int integralWidth)
        {
            int left = x0;
            int top = y0;
            int right = x1 + 1;
            int bottom = y1 + 1;
            return integral[bottom * integralWidth + right]
                - integral[top * integralWidth + right]
                - integral[bottom * integralWidth + left]
                + integral[top * integralWidth + left];
        }

        private ComponentBounds FloodComponent(int startIndex)
        {
            int head = 0;
            int tail = 0;
            floodQueue[tail++] = startIndex;
            visited[startIndex] = 1;

            int startX = startIndex % workingWidth;
            int startY = startIndex / workingWidth;
            var component = new ComponentBounds(startX, startY);

            while (head < tail)
            {
                int index = floodQueue[head++];
                int x = index % workingWidth;
                int y = index / workingWidth;
                component.Include(x, y);

                int yMin = Mathf.Max(0, y - 1);
                int yMax = Mathf.Min(workingHeight - 1, y + 1);
                int xMin = Mathf.Max(0, x - 1);
                int xMax = Mathf.Min(workingWidth - 1, x + 1);
                for (int neighbourY = yMin; neighbourY <= yMax; neighbourY++)
                {
                    int row = neighbourY * workingWidth;
                    for (int neighbourX = xMin; neighbourX <= xMax; neighbourX++)
                    {
                        int neighbourIndex = row + neighbourX;
                        if (visited[neighbourIndex] != 0
                            || blackMask[neighbourIndex] == 0)
                        {
                            continue;
                        }

                        visited[neighbourIndex] = 1;
                        floodQueue[tail++] = neighbourIndex;
                    }
                }
            }

            return component;
        }

        private static bool IsPlausibleQuadrilateral(
            Vector2[] corners,
            float minimumSide)
        {
            float minimumLength = float.PositiveInfinity;
            float maximumLength = 0f;
            for (int index = 0; index < 4; index++)
            {
                float length = Vector2.Distance(
                    corners[index],
                    corners[(index + 1) & 3]);
                minimumLength = Mathf.Min(minimumLength, length);
                maximumLength = Mathf.Max(maximumLength, length);
            }

            if (minimumLength < minimumSide * 0.65f
                || minimumLength < maximumLength * 0.28f)
            {
                return false;
            }

            float doubledArea = 0f;
            for (int index = 0; index < 4; index++)
            {
                Vector2 current = corners[index];
                Vector2 next = corners[(index + 1) & 3];
                doubledArea += current.x * next.y - next.x * current.y;
            }

            return Mathf.Abs(doubledArea) > minimumSide * minimumSide;
        }

        private bool TryDecode(
            Vector2[] corners,
            out int bestBitErrors,
            out int borderErrors)
        {
            bestBitErrors = int.MaxValue;
            borderErrors = int.MaxValue;
            if (!TryComputeHomography(corners, out double[] homography))
            {
                return false;
            }

            int[] cellMeans = new int[MarkerCells * MarkerCells];
            int[] histogram = new int[256];
            for (int row = 0; row < MarkerCells; row++)
            {
                for (int column = 0; column < MarkerCells; column++)
                {
                    int mean = SampleCell(homography, column, row);
                    cellMeans[row * MarkerCells + column] = mean;
                    histogram[mean]++;
                }
            }

            int threshold = ComputeOtsuThreshold(histogram, cellMeans.Length);
            borderErrors = 0;
            ushort code = 0;
            for (int row = 0; row < MarkerCells; row++)
            {
                for (int column = 0; column < MarkerCells; column++)
                {
                    bool white = cellMeans[row * MarkerCells + column]
                        > threshold;
                    bool isBorder = row == 0
                        || column == 0
                        || row == MarkerCells - 1
                        || column == MarkerCells - 1;
                    if (isBorder)
                    {
                        if (white)
                        {
                            borderErrors++;
                        }
                        continue;
                    }

                    code <<= 1;
                    if (white)
                    {
                        code |= 1;
                    }
                }
            }

            if (borderErrors > MaximumBorderErrors)
            {
                return false;
            }

            Vector2[] orientedCorners = (Vector2[])corners.Clone();
            ushort orientedCode = code;
            for (int rotation = 0; rotation < 4; rotation++)
            {
                int errors = CountBits((ushort)(orientedCode ^ ExpectedCode));
                if (errors < bestBitErrors)
                {
                    bestBitErrors = errors;
                    if (rotation != 0)
                    {
                        Array.Copy(orientedCorners, corners, 4);
                    }
                }

                if (errors <= MaximumBitErrors)
                {
                    Array.Copy(orientedCorners, corners, 4);
                    return true;
                }

                orientedCode = RotateCodeClockwise(orientedCode);
                orientedCorners = RotateCornersClockwise(orientedCorners);
            }

            return false;
        }

        private int SampleCell(double[] homography, int column, int row)
        {
            // Sampling away from cell edges makes the read tolerant of print
            // bleed and moderate perspective blur.
            float[] offsets = { 0.24f, 0.5f, 0.76f };
            int sum = 0;
            int samples = 0;
            for (int yIndex = 0; yIndex < offsets.Length; yIndex++)
            {
                for (int xIndex = 0; xIndex < offsets.Length; xIndex++)
                {
                    double canonicalX = column + offsets[xIndex];
                    double canonicalY = row + offsets[yIndex];
                    double denominator = homography[6] * canonicalX
                        + homography[7] * canonicalY
                        + 1.0;
                    if (Math.Abs(denominator) < 1e-7)
                    {
                        continue;
                    }

                    int imageX = Mathf.RoundToInt((float)(
                        (homography[0] * canonicalX
                            + homography[1] * canonicalY
                            + homography[2])
                        / denominator));
                    int imageY = Mathf.RoundToInt((float)(
                        (homography[3] * canonicalX
                            + homography[4] * canonicalY
                            + homography[5])
                        / denominator));
                    imageX = Mathf.Clamp(imageX, 0, workingWidth - 1);
                    imageY = Mathf.Clamp(imageY, 0, workingHeight - 1);
                    sum += grayscale[imageY * workingWidth + imageX];
                    samples++;
                }
            }

            return samples > 0 ? sum / samples : 0;
        }

        private static int ComputeOtsuThreshold(int[] histogram, int count)
        {
            long totalIntensity = 0;
            for (int intensity = 0; intensity < histogram.Length; intensity++)
            {
                totalIntensity += (long)intensity * histogram[intensity];
            }

            long backgroundIntensity = 0;
            int backgroundCount = 0;
            double bestVariance = -1.0;
            int bestThreshold = 127;
            for (int intensity = 0; intensity < histogram.Length; intensity++)
            {
                backgroundCount += histogram[intensity];
                if (backgroundCount == 0)
                {
                    continue;
                }

                int foregroundCount = count - backgroundCount;
                if (foregroundCount == 0)
                {
                    break;
                }

                backgroundIntensity += (long)intensity * histogram[intensity];
                double backgroundMean = backgroundIntensity
                    / (double)backgroundCount;
                double foregroundMean = (totalIntensity - backgroundIntensity)
                    / (double)foregroundCount;
                double meanDifference = backgroundMean - foregroundMean;
                double variance = (double)backgroundCount
                    * foregroundCount
                    * meanDifference
                    * meanDifference;
                if (variance > bestVariance)
                {
                    bestVariance = variance;
                    bestThreshold = intensity;
                }
            }

            return bestThreshold;
        }

        private static bool TryComputeHomography(
            Vector2[] corners,
            out double[] solution)
        {
            var matrix = new double[8, 9];
            Vector2[] canonical =
            {
                new Vector2(0f, 0f),
                new Vector2(MarkerCells, 0f),
                new Vector2(MarkerCells, MarkerCells),
                new Vector2(0f, MarkerCells)
            };

            for (int index = 0; index < 4; index++)
            {
                double x = canonical[index].x;
                double y = canonical[index].y;
                double u = corners[index].x;
                double v = corners[index].y;
                int row = index * 2;

                matrix[row, 0] = x;
                matrix[row, 1] = y;
                matrix[row, 2] = 1.0;
                matrix[row, 6] = -u * x;
                matrix[row, 7] = -u * y;
                matrix[row, 8] = u;

                matrix[row + 1, 3] = x;
                matrix[row + 1, 4] = y;
                matrix[row + 1, 5] = 1.0;
                matrix[row + 1, 6] = -v * x;
                matrix[row + 1, 7] = -v * y;
                matrix[row + 1, 8] = v;
            }

            return SolveLinearSystem(matrix, out solution);
        }

        private static bool SolveLinearSystem(
            double[,] augmented,
            out double[] solution)
        {
            const int size = 8;
            solution = new double[size];
            for (int pivot = 0; pivot < size; pivot++)
            {
                int bestRow = pivot;
                double bestValue = Math.Abs(augmented[pivot, pivot]);
                for (int row = pivot + 1; row < size; row++)
                {
                    double value = Math.Abs(augmented[row, pivot]);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestRow = row;
                    }
                }

                if (bestValue < 1e-9)
                {
                    return false;
                }

                if (bestRow != pivot)
                {
                    for (int column = pivot; column <= size; column++)
                    {
                        double swap = augmented[pivot, column];
                        augmented[pivot, column] = augmented[bestRow, column];
                        augmented[bestRow, column] = swap;
                    }
                }

                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column <= size; column++)
                {
                    augmented[pivot, column] /= divisor;
                }

                for (int row = 0; row < size; row++)
                {
                    if (row == pivot)
                    {
                        continue;
                    }

                    double factor = augmented[row, pivot];
                    if (Math.Abs(factor) < 1e-12)
                    {
                        continue;
                    }

                    for (int column = pivot; column <= size; column++)
                    {
                        augmented[row, column] -=
                            factor * augmented[pivot, column];
                    }
                }
            }

            for (int row = 0; row < size; row++)
            {
                solution[row] = augmented[row, size];
            }

            return true;
        }

        private static ushort RotateCodeClockwise(ushort code)
        {
            ushort rotated = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    int sourceBit = 15 - (row * 4 + column);
                    bool value = ((code >> sourceBit) & 1) != 0;
                    int destinationRow = column;
                    int destinationColumn = 3 - row;
                    int destinationBit = 15
                        - (destinationRow * 4 + destinationColumn);
                    if (value)
                    {
                        rotated |= (ushort)(1 << destinationBit);
                    }
                }
            }
            return rotated;
        }

        private static Vector2[] RotateCornersClockwise(Vector2[] corners)
        {
            return new[]
            {
                corners[3],
                corners[0],
                corners[1],
                corners[2]
            };
        }

        private static int CountBits(ushort value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= (ushort)(value - 1);
                count++;
            }
            return count;
        }

        private struct ComponentBounds
        {
            public int minX;
            public int minY;
            public int maxX;
            public int maxY;
            public int area;

            private int minimumSum;
            private int maximumSum;
            private int minimumDifference;
            private int maximumDifference;
            private Vector2 topLeft;
            private Vector2 topRight;
            private Vector2 bottomRight;
            private Vector2 bottomLeft;

            public ComponentBounds(int x, int y)
            {
                minX = maxX = x;
                minY = maxY = y;
                area = 0;
                minimumSum = maximumSum = x + y;
                minimumDifference = maximumDifference = x - y;
                topLeft = topRight = bottomRight = bottomLeft =
                    new Vector2(x, y);
            }

            public void Include(int x, int y)
            {
                area++;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);

                int sum = x + y;
                int difference = x - y;
                if (sum < minimumSum)
                {
                    minimumSum = sum;
                    topLeft = new Vector2(x, y);
                }
                if (sum > maximumSum)
                {
                    maximumSum = sum;
                    bottomRight = new Vector2(x, y);
                }
                if (difference > maximumDifference)
                {
                    maximumDifference = difference;
                    topRight = new Vector2(x, y);
                }
                if (difference < minimumDifference)
                {
                    minimumDifference = difference;
                    bottomLeft = new Vector2(x, y);
                }
            }

            public Vector2[] GetCorners()
            {
                return new[]
                {
                    topLeft,
                    topRight,
                    bottomRight,
                    bottomLeft
                };
            }
        }
    }
}
