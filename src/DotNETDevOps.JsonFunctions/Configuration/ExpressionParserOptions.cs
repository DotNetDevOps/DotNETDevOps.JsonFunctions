using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public class ExpressionParserOptions<TContext>
    {
        public bool ThrowOnError { get; set; } = true;
        public TContext Document { get; set; }
    }
}
