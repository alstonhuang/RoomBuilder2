using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public UnityEvent onInteract;

    // 修改：變數型態改成 Outline (原本是 GameObject)
    public Outline outlineScript;

    void Start()
    {
        // 確保遊戲開始時是關閉的
        if (outlineScript != null)
        {
            outlineScript.enabled = false;
        }
    }

    public void OnInteract()
    {
        onInteract.Invoke();
    }

    public void OnFocus()
    {
        // 打開腳本
        if (outlineScript != null)
        {
            outlineScript.enabled = true;
        }
    }

    public void OnLoseFocus()
    {
        // 關閉腳本
        if (outlineScript != null)
        {
            outlineScript.enabled = false;
        }
    }
}