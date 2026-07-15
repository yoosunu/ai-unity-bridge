using System.Collections.Generic;
using UnityEngine;

public enum TrackState
{
    Candidate,  // 아직 확정 안 됨 (Python 쪽에서 이미 히트카운트로 걸러주지만, Unity 쪽에서도 최소 관측 유지)
    Confirmed,  // 확정됨 - 동적 객체는 계속 갱신, 정적 객체는 위치 고정
    Frozen,     // 동적 객체가 관측을 놓친 상태 - 마지막 위치에서 정지, 시각적으로 다르게 표시 가능
}

[RequireComponent(typeof(DetectionManager))]
public class ObjectManager : MonoBehaviour
{
    [SerializeField] private PrefabManager prefabManager;
    [SerializeField] private VirtualWalker virtualWalker;

    [Tooltip("동적 객체가 이 시간(초) 동안 안 보이면 Frozen 상태로 전환한다.")]
    [SerializeField] private float freezeTimeout = 0.3f;

    [Tooltip("Frozen 상태의 오브젝트에 적용할 반투명도 (1=원래 그대로)")]
    [SerializeField] private float frozenAlpha = 0.5f;

    [SerializeField] private float movementThreshold = 0.15f;
    [SerializeField] private float autoFreezeAfterStableTime = 2.0f;

    private DetectionManager detectionManager;
    private int groundLayerMask;

    private class Track
    {
        public GameObject instance;
        public TrackState state;
        public bool isStatic;
        public Vector3 originalScale;
        public float lastSeenTime;
        public TextMesh label;
        public Vector3 lastPosition;
        public float positionStableSince;
    }

    private readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();

    void Awake()
    {
        detectionManager = GetComponent<DetectionManager>();
        // "HazardObjects" 레이어를 제외한 모든 레이어에 대해서만 레이캐스트
        int hazardLayer = LayerMask.NameToLayer("HazardObjects");
        groundLayerMask = ~(1 << hazardLayer); // 비트 반전으로 "이 레이어 빼고 전부"
    }

    void Update()
    {
        DetectionData[] detections = detectionManager.LatestDetections;
        Debug.Log($"[ObjectManager] LatestDetections: {(detections == null ? "null" : detections.Length.ToString())}");

        if (detections != null)
        {
            foreach (DetectionData detection in detections)
            {
                ProcessDetection(detection);
            }
        }

        UpdateFreezeStates();
    }

    private void ProcessDetection(DetectionData detection)
    {
        if (tracks.TryGetValue(detection.id, out Track track))
        {
            if (track.isStatic)
            {
                return; // 정적 객체는 최초 배치 이후 절대 갱신 안 함
            }

            track.lastSeenTime = Time.time;
            UpdateTransform(track, detection);
        }
        else
        {
            CreateTrack(detection);
        }
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
        Debug.Log($"[ObjectManager] Creating {detection.label} at worldPos={worldPos}");
        GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);
        instance.layer = LayerMask.NameToLayer("HazardObjects");
        instance.name = $"{detection.label}_{detection.id}";

        Vector3 originalScale = instance.transform.localScale;

        Track track = new Track
        {
            instance = instance,
            state = TrackState.Confirmed,
            isStatic = detection.is_static,
            originalScale = originalScale,
            lastSeenTime = Time.time,
            lastPosition = worldPos,      
            positionStableSince = 0f      
        };

        ApplyScaleAndGrounding(track, detection, detection.label);
        track.label = CreateLabel(instance, $"{detection.label} #{detection.id}");

        tracks[detection.id] = track;
    }

    private void UpdateTransform(Track track, DetectionData detection)
    {
        if (track.state == TrackState.Frozen)
        {
            return;
        }

        Vector3 worldPos = ComputeWorldPos(detection);
        float movedDistance = Vector3.Distance(worldPos, track.lastPosition);

        if (movedDistance < movementThreshold)
        {
            if (track.positionStableSince <= 0f)
            {
                track.positionStableSince = Time.time;
            }
            else if (Time.time - track.positionStableSince > autoFreezeAfterStableTime)
            {
                track.state = TrackState.Frozen;
                return;
            }
        }
        else
        {
            track.positionStableSince = 0f;
        }

        track.instance.transform.position = worldPos;
        track.lastPosition = worldPos;
        ApplyScaleAndGrounding(track, detection, detection.label);
    }

    private Vector3 ComputeWorldPos(DetectionData detection)
    {
        return virtualWalker.transform.position
             + virtualWalker.transform.right * detection.x
             + virtualWalker.transform.forward * detection.z;
    }

    private float FindGroundHeight(float worldX, float worldZ, float searchStartHeight)
    {
        Vector3 rayStart = new Vector3(worldX, searchStartHeight, worldZ);
        bool hitSomething = Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, searchStartHeight * 2f, groundLayerMask);

        Debug.Log($"[FindGroundHeight] rayStart={rayStart}, hit={hitSomething}, hitPoint={(hitSomething ? hit.point.ToString() : "N/A")}, hitObject={(hitSomething ? hit.collider.gameObject.name : "N/A")}");

        if (hitSomething)
        {
            return hit.point.y;
        }
        return 0f;
    }

    private void ApplyScaleAndGrounding(Track track, DetectionData detection, string label)
    {
        track.instance.transform.localScale = track.originalScale;

        Renderer renderer = track.instance.GetComponentInChildren<Renderer>();
        float halfHeight = renderer != null ? renderer.bounds.extents.y : 0f;

        Vector3 pos = track.instance.transform.position;
        float searchStart = virtualWalker.transform.position.y + 200f; // 충분히 높은 지점에서 아래로 쏨
        float groundY = FindGroundHeight(pos.x, pos.z, searchStart);

        pos.y = groundY + halfHeight;
        track.instance.transform.position = pos;
    }
    
    private TextMesh CreateLabel(GameObject parent, string text)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform);

        Renderer parentRenderer = parent.GetComponentInChildren<Renderer>();
        float heightOffset = parentRenderer != null ? parentRenderer.bounds.extents.y + 0.3f : 1f;
        labelObj.transform.localPosition = new Vector3(0, heightOffset, 0);

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.08f;
        textMesh.anchor = TextAnchor.LowerCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        labelObj.AddComponent<BillboardLabel>();

        return textMesh;
    }

    private void UpdateFreezeStates()
    {
        foreach (var pair in tracks)
        {
            Track track = pair.Value;

            if (track.isStatic || track.state == TrackState.Frozen)
            {
                continue; // 정적 객체는 애초에 대상 아님, 이미 Frozen인 것도 스킵
            }

            if (Time.time - track.lastSeenTime > freezeTimeout)
            {
                track.state = TrackState.Frozen;
                SetAlpha(track.instance, frozenAlpha);
            }
        }
    }

    private void SetAlpha(GameObject instance, float alpha)
    {
        Renderer renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        Color color = renderer.material.color;
        color.a = alpha;
        renderer.material.color = color;

        // 반투명 렌더링을 위해 머티리얼을 Transparent 모드로 전환해야 실제로 alpha가 보임
        // (Standard/URP-Lit 셰이더 기준, 필요시 셰이더별로 다르게 처리)
    }
}