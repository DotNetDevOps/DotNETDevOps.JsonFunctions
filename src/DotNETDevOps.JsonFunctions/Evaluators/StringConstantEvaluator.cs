using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public class StringConstantEvaluator : IJTokenEvaluator
    {
        private string text;

        public StringConstantEvaluator(string text)
        {
            this.text = text;
        }

        public Task<JToken> EvaluateAsync()
        {
            return Task.FromResult((JToken)text);
        }
    }
}
