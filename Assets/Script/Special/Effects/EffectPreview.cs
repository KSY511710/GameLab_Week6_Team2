using System.Collections.Generic;
using UnityEngine;

namespace Special.Effects
{
    /// <summary>
    /// 한 효과가 자신을 시각적으로 어떻게 펼칠지 담는 데이터 묶음.
    /// PowerAnimationSequencer 가 화면에 보여줄 텍스트/오버레이/하이라이트 셀을 한 자리에 모은다.
    /// 효과 구현체는 BuildPreview 에서 이 객체를 채워 반환한다.
    /// </summary>
    public class EffectPreview
    {
        /// <summary>패널 헤더에 표시될 효과 이름.</summary>
        public string title;

        /// <summary>한 줄씩 누적해 보여줄 계산 단계 텍스트.</summary>
        public List<string> steps = new List<string>();

        /// <summary>오버레이로 옅게 깔아 영향 범위를 보여줄 셀들 (배열 인덱스).</summary>
        public List<Vector2Int> scopeCells;

        /// <summary>실제로 영향이 적용된 셀들 (배열 인덱스). PlacedBlockVisual 로 플래시한다.</summary>
        public List<Vector2Int> impactCells;

        /// <summary>오버레이 RGBA 색상.</summary>
        public Color overlayColor = new Color(1f, 0.85f, 0.2f, 0.32f);
    }
}
