namespace DotNETDevOps.JsonFunctions
{
    public interface IExpressionFunctionFactory<TContext>
    {
        ExpressionParser<TContext>.ExpressionFunction Get(string name);
    }
}
