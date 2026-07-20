using UnityEngine;

public class StartTrigger : MonoBehaviour
{
    [SerializeField] private SocketReceiver socketReceiver;
    private bool triggered = false;

    void Update()
    {
        if (triggered) return;

        // 키보드(에디터 테스트용) 또는 화면 터치(폰용) 둘 다 지원
        bool keyPressed = Input.GetKeyDown(KeyCode.Space);
        bool touched = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;

        if (keyPressed || touched)
        {
            socketReceiver.SendStartSignal();
            triggered = true;
            Debug.Log("[StartTrigger] 시작 신호 전송함");
        }
    }
}