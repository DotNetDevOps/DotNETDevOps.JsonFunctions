using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public interface IJTokenEvaluator
    {
        Task<JToken> EvaluateAsync();
    }
}
