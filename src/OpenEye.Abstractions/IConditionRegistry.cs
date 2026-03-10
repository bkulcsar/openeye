namespace OpenEye.Abstractions;

public interface IConditionRegistry
{
    IRuleCondition Get(string type);
    void Register(IRuleCondition condition);
}
