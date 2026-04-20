using System.Text;
using Prediction;
using Special.Data;
using Special.Runtime;
using TMPro;
using UnityEngine;

namespace UI.Info
{
    /// <summary>
    /// 단일 정보 패널, 3개 모드(Predict/Built/Special) 스위칭.
    /// - 드래그 중 : PowerPlantPredictor 결과 렌더.
    /// - 일반 블럭/발전소 호버 : GroupInfo.lastTrace 기반 렌더.
    /// - 특수 블럭 호버 : definition + scope + effect 요약 렌더.
    /// 시퀀서(PowerManager.IsAnimating) 진행 중에는 자동 숨김.
    /// </summary>
    public class PowerPlantInfoPanelController : MonoBehaviour
    {
        public static PowerPlantInfoPanelController Instance { get; private set; }

        [Header("Roots")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI footerText;

        [Header("Mode Tint")]
        [SerializeField] private UnityEngine.UI.Image modeBadge;
        [SerializeField] private Color predictTint = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField] private Color builtTint = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color specialTint = new Color(0.8f, 0.4f, 1f, 1f);

        private GridManager grid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Hide();
        }

        private void OnEnable()
        {
            PlacementInteractionHub.OnDragMoved += HandleDragMoved;
            PlacementInteractionHub.OnDragEnded += HandleDragEnded;
            PlacementInteractionHub.OnHoverChanged += HandleHoverChanged;
        }

        private void OnDisable()
        {
            PlacementInteractionHub.OnDragMoved -= HandleDragMoved;
            PlacementInteractionHub.OnDragEnded -= HandleDragEnded;
            PlacementInteractionHub.OnHoverChanged -= HandleHoverChanged;
        }

        private GridManager Grid => grid != null ? grid : (grid = Object.FindFirstObjectByType<GridManager>());

        // ============ Hub handlers ============

        private void HandleDragMoved(DragMovedArgs args)
        {
            if (IsSequencerAnimating()) { Hide(); return; }
            GridManager g = Grid;
            if (g == null) { Hide(); return; }

            PowerPlantProjection proj = PowerPlantPredictor.Predict(g, args.anchorWorldCell, args.shape, args.specialDef, args.colorID, args.shapeID);
            RenderPredict(proj, args.specialDef);
        }

        private void HandleDragEnded()
        {
            if (PlacementInteractionHub.CurrentHover.HasValue)
                HandleHoverChanged(PlacementInteractionHub.CurrentHover);
            else
                Hide();
        }

        private void HandleHoverChanged(HoverTarget? target)
        {
            if (IsSequencerAnimating()) { Hide(); return; }
            if (!target.HasValue) { Hide(); return; }

            GridManager g = Grid;
            if (g == null) { Hide(); return; }
            Vector2Int cell = target.Value.arrayCell;
            BlockData block = g.GetBlockAtArrayIndex(cell);
            if (block == null) { Hide(); return; }

            // 특수 블럭이면 상세 모드 우선.
            if (block.attribute.specialDef != null)
            {
                SpecialBlockInstance inst = SpecialBlockRegistry.Instance != null
                    ? SpecialBlockRegistry.Instance.FindByFootprintCell(cell)
                    : null;
                if (inst != null) { RenderSpecialDetail(inst); return; }
            }

            // 그룹 멤버면 발전소 상세.
            if (block.isGrouped)
            {
                GroupInfo group = FindGroup(block.groupID);
                if (group != null) { RenderBuiltPlant(group); return; }
            }

            Hide();
        }

        // ============ Renderers ============

        private void RenderPredict(PowerPlantProjection proj, SpecialBlockDefinition specialDef)
        {
            if (proj == null) { Hide(); return; }
            Show();
            SetTint(predictTint);

            if (!string.IsNullOrEmpty(proj.blockedReason))
            {
                titleText.text = "청사진";
                bodyText.text = $"<color=#FF8888>{proj.blockedReason}</color>";
                footerText.text = string.Empty;
                return;
            }

            string subject = specialDef != null && !string.IsNullOrEmpty(specialDef.displayName)
                ? specialDef.displayName
                : "발전소";
            titleText.text = $"{subject} 청사진";

            if (!proj.isFormed)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("현재 클러스터: ").Append(proj.currentBlockCount).Append("칸 / ")
                  .Append(proj.currentUniquePartCount).Append("종\n");
                sb.Append("<color=#AAAAAA>발전소 형성 조건:\n 9칸 이상\n");
                sb.Append("<color=#AAAAAA> 부품 3종 이상</color>");
                if (proj.selfContributionPower > 0f)
                {
                    sb.Append("\n").Append("<color=#AADDFF>자체 기여 예상: +")
                      .Append(InfoPanelFormatter.FormatNumber(proj.selfContributionPower)).Append(" GWh</color>");
                }
                bodyText.text = sb.ToString();
                footerText.text = string.Empty;
                return;
            }

