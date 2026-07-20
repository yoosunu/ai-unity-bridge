using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using NativeWebSocket;

public class SocketReceiver : MonoBehaviour
{
    [SerializeField] private string serverUrl = "ws://127.0.0.1:5002";

    private WebSocket websocket;
    private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    async void Start()
    {
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => Debug.Log("[SocketReceiver] Connected");
        websocket.OnError += (e) => Debug.LogError($"[SocketReceiver] Error: {e}");
        websocket.OnClose += (e) => Debug.Log("[SocketReceiver] Closed");
        websocket.OnMessage += (bytes) =>
        {
            messageQueue.Enqueue(Encoding.UTF8.GetString(bytes));
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    public bool TryDequeueMessage(out string message)
    {
        return messageQueue.TryDequeue(out message);
    }

    public async void SendStartSignal()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("START");
            Debug.Log("[SocketReceiver] START 신호 전송");
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}