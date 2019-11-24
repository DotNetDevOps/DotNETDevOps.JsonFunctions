using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DotNETDevOps.JsonFunctions.UnitTests
{

    public class CorsFunctions : IExpressionFunctionFactory<CorsPolicyBuilder>
    {
        public ExpressionParser<CorsPolicyBuilder>.ExpressionFunction Get(string name)
        {
            //CorsPolicyBuilder().AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
            switch (name)
            {
                case "CorsPolicyBuilder":
                    return CorsPolicyBuilder;
                case "AllowAnyOrigin":
                    return AllowAnyOrigin;
                case "AllowAnyMethod":
                    return AllowAnyMethod;
                case "AllowAnyHeader":
                    return AllowAnyHeader;
                default:
                    throw new NotImplementedException();
            }
        }
        public Task<JToken> CorsPolicyBuilder(ExpressionParser<CorsPolicyBuilder> parser, CorsPolicyBuilder document, JToken[] args)
        {
           // document.AddPolicy(document.DefaultPolicyName,new CorsPolicy());

            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyOrigin(ExpressionParser<CorsPolicyBuilder> parser, CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyOrigin();

            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyMethod(ExpressionParser<CorsPolicyBuilder> parser, CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyMethod();
            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyHeader(ExpressionParser<CorsPolicyBuilder> parser, CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyHeader();
            return Task.FromResult(JToken.FromObject(new { }));
        }
    }
    public class log : ILogger, IDisposable
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }

        public void Dispose()
        {
           
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine(formatter(state, exception));
        }
    }
    public class ExpressionsEngine : DefaultExpressionFunctionFactory<JToken>
    {
        private readonly JToken payload;

        public ExpressionsEngine(JToken Payload)
        {
            Functions["variables"] = GetVariable;
            Functions["payload"] = GetPayload;
            Functions["md5"] = Md5;
            payload = Payload;
        }

        private Task<JToken> Md5(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            return Task.FromResult(arguments.First());
        }

        private Task<JToken> GetPayload(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            return Task.FromResult(payload);
        }

        private Task<JToken> GetVariable(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            return Task.FromResult(document.SelectToken($"$.variables.{arguments.First().ToString()}"));
        }
    }
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var ex = new ExpressionParser<CorsPolicyBuilder>(Options.Create(new ExpressionParserOptions<CorsPolicyBuilder>
            {
                ThrowOnError = false,
                Document = new CorsPolicyBuilder()
            }), new log(), new CorsFunctions());

            var test = await ex.EvaluateAsync("[CorsPolicyBuilder().AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()]");
            var cprs = ex.Document.Build();

        }
        [Fact]
        public async Task Test2()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document= JToken.FromObject(new { variables = new { test = new { helloWorld = "b" } } }),
            }), new log(),new ExpressionsEngine(Payload:"helloWorld"));

            var test = await ex.EvaluateAsync("[variables('test')[payload()]]");

            Assert.Equal("b", test.ToString());
        }

        [Fact]
        public async Task Test3()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { test = new { helloWorld = "b" } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));
            try
            {
                var test = await ex.EvaluateAsync("[md5( payload())]");
                Assert.Equal("helloWorld", test.ToString());
            }
            catch(Exception exx)
            {

            }

            
        }

    }
}
