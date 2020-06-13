using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public class ArrayIndexLookup : IJTokenEvaluator
    {
        public string parsedText;

        public ArrayIndexLookup(string parsedText, Sprache.IOption<char> optionalFirst)
        {
            this.parsedText = parsedText;
        }

        public IJTokenEvaluator ArrayEvaluator { get; set; }

        public async Task<JToken> EvaluateAsync()
        {
            return (await ArrayEvaluator.EvaluateAsync())[int.Parse(parsedText)];
        }
    }
}
