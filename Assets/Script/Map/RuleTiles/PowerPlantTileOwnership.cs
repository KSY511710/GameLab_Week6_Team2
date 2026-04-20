using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "이 overlay 셀은 어느 발전소(group)의 표시인가" 를 기억하는 순수 런타임 레지스트리.
///
/// 존재 이유
/// - Unity 의 표준 <see cref="UnityEngine.Tilemaps.RuleTile"/> 은 이웃 매칭을 "같은 타일 에셋인가" 로만 판정한다.
///   그 결과 서로 다른 발전소가 같은 forbidden RuleTile 을 깔면 경계가 합쳐져 보여 "발전소별 점유 범위 표시" 라는
///   기획 의도가 깨진다.
/// - 해결: 셀마다 owner(group) 를 별도로 기록하고, 커스텀 RuleTile 이 이웃 매칭 시 owner 까지 함께 확인하게 한다.
///
/// 좌표계
/// - key 는 월드 셀 좌표(Vector3Int). 현재 프로젝트에는 overlay Tilemap 이 1개뿐이므로 Tilemap 식별자는 키에 넣지 않는다.
///   멀티 overlay 가 필요해지면 key 를 (tilemapId, cell) 쌍으로 확장한다 (명시적 확장 포인트).
///
/// Thread-safety
/// - Unity Tilemap 은 main thread 전용. 락 불필요.
///
/// 수명
/// - 정적 상태. 도메인 리로드 off 환경에서도 play 진입 직후 초기화되도록
///   <see cref="ResetOnPlayMode"/> 를 subsystem registration 타이밍에 건다.
/// - 씬 전환 시에는 소비자(<c>PowerPlantZoneVisualizer.OnDisable</c>) 가 <see cref="ClearAll"/> 로 정리한다.
/// </summary>
public static class PowerPlantTileOwnership
{
    private static readonly Dictionary<Vector3Int, int> CellToOwner = new Dictionary<Vector3Int, int>();

    /// <summary>
    /// 셀의 소유 그룹 ID 를 등록한다. 이미 다른 owner 가 있으면 덮어쓴다.
    /// 덮어쓰기 정책이 싫으면 <see cref="TrySetOwner"/> 를 쓴다.
    /// </summary>
    public static void SetOwner(Vector3Int cell, int ownerGroupId)
    {
        CellToOwner[cell] = ownerGroupId;
    }

    /// <summary>
    /// 기존 owner 가 없을 때만 등록한다. "first writer wins" 정책용.
    /// 두 발전소의 인접 금지 링이 겹치는 셀에서 먼저 처리된 발전소의 소유를 유지하고 싶을 때 사용.
    /// </summary>
    /// <returns>새로 썼으면 true. 이미 누군가 주인이 있었으면 false.</returns>
    public static bool TrySetOwner(Vector3Int cell, int ownerGroupId)
    {
        if (CellToOwner.ContainsKey(cell)) return false;
        CellToOwner[cell] = ownerGroupId;
        return true;
    }

    /// <summary>셀의 소유권을 해제한다. 존재하지 않아도 no-op.</summary>
    public static void ClearOwner(Vector3Int cell)
    {
        CellToOwner.Remove(cell);
    }

    /// <summary>
    /// 셀의 owner 를 조회한다. 등록되지 않은 셀이면 false.
    /// RuleTile 이웃 매칭의 핫 패스이므로 <see cref="Dictionary{TKey,TValue}.TryGetValue"/> 를 그대로 노출.
    /// </summary>
    public static bool TryGetOwner(Vector3Int cell, out int ownerGroupId)
    {
        return CellToOwner.TryGetValue(cell, out ownerGroupId);
    }

    /// <summary>레지스트리 전체 초기화.</summary>
    public static void ClearAll()
    {
        CellToOwner.Clear();
    }

    /// <summary>디버그/테스트용 현재 등록 수.</summary>
    public static int Count => CellToOwner.Count;

    /// <summary>
    /// Enter Play Mode Options 로 도메인 리로드를 끈 환경에서도
    /// 이전 플레이 세션의 잔재가 남지 않도록 강제 초기화.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayMode()
    {
        CellToOwner.Clear();
    }
}
