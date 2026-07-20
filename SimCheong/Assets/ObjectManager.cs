using System.Collections.Generic;
using UnityEngine;

public enum TrackState
{
    Candidate,
    Confirmed,
    Frozen,
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

    [Header("영상 동기화 자동 전진")]
    [SerializeField] private float assumedWalkSpeedMps = 1.2f;
    [SerializeField] private float videoFps = 30f;
    [SerializeField] private int frameSkip = 10;  // Python의 FRAME_SKIP과 반드시 일치시켜야 함

    [Header("중복 스폰 방지 (Merge)")]
    [SerializeField] private float mergeDistanceThreshold = 2.0f;

    [Header("공간음향")]
[SerializeField] private Transform audioListenerTarget; // 비워두면 virtualWalker 사용

    private float DistancePerFrame => (assumedWalkSpeedMps / videoFps) * frameSkip;
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
    private DetectionData[] lastProcessedDetections;

    void Awake()
    {
        detectionManager = GetComponent<DetectionManager>();
        int hazardLayer = LayerMask.NameToLayer("HazardObjects");
        Debug.Log($"[ObjectManager] hazardLayer index: {hazardLayer}");  // 이 줄 추가
        groundLayerMask = ~(1 << hazardLayer);
    }

    void Update()
    {
        DetectionData[] detections = detectionManager.LatestDetections;
        Debug.Log($"[ObjectManager] LatestDetections: {(detections == null ? "null" : detections.Length.ToString())}");

        if (detections != null && detections != lastProcessedDetections)
        {
            virtualWalker.AdvanceOneFrame(DistancePerFrame);
            lastProcessedDetections = detections;
        }

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
                return;
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
        Vector3 worldPos = ComputeWorldPos(detection);

        // 정적 객체는 위치 기반으로 기존 물체와 병합 시도
        if (detection.is_static && TryFindNearbyStaticObject(detection.label, worldPos, out int existingId))
        {
            // 새로 만들지 않고, 기존 track에 이 새 id를 매핑만 해줌 (다음부터 이 id로 들어와도 무시되게)
            tracks[detection.id] = tracks[existingId];
            return;
        }

        GameObject prefab = prefabManager.GetPrefab(detection.label);
        if (prefab == null)
        {
            Debug.LogWarning($"[ObjectManager] No prefab for label: {detection.label}");
            return;
        }

        GameObject instance = Instantiate(prefab, worldPos, virtualWalker.transform.rotation);
        HighlightAsAIDetected(instance);
        SetLayerRecursively(instance, LayerMask.NameToLayer("HazardObjects"));
        instance.name = $"{detection.label}_{detection.id}";

        // 공간음향 경고 부착
        ProximityAlert alert = instance.AddComponent<ProximityAlert>();
        Transform earPosition = audioListenerTarget != null ? audioListenerTarget : virtualWalker.transform;
        alert.Initialize(earPosition);

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
        track.label = CreateLabel(instance, BuildDebugLabelText(detection));

        tracks[detection.id] = track;
    }

    private bool TryFindNearbyStaticObject(string label, Vector3 worldPos, out int existingId)
    {
        foreach (var pair in tracks)
        {
            if (!pair.Value.isStatic) continue;
            if (pair.Value.instance == null) continue;
            if (!pair.Value.instance.name.StartsWith(label)) continue;

            float dist = Vector3.Distance(pair.Value.instance.transform.position, worldPos);
            if (dist < mergeDistanceThreshold)
            {
                existingId = pair.Key;
                return true;
            }
        }
        existingId = -1;
        return false;
    }

    private void HighlightAsAIDetected(GameObject instance)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            Material mat = renderer.material;
            Color highlightColor = new Color(1f, 0.1f, 0.1f);
            mat.color = highlightColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", highlightColor * 2f);
        }
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
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, searchStartHeight * 2f, groundLayerMask))
        {
            Debug.Log($"[FindGroundHeight] hit={hit.collider.gameObject.name}, layer={hit.collider.gameObject.layer}");  // 여기, if 블록 안
            float candidateY = hit.point.y;
            float playerY = virtualWalker.transform.position.y;

            // Player 지면 기준 ±3m 넘게 차이나면, 건물 옥상 등에 잘못 맞은 것으로 간주
            if (Mathf.Abs(candidateY - playerY) > 3f)
            {
                return playerY; // 신뢰 못 하니 Player 지면 높이로 폴백
            }

            return candidateY;
        }
        return virtualWalker.transform.position.y;
    }

    private void ApplyScaleAndGrounding(Track track, DetectionData detection, string label)
    {
        track.instance.transform.localScale = track.originalScale;

        Renderer renderer = track.instance.GetComponentInChildren<Renderer>();
        float halfHeight = renderer != null ? renderer.bounds.extents.y : 0f;

        Vector3 pos = track.instance.transform.position;
        float searchStart = virtualWalker.transform.position.y + 200f;
        float groundY = FindGroundHeight(pos.x, pos.z, searchStart);

        pos.y = groundY + halfHeight;
        track.instance.transform.position = pos;
    }

    private string BuildDebugLabelText(DetectionData detection)
    {
        return $"{detection.label} #{detection.id}\n" +
            $"z={detection.z:F1}m conf={detection.confidence:F2}";
    }

    private Bounds GetCombinedBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return combined;
    }

    private TextMesh CreateLabel(GameObject parent, string text)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform);

        Bounds combinedBounds = GetCombinedBounds(parent);
        float heightOffset = Mathf.Min(combinedBounds.extents.y + 0.3f, 2.0f);
        labelObj.transform.localPosition = new Vector3(0, heightOffset, 0);

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 38;
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
                continue;
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

        Material material = renderer.material;

        if (alpha < 1f)
        {
            SetMaterialTransparent(material);
        }
        else
        {
            SetMaterialOpaque(material);
        }

        Color color = material.color;
        color.a = alpha;
        material.color = color;
    }

    private void SetMaterialTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void SetMaterialOpaque(Material material)
    {
        material.SetFloat("_Surface", 0f);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}