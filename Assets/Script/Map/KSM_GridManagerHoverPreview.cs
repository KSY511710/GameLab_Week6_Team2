using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// GridManager의 기존 확장 시스템은 유지한 채,
/// "확장 가능한 후보 구역을 별도 프리뷰 타일로 표시"하는 partial 파일.
/// 
/// 핵심 목적:
/// 1. baseTile 원본 색/스프라이트에 끌려가지 않도록 별도 프리뷰 타일을 사용한다.
/// 2. 평상시 후보 타일은 회색 계열로 표시한다.
/// 3. hover 중인 타일은 더 진한 회색 + 노란 발광 outline으로 강조한다.
/// 4. hover 중 카메라 이동/축소는 기존 GridManager 옵션을 그대로 사용한다.
/// </summary>
public partial class GridManager
{
    [Header("KSM Preview Tile References")]
    [Tooltip("평상시 확장 가능 후보를 표시할 전용 타일. 비워두면 baseTile을 대신 사용한다.")]
    [SerializeField] private TileBase ksmPassivePreviewTile;

    [Tooltip("hover 중인 확장 후보를 표시할 전용 타일. 비워두면 ksmPassivePreviewTile, 그것도 없으면 baseTile을 사용한다.")]
    [SerializeField] private TileBase ksmHoverPreviewTile;

    [Header("KSM Passive Candidate Visual")]
    [Tooltip("평상시 확장 가능 후보 내부 색상.")]
    [SerializeField] private Color ksmPassiveCandidateFillColor = new Color(0.30f, 0.30f, 0.30f, 0.88f);

    [Tooltip("평상시 확장 가능 후보 외곽 색상.")]
    [SerializeField] private Color ksmPassiveCandidateBorderColor = new Color(0.24f, 0.24f, 0.24f, 0.96f);

