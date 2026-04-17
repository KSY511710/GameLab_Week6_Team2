using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

// ==========================================
//   [데이터 명찰 클래스들]
// ==========================================
[System.Serializable]
public class BlockAttribute
{
    public int colorID;  // 색상 (1: 빨강, 2: 파랑 등 / 0: 색상 없음)
    public int shapeID;  // 모양 (당장 안 써도 0으로 기본값 세팅)

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

    public List<GroupInfo> activeGroups = new List<GroupInfo>();
    private int nextGroupID = 1;
    private int totalPower = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

                // 빈 칸이 아니고, 색상이 있으며(>0), 아직 그룹이 아닌 블록 탐색
                if (cell != null && cell.attribute.colorID > 0 && !cell.isGrouped && !visited[x, y])
                {
                    List<Vector2Int> cluster = GetUnlockedCluster(x, y, board, visited, width, height);

                    // 10칸 이상 모였다면 그룹으로 확정(Lock)
                    if (cluster.Count >= groupMinSize)
                    {
                        CreateNewGroup(cluster, board);
                    }
                }
            }
        }
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

                    if (nextCell != null && nextCell.attribute.colorID > 0 && !nextCell.isGrouped && !visited[nextX, nextY])
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

        float finalPower = (baseProduction + uniquePartsCount) * completionMultiplier * colorMultiplier;

        // --- 정보 저장 ---
        int dominantColor = colorCounts.OrderByDescending(x => x.Value).First().Key;

        GroupInfo newGroup = new GroupInfo
        {
            groupID = nextGroupID,
            blockSize = cluster.Count,
            finalColor = dominantColor,
            formationMultiplier = shapeBonus,
            groupPower = finalPower
        };

        // =========================================================
        // 🚨 여기에 복사해서 붙여넣으세요! 🚨
        // =========================================================
        Color dominantRealColor = Color.white;
        if (dominantColor == 1) dominantRealColor = new Color(1f, 0.2f, 0.2f);
        else if (dominantColor == 2) dominantRealColor = new Color(0.2f, 0.4f, 1f);
        else if (dominantColor == 3) dominantRealColor = new Color(1f, 0.9f, 0.2f);

        List<PlacedBlockVisual> currentGroupVisuals = new List<PlacedBlockVisual>();

        foreach (Vector2Int pos in cluster)
        {
            GameObject placedBlockObj = board[pos.x, pos.y].blockObject;
            if (placedBlockObj != null)
            {
                PlacedBlockVisual visualControl = placedBlockObj.GetComponent<PlacedBlockVisual>();
                if (visualControl != null)
                {
                    currentGroupVisuals.Add(visualControl); // 명단에 추가!
                }
            }
        }

        foreach (PlacedBlockVisual visualControl in currentGroupVisuals)
        {
            // 팀원 명단(currentGroupVisuals)을 함께 넘겨줍니다!
            visualControl.SetGroupState(true, dominantRealColor, currentGroupVisuals);
        }
        // =========================================================

        string debugMsg = $"<color=#00FFFF><b>[전력 정산 영수증 - 그룹 {nextGroupID}]</b></color>\n" +
                          $"1. 크기 및 다양성 : 기본 {baseProduction}칸 + 부품 {uniquePartsCount}종 = <b>{baseProduction + uniquePartsCount}</b>\n" +
                          $"2. 형태 보너스 : 기본 2 + 모양 {shapeBonus} = <b>x {completionMultiplier}</b>\n" +
                          $"3. 색상 순도 : 주력 {maxColorCount}칸 / 불순물 {restColorCount}칸 = <b>x {colorMultiplier:F2}</b>\n" +
                          $"<color=#FFFF00><b>▶ 최종 생산량 : ({baseProduction} + {uniquePartsCount}) * {completionMultiplier} * {colorMultiplier:F2} = {finalPower} GWh</b></color>";

        Debug.Log(debugMsg);

        activeGroups.Add(newGroup);
        nextGroupID++;
    }

    public void CalculateTotalPower(BlockData[,] board, int width, int height)
    {
        float calculatedTotalPower = 0;
        int ungroupedBlocks = 0;

        // 📌 최적화: 그룹 전력만 쓱 더합니다.
        foreach (GroupInfo group in activeGroups)
        {
            calculatedTotalPower += group.groupPower;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];
                if (cell != null && cell.attribute.colorID > 0 && !cell.isGrouped)
                {
                    ungroupedBlocks++;
                }
            }
        }

        totalPower = (int)(calculatedTotalPower + ungroupedBlocks);
        UpdateUI(totalPower, activeGroups.Count);
    }
    private void UpdateUI(int total, int groupCount)
    {
        if (powerText != null)
        {
            powerText.text =
                $"<color=yellow><size=30><b>Total Power: {total} GWh</b></size></color>\n" +
                $"<size=20>(Completed Groups: {groupCount})</size>";
        }
    }

    public int GetTotalPower()
    {
        return totalPower;
    }
}