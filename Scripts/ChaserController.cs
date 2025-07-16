// Unityの基本的な機能を使用するために必要
using UnityEngine;
// シーン遷移（例：タイトル画面に戻る）のために必要
using UnityEngine.SceneManagement;

/// <summary>
/// プレイヤーを追跡するチェイサー（追跡者）のAIを制御するスクリプト。
/// プレイヤーのミス状況に応じて、プレイヤーとの距離を詰めてプレッシャーをかけます。
/// PlayerController1からミスを通知されます。
/// </summary>
public class Chaser : MonoBehaviour
{
    // --- public変数（Unityエディタのインスペクタから設定可能） ---

    [Header("追跡対象")] // インスペクタ上で見出しを表示
    [Tooltip("追跡するプレイヤーのTransform")]
    public Transform playerTransform;
    [Tooltip("位置と向きの基準にするカメラのTransform")]
    public Transform cameraTransform;

    [Header("位置設定")]
    [Tooltip("チェイサーのY座標（高さ）をこの値に固定します。")]
    public float fixedYPosition = 0.5f;
    [Tooltip("プレイヤーが1回ミスした時に、プレイヤーにどれだけ接近するか（後方からの距離）")]
    public float approachDistance = 2.5f;
    [Tooltip("目標位置からこの距離以上離れた場合、ワープして位置を補正します。")]
    public float warpThreshold = 20f;

    [Header("挙動設定")]
    [Tooltip("目標位置への追従の速さ")]
    public float moveSpeed = 5f;
    [Tooltip("1回ミスした後、ミスがリセットされて通常状態に戻るまでの時間（秒）")]
    public float returnDelay = 8f;


    // --- private変数（スクリプト内部での状態管理用） ---

    // プレイヤーのミス回数をカウントする変数
    private int mistakeCount = 0;
    // ミス状態から通常状態に戻るまでの残り時間を計測するタイマー
    private float returnTimer = 0f;
    // 最後に接触した壁のタグ（"L_Wall" or "R_Wall"）を記録。連続ミス判定に使用
    private string lastWallHit = "";

    /// <summary>
    /// スクリプトがロードされた最初のフレームで一度だけ呼び出されるUnityのライフサイクルメソッド。
    /// </summary>
    void Start()
    {
        // ゲーム開始直後は、プレイヤーが1回ミスした状態からスタートする
        mistakeCount = 1;
        returnTimer = returnDelay; // リセットタイマーを開始
        lastWallHit = ""; // 壁接触履歴はリセット
        Debug.Log("ゲーム開始。チェイサーは1ミス状態からスタートします。");
    }

    /// <summary>
    /// フレームごとに呼び出されるUnityのライフサイクルメソッド。
    /// </summary>
    void Update()
    {
        // プレイヤーかカメラが設定されていない場合は、エラーを防ぐために処理を中断
        if (playerTransform == null || cameraTransform == null) return;

        // --- 1. チェイサーの目標位置を決定 ---
        // 現在のミス回数に応じて、追いかけるべき目標位置を取得する
        Vector3 targetPosition = GetTargetPositionXZ();

        // --- 2. Y座標を固定値で上書き ---
        // 常に一定の高さを保つように、Y座標を固定値に設定する
        targetPosition.y = fixedYPosition;

        // --- 3. チェイサーの移動処理 ---
        // 目標位置と現在の位置が離れすぎているかチェック
        if (Vector3.Distance(transform.position, targetPosition) > warpThreshold)
        {
            // 離れすぎている場合は、ワープして即座に目標位置へ移動
            transform.position = targetPosition;
        }
        else
        {
            // 許容範囲内であれば、Lerpを使って目標位置へスムーズに移動
            transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        // --- 4. チェイサーの向きを更新 ---
        // 常にカメラと同じ向きを向くように、回転を同期させる
        transform.rotation = cameraTransform.rotation;

        // --- 5. ミス状態のリセット処理 ---
        // ミス回数が1回の場合のみ、リセットタイマーを動作させる
        if (mistakeCount == 1)
        {
            returnTimer -= Time.deltaTime; // タイマーを減算
            if (returnTimer <= 0)
            {
                // タイマーが0になったら、ミス状態をリセットする
                ResetMistakes();
            }
        }
        
        // --- 6. ゲームオーバー判定 ---
        // ミス回数が2回以上の場合
        if (mistakeCount >= 2)
        {
             // チェイサーとプレイヤーの距離をチェック
             if (Vector3.Distance(transform.position, playerTransform.position) < 1.0f)
             {
                 // 距離が一定値より近くなったら（＝捕まったら）、ゲームオーバーとしてタイトルシーンに遷移
                 SceneManager.LoadScene("TitleScene");
             }
        }
    }
    
    /// <summary>
    /// 現在のミス回数に応じて、チェイサーが目指すべき目標位置を返します。
    /// </summary>
    /// <returns>目標位置のVector3</returns>
    private Vector3 GetTargetPositionXZ()
    {
        switch (mistakeCount)
        {
            case 0: // 通常時（ミスなし）
                // カメラの真下を目標とする
                return cameraTransform.position;

            case 1: // 1ミス時
                // プレイヤーの後方 `approachDistance` の位置を目標とする
                return playerTransform.position - playerTransform.forward * approachDistance;

            case 2: // 2ミス時（またはそれ以上）
            default:
                // プレイヤー自身を目標とし、接触を試みる
                return playerTransform.position;
        }
    }

    /// <summary>
    /// PlayerController1から呼び出される、プレイヤーがミスをした際の通知メソッド。
    /// </summary>
    /// <param name="wallTag">プレイヤーが接触した壁のタグ名</param>
    public void OnPlayerMistake(string wallTag)
    {
        // 前回当たった壁と同じ壁に連続で当たった場合のみ、ミスとしてカウントする
        if (lastWallHit == wallTag)
        {
            // ミス回数がまだ2回未満の場合
            if (mistakeCount < 2)
            {
                mistakeCount++; // ミスカウントを増やす
                returnTimer = returnDelay; // リセットタイマーを再設定
                Debug.Log($"ミス！カウント: {mistakeCount}");
            }
        }
        else
        {
            // 違う壁に当たった場合は、ミスとはカウントせず、
            // 最後に当たった壁の情報を更新するだけ（次回の連続ヒット判定のため）
            lastWallHit = wallTag;
        }
    }

    /// <summary>
    /// ミス状態をリセットし、通常状態に戻します。
    /// </summary>
    private void ResetMistakes()
    {
        mistakeCount = 0;
        lastWallHit = "";
        returnTimer = 0;
        Debug.Log("ミス状態がリセットされました。");
    }
}