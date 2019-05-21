using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class StringConstantEvaluator : IJTokenEvaluator
    {
        private string text;

        public StringConstantEvaluator(string text)
        {
            this.text = text;
        }

        public JToken Evaluate()
        {
            return text;
        }
    }
}
