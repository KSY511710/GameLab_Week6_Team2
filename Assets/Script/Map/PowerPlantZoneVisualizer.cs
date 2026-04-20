using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 발전소(그룹화된 블럭) 의 점유 영역과 그 주변 "인접 설치 금지" 영역을 overlay Tilemap 위에 시각화한다.
///
/// 설계 개요
/// - GridManager 는 <see cref="GridManager.OnBoardMutated"/> 만 발화하고 이 컴포넌트 존재를 모른다.
///   → 시각 레이어를 추가/교체/삭제해도 GridManager 를 건드릴 필요가 없다.
/// - 이 컴포넌트는 GridManager 의 public API (width/height, GetBlockAtArrayIndex, ArrayIndexToWorldCell,
///   IsWorldCellUnlocked) 만 읽는다. 내부 boardData 에는 접근하지 않는다.
/// - 오버레이는 전용 Tilemap 에 그린다. groundTilemap 의 baseTile / 확장 프리뷰 로직과 겹치지 않도록
///   분리했다. 이 컴포넌트가 overlay Tilemap 의 유일한 writer 라는 전제를 유지한다.
///
/// 발전소별 독립 시각화
/// - 표준 RuleTile 은 이웃 매칭을 "같은 타일 에셋인가" 로만 본다 → 여러 발전소의 링이 합쳐져 보이는 문제가 있다.
/// - 해결: 본 컴포넌트는 타일을 깔 때 <see cref="PowerPlantTileOwnership"/> 에 "이 셀은 groupID N 의 것" 을 함께 기록한다.
/// - <see cref="PowerPlantGroupRuleTile"/> 이 평가 시 owner 를 참조해 그룹 단위로 매칭을 분리한다.
/// - forbiddenRuleTile 슬롯에 표준 RuleTile 을 넣어도 안전하게 동작한다 (소유권은 무시되고 기존 동작 유지 = 폴백).
///
/// 시각 규칙 (요구사항 대응)
/// 1) 그룹화된 셀(= 완성된 발전소의 footprint 또는 PowerPlant 솔로 footprint) 은
///    <see cref="occupiedTile"/> 로 바닥을 교체해 "발전소가 점유한 영역" 임을 명확히 드러낸다.
/// 2) 그룹화된 셀의 8방향 이웃 중 "열려 있고, 아직 그룹에 속하지 않은" 셀은
///    <see cref="forbiddenRuleTile"/>(RuleTile) 로 덮어 "이 칸은 설치 불가" 규칙을 표시한다.
///    RuleTile 이므로 이웃 상태에 따라 모서리/직선 스프라이트가 자동 선택된다.
/// 3) 실제 배치 판정(<see cref="GridManager.CanPlaceShape"/>) 이 사용하는 8방향 + isGrouped 규칙과
///    동일 조건으로 overlay 를 만든다 → 판정과 시각이 절대 어긋나지 않는다.
///
/// 확장 포인트
/// - 인접 범위(1칸 → N칸) 규칙이 바뀌면 <see cref="ForbiddenOffsets"/> 만 교체.
/// - 8방향/4방향/기타 패턴이 필요하면 <see cref="ForbiddenOffsets"/> 만 교체.
/// - 발전소 역할별로 다른 타일을 쓰고 싶다면 그룹 순회 시 BlockData 를 참조해 타일을 분기하면 된다.
/// - 그룹 간 링이 겹치는 셀의 처리 정책은 <see cref="ForbiddenOverlapPolicy"/> 에서 조정.
/// </summary>
[DisallowMultipleComponent]
public sealed class PowerPlantZoneVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GridManager. 보드 상태(그룹/열린 구역) 질의 출처.")]
    [SerializeField] private GridManager grid;

    [Tooltip("발전소 점유 / 인접 금지 영역을 그릴 전용 Tilemap. groundTilemap 위 sorting layer 에 두는 것을 권장.")]
    [SerializeField] private Tilemap overlayTilemap;

    [Header("Tiles")]
    [Tooltip("그룹화된 블럭이 점유한 셀에 깔릴 바닥 타일. 발전소 구역 강조용.")]
    [SerializeField] private TileBase occupiedTile;

    [Tooltip("발전소에 인접해 설치가 금지된 셀에 깔릴 RuleTile. 이웃 상태에 따라 자동 스프라이트 선택.")]
    [SerializeField] private TileBase forbiddenRuleTile;

    [Header("Behavior")]
    [Tooltip("true 면 설정된 참조가 누락됐을 때 경고 로그를 1회만 출력한다.")]
    [SerializeField] private bool warnOnMissingReferences = true;

    [Tooltip("두 발전소의 인접 금지 링이 같은 셀을 요구할 때 누가 그 셀의 owner 가 될지 정한다.\n" +
             "FirstWriterWins: 먼저 처리된 발전소(작은 groupID 우선)가 차지. 후속 발전소는 해당 셀을 자기 것으로 표시하지 않는다.\n" +
             "LastWriterWins: 나중에 처리된 발전소가 덮어쓴다.")]
    [SerializeField] private ForbiddenOverlapPolicy overlapPolicy = ForbiddenOverlapPolicy.FirstWriterWins;

    public enum ForbiddenOverlapPolicy
    {
        FirstWriterWins,
        LastWriterWins,
    }

    /// <summary>
    /// 발전소 기준 "인접 금지" 오프셋. 현재 규칙은 8방향 1칸.
    /// GridManager.CanPlaceShape 에서 사용하는 방향 배열과 동일해야 규칙/시각 일치가 유지된다.
    /// </summary>
    private static readonly Vector2Int[] ForbiddenOffsets =
    {
        new Vector2Int( 0,  1), new Vector2Int( 0, -1),
        new Vector2Int( 1,  0), new Vector2Int(-1,  0),
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1),
    };

    // 직전 Refresh 에서 overlay 에 실제 써둔 셀들. 다음 Refresh 때 정확히 이 집합만 지워
    // "다른 시스템이 쓴 셀" 을 건드리지 않고, 이 컴포넌트가 overlay 의 유일 writer 임을 유지한다.
    private readonly HashSet<Vector3Int> lastWrittenCells = new HashSet<Vector3Int>();

    private bool hasLoggedMissingRefs;

    private void OnEnable()
    {
        GridManager.OnBoardMutated += HandleBoardMutated;
        // 씬 재진입 / 도메인 리로드 시 상태 복원을 위해 즉시 1회 동기화.
        // OnEnable → Start 순서가 보장되지 않아도, GridManager 가 초기화 전이면
        // Refresh 가 안전하게 no-op 으로 끝나도록 내부에서 방어한다.
        Refresh();
    }

    private void OnDisable()
    {
        GridManager.OnBoardMutated -= HandleBoardMutated;
        // 비활성화 상태에서 낡은 시각이 남아 헷갈리지 않도록 정리.
        ClearOverlay();
    }

    private void HandleBoardMutated()
    {
        Refresh();
    }

    /// <summary>
    /// GridManager 의 현재 보드 상태를 읽어 overlay 를 재구성한다.
    /// 외부(에디터 버튼 / 테스트 / 치트 UI 등)가 수동 갱신할 때도 호출할 수 있도록 public.
    /// </summary>
    public void Refresh()
    {
        if (!ValidateReferences()) return;
        if (overlayTilemap == null || grid == null) return;

        ClearOverlay();

        int w = grid.width;
        int h = grid.height;
        if (w <= 0 || h <= 0) return;

        // 1차 패스: 그룹 ID 별로 셀을 수집.
        // 소유권이 발전소 단위로 분리되어야 하므로 groupID 를 키로 묶는다.
        // SortedDictionary 로 결정적 순서(작은 groupID 먼저)를 확보해 FirstWriterWins 정책의 결과가 재현 가능하도록 한다.
        SortedDictionary<int, List<Vector2Int>> cellsByGroup = new SortedDictionary<int, List<Vector2Int>>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2Int idx = new Vector2Int(x, y);
                BlockData cell = grid.GetBlockAtArrayIndex(idx);
                if (cell == null || !cell.isGrouped) continue;
                // 방어적으로 groupID <= 0 은 "아직 그룹 미배정" 으로 간주하고 건너뛴다.
                if (cell.groupID <= 0) continue;

                if (!cellsByGroup.TryGetValue(cell.groupID, out List<Vector2Int> bucket))
                {
                    bucket = new List<Vector2Int>();
                    cellsByGroup[cell.groupID] = bucket;
                }
                bucket.Add(idx);
            }
        }

        // 2차 패스: 그룹별로 점유 타일 작성 + 소유권 등록.
        foreach (KeyValuePair<int, List<Vector2Int>> kv in cellsByGroup)
        {
            int groupId = kv.Key;
            List<Vector2Int> groupCells = kv.Value;

            if (occupiedTile != null)
            {
                for (int i = 0; i < groupCells.Count; i++)
                {
                    Vector3Int worldCell = grid.ArrayIndexToWorldCell(groupCells[i]);
                    overlayTilemap.SetTile(worldCell, occupiedTile);
                    PowerPlantTileOwnership.SetOwner(worldCell, groupId);
                    lastWrittenCells.Add(worldCell);
                }
            }
        }

        // 3차 패스: 그룹별로 인접 금지 링 작성 + 소유권 등록.
        // forbiddenRuleTile 을 따로 두는 이유는 occupiedTile 위에 덮이지 않도록 하기 위함.
        if (forbiddenRuleTile != null)
        {
            foreach (KeyValuePair<int, List<Vector2Int>> kv in cellsByGroup)
            {
                int groupId = kv.Key;
                List<Vector2Int> groupCells = kv.Value;

                for (int i = 0; i < groupCells.Count; i++)
                {
                    Vector2Int center = groupCells[i];
                    for (int d = 0; d < ForbiddenOffsets.Length; d++)
                    {
                        Vector2Int nIdx = center + ForbiddenOffsets[d];
                        if (nIdx.x < 0 || nIdx.x >= w || nIdx.y < 0 || nIdx.y >= h) continue;

                        BlockData neighbor = grid.GetBlockAtArrayIndex(nIdx);
                        if (neighbor != null && neighbor.isGrouped) continue;

                        Vector3Int worldCell = grid.ArrayIndexToWorldCell(nIdx);
                        if (!grid.IsWorldCellUnlocked(worldCell)) continue;

                        WriteForbiddenCell(worldCell, groupId);
                    }
                }
            }
        }

        // RuleTile 은 이웃 상태에 따라 자신의 스프라이트를 결정한다. 경계 셀(우리가 새로 쓴 셀
        // 과 그 주변 1칸) 을 리프레시해 인접 셀 변경으로 인한 스프라이트 재선택을 보장한다.
        RefreshTilesWithNeighbors(lastWrittenCells);
    }

    /// <summary>
    /// 인접 금지 셀에 타일과 소유권을 기록. 겹치는 셀의 정책은 <see cref="overlapPolicy"/> 에 따른다.
    /// </summary>
    private void WriteForbiddenCell(Vector3Int worldCell, int groupId)
    {
        bool alreadyOwned = PowerPlantTileOwnership.TryGetOwner(worldCell, out _);

        if (alreadyOwned && overlapPolicy == ForbiddenOverlapPolicy.FirstWriterWins)
        {
            // 이미 다른 발전소가 차지한 셀. 타일/owner 모두 건드리지 않는다.
            // lastWrittenCells 에는 이미 포함되어 있음 (앞 순회의 writer 가 추가).
            return;
        }

        overlayTilemap.SetTile(worldCell, forbiddenRuleTile);
        PowerPlantTileOwnership.SetOwner(worldCell, groupId);
        lastWrittenCells.Add(worldCell);
    }

    /// <summary>
    /// overlay 를 완전히 비운다. 외부 호출용.
    /// </summary>
    public void Clear()
    {
        ClearOverlay();
    }

    private void ClearOverlay()
    {
        if (overlayTilemap == null || lastWrittenCells.Count == 0)
        {
            // 잔존 owner 레코드만 있다 해도 Tilemap 이 없으면 의미 없으나,
            // 일관성을 위해 우리가 마지막에 기록했던 셀의 owner 는 털어낸다.
            if (lastWrittenCells.Count > 0)
            {
                foreach (Vector3Int cell in lastWrittenCells)
                {
                    PowerPlantTileOwnership.ClearOwner(cell);
                }
            }
            lastWrittenCells.Clear();
            return;
        }

        // Clear 시점의 스냅샷으로 refresh 범위도 확보한다.
        HashSet<Vector3Int> previous = new HashSet<Vector3Int>(lastWrittenCells);
        foreach (Vector3Int cell in previous)
        {
            overlayTilemap.SetTile(cell, null);
            // 소유권도 같이 해제. 다른 writer 가 이 셀을 관리하지 않는다는 "유일 writer" 가정을 유지.
            PowerPlantTileOwnership.ClearOwner(cell);
        }
        lastWrittenCells.Clear();
        RefreshTilesWithNeighbors(previous);
    }

    private void RefreshTilesWithNeighbors(HashSet<Vector3Int> cells)
    {
        if (overlayTilemap == null || cells == null || cells.Count == 0) return;

        HashSet<Vector3Int> refreshSet = new HashSet<Vector3Int>();
        foreach (Vector3Int c in cells)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    refreshSet.Add(new Vector3Int(c.x + dx, c.y + dy, 0));
                }
            }
        }
        foreach (Vector3Int c in refreshSet)
        {
            overlayTilemap.RefreshTile(c);
        }
    }

    /// <summary>
    /// 필수 참조 누락 여부를 1회 로그로 알림. 런타임 경고는 요란하지 않게 묶어서 출력.
    /// </summary>
    private bool ValidateReferences()
    {
        if (grid != null && overlayTilemap != null) return true;

        if (warnOnMissingReferences && !hasLoggedMissingRefs)
        {
            hasLoggedMissingRefs = true;
            Debug.LogWarning(
                $"[{nameof(PowerPlantZoneVisualizer)}] 필수 참조 누락: " +
                $"grid={(grid != null)}, overlayTilemap={(overlayTilemap != null)}. " +
                "Inspector 에서 GridManager 와 overlay Tilemap 을 연결해 주세요.", this);
        }
        return false;
    }
}
