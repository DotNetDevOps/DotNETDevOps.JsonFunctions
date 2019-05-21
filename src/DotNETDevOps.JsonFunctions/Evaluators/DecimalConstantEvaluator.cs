using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class DecimalConstantEvaluator : IJTokenEvaluator
    {
        private decimal @decimal;

        public DecimalConstantEvaluator(decimal @decimal)
        {
            this.@decimal = @decimal;
        }

        public JToken Evaluate()
        {
            return JToken.FromObject(@decimal);
        }
    }
}
