using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Sprache;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNETDevOps.JsonFunctions
{
    public interface IExpressionParser
    {
        Task<JToken> EvaluateAsync(string name, params JToken[] arguments);
    }

    internal class ChildExpressionParser<TContext> : IJTokenEvaluator , IObjectHolder
    {
        private readonly IJTokenEvaluator[] childs;
        private readonly bool throwOnError;

        public ChildExpressionParser (IJTokenEvaluator[] childs, IOption<char> optionalFirst, bool throwOnError)
        {
            this.childs = childs;
            this.throwOnError = throwOnError;
            NullConditional = optionalFirst.IsDefined;
        }

        public IJTokenEvaluator Object { get;  set; }
        public bool NullConditional { get; set; }

        public async Task<JToken> EvaluateAsync()
        {
            if (childs.Skip(1).Any() && Object == null)
                return new JArray( await Task.WhenAll(childs.Select(k => k.EvaluateAsync())));

            var propertyName = await childs[0].EvaluateAsync();

            var token = await Object.EvaluateAsync();
        
            if ((token is null || token.Type == JTokenType.Null) && NullConditional)
                return token;

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
        public readonly Parser<IJTokenEvaluator> BracketParser;
        
        public readonly Parser<IJTokenEvaluator> ObjectFunction;        
        public readonly Parser<IJTokenEvaluator[]> Tokenizer;

        private static readonly Parser<char> DoubleQuote = Parse.Char('"');
       // private static readonly Parser<char> SingleQuote = Parse.Char('\'');
       // private static readonly Parser<char> Backslash = Parse.Char('\\');
       // private static readonly Parser<IEnumerable<char>> EscapedQuote = Parse.String("\\'").Text().Named("Escaped delimiter");
        private static readonly Parser<string> escapedDelimiter = Parse.String("''").Text().Named("Escaped delimiter");
        private static readonly Parser<string> escapedDelimiter2 = Parse.String("\\'").Text().Named("Escaped delimiter");
        private static readonly Parser<string> singleEscape = Parse.String("\\").Text().Named("Single escape character");
        private static readonly Parser<string> doubleEscape = Parse.String("\\\\").Text().Named("Escaped escape character");
        private static readonly Parser<char> delimiter = Parse.Char('\'').Named("Delimiter");

        private static readonly Parser<char> QdText =
            Parse.AnyChar.Except(DoubleQuote);
        //private static readonly Parser<char> QdText1 =
        //    (Parse.AnyChar).Except(SingleQuote);

        //private static readonly Parser<char> QuotedPair =
        //    from _ in Backslash
        //    from c in SingleQuote
        //    select c;

        private static readonly Parser<IEnumerable<char>> simpleLiteral = Parse.AnyChar
            .Except(singleEscape).Except(delimiter).Many().Text().Named("Literal without escape/delimiter character");

        public static readonly Parser<StringConstantEvaluator> StringLiteral = 
            (from start in delimiter
                             from v in Parse.Ref(()=> escapedDelimiter.Or(escapedDelimiter2).Or(doubleEscape).Or(singleEscape).Or(simpleLiteral)).Text().Many()
                             from end in delimiter
                             select new StringConstantEvaluator( string.Concat(v.Select(k=>k=="''" || k=="\\'" ?"'":k)) ));

        private static readonly Parser<StringConstantEvaluator> QuotedString =
            from open in DoubleQuote
            from text in QdText.Many().Text()
            from close in DoubleQuote
            select new StringConstantEvaluator(text);

        //private static readonly Parser<StringConstantEvaluator> QuotedSingleString =
        //   from open in SingleQuote
        //   from text in QdText1.Many().Text()
        //   from close in SingleQuote
        //   select new StringConstantEvaluator(string.Join('\'',text));

       // public Dictionary<string, Func<JToken, JToken[], JToken>> Functions { get; set; } = new Dictionary<string, Func<JToken, JToken[], JToken>>();

        private readonly Parser<IJTokenEvaluator> Number = from op in Parse.Optional(Parse.Char('-').Token())
                                                           from num in Parse.Decimal
                                                           from trailingSpaces in Parse.Char(' ').Many()
                                                           select new DecimalConstantEvaluator(decimal.Parse(num) * (op.IsDefined ? -1 : 1));
        private readonly IOptions<ExpressionParserOptions<TContext>> options;
        private readonly ILogger logger;
        private readonly IExpressionFunctionFactory<TContext> functions;

        public TContext Document => this.options.Value.Document;
        public ExpressionParserOptions<TContext> Options => this.options.Value;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        private readonly ConcurrentDictionary<string, ValueTask<JToken>> cache = new ConcurrentDictionary<string, ValueTask<JToken>>();

        public ExpressionParser(IOptions<ExpressionParserOptions<TContext>> options, ILogger logger, IExpressionFunctionFactory<TContext> functions)
        {
            Constant = Parse.LetterOrDigit.AtLeastOnce().Text().Select(k => new ConstantEvaluator(k));

            Tokenizer = 
                from expr in Parse.Ref(
                    () => Parse.Ref(
                        () => (
                            Function
                            .Or(Number)
                            .Or(QuotedString)
                            .Or(Parse.Ref(()=> StringLiteral))
                            .Or(Constant)
                        )
                        .Or(ArrayIndexer)
                        .Or(ObjectFunction)
                        .Or(PropertyAccessByDot)
                        .Or(PropertyAccessByBracket)
                        .Or(BracketParser)
                    )
                    .AtLeastOnce()
                ).Optional().DelimitedBy(Parse.Char(',').Or(Parse.WhiteSpace).Token())
                select FixArrayIndexers(expr.Select(c => (c.GetOrDefault() ?? Enumerable.Empty<IJTokenEvaluator>()).ToArray()).ToArray());

            Function = 
                from whitespace1 in Parse.WhiteSpace.Many().Text()
                from name in Parse.Letter.AtLeastOnce().Text()
                from charOrNumber in Parse.LetterOrDigit.Many().Text()
                from lparen in Parse.Char('(')
                from whitespace2 in Parse.WhiteSpace.Many().Text()
                from expr in Tokenizer
                from whitespace3 in Parse.WhiteSpace.Many().Text()
                from rparen in Parse.Char(')')
                select CallFunction(name + charOrNumber,null, expr);

           
            PropertyAccessByDot = 
                from optionalFirst in Parse.Optional(Parse.Char('?'))
                from first in Parse.Char('.')
                from propertyName in Parse.LetterOrDigit.Or(Parse.Char('_')).AtLeastOnce().Text()
                select new ObjectLookup(propertyName, optionalFirst, options.Value.ThrowOnError);
            PropertyAccessByBracket =
                from optionalFirst in Parse.Optional(Parse.Char('?'))
                from first in Parse.Char('[')
                from propertyName in (Parse.LetterOrDigit.Or(Parse.Char('\'')).Or(Parse.Char('_')).AtLeastOnce().Text())
                from last in Parse.Char(']')
                select new ObjectLookup(propertyName, optionalFirst, options.Value.ThrowOnError);

            BracketParser =
                from optionalFirst in Parse.Optional(Parse.Char('?'))
                from first in Parse.Char('[')
                from propertyNameOrChilds in Tokenizer
                from last in Parse.Char(']')
                select new ChildExpressionParser<TContext>(propertyNameOrChilds, optionalFirst, options.Value.ThrowOnError);


            ObjectFunction =
                from optionalFirst in Parse.Optional(Parse.Char('?'))
                from first in Parse.Char('.')
                from name in Parse.LetterOrDigit.Or(Parse.Char('_')).AtLeastOnce().Text()
                from lparen in Parse.Char('(')
                from expr in Tokenizer
                from rparen in Parse.Char(')')
                select CallFunction(name, optionalFirst, expr);

            ArrayIndexer =
                 from optionalFirst in Parse.Optional(Parse.Char('?'))
                 from first in Parse.Char('[')
                from text in Parse.Number
                from last in Parse.Char(']')
                select new ArrayIndexLookup(text, optionalFirst);

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


            if (c.Length > 1 && c.All(a=>a is IObjectHolder))
            {
                var functions = c.OfType<IObjectHolder>().ToArray();
                
                for (var j = functions.Length - 1; j > 0; j--)
                {
                    functions[j].Object = functions[j - 1];


                }
                //for(var i = 0; i < functions.Length-1; i++)
                //{
                //    if (functions[i].NullConditional)
                //    {
                //        functions[i + 1].NullConditional = true;
                //    }
                //}
                return functions.Last();
            }

            return null;

        }
       
        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        public async Task<JToken> EvaluateAsync(string name, params JToken[] arguments)
        {
            var function = functions.Get(name);
            if(function == null)
            {
                throw new Exception($"{name} not found in functions");
            }

            if (options.Value.EnableFunctionEvaluationCaching && arguments.Any())
            {
                
                var key = CreateMD5($"{name}{string.Join("", arguments.Select(c => c?.ToString()))}");

                if(Options.Cache != null)
                {
                    return await Options.Cache.GetOrAdd(key, async (hash) => await function(this, Document, arguments));
                }

                return await cache.GetOrAdd(key, async (hash) => await function(this, Document, arguments));
            }
            
            var value =await function(this,Document, arguments);


            return value;
        }

        IJTokenEvaluator CallFunction(string name, IOption<char> optionalFirst, IJTokenEvaluator[] parameters)
        {
            return new FunctionEvaluator(this, name, optionalFirst, parameters);
        }

        public async Task<JToken> EvaluateAsync(string str)
        {
            using (logger.BeginScope(new Dictionary<string, string> { ["expression"] = str }))
            {
                try
                {
                    var value = await EvaluateImp(str);
                    logger.LogTrace("Evaluating '{str}' to '{value}'", str, value?.ToString());
                    return value;
                }catch(Exception ex)
                {
                    logger.LogError(ex, "Failed to evaluate expression");
                    throw;
                }
            }

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
