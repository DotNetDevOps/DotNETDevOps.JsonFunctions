using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions.CLI
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
        public Task<JToken> PrintFunction(JToken document, JToken[] arguments)
        {
            if (name == "parameters")
            {
                consolePrintExpressionFunctionFactory.Parameters.Add(arguments.FirstOrDefault().ToString());

                if(document.SelectToken($"$.parameters.{arguments.FirstOrDefault().ToString()}") == null)
                {

                }
            }

            consolePrintExpressionFunctionFactory.FoundFunctions.Add(name);

            return Task.FromResult((JToken)$"{name}({string.Join(',', arguments.Select(k => ToString(k)))})");
        }

        private string ToString(JToken k)
        {
            if (k.Type == JTokenType.String)
                return $"'{k.ToString()}'";
            return k.ToString();
        }
    }
    public class ConsolePrintExpressionFunctionFactory : IExpressionFunctionFactory<JToken>
    {
        public HashSet<string> FoundFunctions = new HashSet<string>();
        public HashSet<string> Parameters = new HashSet<string>();

        public ExpressionParser<JToken>.ExpressionFunction Get(string name)
        {
         
            return new ConsolePrintExpressionFunction(this,name).PrintFunction;
        }

         
    }

    [HelpOption]
    class Program
    {
        static Task<int> Main(string[] args)
       => new HostBuilder()
           .ConfigureHostConfiguration(b =>
           {

               Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Debug()
                 .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                 .Enrich.FromLogContext()
                 .WriteTo.Console()
                 .CreateLogger();

           }).ConfigureServices((context, services) =>
           {



           })
          .UseSerilog()
       .RunCommandLineApplicationAsync<Program>(args);

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IApplicationLifetime applicationLifetime, ILogger<Program> logger)
        {

            var tmp = await new HttpClient().GetStringAsync("https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/101-function-app-create-dynamic/azuredeploy.json");
            var document = JToken.Parse(tmp);
            var factory = new ConsolePrintExpressionFunctionFactory();
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken> {  ThrowOnError=false, Document=document}), logger, factory);

            await Recursive(ex, document);

            foreach(var function in factory.FoundFunctions)
            {
                Console.WriteLine(function);
            }
            Console.WriteLine("Parameters:");
            foreach (var function in factory.Parameters)
            {
                Console.WriteLine(function);
            }
            return 0;
        }

        private async Task Recursive(ExpressionParser<JToken> ex, JToken document)
        {
            if(document is JObject jobject)
            {
                foreach(var prop in jobject.Properties())
                {
                    await Recursive(ex, prop.Value);
                }
            }else if(document is JArray jarray)
            {
                foreach(var value in jarray)
                {
                    await Recursive(ex, value);
                }
            }else if(document.Type == JTokenType.String && document.ToString().StartsWith("["))
            {
                var token = await ex.EvaluateAsync(document.ToString());

                

                if(token.Type !=  JTokenType.String)
                {
                    document.Replace(token);

                    await Recursive(ex, token);
                }
                
            }


        }
    }
}
