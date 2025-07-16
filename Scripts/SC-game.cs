using UnityEngine;
using UnityEngine.SceneManagement; // SceneManagementを扱うために必要

public class SceneController : MonoBehaviour
{
    /// <summary>
    /// 指定された名前のシーンに遷移します。
    /// このメソッドをUnityエディタのボタンのOnClick()イベントから呼び出します。
    /// </summary>
    /// <param name="GameScene">遷移したいシーンの名前</param>
    public void LoadScene(string GameScene)
    {
        // sceneNameが空でないことを確認
        if (!string.IsNullOrEmpty(GameScene))
        {
            SceneManager.LoadScene(GameScene);
        }
        else
        {
            Debug.LogError("シーン名が指定されていません！");
        }
    }
}