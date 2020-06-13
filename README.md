# DotNetDevOps Expression Parser for Json Documents

This libray makes it possible to define your own expression functions and parse expressions from your json document.

# Supported expressions operations

* NullCondition Operator | ?. or ?[ accepts and return null if the .property or ['property'] is null
* Custom Functions | You define your own functions 'myfunction' as a lambda function that takes the document, arguments and return a new jtoken.
* . and [ notation for property lookup
* [] array indexing


# How to use

```
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
        public async Task Test7()
        {
            var ex = new ExpressionParser<JToken>(Options.Create(new ExpressionParserOptions<JToken>
            {
                ThrowOnError = false,
                Document = JToken.FromObject(new { variables = new { testvariable = new { test = new { nested = "b" } } } }),
            }), new log(), new ExpressionsEngine(Payload: "helloWorld"));

            var test = await ex.EvaluateAsync("[variables('testvariable')?['test2'].nested]");

            Assert.Null(test);


        }
```
