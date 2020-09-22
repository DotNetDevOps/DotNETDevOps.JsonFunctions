# DotNetDevOps Expression Parser for Json Documents

This libray makes it possible to define your own expression functions and parse expressions from your json document.

# Supported expressions operations

* NullCondition Operator | ?. or ?[ accepts and return null if the .property or ['property'] is null
* Custom Functions | You define your own functions 'myfunction' as a lambda function that takes the document, arguments and return a new jtoken.
* . and [ notation for property lookup
* [] array indexing


# Examples

```
  public class ExpressionsEngine : DefaultExpressionFunctionFactory<JToken>
    {
        private readonly JToken payload;

        public ExpressionsEngine(JToken Payload)
        {
            Functions["variables"] = GetVariable;
            Functions["payload"] = GetPayload;
            Functions["md5"] = Md5;
            Functions["merge"] = Merge;
            Functions["select"] = Select;
            Functions["dummy"] = Dummy;
            Functions["lookup"] = (_, __, ___) => Task.FromResult<JToken>(null);
            Functions["body"] = (_, __, ___) => Task.FromResult<JToken>(null);
            Functions["in"] = InExpressionFunction;
            Functions["xpath"] = (_,__,___)=> Task.FromResult<JToken>(null);
            Functions["if"] = (_, __, ___) => Task.FromResult<JToken>(___[0].ToObject<bool>()?___[1]:___[2]);
            Functions["concat"] = (_, __, ___) => Task.FromResult<JToken>(string.Join("",___.Select(c=>c.ToString())));
            payload = Payload;
        }
        private Task<JToken> InExpressionFunction(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            return Task.FromResult<JToken>(arguments[1].Any(el => el.Equals(arguments.First())));
        }

        private Task<JToken> Dummy(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            return Task.FromResult<JToken>("entity");
        }

        private Task<JToken> Select(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            var item = arguments.FirstOrDefault();
            if (item is JObject obj)
            {
                return Task.FromResult(JToken.FromObject(arguments.Skip(1).ToDictionary(k => k.ToString(), a => obj[a.ToString()])));
            }
            throw new NotImplementedException();
        }

        private Task<JToken> Merge(ExpressionParser<JToken> parser, JToken document, JToken[] arguments)
        {
            var first = new JObject();
            foreach (JObject obj in arguments)
            {
                foreach (var prop in obj.Properties())
                    first[prop.Name] = prop.Value;
            }
            return Task.FromResult(first as JToken);
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
        [Fact]
        public async Task Test4()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { test = new { helloWorld = "b" } } }),
            }), new log(), new ExpressionsEngine(Payload: new JObject(new JProperty( "helloWorld","a"))));
            try
            {
                var test = await ex.EvaluateAsync("[merge(select(payload(1),'seqno','data','port','fcnt','freq','dr'),select(payload(0),'rssi','snr'))]");
                Assert.Equal("helloWorld", test.ToString());
            }
            catch (Exception exx)
            {

            }


        }

        [Fact]
        public async Task Test5()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { testvariable = new { test = new { nested = "b" } } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[variables('testvariable')['test'].nested]");

            Assert.Equal("b", test.ToString());


        }

        [Fact]
        public async Task Test6()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { testvariable = new { test = new { nested = "b" } } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[variables('testvariable')['test2']?.nested]");

            Assert.Null(test);


        }
 
        [Fact]
        public async Task Test8()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { forms = new { entity = new { attribute = new { main = new { disabled=false} } } } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[variables('forms')[dummy()]?['attribute']['main'].disabled]");

            Assert.False(test?.ToObject<bool>());


        }

        [Fact]
        public async Task Test9()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { forms = new { entity = new { attribute = new { main2 = new { disabled = false } } } } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[variables('forms')?[dummy()]?['attribute']?['main']?.disabled]");

            Assert.Null(test);


        }
       

        [Fact]
        public async Task Test11()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
               
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[in(9,[9, 10, 11, 15, 16, 20, 21])]");
             
            Assert.True(test.ToObject<bool>());


        }

        [Fact]
        public async Task Test12()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,

            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[concat('test = ','another','')]");

            Assert.Equal("test = another",test?.ToString());


        }

        [Fact]
        public async Task Test13()
        {
            
 
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,

            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[concat('test = ','''anot''her''','123')]");

            Assert.Equal("test = 'anot'her'123", test?.ToString());


        }
        [Fact]
        public async Task Test14()
        {


 
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,

            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[concat('test = ','\\'anot\\'her\\'','123')]");

            Assert.Equal("test = 'anot'her'123", test?.ToString());


        }

        [Fact]
        public async Task Test15()
        {
 
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,

            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[if(true,null,'hello')]");

            Assert.Equal(JTokenType.Null, test.Type);


        }
```
