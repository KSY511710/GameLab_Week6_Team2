using System;
using System.Collections.Generic;
using System.Text;
using Prediction;
using Special.Data;
using Special.Effects;
using Special.Integration;
using Special.Runtime;
using TMPro;
using UnityEngine;

namespace UI.Info
{
    /// <summary>
    /// 단일 정보 패널, 3개 모드(Predict/Built/Special) 스위칭.
    /// - 드래그 중 : PowerPlantPredictor 결과 렌더.
    /// - 일반 블럭/발전소 호버 : GroupInfo.lastTrace 기반 렌더.
    /// - 특수 블럭 호버 : definition + scope + effect 요약 렌더 + 범위 오버레이.
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

        [Header("Scope Overlay (특수 블럭 호버 시 범위 표시)")]
        [Tooltip("범위 오버레이 1셀 스프라이트. 비워두면 흰색 1x1 폴백 사용.")]
        [SerializeField] private Sprite hoverOverlaySprite;
        [SerializeField] private int hoverOverlaySortingOrder = 50;
        [Tooltip("효과가 영향을 주는 셀들의 기본 오버레이 색상 (각 EffectAsset 이 자기 색을 지정하면 우선).")]
        [SerializeField] private Color scopeOverlayColor = new Color(0.7f, 0.35f, 1f, 0.32f);

