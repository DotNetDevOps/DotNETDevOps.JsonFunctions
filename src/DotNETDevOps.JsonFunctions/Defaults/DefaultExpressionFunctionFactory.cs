using System.Collections.Generic;

namespace DotNETDevOps.JsonFunctions
{
    public class DefaultExpressionFunctionFactory<TContext> : IExpressionFunctionFactory<TContext>
    {
       
        public Dictionary<string, ExpressionParser<TContext>.ExpressionFunction> Functions { get; set; } = new Dictionary<string, ExpressionParser<TContext>.ExpressionFunction>();

        public ExpressionParser<TContext>.ExpressionFunction Get(string name)
        {
            return Functions[name];
        }
    }
}
