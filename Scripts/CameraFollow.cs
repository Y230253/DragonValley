// Unityの基本的な機能を使用するために必要
using UnityEngine;

/// <summary>
/// プレイヤーを追従するカメラの振る舞いを制御するスクリプト。
/// プレイヤーの後方から、一定の距離と角度を保ちながらスムーズに追従します。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    // --- public変数（Unityエディタのインスペクタから設定可能） ---

    [Tooltip("追従するターゲットとなるオブジェクト（通常はプレイヤー）のTransform")]
    public Transform target;

    [Tooltip("カメラとターゲットとの相対的な位置関係。ターゲットの向きに応じてオフセットが適用されます。")]
    public Vector3 offset = new Vector3(0, 5, -10); // 例: Y軸方向に5、Z軸後方に10の位置

    [Tooltip("カメラのY座標をこの値に固定します。これにより、プレイヤーがジャンプしてもカメラの高さが変わりません。")]
    public float fixedYPosition = 6.0f;

    [Tooltip("カメラの角度（X軸の回転、Y軸の回転、Z軸の回転）を設定します。")]
    public Vector3 cameraAngles = new Vector3(20, 0, 0); // 例: 20度下を向く

    [Tooltip("カメラがターゲットに追従する際の滑らかさ。値が大きいほど速く追従します。")]
    public float smoothSpeed = 10f;


    /// <summary>
    /// 全てのUpdate処理が終わった後にフレームごとに呼び出されるUnityのライフサイクルメソッド。
    /// プレイヤーの移動が完了した後にカメラ位置を更新することで、カクつきを防ぎます。
    /// </summary>
    void LateUpdate()
    {
        // 追従対象のターゲットが設定されていない場合は、何もせずに処理を終了する
        if (target == null) return;

        // --- 1. カメラの目標位置を計算 ---

        // プレイヤー(target)の位置に、プレイヤーの向きを考慮したオフセットを加算して、カメラの基本目標位置を算出する
        // (target.rotation * offset) により、プレイヤーがどの方向を向いていても常に背後からの視点になる
        Vector3 desiredPosition = target.position + (target.rotation * offset);

        // カメラのY座標（高さ）を、設定された固定値で上書きする
        desiredPosition.y = fixedYPosition;


        // --- 2. カメラの位置をスムーズに更新 ---

        // 現在のカメラ位置から、算出した目標位置(desiredPosition)へ滑らかに移動させる
        // Vector3.Lerpは線形補間で、第三引数（この場合はsmoothSpeed * Time.deltaTime）に応じてスムーズな移動を実現する
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);


        // --- 3. カメラの角度を更新 ---

        // LookAtを使用せず、プレイヤーの向きに追従しつつ、指定された角度を適用する
        // target.rotationでプレイヤーの向きに合わせ、Quaternion.Euler(cameraAngles)で設定した角度を追加で回転させる
        transform.rotation = target.rotation * Quaternion.Euler(cameraAngles);
    }
}