using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    internal class ObjectLookup : IJTokenEvaluator
    {
        public string propertyName;
        private readonly bool throwOnError;

        public ObjectLookup(string propertyName, bool throwOnError)
        {
            this.propertyName = propertyName;
            this.throwOnError = throwOnError;
        }

        public IJTokenEvaluator Object { get; internal set; }

        public async Task<JToken> EvaluateAsync()
        {
            var token = await Object.EvaluateAsync();
            if(token is JObject jobject)
                return jobject[propertyName];

            if (throwOnError)
                throw new Exception("Cant look up property on none object");

            return $"{token.ToString()}.{propertyName}";
        }
    }
}
