// Unityの基本的な機能を使用するために必要
using UnityEngine;
// シーン遷移（例：ゲームオーバー時）のために必要
using UnityEngine.SceneManagement;

/// <summary>
/// プレイヤーキャラクターの操作全般を管理するスクリプト。
/// 移動、ジャンプ、スライディング、レーンチェンジ、分岐点での方向転換などを処理します。
/// StageGenerator1, Chaser1, BranchDirectionCheckerと連携します。
/// </summary>
public class PlayerController : MonoBehaviour
{
    // --- 外部スクリプトへの参照 ---
    private Chaser chaser; // プレイヤーを追いかけるChaser1スクリプトへの参照
    private StageGenerator stageGenerator; // ステージを生成するStageGenerator1スクリプトへの参照

    // --- public変数（Unityエディタのインスペクタから設定可能） ---

    [Header("移動設定")]
    [Tooltip("ゲーム開始時のプレイヤーの走行速度")]
    public float initialSpeed = 8.0f;
    [Tooltip("1秒ごとにどれだけ速度が上がるか")]
    public float accelerationPerSecond = 0.1f;
    [Tooltip("左右のレーン間の距離")]
    public float laneDistance = 2.5f;
    [Tooltip("レーンを移動する際の速さ（基本値）")]
    public float initialLaneChangeSpeed = 15f;

    [Header("アクション設定")]
    [Tooltip("ジャンプの高さ（基本値）")]
    public float initialJumpForce = 20.0f;
    [Tooltip("キャラクターにかかる重力の強さ（基本値）")]
    public float initialGravity = 40.0f;
    [Tooltip("スライディング状態が継続する時間（秒）")]
    public float slideDuration = 0.8f;
    [Tooltip("走行速度が上がった際、ジャンプ力がどれだけ強化されるかの倍率")]
    public float jumpSpeedScaling = 1.0f;
    [Tooltip("走行速度が上がった際、重力がどれだけ強くなるかの倍率（速く落下するようになる）")]
    public float gravitySpeedScaling = 1.2f;


    // --- private変数（スクリプト内部での状態管理用） ---
    private float elapsedTime = 0f; // ゲーム開始からの経過時間
    private float currentSpeed; // 現在の走行速度
    private Vector3 verticalVelocity = Vector3.zero; // Y軸方向の速度（ジャンプや重力）
    private CharacterController controller; // 物理挙動を制御するCharacterControllerコンポーネント
    private int lane = 1; // 現在のレーン (0:左, 1:中央, 2:右)

    private bool isSliding = false; // スライディング中かどうかのフラグ
    private float slideTimer = 0f; // スライディングの残り時間タイマー
    private float originalHeight; // CharacterControllerの元の高さ
    private Vector3 originalCenter; // CharacterControllerの元の中心位置

    private bool atBranch = false; // 分岐点にいるかどうかのフラグ
    private Transform branchTransform; // 現在いる分岐点のTransform
    
    private Animator animator; // アニメーションを制御するAnimatorコンポーネントへの参照

    // 壁との接触状態を判定するためのフラグ
    private bool isTouchingLeftWall = false;
    private bool isTouchingRightWall = false;

    /// <summary>
    /// スクリプトがロードされた最初のフレームで一度だけ呼び出されるUnityのライフサイクルメソッド。
    /// </summary>
    void Start()
    {
        // 必要なコンポーネントやオブジェクトを取得して変数にキャッシュする
        controller = GetComponent<CharacterController>();
        stageGenerator = FindObjectOfType<StageGenerator>();
        chaser = FindObjectOfType<Chaser>();
        animator = GetComponent<Animator>();

        // スライディングから戻るために、元のCharacterControllerの情報を保存しておく
        originalHeight = controller.height;
        originalCenter = controller.center;

        // 初期速度を設定
        currentSpeed = initialSpeed;
    }

    /// <summary>
    /// フレームごとに呼び出されるUnityのライフサイクルメソッド。
    /// </summary>
    void Update()
    {
        // フレームの開始時に、壁接触フラグをリセットする
        // (OnControllerColliderHitは衝突しているフレームでのみ呼ばれるため、毎フレームリセットが必要)
        isTouchingLeftWall = false;
        isTouchingRightWall = false;
        
        // 時間経過に応じて走行速度を徐々に上げていく
        elapsedTime += Time.deltaTime;
        currentSpeed = initialSpeed + (elapsedTime * accelerationPerSecond);
        
        // Animatorに接地状態（isGrounded）を毎フレーム渡す
        if (animator != null)
        {
            animator.SetBool("IsGrounded", controller.isGrounded);
        }

        // プレイヤーの入力処理、分岐処理、移動処理を順番に実行
        HandleInputs();
        HandleBranching();
        HandleMovement();
    }

