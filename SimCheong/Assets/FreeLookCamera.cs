using UnityEngine;

/// <summary>
/// 관측자 전용 카메라. Walker(가상 보행자)와 완전히 독립적으로 움직인다.
/// WASD+마우스로 자유 비행, Tab으로 탑뷰 전환.
/// </summary>
public class FreeLookCamera : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float fastMoveMultiplier = 3f; // Shift 누르면 빨라짐

    [Header("시점 회전 (우클릭 드래그)")]
    [SerializeField] private float lookSensitivity = 2f;

    [Header("탑뷰")]
    [SerializeField] private float topDownHeight = 30f;

    private float yaw;
    private float pitch;
    private bool isTopDown = false;

    private Vector3 freeCamPosition;
    private Quaternion freeCamRotation;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        freeCamPosition = transform.position;
        freeCamRotation = transform.rotation;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleTopDown();
        }

        if (isTopDown)
        {
            HandleTopDownMove();
        }
        else
        {
            HandleFreeLook();
        }
    }

    private void ToggleTopDown()
    {
        isTopDown = !isTopDown;

        if (isTopDown)
        {
            // 자유 시점 상태 저장해두고 탑뷰로 전환
            freeCamPosition = transform.position;
            freeCamRotation = transform.rotation;

            Vector3 pos = transform.position;
            pos.y = topDownHeight;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 정면으로 아래를 내려다봄
        }
        else
        {
            // 자유 시점으로 복귀
            transform.position = freeCamPosition;
            transform.rotation = freeCamRotation;
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }
    }

    private void HandleFreeLook()
    {
        // 우클릭 드래그 중일 때만 시점 회전
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }

    private void HandleTopDownMove()
    {
        // 탑뷰에서는 좌우/앞뒤로만 수평 이동 (마우스 스크롤로 높이 조절)
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move -= Vector3.forward;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.A)) move -= Vector3.right;
        transform.position += move.normalized * speed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.Clamp(pos.y - scroll * 10f, 5f, 100f);
            transform.position = pos;
        }
    }
}