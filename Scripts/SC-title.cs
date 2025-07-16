using UnityEngine;
using UnityEngine.SceneManagement; // SceneManagementを扱うために必要

public class SceneController2 : MonoBehaviour
{
    /// <summary>
    /// 指定された名前のシーンに遷移します。
    /// このメソッドをUnityエディタのボタンのOnClick()イベントから呼び出します。
    /// </summary>
    /// <param name="TitleScene">遷移したいシーンの名前</param>
    public void LoadScene(string TitleScene)
    {
        // sceneNameが空でないことを確認
        if (!string.IsNullOrEmpty(TitleScene))
        {
            SceneManager.LoadScene(TitleScene);
        }
        else
        {
            Debug.LogError("シーン名が指定されていません！");
        }
    }
}