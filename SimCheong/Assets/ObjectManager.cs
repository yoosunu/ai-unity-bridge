using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DetectionManager))]
public class ObjectManager : MonoBehaviour
{
    [SerializeField] private PrefabManager prefabManager;
    [SerializeField] private VirtualWalker virtualWalker;

    [Tooltip("이 시간(초) 동안 같은 id가 안 보이면, 그 자리에 위치를 고정한다.")]
    [SerializeField] private float freezeTimeout = 0.3f;

    private DetectionManager detectionManager;

    private class ActiveTrack
    {
        public GameObject instance;
        public float lastSeenTime;
        public Vector3 originalScale; // prefab 고유의 원본 비율 (볼라드=가늘고 김, 차=넓적함 등)
    }

    // 현재 실시간으로 계속 추적/갱신 중인 오브젝트
    private readonly Dictionary<int, ActiveTrack> activeTracks = new Dictionary<int, ActiveTrack>();
    // 이미 위치가 고정되어 더 이상 갱신하지 않는 id들
    private readonly HashSet<int> frozenIds = new HashSet<int>();

    void Awake()
    {
        detectionManager = GetComponent<DetectionManager>();
    }

    void Update()
    {
        DetectionData[] detections = detectionManager.LatestDetections;

        if (detections != null)
        {
            foreach (DetectionData detection in detections)
            {
                if (frozenIds.Contains(detection.id))
                {
                    continue; // 이미 고정된 물체는 다시 나타나도 무시
                }

                if (activeTracks.TryGetValue(detection.id, out ActiveTrack track))
                {
                    UpdateTrack(track, detection);
                }
                else
                {
                    CreateTrack(detection);
                }
            }
        }

        FreezeStaleTracks();
    }

    private void CreateTrack(DetectionData detection)
    {
        GameObject prefab = prefabManager.GetPrefab(detection.label);
        if (prefab == null)
        {
            Debug.LogWarning($"[ObjectManager] No prefab for label: {detection.label}");
            return;
        }

        Vector3 worldPos = ComputeWorldPos(detection);
        GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);
        instance.name = $"{detection.label}_{detection.id}";

        // Instantiate 직후 아직 prefab 원본 그대로인 상태 -> 이때의 비율을 기억해둠
        Vector3 originalScale = instance.transform.localScale;

        ApplyScaleAndGrounding(instance, detection, originalScale);

        activeTracks[detection.id] = new ActiveTrack
        {
            instance = instance,
            lastSeenTime = Time.time,
            originalScale = originalScale
        };
    }

    private void UpdateTrack(ActiveTrack track, DetectionData detection)
    {
        Vector3 worldPos = ComputeWorldPos(detection);
        track.instance.transform.position = worldPos;
        ApplyScaleAndGrounding(track.instance, detection, track.originalScale);
        track.lastSeenTime = Time.time;
    }

    private Vector3 ComputeWorldPos(DetectionData detection)
    {
        return virtualWalker.transform.position
             + virtualWalker.transform.right * detection.x
             + virtualWalker.transform.forward * detection.z;
    }

    private void ApplyScaleAndGrounding(GameObject instance, DetectionData detection, Vector3 originalScale)
    {
        float scaleFactor = Mathf.Clamp(detection.box_height / 400f, 0.5f, 1.5f);

        // 매번 "원본 비율" 기준으로 다시 계산 -> 누적 곱셈 없음, 비율도 유지됨
        instance.transform.localScale = originalScale * scaleFactor;

        Renderer renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            float halfHeight = renderer.bounds.extents.y;
            Vector3 pos = instance.transform.position;
            pos.y = halfHeight; // 바닥(y=0) 기준으로 다시 계산
            instance.transform.position = pos;
        }
    }

    private void FreezeStaleTracks()
    {
        List<int> toFreeze = null;

        foreach (var pair in activeTracks)
        {
            if (Time.time - pair.Value.lastSeenTime > freezeTimeout)
            {
                toFreeze ??= new List<int>();
                toFreeze.Add(pair.Key);
            }
        }

        if (toFreeze == null) return;

        foreach (int id in toFreeze)
        {
            activeTracks.Remove(id);
            frozenIds.Add(id);
            // instance는 씬에 그대로 남음 - 갱신 대상에서만 제외됨 (freeze)
        }
    }
}