using UnityEngine;

namespace UI.Tutorial
{
    /// <summary>
    /// 화면을 반투명 "그림자" 4장으로 덮어 타겟 사각형만 밝게 남기는 cutout 포커스.
    /// UI 타겟(RectTransform)과 월드 타겟(Vector3 + size) 두 모드를 지원한다.
    ///
    /// 계층 구성 (인스펙터에서 구성):
    ///   TutorialHighlighter (RectTransform, anchorMin=(0,0), anchorMax=(1,1), offsets=0)
    ///   ├ ShroudBottom  (Image, raycastTarget=false)
    ///   ├ ShroudTop     (Image, raycastTarget=false)
    ///   ├ ShroudLeft    (Image, raycastTarget=false)
    ///   ├ ShroudRight   (Image, raycastTarget=false)
    ///   └ Frame         (Image, 테두리 강조용 · 옵션)
    ///
    /// shroud 의 raycastTarget 은 false 로 두어 플레이어가 강조 상태에서도 게임 상호작용을
    /// 이어갈 수 있게 한다 (예: Step 3 인벤토리 드래그, Step 4 드래그 진행 중 정보 패널).
    /// </summary>
    public class TutorialHighlighter : MonoBehaviour
    {
        [SerializeField] private RectTransform shroudTop;
        [SerializeField] private RectTransform shroudBottom;
        [SerializeField] private RectTransform shroudLeft;
        [SerializeField] private RectTransform shroudRight;
        [SerializeField] private RectTransform frame;

        [Tooltip("월드 타겟 변환에 사용할 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("강조 사각형 외곽 여유 (로컬 유닛).")]
        [SerializeField] private float defaultPadding = 12f;

        private enum Mode { None, UiRect, World }

        private Mode mode = Mode.None;
        private RectTransform uiTarget;
        private Vector3 worldPos;
        private Vector2 worldSize;
        private float padding;
        private RectTransform rt;

        private void Awake()
        {
            rt = (RectTransform)transform;
            Show(false);
        }

        public void Highlight(RectTransform target, float? overridePadding = null)
        {
            if (target == null) { Clear(); return; }
            mode = Mode.UiRect;
            uiTarget = target;
            padding = overridePadding ?? defaultPadding;
            Show(true);
            UpdateLayout();
        }

        public void SetWorldTarget(Vector3 worldPosition, Vector2 size, float? overridePadding = null)
        {
            mode = Mode.World;
            worldPos = worldPosition;
            worldSize = size;
            padding = overridePadding ?? defaultPadding;
            Show(true);
            UpdateLayout();
        }

        public void Clear()
        {
            mode = Mode.None;
            uiTarget = null;
            Show(false);
        }

        private void Show(bool on)
        {
            SetActiveSafe(shroudTop, on);
            SetActiveSafe(shroudBottom, on);
            SetActiveSafe(shroudLeft, on);
            SetActiveSafe(shroudRight, on);
            SetActiveSafe(frame, on);
        }

        private static void SetActiveSafe(RectTransform r, bool on)
        {
            if (r != null) r.gameObject.SetActive(on);
        }

        private void LateUpdate()
        {
            // UI 레이아웃이 변할 수 있고, 월드 타겟은 카메라 이동/줌에 따라 스크린 위치가 매 프레임 변동될 수 있으므로 재계산.
            if (mode != Mode.None) UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (rt == null) return;
            if (!TryGetTargetScreenCorners(out Vector2 sBL, out Vector2 sTR)) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sBL, null, out Vector2 lBL)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, sTR, null, out Vector2 lTR)) return;

            Vector2 pad = Vector2.one * padding;
            lBL -= pad;
            lTR += pad;

            Rect parentRect = rt.rect;
            float nbx = Mathf.Clamp01(Mathf.InverseLerp(parentRect.xMin, parentRect.xMax, lBL.x));
            float nby = Mathf.Clamp01(Mathf.InverseLerp(parentRect.yMin, parentRect.yMax, lBL.y));
            float ntx = Mathf.Clamp01(Mathf.InverseLerp(parentRect.xMin, parentRect.xMax, lTR.x));
            float nty = Mathf.Clamp01(Mathf.InverseLerp(parentRect.yMin, parentRect.yMax, lTR.y));

            SetAnchors(shroudBottom, new Vector2(0f, 0f),   new Vector2(1f, nby));
            SetAnchors(shroudTop,    new Vector2(0f, nty),  new Vector2(1f, 1f));
            SetAnchors(shroudLeft,   new Vector2(0f, nby),  new Vector2(nbx, nty));
            SetAnchors(shroudRight,  new Vector2(ntx, nby), new Vector2(1f, nty));
            SetAnchors(frame,        new Vector2(nbx, nby), new Vector2(ntx, nty));
        }

        private bool TryGetTargetScreenCorners(out Vector2 sBL, out Vector2 sTR)
        {
            sBL = sTR = default;
            if (mode == Mode.UiRect)
            {
                if (uiTarget == null) return false;
                var corners = new Vector3[4];
                uiTarget.GetWorldCorners(corners);
                sBL = corners[0];
                sTR = corners[2];
                return true;
            }
            if (mode == Mode.World)
            {
                Camera cam = worldCamera != null ? worldCamera : Camera.main;
                if (cam == null) return false;
                Vector2 half = worldSize * 0.5f;
                Vector3 wBL = worldPos + new Vector3(-half.x, -half.y, 0f);
                Vector3 wTR = worldPos + new Vector3(+half.x, +half.y, 0f);
                sBL = RectTransformUtility.WorldToScreenPoint(cam, wBL);
                sTR = RectTransformUtility.WorldToScreenPoint(cam, wTR);
                return true;
            }
            return false;
        }

        private static void SetAnchors(RectTransform r, Vector2 min, Vector2 max)
        {
            if (r == null) return;
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
    }
}