    /// <summary>
    /// キーボード入力に基づいて、ジャンプ、スライディング、レーン移動を処理します。
    /// </summary>
    private void HandleInputs()
    {
        // キャラクターが地面にいるときのみジャンプとスライディングが可能
        if (controller.isGrounded)
        {
            // 上矢印キーが押され、かつスライディング中でない場合
            if (Input.GetKeyDown(KeyCode.UpArrow) && !isSliding)
            {
                // 走行速度に応じてジャンプ力をスケーリング（速いほど高く飛ぶ）
                float speedFactor = 1 + ((currentSpeed - initialSpeed) / initialSpeed) * jumpSpeedScaling;
                verticalVelocity.y = initialJumpForce * speedFactor;

                // Animatorに"Jump"トリガーを送り、ジャンプアニメーションを再生
                if (animator != null)
                {
                    animator.SetTrigger("Jump");
                }
            }
            // 下矢印キーが押され、かつスライディング中でない場合
            if (Input.GetKeyDown(KeyCode.DownArrow) && !isSliding)
            {
                // スライディングを開始
                StartSlide();
            }
        }
        
        // --- レーン移動の入力処理 ---
        if (Input.GetKeyDown(KeyCode.LeftArrow)) 
        {
            // すでに左の壁に接触している状態で、さらに左に行こうとした場合
            if (isTouchingLeftWall)
            {
                 // Chaserにミスを通知する
                 chaser?.OnPlayerMistake("L_Wall");
            }
            lane--; // 左のレーンへ
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            // すでに右の壁に接触している状態で、さらに右に行こうとした場合
            if (isTouchingRightWall)
            {
                 // Chaserにミスを通知する
                 chaser?.OnPlayerMistake("R_Wall");
            }
            lane++; // 右のレーンへ
        }
        // レーン番号が0から2の範囲に収まるようにClamp（制限）する
        lane = Mathf.Clamp(lane, 0, 2);
    }

