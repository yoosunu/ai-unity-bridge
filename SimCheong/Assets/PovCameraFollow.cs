using UnityEngine;

public class PovCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float eyeHeight = 1.7f;

    void LateUpdate()
    {
        Vector3 pos = player.position;
        pos.y += eyeHeight;
        transform.position = pos;
        transform.rotation = player.rotation;
    }
}