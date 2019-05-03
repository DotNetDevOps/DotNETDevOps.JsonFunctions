﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNETDevOps.JsonFunctions
{
    public interface IJTokenEvaluator
    {
        JToken Evaluate();
    }
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

        public JToken Evaluate()
        {
            var token = Object.Evaluate();
            if(token is JObject jobject)
                return jobject[propertyName];

            if (throwOnError)
                throw new Exception("Cant look up property on none object");

            return $"{token.ToString()}.{propertyName}";
        }
    }

    public class StringConstantEvaluator : IJTokenEvaluator
    {
        private string text;

        public StringConstantEvaluator(string text)
        {
            this.text = text;
        }

        public JToken Evaluate()
        {
            return text;
        }
    }
    public class DecimalConstantEvaluator : IJTokenEvaluator
    {
        private decimal @decimal;

        public DecimalConstantEvaluator(decimal @decimal)
        {
            this.@decimal = @decimal;
        }

        public JToken Evaluate()
        {
            return JToken.FromObject(@decimal);
        }
    }
    public class ConstantEvaluator : IJTokenEvaluator
    {
        private string k;

        public ConstantEvaluator(string k)
        {
            this.k = k;
        }

        public JToken Evaluate()
        {
            return JToken.Parse(k);
        }
    }
    public class ArrayIndexLookup : IJTokenEvaluator
    {
        public string parsedText;

        public ArrayIndexLookup(string parsedText)
        {
            this.parsedText = parsedText;
        }

        public IJTokenEvaluator ArrayEvaluator { get; set; }

        public JToken Evaluate()
        {
            return ArrayEvaluator.Evaluate()[int.Parse(parsedText)];
        }
    }
    public interface IExpressionFunctionFactory
    {
        ExpressionParser.ExpressionFunction Get(string name);
    }
    public class DefaultExpressionFunctionFactory : IExpressionFunctionFactory
    {
       
        public Dictionary<string, ExpressionParser.ExpressionFunction> Functions { get; set; } = new Dictionary<string, ExpressionParser.ExpressionFunction>();

        public ExpressionParser.ExpressionFunction Get(string name)
        {
            return Functions[name];
        }
    }
    public class ExpressionParserOptions
    {
        public bool ThrowOnError { get; set; } = true;
        public JToken Document { get; set; }
    }
    public class ExpressionParser
    {

        public delegate JToken ExpressionFunction(JToken document, JToken[] arguments);

        public readonly Parser<IJTokenEvaluator> Function;
        public readonly Parser<IJTokenEvaluator> Constant;
        public readonly Parser<IJTokenEvaluator> ArrayIndexer;
        public readonly Parser<IJTokenEvaluator> PropertyAccess;
        public readonly Parser<IJTokenEvaluator[]> Tokenizer;

        private static readonly Parser<char> DoubleQuote = Parse.Char('"');
        private static readonly Parser<char> SingleQuote = Parse.Char('\'');
        private static readonly Parser<char> Backslash = Parse.Char('\\');

        private static readonly Parser<char> QdText =
            Parse.AnyChar.Except(DoubleQuote);
        private static readonly Parser<char> QdText1 =
            Parse.AnyChar.Except(SingleQuote);

        //private static readonly Parser<char> QuotedPair =
        //    from _ in Backslash
        //    from c in Parse.AnyChar
        //    select c;

        private static readonly Parser<StringConstantEvaluator> QuotedString =
            from open in DoubleQuote
            from text in QdText.Many().Text()
            from close in DoubleQuote
            select new StringConstantEvaluator(text);

        private static readonly Parser<StringConstantEvaluator> QuotedSingleString =
           from open in SingleQuote
           from text in QdText1.Many().Text()
           from close in SingleQuote
           select new StringConstantEvaluator(text);

       // public Dictionary<string, Func<JToken, JToken[], JToken>> Functions { get; set; } = new Dictionary<string, Func<JToken, JToken[], JToken>>();

        private readonly Parser<IJTokenEvaluator> Number = from op in Parse.Optional(Parse.Char('-').Token())
                                                           from num in Parse.Decimal
                                                           from trailingSpaces in Parse.Char(' ').Many()
                                                           select new DecimalConstantEvaluator(decimal.Parse(num) * (op.IsDefined ? -1 : 1));
        private readonly IOptions<ExpressionParserOptions> options;
        private readonly ILogger logger;
        private readonly IExpressionFunctionFactory functions;

        public JToken Document => this.options.Value.Document;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public ExpressionParser(IOptions<ExpressionParserOptions> options, ILogger logger, IExpressionFunctionFactory functions)
        {
            Constant = Parse.LetterOrDigit.AtLeastOnce().Text().Select(k => new ConstantEvaluator(k));

            Tokenizer = from expr in Parse.Ref(() => Parse.Ref(() => (Function.Or(Number).Or(QuotedString).Or(QuotedSingleString).Or(Constant)).Or(ArrayIndexer).Or(PropertyAccess)).AtLeastOnce()).Optional().DelimitedBy(Parse.Char(',').Token())
                        select FixArrayIndexers(expr.Select(c => (c.GetOrDefault() ?? Enumerable.Empty<IJTokenEvaluator>()).ToArray()).ToArray());

            Function = from name in Parse.Letter.AtLeastOnce().Text()
                       from lparen in Parse.Char('(')
                       from expr in Tokenizer
                       from rparen in Parse.Char(')')
                       select CallFunction(name, expr);

            PropertyAccess = from first in Parse.Char('.')
                             from propertyName in Parse.LetterOrDigit.AtLeastOnce().Text()
                             select new ObjectLookup(propertyName,options.Value.ThrowOnError);

            ArrayIndexer = from first in Parse.Char('[')
                           from text in Parse.Number
                           from last in Parse.Char(']')
                           select new ArrayIndexLookup(text); ;
            this.options = options;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.functions = functions ?? throw new ArgumentNullException(nameof(functions));
        }

        private IJTokenEvaluator[] FixArrayIndexers(IJTokenEvaluator[][] enumerable)
        {
            return enumerable.Where(c => c.Any()).Select(c => ArrayLookup(c)).ToArray();
        }

        private IJTokenEvaluator ArrayLookup(IJTokenEvaluator[] c)
        {
            if (c.Length == 1)
                return c.First();

            if (c.Length == 2 && c[1] is ArrayIndexLookup looup)
            {
                looup.ArrayEvaluator = c[0];
                return looup;
            }

            if (c.Length == 2 && c[1] is ObjectLookup lookup)
            {
                lookup.Object = c[0];
                return lookup;
            }

            return null;

        }

        public JToken Evaluate(string name, params JToken[] arguments)
        {
            var function = functions.Get(name);
            if(function == null)
            {
                throw new Exception($"{name} not found in functions");
            }
           

            var value =function(Document, arguments);


            return value;
        }

        IJTokenEvaluator CallFunction(string name, IJTokenEvaluator[] parameters)
        {
            return new FunctionEvaluator(this, name, parameters);
        }

        public JToken Evaluate(string str)
        {
            var value = EvaluateImp(str);
            logger.LogInformation("Evaluating '{str}' to '{value}'", str, value.ToString());
            return value;

        }

        private JToken EvaluateImp(string str)
        {
            Parser<IJTokenEvaluator[]> stringParser =
                 from first in Parse.Char('[')
                 from evaluator in Tokenizer
                 from last in Parse.Char(']')
                 select evaluator;



            var func = stringParser.Parse(str).ToArray();
            if (func.Length == 1)
                return func.First().Evaluate();

            for (var i = 0; i < func.Length; i++)
            {
                if (func[i] is ArrayIndexLookup array)
                {
                    var arrayToken = func[i - 1].Evaluate();
                    if (arrayToken.Type != JTokenType.Array)
                        throw new Exception("not an array");

                    return arrayToken[int.Parse(array.parsedText)];

                }
                else if (func[i] is ObjectLookup objectLookup)
                {
                    var arrayToken = func[i - 1].Evaluate();
                    if (arrayToken.Type != JTokenType.Object)
                        throw new Exception("not an object");

                    return arrayToken[objectLookup.propertyName];

                }
            }

            return null;
        }
    }
    public class FunctionEvaluator : IJTokenEvaluator
    {
        private string name;
        private IJTokenEvaluator[] parameters;
        private ExpressionParser evaluator;
        public FunctionEvaluator(ExpressionParser evaluator, string name, IJTokenEvaluator[] parameters)
        {
            this.name = name;
            this.parameters = parameters;
            this.evaluator = evaluator;
        }

        public JToken Evaluate()
        {
            return evaluator.Evaluate(name, parameters.Select(p => p.Evaluate()).ToArray());
        }


    }
}
