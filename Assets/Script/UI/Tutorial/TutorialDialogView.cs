using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사 패널. 본문 텍스트 + "계속" 버튼 + 이벤트 대기용 힌트 표시.
    /// 인스펙터 구성:
    ///   TutorialDialogView (RectTransform, pivot=(0.5,0.5), anchor=(0.5,0.5))
    ///   ├ Background (Image, raycastTarget=true)
    ///   ├ MessageText (TextMeshProUGUI)
    ///   ├ ContinueButton (Button) + "계속" 레이블
    ///   └ WaitHint (GameObject · 이벤트 대기 스텝에서 "..." 표시)
    /// </summary>
    public class TutorialDialogView : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject waitHint;

        [Header("Layout")]
        [Tooltip("타겟 바깥으로 띄울 간격.")]
        [SerializeField] private float spacing = 24f;
        [Tooltip("부모 영역 가장자리 안전 여백.")]
        [SerializeField] private float screenMargin = 16f;

        private Action onContinueCallback;
        private RectTransform parentRt;

        private void Awake()
        {
            parentRt = panelRoot != null ? panelRoot.parent as RectTransform : null;
            if (continueButton != null) continueButton.onClick.AddListener(HandleContinueClicked);
            Hide();
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinueClicked);
        }

        public void Show(string message, DialogAnchor anchor, RectTransform target, bool showContinueButton, Action onContinue)
        {
            if (panelRoot == null) return;
            if (messageText != null) messageText.text = message;
            if (continueButton != null) continueButton.gameObject.SetActive(showContinueButton);
            if (waitHint != null) waitHint.SetActive(!showContinueButton);

            onContinueCallback = onContinue;
            panelRoot.gameObject.SetActive(true);

            // 레이아웃을 강제로 갱신해 패널의 실제 크기를 읽은 뒤 위치 계산.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            Reposition(anchor, target);
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.gameObject.SetActive(false);
            onContinueCallback = null;
        }

        private void HandleContinueClicked()
        {
            Action cb = onContinueCallback;
            onContinueCallback = null;
            cb?.Invoke();
        }

        private void Reposition(DialogAnchor anchor, RectTransform target)
        {
            if (panelRoot == null) return;

            // 기본 anchor/pivot 을 중앙으로 통일해 anchoredPosition 만으로 위치 제어.
            panelRoot.anchorMin = panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);

            if (anchor == DialogAnchor.Center || target == null || parentRt == null)
            {
                panelRoot.anchoredPosition = Vector2.zero;
                return;
            }

            if (!TryGetTargetLocal(target, out Vector2 targetCenter, out Vector2 targetHalf))
            {
                panelRoot.anchoredPosition = Vector2.zero;
                return;
            }

            Vector2 panelHalf = panelRoot.rect.size * 0.5f;
            Vector2 offset = Vector2.zero;

            switch (anchor)
            {
                case DialogAnchor.LeftOfTarget:
                    offset = new Vector2(-(targetHalf.x + spacing + panelHalf.x), 0f);
                    break;
                case DialogAnchor.RightOfTarget:
                    offset = new Vector2(+(targetHalf.x + spacing + panelHalf.x), 0f);
                    break;
                case DialogAnchor.AboveTarget:
                    offset = new Vector2(0f, +(targetHalf.y + spacing + panelHalf.y));
                    break;
                case DialogAnchor.BelowTarget:
                    offset = new Vector2(0f, -(targetHalf.y + spacing + panelHalf.y));
                    break;
            }

            Vector2 pos = targetCenter + offset;
            Rect parentRect = parentRt.rect;
            float minX = parentRect.xMin + panelHalf.x + screenMargin;
            float maxX = parentRect.xMax - panelHalf.x - screenMargin;
            float minY = parentRect.yMin + panelHalf.y + screenMargin;
            float maxY = parentRect.yMax - panelHalf.y - screenMargin;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            panelRoot.anchoredPosition = pos;
        }

        private bool TryGetTargetLocal(RectTransform target, out Vector2 center, out Vector2 halfExt)
        {
            center = default;
            halfExt = default;
            if (target == null || parentRt == null) return false;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 sBL = corners[0];
            Vector2 sTR = corners[2];

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, sBL, null, out Vector2 lBL)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, sTR, null, out Vector2 lTR)) return false;

            center = (lBL + lTR) * 0.5f;
            halfExt = (lTR - lBL) * 0.5f;
            return true;
        }
    }
}
