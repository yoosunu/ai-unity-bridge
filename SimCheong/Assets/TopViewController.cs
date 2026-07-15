using UnityEngine;

public class TopViewController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 100f;
    [SerializeField] private float fastMoveMultiplier = 3f;

    void Update()
    {
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move -= Vector3.forward;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;
        if (Input.GetKey(KeyCode.A)) move -= Vector3.right;

        transform.position += move.normalized * speed * Time.deltaTime;
    }
}