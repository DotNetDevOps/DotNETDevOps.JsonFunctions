using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public interface IExpressionParser
    {
        Task<JToken> EvaluateAsync(string name, params JToken[] arguments);
    }

    internal class ChildExpressionParser<TContext> : IJTokenEvaluator
    {
        private readonly IJTokenEvaluator[] childs;
        private readonly bool throwOnError;

        public ChildExpressionParser (IJTokenEvaluator[] childs, bool throwOnError)
        {
            this.childs = childs;
            this.throwOnError = throwOnError;
        }

        public IJTokenEvaluator Object { get; internal set; }

        public async Task<JToken> EvaluateAsync()
        {
            var propertyName = await childs[0].EvaluateAsync();

            var token = await Object.EvaluateAsync();
            if (token is JObject jobject)
                return jobject[propertyName.ToString()];

            if (throwOnError)
                throw new Exception("Cant look up property on none object");

            return $"{token.ToString()}.{propertyName}";

             
        }
    }
    public class ExpressionParser<TContext> : IExpressionParser
    {

        public delegate Task<JToken> ExpressionFunction(ExpressionParser<TContext> parser, TContext document, JToken[] arguments);

        public readonly Parser<IJTokenEvaluator> Function;
        public readonly Parser<IJTokenEvaluator> Constant;
        public readonly Parser<IJTokenEvaluator> ArrayIndexer;
        public readonly Parser<IJTokenEvaluator> PropertyAccessByDot;
        public readonly Parser<IJTokenEvaluator> PropertyAccessByBracket;
        public readonly Parser<IJTokenEvaluator> ChildAccessByBracket;
        
        public readonly Parser<IJTokenEvaluator> ObjectFunction;        
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
        private readonly IOptions<ExpressionParserOptions<TContext>> options;
        private readonly ILogger logger;
        private readonly IExpressionFunctionFactory<TContext> functions;

        public TContext Document => this.options.Value.Document;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public ExpressionParser(IOptions<ExpressionParserOptions<TContext>> options, ILogger logger, IExpressionFunctionFactory<TContext> functions)
        {
            Constant = Parse.LetterOrDigit.AtLeastOnce().Text().Select(k => new ConstantEvaluator(k));

            Tokenizer = from expr in Parse.Ref(() => Parse.Ref(() => (Function.Or(Number).Or(QuotedString).Or(QuotedSingleString).Or(Constant)).Or(ArrayIndexer).Or(ObjectFunction).Or(PropertyAccessByDot).Or(PropertyAccessByBracket).Or(ChildAccessByBracket)).AtLeastOnce()).Optional().DelimitedBy(Parse.Char(',').Token())
                        select FixArrayIndexers(expr.Select(c => (c.GetOrDefault() ?? Enumerable.Empty<IJTokenEvaluator>()).ToArray()).ToArray());

            Function = from name in Parse.Letter.AtLeastOnce().Text()
                       from lparen in Parse.Char('(')
                       from expr in Tokenizer
                       from rparen in Parse.Char(')')
                       select CallFunction(name, expr);

            PropertyAccessByDot = from first in Parse.Char('.')
                             from propertyName in Parse.LetterOrDigit.AtLeastOnce().Text()
                             select new ObjectLookup(propertyName,options.Value.ThrowOnError);
            PropertyAccessByBracket = from first in Parse.Char('[')
                                      from propertyName in (Parse.LetterOrDigit.Or(Parse.Char('\'')).AtLeastOnce().Text())
                                      from last in Parse.Char(']')
                                      select new ObjectLookup(propertyName, options.Value.ThrowOnError);

            ChildAccessByBracket = from first in Parse.Char('[')
                                   from propertyName in Tokenizer
                                   from last in Parse.Char(']')
                                   select new ChildExpressionParser<TContext>(propertyName, options.Value.ThrowOnError);


            ObjectFunction = from first in Parse.Char('.')
                                 from name in Parse.LetterOrDigit.AtLeastOnce().Text()
                                 from lparen in Parse.Char('(')
                                 from expr in Tokenizer
                                 from rparen in Parse.Char(')')
                                 select CallFunction(name, expr);

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
            if (c.Length == 2 && c[1] is ChildExpressionParser<TContext> childContext)
            {
               childContext.Object = c[0];
                return childContext;
            }


            if (c.Length > 1 && c.All(a=>a is FunctionEvaluator))
            {
                var functions = c.Cast<FunctionEvaluator>().ToArray();
                
                for (var j = functions.Length - 1; j > 0; j--)
                {
                    functions[j].Object = functions[j - 1];


                 }
                return functions.Last();
            }

            return null;

        }

        public async Task<JToken> EvaluateAsync(string name, params JToken[] arguments)
        {
            var function = functions.Get(name);
            if(function == null)
            {
                throw new Exception($"{name} not found in functions");
            }
           

            var value =await function(this,Document, arguments);


            return value;
        }

        IJTokenEvaluator CallFunction(string name, IJTokenEvaluator[] parameters)
        {
            return new FunctionEvaluator(this, name, parameters);
        }

        public async Task<JToken> EvaluateAsync(string str)
        {
            var value = await EvaluateImp(str);
            logger.LogInformation("Evaluating '{str}' to '{value}'", str, value?.ToString());
            return value;

        }

        private async Task<JToken> EvaluateImp(string str)
        {
            Parser<IJTokenEvaluator[]> stringParser =
                 from first in Parse.Char('[')
                 from evaluator in Tokenizer
                 from last in Parse.Char(']')
                 select evaluator;



            var func = stringParser.Parse(str).ToArray();
            if (func.Length == 1)
                return await func.First().EvaluateAsync();

            for (var i = 0; i < func.Length; i++)
            {
                if (func[i] is ArrayIndexLookup array)
                {
                    var arrayToken = await func[i - 1].EvaluateAsync();
                    if (arrayToken.Type != JTokenType.Array)
                        throw new Exception("not an array");

                    return arrayToken[int.Parse(array.parsedText)];

                }
                else if (func[i] is ObjectLookup objectLookup)
                {
                    var arrayToken = await func[i - 1].EvaluateAsync();
                    if (arrayToken.Type != JTokenType.Object)
                        throw new Exception("not an object");

                    return arrayToken[objectLookup.propertyName];

                }
            }

            return null;
        }
    }
}
