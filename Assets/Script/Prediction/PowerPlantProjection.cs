using System.Collections.Generic;
using Special.Runtime;
using UnityEngine;

namespace Prediction
{
    /// <summary>
    /// 발전소 스펙을 기술하는 단일 DTO.
    /// - 드래그 예측(PowerPlantPredictor)이 "가상 배치 후 상태"를 채워 이 구조체를 만든다.
    /// - 건설된 발전소 호버 경로는 FromGroupInfo 로 동일 타입에 수렴시킨다.
    /// 정보 패널은 이 DTO 만 읽으면 두 경로를 구분 없이 렌더한다.
    /// </summary>
    public sealed class PowerPlantProjection
    {
        // 기본 스펙 (모두 최종값 — 계산식 표시는 trace 에서 가져올 것)
        public int blockSize;
        public int baseProduction;
        public int uniqueParts;
        public int baseCompletion;
        public int shapeCompletion;
        public float colorMultiplier;
        public float finalMultiplier = 1f;
        public float groupPower;
        public float appliedExchangeRatio;
        public float estimatedMoneyGen;

        public int dominantColor;
        public Color dominantRealColor = Color.white;

        public List<Vector2Int> clusterPositions;

        /// <summary>효과 누적 과정 및 최종 결과 기록. null 이면 단계 표시 불가(raw 값만 표시).</summary>
        public CalculationTrace trace;

        /// <summary>9칸·3종 충족 여부. false 면 아직 발전소가 되지 않는 클러스터임을 뜻한다.</summary>
        public bool isFormed;

        /// <summary>배치 불가 사유. null 이면 배치 가능 (또는 이미 설치된 발전소).</summary>
        public string blockedReason;

        /// <summary>드래그 중인 블럭이 특수 블럭일 때, 자체가 바로 기여하는 OnProductionSettle 류 생산분.</summary>
        public float selfContributionPower;

        /// <summary>예측 시 9칸·3종 기준을 얼마나 충족했는지 (UI 힌트용).</summary>
        public int currentBlockCount;
        public int currentUniquePartCount;

        public PowerPlantProjection() { }

        /// <summary>건설된 발전소 → projection. 호버 시 같은 렌더 경로를 타도록 수렴.</summary>
        public static PowerPlantProjection FromGroupInfo(GroupInfo g)
        {
            if (g == null) return null;
            return new PowerPlantProjection
            {
                blockSize = g.blockSize,
                baseProduction = g.baseProduction,
                uniqueParts = g.uniqueParts,
                baseCompletion = 2,
                shapeCompletion = g.formationMultiplier,
                colorMultiplier = g.colorMultiplier,
                finalMultiplier = 1f,
                groupPower = g.groupPower,
                appliedExchangeRatio = g.appliedExchangeRatio,
                estimatedMoneyGen = g.estimatedMoneyGen,
                dominantColor = g.finalColor,
                dominantRealColor = g.dominantRealColor,
                clusterPositions = g.clusterPositions,
                trace = g.lastTrace,
                isFormed = true,
                currentBlockCount = g.blockSize,
                currentUniquePartCount = g.uniqueParts
            };
        }

        /// <summary>배치 불가 시 사유만 담긴 projection.</summary>
        public static PowerPlantProjection Blocked(string reason)
        {
            return new PowerPlantProjection { blockedReason = reason, isFormed = false };
        }
    }
}
