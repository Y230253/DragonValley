// Unityの基本的な機能を使用するために必要
using UnityEngine;
// List<T>のようなコレクションクラスを使用するために必要
using System.Collections.Generic;
// LINQ（例: Sum, Where, Lastなど）を使用してコレクションを便利に操作するために必要
using System.Linq;
// 正規表現（Regex）を使用して文字列パターンをマッチングするために必要
using System.Text.RegularExpressions;

/// <summary>
/// プレイヤーの進行に合わせてステージを動的に生成・管理するコアスクリプト。
/// プレイヤーの前方に道を生成し、後方になった道を削除することで、無限に続くステージを実現します。
/// PlayerController1と連携して、分岐路の選択を処理します。
/// </summary>
public class StageGenerator : MonoBehaviour
{
    // --- publicな入れ子クラスとenum定義 ---

    /// <summary>
    /// 重み付きでプレハブを管理するためのクラス。
    /// 重みが大きいほど、ランダム選択時に選ばれやすくなります。
    /// </summary>
    [System.Serializable] // インスペクタに表示させるために必要
    public class WeightedPrefab
    {
        [Tooltip("ステージ部品のプレハブ")]
        public GameObject prefab;
        [Tooltip("このプレハブが選択される確率の重み（0から1の範囲）")]
        [Range(0f, 1f)]
        public float weight = 1f;
    }

    /// <summary>
    /// 特殊な道のシーケンス（連続したパターン）の状態を管理するためのenum。
    /// </summary>
    private enum SequenceState
    {
        None,       // シーケンス中でない
        InSequence  // シーケンス中
    }


    // --- Unityエディタのインスペクタから設定する項目 ---

    [Header("基本的なプレハブリック")]
    [Tooltip("通常の直線の道として使用するプレハブのリスト")]
    public WeightedPrefab[] roadPrefabs;
    [Tooltip("分岐路として使用するプレハブのリスト")]
    public WeightedPrefab[] branchPrefabs;

    [Header("シーケンス用のプレハブリック")]
    [Tooltip("中央レーンのみに障害物があるシーケンス用の道のプレハブ")]
    public WeightedPrefab[] cOnlyLaneRoadPrefabs;
    [Tooltip("左レーンのみに障害物があるシーケンス用の道のプレハブ")]
    public WeightedPrefab[] lOnlyLaneRoadPrefabs;
    [Tooltip("右レーンのみに障害物があるシーケンス用の道のプレハブ")]
    public WeightedPrefab[] rOnlyLaneRoadPrefabs;
    [Tooltip("シーケンスが続く最大の道の数")]
    public int maxSequenceLength = 5;

    [Header("ステージ生成設定")]
    [Tooltip("プレイヤーの前方に常に維持しておく道の数")]
    public int forwardCount = 15;
    [Tooltip("プレイヤーの後方にどれだけ道を残しておくか（この数を超えると削除される）")]
    public int backwardCount = 10;
    [Tooltip("道のかわりに分岐が生成される確率")]
    [Range(0f, 1f)]
    public float branchProbability = 0.2f;
    [Tooltip("分岐を生成するために必要な、直前の最低直線道の数")]
    public int minRoadsBetweenBranches = 3;

    [Header("物理的なプロパティ")]
    [Tooltip("道のプレハブ1つあたりの物理的な長さ（Z軸方向）")]
    public float roadPhysicalLength = 30f;
    [Tooltip("分岐プレハブ1つあたりの物理的な長さ（Z軸方向）")]
    public float branchPhysicalLength = 10f;
    [Tooltip("ステージオブジェクトを識別するためのレイヤー")]
    public LayerMask stageLayer;

    // インスペクタには表示しないが、他のスクリプト（PlayerController1）からアクセスされるフラグ
    [HideInInspector]
    public bool isPlayerAtUnresolvedBranch = false;


    // --- privateな状態管理用変数 ---

