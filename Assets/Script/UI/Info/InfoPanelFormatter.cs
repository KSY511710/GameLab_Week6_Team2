using System.Collections.Generic;
using System.Text;
using Special.Runtime;

namespace UI.Info
{
    /// <summary>
    /// CalculationTrace 를 정보 패널/시퀀서가 공유하는 단계별 요약 텍스트로 변환한다.
    /// 재사용 지점: 예측/호버/시퀀서 3곳 모두 이 포맷터를 거쳐 동일한 레이아웃을 얻는다.
    /// </summary>
    public static class InfoPanelFormatter
    {
        /// <summary>시퀀서/패널이 공통으로 사용하는 단계 진행 순서.</summary>
        public static readonly CalcStage[] ProgressionStages =
        {
            CalcStage.Base,
            CalcStage.UniqueParts,
            CalcStage.BaseCompletion,
            CalcStage.ShapeCompletion,
            CalcStage.ColorMultiplier,
            CalcStage.FinalMultiplier
        };

        public static string BuildBody(CalculationTrace trace)
        {
            if (trace == null || trace.Steps.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder(256);
            for (int s = 0; s < ProgressionStages.Length; s++)
            {
                string section = BuildStageSection(trace, ProgressionStages[s]);
                if (string.IsNullOrEmpty(section)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(section);
            }

            string finalLine = BuildFinalLine(trace);
            if (!string.IsNullOrEmpty(finalLine))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(finalLine);
            }
            return sb.ToString();
        }

        /// <summary>단일 단계만 포맷해서 돌려준다. 해당 단계에 기록이 없으면 빈 문자열.</summary>
        public static string BuildStageSection(CalculationTrace trace, CalcStage stage)
        {
            if (trace == null) return string.Empty;
            List<CalculationStep> steps = CollectStage(trace, stage);
            if (steps.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder(128);
            sb.Append("<b>").Append(StageTitle(stage)).Append("</b>\n");
            AppendStageLines(sb, steps);
            // 끝의 줄바꿈 제거해 호출자가 구분자를 마음대로 붙이도록.
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--;
            return sb.ToString();
        }

        /// <summary>최종 단계를 강조색으로 포맷. 기록이 없으면 빈 문자열.</summary>
        public static string BuildFinalLine(CalculationTrace trace)
        {
            CalculationStep? final = FindFinal(trace);
            if (!final.HasValue) return string.Empty;
            StringBuilder sb = new StringBuilder(64);
            sb.Append("<color=#FFD35A><b>▶ ")
              .Append(final.Value.label)
              .Append(" = ")
              .Append(FormatNumber(final.Value.value))
              .Append(" GWh</b></color>");
            return sb.ToString();
        }

        public static string StageTitle(CalcStage stage)
        {
            switch (stage)
            {
                case CalcStage.Base: return "기본 생산량";
                case CalcStage.UniqueParts: return "부품 종류";
                case CalcStage.BaseCompletion: return "기본 완성도";
                case CalcStage.ShapeCompletion: return "모양 완성도";
                case CalcStage.ColorMultiplier: return "색상 순도";
                case CalcStage.FinalMultiplier: return "최종 배율";
                case CalcStage.Exchange: return "환전 비율";
                default: return stage.ToString();
            }
        }

        private static List<CalculationStep> CollectStage(CalculationTrace trace, CalcStage stage)
        {
            List<CalculationStep> list = new List<CalculationStep>();
            for (int i = 0; i < trace.Steps.Count; i++)
            {
                if (trace.Steps[i].stage == stage) list.Add(trace.Steps[i]);
            }
            return list;
        }

        private static void AppendStageLines(StringBuilder sb, List<CalculationStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                CalculationStep step = steps[i];
                sb.Append("  ");
                switch (step.op)
                {
                    case CalcOp.Raw:
                        sb.Append("• ").Append(FormatNumber(step.value));
                        break;
                    case CalcOp.Add:
                        sb.Append("• +").Append(FormatNumber(step.value));
                        if (!string.IsNullOrEmpty(step.source))
                            sb.Append(" <color=#AADDFF>(").Append(step.source).Append(")</color>");
                        sb.Append(" → ").Append(FormatNumber(step.after));
                        break;
                    case CalcOp.Mul:
                        sb.Append("• ×").Append(FormatNumber(step.value));
                        if (!string.IsNullOrEmpty(step.source))
                            sb.Append(" <color=#AADDFF>(").Append(step.source).Append(")</color>");
                        sb.Append(" → ").Append(FormatNumber(step.after));
                        break;
                }
                sb.Append('\n');
            }
        }

        private static CalculationStep? FindFinal(CalculationTrace trace)
        {
            for (int i = trace.Steps.Count - 1; i >= 0; i--)
            {
                if (trace.Steps[i].stage == CalcStage.Final) return trace.Steps[i];
            }
            return null;
        }

        public static string FormatNumber(float v)
        {
            if (v == (int)v) return ((int)v).ToString();
            return v.ToString("0.##");
        }
    }
}
