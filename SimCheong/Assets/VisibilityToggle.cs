using UnityEngine;

public class VisibilityToggle : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private KeyCode toggleKey = KeyCode.H;

    void Update()
    {
        Debug.Log("VisibilityToggle Update 실행 중");  // 임시로 추가 - 매 프레임 찍힘

        if (Input.GetKeyDown(toggleKey))
        {
            Debug.Log($"[VisibilityToggle] 토글 전: {target.activeSelf}");
            target.SetActive(!target.activeSelf);
            Debug.Log($"[VisibilityToggle] 토글 후: {target.activeSelf}");
        }
    }
}