    // 現在のシーケンス生成状態
    private SequenceState currentSequenceState = SequenceState.None;
    // 現在のシーケンスのタイプ（"C", "L", "R"）
    private string currentSequenceType = "";
    // 現在のシーケンスが何個続いているか
    private int currentSequenceCount = 0;
    // 現在アクティブな（表示されている）ステージセグメントのリスト
    private List<StageSegment> activeSegments = new List<StageSegment>();
    // 現在のステージの進行方向
    private Vector3 currentDirection = Vector3.forward;
    // プレイヤーのTransformへの参照
    private Transform player;
    // 最後に分岐を生成してから何個の直線道が生成されたか
    private int segmentsSinceLastBranch = 0;
    // 最後に生成されたステージセグメント
    private StageSegment lastGeneratedSegment;


    /// <summary>
    /// 生成されたステージの各部品（道や分岐）の情報を保持する内部クラス。
    /// </summary>
    [System.Serializable]
    public class StageSegment
    {
        public GameObject gameObject; // 生成されたゲームオブジェクト
        public Vector3 position;      // 生成された位置
        public Vector3 direction;     // このセグメントの向き
        public bool isBranch;         // 分岐かどうか
        public float length;          // このセグメントの長さ
        public List<GameObject> childRoads; // 分岐の場合、その先に仮生成された道

        public StageSegment(GameObject go, Vector3 pos, Vector3 dir, bool branch, float len)
        {
            gameObject = go; position = pos; direction = dir; isBranch = branch; length = len;
            childRoads = new List<GameObject>();
        }
    }


    /// <summary>
    /// スクリプト開始時の初期化処理。
    /// </summary>
    void Start()
    {
        // "Player"タグを持つオブジェクトを探して参照を取得
        player = GameObject.FindWithTag("Player")?.transform;
        if (player == null) { Debug.LogError("Player not found!"); return; }

        // ゲーム開始直後から分岐を生成できるように、カウンターを初期化
        segmentsSinceLastBranch = minRoadsBetweenBranches;
        // 初期ステージを生成
        GenerateInitialStage();
    }

    /// <summary>
    /// 毎フレームの更新処理。
    /// </summary>
    void Update()
    {
        if (player == null) return;
        // プレイヤーの位置に応じてステージを更新
        UpdateStage();
    }

    /// <summary>
    /// ゲーム開始時に初期ステージを生成します。
    /// </summary>
    void GenerateInitialStage()
    {
        // 生成の基点となるダミーのセグメントを作成
        lastGeneratedSegment = new StageSegment(null, Vector3.zero, currentDirection, false, 0);
        // プレイヤーの前後を埋めるのに十分な数のセグメントを生成
        for (int i = 0; i < forwardCount + backwardCount; i++)
        {
            GenerateNextSegment();
        }
    }

    /// <summary>
    /// プレイヤーの位置を監視し、必要に応じてステージの生成と削除を行います。
    /// </summary>
    void UpdateStage()
    {
        // プレイヤーが分岐点で方向選択を終えていない間は、ステージの更新を一時停止
        if (isPlayerAtUnresolvedBranch) return;

        // --- 1. 古いセグメントの削除 ---
        if (activeSegments.Count > 0)
        {
            StageSegment oldestSegment = activeSegments.FirstOrDefault(); // 最も古いセグメントを取得
            if (oldestSegment != null && oldestSegment.gameObject != null)
            {
                // プレイヤーが最も古いセグメントをどれだけ通り過ぎたかを計算
                float distanceBehind = Vector3.Dot(player.position - oldestSegment.position, oldestSegment.direction);
                // 規定の距離以上後方になったら、そのセグメントを削除
                if (distanceBehind > backwardCount * roadPhysicalLength)
                {
                    DestroySegment(oldestSegment);
                    activeSegments.RemoveAt(0);
                }
            }
        }

        // --- 2. 新しいセグメントの生成 ---
        if (lastGeneratedSegment != null)
        {
            // プレイヤーが最後に生成されたセグメントにどれだけ近づいたかを計算
            float distanceToLastSegment = Vector3.Dot(lastGeneratedSegment.position - player.position, currentDirection);
            // 規定の距離より近づいたら、次のセグメントを生成
            if (distanceToLastSegment < forwardCount * roadPhysicalLength)
            {
                GenerateNextSegment();
            }
        }
    }

