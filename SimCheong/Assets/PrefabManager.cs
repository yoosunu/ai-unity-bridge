using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// label 문자열 -> prefab 매핑만 담당
/// Instantiate는 하지 않고, prefab 애셋 자체만 반환
/// </summary>
public class PrefabManager : MonoBehaviour
{
    [Serializable]
    public struct LabelPrefabEntry
    {
        public string label;
        public GameObject prefab;
        public float groundOffset;
    }

    [SerializeField] private List<LabelPrefabEntry> entries = new List<LabelPrefabEntry>();

    private Dictionary<string, GameObject> lookup;

    void Awake()
    {
        lookup = new Dictionary<string, GameObject>();
        foreach (var entry in entries)
        {
            lookup[entry.label.ToLowerInvariant()] = entry.prefab;
        }
    }
    public float GetGroundOffset(string label)
    {
        foreach (var entry in entries)
        {
            if (entry.label.ToLowerInvariant() == label.ToLowerInvariant())
            {
                return entry.groundOffset;
            }
        }
        return 0f;
    }

    public GameObject GetPrefab(string label)
    {
        lookup.TryGetValue(label.ToLowerInvariant(), out GameObject prefab);
        return prefab;
    }
}