    /// <summary>
    /// 分岐点にいる場合の方向転換処理。
    /// </summary>
    private void HandleBranching()
    {
        // 分岐点にいない、または分岐点の情報がない場合は処理を中断
        if (!atBranch || branchTransform == null) return;
        
        // 分岐プレハブの子オブジェクトである"TriggerArea"を探す
        Transform triggerObject = branchTransform.Find("TriggerArea");
        if (triggerObject == null) return;
        // "TriggerArea"にアタッチされているBranchDirectionCheckerスクリプトを取得
        BranchDirectionChecker checker = triggerObject.GetComponent<BranchDirectionChecker>();
        if (checker == null) return;

        Vector3 newDirection = transform.forward; // デフォルトは直進
        bool directionChosen = false; // 方向が選択されたかのフラグ

        // 'D'キーが押され、かつ右に進める場合
        if (Input.GetKeyDown(KeyCode.D) && checker.canGoRight)
        {
            // 進行方向を右（90度回転）に設定
            newDirection = new Vector3(transform.forward.z, 0, -transform.forward.x);
            directionChosen = true;
        }
        // 'A'キーが押され、かつ左に進める場合
        else if (Input.GetKeyDown(KeyCode.A) && checker.canGoLeft)
        {
            // 進行方向を左（-90度回転）に設定
            newDirection = new Vector3(-transform.forward.z, 0, transform.forward.x);
            directionChosen = true;
        }
        
        // AかDが押されたが、その方向には進めなかった場合（例：右折不可の場所でDを押した）
        // この場合でも「方向選択のアクションは行われた」とみなし、直進が確定する
        if (!directionChosen && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)))
        {
            directionChosen = true;
        }

        // いずれかの方向が選択された場合
        if (directionChosen)
        {
            transform.forward = newDirection; // プレイヤーの向きを更新
            lane = 1; // 方向転換後は中央レーンにリセット
            
            // StageGeneratorに、プレイヤーが選択した方向を通知する
            stageGenerator?.CommitDirection(newDirection, branchTransform);
            
            // 分岐処理を完了し、状態をリセット
            atBranch = false;
            branchTransform = null;
            if (stageGenerator != null) stageGenerator.isPlayerAtUnresolvedBranch = false;
        }
    }

    /// <summary>
    /// キャラクターの前進、横移動、重力を計算し、最終的な移動を実行します。
    /// </summary>
    private void HandleMovement()
    {
        // 現在の速度が初期速度の何倍かを表す係数
        float speedFactor = currentSpeed / initialSpeed;

        // --- 1. 重力計算 ---
        // 接地していて、かつ落下中でない場合（y速度が負でない場合）は、地面に吸着させるために小さな下向きの力を加える
        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }
        
        // Y軸の速度に重力を加算する。速度が上がるほど重力も強くする（より速く落下する）
        verticalVelocity.y -= (initialGravity * Mathf.Pow(speedFactor, gravitySpeedScaling)) * Time.deltaTime;

        // --- 2. 前進速度ベクトル ---
        // キャラクターの向いている方向に現在の速度を掛け合わせ、前進する力を計算
        Vector3 forwardVelocity = transform.forward * currentSpeed;
        
        // --- 3. 横移動（レーン移動）速度ベクトル ---
        // 現在のプレイヤーの位置からY軸成分を除いた、地面上の位置を基準点とする
        Vector3 pathCenter = Vector3.ProjectOnPlane(transform.position, transform.up);
        // 基準点から、プレイヤーの現在の右方向(transform.right)に、目標レーンまでの距離を掛け合わせた位置を目標地点とする。
        // これにより、キャラクターがどの方向を向いていても正しく横移動できる。
        // (lane - 1)は、レーン番号(0,1,2)を(-1,0,1)に変換し、中央レーンを0とするための計算。
        Vector3 targetLanePosition = pathCenter + transform.right * ((lane - 1) * laneDistance);

        // 走行速度に応じてレーンチェンジの速度も上げる
        float currentLaneChangeSpeed = initialLaneChangeSpeed * speedFactor;
        // 目標レーン位置と現在位置の差から、移動すべき方向と強さを計算する
        Vector3 horizontalVelocity = (targetLanePosition - transform.position) * currentLaneChangeSpeed;
        // この計算にはY軸の差分も含まれうるので、上下移動に影響しないようY成分を0にリセットする
        horizontalVelocity.y = 0;

        // --- 4. 最終的な移動ベクトルを合成 ---
        Vector3 finalVelocity = forwardVelocity + horizontalVelocity; // 前進と横移動を合成
        finalVelocity.y = verticalVelocity.y; // Y軸の速度（重力/ジャンプ）を適用

        // --- 5. CharacterControllerを動かす ---
        // 計算した最終的な移動ベクトルを使って、キャラクターを動かす
        controller.Move(finalVelocity * Time.deltaTime);

        // --- 6. スライディングタイマーの更新 ---
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                // タイマーが0になったらスライディングを終了
                EndSlide();
            }
        }
    }

    /// <summary>
    /// CharacterControllerが他のColliderと衝突したときに呼び出されるUnityのイベント。
    /// </summary>
    /// <param name="hit">衝突情報</param>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 衝突した相手のタグで判定
        if (hit.collider.CompareTag("R_Wall"))
        {
            isTouchingRightWall = true; // 右の壁に触れているフラグを立てる
        }
        else if (hit.collider.CompareTag("L_Wall"))
        {
            isTouchingLeftWall = true; // 左の壁に触れているフラグを立てる
        }
        else if (hit.collider.CompareTag("Obstacle"))
        {
            // 障害物に衝突した場合は即座にゲームオーバー
            Die("障害物に衝突しました。");
        }
    }
    
    /// <summary>
    /// スライディングを開始します。
    /// </summary>
    private void StartSlide()
    {
        if (isSliding) return; // すでにスライディング中なら何もしない
        isSliding = true;
        slideTimer = slideDuration;
        // CharacterControllerの高さを半分にし、中心を下にずらすことで当たり判定を小さくする
        controller.height = originalHeight / 2f;
        controller.center = new Vector3(originalCenter.x, originalCenter.y - originalHeight / 4f, originalCenter.z);

        // Animatorに"IsSliding"フラグをtrueにし、スライディングアニメーションを開始
        if (animator != null)
        {
            animator.SetBool("IsSliding", true);
        }
    }

    /// <summary>
    /// スライディングを終了します。
    /// </summary>
    private void EndSlide()
    {
        isSliding = false;
        // CharacterControllerの高さと中心を元の値に戻す
        controller.height = originalHeight;
        controller.center = originalCenter;

        // Animatorに"IsSliding"フラグをfalseにし、スライディングアニメーションを終了
        if (animator != null)
        {
            animator.SetBool("IsSliding", false);
        }
    }
    
    /// <summary>
    /// Trigger属性を持つColliderに侵入したときに呼び出されるUnityのイベント。
    /// </summary>
    /// <param name="other">侵入した相手のCollider</param>
    void OnTriggerEnter(Collider other)
    {
        // 分岐エリアのTriggerに侵入した場合
        if (other.CompareTag("Branch"))
        {
            atBranch = true; // 分岐点にいるフラグを立てる
            branchTransform = other.transform.parent; // 分岐プレハブ本体のTransformを保持
            if (stageGenerator != null) stageGenerator.isPlayerAtUnresolvedBranch = true; // StageGeneratorに通知
        }
        // 落下判定エリアに侵入した場合
        else if (other.CompareTag("Fell"))
        {
            Die("落下しました。");
        }
    }

    /// <summary>
    /// Trigger属性を持つColliderから出たときに呼び出されるUnityのイベント。
    /// </summary>
    /// <param name="other">出た相手のCollider</param>
    void OnTriggerExit(Collider other)
    {
        // 分岐エリアから出た場合（方向転換せずに通り過ぎた場合など）
        if (other.CompareTag("Branch") && other.transform.parent == branchTransform)
        {
            // 分岐状態をリセット
            atBranch = false;
            branchTransform = null;
            if (stageGenerator != null) stageGenerator.isPlayerAtUnresolvedBranch = false;
        }
    }

    /// <summary>
    /// プレイヤーが死亡した（ゲームオーバーになった）際の処理。
    /// </summary>
    /// <param name="reason">死亡理由（デバッグログ用）</param>
    private void Die(string reason)
    {
        Debug.Log($"ゲームオーバー: {reason}");
        // このスクリプトを無効化し、Updateなどが二重に実行されるのを防ぐ
        this.enabled = false;
        // タイトルシーンをロードする
        SceneManager.LoadScene("TitleScene");
    }
}