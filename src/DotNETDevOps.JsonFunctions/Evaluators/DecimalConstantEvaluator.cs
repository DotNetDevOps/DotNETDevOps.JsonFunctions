using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public class DecimalConstantEvaluator : IJTokenEvaluator
    {
        private decimal @decimal;

        public DecimalConstantEvaluator(decimal @decimal)
        {
            this.@decimal = @decimal;
        }

        public Task<JToken> EvaluateAsync()
        {
            return Task.FromResult(JToken.FromObject(@decimal));
        }
    }
}
