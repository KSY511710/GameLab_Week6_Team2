using Special.Effects;
using UnityEngine;

namespace Special.Data
{
    public enum SpecialColorBinding { Single, MultiPrimary, OffPalette }

    [System.Flags]
    public enum ColorSet
    {
        None = 0,
        Red = 1 << 0,
        Blue = 1 << 1,
        Yellow = 1 << 2,
        All = Red | Blue | Yellow
    }

    /// <summary>
    /// 특수 블럭이 정산 파이프라인에서 취급되는 방식.
    /// Grouping : 일반 발전소처럼 주변 블럭과 BFS 로 그룹을 형성한다.
    /// Independent : 그룹 BFS 와 인접 금지 규칙 양쪽에서 제외되는 "공용 부품" 성격의 블럭.
    ///               ungrouped 기본 +1 생산은 유지되므로 SettlementData 의 scrap 에 합산된다.
    /// PowerPlant : "그 자체로 하나의 발전소" 인 특수 블럭. 색상 BFS 로 일반 블럭과
    ///              합쳐지지는 않지만, 설치 직후 자기 footprint 만으로 이루어진 솔로 그룹이
    ///              자동 생성되어 activeGroups 에 들어간다. 즉 "그룹화된 블럭" 과 동일한 판정
    ///              (인접 배치 금지, OwnPowerPlant/AdjacentPowerPlant 스코프 대상)을 받는다.
    ///              일일 생산량은 각 Effect 의 EstimateLivePower 합산으로 산출되어
    ///              PowerText 실시간 합계와 SettlementUIController 색상 막대에 그대로 반영된다.
    /// </summary>
    public enum SpecialBlockRole { Grouping, Independent, PowerPlant }

    [CreateAssetMenu(menuName = "Special/Block Definition", fileName = "SpecialBlock_")]
    public class SpecialBlockDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        [Tooltip("인벤토리 UI 슬롯에 표시될 아이콘. 모양/크기/문양이 한 장에 드러나는 이미지.")]
        public Sprite icon;

        [Header("Shape & Size")]
        public Vector2Int[] shapeCoords = new[] { Vector2Int.zero };
        public bool allowRotation = false;

        [Header("Color")]
        public SpecialColorBinding colorBinding = SpecialColorBinding.Single;
        [Tooltip("Single 에선 첫 번째 플래그만 사용. MultiPrimary 에선 다중. OffPalette 에선 무시.")]
        public ColorSet includedPrimaries = ColorSet.Red;
        [Tooltip("OffPalette 에서만 사용. 기본 파/노/빨 팔레트 밖의 표시 색.")]
        public Color offPaletteColor = Color.black;

        [Header("Shape / Symbol")]
        [Tooltip("일반 부품 shapeID 공간(1-9)과 겹치지 않도록 1000+ 권장.")]
        public int uniqueShapeId = 1000;

        [Header("Role")]
        public SpecialBlockRole role = SpecialBlockRole.Grouping;

        [Header("Placement Limits")]
        [Min(1)] public int maxPerGame = 1;
        [Min(1)] public int maxPerZone = 1;

        [Header("Effects")]
        [Tooltip("재사용 가능한 SO 효과들. 일반적으로 CompositeEffectAsset 인스턴스를 조립해 슬롯에 채운다.")]
        public EffectAsset[] effectAssets;

        /// <summary>Single 바인딩에서 사용할 단일 색상 ID(1=Red, 2=Blue, 3=Yellow, 0=off).</summary>
        public int ResolveSingleColorID()
        {
            if (colorBinding != SpecialColorBinding.Single) return 0;
            if ((includedPrimaries & ColorSet.Red) != 0) return 1;
            if ((includedPrimaries & ColorSet.Blue) != 0) return 2;
            if ((includedPrimaries & ColorSet.Yellow) != 0) return 3;
            return 0;
        }
    }
}
