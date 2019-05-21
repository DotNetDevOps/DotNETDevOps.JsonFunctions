using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class ConstantEvaluator : IJTokenEvaluator
    {
        private string k;

        public ConstantEvaluator(string k)
        {
            this.k = k;
        }

        public JToken Evaluate()
        {
            return JToken.Parse(k);
        }
    }
}
