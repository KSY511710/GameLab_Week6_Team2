using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class FormationDetector
{
    // 좌표 리스트를 넘겨받아 최종 보너스 배율을 반환하는 함수
    public static int GetFormationMultiplier(List<Vector2Int> cluster)
    {
        // 1. 정사각형 판정 (보너스 2배)
        if (IsSquare(cluster))
        {
            return 3;
        }
        return 1;
    }


    private static bool IsSquare(List<Vector2Int> cluster)
    {
        int minX = cluster.Min(p => p.x);
        int maxX = cluster.Max(p => p.x);
        int minY = cluster.Min(p => p.y);
        int maxY = cluster.Max(p => p.y);

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        return (width == height) && (cluster.Count == width * height);
    }
}