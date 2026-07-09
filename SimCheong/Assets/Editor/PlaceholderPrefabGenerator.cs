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
            new Entry("Bollard",      PrimitiveType.Cylinder, new Vector3(0.15f, 0.5f, 0.15f), new Color(0.1f, 0.1f, 0.1f)),
            new Entry("Pillar",       PrimitiveType.Cylinder, new Vector3(0.3f, 1.2f, 0.3f),   Color.gray),
            new Entry("Car",          PrimitiveType.Cube,     new Vector3(1.8f, 0.7f, 4.0f),   Color.blue),
            new Entry("Bike",         PrimitiveType.Cube,     new Vector3(0.5f, 0.5f, 1.6f),   new Color(1f, 0.5f, 0f)),
            new Entry("Kickboard",    PrimitiveType.Cube,     new Vector3(0.3f, 0.4f, 0.9f),   Color.green),
            new Entry("Curb",         PrimitiveType.Cube,     new Vector3(1.0f, 0.15f, 0.3f),  Color.gray),
            new Entry("Hole",         PrimitiveType.Cylinder, new Vector3(0.5f, 0.02f, 0.5f),  Color.red),
            new Entry("Stairs",       PrimitiveType.Cube,     new Vector3(1.0f, 0.3f, 1.0f),   new Color(0.5f, 0.3f, 0.1f)),

            // 새로 추가된 stock 매핑 클래스용
            new Entry("TrafficLight", PrimitiveType.Cylinder, new Vector3(0.15f, 1.5f, 0.15f), Color.black),
            new Entry("Sign",         PrimitiveType.Cylinder, new Vector3(0.08f, 1.2f, 0.08f), Color.gray),
            new Entry("Animal",       PrimitiveType.Capsule,  new Vector3(0.2f, 0.3f, 0.4f),   new Color(0.6f, 0.4f, 0.2f)),
            new Entry("Obstacle",     PrimitiveType.Cube,     new Vector3(0.6f, 0.5f, 0.4f),   Color.gray),
        };

        foreach (Entry entry in entries)
        {
            CreatePrefab(entry);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlaceholderPrefabGenerator] {entries.Length}개 prefab 생성 완료 (Transparent 지원): {OutputFolder}");
    }

    private static void CreatePrefab(Entry entry)
    {
        GameObject go = GameObject.CreatePrimitive(entry.primitive);
        go.name = entry.label;
        go.transform.localScale = entry.scale;

        Renderer renderer = go.GetComponent<Renderer>();
        Material material = new Material(FindBestShader());
        SetupTransparentMode(material);
        material.color = entry.color;
        renderer.sharedMaterial = material;

        string materialPath = $"{OutputFolder}/{entry.label}_Material.mat";
        AssetDatabase.CreateAsset(material, materialPath);

        string prefabPath = $"{OutputFolder}/{entry.label}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

        Object.DestroyImmediate(go);
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

    /// <summary>
    /// 셰이더 종류에 따라 알파값이 실제로 반투명하게 보이도록 렌더 모드를 설정한다.
    /// 이걸 안 하면 Opaque 모드 그대로라, alpha를 바꿔도 시각적으로 불투명하게 남는다.
    /// </summary>
    private static void SetupTransparentMode(Material material)
    {
        string shaderName = material.shader.name;

        if (shaderName.Contains("Universal Render Pipeline"))
        {
            // URP Lit: Surface Type을 Transparent로
            material.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            material.SetFloat("_Blend", 0f);   // Alpha blend
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (shaderName.Contains("HDRP"))
        {
            material.SetFloat("_SurfaceType", 1f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (shaderName.Contains("Standard"))
        {
            // Built-in Standard 셰이더: Rendering Mode를 Transparent로
            material.SetFloat("_Mode", 3f); // 3 = Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        // Unlit/Color는 기본적으로 alpha를 지원하므로 별도 처리 불필요
    }
}