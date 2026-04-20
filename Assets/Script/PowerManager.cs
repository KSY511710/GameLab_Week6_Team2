using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Special.Composition;
using Special.Composition.Contexts;
using Special.Data;
using Special.Effects;
using Special.Integration;
using Special.Runtime;

// ==========================================
//   [데이터 명찰 클래스들]
// ==========================================
[System.Serializable]
public class BlockAttribute
{
    public int colorID;  // 색상 (1: 빨강, 2: 파랑 등 / 0: 색상 없음)
    public int shapeID;  // 모양 (당장 안 써도 0으로 기본값 세팅)
    // 특수 블럭이면 비null. 일반 블럭 경로를 보존하기 위해 기본은 null.
    [System.NonSerialized] public SpecialBlockDefinition specialDef;

    public BlockAttribute(int color)
    {
        colorID = color;
        shapeID = 0;
    }

    public BlockAttribute(int color, int shape)
    {
        colorID = color;
        shapeID = shape;
    }

    public BlockAttribute(int color, int shape, SpecialBlockDefinition def)
    {
        colorID = color;
        shapeID = shape;
        specialDef = def;
    }
}

public class BlockData
{
    public BlockAttribute attribute;
    public bool isGrouped;
    public int groupID;
    public GameObject blockObject;
}

public class GroupInfo
{
    public int groupID;
    public int blockSize;
    public int finalColor;
    public int finalShape;
    public int formationMultiplier;
    public float groupPower;
    public float appliedExchangeRatio;
    public float estimatedMoneyGen;

    // 시각 시퀀서가 재계산 없이 표시할 수 있도록 중간값/멤버 캐시
    public int baseProduction;
    public int uniqueParts;
    public int completionMultiplier;
    public float colorMultiplier;
    public Color dominantRealColor;
    public List<PlacedBlockVisual> members;
    // 특수 블럭 효과가 Scope 판정에 사용 (배열 인덱스 좌표).
    public List<Vector2Int> clusterPositions;

    /// <summary>
    /// PowerPlant role 특수 블럭이 자기 footprint 로만 이루어진 솔로 그룹을 형성했을 때 true.
    /// BFS 가 만든 "실제 그룹" 과 라이브 파워 갱신/정산 규칙을 구분하기 위한 플래그.
    /// </summary>
    public bool isPowerPlantSolo;

    /// <summary>
    /// 가장 최근 전력 계산 과정 기록. 정보 패널/시퀀서가 실제 수치 기반 텍스트를 만들 때 소스로 사용.
    /// CreateNewGroup 최초 생성 시, 이후 RecalculateAllGroupPowers 호출마다 갱신된다.
    /// </summary>
    public Special.Runtime.CalculationTrace lastTrace;
}

/// <summary>
/// 효과(Effect) 가 ProductionSettle 훅에서 제출하는 일일 생산 기여분.
/// PowerManager.SubmitSpecialContribution 으로 등록되며
/// SettlementUIController 정산 단계에서 색상 버킷(또는 자투리)에 합산된다.
/// </summary>
public class SpecialPowerContribution
{
    public SpecialBlockInstance owner;
    public SpecialBlockDefinition definition;   // 디버깅/확장용 백참조.
    public int colorID;                          // 1=Red, 2=Blue, 3=Yellow, 0=자투리/OffPalette
    public float power;                          // GWh
    public float appliedExchangeRatio;           // 돈 환산 시 사용한 비율(기본 환율)
    public float estimatedMoney;                 // power / appliedExchangeRatio
    public string sourceTag;                     // 디버그 로그용 소스 식별자(효과 이름 등)
}

