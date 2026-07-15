using UnityEngine;

/// <summary>
/// 실제 사용자(클라이언트)의 이동/회전을 담당한다.
/// ObjectManager가 참조하는 기준점(VirtualWalker)이 곧 이 오브젝트다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
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
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;

        transform.position += move.normalized * moveSpeed * Time.deltaTime;
    }
}