using UnityEditor;
using UnityEngine;
using System.IO;

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
            new Entry("Bike",         PrimitiveType.Cube,     new Vector3(0.5f, 0.5f, 1.6f),   new Color(1f, 0.5f, 0f)),
            new Entry("Bollard",      PrimitiveType.Cylinder, new Vector3(0.15f, 0.5f, 0.15f), new Color(0.1f, 0.1f, 0.1f)),
            new Entry("Car",          PrimitiveType.Cube,     new Vector3(1.8f, 0.7f, 4.0f),   Color.blue),
            new Entry("Curb",         PrimitiveType.Cube,     new Vector3(1.0f, 0.15f, 0.3f),  Color.gray),
            new Entry("Hole",         PrimitiveType.Cylinder, new Vector3(0.5f, 0.02f, 0.5f),  Color.red),
            new Entry("Kickboard",    PrimitiveType.Cube,     new Vector3(0.3f, 0.4f, 0.9f),   Color.green),
            new Entry("Pillar",       PrimitiveType.Cylinder, new Vector3(0.3f, 1.2f, 0.3f),   Color.gray),
            new Entry("Stairs",       PrimitiveType.Cube,     new Vector3(1.0f, 0.3f, 1.0f),   new Color(0.5f, 0.3f, 0.1f)),
            new Entry("Animal",       PrimitiveType.Capsule,  new Vector3(0.2f, 0.3f, 0.4f),   new Color(0.6f, 0.4f, 0.2f)),
            new Entry("Obstacle",     PrimitiveType.Cube,     new Vector3(0.6f, 0.5f, 0.4f),   Color.gray),
            new Entry("Sign",         PrimitiveType.Cylinder, new Vector3(0.08f, 1.2f, 0.08f), Color.gray),
            new Entry("TrafficLight", PrimitiveType.Cylinder, new Vector3(0.15f, 1.5f, 0.15f), Color.black),
            // Person은 아래에서 관절 조합형으로 별도 생성
        };

        foreach (Entry entry in entries)
        {
            CreatePrefab(entry);
        }

        CreateHumanoidPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlaceholderPrefabGenerator] {entries.Length + 1}개 prefab 생성 완료: {OutputFolder}");
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

        Object.DestroyImmediate(go);
    }

    private static void CreateHumanoidPrefab()
    {
        GameObject root = new GameObject("Person");

        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(root.transform);
        torso.transform.localPosition = new Vector3(0, 0.55f, 0);
        torso.transform.localScale = new Vector3(0.35f, 0.4f, 0.2f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform);
        head.transform.localPosition = new Vector3(0, 1.05f, 0);
        head.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);

        CreateLimb(root.transform, "ArmLeft", new Vector3(-0.28f, 0.6f, 0), new Vector3(0.1f, 0.4f, 0.1f));
        CreateLimb(root.transform, "ArmRight", new Vector3(0.28f, 0.6f, 0), new Vector3(0.1f, 0.4f, 0.1f));
        CreateLimb(root.transform, "LegLeft", new Vector3(-0.12f, 0.05f, 0), new Vector3(0.12f, 0.45f, 0.12f));
        CreateLimb(root.transform, "LegRight", new Vector3(0.12f, 0.05f, 0), new Vector3(0.12f, 0.45f, 0.12f));

        Material material = new Material(FindBestShader());
        material.color = Color.yellow;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = material;
        }

        string materialPath = $"{OutputFolder}/Person_Material.mat";
        AssetDatabase.CreateAsset(material, materialPath);

        string prefabPath = $"{OutputFolder}/Person.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        Object.DestroyImmediate(root);
    }

    private static void CreateLimb(Transform parent, string name, Vector3 localPos, Vector3 scale)
    {
        GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        limb.name = name;
        limb.transform.SetParent(parent);
        limb.transform.localPosition = localPos;
        limb.transform.localScale = scale;
    }

    private static Shader FindBestShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) return shader;

        shader = Shader.Find("HDRP/Lit");
        if (shader != null) return shader;

        shader = Shader.Find("Standard");
        if (shader != null) return shader;

        Debug.LogWarning("[PlaceholderPrefabGenerator] 적절한 셰이더를 못 찾음, Unlit/Color로 대체");
        return Shader.Find("Unlit/Color");
    }
}