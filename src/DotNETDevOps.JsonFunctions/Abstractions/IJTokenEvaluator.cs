using Newtonsoft.Json.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public interface IJTokenEvaluator
    {
        JToken Evaluate();
    }
}
