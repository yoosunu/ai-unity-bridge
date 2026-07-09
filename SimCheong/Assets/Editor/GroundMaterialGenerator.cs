using UnityEditor;
using UnityEngine;
using System.IO;

public class GroundMaterialGenerator
{
    [MenuItem("Tools/WalkGuide/Generate Ground Material")]
    public static void Generate()
    {
        int size = 256;
        Texture2D texture = new Texture2D(size, size);

        Color baseColor = new Color(0.55f, 0.55f, 0.55f); // 아스팔트 회색
        Color lineColor = new Color(0.45f, 0.45f, 0.45f); // 살짝 어두운 줄눈

        int tileSize = 32; // 보도블록 한 칸 크기(픽셀 기준)

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isLine = (x % tileSize == 0) || (y % tileSize == 0);
                // 약간의 랜덤 노이즈를 섞어서 완전히 균일하지 않게
                float noise = Random.Range(-0.03f, 0.03f);
                Color c = isLine ? lineColor : baseColor;
                c += new Color(noise, noise, noise);
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;

        string textureFolder = "Assets/Textures";
        if (!Directory.Exists(textureFolder)) Directory.CreateDirectory(textureFolder);

        byte[] pngData = texture.EncodeToPNG();
        string texturePath = $"{textureFolder}/Ground_Texture.png";
        File.WriteAllBytes(texturePath, pngData);
        AssetDatabase.ImportAsset(texturePath);

        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        Material material = new Material(FindGroundShader());
        material.mainTexture = importedTexture;
        material.mainTextureScale = new Vector2(10, 10); // 반복 횟수 - Ground 크기에 맞게 조정

        string materialFolder = "Assets/Materials";
        if (!Directory.Exists(materialFolder)) Directory.CreateDirectory(materialFolder);
        AssetDatabase.CreateAsset(material, $"{materialFolder}/Ground_Material.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GroundMaterialGenerator] Ground_Material 생성 완료 - Ground 오브젝트에 드래그해서 적용하세요.");
    }

    private static Shader FindGroundShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Standard");
        if (shader != null) return shader;
        return Shader.Find("Unlit/Texture");
    }
}