    /// <summary>
    /// 次に生成するセグメントが「道」か「分岐」かを決定し、生成を実行します。
    /// </summary>
    void GenerateNextSegment()
    {
        // シーケンスの途中なら、強制的に道(Road)を生成
        if (currentSequenceState != SequenceState.None) { CreateRoadSegment(); return; }

        // 分岐を生成できる条件が整っているかチェック
        bool canTryBranch = segmentsSinceLastBranch >= minRoadsBetweenBranches;
        // 確率に基づいて分岐を生成するかどうかを決定
        bool shouldCreateBranch = canTryBranch && Random.value < branchProbability;

        if (shouldCreateBranch)
        {
            // 分岐を生成
            segmentsSinceLastBranch = 0; // カウンターをリセット
            CreateBranchSegment();
        }
        else
        {
            // 通常の道を生成
            segmentsSinceLastBranch++; // カウンターを増やす
            CreateRoadSegment();
        }
    }

    /// <summary>
    /// 指定された位置に既に他のステージオブジェクトがないか確認し、あれば削除します。
    /// これは分岐などで道がオーバーラップするのを防ぐためです。
    /// </summary>
    private void CheckAndClearArea(Vector3 position)
    {
        // 指定位置の周辺に存在するColliderを全て取得
        Collider[] colliders = Physics.OverlapSphere(position, 1f, stageLayer);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Player")) continue; // プレイヤーは削除しない
            // activeSegmentsリストから該当オブジェクトを探す
            StageSegment segmentToDestroy = activeSegments.Find(s => s.gameObject == collider.gameObject);
            if (segmentToDestroy != null)
            {
                // リストにあれば、正式な手順で削除
                DestroySegment(segmentToDestroy);
                activeSegments.Remove(segmentToDestroy);
            }
            else
            {
                // リストにない場合（分岐の仮生成パスなど）は、オブジェクトのルートを辿って強制的に削除
                Transform root = collider.transform;
                while (root.parent != null && !root.CompareTag("Player")) { root = root.parent; }
                Destroy(root.gameObject);
            }
        }
    }

    /// <summary>
    /// 指定されたゲームオブジェクト内の全ての "RoadWall" という名前の子オブジェクトの表示/非表示を切り替えます。
    /// 分岐の直前・直後の道の壁を消すために使います。
    /// </summary>
    private void SetAllRoadWallsActive(GameObject targetObject, bool isActive)
    {
        if (targetObject == null) return;
        
        // 非アクティブなオブジェクトも含めて全ての子Transformを取得
        Transform[] allChildren = targetObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name == "RoadWall")
            {
                // 現在の状態と設定したい状態が違う場合のみ、SetActiveを呼び出す（パフォーマンスのため）
                if (child.gameObject.activeSelf != isActive)
                {
                    child.gameObject.SetActive(isActive);
                }
            }
        }
    }

    /// <summary>
    /// 指定されたプレハブを使って、新しい「道」セグメントをインスタンス化します。
    /// </summary>
    void InstantiateRoad(GameObject prefab)
    {
        // 最後に生成したセグメントの終端に接続するように、新しい道のスポーン位置を計算
        float offset = (lastGeneratedSegment?.length ?? 0) / 2f + roadPhysicalLength / 2f;
        Vector3 spawnPosition = (lastGeneratedSegment?.position ?? Vector3.zero) + currentDirection * offset;
        CheckAndClearArea(spawnPosition); // スポーン位置をクリア

        // プレハブをインスタンス化し、向きとスケール、位置を設定
        GameObject road = Instantiate(prefab, spawnPosition, Quaternion.LookRotation(currentDirection));
        road.transform.localScale = new Vector3(1, 1, 3);
        road.transform.position = new Vector3(road.transform.position.x, 0, road.transform.position.z); // Y座標を0に固定

        // この道の壁を有効化する
        SetAllRoadWallsActive(road, true);

        // 新しいセグメントとして情報をリストに登録
        StageSegment newSegment = new StageSegment(road, spawnPosition, currentDirection, false, roadPhysicalLength);
        activeSegments.Add(newSegment);
        lastGeneratedSegment = newSegment; // 最後に生成したセグメントとして更新
    }

    /// <summary>
    /// 重み付きプレハブのリストから、重みに応じてランダムに1つを選択します。
    /// </summary>
    WeightedPrefab SelectRandomPrefab(WeightedPrefab[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        var validPrefabs = prefabs.Where(p => p.prefab != null).ToArray(); // nullでないプレハブのみ抽出
        if (validPrefabs.Length == 0) return null;

        float totalWeight = validPrefabs.Sum(p => p.weight); // 重みの合計を計算
        if (totalWeight <= 0) return validPrefabs.FirstOrDefault(); // 重みがなければ最初のものを返す

        float randomValue = Random.Range(0f, totalWeight); // 0から合計値までの乱数を生成
        float currentWeight = 0f;
        foreach (var item in validPrefabs)
        {
            currentWeight += item.weight;
            if (randomValue <= currentWeight) return item; // 乱数が現在の重みの範囲内なら、そのプレハブを返す
        }
        return validPrefabs.Last(); // 万が一のためのフォールバック
    }
    
    /// <summary>
    /// シーケンス用のプレハブリストから、現在のシーケンス状態に合ったプレハブを選択します。
    /// </summary>
    /// <param name="forcePart">強制的に選択するパート('b':中間, 'c':終端)</param>
    private WeightedPrefab SelectSequencePrefab(char? forcePart = null)
    {
        WeightedPrefab[] targetList;
        // 現在のシーケンスタイプに応じて、使用するプレハブリストを決定
        switch (currentSequenceType)
        {
            case "C": targetList = cOnlyLaneRoadPrefabs; break;
            case "L": targetList = lOnlyLaneRoadPrefabs; break;
            case "R": targetList = rOnlyLaneRoadPrefabs; break;
            default: return null;
        }
        
        // プレハブ名にマッチさせるための正規表現パターンを作成
        // 例: プレハブ名が "C_a_..."(開始), "C_b_..."(中間), "C_c_..."(終端) のようになっていると仮定
        string pattern = forcePart.HasValue
            ? $"^({currentSequenceType})_{forcePart.Value}" // 終端を強制する場合: "C_c" で始まるものを探す
            : $"^({currentSequenceType})_(b|c)";          // 通常の中間: "C_b" or "C_c" で始まるものを探す

        // パターンに一致するプレハブをリストからフィルタリング
        var filteredList = targetList.Where(p => p.prefab != null && Regex.IsMatch(p.prefab.name, pattern)).ToArray();
        // フィルタリングされたリストからランダムに1つ選択
        return SelectRandomPrefab(filteredList);
    }

    /// <summary>
    /// 通常の道、またはシーケンスの一部となる道を生成します。
    /// </summary>
    void CreateRoadSegment()
    {
        WeightedPrefab selectedWeightedPrefab = null;
        bool forceEndSequence = false; // シーケンスを強制終了させるかのフラグ

        if (currentSequenceState == SequenceState.InSequence)
        {
            // --- シーケンス中の処理 ---
            // シーケンスが最大長に達したら、強制的に終了させる
            if (maxSequenceLength != -1 && currentSequenceCount >= maxSequenceLength - 1)
            {
                forceEndSequence = true;
            }
            
            // 強制終了なら終端プレハブ('c')を、そうでなければ中間('b')か終端('c')プレハブを選択
            selectedWeightedPrefab = forceEndSequence ? SelectSequencePrefab('c') : SelectSequencePrefab();
            if (forceEndSequence && selectedWeightedPrefab != null)
            {
                 Debug.Log($"<color=red>シーケンス '{currentSequenceType}' を強制終了します。</color>");
            }
            
            if (selectedWeightedPrefab != null)
            {
                // 選択したプレハブで道を生成
                InstantiateRoad(selectedWeightedPrefab.prefab);
                // プレハブ名が終端パターンに一致するか、強制終了フラグが立っていればシーケンスを終了
                string endPattern = $"^({currentSequenceType})_c";
                if (forceEndSequence || Regex.IsMatch(selectedWeightedPrefab.prefab.name, endPattern))
                {
                    // 状態をリセットして通常モードに戻す
                    currentSequenceState = SequenceState.None; currentSequenceType = ""; currentSequenceCount = 0;
                }
                else
                {
                    // シーケンス継続、カウンターをインクリメント
                    currentSequenceCount++;
                }
            }
            else
            {
                // 適切なプレハブが見つからなかった場合、安全のためにシーケンスを終了し、通常の道を生成し直す
                currentSequenceState = SequenceState.None; currentSequenceType = ""; currentSequenceCount = 0;
                CreateRoadSegment();
            }
        }
        else
        {
            // --- 通常時の処理 ---
            // 通常の道プレハブリストからランダムに選択
            selectedWeightedPrefab = SelectRandomPrefab(roadPrefabs);
            if (selectedWeightedPrefab?.prefab != null)
            {
                InstantiateRoad(selectedWeightedPrefab.prefab);
                // プレハブ名がシーケンス開始パターン（例: "C_a_..."）に一致するかチェック
                Regex regex = new Regex(@"^(C|L|R)_a");
                Match match = regex.Match(selectedWeightedPrefab.prefab.name);
                if (match.Success)
                {
                    // 一致した場合、シーケンス開始
                    currentSequenceType = match.Groups[1].Value; // "C", "L", "R" のいずれか
                    currentSequenceState = SequenceState.InSequence;
                    currentSequenceCount = 1;
                    Debug.Log($"<color=cyan>シーケンス開始:</color> タイプ'{currentSequenceType}'");
                }
            }
        }
    }

    /// <summary>
    /// 新しい「分岐」セグメントを生成します。
    /// </summary>
    void CreateBranchSegment()
    {
        // 最後に生成したのが道であれば、その道の壁を非表示にして分岐とスムーズにつなげる
        if (lastGeneratedSegment != null && !lastGeneratedSegment.isBranch && lastGeneratedSegment.gameObject != null)
        {
            SetAllRoadWallsActive(lastGeneratedSegment.gameObject, false);
        }

        // 分岐のスポーン位置を計算
        float offset = (lastGeneratedSegment?.length ?? 0) / 2f + branchPhysicalLength / 2f;
        Vector3 spawnPosition = (lastGeneratedSegment?.position ?? Vector3.zero) + currentDirection * offset;
        CheckAndClearArea(spawnPosition);

        // 分岐プレハブをランダムに選択
        GameObject prefab = SelectRandomPrefab(branchPrefabs)?.prefab;
        if (prefab == null) { Debug.LogError("生成できる分岐プレハブがありません！"); return; }
        
        // 分岐をインスタンス化
        GameObject branch = Instantiate(prefab, spawnPosition, Quaternion.LookRotation(currentDirection));
        branch.transform.localScale = Vector3.one;
        branch.transform.position = new Vector3(branch.transform.position.x, 0, branch.transform.position.z);
        
        // 分岐プレハブ内の"TriggerArea"オブジェクトを探し、BranchDirectionCheckerスクリプトを取得
        Transform triggerObject = branch.transform.Find("TriggerArea");
        if (triggerObject == null) { Debug.LogError("Branch prefab must have a child 'TriggerArea'!"); return; }
        BranchDirectionChecker checker = triggerObject.GetComponent<BranchDirectionChecker>();
        if (checker == null) { Debug.LogError("'TriggerArea' must have a BranchDirectionChecker script!"); return; }
        
        // 新しい分岐セグメントとしてリストに登録
        StageSegment newSegment = new StageSegment(branch, spawnPosition, currentDirection, true, branchPhysicalLength);
        activeSegments.Add(newSegment);
        lastGeneratedSegment = newSegment;

        // 分岐の先に、進行可能な方向すべての道を「仮に」生成する
        CreateAdjacentBranchRoads(newSegment, checker);
        // プレイヤーがまだ方向を選択していない状態に設定
        isPlayerAtUnresolvedBranch = true;
    }

    /// <summary>
    /// 分岐の先に、進行可能な方向（前、左、右）それぞれに仮の道を生成します。
    /// </summary>
    void CreateAdjacentBranchRoads(StageSegment branchSegment, BranchDirectionChecker checker)
    {
        Vector3 branchCenter = branchSegment.position;
        // 各方向に道が生成される可能性のあるエリアをクリア
        CheckAndClearArea(branchCenter + branchSegment.direction * roadPhysicalLength);
        CheckAndClearArea(branchCenter + GetLeftDirection(branchSegment.direction) * roadPhysicalLength);
        CheckAndClearArea(branchCenter + GetRightDirection(branchSegment.direction) * roadPhysicalLength);

        // BranchDirectionCheckerの設定に基づき、各方向に道を生成
        if (checker.canGoForward) GenerateRoadsForDirection(branchSegment, branchCenter, branchSegment.direction, forwardCount);
        if (checker.canGoLeft) GenerateRoadsForDirection(branchSegment, branchCenter, GetLeftDirection(branchSegment.direction), 1);
        if (checker.canGoRight) GenerateRoadsForDirection(branchSegment, branchCenter, GetRightDirection(branchSegment.direction), 1);
    }
    
    /// <summary>
    /// 指定された方向に、指定された数だけ道を連続で生成するヘルパーメソッド。
    /// </summary>
    void GenerateRoadsForDirection(StageSegment parentBranch, Vector3 startCenter, Vector3 direction, int count)
    {
        Vector3 lastRoadPos = startCenter;
        float lastLength = parentBranch.length;

        for (int i = 0; i < count; i++)
        {
            // 次の道のスポーン位置を計算
            float offset = lastLength / 2f + roadPhysicalLength / 2f;
            Vector3 spawnPos = lastRoadPos + direction * offset;
            
            GameObject prefab = SelectRandomPrefab(roadPrefabs)?.prefab;
            if(prefab == null) continue;

            // 道をインスタンス化
            GameObject road = Instantiate(prefab, spawnPos, Quaternion.LookRotation(direction));
            road.transform.localScale = new Vector3(1, 1, 3);
            road.transform.position = new Vector3(road.transform.position.x, 0, road.transform.position.z);
            
            // 分岐に直接つながる最初の道(i=0)の壁は非表示にし、それ以降は表示する
            SetAllRoadWallsActive(road, i != 0);
            
            // 生成した道を親である分岐セグメントの「子」としてリストに保持（後で不要な道を削除するため）
            parentBranch.childRoads.Add(road);
            lastRoadPos = spawnPos;
            lastLength = roadPhysicalLength;
        }
    }

    /// <summary>
    /// PlayerControllerから呼び出され、プレイヤーが選択した方向を確定させます。
    /// 選択されなかった他の方向の道は削除されます。
    /// </summary>
    public void CommitDirection(Vector3 newDirection, Transform branchTransform)
    {
        // どの分岐での選択かを特定
        StageSegment branchSegment = activeSegments.FirstOrDefault(s => s.gameObject != null && s.gameObject.transform == branchTransform);
        if (branchSegment == null) return;

        // ステージの進行方向をプレイヤーが選択した新しい方向に更新
        currentDirection = newDirection.normalized;
        List<GameObject> roadsToKeep = new List<GameObject>(); // 選択された方向の道だけを保持するリスト

        // 分岐が持っている仮生成された子ロードをすべてチェック
        foreach (GameObject childRoad in branchSegment.childRoads)
        {
            if (childRoad != null)
            {
                // 道の向きと新しい進行方向がほぼ同じ（内積が1に近い）か判定
                if (Vector3.Dot(childRoad.transform.forward, currentDirection) > 0.99f)
                {
                    // 選択された方向の道なので、保持リストに追加
                    roadsToKeep.Add(childRoad);
                }
                else
                {
                    // 選択されなかった方向の道なので、破棄
                    Destroy(childRoad);
                }
            }
        }
        
        if (roadsToKeep.Count > 0)
        {
            // 保持する道を分岐に近い順にソート
            roadsToKeep.Sort((a, b) => 
                Vector3.Distance(branchSegment.position, a.transform.position)
                .CompareTo(Vector3.Distance(branchSegment.position, b.transform.position)));

            // --- ★ここからが重要★ ---
            // 保持することになった道を、仮の状態から正式なアクティブセグメントとして再登録する
            for (int i = 0; i < roadsToKeep.Count; i++)
            {
                GameObject road = roadsToKeep[i];
                // 分岐の直後(i=0)の道の壁は非表示、それ以降は表示、というルールを再適用
                SetAllRoadWallsActive(road, i != 0);

                // 新しいアクティブセグメントとして正式にリストに追加
                StageSegment newActiveSegment = new StageSegment(road, road.transform.position, currentDirection, false, roadPhysicalLength);
                activeSegments.Add(newActiveSegment);
            }
            // --- ★ここまで★ ---

            // 最後に生成されたセグメントの情報を、今確定した道の最後のものに更新
            lastGeneratedSegment = activeSegments.Last(s => s.gameObject == roadsToKeep.Last());

            // 確定した道の最初のプレハブ名から、シーケンスが開始するかチェック
            GameObject chosenRoad = roadsToKeep.First(); 
            Regex regex = new Regex(@"^(C|L|R)_a");
            Match match = regex.Match(chosenRoad.name);
            if (match.Success)
            {
                currentSequenceType = match.Groups[1].Value; 
                currentSequenceState = SequenceState.InSequence; 
                currentSequenceCount = 1;
            }
        }
        
        // 分岐セグメントが持っていた仮生成道のリストをクリア
        branchSegment.childRoads.Clear();
        // プレイヤーの分岐選択が完了したことを示すフラグを戻す
        isPlayerAtUnresolvedBranch = false;
    }

    /// <summary>
    /// 指定されたセグメントを構成する全てのゲームオブジェクトを破棄します。
    /// </summary>
    void DestroySegment(StageSegment segment)
    {
        if (segment == null) return;
        // セグメント本体（道 or 分岐）を破棄
        if (segment.gameObject != null) Destroy(segment.gameObject);
        // 分岐の場合、まだ残っている子ロードがあればそれも全て破棄
        if (segment.childRoads != null)
        {
            foreach (GameObject childRoad in segment.childRoads)
            {
                if (childRoad != null) Destroy(childRoad);
            }
            segment.childRoads.Clear();
        }
    }
    
    // --- ヘルパーメソッド ---
    
    /// <summary>前方向ベクトルから左方向（-90度回転）のベクトルを取得します。</summary>
    Vector3 GetLeftDirection(Vector3 forward) => new Vector3(-forward.z, 0, forward.x);
    
    /// <summary>前方向ベクトルから右方向（+90度回転）のベクトルを取得します。</summary>
    Vector3 GetRightDirection(Vector3 forward) => new Vector3(forward.z, 0, -forward.x);
}