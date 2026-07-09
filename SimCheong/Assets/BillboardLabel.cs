using UnityEngine;

/// <summary>
/// 텍스트 라벨이 항상 카메라를 정면으로 바라보게 한다 (빌보드 효과).
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main == null) return;

        transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                          Camera.main.transform.rotation * Vector3.up);
    }
}