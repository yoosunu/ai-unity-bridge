using UnityEngine;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;

/// <summary>
/// 플레이어(VirtualWalker)의 위경도를 따라다니는 탑뷰 카메라.
/// 회전은 고정(북쪽이 항상 위), 고도만 고정된 절대 높이 유지.
/// </summary>
public class TopViewFollow : MonoBehaviour
{
    [SerializeField] private ArcGISLocationComponent playerLocation; // VirtualWalker의 것
    [SerializeField] private double topViewAltitude = 300.0; // 절대 고도(미터), 필요시 조정

    private ArcGISLocationComponent myLocation;

    void Awake()
    {
        myLocation = GetComponent<ArcGISLocationComponent>();
    }

    void LateUpdate()
    {
        ArcGISPoint playerPos = playerLocation.Position;
        myLocation.Position = new ArcGISPoint(
            playerPos.X, playerPos.Y, topViewAltitude, playerPos.SpatialReference
        );
        // 회전은 안 건드림 - 아래를 내려다보는 각도로 Inspector에서 미리 고정 (Rotation Pitch = -90 근처)
    }
}