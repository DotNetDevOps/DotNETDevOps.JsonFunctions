using System.Collections.Generic;

namespace DotNETDevOps.JsonFunctions
{
    public class DefaultExpressionFunctionFactory : IExpressionFunctionFactory
    {
       
        public Dictionary<string, ExpressionParser.ExpressionFunction> Functions { get; set; } = new Dictionary<string, ExpressionParser.ExpressionFunction>();

        public ExpressionParser.ExpressionFunction Get(string name)
        {
            return Functions[name];
        }
    }
}
