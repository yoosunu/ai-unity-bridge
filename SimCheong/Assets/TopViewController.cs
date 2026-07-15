using UnityEngine;

public class TopViewController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float heightSpeed = 30f;
    [SerializeField] private float fastMoveMultiplier = 3f;

    void Update()
    {
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);
        float hSpeed = heightSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move -= Vector3.forward;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.A)) move -= Vector3.right;

        float safeDeltaTime = Mathf.Min(Time.deltaTime, 0.033f);

        transform.position += move.normalized * speed * safeDeltaTime;

        float heightInput = 0f;
        if (Input.GetKey(KeyCode.E)) heightInput += 1f;
        if (Input.GetKey(KeyCode.Q)) heightInput -= 1f;

        transform.position += Vector3.up * heightInput * hSpeed * safeDeltaTime;
    }
}