        private GridManager grid;
        private ScopeOverlayRenderer hoverOverlay;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            hoverOverlay = new ScopeOverlayRenderer(transform, hoverOverlaySprite, hoverOverlaySortingOrder);
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
            hoverOverlay?.Hide();
        }

        private GridManager Grid => grid != null ? grid : (grid = UnityEngine.Object.FindFirstObjectByType<GridManager>());

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

            Vector2Int cell = target.Value.arrayCell;

            // 1) 특수 블럭 우선 조회. BlockData.attribute.specialDef 유실/지연 가능성을 우회해
            //    Registry 를 진실 소스로 삼는다 — Independent role 등 그룹화되지 않는 블럭도 일관되게 잡힌다.
            SpecialBlockInstance inst = SpecialBlockRegistry.InstanceOrNull?.FindByFootprintCell(cell);
            if (inst != null) { RenderSpecialDetail(inst); return; }

            // 2) 일반 블럭 → 그룹 체크.
            GridManager g = Grid;
            if (g == null) { Hide(); return; }
            BlockData block = g.GetBlockAtArrayIndex(cell);
            if (block == null) { Hide(); return; }
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
            hoverOverlay?.Hide();
            if (proj == null) { Hide(); return; }
            Show();
            SetTint(predictTint);

            if (!string.IsNullOrEmpty(proj.blockedReason))
            {
                titleText.text = "🔮 예측";
                bodyText.text = $"<color=#FF8888>{proj.blockedReason}</color>";
                footerText.text = string.Empty;
                return;
            }

            string subject = specialDef != null && !string.IsNullOrEmpty(specialDef.displayName)
                ? specialDef.displayName
                : "이 블럭";
            titleText.text = $"🔮 예측 — {subject}";

            if (!proj.isFormed)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("현재 클러스터: ").Append(proj.currentBlockCount).Append("칸 / ")
                  .Append(proj.currentUniquePartCount).Append("종\n");
                sb.Append("<color=#AAAAAA>발전소 형성 조건: 9칸 이상 · 부품 3종 이상</color>");
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
            footer.Append("  ·  예상 수익: <b>").Append(InfoPanelFormatter.FormatNumber(proj.estimatedMoneyGen)).Append("</b>");
            if (proj.selfContributionPower > 0f)
            {
                footer.Append("\n<color=#AADDFF>+ 자체 기여 ")
                      .Append(InfoPanelFormatter.FormatNumber(proj.selfContributionPower)).Append(" GWh</color>");
            }
            footerText.text = footer.ToString();
        }

        private void RenderBuiltPlant(GroupInfo group)
        {
            hoverOverlay?.Hide();
            Show();
            SetTint(builtTint);

            titleText.text = $"⚡ 발전소 #{group.groupID}";

            if (group.isPowerPlantSolo)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("특수 발전소 (").Append(group.blockSize).Append("칸)\n");
                sb.Append("현재 발전량: <b>")
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
            footer.Append("  ·  수익: <b>").Append(InfoPanelFormatter.FormatNumber(group.estimatedMoneyGen)).Append("</b>");
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
            body.Append("역할: ").Append(def != null ? RoleLabel(def.role) : "?")
                .Append(" · 점유 ").Append(inst.footprint.Count).Append("칸");
            if (inst.groupId > 0) body.Append(" · 그룹 #").Append(inst.groupId);
            body.Append('\n');

            // 효과별 BuildPreview 호출해서 실제 수치(배율/가산치/충족 여부/scalar) 를 본문에 그대로 노출.
            HashSet<Vector2Int> aggScope = new HashSet<Vector2Int>();
            HashSet<Vector2Int> aggImpact = new HashSet<Vector2Int>();
            HashSet<int> impactedGroupIds = new HashSet<int>();
            Color activeOverlayColor = scopeOverlayColor;

            EffectAsset[] assets = def != null ? def.effectAssets : null;
            if (assets != null && assets.Length > 0)
            {
                body.Append("\n<b>효과</b>\n");
                for (int i = 0; i < assets.Length; i++)
                {
                    EffectAsset eff = assets[i];
                    if (eff == null) continue;
                    EffectPreview pv = null;
                    try { pv = eff.BuildPreview(inst); }
                    catch (Exception e) { Debug.LogException(e); }
                    if (pv == null) continue;

                    body.Append("<color=#FFD35A>▸ ").Append(pv.title).Append("</color>\n");
                    if (pv.steps != null)
                    {
                        for (int s = 0; s < pv.steps.Count; s++)
                            body.Append("  ").Append(pv.steps[s]).Append('\n');
                    }

                    if (pv.scopeCells != null)
                        for (int s = 0; s < pv.scopeCells.Count; s++) aggScope.Add(pv.scopeCells[s]);
                    if (pv.impactCells != null)
                        for (int s = 0; s < pv.impactCells.Count; s++) aggImpact.Add(pv.impactCells[s]);
                    if (pv.overlayColor.a > 0.01f) activeOverlayColor = pv.overlayColor;
                }
            }
            else
            {
                body.Append('\n').Append("<color=#AAAAAA>연결된 효과 없음</color>\n");
            }

            // 영향 받는 발전소 집계 — impact 셀이 어느 그룹 멤버인지 교차.
            PowerManager pm = PowerManager.Instance;
            if (pm != null && pm.activeGroups != null && (aggImpact.Count > 0 || aggScope.Count > 0))
            {
                HashSet<Vector2Int> probe = aggImpact.Count > 0 ? aggImpact : aggScope;
                for (int gi = 0; gi < pm.activeGroups.Count; gi++)
                {
                    GroupInfo gp = pm.activeGroups[gi];
                    if (gp == null || gp.clusterPositions == null) continue;
                    for (int mi = 0; mi < gp.clusterPositions.Count; mi++)
                    {
                        if (probe.Contains(gp.clusterPositions[mi])) { impactedGroupIds.Add(gp.groupID); break; }
                    }
                }
            }

            bodyText.text = body.ToString();

            StringBuilder footer = new StringBuilder();
            if (impactedGroupIds.Count > 0)
            {
                footer.Append("영향 발전소: ");
                bool first = true;
                foreach (int id in impactedGroupIds)
                {
                    if (!first) footer.Append(", ");
                    footer.Append('#').Append(id);
                    first = false;
                }
                if (def != null) footer.Append("  ·  ");
            }
            if (def != null)
                footer.Append("설치 한도 ").Append(def.maxPerGame).Append("/").Append(def.maxPerZone);
            footerText.text = footer.ToString();

            // 범위 오버레이: footprint(자기 위치) + scope(영향 범위). scope 가 비어있어도(Global/Zone)
            // footprint 는 항상 표시해 "지금 이 블럭이 여기 있습니다" 를 시각화한다.
            ShowHoverOverlay(inst.footprint, aggScope, activeOverlayColor);
        }

        // ============ Helpers ============

        private void ShowHoverOverlay(IReadOnlyList<Vector2Int> footprint, HashSet<Vector2Int> scope, Color scopeColor)
        {
            GridManager g = Grid;
            if (hoverOverlay == null || g == null) return;

            // footprint 를 먼저 깔고(강한 색), scope 에서 footprint 와 겹치는 셀은 제거한 뒤 같은 렌더러에 이어 붙인다.
            // 현재 ScopeOverlayRenderer 는 Show 시 단일 색상만 지원하므로 footprint 는 별도 호출로 상위 색 덮어쓴다.
            List<Vector2Int> scopeList = new List<Vector2Int>(scope.Count);
            foreach (Vector2Int c in scope)
            {
                if (footprint != null && ListContains(footprint, c)) continue;
                scopeList.Add(c);
            }
            // 우선 scope 먼저 깔고(Hide 는 내부에서 되어있음), footprint 는 같은 pool 을 재사용하므로
            // 한 번의 Show 호출에 합쳐서 색을 footprint 색 우선으로 섞어 보낼 수 없다.
            // 여기서는 scope 를 기본 색으로, footprint 는 별도로 덧칠하는 2단 호출 대신,
            // 단일 리스트에 footprint + scope 를 합쳐 scope 색으로 표시한다.
            // 필요하면 추후 renderer 를 확장해 per-cell color 를 받도록 개선.
            if (footprint != null)
            {
                for (int i = 0; i < footprint.Count; i++) scopeList.Add(footprint[i]);
            }
            if (scopeList.Count == 0) { hoverOverlay.Hide(); return; }
            hoverOverlay.Show(scopeList, scopeColor, g);
        }

        private static bool ListContains(IReadOnlyList<Vector2Int> list, Vector2Int v)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == v) return true;
            return false;
        }

        private static string RoleLabel(SpecialBlockRole role)
        {
            switch (role)
            {
                case SpecialBlockRole.Grouping: return "Grouping";
                case SpecialBlockRole.Independent: return "Independent";
                case SpecialBlockRole.PowerPlant: return "PowerPlant";
                default: return role.ToString();
            }
        }

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
            hoverOverlay?.Hide();
        }

        private void SetTint(Color c)
        {
            if (modeBadge != null) modeBadge.color = c;
        }
    }
}