// ==========================================
//   [메인 매니저 클래스]
// ==========================================
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI powerText;

    [Header("Group Settings")]
    public int groupMinSize = 9; // 인스펙터에서 수정 가능 (10칸부터 그룹)
    public int groupMinpart = 3;

    public List<GroupInfo> activeGroups = new List<GroupInfo>();
    private int nextGroupID = 1;
    private int totalPower = 0;
    private int yesterdayProduction = 0;

    // 시퀀서 등 외부에서 보드 배열을 다시 순회하지 않고 미그룹 블럭에 접근하도록 캐시
    public int LastUngroupedCount { get; private set; }
    public List<PlacedBlockVisual> LastUngroupedVisuals { get; private set; } = new List<PlacedBlockVisual>();

    // ProductionSettle 훅에서 효과들이 제출하는 일일 기여 누적.
    // ProceedToNextDay 시작 시 초기화되어 정산 1회 주기 동안만 유효.
    private readonly List<SpecialPowerContribution> pendingContributions = new List<SpecialPowerContribution>();
    public IReadOnlyList<SpecialPowerContribution> PendingContributions => pendingContributions;

    public bool IsAnimating { get; private set; }

    /// <summary>현재 보드의 라이브 총 발전량이 변경되었을 때 발화. Skip 가용성 재평가 등에 사용.</summary>
    public static event Action OnTotalPowerChanged;
    public GachaConnector GachaConnector;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateDisplayedPowerUI();
    }

    // 1. 그룹 검사 및 형성
    public void CheckAndFormGroups(BlockData[,] board, int width, int height)
    {
        bool[,] visited = new bool[width, height];
        bool isAnyGroupCreated = false; // 🌟 이번 체크에서 그룹이 생겼는지 확인하는 플래그

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];

                if (cell != null && IsEligibleForGrouping(cell) && !cell.isGrouped && !visited[x, y])
                {
                    List<Vector2Int> cluster = GetUnlockedCluster(x, y, board, visited, width, height);

                    if (cluster.Count >= groupMinSize)
                    {
                        HashSet<int> uniqueParts = new HashSet<int>();
                        foreach (Vector2Int pos in cluster)
                        {
                            if (board[pos.x, pos.y].attribute.shapeID > 0)
                                uniqueParts.Add(board[pos.x, pos.y].attribute.shapeID);
                        }

                        if (uniqueParts.Count >= groupMinpart)
                        {
                            CreateNewGroup(cluster, board);
                            isAnyGroupCreated = true; // 🌟 그룹이 생성됨을 기록!
                        }
                    }
                }
            }
        }

        // 🌟 그룹이 하나라도 생겼다면, 그때만 보드 전체의 아웃라인을 업데이트합니다.
        if (isAnyGroupCreated)
        {
            UpdateAllOutlines(board, width, height);
        }

        FormPowerPlantSoloGroups(board);
    }

    // 그룹에 참여할 수 있는 셀인지 판정. Grouping 이외 role(Independent / PowerPlant) 은 BFS 에서 제외.
    // PowerPlant 는 별도로 FormPowerPlantSoloGroups 에서 솔로 그룹을 만든다.
    private static bool IsEligibleForGrouping(BlockData cell)
    {
        if (cell.attribute.colorID <= 0) return false;
        SpecialBlockDefinition def = cell.attribute.specialDef;
        if (def != null && def.role != SpecialBlockRole.Grouping) return false;
        return true;
    }

    // PowerPlant role 은 솔로 그룹에서 별도 groupPower 로 반영되므로 ungrouped 기본 +1 대상에서 제외.
    // Independent 는 ungrouped +1 로 scrap 에 합산된다.
    private static bool CountsAsUngroupedBase(BlockData cell)
    {
        if (cell == null || cell.attribute.colorID <= 0 || cell.isGrouped) return false;
        SpecialBlockDefinition def = cell.attribute.specialDef;
        if (def != null && def.role == SpecialBlockRole.PowerPlant) return false;
        return true;
    }

    /// <summary>
    /// 설치된 PowerPlant 인스턴스 중 아직 groupId 가 없는 것들을 각자 솔로 그룹으로 묶는다.
    /// 솔로 그룹은 다음 세 가지 역할을 동시에 수행한다.
    /// 1) cell.isGrouped=true 로 세팅 → GridManager 인접 금지 규칙이 PowerPlant 주변 배치를 차단.
    /// 2) activeGroups 에 GroupInfo 로 포함 → ScopeEvaluator 의 OwnPowerPlant / AdjacentPowerPlant /
    ///    GroupWithinRange / GroupInZone 질의가 PowerPlant 를 "발전소" 로 인지.
    /// 3) groupPower = sum(EffectAsset.EstimateLivePower) → PowerText 실시간 합계와
    ///    SettlementUIController 색상 막대에 그대로 반영.
    /// Animation Sequencer 에는 enqueue 하지 않는다. 솔로 그룹 형성은 매 프레임/매 설치마다 일어날 수 있고,
    /// CreateNewGroup 처럼 "오늘의 연출" 대상이 아니기 때문.
    /// </summary>
    private void FormPowerPlantSoloGroups(BlockData[,] board)
    {
        if (SpecialBlockRegistry.Instance == null) return;
        int bw = board.GetLength(0);
        int bh = board.GetLength(1);

        IReadOnlyList<SpecialBlockInstance> installed = SpecialBlockRegistry.Instance.Installed;
        for (int i = 0; i < installed.Count; i++)
        {
            SpecialBlockInstance inst = installed[i];
            if (inst == null) continue;
            SpecialBlockDefinition def = inst.definition;
            if (def == null || def.role != SpecialBlockRole.PowerPlant) continue;
            if (inst.groupId > 0) continue;

            List<Vector2Int> footprintCells = new List<Vector2Int>(inst.footprint.Count);
            List<PlacedBlockVisual> visuals = new List<PlacedBlockVisual>();

            for (int f = 0; f < inst.footprint.Count; f++)
            {
                Vector2Int pos = inst.footprint[f];
                if (pos.x < 0 || pos.x >= bw || pos.y < 0 || pos.y >= bh) continue;
                BlockData cell = board[pos.x, pos.y];
                if (cell == null) continue;

                cell.isGrouped = true;
                cell.groupID = nextGroupID;
                footprintCells.Add(pos);

                if (cell.blockObject != null)
                {
                    PlacedBlockVisual v = cell.blockObject.GetComponent<PlacedBlockVisual>();
                    if (v != null) visuals.Add(v);
                }
            }
            if (footprintCells.Count == 0) continue;

            int colorID = ResolveContributionColorID(def);
            Color realColor = ColorIDToRealColor(colorID);
            float livePower = EstimateLivePowerOf(inst);
            float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
            if (baseRatio <= 0f) baseRatio = 1f;

            GroupInfo solo = new GroupInfo
            {
                groupID = nextGroupID,
                blockSize = footprintCells.Count,
                finalColor = colorID,
                finalShape = def.uniqueShapeId,
                formationMultiplier = 0,
                groupPower = livePower,
                appliedExchangeRatio = baseRatio,
                estimatedMoneyGen = baseRatio > 0f ? livePower / baseRatio : 0f,
                baseProduction = footprintCells.Count,
                uniqueParts = 0,
                completionMultiplier = 0,
                colorMultiplier = 1f,
                dominantRealColor = realColor,
                members = visuals,
                clusterPositions = footprintCells,
                isPowerPlantSolo = true
            };

            foreach (PlacedBlockVisual v in visuals)
            {
                v.SetGroupState(true, realColor, visuals);
            }

            activeGroups.Add(solo);
            inst.groupId = nextGroupID;
            nextGroupID++;

            EffectRuntime.Instance.NotifyGroupFormed(solo);
        }
    }

    private static Color ColorIDToRealColor(int colorID)
    {
        if (colorID == 1) return new Color(1f, 0.2f, 0.2f);
        if (colorID == 2) return new Color(0.2f, 0.4f, 1f);
        if (colorID == 3) return new Color(0.2f, 1f, 0.2f);
        return Color.white;
    }

    private static float EstimateLivePowerOf(SpecialBlockInstance inst)
    {
        if (inst == null) return 0f;
        float sum = 0f;
        IReadOnlyList<EffectAsset> effects = inst.EffectInstances;
        for (int i = 0; i < effects.Count; i++)
        {
            EffectAsset asset = effects[i];
            if (asset == null) continue;
            try { sum += asset.EstimateLivePower(inst); }
            catch (Exception e) { Debug.LogException(e); }
        }
        return sum;
    }

    /// <summary>
    /// 비-PowerPlant(Grouping/Independent) 블럭의 라이브 파워 기여분.
    /// CompositeEffectAsset 의 OnProductionSettle 모듈만 합산하므로 OnPowerCalculation 을 통해
    /// 그룹 groupPower 에 이미 반영된 값과는 이중 집계되지 않는다.
    /// </summary>
    private static float EstimateNonPowerPlantLiveContributionOf(SpecialBlockInstance inst)
    {
        if (inst == null) return 0f;
        float sum = 0f;
        IReadOnlyList<EffectAsset> effects = inst.EffectInstances;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is CompositeEffectAsset composite)
            {
                try { sum += composite.EstimateLiveContributionPower(inst); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
        return sum;
    }

    // BFS 덩어리 탐색
    private List<Vector2Int> GetUnlockedCluster(int startX, int startY, BlockData[,] board, bool[,] visited, int width, int height)
    {
        List<Vector2Int> clusterList = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();
            clusterList.Add(curr);

            foreach (Vector2Int dir in directions)
            {
                int nextX = curr.x + dir.x;
                int nextY = curr.y + dir.y;

                if (nextX >= 0 && nextX < width && nextY >= 0 && nextY < height)
                {
                    BlockData nextCell = board[nextX, nextY];

                    if (nextCell != null && IsEligibleForGrouping(nextCell) && !nextCell.isGrouped && !visited[nextX, nextY])
                    {
                        visited[nextX, nextY] = true;
                        queue.Enqueue(new Vector2Int(nextX, nextY));
                    }
                }
            }
        }
        return clusterList;
    }

    // 새로운 속성 판정 시스템
    private void CreateNewGroup(List<Vector2Int> cluster, BlockData[,] board)
    {
        // MultiPrimary 특수 블럭의 colorID 를 주변 최다 색으로 확정해야 이후 color 카운트가 정확하다.
        SpecialBlockResolver.ResolveGroupColors(cluster, board);

        Dictionary<int, int> colorCounts = new Dictionary<int, int>();
        HashSet<int> uniqueParts = new HashSet<int>();

        foreach (Vector2Int pos in cluster)
        {
            BlockData cell = board[pos.x, pos.y];
            cell.isGrouped = true;
            cell.groupID = nextGroupID;

            int c = cell.attribute.colorID;
            if (colorCounts.ContainsKey(c)) colorCounts[c]++;
            else colorCounts[c] = 1;

            uniqueParts.Add(cell.attribute.shapeID); // 부품 종류 수집
        }

        // --- 새로운 전력 계산 공식 ---
        int baseProduction = cluster.Count;
        int uniquePartsCount = uniqueParts.Count;

        int shapeBonus = FormationDetector.GetFormationMultiplier(cluster);
        if (shapeBonus < 0) shapeBonus = 0;
        int completionMultiplier = 2 + shapeBonus;

        int maxColorCount = colorCounts.Values.Max();
        int restColorCount = cluster.Count - maxColorCount;
        float colorMultiplier = 1f + (maxColorCount - restColorCount) * 0.2f;

        // 효과 훅: PowerCalculationContext 에 누적 후 최종식에 반영. 효과가 없으면 기존 공식과 동일.
        CalculationTrace trace = new CalculationTrace();
        PowerCalculationContext ctx = new PowerCalculationContext
        {
            BaseProductionRaw = baseProduction,
            UniquePartsRaw = uniquePartsCount,
            BaseCompletionRaw = 2,
            ShapeCompletionRaw = shapeBonus,
            ColorMultiplierRaw = colorMultiplier,
            ClusterPositions = cluster,
            Trace = trace
        };
        SeedTraceRawValues(ctx);
        EffectRuntime.Instance.ApplyPowerHooks(ctx);

        float finalPower = ctx.Compute();
        // float finalPower = (baseProduction + uniquePartsCount) * completionMultiplier * colorMultiplier;
        float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
        float currentRatio = baseRatio;
        float estimatedMoney = finalPower / currentRatio;

        // --- 정보 저장 ---
        int dominantColor = colorCounts.OrderByDescending(x => x.Value).First().Key;

        Color dominantRealColor = Color.white;
        if (dominantColor == 1) dominantRealColor = new Color(1f, 0.2f, 0.2f);
        else if (dominantColor == 2) dominantRealColor = new Color(0.2f, 0.4f, 1f);
        else if (dominantColor == 3) dominantRealColor = new Color(0.2f, 1f, 0.2f);

        List<PlacedBlockVisual> currentGroupVisuals = new List<PlacedBlockVisual>();

        foreach (Vector2Int pos in cluster)
        {
            GameObject placedBlockObj = board[pos.x, pos.y].blockObject;
            if (placedBlockObj != null)
            {
                PlacedBlockVisual visualControl = placedBlockObj.GetComponent<PlacedBlockVisual>();
                if (visualControl != null)
                {
                    currentGroupVisuals.Add(visualControl);
                }
            }
        }

        foreach (PlacedBlockVisual visualControl in currentGroupVisuals)
        {
            visualControl.SetGroupState(true, dominantRealColor, currentGroupVisuals);
        }

        GroupInfo newGroup = new GroupInfo
        {
            groupID = nextGroupID,
            blockSize = cluster.Count,
            finalColor = dominantColor,
            formationMultiplier = shapeBonus,
            groupPower = finalPower,
            appliedExchangeRatio = currentRatio,
            estimatedMoneyGen = estimatedMoney,
            baseProduction = baseProduction,
            uniqueParts = uniquePartsCount,
            completionMultiplier = completionMultiplier,
            colorMultiplier = colorMultiplier,
            dominantRealColor = dominantRealColor,
            members = currentGroupVisuals,
            clusterPositions = new List<Vector2Int>(cluster),
            lastTrace = trace
        };

        string debugMsg = $"<color=#00FFFF><b>[전력 정산 영수증 - 그룹 {nextGroupID}]</b></color>\n" +
                          $"1. 크기 및 다양성 : 기본 {baseProduction}칸 + 부품 {uniquePartsCount}종 = <b>{baseProduction + uniquePartsCount}</b>\n" +
                          $"2. 형태 보너스 : 기본 2 + 모양 {shapeBonus} = <b>x {completionMultiplier}</b>\n" +
                          $"3. 색상 순도 : 주력 {maxColorCount}칸 / 불순물 {restColorCount}칸 = <b>x {colorMultiplier:F2}</b>\n" +
                          $"<color=#FFFF00><b>▶ 최종 생산량 : ({baseProduction} + {uniquePartsCount}) * {completionMultiplier} * {colorMultiplier:F2} = {finalPower} GWh</b></color>";

        Debug.Log(debugMsg);

        activeGroups.Add(newGroup);
        nextGroupID++;

        // 특수 블럭의 groupId 갱신 + OwnPowerPlant 스코프 효과 활성 신호
        foreach (Vector2Int pos in cluster)
        {
            BlockData cell = board[pos.x, pos.y];
            if (cell?.attribute?.specialDef == null) continue;
            SpecialBlockInstance inst = SpecialBlockRegistry.Instance.FindByFootprintCell(pos);
            if (inst != null) inst.groupId = newGroup.groupID;
        }

        EffectRuntime.Instance.NotifyGroupFormed(newGroup);

        // 발전소 건설 완료 SFX
        PlayConstructionCompleteSfx();

        if (PowerAnimationSequencer.Instance != null)
        {
            PowerAnimationSequencer.Instance.EnqueueAnimation(newGroup);
        }
    }

    /// <summary>
    /// 일반 그룹형 발전소가 새로 완성된 순간 Construction SFX를 1회 재생한다.
    /// 
    /// 주의:
    /// - PowerPlant 솔로 그룹에는 사용하지 않는다.
    /// - 실제 CreateNewGroup 경로에서만 호출한다.
    /// </summary>
    private void PlayConstructionCompleteSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlaySfx(KSM_SfxType.Construction);
        }
    }

    public void CalculateTotalPower(BlockData[,] board, int width, int height)
    {
        // 훅은 CreateNewGroup 시점에만 fire 하고, 이후 새로 들어온 특수 블럭은
        // 기존 그룹의 groupPower 를 자동으로 갱신하지 못한다. 합산 전에 훅을 재반영해
        // AddBaseProductionModule 같은 "존재하는 발전소를 건드리는" 효과가 실제 값에 반영되게 한다.
        RecalculateAllGroupPowers();

        float calculatedTotalPower = 0;
        foreach (GroupInfo group in activeGroups)
        {
            calculatedTotalPower += group.groupPower;
        }

        // 미그룹 블럭 카운트와 비주얼 참조를 같은 패스에서 수집
        // Electricity role 은 기본 +1 대상에서 제외 (생산은 효과가 SubmitSpecialContribution 으로 전달).
        LastUngroupedVisuals.Clear();
        int ungroupedBlocks = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];
                if (!CountsAsUngroupedBase(cell)) continue;

                ungroupedBlocks++;
                if (cell.blockObject != null)
                {
                    PlacedBlockVisual v = cell.blockObject.GetComponent<PlacedBlockVisual>();
                    if (v != null) LastUngroupedVisuals.Add(v);
                }
            }
        }
        LastUngroupedCount = ungroupedBlocks;

        // 비-PowerPlant 특수 블럭(Grouping/Independent)의 OnProductionSettle 기여분을 라이브 파워에 합산.
        // - PowerPlant role 은 솔로 그룹 경로에서 이미 EstimateLivePower 가 groupPower 로 반영됨.
        // - OnPowerCalculation 모듈은 PowerCalculationContext 경로로 groupPower 에 녹아들어 있으므로
        //   EstimateLiveContributionPower 가 OnProductionSettle 만 합산해 이중 집계를 막는다.
        float nonPowerPlantLiveContrib = 0f;
        SpecialBlockRegistry registry = SpecialBlockRegistry.Instance;
        if (registry != null)
        {
            IReadOnlyList<SpecialBlockInstance> installed = registry.Installed;
            for (int i = 0; i < installed.Count; i++)
            {
                SpecialBlockInstance inst = installed[i];
                if (inst == null || inst.definition == null) continue;
                if (inst.definition.role == SpecialBlockRole.PowerPlant) continue;
                nonPowerPlantLiveContrib += EstimateNonPowerPlantLiveContributionOf(inst);
            }
        }

        int previous = totalPower;
        totalPower = (int)(calculatedTotalPower + ungroupedBlocks + nonPowerPlantLiveContrib);

        // 라이브 총합은 더 이상 powerText에 즉시 출력하지 않는다 (Feature 1).
        // 변동이 있을 때만 외부 구독자(예: ResourceManager)에게 알림.
        if (totalPower != previous)
        {
            OnTotalPowerChanged?.Invoke();
        }
        UpdateDisplayedPowerUI();
    }

    /// <summary>
    /// 현재 등록된 훅/효과 상태를 기준으로 모든 활성 그룹의 groupPower / 예상 수익을 다시 계산한다.
    /// - 일반 그룹 : CreateNewGroup 시점에 캐시해 둔 raw 중간값을 재사용해 PowerCalculationContext 훅만 다시 반영.
    /// - PowerPlant 솔로 그룹 : 소유 인스턴스의 모든 EffectAsset.EstimateLivePower 합으로 매번 재산출.
    ///   이 덕분에 "범위 내 빈칸이 변할 때마다" PowerText 실시간 합계가 즉시 갱신된다.
    /// </summary>
    public void RecalculateAllGroupPowers()
    {
        if (activeGroups == null || activeGroups.Count == 0) return;
        float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
        if (baseRatio <= 0f) baseRatio = 1f;

        for (int i = 0; i < activeGroups.Count; i++)
        {
            GroupInfo g = activeGroups[i];
            if (g == null || g.clusterPositions == null) continue;

            if (g.isPowerPlantSolo)
            {
                SpecialBlockInstance owner = FindPowerPlantOwner(g);
                g.groupPower = owner != null ? EstimateLivePowerOf(owner) : 0f;
                float soloRatio = ResolveExchangeRatio(g, baseRatio);
                g.appliedExchangeRatio = soloRatio;
                g.estimatedMoneyGen = g.groupPower / soloRatio;
                continue;
            }

            CalculationTrace refreshed = new CalculationTrace();
            PowerCalculationContext ctx = new PowerCalculationContext
            {
                BaseProductionRaw = g.baseProduction,
                UniquePartsRaw = g.uniqueParts,
                BaseCompletionRaw = 2,
                ShapeCompletionRaw = g.formationMultiplier,
                ColorMultiplierRaw = g.colorMultiplier,
                ClusterPositions = g.clusterPositions,
                Trace = refreshed
            };
            SeedTraceRawValues(ctx);
            EffectRuntime.Instance.ApplyPowerHooks(ctx);

            g.groupPower = ctx.Compute();
            float ratio = ResolveExchangeRatio(g, baseRatio);
            g.appliedExchangeRatio = ratio;
            g.estimatedMoneyGen = g.groupPower / ratio;
            g.lastTrace = refreshed;
        }
    }

    /// <summary>
    /// 효과와 무관한 공통 raw 단계를 Trace 에 순서대로 push. 효과 훅이 찍는 +/× 단계보다 반드시 먼저 기록되어야
    /// 정보 패널/시퀀서가 "raw → 효과 누적 → 최종" 순으로 자연스럽게 읽어올 수 있다.
    /// </summary>
    private static void SeedTraceRawValues(PowerCalculationContext ctx)
    {
        if (ctx.Trace == null) return;
        ctx.Trace.RecordRaw(CalcStage.Base, "기본 생산량(칸)", ctx.BaseProductionRaw);
        ctx.Trace.RecordRaw(CalcStage.UniqueParts, "부품 종류", ctx.UniquePartsRaw);
        ctx.Trace.RecordRaw(CalcStage.BaseCompletion, "기본 완성도", ctx.BaseCompletionRaw);
        ctx.Trace.RecordRaw(CalcStage.ShapeCompletion, "모양 완성도", ctx.ShapeCompletionRaw);
        ctx.Trace.RecordRaw(CalcStage.ColorMultiplier, "색상 순도 배율", ctx.ColorMultiplierRaw);
    }

    /// <summary>
    /// 그룹 환전 비율 훅을 발화해 최종 ratio 를 돌려준다. 효과가 없으면 baseRatio 그대로.
    /// 합성식 Compute() 가 하한 0.01 클램프를 이미 보장하지만, 호출부에서 /ratio 를 쓰므로 추가 방어 하지 않음.
    /// </summary>
    private static float ResolveExchangeRatio(GroupInfo group, float baseRatio)
    {
        ExchangeRatioContext exCtx = new ExchangeRatioContext
        {
            Group = group,
            BaseRatio = baseRatio
        };
        EffectRuntime.Instance.ApplyExchangeRatioHooks(exCtx);
        return exCtx.Compute();
    }

    /// <summary>PowerPlant 솔로 그룹에 대응되는 SpecialBlockInstance 역조회.</summary>
    private static SpecialBlockInstance FindPowerPlantOwner(GroupInfo g)
    {
        if (g == null || g.clusterPositions == null || g.clusterPositions.Count == 0) return null;
        if (SpecialBlockRegistry.Instance == null) return null;
        return SpecialBlockRegistry.Instance.FindByFootprintCell(g.clusterPositions[0]);
    }

    /// <summary>시퀀서가 일일 정산 직후 호출. powerText에 표시될 "전날 발전량"을 갱신.</summary>
    public void CommitYesterdayProduction(int production)
    {
        yesterdayProduction = production;
        UpdateDisplayedPowerUI();
    }

    /// <summary>시퀀서가 시작/종료 시 토글. true 동안에는 입력 차단/Skip 비활성에 사용.</summary>
    public void SetAnimating(bool value)
    {
        if (IsAnimating == value) return;
        IsAnimating = value;
        // Skip 가용성이 IsAnimating에 의존하므로 재평가 신호로 OnTotalPowerChanged 재발화.
        OnTotalPowerChanged?.Invoke();
    }

    private void UpdateDisplayedPowerUI()
    {
        if (powerText == null) return;
        powerText.text =
            $"{totalPower}";
    }

    public int GetTotalPower()
    {
        return totalPower;
    }

    public int YesterdayProduction => yesterdayProduction;
    public void UpdateAllOutlines(BlockData[,] board, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData myData = board[x, y];
                if (myData == null || myData.blockObject == null) continue;

                bool showTop, showBottom, showLeft, showRight;

                // 🌟 그룹인 경우에만 이웃을 체크해서 선을 끕니다.
                if (myData.isGrouped)
                {
                    int myGroupID = myData.groupID;
                    showTop = (y + 1 >= height) || (board[x, y + 1] == null) || (board[x, y + 1].groupID != myGroupID);
                    showBottom = (y - 1 < 0) || (board[x, y - 1] == null) || (board[x, y - 1].groupID != myGroupID);
                    showLeft = (x - 1 < 0) || (board[x - 1, y] == null) || (board[x - 1, y].groupID != myGroupID);
                    showRight = (x + 1 >= width) || (board[x + 1, y] == null) || (board[x + 1, y].groupID != myGroupID);
                }
                else
                {
                    // 🌟 그룹이 아니면 무조건 4방향 선을 다 켭니다!
                    showTop = showBottom = showLeft = showRight = false;
                }

                // 시각적 업데이트 (Line_U, D, L, R 오브젝트 제어)
                UpdateLineObjects(myData.blockObject, showTop, showBottom, showLeft, showRight);
            }
        }
    }
    private void UpdateLineObjects(GameObject obj, bool t, bool b, bool l, bool r)
    {
        Transform u = obj.transform.Find("Line_U");
        Transform d = obj.transform.Find("Line_D");
        Transform ll = obj.transform.Find("Line_L");
        Transform rr = obj.transform.Find("Line_R");

        if (u != null) u.gameObject.SetActive(t);
        if (d != null) d.gameObject.SetActive(b);
        if (ll != null) ll.gameObject.SetActive(l);
        if (rr != null) rr.gameObject.SetActive(r);
    }
    public void ProceedToNextDay()
    {
        if (IsAnimating) return;

        // 0. 정산 직전 라이브 파워 리프레시.
        //    PowerPlant 솔로 그룹의 groupPower (= EstimateLivePower 합) 를 지금 이 순간 보드 상태 기준으로 재산출해
        //    CalculateTotalPower 가 마지막으로 불린 이후 보드가 바뀌었어도 SettlementData 에 최신 값이 실리도록 한다.
        RecalculateAllGroupPowers();

        // 1. 효과별 일일 생산 기여분 수집 페이즈.
        //    비-PowerPlant 효과가 ProductionSettle 훅에서 SubmitSpecialContribution 을 호출해 pendingContributions 를 채운다.
        pendingContributions.Clear();
        EffectRuntime.Instance.NotifyProductionSettle();

        // 2. 정산 데이터 빌드 (그룹 + 특수 기여 + 자투리).
        SettlementData data = BuildSettlementData();

        // 3. 애니메이션 & 사후 처리.
        //    NotifyDailySettle 은 애니메이션 후 시각/타이머 계열 효과용으로 유지.
        if (SettlementUIController.Instance != null)
        {
            SetAnimating(true);

            SettlementUIController.Instance.PlaySettlementAnimation(data, () =>
            {
                FinalizeDailySettlement();
                CommitYesterdayProduction(totalPower);
                EffectRuntime.Instance.NotifyDailySettle();
                if (ResourceManager.Instance != null) ResourceManager.Instance.ProcessNextDay();
                GachaConnector.OnOffShop(true);
                SetAnimating(false);
            });
        }
        else
        {
            FinalizeDailySettlement();
            EffectRuntime.Instance.NotifyDailySettle();
            GachaConnector.OnOffShop(true);
        }
    }
    /// <summary>
    /// 정산 UI 에 전달할 SettlementData 빌더.
    /// 집계 규칙:
    /// - 색상(1/2/3) 을 가진 그룹(일반 BFS 그룹 + Single/MultiPrimary PowerPlant 솔로) 의 groupPower 는 red/blue/green 버킷에 합류.
    /// - 색상이 없는 그룹(OffPalette PowerPlant 솔로 = finalColor 0) 의 groupPower 는 scrap 버킷에 합류.
    ///   → PowerPlant 는 colorBinding 과 무관하게 항상 정산 UI 에 표시되어 플레이어가 자기 발전소의 기여를 볼 수 있다.
    /// - 자투리의 기본치는 LastUngroupedCount (그룹화되지 않은 일반/Independent 블럭 1칸 = 1 GWh) 로 산출.
    ///   PowerPlant 는 CountsAsUngroupedBase 에서 이미 제외되므로 이중 집계되지 않는다.
    /// - 비-PowerPlant 효과가 제출한 pendingContributions 는 색상/자투리 버킷에 그대로 덧붙여진다.
    /// </summary>
    private SettlementData BuildSettlementData()
    {
        SettlementData data = new SettlementData
        {
            totalMoneyCap = ResourceManager.Instance != null ? ResourceManager.Instance.RemainingExchangeCap : 100f
        };

        float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
        if (baseRatio <= 0f) baseRatio = 1f;

        float groupedRed = 0f, groupedBlue = 0f, groupedGreen = 0f;
        float groupedRedMoney = 0f, groupedBlueMoney = 0f, groupedGreenMoney = 0f;
        // 색상 없는 그룹(OffPalette PowerPlant 등) 은 scrap 버킷에 합류시켜 SettlementUI 에 반드시 노출되게 한다.
        float groupedScrap = 0f, groupedScrapMoney = 0f;

        foreach (GroupInfo group in activeGroups)
        {
            // 특수 블럭 생산 횟수 훅 (효과 g) — ExtraRepeatCount 만큼 추가 1회 더 생산 취급.
            // 콜백이 0 개이면 ExtraRepeatCount=0 이 유지되어 기존 동작과 동일.
            var pc = new Special.Composition.Contexts.ProductionCountContext { Group = group, ExtraRepeatCount = 0 };
            EffectRuntime.Instance.ApplyProductionCountHooks(pc);
            float repeatFactor = 1f + Mathf.Max(0, pc.ExtraRepeatCount);
            float effectivePower = group.groupPower * repeatFactor;
            float effectiveMoney = group.estimatedMoneyGen * repeatFactor;

            switch (group.finalColor)
            {
                case 1: groupedRed   += effectivePower; groupedRedMoney   += effectiveMoney; break;
                case 2: groupedBlue  += effectivePower; groupedBlueMoney  += effectiveMoney; break;
                case 3: groupedGreen += effectivePower; groupedGreenMoney += effectiveMoney; break;
                default: groupedScrap += effectivePower; groupedScrapMoney += effectiveMoney; break;
            }
        }

        // 자투리(Scrap) 기본: ungrouped 일반/Independent 블럭 1칸 = 1 GWh. PowerPlant 는 CountsAsUngroupedBase 에서 이미 제외됨.
        float scrapBasePower = Mathf.Max(0f, (float)LastUngroupedCount) + groupedScrap;
        float scrapBaseMoney = Mathf.Max(0f, (float)LastUngroupedCount) / baseRatio + groupedScrapMoney;

        // 특수 블럭 기여분을 색상 버킷으로 분배. OffPalette/0 은 자투리에 합류.
        float contribRed = 0f, contribBlue = 0f, contribGreen = 0f, contribScrap = 0f;
        float contribRedMoney = 0f, contribBlueMoney = 0f, contribGreenMoney = 0f, contribScrapMoney = 0f;

        for (int i = 0; i < pendingContributions.Count; i++)
        {
            SpecialPowerContribution c = pendingContributions[i];
            switch (c.colorID)
            {
                case 1: contribRed += c.power; contribRedMoney += c.estimatedMoney; break;
                case 2: contribBlue += c.power; contribBlueMoney += c.estimatedMoney; break;
                case 3: contribGreen += c.power; contribGreenMoney += c.estimatedMoney; break;
                default: contribScrap += c.power; contribScrapMoney += c.estimatedMoney; break;
            }
        }

        data.redPower    = groupedRed    + contribRed;
        data.bluePower   = groupedBlue   + contribBlue;
        data.greenPower  = groupedGreen  + contribGreen;
        data.scrapPower  = scrapBasePower + contribScrap;

        data.redMoney    = groupedRedMoney    + contribRedMoney;
        data.blueMoney   = groupedBlueMoney   + contribBlueMoney;
        data.greenMoney  = groupedGreenMoney  + contribGreenMoney;
        data.scrapMoney  = scrapBaseMoney     + contribScrapMoney;

        return data;
    }

    /// <summary>
    /// 애니메이션 종료 후 호출되는 정산 마감 단계.
    /// 1) powerText 의 "어제 생산량" 갱신. 비-PowerPlant 기여분은 이미 CalculateTotalPower 에서
    ///    totalPower 에 합산되어 있으므로 여기서 별도로 더하지 않는다.
    /// 2) DailySettle 훅 (시각/타이머 등) 발화.
    /// 3) ResourceManager 의 일일 처리 위임 (AddCurrency(Electricity, totalPower) 로 지갑 반영).
    /// 4) 기여분 버퍼 정리. pendingContributions 는 SettlementData 색상 막대 표시 용도로만 유지되므로
    ///    지갑/yesterday 크레딧에는 다시 반영하지 않는다(이중 집계 방지).
    /// </summary>
    private void FinalizeDailySettlement()
    {
        CommitYesterdayProduction(totalPower);
        EffectRuntime.Instance.NotifyDailySettle();
        if (ResourceManager.Instance != null) ResourceManager.Instance.ProcessNextDay();

        pendingContributions.Clear();
    }

    /// <summary>
    /// 비-PowerPlant 효과가 자신의 일일 기여 전력을 PowerManager 에 제출하는 escape hatch.
    /// PowerPlant 블럭은 솔로 그룹 경로(EstimateLivePower → groupPower)로 자동 집계되므로 이 API 를 쓸 필요가 없다.
    /// 이 API 는 "Grouping 발전소에 추가 전력을 덧붙이는" 류의 향후 효과가 SettlementUI 색상 막대에 반영되고자 할 때 사용한다.
    /// 색상은 owner.definition 의 colorBinding 으로부터 유추 (Single/MultiPrimary → 포함된 주색, OffPalette → 자투리).
    /// 제출된 값은 SettlementUIController 해당 색 막대에 더해지고, AutoExchange 시 기본 환율로 돈으로 환산된다.
    /// </summary>
    public void SubmitSpecialContribution(SpecialBlockInstance owner, float power, string sourceTag = null)
    {
        if (owner == null || power <= 0f) return;

        SpecialBlockDefinition def = owner.definition;
        int colorID = ResolveContributionColorID(def);

        float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
        if (baseRatio <= 0f) baseRatio = 1f;

        pendingContributions.Add(new SpecialPowerContribution
        {
            owner = owner,
            definition = def,
            colorID = colorID,
            power = power,
            appliedExchangeRatio = baseRatio,
            estimatedMoney = power / baseRatio,
            sourceTag = sourceTag
        });
    }

    private static int ResolveContributionColorID(SpecialBlockDefinition def)
    {
        if (def == null) return 0;
        if (def.colorBinding == SpecialColorBinding.OffPalette) return 0;

        // Single 은 ResolveSingleColorID 와 동일한 우선순위(Red > Blue > Yellow).
        // MultiPrimary 는 표시용 대표색이 없으므로 같은 우선순위로 "첫 허용 주색" 을 선택한다.
        if ((def.includedPrimaries & ColorSet.Red)    != 0) return 1;
        if ((def.includedPrimaries & ColorSet.Blue)   != 0) return 2;
        if ((def.includedPrimaries & ColorSet.Yellow) != 0) return 3;
        return 0;
    }
}