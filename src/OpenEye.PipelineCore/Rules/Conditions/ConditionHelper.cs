namespace OpenEye.PipelineCore.Rules.Conditions;

internal static class ConditionHelper
{
    public static bool Compare(double actual, string? op, double? threshold)
    {
        if (threshold is null) return false;
        return op switch
        {
            ">" => actual > threshold.Value,
            ">=" => actual >= threshold.Value,
            "<" => actual < threshold.Value,
            "<=" => actual <= threshold.Value,
            "==" => Math.Abs(actual - threshold.Value) < 0.001,
            _ => false
        };
    }
}
