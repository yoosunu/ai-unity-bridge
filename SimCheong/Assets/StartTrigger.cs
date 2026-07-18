using UnityEngine;

public class StartTrigger : MonoBehaviour
{
    [SerializeField] private SocketReceiver socketReceiver;
    private bool triggered = false;

    void Update()
    {
        if (!triggered && Input.GetKeyDown(KeyCode.Space))
        {
            socketReceiver.SendStartSignal();
            triggered = true;
            Debug.Log("[StartTrigger] 시작 신호 전송함 - Player를 원하는 위치에 두고 Space를 눌렀어야 함");
        }
    }
}