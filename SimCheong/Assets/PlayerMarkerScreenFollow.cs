using UnityEngine;

public class PlayerMarkerScreenFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Camera topViewCamera;
    [SerializeField] private RectTransform markerUI;
    [SerializeField] private RectTransform canvasRect; // Canvas 자체의 RectTransform

    void LateUpdate()
    {
        Vector3 screenPos = topViewCamera.WorldToScreenPoint(player.position);

        // 카메라 뒤에 있으면(behind) 표시 안 함
        if (screenPos.z < 0)
        {
            markerUI.gameObject.SetActive(false);
            return;
        }
        markerUI.gameObject.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, topViewCamera, out Vector2 localPoint);

        markerUI.anchoredPosition = localPoint;
    }
}