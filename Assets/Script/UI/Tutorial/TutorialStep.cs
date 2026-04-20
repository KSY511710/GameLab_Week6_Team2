using System;
using UnityEngine;

namespace UI.Tutorial
{
    public enum TutorialAdvanceTrigger
    {
        ContinueButton,
        OnFirstDragStart,
        OnFirstPowerPlantBuilt,
        OnSkipAvailable
    }

    public enum DialogAnchor
    {
        Center,
        LeftOfTarget,
        RightOfTarget,
        AboveTarget,
        BelowTarget
    }

    public enum TutorialTargetKind
    {
        None,
        UIRect,
        FirstBuiltPlant
    }

    public enum StepShowCondition
    {
        Immediately,
        WhenSkipAvailable
    }

    [Serializable]
    public class TutorialStep
    {
        [Tooltip("로그/디버깅용 식별자. 예: welcome, goal, inventory.")]
        public string id;

        [TextArea(2, 6)]
        public string message;

        public TutorialTargetKind targetKind = TutorialTargetKind.None;

        [Tooltip("targetKind=UIRect 일 때 강조할 UI 영역.")]
        public RectTransform uiTarget;

        public TutorialAdvanceTrigger advanceTrigger = TutorialAdvanceTrigger.ContinueButton;

        public DialogAnchor anchor = DialogAnchor.Center;

        [Tooltip("스텝 활성화 후 다이얼로그 표시 조건. WhenSkipAvailable 은 스킵이 켜질 때까지 대기.")]
        public StepShowCondition showCondition = StepShowCondition.Immediately;

        [Tooltip("ContinueButton 스텝에서도 플레이어가 드래그를 마치는 순간 자동으로 다음 스텝으로 넘어간다.")]
        public bool autoAdvanceOnDragEnded;
    }
}
