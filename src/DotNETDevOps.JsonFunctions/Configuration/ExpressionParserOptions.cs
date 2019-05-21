using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class ExpressionParserOptions
    {
        public bool ThrowOnError { get; set; } = true;
        public JToken Document { get; set; }
    }
}
