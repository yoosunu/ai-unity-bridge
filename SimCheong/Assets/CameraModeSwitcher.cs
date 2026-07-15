using UnityEngine;
using Esri.ArcGISMapsSDK.Components;

public class CameraModeSwitcher : MonoBehaviour
{
    [SerializeField] private Camera povCamera;
    [SerializeField] private Camera topViewCamera;

    private ArcGISCameraComponent povArcGISCamera;
    private ArcGISCameraComponent topViewArcGISCamera;

    private bool isTopView = false;

    void Awake()
    {
        povArcGISCamera = povCamera.GetComponent<ArcGISCameraComponent>();
        topViewArcGISCamera = topViewCamera.GetComponent<ArcGISCameraComponent>();
    }

    void Start()
    {
        SetView(isTopView);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SyncPosition();
            isTopView = !isTopView;
            SetView(isTopView);
        }
    }

    private void SyncPosition()
    {
        Transform from = isTopView ? topViewCamera.transform : povCamera.transform;
        Transform to = isTopView ? povCamera.transform : topViewCamera.transform;

        Vector3 pos = to.position;
        pos.x = from.position.x;
        pos.z = from.position.z;
        to.position = pos;
    }

    private void SetView(bool topView)
    {
        povCamera.enabled = !topView;
        topViewCamera.enabled = topView;

        // 핵심 추가: ArcGISCameraComponent도 같이 켜고 끄기
        if (povArcGISCamera != null) povArcGISCamera.enabled = !topView;
        if (topViewArcGISCamera != null) topViewArcGISCamera.enabled = topView;

        var povListener = povCamera.GetComponent<AudioListener>();
        var topListener = topViewCamera.GetComponent<AudioListener>();
        if (povListener != null) povListener.enabled = !topView;
        if (topListener != null) topListener.enabled = topView;
    }
}