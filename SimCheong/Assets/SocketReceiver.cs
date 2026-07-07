using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP 소켓 연결과 원시 문자열 수신만 담당
/// </summary>
public class SocketReceiver : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 5002;

    private TcpClient client;
    private StreamReader reader;
    private Thread receiveThread;
    private volatile bool isRunning;
    private volatile bool isCleanedUp; // 중복 정리 방지 가드

    private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(ConnectAndListen);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ConnectAndListen()
    {
        try
        {
            client = new TcpClient(host, port);
            reader = new StreamReader(client.GetStream());
            Debug.Log($"[SocketReceiver] Connected to {host}:{port}");

            while (isRunning)
            {
                string line = reader.ReadLine();

                if (!string.IsNullOrEmpty(line))
                {
                    messageQueue.Enqueue(line);
                }
                else if (line == null)
                {
                    break; // 서버가 연결을 닫음
                }
            }
        }
        catch (Exception e)
        {
            // Close()에 의해 강제로 깨어난 경우도 여기로 들어오며, 정상 종료 경로.
            Debug.Log($"[SocketReceiver] Receive loop ended: {e.Message}");
        }
    }

    public bool TryDequeueMessage(out string message)
    {
        return messageQueue.TryDequeue(out message);
    }

    /// <summary>
    /// 실제 자원 정리. OnDestroy / OnApplicationQuit 양쪽에서 호출되어도
    /// 한 번만 실행되도록 가드
    /// </summary>
    private void Cleanup()
    {
        if (isCleanedUp)
        {
            return;
        }
        isCleanedUp = true;

        isRunning = false;

        // Close()가 블로킹 중인 ReadLine()을 깨워 스레드가 스스로 빠져나가게 한다.
        reader?.Close();
        client?.Close();

        receiveThread?.Join(200);

        Debug.Log("[SocketReceiver] Cleaned up.");
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void OnApplicationQuit()
    {
        Cleanup();
    }
}