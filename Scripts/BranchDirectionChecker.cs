// Unityの基本的な機能を使用するために必要
using UnityEngine;
// List<T>のようなコレクションクラスを使用するために必要
using System.Collections.Generic;

/// <summary>
/// 分岐路にアタッチし、どの方向に進めるかを制御するスクリプト。
/// PlayerController1やStageGenerator1から参照され、分岐の挙動を決定します。
/// </summary>
public class BranchDirectionChecker : MonoBehaviour
{
    // --- public変数（Unityエディタのインスペクタから設定可能） ---

    [Tooltip("左方向に進めるかどうかのフラグ")]
    public bool canGoLeft = false;

    [Tooltip("右方向に進めるかどうかのフラグ")]
    public bool canGoRight = false;

    [Tooltip("前方向に進めるかどうかのフラグ")]
    public bool canGoForward = true;

    /// <summary>
    /// スクリプトがロードされた最初のフレームで一度だけ呼び出されるUnityのライフサイクルメソッド。
    /// </summary>
    void Start()
    {
        // このゲームオブジェクトにColliderコンポーネントが付いていない場合、
        // プレイヤーとの接触を検知するためのTrigger用Colliderを自動的に追加する。
        if (GetComponent<Collider>() == null)
        {
            // BoxColliderを追加
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            // isTriggerをtrueに設定し、物理的な衝突ではなく接触イベントのみを発生させる
            col.isTrigger = true;
            // プレイヤーが確実に通過・検知できるように、Colliderのサイズを設定
            col.size = new Vector3(2f, 2f, 2f);
        }
    }

    /// <summary>
    /// 現在の分岐で進行可能な方向をVector3のリストとして取得します。
    /// （このプロジェクトでは現在直接使用されていませんが、将来的な拡張のために残されています）
    /// </summary>
    /// <returns>進行可能な方向を示すVector3のリスト</returns>
    public List<Vector3> GetEnabledDirections()
    {
        // 進行可能な方向を格納するための新しいリストを作成
        List<Vector3> dirs = new List<Vector3>();

        // 各フラグをチェックし、trueであれば対応する方向ベクトルをリストに追加
        if (canGoForward) dirs.Add(Vector3.forward); // 前方
        if (canGoLeft) dirs.Add(Vector3.left);       // 左方
        if (canGoRight) dirs.Add(Vector3.right);     // 右方

        // 作成したリストを返す
        return dirs;
    }
}