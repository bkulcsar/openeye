namespace OpenEye.Shared.Models;

public record ConditionConfig(
    string Type,
    string? Operator = null,
    double? Value = null,
    string? FeatureName = null);
