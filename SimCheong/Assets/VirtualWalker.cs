using UnityEngine;

/// <summary>
/// 실제 사용자(플레이어/클라이언트)의 이동을 담당한다.
/// 에디터: WASD 이동 + 방향키 회전
/// 모바일: 화면 왼쪽 터치=이동, 오른쪽 드래그=회전
/// </summary>
public class VirtualWalker : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float sprintMultiplier = 2.5f;

    [Header("회전 (방향키/터치드래그)")]
    [SerializeField] private float turnSpeed = 60f;
    [SerializeField] private float touchLookSensitivity = 0.2f;

    [Header("초기 방향 보정")]
    [SerializeField] private float initialYawOffset = 27f;

    private float yaw;

    void Start()
    {
        yaw = transform.eulerAngles.y + initialYawOffset;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
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

        // 화면 오른쪽 절반을 좌우로 드래그하면 회전
        foreach (Touch touch in Input.touches)
        {
            if (touch.position.x > Screen.width / 2f && touch.phase == TouchPhase.Moved)
            {
                yaw += touch.deltaPosition.x * touchLookSensitivity;
            }
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void HandleMove()
    {
        Vector3 inputDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) inputDir += transform.forward;
        if (Input.GetKey(KeyCode.S)) inputDir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) inputDir += transform.right;
        if (Input.GetKey(KeyCode.A)) inputDir -= transform.right;

        // 화면 왼쪽 절반을 누르고 있으면 전진
        foreach (Touch touch in Input.touches)
        {
            if (touch.position.x <= Screen.width / 2f)
            {
                inputDir += transform.forward;
            }
        }

        if (inputDir.sqrMagnitude < 0.01f) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        transform.position += inputDir.normalized * speed * Time.deltaTime;
    }
}