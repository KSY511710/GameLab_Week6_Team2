using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Special.Data;
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

    public bool IsAnimating { get; private set; }

    /// <summary>현재 보드의 라이브 총 발전량이 변경되었을 때 발화. Skip 가용성 재평가 등에 사용.</summary>
    public static event Action OnTotalPowerChanged;

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

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];

                if (cell != null && IsEligibleForGrouping(cell) && !cell.isGrouped && !visited[x, y])
                {
                    List<Vector2Int> cluster = GetUnlockedCluster(x, y, board, visited, width, height);

                    // 1. 먼저 10칸 이상 모였는지 확인
                    if (cluster.Count >= groupMinSize)
                    {
                        // 🌟 '부품의 종류'를 세기 위해 HashSet(중복 방지 주머니)을 만듭니다.
                        HashSet<int> uniqueParts = new HashSet<int>();

                        foreach (Vector2Int pos in cluster)
                        {
                            BlockData blockInCluster = board[pos.x, pos.y];

                            if (blockInCluster.attribute.shapeID > 0)
                            {
                                // HashSet은 똑같은 부품(shapeID)이 여러 번 들어와도 알아서 1개로 칩니다!
                                uniqueParts.Add(blockInCluster.attribute.shapeID);
                            }
                        }

                        // 3. 서로 다른 종류의 부품이 3종류 이상 포함되어 있을 때만 그룹 확정(Lock)!
                        if (uniqueParts.Count >= groupMinpart)
                        {
                            CreateNewGroup(cluster, board);
                        }
                    }
                }
            }
        }
    }

    // 그룹에 참여할 수 있는 셀인지 판정. Independent role 특수 블럭은 BFS 에서 제외.
    private static bool IsEligibleForGrouping(BlockData cell)
    {
        if (cell.attribute.colorID <= 0) return false;
        SpecialBlockDefinition def = cell.attribute.specialDef;
        if (def != null && def.role == SpecialBlockRole.Independent) return false;
        return true;
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
        PowerCalculationContext ctx = new PowerCalculationContext
        {
            BaseProductionRaw = baseProduction,
            UniquePartsRaw = uniquePartsCount,
            CompletionMultiplierRaw = completionMultiplier,
            ColorMultiplierRaw = colorMultiplier,
            ClusterPositions = cluster
        };
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
            clusterPositions = new List<Vector2Int>(cluster)
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

        if (PowerAnimationSequencer.Instance != null)
        {
            PowerAnimationSequencer.Instance.EnqueueAnimation(newGroup);
        }
    }

    public void CalculateTotalPower(BlockData[,] board, int width, int height)
    {
        float calculatedTotalPower = 0;
        foreach (GroupInfo group in activeGroups)
        {
            calculatedTotalPower += group.groupPower;
        }

        // 미그룹 블럭 카운트와 비주얼 참조를 같은 패스에서 수집
        LastUngroupedVisuals.Clear();
        int ungroupedBlocks = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];
                if (cell == null || cell.attribute.colorID <= 0 || cell.isGrouped) continue;

                ungroupedBlocks++;
                if (cell.blockObject != null)
                {
                    PlacedBlockVisual v = cell.blockObject.GetComponent<PlacedBlockVisual>();
                    if (v != null) LastUngroupedVisuals.Add(v);
                }
            }
        }
        LastUngroupedCount = ungroupedBlocks;

        int previous = totalPower;
        totalPower = (int)(calculatedTotalPower + ungroupedBlocks);

        // 라이브 총합은 더 이상 powerText에 즉시 출력하지 않는다 (Feature 1).
        // 변동이 있을 때만 외부 구독자(예: ResourceManager)에게 알림.
        if (totalPower != previous)
        {
            OnTotalPowerChanged?.Invoke();
        }
        UpdateDisplayedPowerUI();
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
            $"<color=#00FFFF><size=30><b>Live Power: {totalPower} GWh</b></size></color>\n" +
            $"<size=20>(Completed Groups: {activeGroups.Count})</size>";
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

                // 블록이 없거나 연결된 실제 오브젝트가 없으면 패스!
                if (myData == null || myData.blockObject == null) continue;

                PlacedBlockVisual visual = myData.blockObject.GetComponent<PlacedBlockVisual>();
                if (visual == null) continue;

                // 내 그룹 번호 기억 (그룹이 없으면 0으로 취급)
                int myGroupID = myData.isGrouped ? myData.groupID : 0;

                // 4방향 이웃 검사! (배열 범위를 벗어나거나, 비어있거나, 나랑 그룹ID가 다르면 선을 켬)
                bool showTop = (y + 1 >= height) || (board[x, y + 1] == null) || (board[x, y + 1].groupID != myGroupID);
                bool showBottom = (y - 1 < 0) || (board[x, y - 1] == null) || (board[x, y - 1].groupID != myGroupID);
                bool showLeft = (x - 1 < 0) || (board[x - 1, y] == null) || (board[x - 1, y].groupID != myGroupID);
                bool showRight = (x + 1 >= width) || (board[x + 1, y] == null) || (board[x + 1, y].groupID != myGroupID);

                // 시각 컨트롤러에게 선을 켜고 끄라고 명령 전달
                visual.UpdateOutline(showTop, showBottom, showLeft, showRight);
            }
        }
    }
    public void ProceedToNextDay()
    {
        if (IsAnimating) return;

        SettlementData data = new SettlementData();
        data.totalMoneyCap = ResourceManager.Instance != null ? ResourceManager.Instance.RemainingExchangeCap : 100f;

        // 1. 빨/파/초 그룹 생산량 합산
        foreach (GroupInfo group in activeGroups)
        {
            if (group.finalColor == 1) { data.redPower += group.groupPower; data.redMoney += group.estimatedMoneyGen; }
            else if (group.finalColor == 2) { data.bluePower += group.groupPower; data.blueMoney += group.estimatedMoneyGen; }
            else if (group.finalColor == 3) { data.greenPower += group.groupPower; data.greenMoney += group.estimatedMoneyGen; }
        }

        // 🌟 2. 자투리(Scrap) 생산량 및 예상 수익 계산
        // 전체 전력에서 그룹이 생산한 전력을 모두 빼면 자투리 전력이 남습니다.
        data.scrapPower = totalPower - (data.redPower + data.bluePower + data.greenPower);

        // 자투리 전력은 특별 우대 환율 없이 '기본 환율'로만 돈으로 바뀝니다.
        float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
        data.scrapMoney = data.scrapPower / baseRatio;

        // 3. 애니메이션 재생
        // NotifyDailySettle 은 두 경로 모두에서 발화돼야 ProduceFromEmptyCellsEffect 같은
        // DailySettle 훅이 정상 동작한다 (SettlementUI 가 있을 때만 동작하면 안 됨).
        if (SettlementUIController.Instance != null)
        {
            SetAnimating(true);

            SettlementUIController.Instance.PlaySettlementAnimation(data, () =>
            {
                CommitYesterdayProduction(totalPower);
                EffectRuntime.Instance.NotifyDailySettle();
                if (ResourceManager.Instance != null) ResourceManager.Instance.ProcessNextDay();
                SetAnimating(false);
            });
        }
        else
        {
            CommitYesterdayProduction(totalPower);
            EffectRuntime.Instance.NotifyDailySettle();
            if (ResourceManager.Instance != null) ResourceManager.Instance.ProcessNextDay();
        }
    }
}