    [Header("KSM Hover Candidate Visual")]
    [Tooltip("hover 중인 후보 내부 색상.")]
    [SerializeField] private Color ksmHoverPreviewFillColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);

    [Tooltip("hover 중인 후보 중앙 가이드 색상.")]
    [SerializeField] private Color ksmHoverPreviewGuideColor = new Color(0.34f, 0.34f, 0.34f, 1.00f);

    [Tooltip("hover 중 outline 발광 최소 색상.")]
    [SerializeField] private Color ksmHoverBorderPulseMinColor = new Color(1.00f, 0.78f, 0.08f, 0.95f);

    [Tooltip("hover 중 outline 발광 최대 색상.")]
    [SerializeField] private Color ksmHoverBorderPulseMaxColor = new Color(1.00f, 0.96f, 0.28f, 1.00f);

    [Tooltip("hover 중 outline 발광 속도.")]
    [SerializeField, Min(0.1f)] private float ksmHoverBorderPulseSpeed = 5.5f;

    /// <summary>
    /// 평상시 후보 타일로 표시된 셀 목록.
    /// </summary>
    private readonly List<Vector3Int> ksmPassiveCandidateCells = new List<Vector3Int>();

    /// <summary>
    /// hover 중 outline 발광 대상 셀 목록.
    /// </summary>
    private readonly List<Vector3Int> ksmHoverBorderCells = new List<Vector3Int>();

    /// <summary>
    /// hover 중 outline 발광 코루틴 핸들.
    /// </summary>
    private Coroutine ksmHoverBorderPulseRoutine;

    /// <summary>
    /// 현재 구조적으로 확장 가능한 모든 targetRegion을 평상시 회색 후보 상태로 다시 그린다.
    /// </summary>
    public void KSM_RefreshPassiveExpandCandidates()
    {
        KSM_ClearPassiveExpandCandidatesVisualOnly();

        if (groundTilemap == null || baseTile == null)
        {
            return;
        }

        List<Vector2Int> openedRegions = GetOpenedRegions();
        HashSet<Vector2Int> claimedTargetRegions = new HashSet<Vector2Int>();

        for (int i = 0; i < openedRegions.Count; i++)
        {
            Vector2Int sourceRegion = openedRegions[i];

            KSM_TryDrawPassiveCandidate(sourceRegion, KSM_ExpandDirection.North, claimedTargetRegions);
            KSM_TryDrawPassiveCandidate(sourceRegion, KSM_ExpandDirection.South, claimedTargetRegions);
            KSM_TryDrawPassiveCandidate(sourceRegion, KSM_ExpandDirection.West, claimedTargetRegions);
            KSM_TryDrawPassiveCandidate(sourceRegion, KSM_ExpandDirection.East, claimedTargetRegions);
        }
    }

    /// <summary>
    /// 평상시 후보 타일 표시를 모두 제거한다.
    /// </summary>
    public void KSM_ClearPassiveExpandCandidates()
    {
        KSM_ClearPassiveExpandCandidatesVisualOnly();
    }

    /// <summary>
    /// hover 중인 targetRegion을 더 강하게 강조해서 보여준다.
    /// </summary>
    public void KSM_ShowExpansionHoverPreview(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null)
        {
            return;
        }

        if (!HasStructuralExpansionPort(sourceRegion, direction))
        {
            KSM_ClearExpansionHoverPreview();
            return;
        }

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);

        if (isPreviewActive && hasPreviewRegion && currentPreviewRegion == targetRegion)
        {
            return;
        }

        KSM_ClearExpansionHoverPreviewVisualOnly();
        KSM_RefreshPassiveExpandCandidates();

        isPreviewActive = true;
        hasPreviewRegion = true;
        currentPreviewRegion = targetRegion;

        RectInt previewRect = GetRegionRect(targetRegion);

        int centerX = previewRect.xMin + (previewRect.width / 2);
        int centerY = previewRect.yMin + (previewRect.height / 2);

        TileBase hoverTileToUse = KSM_GetHoverPreviewTile();

        for (int x = previewRect.xMin; x < previewRect.xMax; x++)
        {
            for (int y = previewRect.yMin; y < previewRect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                bool isBorder =
                    x == previewRect.xMin ||
                    x == previewRect.xMax - 1 ||
                    y == previewRect.yMin ||
                    y == previewRect.yMax - 1;

                bool isGuide =
                    direction == KSM_ExpandDirection.North || direction == KSM_ExpandDirection.South
                        ? x == centerX
                        : y == centerY;

                Color targetColor = ksmHoverPreviewFillColor;

                if (isBorder)
                {
                    targetColor = ksmHoverBorderPulseMinColor;
                    ksmHoverBorderCells.Add(cell);
                }
                else if (isGuide)
                {
                    targetColor = ksmHoverPreviewGuideColor;
                }

                groundTilemap.SetTile(cell, hoverTileToUse);
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, targetColor);
                previewCells.Add(cell);
            }
        }

        RefreshTilesAroundRect(previewRect);
        KSM_RestartHoverBorderPulse();

        if (moveCameraOnHoverPreview)
        {
            UpdatePreviewCameraTarget(previewRect, false);
        }
    }

    /// <summary>
    /// hover 강조 상태를 제거하고, 필요하면 카메라를 원래 위치로 복귀시킨다.
    /// </summary>
    public void KSM_ClearExpansionHoverPreview()
    {
        KSM_ClearExpansionHoverPreview(moveCameraOnHoverPreview);
    }

    /// <summary>
    /// hover 강조 상태 제거 내부 구현.
    /// </summary>
    private void KSM_ClearExpansionHoverPreview(bool restoreCamera)
    {
        bool hadPreview = isPreviewActive;

        KSM_ClearExpansionHoverPreviewVisualOnly();

        isPreviewActive = false;
        hasPreviewRegion = false;
        currentPreviewRegion = new Vector2Int(int.MinValue, int.MinValue);

        KSM_RefreshPassiveExpandCandidates();

        if (restoreCamera && hadPreview)
        {
            UpdateCameraTarget(false);
        }
    }

    /// <summary>
    /// hover 전용 강조 셀만 제거한다.
    /// </summary>
    private void KSM_ClearExpansionHoverPreviewVisualOnly()
    {
        KSM_StopHoverBorderPulse();

        if (groundTilemap == null)
        {
            previewCells.Clear();
            ksmHoverBorderCells.Clear();
            return;
        }

        if (previewCells.Count == 0)
        {
            ksmHoverBorderCells.Clear();
            return;
        }

        List<Vector3Int> oldHoverCells = new List<Vector3Int>(previewCells);

        for (int i = 0; i < previewCells.Count; i++)
        {
            Vector3Int cell = previewCells[i];

            if (!IsWorldCellUnlocked(cell))
            {
                groundTilemap.SetTile(cell, null);
            }
            else
            {
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, Color.white);
            }
        }

        previewCells.Clear();
        ksmHoverBorderCells.Clear();
        RefreshTilesAroundCells(oldHoverCells);
    }

    /// <summary>
    /// 평상시 후보 타일만 제거한다.
    /// </summary>
    private void KSM_ClearPassiveExpandCandidatesVisualOnly()
    {
        if (groundTilemap == null)
        {
            ksmPassiveCandidateCells.Clear();
            return;
        }

        if (ksmPassiveCandidateCells.Count == 0)
        {
            return;
        }

        List<Vector3Int> oldPassiveCells = new List<Vector3Int>(ksmPassiveCandidateCells);

        for (int i = 0; i < ksmPassiveCandidateCells.Count; i++)
        {
            Vector3Int cell = ksmPassiveCandidateCells[i];

            if (!IsWorldCellUnlocked(cell))
            {
                groundTilemap.SetTile(cell, null);
            }
            else
            {
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, Color.white);
            }
        }

        ksmPassiveCandidateCells.Clear();
        RefreshTilesAroundCells(oldPassiveCells);
    }

    /// <summary>
    /// 특정 targetRegion을 평상시 회색 후보 타일로 그린다.
    /// </summary>
    private void KSM_TryDrawPassiveCandidate(
        Vector2Int sourceRegion,
        KSM_ExpandDirection direction,
        HashSet<Vector2Int> claimedTargetRegions)
    {
        if (!HasStructuralExpansionPort(sourceRegion, direction))
        {
            return;
        }

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);

        if (!claimedTargetRegions.Add(targetRegion))
        {
            return;
        }

        RectInt rect = GetRegionRect(targetRegion);
        TileBase passiveTileToUse = KSM_GetPassivePreviewTile();

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                bool isBorder =
                    x == rect.xMin ||
                    x == rect.xMax - 1 ||
                    y == rect.yMin ||
                    y == rect.yMax - 1;

                Color targetColor = isBorder ? ksmPassiveCandidateBorderColor : ksmPassiveCandidateFillColor;

                groundTilemap.SetTile(cell, passiveTileToUse);
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, targetColor);
                ksmPassiveCandidateCells.Add(cell);
            }
        }

        RefreshTilesAroundRect(rect);
    }

    /// <summary>
    /// 평상시 후보 표시용 타일을 반환한다.
    /// </summary>
    private TileBase KSM_GetPassivePreviewTile()
    {
        if (ksmPassivePreviewTile != null)
        {
            return ksmPassivePreviewTile;
        }

        return baseTile;
    }

    /// <summary>
    /// hover 강조 표시용 타일을 반환한다.
    /// </summary>
    private TileBase KSM_GetHoverPreviewTile()
    {
        if (ksmHoverPreviewTile != null)
        {
            return ksmHoverPreviewTile;
        }

        if (ksmPassivePreviewTile != null)
        {
            return ksmPassivePreviewTile;
        }

        return baseTile;
    }

    /// <summary>
    /// hover outline 발광 코루틴 재시작.
    /// </summary>
    private void KSM_RestartHoverBorderPulse()
    {
        KSM_StopHoverBorderPulse();

        if (!isActiveAndEnabled || ksmHoverBorderCells.Count == 0)
        {
            return;
        }

        ksmHoverBorderPulseRoutine = StartCoroutine(KSM_HoverBorderPulseRoutine());
    }

    /// <summary>
    /// hover outline 발광 코루틴 정지.
    /// </summary>
    private void KSM_StopHoverBorderPulse()
    {
        if (ksmHoverBorderPulseRoutine == null)
        {
            return;
        }

        StopCoroutine(ksmHoverBorderPulseRoutine);
        ksmHoverBorderPulseRoutine = null;
    }

    /// <summary>
    /// hover 중인 후보 외곽선을 노란색으로 발광시킨다.
    /// </summary>
    private IEnumerator KSM_HoverBorderPulseRoutine()
    {
        while (isPreviewActive && groundTilemap != null && ksmHoverBorderCells.Count > 0)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * ksmHoverBorderPulseSpeed) + 1f) * 0.5f;
            Color borderColor = Color.Lerp(ksmHoverBorderPulseMinColor, ksmHoverBorderPulseMaxColor, pulse);

            for (int i = 0; i < ksmHoverBorderCells.Count; i++)
            {
                Vector3Int cell = ksmHoverBorderCells[i];

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, borderColor);
            }

            yield return null;
        }

        ksmHoverBorderPulseRoutine = null;
    }
}