using UnityEngine;
using Esri.ArcGISMapsSDK.Components;

public class CameraModeSwitcher : MonoBehaviour
{
    [SerializeField] private Camera povCamera;
    [SerializeField] private Camera topViewCamera;
    [SerializeField] private MonoBehaviour povCameraFollow;   // PovCameraFollow 컴포넌트
    [SerializeField] private MonoBehaviour topViewController; // TopViewController 컴포넌트

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
            isTopView = !isTopView;
            SetView(isTopView);
        }
    }

    [SerializeField] private VirtualWalker virtualWalker; // Player의 이동 스크립트

    private void SetView(bool topView)
    {
        povCamera.enabled = !topView;
        topViewCamera.enabled = topView;

        if (povArcGISCamera != null) povArcGISCamera.enabled = !topView;
        if (topViewArcGISCamera != null) topViewArcGISCamera.enabled = topView;

        if (topViewController != null) topViewController.enabled = topView;

        // 핵심 추가: TopView 볼 때는 Player 이동 꺼버리기
        if (virtualWalker != null) virtualWalker.enabled = !topView;

        var povListener = povCamera.GetComponent<AudioListener>();
        var topListener = topViewCamera.GetComponent<AudioListener>();
        if (povListener != null) povListener.enabled = !topView;
        if (topListener != null) topListener.enabled = topView;
    }
}