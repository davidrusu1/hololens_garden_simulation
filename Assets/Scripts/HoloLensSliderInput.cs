using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public sealed class HoloLensSliderInput : MonoBehaviour,
    IInitializePotentialDragHandler,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler
{
    [SerializeField] private Slider slider;
    [SerializeField] private RectTransform trackRect;

    public void Initialize(Slider targetSlider, RectTransform targetTrack)
    {
        slider = targetSlider;
        trackRect = targetTrack;
    }

    private void Awake()
    {
        if (trackRect == null)
        {
            trackRect = transform as RectTransform;
        }

        if (slider == null)
        {
            slider = GetComponentInParent<Slider>();
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // Hand rays move only a few screen pixels during a pinch, so the
        // mouse-oriented threshold must not delay the drag.
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyPointerPosition(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ApplyPointerPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        ApplyPointerPosition(eventData);
    }

    private void ApplyPointerPosition(PointerEventData eventData)
    {
        if (slider == null || trackRect == null || !slider.IsInteractable())
        {
            return;
        }

        Vector2 localPoint;
        RaycastResult currentRaycast = eventData.pointerCurrentRaycast;
        if (currentRaycast.gameObject != null
            && (eventData is TrackedDeviceEventData
                || currentRaycast.module is TrackedDeviceGraphicRaycaster)
            && IsFinite(currentRaycast.worldPosition))
        {
            Vector3 localWorldPoint = trackRect.InverseTransformPoint(
                currentRaycast.worldPosition);
            localPoint = new Vector2(localWorldPoint.x, localWorldPoint.y);
        }
        else if (eventData is TrackedDeviceEventData trackedEventData
            && TryGetTrackedRayPoint(trackedEventData, out Vector3 worldPoint))
        {
            Vector3 localWorldPoint =
                trackRect.InverseTransformPoint(worldPoint);
            localPoint = new Vector2(localWorldPoint.x, localWorldPoint.y);
        }
        else
        {
            Camera eventCamera = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    trackRect,
                    eventData.position,
                    eventCamera,
                    out localPoint))
            {
                return;
            }
        }

        Rect rect = trackRect.rect;
        float normalizedValue;
        if (slider.direction == Slider.Direction.LeftToRight
            || slider.direction == Slider.Direction.RightToLeft)
        {
            normalizedValue = Mathf.InverseLerp(
                rect.xMin,
                rect.xMax,
                localPoint.x);
            if (slider.direction == Slider.Direction.RightToLeft)
            {
                normalizedValue = 1f - normalizedValue;
            }
        }
        else
        {
            normalizedValue = Mathf.InverseLerp(
                rect.yMin,
                rect.yMax,
                localPoint.y);
            if (slider.direction == Slider.Direction.TopToBottom)
            {
                normalizedValue = 1f - normalizedValue;
            }
        }

        slider.normalizedValue = Mathf.Clamp01(normalizedValue);
    }

    private bool TryGetTrackedRayPoint(
        TrackedDeviceEventData eventData,
        out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (eventData.rayPoints == null || eventData.rayPoints.Count < 2)
        {
            return false;
        }

        Plane panelPlane = new Plane(trackRect.forward, trackRect.position);
        for (int index = 0; index < eventData.rayPoints.Count - 1; index++)
        {
            Vector3 start = eventData.rayPoints[index];
            Vector3 segment = eventData.rayPoints[index + 1] - start;
            float length = segment.magnitude;
            if (length <= Mathf.Epsilon)
            {
                continue;
            }

            Ray ray = new Ray(start, segment / length);
            if (panelPlane.Raycast(ray, out float distance)
                && distance <= length + 0.001f)
            {
                worldPoint = ray.GetPoint(distance);
                return IsFinite(worldPoint);
            }
        }

        return false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }
}
