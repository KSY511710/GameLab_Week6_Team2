using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance;
    public int maxEfficiencySize = 10;
    public int elecexpenses = 2;
    public float bounus = 0.1f;
    [Header("UI References")]
    public TextMeshProUGUI powerText;

    private int totalPower = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void CalculateTotalPower(int[,] boardData, int width, int height)
    {
        int clusterCount = 0;      // 덩어리의 개수
        int basePowerSum = 0;      // 덩어리들의 기저 전력 합계
        int totalBuildingCells = 0; // 전체 설치된 칸 수 계산 로직 필요
        bool[,] visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 발전소가 있고 아직 방문하지 않은 새로운 덩어리 발견!
                if (boardData[x, y] > 0 && !visited[x, y])
                {
                    // 1. 덩어리 크기(n) 측정
                    int size = GetClusterSize(x, y, boardData, visited, width, height);

                    int clusterBasePower = 0;
                    for (int i = 1; i <= size; i++)
                    {
                        if (i <= maxEfficiencySize) clusterBasePower += i; // 10칸까지는 정상 효율 (1, 2, 3...)
                        else clusterBasePower += 1;          // 10칸 넘어가면 추가 전력 고정 (효율 급감)
                    }

                    // 3. 전체 합계에 더하고, 덩어리 개수 카운트 증가
                    basePowerSum += clusterBasePower;
                    totalBuildingCells += size;
                    clusterCount++;
                }
            }
        }
        float multiplier = 1f + (clusterCount * bounus);
        int calculatedPower = (int)(basePowerSum * multiplier);

        // 📌 2. 유지비 차감 (한 칸당 10GWh 소모라고 가정)
        int maintenanceCost = totalBuildingCells * elecexpenses;

        // 최종 결과: (생산량 - 유지비)
        totalPower = (int)calculatedPower - maintenanceCost;

        // 전력이 마이너스가 되지 않도록 방지
        if (totalPower < 0) totalPower = 0;

        UpdateUI(calculatedPower, maintenanceCost, clusterCount, basePowerSum);
    }

    private int GetClusterSize(int startX, int startY, int[,] boardData, bool[,] visited, int width, int height)
    {
        int size = 0;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();
            size++;

            foreach (Vector2Int dir in directions)
            {
                int nextX = curr.x + dir.x;
                int nextY = curr.y + dir.y;

                if (nextX >= 0 && nextX < width && nextY >= 0 && nextY < height)
                {
                    if (boardData[nextX, nextY] > 0 && !visited[nextX, nextY])
                    {
                        visited[nextX, nextY] = true;
                        queue.Enqueue(new Vector2Int(nextX, nextY));
                    }
                }
            }
        }
        return size;
    }

    private void UpdateUI(int production, int maintenance, int clusters, int baseSum)
    {
        if (powerText != null)
        {
            // Rich Text를 사용하여 가독성을 높입니다.
            // <color> 태그로 생산은 노란색, 유지비는 빨간색으로 표시합니다.
            powerText.text =
                $"<color=yellow>Production: +{production}</color> GWh\n" +
                $"<color=red>Maintenance: -{maintenance}</color> GWh\n" +
                $"<size=30><b>Total: {totalPower} GWh</b></size>\n" +
                $"<size=20>({baseSum} * {1f + clusters * 0.1f:F1} bonus)</size>";
        }
    }
    public int GetTotalPower()
    {
        return totalPower; // 현재 계산되어 있는 최종 전력 생산량 반환
    }
}