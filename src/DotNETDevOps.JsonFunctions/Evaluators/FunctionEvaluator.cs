using Newtonsoft.Json.Linq;
using System.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class FunctionEvaluator : IJTokenEvaluator
    {
        private string name;
        private IJTokenEvaluator[] parameters;
        private IExpressionParser evaluator;
        public FunctionEvaluator(IExpressionParser evaluator, string name, IJTokenEvaluator[] parameters)
        {
            this.name = name;
            this.parameters = parameters;
            this.evaluator = evaluator;
        }

        public JToken Evaluate()
        {
            return evaluator.Evaluate(name, parameters.Select(p => p.Evaluate()).ToArray());
        }


    }
}
