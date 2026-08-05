using System;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlantLSystem : MonoBehaviour
{
    private const float GoldenAngle = 137.5f;
    private const int MaximumTraceLength = 4096;

    [Header("L-system seed")]
    [SerializeField]
    private bool randomizeSeedOnPlay = true;

    [SerializeField]
    private int fixedSeed = 17321;

    [SerializeField]
    private int activeSeed;

    [Header("Parametric rule variation")]
    [SerializeField]
    private Vector2 lengthScaleVariation = new Vector2(0.92f, 1.08f);

    [SerializeField]
    private Vector2 thicknessScaleVariation = new Vector2(0.94f, 1.06f);

    [SerializeField, Range(0f, 0.08f)]
    private float attachmentFractionJitter = 0.025f;

    [SerializeField, Range(0f, 15f)]
    private float tiltJitterDegrees = 6f;

    [SerializeField, Range(0f, 30f)]
    private float azimuthJitterDegrees = 12f;

    [Header("Runtime derivation")]
    [SerializeField, TextArea(3, 8)]
    private string latestDerivation = "F0";

    private System.Random random;
    private readonly StringBuilder traceBuilder = new StringBuilder();

    public int ActiveSeed => activeSeed;
    public string LatestDerivation => latestDerivation;
    public bool RandomizesSeedOnPlay => randomizeSeedOnPlay;

    public readonly struct StemPlan
    {
        public StemPlan(
            int order,
            float attachmentFraction,
            float preferredAzimuth,
            float tiltDegrees,
            float lengthMultiplier,
            float thicknessMultiplier,
            int leafBudget,
            int maximumOrder,
            bool isCrownFiller)
        {
            Order = order;
            AttachmentFraction = attachmentFraction;
            PreferredAzimuth = preferredAzimuth;
            TiltDegrees = tiltDegrees;
            LengthMultiplier = lengthMultiplier;
            ThicknessMultiplier = thicknessMultiplier;
            LeafBudget = leafBudget;
            MaximumOrder = maximumOrder;
            IsCrownFiller = isCrownFiller;
        }

        public int Order { get; }
        public float AttachmentFraction { get; }
        public float PreferredAzimuth { get; }
        public float TiltDegrees { get; }
        public float LengthMultiplier { get; }
        public float ThicknessMultiplier { get; }
        public int LeafBudget { get; }
        public int MaximumOrder { get; }
        public bool IsCrownFiller { get; }
    }

    public void BeginPlant(Branch rootStem)
    {
        if (random != null)
        {
            return;
        }

        activeSeed = randomizeSeedOnPlay
            ? unchecked(
                Environment.TickCount
                ^ GetInstanceID() * 397
                ^ (int)DateTime.UtcNow.Ticks)
            : fixedSeed;
        random = new System.Random(activeSeed);
        traceBuilder.Clear();
        traceBuilder.Append("Axiom: F0");
        latestDerivation = traceBuilder.ToString();
    }

    public void RestartPlantPreservingSeed(Branch rootStem)
    {
        if (activeSeed == 0)
        {
            random = null;
            BeginPlant(rootStem);
            return;
        }

        random = new System.Random(activeSeed);
        traceBuilder.Clear();
        traceBuilder.Append("Axiom: F0");
        latestDerivation = traceBuilder.ToString();
    }

    public int CreateRootLeafBudget(Branch rootStem)
    {
        EnsureInitialized(rootStem);
        return CreateLeafBudget(rootStem, 0);
    }

    public StemPlan[] RewriteStem(Branch parentStem)
    {
        EnsureInitialized(parentStem);
        int childOrder = parentStem.currGen + 1;
        if (childOrder > parentStem.maxGen)
        {
            return Array.Empty<StemPlan>();
        }

        int childCount = GetChildCount(parentStem, childOrder);
        //int crownFillerCount = parentStem.currGen == 0
        //    ? NextInclusive(
        //        parentStem.crownFillerBranchesMin,
        //        parentStem.crownFillerBranchesMax)
        //    : 0;
        int crownFillerCount = parentStem.currGen == 0 ? 6 : 0;
        int totalChildCount = childCount + crownFillerCount;
        if (totalChildCount <= 0)
        {
            AppendRuleTrace(parentStem.currGen, Array.Empty<StemPlan>());
            return Array.Empty<StemPlan>();
        }

        GetAttachmentZone(
            parentStem,
            out float attachmentStart,
            out float attachmentEnd);
        var plans = new StemPlan[totalChildCount];
        float[] attachmentFractions = CreateAttachmentFractions(
            attachmentStart,
            attachmentEnd,
            childCount);
        for (int index = 0; index < childCount; index++)
        {
            float attachmentFraction = attachmentFractions[index];
            float progress = Mathf.InverseLerp(
                attachmentStart,
                attachmentEnd,
                attachmentFraction);
            float baseTilt = Mathf.Lerp(
                parentStem.lowerChildBranchAngle,
                parentStem.upperChildBranchAngle,
                progress);
            float tilt = Mathf.Clamp(
                baseTilt + NextSignedFloat() * tiltJitterDegrees,
                5f,
                80f);
            float azimuth = index * GoldenAngle
                + parentStem.currGen * 41f
                + NextSignedFloat() * azimuthJitterDegrees;
            float primaryLengthBoost = childOrder == 1
                ? parentStem.primaryBranchLengthBoost
                : 1f;
            float lengthMultiplier = Mathf.Clamp(
                parentStem.childLengthMultiplier
                * primaryLengthBoost
                * NextRange(lengthScaleVariation),
                0.35f,
                1.1f);
            float thicknessMultiplier = Mathf.Clamp(
                parentStem.childThicknessMultiplier
                * NextRange(thicknessScaleVariation),
                0.35f,
                0.95f);

            plans[index] = new StemPlan(
                childOrder,
                attachmentFraction,
                azimuth,
                tilt,
                lengthMultiplier,
                thicknessMultiplier,
                CreateLeafBudget(parentStem, childOrder),
                parentStem.maxGen,
                false);
        }

        if (crownFillerCount > 0)
        {
            float[] crownFractions = CreateAttachmentFractions(
                parentStem.crownFillerAttachmentStartFraction,
                parentStem.crownFillerAttachmentEndFraction,
                crownFillerCount);
            for (int fillerIndex = 0;
                fillerIndex < crownFillerCount;
                fillerIndex++)
            {
                float attachmentFraction = crownFractions[fillerIndex];
                float crownProgress = Mathf.InverseLerp(
                    parentStem.crownFillerAttachmentStartFraction,
                    parentStem.crownFillerAttachmentEndFraction,
                    attachmentFraction);
                float tilt = Mathf.Clamp(
                    Mathf.Lerp(
                        parentStem.crownFillerLowerTiltDegrees,
                        parentStem.crownFillerUpperTiltDegrees,
                        crownProgress)
                        + NextSignedFloat() * tiltJitterDegrees * 0.45f,
                    15f,
                    70f);
                float lengthMultiplier = Mathf.Clamp(
                    Mathf.Lerp(
                        parentStem.crownFillerLowerLengthMultiplier,
                        parentStem.crownFillerUpperLengthMultiplier,
                        crownProgress)
                        * NextRange(lengthScaleVariation),
                    0.15f,
                    0.7f);
                float thicknessMultiplier = Mathf.Clamp(
                    parentStem.crownFillerThicknessMultiplier
                        * NextRange(thicknessScaleVariation),
                    0.3f,
                    0.8f);
                float azimuth = (childCount + fillerIndex) * GoldenAngle
                    + 23f
                    + NextSignedFloat() * azimuthJitterDegrees;
                int maximumOrder = Mathf.Min(
                    parentStem.maxGen,
                    childOrder
                        + parentStem.crownFillerDescendantGenerations);
                plans[childCount + fillerIndex] = new StemPlan(
                    childOrder,
                    attachmentFraction,
                    azimuth,
                    tilt,
                    lengthMultiplier,
                    thicknessMultiplier,
                    CreateLeafBudget(parentStem, childOrder),
                    maximumOrder,
                    true);
            }
        }

        AppendRuleTrace(parentStem.currGen, plans);
        return plans;
    }

    private static void GetAttachmentZone(
        Branch parentStem,
        out float start,
        out float end)
    {
        bool attachesToMainTrunk = parentStem.currGen == 0;
        start = attachesToMainTrunk
            ? parentStem.trunkBranchAttachmentStartFraction
            : parentStem.childStemAttachmentStartFraction;
        end = attachesToMainTrunk
            ? parentStem.trunkBranchAttachmentEndFraction
            : parentStem.childStemAttachmentEndFraction;

        if (end < start)
        {
            float swap = start;
            start = end;
            end = swap;
        }
    }

    private float[] CreateAttachmentFractions(
        float start,
        float end,
        int childCount)
    {
        var fractions = new float[childCount];
        float zoneWidth = Mathf.Max(0.001f, end - start);
        float slotWidth = zoneWidth / Mathf.Max(1, childCount);

        // Every branch receives its own height band. Sampling randomly inside
        // that band keeps the silhouette different on every run while making
        // coincident attachment points impossible.
        for (int index = 0; index < childCount; index++)
        {
            float slotCenter = start + (index + 0.5f) * slotWidth;
            float safeJitter = Mathf.Min(
                attachmentFractionJitter,
                slotWidth * 0.35f);
            fractions[index] = Mathf.Clamp(
                slotCenter + NextSignedFloat() * safeJitter,
                start,
                end);
        }

        return fractions;
    }

    public Vector3 SampleInsideUnitSphere(Branch stem)
    {
        EnsureInitialized(stem);
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var sample = new Vector3(
                NextSignedFloat(),
                NextSignedFloat(),
                NextSignedFloat());
            if (sample.sqrMagnitude <= 1f)
            {
                return sample;
            }
        }

        return Vector3.zero;
    }

    private void EnsureInitialized(Branch stem)
    {
        if (random == null)
        {
            BeginPlant(stem);
        }
    }

    private int GetChildCount(Branch stem, int childOrder)
    {
        if (childOrder == 1)
        {
            return NextInclusive(
                stem.firstOrderBranchesMin,
                stem.firstOrderBranchesMax);
        }

        if (childOrder == 2)
        {
            return NextInclusive(
                stem.secondOrderBranchesMin,
                stem.secondOrderBranchesMax);
        }

        if (childOrder == 3)
        {
            return NextInclusive(
                stem.thirdOrderBranchesMin,
                stem.thirdOrderBranchesMax);
        }

        if (childOrder == 4)
        {
            return NextInclusive(
                stem.fourthOrderBranchesMin,
                stem.fourthOrderBranchesMax);
        }

        return NextInclusive(
            stem.fifthOrderBranchesMin,
            stem.fifthOrderBranchesMax);
    }

    private int CreateLeafBudget(Branch stem, int order)
    {
        int randomizedLimit = NextInclusive(
            stem.minimumLeavesPerStem,
            stem.leavesPerStem);
        // Later-order branches stay leafy instead of losing two possible
        // nodes at every recursive level. Physical node spacing and visible
        // stem support still decide how many of these leaves can actually fit.
        return Mathf.Max(0, randomizedLimit - order);
    }

    private int NextInclusive(int minimum, int maximum)
    {
        int safeMinimum = Mathf.Min(minimum, maximum);
        int safeMaximum = Mathf.Max(minimum, maximum);
        return random.Next(safeMinimum, safeMaximum + 1);
    }

    private float NextRange(Vector2 range)
    {
        return Mathf.Lerp(
            range.x,
            range.y,
            (float)random.NextDouble());
    }

    private float NextSignedFloat()
    {
        return (float)random.NextDouble() * 2f - 1f;
    }

    private void AppendRuleTrace(int parentOrder, StemPlan[] plans)
    {
        if (traceBuilder.Length >= MaximumTraceLength)
        {
            return;
        }

        traceBuilder.AppendLine();
        traceBuilder.Append('F');
        traceBuilder.Append(parentOrder);
        traceBuilder.Append(" -> F");
        traceBuilder.Append(parentOrder);
        for (int index = 0; index < plans.Length; index++)
        {
            StemPlan plan = plans[index];
            traceBuilder.Append("[B");
            traceBuilder.Append(plan.Order);
            traceBuilder.Append(" a=");
            traceBuilder.Append(plan.AttachmentFraction.ToString("0.00"));
            traceBuilder.Append(" l=");
            traceBuilder.Append(plan.LengthMultiplier.ToString("0.00"));
            traceBuilder.Append(']');
        }

        if (traceBuilder.Length > MaximumTraceLength)
        {
            traceBuilder.Length = MaximumTraceLength;
        }

        latestDerivation = traceBuilder.ToString();
    }

    private void OnValidate()
    {
        NormalizeRange(ref lengthScaleVariation, 0.75f, 1.2f);
        NormalizeRange(ref thicknessScaleVariation, 0.75f, 1.2f);
        attachmentFractionJitter = Mathf.Clamp(
            attachmentFractionJitter,
            0f,
            0.08f);
        tiltJitterDegrees = Mathf.Clamp(tiltJitterDegrees, 0f, 15f);
        azimuthJitterDegrees = Mathf.Clamp(azimuthJitterDegrees, 0f, 30f);
    }

    private static void NormalizeRange(
        ref Vector2 range,
        float minimum,
        float maximum)
    {
        float lower = Mathf.Clamp(Mathf.Min(range.x, range.y), minimum, maximum);
        float upper = Mathf.Clamp(Mathf.Max(range.x, range.y), lower, maximum);
        range = new Vector2(lower, upper);
    }
}
