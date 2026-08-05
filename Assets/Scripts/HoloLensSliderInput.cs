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
        if (eventData is TrackedDeviceEventData
            && currentRaycast.gameObject != null)
        {
            Vector3 localWorldPoint = trackRect.InverseTransformPoint(
                currentRaycast.worldPosition);
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
}
