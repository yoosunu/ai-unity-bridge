using UnityEngine;

/// <summary>
/// SocketReceiver로부터 원시 JSON 문자열을 가져와 파싱하고,
/// "현재 프레임 기준 가장 최신 detection 결과"를 보관한다.
/// GameObject, Prefab 등 씬 관련 개념은 전혀 알지 못한다.
/// </summary>
[RequireComponent(typeof(SocketReceiver))]
public class DetectionManager : MonoBehaviour
{
    private SocketReceiver socketReceiver;

    // 가장 최근에 파싱된 detection 배열. 없으면 null.
    public DetectionData[] LatestDetections { get; private set; }

    void Awake()
    {
        socketReceiver = GetComponent<SocketReceiver>();
    }

    void Update()
    {
        string latestMessage = null;

        // 큐를 끝까지 비우면서, 마지막(가장 최신) 메시지만 남긴다.
        while (socketReceiver.TryDequeueMessage(out string message))
        {
            latestMessage = message;
        }

        if (latestMessage == null)
        {
            return;
        }

        DetectionPacket packet;
        try
        {
            packet = JsonUtility.FromJson<DetectionPacket>(latestMessage);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DetectionManager] JSON parse failed: {e.Message}");
            return;
        }

        if (packet?.objects == null)
        {
            return;
        }

        LatestDetections = packet.objects;
    }
}

[System.Serializable]
public class DetectionData
{
    public int id;
    public string label;
    public float x;
    public float z;
    public float confidence;   // 이 필드가 있는지 확인, 없으면 추가
    public float box_width;
    public float box_height;
    public bool is_static;
}

[System.Serializable]
public class DetectionPacket
{
    public DetectionData[] objects;
}