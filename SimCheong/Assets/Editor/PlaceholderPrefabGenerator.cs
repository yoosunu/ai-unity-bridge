using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 9개 클래스에 대한 임시 primitive prefab을 자동 생성한다.
/// 실제 3D 에셋이 준비되면 이 prefab들의 내용만 교체하면 되고,
/// PrefabManager의 참조는 그대로 유지된다.
/// </summary>
public class PlaceholderPrefabGenerator
{
    private const string OutputFolder = "Assets/Prefabs/Placeholders";

    private struct Entry
    {
        public string label;
        public PrimitiveType primitive;
        public Vector3 scale;
        public Color color;

        public Entry(string label, PrimitiveType primitive, Vector3 scale, Color color)
        {
            this.label = label;
            this.primitive = primitive;
            this.scale = scale;
            this.color = color;
        }
    }

    [MenuItem("Tools/WalkGuide/Generate Placeholder Prefabs")]
    public static void Generate()
    {
        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
        }

        Entry[] entries = new Entry[]
        {
            new Entry("Bollard",   PrimitiveType.Cylinder, new Vector3(0.15f, 0.5f, 0.15f), new Color(0.1f, 0.1f, 0.1f)),
            new Entry("Pillar",    PrimitiveType.Cylinder, new Vector3(0.3f, 1.2f, 0.3f),   Color.gray),
            new Entry("Person",    PrimitiveType.Capsule,  new Vector3(0.3f, 0.9f, 0.3f),   Color.yellow),
            new Entry("Car",       PrimitiveType.Cube,     new Vector3(1.8f, 0.7f, 4.0f),   Color.blue),
            new Entry("Bike",      PrimitiveType.Cube,     new Vector3(0.5f, 0.5f, 1.6f),   new Color(1f, 0.5f, 0f)),
            new Entry("Kickboard", PrimitiveType.Cube,     new Vector3(0.3f, 0.4f, 0.9f),   Color.green),
            new Entry("Curb",      PrimitiveType.Cube,     new Vector3(1.0f, 0.15f, 0.3f),  Color.gray),
            new Entry("Hole",      PrimitiveType.Cylinder, new Vector3(0.5f, 0.02f, 0.5f),  Color.red),
            new Entry("Stairs",    PrimitiveType.Cube,     new Vector3(1.0f, 0.3f, 1.0f),   new Color(0.5f, 0.3f, 0.1f)),
        };

        foreach (Entry entry in entries)
        {
            CreatePrefab(entry);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlaceholderPrefabGenerator] {entries.Length}개 prefab 생성 완료: {OutputFolder}");
    }

    private static Shader FindBestShader()
{
    // URP
    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
    if (shader != null) return shader;

    // HDRP
    shader = Shader.Find("HDRP/Lit");
    if (shader != null) return shader;

    // Built-in Render Pipeline
    shader = Shader.Find("Standard");
    if (shader != null) return shader;

    Debug.LogWarning("[PlaceholderPrefabGenerator] 적절한 셰이더를 못 찾음, Unlit/Color로 대체");
    return Shader.Find("Unlit/Color");
}

    private static void CreatePrefab(Entry entry)
    {
        GameObject go = GameObject.CreatePrimitive(entry.primitive);
        go.name = entry.label;
        go.transform.localScale = entry.scale;

        Renderer renderer = go.GetComponent<Renderer>();
        Material material = new Material(FindBestShader());
        material.color = entry.color;
        renderer.sharedMaterial = material;

        string materialPath = $"{OutputFolder}/{entry.label}_Material.mat";
        AssetDatabase.CreateAsset(material, materialPath);

        string prefabPath = $"{OutputFolder}/{entry.label}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

        Object.DestroyImmediate(go); // 씬에는 안 남기고 prefab 파일만 남김
    }
}