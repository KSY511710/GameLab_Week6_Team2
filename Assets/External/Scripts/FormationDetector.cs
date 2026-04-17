using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class FormationDetector
{
    public static int GetFormationMultiplier(List<Vector2Int> cluster)
    {
        // 📌 판정 순서가 중요합니다! (특수한 모양부터 검사)
        if (IsLine(cluster))
        {
            Debug.Log("📏 [1자 모양] 보너스 2배!");
            return 2;
        }
        if (IsCross(cluster))
        {
            Debug.Log("➕ [십자 모양] 보너스 3배!");
            return 3;
        }
        if (IsLShape(cluster))
        {
            Debug.Log("📐 [ㄴ/ㄱ자 모양] 보너스 3배!");
            return 3;
        }
        if (IsSquare(cluster))
        {
            Debug.Log("🧊 [정사각형] 보너스 2배!");
            return 2;
        }

        return 1; // 아무 모양도 아니면 기본 1배
    }

    // =========================================================
    // 📌 [공통 데이터 추출기] 가로/세로 길이 및 각 줄마다 블록이 몇 개인지 셉니다.
    // =========================================================
    private static void GetBoundsInfo(List<Vector2Int> cluster,
        out int width, out int height,
        out int minX, out int maxX, out int minY, out int maxY,
        out Dictionary<int, int> rowCounts, out Dictionary<int, int> colCounts)
    {
        minX = cluster.Min(p => p.x);
        maxX = cluster.Max(p => p.x);
        minY = cluster.Min(p => p.y);
        maxY = cluster.Max(p => p.y);

        width = maxX - minX + 1;
        height = maxY - minY + 1;

        rowCounts = new Dictionary<int, int>();
        colCounts = new Dictionary<int, int>();

        foreach (var p in cluster)
        {
            if (!rowCounts.ContainsKey(p.y)) rowCounts[p.y] = 0;
            rowCounts[p.y]++;

            if (!colCounts.ContainsKey(p.x)) colCounts[p.x] = 0;
            colCounts[p.x]++;
        }
    }

    // =========================================================
    // 1. 1자 모양 (가로든 세로든 너비나 높이가 1이면 통과)
    // =========================================================
    private static bool IsLine(List<Vector2Int> cluster)
    {
        GetBoundsInfo(cluster, out int w, out int h, out _, out _, out _, out _, out _, out _);

        return (w == 1 || h == 1) && (cluster.Count == Mathf.Max(w, h));
    }

    // =========================================================
    // 2. 십자 모양 (가운데 교차)
    // =========================================================
    private static bool IsCross(List<Vector2Int> cluster)
    {
        GetBoundsInfo(cluster, out int w, out int h, out int minX, out int maxX, out int minY, out int maxY, out var rowCounts, out var colCounts);

        // 꽉 찬 가로줄과 세로줄 찾기
        var fullRows = rowCounts.Where(kvp => kvp.Value == w).ToList();
        var fullCols = colCounts.Where(kvp => kvp.Value == h).ToList();

        // 십자가는 무조건 꽉 찬 줄이 가로 1개, 세로 1개여야 함
        if (fullRows.Count != 1 || fullCols.Count != 1) return false;

        // 잔가지가 없는지 확인 (블록 수 = 가로 + 세로 - 교차점 1개)
        if (cluster.Count != w + h - 1) return false;

        int crossRow = fullRows[0].Key;
        int crossCol = fullCols[0].Key;

        // 📌 십자가이려면 교차하는 줄이 테두리(끝)에 있으면 안 됨
        bool isRowInMiddle = (crossRow > minY && crossRow < maxY);
        bool isColInMiddle = (crossCol > minX && crossCol < maxX);

        return isRowInMiddle && isColInMiddle;
    }

    // =========================================================
    // 3. ㄴ/ㄱ자 모양 (모든 방향 회전 커버)
    // =========================================================
    private static bool IsLShape(List<Vector2Int> cluster)
    {
        GetBoundsInfo(cluster, out int w, out int h, out int minX, out int maxX, out int minY, out int maxY, out var rowCounts, out var colCounts);

        // 📌 [추가된 핵심 조건] 가로 길이(w)와 세로 길이(h)가 다르면 가차 없이 탈락시킵니다!
        if (w != h) return false;

        var fullRows = rowCounts.Where(kvp => kvp.Value == w).ToList();
        var fullCols = colCounts.Where(kvp => kvp.Value == h).ToList();

        if (fullRows.Count != 1 || fullCols.Count != 1) return false;
        if (cluster.Count != w + h - 1) return false;

        int lRow = fullRows[0].Key;
        int lCol = fullCols[0].Key;

        bool isRowAtEdge = (lRow == minY || lRow == maxY);
        bool isColAtEdge = (lCol == minX || lCol == maxX);

        return isRowAtEdge && isColAtEdge;
    }

    // =========================================================
    // 4. 정사각형 
    // =========================================================
    private static bool IsSquare(List<Vector2Int> cluster)
    {
        GetBoundsInfo(cluster, out int w, out int h, out _, out _, out _, out _, out _, out _);
        return (w == h) && (cluster.Count == w * h);
    }
}