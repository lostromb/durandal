

namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// FIXME: HOLY FEATHER this needs a proper recursive descent parser, all it is now is a giant hack
    /// </summary>
    public static class ScriptParser
    {
        private static readonly Regex statementsParser = new Regex("\\s*(.+?)\\s*(?:$|;)");

        private delegate IStatement StatementParser(string codeString, string ruleId);
        private delegate IExpression ExpressionParser(string codeString, string ruleId);

        private static readonly IList<ExpressionParser> expressionParsers;
        private static readonly IList<StatementParser> statementParsers;

        static ScriptParser()
        {
            // List of all supported expression types in descending order of precedence
            expressionParsers = new List<ExpressionParser>();
            expressionParsers.Add(NormalizeExpression.TryParse);
            expressionParsers.Add(SubstringExpression.TryParse);
            expressionParsers.Add(MultExpression.TryParse);
            expressionParsers.Add(MinusExpression.TryParse);
            expressionParsers.Add(ArrayLookupExpression.TryParse);
            expressionParsers.Add(SumExpression.TryParse);
            expressionParsers.Add(ConcatExpression.TryParse);
            expressionParsers.Add(ValueExpression.TryParse);
            expressionParsers.Add(ConstExpression.TryParse);

            // List of all supported statement types in descending order of precedence
            statementParsers = new List<StatementParser>();
            statementParsers.Add(TimexAssignmentStatement.TryParse);
        }

        public static IList<IStatement> ParseStatements(string code, string ruleId)
        {
            IList<IStatement> returnVal = new List<IStatement>();
            foreach (Match m in statementsParser.Matches(code))
            {
                if (string.IsNullOrWhiteSpace(m.Groups[1].Value))
                {
                    continue;
                }
                
                IStatement singleStatement = ParseStatement(m.Groups[1].Value, ruleId);
                if (singleStatement == null)
                {
                    throw new TimexException("Tagscript statement could not be parsed: \"" + code + "\"", ruleId);   
                }

                returnVal.Add(singleStatement);
            }

            return returnVal;
        }

        public static IStatement ParseStatement(string code, string ruleId)
        {
            foreach (StatementParser parser in statementParsers)
            {
                IStatement parsedStatement = parser(code, ruleId);
                if (parsedStatement != null)
                {
                    return parsedStatement;
                }
            }

            throw new TimexException("Could not parse script statement: " + code, ruleId);
        }

        public static IExpression ParseExpression(string code, string ruleId)
        {
            foreach (ExpressionParser parser in expressionParsers)
            {
                IExpression parsedExpression = parser(code, ruleId);
                if (parsedExpression != null)
                {
                    return parsedExpression;
                }
            }

            throw new TimexException("Could not parse script expression: " + code, ruleId);
        }

        /// <summary>
        /// Kinda hacky code since we don't implement a proper recursive descent parser.
        /// Inspects a function parameter string like "3", Normalize("test", 1) and attempts to figure
        /// out which comma is the one which separates the two parameters by enforcing the rule
        /// of balanced parentheses. The first comma that satisfies the conditions will have its string
        /// index returned. Otherwise this returns -1
        /// </summary>
        /// <param name="parameterString"></param>
        /// <returns></returns>
        public static int SplitTwoParametersWithMatchedParentheses(string parameterString)
        {
            int separator = parameterString.IndexOf(',');
            while (separator >= 0)
            {
                string left = parameterString.Substring(0, separator);
                string right = parameterString.Substring(separator + 1);
                if (CountOpeningParens(left) == CountClosingParens(left) &&
                    CountOpeningParens(right) == CountClosingParens(right))
                {
                    return separator;
                }

                separator = parameterString.IndexOf(',', separator + 1);
            }

            return -1;
        }

        private static int CountOpeningParens(string substring)
        {
            int sum = 0;
            foreach (char c in substring)
            {
                if (c == '(' || c == '{' || c == '[')
                    sum++;
            }
            return sum;
        }

        private static int CountClosingParens(string substring)
        {
            int sum = 0;
            foreach (char c in substring)
            {
                if (c == ')' || c == '}' || c == ']')
                    sum++;
            }
            return sum;
        }
    }
}
