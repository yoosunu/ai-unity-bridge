using UnityEngine;

public class VirtualWalker : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float sprintMultiplier = 2.5f;

    [Header("회전 (방향키)")]
    [SerializeField] private float turnSpeed = 60f;

    [Header("초기 방향 보정")]
    [SerializeField] private float initialYawOffset = 23f;

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

    public void AdvanceOneFrame(float distance)
    {
        transform.position += transform.forward * distance;
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

        if (inputDir.sqrMagnitude < 0.01f) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        transform.position += inputDir.normalized * speed * Time.deltaTime;
    }
}