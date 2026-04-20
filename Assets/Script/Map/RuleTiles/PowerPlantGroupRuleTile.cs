using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 발전소 그룹 단위로 이웃 매칭을 분리하는 RuleTile 확장.
///
/// 표준 <see cref="RuleTile"/> 과의 차이
/// - 표준: 이웃 셀의 "타일 에셋 identity" 만 비교 → 서로 다른 발전소의 링이 같은 에셋이면 연결되어 보임.
/// - 본 클래스: 이웃 셀의 owner group 까지 함께 비교 → 발전소별로 독립된 경계를 유지.
///
/// 후킹 지점 선택 사유 (중요)
/// - com.unity.2d.tilemap.extras 6.0.1 기준으로 다음 두 메서드는 <c>virtual 이 아님</c>:
///     public bool RuleMatches(TilingRule rule, Vector3Int pos, ITilemap tm, int angle, bool mirrorX = false)
///     public bool RuleMatches(TilingRule rule, Vector3Int pos, ITilemap tm, bool mirrorX, bool mirrorY)
///   → 이 둘을 override 하면 컴파일 실패. override 가능한 것은 상위 엔트리인
///     public virtual bool RuleMatches(TilingRule, Vector3Int, ITilemap, ref Matrix4x4) 뿐이다.
/// - 그래서 상위 엔트리만 override 하고, 내부의 회전/미러 dispatch 를 여기서 재구현한다.
/// - 좌표 계산(GetRotatedPosition/GetMirroredPosition/GetOffsetPosition) 은 base 의 virtual 을 그대로 호출하므로
///   Unity 패키지 업데이트에도 회전/미러 연산 자체는 영향을 받지 않는다.
///
/// 사용법
/// 1) 기존 forbidden RuleTile 에셋의 <c>m_Script</c> 를 이 클래스로 교체
///    (또는 Assets > Create > Tiles > Power Plant Group Rule Tile 로 신규 생성 후 스프라이트 재연결).
/// 2) <c>PowerPlantZoneVisualizer</c> 가 타일을 깔 때 <see cref="PowerPlantTileOwnership.SetOwner"/> 로 소유권을 함께 기록.
/// 3) 소유권이 등록되지 않은 셀에서 이 타일이 평가되면 표준 RuleTile 동작으로 안전하게 폴백.
///
/// 확장 포인트
/// - 매칭 정책을 바꿀 때(예: "같은 팀 발전소끼리는 합쳐 보이게") <see cref="NeighborMatches"/> 만 수정.
/// - 커스텀 Neighbor enum(3 이상) 을 쓰는 RuleTile 확장과도 호환: 인식 못 하는 값은 base 의 <see cref="RuleTile.RuleMatch"/> 로 위임.
/// </summary>
[CreateAssetMenu(
    fileName = "PowerPlantGroupRuleTile",
    menuName = "Tiles/Power Plant Group Rule Tile",
    order = 200)]
public class PowerPlantGroupRuleTile : RuleTile
{
    // --- Evaluation 컨텍스트 -----------------------------------------------
    // Unity 의 RuleTile 평가는 메인 스레드에서 "타일 1개 refresh → RuleMatches → 이웃 반복" 순으로 수행된다.
    // 중첩 호출이 없으므로 스택 불필요. NonSerialized 로 에셋 직렬화에서 배제.
    [NonSerialized] private bool _hasOwnerContext;
    [NonSerialized] private int _currentOwnerId;

