namespace DotNETDevOps.JsonFunctions
{
    public interface IExpressionFunctionFactory
    {
        ExpressionParser.ExpressionFunction Get(string name);
    }
}
