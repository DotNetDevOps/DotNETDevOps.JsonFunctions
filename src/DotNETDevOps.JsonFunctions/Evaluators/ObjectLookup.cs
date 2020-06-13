using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    internal class ObjectLookup : IJTokenEvaluator, IObjectHolder
    {
        public string propertyName;
       
        private readonly bool throwOnError;

        public ObjectLookup(string propertyName, Sprache.IOption<char> optionalFirst, bool throwOnError)
        {
            this.propertyName = propertyName.Trim('\'');
         
            this.throwOnError = throwOnError;
            NullConditional = optionalFirst.IsDefined;
        }

        public IJTokenEvaluator Object { get;  set; }
        public bool NullConditional { get; set; }

        public async Task<JToken> EvaluateAsync()
        {
           
            var token = await Object.EvaluateAsync();
            if (token is null || token.Type == JTokenType.Null && NullConditional)
                return token;

            if (token is JObject jobject)
            {
                
                return jobject[propertyName];
            }

            if (throwOnError)
                throw new Exception("Cant look up property on none object");

            return $"{token.ToString()}.{propertyName}";
        }
    }
}