            bodyText.text = InfoPanelFormatter.BuildBody(proj.trace);

            StringBuilder footer = new StringBuilder();
            footer.Append("예상 발전량: <b>").Append(InfoPanelFormatter.FormatNumber(proj.groupPower)).Append(" GWh</b>");
            footer.Append("\n 예상 수익: <b>").Append(InfoPanelFormatter.FormatNumber(proj.estimatedMoneyGen)).Append("$</b>");
            if (proj.selfContributionPower > 0f)
            {
                footer.Append("\n<color=#AADDFF>+ 자체 기여 ")
                      .Append(InfoPanelFormatter.FormatNumber(proj.selfContributionPower)).Append(" GWh</color>");
            }
            footerText.text = footer.ToString();
        }

        private void RenderBuiltPlant(GroupInfo group)
        {
            Show();
            SetTint(builtTint);

            titleText.text = $"⚡ 발전소 #{group.groupID}";

            if (group.isPowerPlantSolo)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("특수 발전소 (").Append(group.blockSize).Append("칸)\n");
                sb.Append("현재 발전량: <b> \n")
                  .Append(InfoPanelFormatter.FormatNumber(group.groupPower)).Append(" GWh</b>\n");
                sb.Append("환전 비율: ")
                  .Append(InfoPanelFormatter.FormatNumber(group.appliedExchangeRatio));
                bodyText.text = sb.ToString();
            }
            else
            {
                bodyText.text = group.lastTrace != null
                    ? InfoPanelFormatter.BuildBody(group.lastTrace)
                    : "<color=#AAAAAA>최근 계산 정보 없음</color>";
            }

            StringBuilder footer = new StringBuilder();
            footer.Append("현재 발전량: <b>").Append(InfoPanelFormatter.FormatNumber(group.groupPower)).Append(" GWh</b>");
            footer.Append("\n 수익: <b>").Append(InfoPanelFormatter.FormatNumber(group.estimatedMoneyGen)).Append("$</b>");
            footerText.text = footer.ToString();
        }

        private void RenderSpecialDetail(SpecialBlockInstance inst)
        {
            Show();
            SetTint(specialTint);

            SpecialBlockDefinition def = inst.definition;
            titleText.text = def != null && !string.IsNullOrEmpty(def.displayName)
                ? $"✨ {def.displayName}"
                : "✨ 특수 블럭";

            StringBuilder body = new StringBuilder();
            body.Append("역할: ").Append(def != null ? def.role.ToString() : "?").Append('\n');
            body.Append("점유 칸: ").Append(inst.footprint.Count).Append('\n');
            if (inst.groupId > 0)
                body.Append("소속 그룹: #").Append(inst.groupId).Append('\n');

            if (inst.EffectInstances != null && inst.EffectInstances.Count > 0)
            {
                body.Append("\n<b>효과</b>\n");
                for (int i = 0; i < inst.EffectInstances.Count; i++)
                {
                    var eff = inst.EffectInstances[i];
                    if (eff == null) continue;
                    body.Append("  • ").Append(eff.name).Append('\n');
                }
            }

            bodyText.text = body.ToString();
            footerText.text = def != null
                ? $"설치 한도: 게임 {def.maxPerGame} · 구역 {def.maxPerZone}"
                : string.Empty;
        }

        // ============ Helpers ============

        private static GroupInfo FindGroup(int groupID)
        {
            PowerManager pm = PowerManager.Instance;
            if (pm == null) return null;
            for (int i = 0; i < pm.activeGroups.Count; i++)
                if (pm.activeGroups[i].groupID == groupID) return pm.activeGroups[i];
            return null;
        }

        private static bool IsSequencerAnimating()
        {
            return PowerManager.Instance != null && PowerManager.Instance.IsAnimating;
        }

        private void Show()
        {
            if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
        }

        private void Hide()
        {
            if (panelRoot != null && panelRoot.activeSelf) panelRoot.SetActive(false);
        }

        private void SetTint(Color c)
        {
            if (modeBadge != null) modeBadge.color = c;
        }
    }
}
