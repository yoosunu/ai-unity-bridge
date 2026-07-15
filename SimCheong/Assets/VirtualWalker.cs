using UnityEngine;

/// <summary>
/// 실제 사용자(플레이어/클라이언트)의 이동을 담당한다.
/// ObjectManager는 이 오브젝트의 Transform을 기준으로 탐지 객체의 절대 좌표를 계산한다.
/// </summary>
public class VirtualWalker : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 1.5f;       // 기본 속도 (m/s)
    [SerializeField] private float sprintMultiplier = 2.5f; // Shift 눌렀을 때 배속

    [Header("회전 (방향키)")]
    [SerializeField] private float turnSpeed = 60f;

    private float yaw;

    void Start()
    {
        yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        float turnInput = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) turnInput -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) turnInput += 1f;

        yaw += turnInput * turnSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleMove()
    {
        Vector3 inputDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) inputDir += transform.forward;
        if (Input.GetKey(KeyCode.S)) inputDir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) inputDir += transform.right;
        if (Input.GetKey(KeyCode.A)) inputDir -= transform.right;

        if (inputDir.sqrMagnitude < 0.01f)
        {
            return; // 입력 없으면 즉시 정지, 관성 없음
        }

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        transform.position += inputDir.normalized * speed * Time.deltaTime;
    }
}