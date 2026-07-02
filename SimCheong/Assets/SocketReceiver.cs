using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

[Serializable]
public class DetectionData
{
    public int id;
    public string label;
    public float x;
    public float z;
}

[Serializable]
public class DetectionPacket
{
    public DetectionData[] objects;
}

public class SocketReceiver : MonoBehaviour
{
    public GameObject carPrefab;

    private TcpClient client;
    private StreamReader reader;
    private Thread receiveThread;

    private readonly object lockObject = new object();
    private DetectionPacket latestPacket;

    private Dictionary<int, GameObject> spawnedObjects = new Dictionary<int, GameObject>();

    void Start()
    {
        receiveThread = new Thread(ConnectToServer);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("SocketReceiver started.");
    }

    private void ConnectToServer()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 5000);
            reader = new StreamReader(client.GetStream());

            Debug.Log("Connected to Python server.");

            while (true)
            {
                string message = reader.ReadLine();

                if (!string.IsNullOrEmpty(message))
                {
                    DetectionPacket packet = JsonUtility.FromJson<DetectionPacket>(message);

                    lock (lockObject)
                    {
                        latestPacket = packet;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Socket error: " + e.Message);
        }
    }

    void Update()
    {
        DetectionPacket packet = null;

        lock (lockObject)
        {
            packet = latestPacket;
        }

        if (packet == null || packet.objects == null)
        {
            return;
        }

        foreach (DetectionData detection in packet.objects)
        {
            if (!spawnedObjects.ContainsKey(detection.id))
            {
                GameObject prefab = GetPrefabByLabel(detection.label);

                if (prefab == null)
                {
                    Debug.LogWarning("No prefab for label: " + detection.label);
                    continue;
                }

                GameObject newObject = Instantiate(prefab);
                newObject.name = detection.label + "_" + detection.id;

                spawnedObjects.Add(detection.id, newObject);
            }

            GameObject target = spawnedObjects[detection.id];
            target.transform.position = new Vector3(detection.x, 0, detection.z);
        }
    }

    private GameObject GetPrefabByLabel(string label)
    {
        label = label.ToLower();

        if (label == "car")
        {
            return carPrefab;
        }

        return null;
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        reader?.Close();
        client?.Close();
    }
}