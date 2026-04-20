using System;
using Special.Data;
using UnityEngine;

namespace Prediction
{
    /// <summary>
    /// 드래그/호버 이벤트를 여러 구독자(정보 패널, 오버레이 등) 에게 나눠주는 얇은 정적 버스.
    /// DraggableBlock / SpecialDraggableBlock / PlacedBlockVisual 이 발행자,
    /// PowerPlantInfoPanelController 등이 구독자.
    /// </summary>
    public static class PlacementInteractionHub
    {
        public static event Action<DragMovedArgs> OnDragMoved;
        public static event Action OnDragEnded;
        public static event Action<HoverTarget?> OnHoverChanged;

        public static bool IsDragging { get; private set; }
        public static HoverTarget? CurrentHover { get; private set; }

        public static void BroadcastDragMoved(Vector3Int anchorWorldCell, Vector2Int[] shape, SpecialBlockDefinition specialDef, int colorID, int shapeID)
        {
            IsDragging = true;
            CurrentHover = null;
            OnDragMoved?.Invoke(new DragMovedArgs
            {
                anchorWorldCell = anchorWorldCell,
                shape = shape,
                specialDef = specialDef,
                colorID = colorID,
                shapeID = shapeID
            });
        }

        public static void BroadcastDragEnded()
        {
            IsDragging = false;
            OnDragEnded?.Invoke();
        }

        public static void BroadcastHoverChanged(HoverTarget? target)
        {
            CurrentHover = target;
            if (IsDragging) return;
            OnHoverChanged?.Invoke(target);
        }
    }

    /// <summary>드래그 위치 갱신 페이로드. anchor 는 월드 타일 좌표(Vector3Int).</summary>
    public struct DragMovedArgs
    {
        public Vector3Int anchorWorldCell;
        public Vector2Int[] shape;
        public SpecialBlockDefinition specialDef; // 일반 블럭이면 null
        public int colorID;
        public int shapeID;
    }

    /// <summary>호버 타겟. arrayCell 은 보드 내부 배열 인덱스(0..W-1, 0..H-1).</summary>
    public struct HoverTarget
    {
        public Vector2Int arrayCell;

        public static HoverTarget FromArrayCell(Vector2Int arrayCell) => new HoverTarget { arrayCell = arrayCell };
    }
}
