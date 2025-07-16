using UnityEngine;

/// <summary>
/// 指定された中心点の周りを公転し、波のように上下運動するオブジェクトを制御するクラス
/// </summary>
public class RotateAndOrbit : MonoBehaviour
{
    // --- インスペクターで設定する項目 ---

    /// <summary>
    /// 公転の中心となるオブジェクトのTransform
    /// </summary>
    [Tooltip("公転の中心となるオブジェクトを設定してください")]
    public Transform orbitCenter;

    /// <summary>
    /// 公転の半径
    /// </summary>
    [Tooltip("中心点からの公転半径を設定してください")]
    public float orbitRadius = 10.0f;

    /// <summary>
    /// 公転の速さ（度/秒）
    /// </summary>
    [Tooltip("1秒間に回転する角度を設定してください")]
    public float orbitSpeed = 30.0f;

    /// <summary>
    /// 上下運動の揺れの大きさ（振幅）
    /// </summary>
    [Tooltip("上下にどれくらい揺れるかを設定してください")]
    public float waveAmplitude = 2.0f;

    /// <summary>
    /// 上下運動の速さ（周波数）
    /// </summary>
    [Tooltip("上下に揺れる速さを設定してください")]
    public float waveFrequency = 2.0f;


    // --- 内部で使用する変数 ---

    /// <summary>
    /// 上下運動の基準となるY座標
    /// </summary>
    private float baseY;

    /// <summary>
    /// 上下運動の波形を計算するための時間
    /// </summary>
    private float waveTime;

    /// <summary>
    /// ゲーム開始時に一度だけ呼び出される初期化処理
    /// </summary>
    void Start()
    {
        // orbitCenterが設定されていない場合はエラーを表示して処理を中断
        if (orbitCenter == null)
        {
            Debug.LogError("公転の中心(orbitCenter)が設定されていません！", this.gameObject);
            // このコンポーネントを無効化する
            this.enabled = false;
            return;
        }

        // 自身の初期Y座標を基準の高さとして保存
        baseY = transform.position.y;

        // 自身の初期位置を、公転中心から半径(orbitRadius)分離れた位置に設定
        // これにより、公転が常に指定した半径で行われるようになります
        Vector3 initialPosition = orbitCenter.position + new Vector3(orbitRadius, 0, 0);
        initialPosition.y = baseY; // Y座標は基準の高さを維持
        transform.position = initialPosition;
    }


    /// <summary>
    /// 毎フレーム呼び出される更新処理
    /// </summary>
    void Update()
    {
        // orbitCenterが設定されていない場合は、Update処理を実行しない
        if (orbitCenter == null) return;

        // 公転処理を実行
        OrbitAround();

        // 波のような上下運動を適用
        ApplyWaveMotion();
    }

    /// <summary>
    /// 中心の周りを公転させる処理
    /// </summary>
    void OrbitAround()
    {
        // 指定された中心(orbitCenter)を軸として、Y軸周りにオブジェクトを回転させます。
        // これにより、オブジェクトは中心の周りを円を描くように移動します。
        // 第1引数: 回転の中心点
        // 第2引数: 回転の軸 (Vector3.upはY軸を指します)
        // 第3引数: 1フレームあたりの回転角度 (速度 * 時間)
        transform.RotateAround(orbitCenter.position, Vector3.up, orbitSpeed * Time.deltaTime);

        // オブジェクトが進行方向を向くように回転させる処理
        // (中心点から自身へのベクトル) と (上方向ベクトル) の外積を計算し、接線方向を求めます
        Vector3 directionToCenter = (orbitCenter.position - transform.position).normalized;
        Vector3 tangentDirection = Vector3.Cross(directionToCenter, Vector3.up).normalized;

        // 接線方向が計算できた場合（ゼロベクトルでない場合）、その方向を向くように回転を設定
        if (tangentDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(tangentDirection);
        }
    }

    /// <summary>
    /// 波のような上下運動をY座標に適用する処理
    /// </summary>
    void ApplyWaveMotion()
    {
        // 経過時間と周波数（速さ）を元に、波の計算用の時間を更新
        waveTime += Time.deltaTime * waveFrequency;

        // Mathf.Sin()を使って、-1.0fから1.0fの範囲で滑らかに変化する値を生成
        // これに振幅(waveAmplitude)を掛けることで、揺れの大きさを調整します
        float waveOffset = Mathf.Sin(waveTime) * waveAmplitude;

        // 現在の位置情報を取得
        Vector3 newPosition = transform.position;
        // Y座標を「基準の高さ + 波によるオフセット」に更新
        newPosition.y = baseY + waveOffset;
        // 計算後の新しい位置をオブジェクトに適用
        transform.position = newPosition;
    }
}