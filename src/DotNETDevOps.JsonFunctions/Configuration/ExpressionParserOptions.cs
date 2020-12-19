using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public interface IExpressionFunctionCache
    {
        ValueTask<JToken> GetOrAdd(string key, Func<string, ValueTask<JToken>> p);
    }
    public class ExpressionParserOptions<TContext>
    {
        public bool ThrowOnError { get; set; } = true;
        public TContext Document { get; set; }
        public bool EnableFunctionEvaluationCaching { get; set; } = false;
        public IExpressionFunctionCache Cache { get; set; }
    }
}
