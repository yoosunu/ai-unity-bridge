using UnityEngine;

/// <summary>
/// 실제 카메라 추적(ARKit) 없이, "사용자가 일정 속도로 똑바로 걷고 있다"고
/// 가정한 가상의 기준점. 이 Transform을 기준으로 detection의 상대 좌표를
/// 절대(월드) 좌표로 변환함
/// </summary>
public class VirtualWalker : MonoBehaviour
{
    [Tooltip("초당 전진 속도 (Unity 단위). 실제 보행 속도와 x,z 스케일에 맞춰 조정 필요")]
    [SerializeField] private float walkSpeed = 1.2f;

    void Update()
    {
        transform.position += transform.forward * walkSpeed * Time.deltaTime;
    }
}