    /// <summary>
    /// 상위 엔트리 override. base 의 회전/미러 dispatch 를 owner 인지 방식으로 재구현한다.
    /// 로직은 com.unity.2d.tilemap.extras 6.0.1 의 base 구현과 동일하되
    /// 이웃 슬롯 매칭 단계만 <see cref="RuleMatchesOwned"/> / <see cref="RuleMatchesMirroredOwned"/> 로 치환.
    /// </summary>
    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        BeginEvaluation(position);
        try
        {
            if (RuleMatchesOwned(rule, position, tilemap, 0, false))
            {
                transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 0f), Vector3.one);
                return true;
            }

            if (rule.m_RuleTransform == TilingRuleOutput.Transform.Rotated)
            {
                for (int angle = m_RotationAngle; angle < 360; angle += m_RotationAngle)
                {
                    if (RuleMatchesOwned(rule, position, tilemap, angle, false))
                    {
                        transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, -angle), Vector3.one);
                        return true;
                    }
                }
            }
            else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorXY)
            {
                if (RuleMatchesMirroredOwned(rule, position, tilemap, true, true))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, -1f, 1f));
                    return true;
                }
                if (RuleMatchesMirroredOwned(rule, position, tilemap, true, false))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
                    return true;
                }
                if (RuleMatchesMirroredOwned(rule, position, tilemap, false, true))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f));
                    return true;
                }
            }
            else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorX)
            {
                if (RuleMatchesMirroredOwned(rule, position, tilemap, true, false))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
                    return true;
                }
            }
            else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorY)
            {
                if (RuleMatchesMirroredOwned(rule, position, tilemap, false, true))
                {
                    transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f));
                    return true;
                }
            }
            else if (rule.m_RuleTransform == TilingRuleOutput.Transform.RotatedMirror)
            {
                for (int angle = 0; angle < 360; angle += m_RotationAngle)
                {
                    if (angle != 0 && RuleMatchesOwned(rule, position, tilemap, angle, false))
                    {
                        transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, -angle), Vector3.one);
                        return true;
                    }
                    if (RuleMatchesOwned(rule, position, tilemap, angle, true))
                    {
                        transform = Matrix4x4.TRS(
                            Vector3.zero,
                            Quaternion.Euler(0f, 0f, -angle),
                            new Vector3(-1f, 1f, 1f));
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            EndEvaluation();
        }
    }

    /// <summary>
    /// 회전(+옵션 mirrorX) 경로의 이웃 매칭. base 의 <c>RuleMatches(int angle, bool mirrorX)</c> 와 동일하나
    /// 매 슬롯 비교를 <see cref="NeighborMatches"/> 로 치환해 owner 까지 확인한다.
    /// </summary>
    private bool RuleMatchesOwned(TilingRule rule, Vector3Int position, ITilemap tilemap, int angle, bool mirrorX)
    {
        int minCount = Mathf.Min(rule.m_Neighbors.Count, rule.m_NeighborPositions.Count);
        for (int i = 0; i < minCount; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int neighborPosition = rule.m_NeighborPositions[i];
            if (mirrorX)
                neighborPosition = GetMirroredPosition(neighborPosition, true, false);
            Vector3Int positionOffset = GetRotatedPosition(neighborPosition, angle);
            Vector3Int absoluteNeighbor = GetOffsetPosition(position, positionOffset);
            TileBase other = tilemap.GetTile(absoluteNeighbor);

            if (!NeighborMatches(neighbor, other, absoluteNeighbor)) return false;
        }
        return true;
    }

    /// <summary>
    /// 미러 경로의 이웃 매칭. base 의 <c>RuleMatches(bool mirrorX, bool mirrorY)</c> 와 동일하나
    /// 매 슬롯 비교를 <see cref="NeighborMatches"/> 로 치환해 owner 까지 확인한다.
    /// </summary>
    private bool RuleMatchesMirroredOwned(TilingRule rule, Vector3Int position, ITilemap tilemap, bool mirrorX, bool mirrorY)
    {
        int minCount = Mathf.Min(rule.m_Neighbors.Count, rule.m_NeighborPositions.Count);
        for (int i = 0; i < minCount; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int positionOffset = GetMirroredPosition(rule.m_NeighborPositions[i], mirrorX, mirrorY);
            Vector3Int absoluteNeighbor = GetOffsetPosition(position, positionOffset);
            TileBase other = tilemap.GetTile(absoluteNeighbor);

            if (!NeighborMatches(neighbor, other, absoluteNeighbor)) return false;
        }
        return true;
    }

    /// <summary>
    /// 이웃 슬롯 매칭 판정. 표준 RuleTile 의 <see cref="RuleTile.RuleMatch(int, TileBase)"/> 를
    /// owner 일치 조건으로 감싼다.
    ///
    /// 정책
    /// - neighbor == This: "같은 타일" 이면서 owner 가 동일해야 true. owner 가 다르면 남이 쓴 타일로 취급해 경계를 분리.
    /// - neighbor == NotThis: 다른 타일이거나 owner 가 달라도 true. → 이웃 그룹도 "남" 으로 취급.
    /// - 그 외(확장 Neighbor 값): base 의 <see cref="RuleTile.RuleMatch"/> 로 위임.
    ///
    /// owner 컨텍스트가 없으면(해당 셀에 소유권 미등록) 표준 RuleTile 동작으로 폴백.
    /// </summary>
    protected virtual bool NeighborMatches(int neighbor, TileBase other, Vector3Int neighborPos)
    {
        if (!_hasOwnerContext)
        {
            return RuleMatch(neighbor, other);
        }

        bool sameTileAsset = other == this;

        if (neighbor == TilingRuleOutput.Neighbor.This)
        {
            if (!sameTileAsset) return false;
            return IsSameOwnerGroup(neighborPos);
        }

        if (neighbor == TilingRuleOutput.Neighbor.NotThis)
        {
            if (!sameTileAsset) return true;
            return !IsSameOwnerGroup(neighborPos);
        }

        return RuleMatch(neighbor, other);
    }

    /// <summary>
    /// 이웃 셀의 owner 가 현재 중심 셀과 같은 그룹인지 판정.
    /// 이웃 owner 미등록 시 false (= 같은 그룹 아님, 즉 경계로 간주).
    /// </summary>
    protected bool IsSameOwnerGroup(Vector3Int neighborPos)
    {
        if (!PowerPlantTileOwnership.TryGetOwner(neighborPos, out int neighborOwner)) return false;
        return neighborOwner == _currentOwnerId;
    }

    private void BeginEvaluation(Vector3Int position)
    {
        _hasOwnerContext = PowerPlantTileOwnership.TryGetOwner(position, out _currentOwnerId);
    }

    private void EndEvaluation()
    {
        _hasOwnerContext = false;
        _currentOwnerId = 0;
    }
}
