using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class UiAspectConstraints : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float tallAspectConstraint = 1.6f;
    [SerializeField, Min(0.01f)] private float wideAspectConstraint = float.PositiveInfinity;
    [SerializeField] private bool fill;

    private RectTransform _rectTransform;
    private RectTransform rectTransform => _rectTransform ??= GetComponent<RectTransform>();

    private void OnDisable()
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = rectTransform.localScale = new Vector3(1, 1, rectTransform.localScale.z);
    }

    private void LateUpdate()
    {
        if (transform.parent is not RectTransform parentRectTrans)
            return;

        var parentRect = parentRectTrans.rect;
        var parentAspect = parentRect.width / parentRect.height;
        if (parentAspect > wideAspectConstraint)
        {
            var xAdjust = 0.5f * (1f - wideAspectConstraint / parentAspect);
            rectTransform.localScale = new Vector3(1, 1, rectTransform.localScale.z);
            rectTransform.anchorMin = new Vector2(xAdjust, 0);
            rectTransform.anchorMax = new Vector2(1 - xAdjust, 1);
        }
        else if (parentAspect < tallAspectConstraint)
        {
            var ratioRatio = parentAspect / tallAspectConstraint;
            var coverage = 0.5f / ratioRatio;
            rectTransform.localScale = new Vector3(ratioRatio, ratioRatio, rectTransform.localScale.z);

            if (fill)
            {
                rectTransform.anchorMin = new Vector2(-coverage + 0.5f, -coverage + 0.5f);
                rectTransform.anchorMax = new Vector2(coverage + 0.5f, coverage + 0.5f);
            }
            else
            {
                rectTransform.anchorMin = new Vector2(-coverage + 0.5f, 0);
                rectTransform.anchorMax = new Vector2(coverage + 0.5f, 1);
            }
        }
        else
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = rectTransform.localScale = new Vector3(1, 1, rectTransform.localScale.z);
        }
        rectTransform.anchoredPosition = rectTransform.sizeDelta = Vector2.zero;
    }
}
