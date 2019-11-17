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
        public Task<JToken> CorsPolicyBuilder(CorsPolicyBuilder document, JToken[] args)
        {
           // document.AddPolicy(document.DefaultPolicyName,new CorsPolicy());

            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyOrigin(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyOrigin();

            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyMethod(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyMethod();
            return Task.FromResult(JToken.FromObject(new { }));
        }
        public Task<JToken> AllowAnyHeader(CorsPolicyBuilder document, JToken[] args)
        {
            document.AllowAnyHeader();
            return Task.FromResult(JToken.FromObject(new { }));
        }
    }
    public class log : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
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
    public class MyFac : DefaultExpressionFunctionFactory<JToken>
    {
        private readonly JToken payload;

        public MyFac(JToken Payload)
        {
            Functions["variables"] = GetVariable;
            Functions["payload"] = GetPayload;
            payload = Payload;
        }

        private Task<JToken> GetPayload(JToken document, JToken[] arguments)
        {
            return Task.FromResult(payload);
        }

        private Task<JToken> GetVariable(JToken document, JToken[] arguments)
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
                Document= JToken.FromObject(new { variables = new { test = new { helloWorld="b"} } } )
            }), new log(),new MyFac("helloWorld"));

            var test = await ex.EvaluateAsync("[variables('test')[payload()]]");

            Assert.Equal("b", test.ToString());
        }
    }
}
