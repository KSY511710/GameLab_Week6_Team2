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
}

public class GroupInfo
{
    public int groupID;
    public int blockSize;
    public int finalColor;
    public int finalShape;
    public int formationMultiplier;
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
    public int groupMinSize = 10; // 인스펙터에서 수정 가능 (10칸부터 그룹)

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
        Dictionary<int, int> shapeCounts = new Dictionary<int, int>();

        foreach (Vector2Int pos in cluster)
        {
            BlockData cell = board[pos.x, pos.y];
            cell.isGrouped = true;
            cell.groupID = nextGroupID;

            int c = cell.attribute.colorID;
            if (colorCounts.ContainsKey(c)) colorCounts[c]++;
            else colorCounts[c] = 1;

            int s = cell.attribute.shapeID;
            if (shapeCounts.ContainsKey(s)) shapeCounts[s]++;
            else shapeCounts[s] = 1;
        }

        int dominantColor = colorCounts.OrderByDescending(x => x.Value).First().Key;
        int dominantShape = shapeCounts.OrderByDescending(x => x.Value).First().Key;
        int bonusMultiplier = FormationDetector.GetFormationMultiplier(cluster);

        GroupInfo newGroup = new GroupInfo
        {
            groupID = nextGroupID,
            blockSize = cluster.Count,
            finalColor = dominantColor,
            finalShape = dominantShape,
            formationMultiplier = bonusMultiplier

        };

        activeGroups.Add(newGroup);
        nextGroupID++;

        Debug.Log($"새 그룹 확정! ID:{newGroup.groupID} | 대표색상:{newGroup.finalColor} | 대표모양:{newGroup.finalShape}");
    }

    // 2. 전력 계산 (1칸당 1전력 일치시마다 *2)
    public void CalculateTotalPower(BlockData[,] board, int width, int height)
    {
        int calculatedTotalPower = 0;
        Dictionary<int, GroupInfo> groupLookup = activeGroups.ToDictionary(g => g.groupID);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData cell = board[x, y];

                if (cell == null || cell.attribute.colorID <= 0) continue;

                int powerPerCell = 1;

                if (cell.isGrouped && groupLookup.ContainsKey(cell.groupID))
                {
                    powerPerCell *= 2;
                    GroupInfo myGroup = groupLookup[cell.groupID];

                    if (cell.attribute.colorID == myGroup.finalColor) powerPerCell *= 2;
                    if (cell.attribute.shapeID > 0 && cell.attribute.shapeID == myGroup.finalShape) powerPerCell *= 2;
                    powerPerCell *= myGroup.formationMultiplier;
                }

                calculatedTotalPower += powerPerCell;
            }
        }

        totalPower = calculatedTotalPower;
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