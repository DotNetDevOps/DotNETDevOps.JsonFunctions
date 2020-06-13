using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public class ConstantEvaluator : IJTokenEvaluator
    {
        private string k;

        public ConstantEvaluator(string k)
        {
            this.k = k;
        }

        public Task<JToken> EvaluateAsync()
        {
            return Task.FromResult(JToken.Parse(k));
        }
    }
}
