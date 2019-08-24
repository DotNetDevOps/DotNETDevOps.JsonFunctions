using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<JToken> EvaluateAsync()
        {
            if(Object!=null)
                return await evaluator.EvaluateAsync(name, await Task.WhenAll(new[] { Object.EvaluateAsync() }.Concat(parameters.Select(p => p.EvaluateAsync()))));

            return await evaluator.EvaluateAsync(name, await Task.WhenAll(parameters.Select(p => p.EvaluateAsync())));
        }

        public IJTokenEvaluator Object { get; internal set; }
    }
}
