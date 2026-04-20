using System.Collections.Generic;

namespace Special.Runtime
{
    /// <summary>
    /// 발전소 전력식 단계를 의미 단위로 분류. 정보 패널과 시퀀서가 단계별로 묶어 출력한다.
    /// </summary>
    public enum CalcStage
    {
        Base,              // baseProduction
        UniqueParts,       // uniqueParts
        BaseCompletion,    // 기본 완성(= 기존 상수 2)
        ShapeCompletion,   // 모양 완성(= shapeBonus)
        ColorMultiplier,   // 색상 순도 배율
        FinalMultiplier,   // 최종 곱셈
        Exchange,          // 환전 비율
        Final              // 최종 결과(마무리 1줄)
    }

    public enum CalcOp
    {
        Raw,   // 초기값
        Add,   // before + delta → after
        Mul    // before * factor → after
    }

    /// <summary>
    /// 한 번의 +/× 조작 기록. source 가 비어 있으면 "순수 규칙" (예: FormationDetector 가 센 shapeBonus).
    /// </summary>
    public readonly struct CalculationStep
    {
        public readonly CalcStage stage;
        public readonly CalcOp op;
        public readonly string label;
        public readonly string source;
        public readonly float before;
        public readonly float value;  // Raw=value, Add=delta, Mul=factor
        public readonly float after;

        public CalculationStep(CalcStage stage, CalcOp op, string label, string source, float before, float value, float after)
        {
            this.stage = stage;
            this.op = op;
            this.label = label;
            this.source = source;
            this.before = before;
            this.value = value;
            this.after = after;
        }
    }

    /// <summary>
    /// PowerCalculationContext.Trace 에 주입되는 기록기. 계산 과정의 단계 및 효과 누적을 순서대로 기록해
    /// 정보 패널/시퀀서가 동일한 소스를 공유하도록 한다.
    /// </summary>
    public sealed class CalculationTrace
    {
        public readonly List<CalculationStep> Steps = new List<CalculationStep>();

        public void RecordRaw(CalcStage stage, string label, float value)
        {
            Steps.Add(new CalculationStep(stage, CalcOp.Raw, label, null, 0f, value, value));
        }

        public void RecordAdd(CalcStage stage, string label, string source, float before, float delta)
        {
            Steps.Add(new CalculationStep(stage, CalcOp.Add, label, source, before, delta, before + delta));
        }

        public void RecordMul(CalcStage stage, string label, string source, float before, float factor)
        {
            Steps.Add(new CalculationStep(stage, CalcOp.Mul, label, source, before, factor, before * factor));
        }

        public void RecordFinal(string label, float value)
        {
            Steps.Add(new CalculationStep(CalcStage.Final, CalcOp.Raw, label, null, 0f, value, value));
        }
    }
}
