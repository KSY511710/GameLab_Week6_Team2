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

    public enum SpecialBlockRole { Grouping, Independent }

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

        [Header("Effects (Hybrid)")]
        [Tooltip("재사용 가능한 SO 효과들. 파라미터만 다르면 여기서 에셋 인스턴스로 조합한다.")]
        public EffectAsset[] effectAssets;
        [Tooltip("특수 블럭 전용 MonoBehaviour 효과 프리팹. 인스턴스별 상태가 필요한 경우 사용.")]
        public GameObject[] customEffectPrefabs;

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
