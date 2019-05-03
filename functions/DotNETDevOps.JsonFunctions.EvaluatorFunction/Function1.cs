using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace DotNETDevOps.JsonFunctions.EvaluatorFunction
{
    public class ConsolePrintExpressionFunction
    {
        private readonly ConsolePrintExpressionFunctionFactory consolePrintExpressionFunctionFactory;
        private readonly string name;

        public ConsolePrintExpressionFunction(ConsolePrintExpressionFunctionFactory consolePrintExpressionFunctionFactory, string name)
        {
            this.consolePrintExpressionFunctionFactory = consolePrintExpressionFunctionFactory;
            this.name = name ?? throw new ArgumentNullException(nameof(name));
        }
        public JToken PrintFunction(JToken document, JToken[] arguments)
        {
            if (name == "parameters")
            {
                consolePrintExpressionFunctionFactory.Parameters.Add(arguments.FirstOrDefault().ToString());

                if (document.SelectToken($"$.parameters.{arguments.FirstOrDefault().ToString()}") == null)
                {

                }
            }

            consolePrintExpressionFunctionFactory.FoundFunctions.Add(name);

            return $"{name}({string.Join(',', arguments.Select(k => ToString(k)))})";
        }

        private string ToString(JToken k)
        {
            if (k.Type == JTokenType.String)
                return $"'{k.ToString()}'";
            return k.ToString();
        }
    }
    public class ConsolePrintExpressionFunctionFactory : IExpressionFunctionFactory
    {
        public HashSet<string> FoundFunctions = new HashSet<string>();
        public HashSet<string> Parameters = new HashSet<string>();

        public ExpressionParser.ExpressionFunction Get(string name)
        {

            return new ConsolePrintExpressionFunction(this, name).PrintFunction;
        }


    }

    public class EvaluatorFunction
    {
        [FunctionName("EvaluatorFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
             
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var document = JToken.Parse(requestBody);
            
                var factory = new ConsolePrintExpressionFunctionFactory();
                var ex = new ExpressionParser(Options.Create(new ExpressionParserOptions { ThrowOnError = false, Document = document }), log, factory);

            Recursive(ex, document);

            return new OkObjectResult(new { parameters = factory.Parameters, functions = factory.FoundFunctions });
        }
        private void Recursive(ExpressionParser ex, JToken document)
        {
            if (document is JObject jobject)
            {
                foreach (var prop in jobject.Properties())
                {
                    Recursive(ex, prop.Value);
                }
            }
            else if (document is JArray jarray)
            {
                foreach (var value in jarray)
                {
                    Recursive(ex, value);
                }
            }
            else if (document.Type == JTokenType.String && document.ToString().StartsWith("["))
            {
                var token = ex.Evaluate(document.ToString());



                if (token.Type != JTokenType.String)
                {
                    document.Replace(token);

                    Recursive(ex, token);
                }

            }


        }
    }
}
