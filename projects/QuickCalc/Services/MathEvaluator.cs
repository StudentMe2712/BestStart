using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QuickCalc.Services;

/// <summary>
/// A culture-independent, robust recursive descent math expression evaluator tailored for everyday calculations.
/// Supports everyday percentage semantics (+%, -%, *%, /%, chained %, 'of'/'от'),
/// flexible operator aliases (÷, :, \, ×, •, x), thousands separators, and auto-closing parentheses.
/// </summary>
public static class MathEvaluator
{
    /// <summary>
    /// Evaluates a mathematical expression string and returns the double result.
    /// Throws FormatException or ArgumentException on invalid expressions.
    /// </summary>
    public static double Evaluate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression cannot be null or empty.", nameof(expression));
        }

        var lexer = new Lexer(expression);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    /// <summary>
    /// Safely attempts to evaluate a mathematical expression.
    /// Returns true if evaluation succeeds with a finite double value.
    /// </summary>
    public static bool TryEvaluate(string? expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            result = Evaluate(expression);
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely attempts to evaluate an expression and format the result string.
    /// </summary>
    public static bool TryEvaluate(string? expression, out double result, out string? formattedResult)
    {
        formattedResult = null;
        if (TryEvaluate(expression, out result))
        {
            formattedResult = FormatResult(result);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates an expression and returns the formatted string result, or null if invalid/incomplete.
    /// </summary>
    public static string? EvaluateToString(string? expression)
    {
        if (TryEvaluate(expression, out _, out string? formatted))
        {
            return formatted;
        }
        return null;
    }

    /// <summary>
    /// Formats a double value cleanly using CultureInfo.InvariantCulture without trailing zeros.
    /// Eliminates minor floating-point precision artifacts (e.g. 0.1 + 0.2 => 0.3).
    /// </summary>
    public static string FormatResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "Error";
        }

        if (value == 0.0)
        {
            return "0";
        }

        // Eliminate minor IEEE 754 precision artifacts (e.g. 0.1 + 0.2 = 0.30000000000000004)
        double rounded = Math.Round(value, 12);
        if (Math.Abs(value - rounded) < 1e-13)
        {
            value = rounded;
        }

        // Use exponential format for very large or tiny non-zero numbers
        if (Math.Abs(value) >= 1e15 || (Math.Abs(value) > 0 && Math.Abs(value) < 1e-6))
        {
            return value.ToString("G12", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.############", CultureInfo.InvariantCulture);
    }

    #region Lexer & Parser Implementation

    private enum TokenType
    {
        Number,
        Identifier,
        Plus,
        Minus,
        Multiply,
        Divide,
        Percent,
        Power,
        OpenParen,
        CloseParen,
        EndOfInput
    }

    private sealed class Token
    {
        public TokenType Type { get; }
        public double NumberValue { get; }
        public string? Text { get; }
        public int Position { get; }

        public Token(TokenType type, int position, double numberValue = 0, string? text = null)
        {
            Type = type;
            Position = position;
            NumberValue = numberValue;
            Text = text;
        }

        public override string ToString() => Type switch
        {
            TokenType.Number => $"Number({NumberValue})",
            TokenType.Identifier => $"Identifier({Text})",
            _ => Type.ToString()
        };
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private int _pos;

        public Lexer(string text)
        {
            _text = text ?? string.Empty;
            _pos = 0;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';
        private char PeekNext() => (_pos + 1) < _text.Length ? _text[_pos + 1] : '\0';
        private char Advance() => _pos < _text.Length ? _text[_pos++] : '\0';

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();

            while (_pos < _text.Length)
            {
                char c = Peek();

                if (char.IsWhiteSpace(c))
                {
                    Advance();
                    continue;
                }

                int startPos = _pos;

                if (c == '+')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Plus, startPos));
                }
                else if (c == '-')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Minus, startPos));
                }
                else if (c == '*')
                {
                    Advance();
                    if (Peek() == '*')
                    {
                        Advance();
                        tokens.Add(new Token(TokenType.Power, startPos));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Multiply, startPos));
                    }
                }
                else if (c == '×' || c == '\u00D7' || c == '•' || c == '\u2022' || c == '∙' || c == '\u2219' || c == '·' || c == '\u00B7' || c == '⋅' || c == '\u22C5')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Multiply, startPos));
                }
                else if (c == '/' || c == '÷' || c == '\u00F7' || c == ':' || c == '\\')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Divide, startPos));
                }
                else if (c == '%')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Percent, startPos));
                }
                else if (c == '^')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Power, startPos));
                }
                else if (c == '(')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.OpenParen, startPos));
                }
                else if (c == ')')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.CloseParen, startPos));
                }
                else if (char.IsDigit(c) || ((c == '.' || c == ',') && char.IsDigit(PeekNext())))
                {
                    tokens.Add(ReadNumber());
                }
                else if (char.IsLetter(c))
                {
                    tokens.Add(ReadIdentifierOrOperator());
                }
                else
                {
                    throw new FormatException($"Unexpected character '{c}' at position {startPos}.");
                }
            }

            tokens.Add(new Token(TokenType.EndOfInput, _pos));
            return tokens;
        }

        private Token ReadNumber()
        {
            int startPos = _pos;
            var sb = new StringBuilder();
            bool hasDecimal = false;

            while (_pos < _text.Length)
            {
                char c = Peek();

                if (char.IsDigit(c))
                {
                    sb.Append(Advance());
                }
                else if (c == '_')
                {
                    // Thousands separator e.g. 10_000
                    Advance();
                }
                else if (IsSpaceChar(c))
                {
                    // Thousands separator with space: e.g. 1 000 000
                    int lookahead = _pos;
                    while (lookahead < _text.Length && (IsSpaceChar(_text[lookahead]) || _text[lookahead] == '_'))
                    {
                        lookahead++;
                    }

                    if (lookahead < _text.Length && char.IsDigit(_text[lookahead]))
                    {
                        _pos = lookahead;
                    }
                    else
                    {
                        break;
                    }
                }
                else if ((c == '.' || c == ',') && !hasDecimal)
                {
                    hasDecimal = true;
                    Advance();
                    sb.Append('.');
                }
                else
                {
                    break;
                }
            }

            // Scientific notation exponent: e.g. 1e5, 1.5e-3, 2E+10
            if (_pos < _text.Length && (Peek() == 'e' || Peek() == 'E'))
            {
                char next1 = PeekNext();
                char next2 = (_pos + 2 < _text.Length) ? _text[_pos + 2] : '\0';

                if (char.IsDigit(next1) || ((next1 == '+' || next1 == '-') && char.IsDigit(next2)))
                {
                    sb.Append(Advance()); // 'e' or 'E'
                    if (Peek() == '+' || Peek() == '-')
                    {
                        sb.Append(Advance());
                    }
                    while (_pos < _text.Length && char.IsDigit(Peek()))
                    {
                        sb.Append(Advance());
                    }
                }
            }

            string numStr = sb.ToString();
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                throw new FormatException($"Invalid number '{numStr}' at position {startPos}.");
            }

            return new Token(TokenType.Number, startPos, numberValue: val);
        }

        private static bool IsSpaceChar(char c) => c == ' ' || c == '\t' || c == '\u00A0' || c == '\u202F';

        private Token ReadIdentifierOrOperator()
        {
            int startPos = _pos;
            var sb = new StringBuilder();

            while (_pos < _text.Length && char.IsLetter(Peek()))
            {
                sb.Append(Advance());
            }

            string word = sb.ToString();

            // Standalone 'x' or 'X' is treated as multiplication
            if (string.Equals(word, "x", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenType.Multiply, startPos);
            }

            // Natural language percentage & multiplication aliases: "of" / "от"
            if (string.Equals(word, "of", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(word, "от", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenType.Multiply, startPos);
            }

            return new Token(TokenType.Identifier, startPos, text: word.ToLowerInvariant());
        }
    }

    private readonly struct EvalResult
    {
        public double Value { get; }
        public bool IsPercent { get; }

        public EvalResult(double value, bool isPercent = false)
        {
            Value = value;
            IsPercent = isPercent;
        }

        public override string ToString() => IsPercent ? $"{Value * 100}%" : Value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _current;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
        }

        private Token Peek() => _tokens[_current];
        private Token Previous() => _tokens[_current - 1];
        private bool IsAtEnd() => Peek().Type == TokenType.EndOfInput;

        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return type == TokenType.EndOfInput;
            return Peek().Type == type;
        }

        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return Previous();
        }

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw new FormatException(message);
        }

        public double Parse()
        {
            if (_tokens.Count == 0 || (_tokens.Count == 1 && _tokens[0].Type == TokenType.EndOfInput))
            {
                throw new FormatException("Empty expression.");
            }

            EvalResult result = ParseExpression();

            if (!IsAtEnd())
            {
                throw new FormatException($"Unexpected token '{Peek()}' at position {Peek().Position}.");
            }

            return result.Value;
        }

        // Expression -> Additive
        private EvalResult ParseExpression()
        {
            return ParseAdditive();
        }

        // Additive -> Multiplicative ( ('+' | '-') Multiplicative )*
        private EvalResult ParseAdditive()
        {
            EvalResult left = ParseMultiplicative();

            while (Match(TokenType.Plus, TokenType.Minus))
            {
                TokenType op = Previous().Type;
                EvalResult right = ParseMultiplicative();

                if (op == TokenType.Plus)
                {
                    left = Add(left, right);
                }
                else
                {
                    left = Subtract(left, right);
                }
            }

            return left;
        }

        private static EvalResult Add(EvalResult left, EvalResult right)
        {
            // If base + percentage (e.g. 100 + 20% = 120, 2500 + 13% = 2825)
            if (!left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value + (left.Value * right.Value), isPercent: false);
            }
            // If direct percentage addition without base (e.g. 20% + 30% = 0.5)
            if (left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value + right.Value, isPercent: true);
            }
            // Normal addition (e.g. 100 + 20 = 120, 20% + 10 = 10.2)
            return new EvalResult(left.Value + right.Value, isPercent: false);
        }

        private static EvalResult Subtract(EvalResult left, EvalResult right)
        {
            // If base - percentage (e.g. 100 - 20% = 80, 1500 - 15% = 1275)
            if (!left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value - (left.Value * right.Value), isPercent: false);
            }
            // If direct percentage subtraction (e.g. 50% - 20% = 0.3)
            if (left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value - right.Value, isPercent: true);
            }
            // Normal subtraction (e.g. 100 - 20 = 80)
            return new EvalResult(left.Value - right.Value, isPercent: false);
        }

        // Multiplicative -> Unary ( ('*' | '/') Unary | [implicit multiply] Power )*
        private EvalResult ParseMultiplicative()
        {
            EvalResult left = ParseUnary();

            while (true)
            {
                if (Match(TokenType.Multiply, TokenType.Divide))
                {
                    TokenType op = Previous().Type;
                    EvalResult right = ParseUnary();

                    if (op == TokenType.Multiply)
                    {
                        left = Multiply(left, right);
                    }
                    else
                    {
                        left = Divide(left, right);
                    }
                }
                else if (CanStartPrimary(Peek()))
                {
                    // Implicit multiplication (e.g. 2(3+4), 2pi, (2)(3), 2sqrt(9))
                    EvalResult right = ParsePower();
                    left = Multiply(left, right);
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        private static EvalResult Multiply(EvalResult left, EvalResult right)
        {
            // If percent * scalar or scalar * percent -> result is value
            // (e.g. 100 * 20% = 20, 20% * 100 = 20, 20% of 150 = 30)
            if (left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value * right.Value, isPercent: true);
            }
            return new EvalResult(left.Value * right.Value, isPercent: false);
        }

        private static EvalResult Divide(EvalResult left, EvalResult right)
        {
            if (right.Value == 0.0)
            {
                return new EvalResult(left.Value >= 0 ? double.PositiveInfinity : double.NegativeInfinity, isPercent: false);
            }

            // e.g. 100 / 20% = 500
            if (!left.IsPercent && right.IsPercent)
            {
                return new EvalResult(left.Value / right.Value, isPercent: false);
            }
            // e.g. 20% / 2 = 10% (0.10)
            if (left.IsPercent && !right.IsPercent)
            {
                return new EvalResult(left.Value / right.Value, isPercent: true);
            }
            // e.g. 20% / 10% = 2
            return new EvalResult(left.Value / right.Value, isPercent: false);
        }

        // Unary -> ('+' | '-') Unary | Power
        private EvalResult ParseUnary()
        {
            if (Match(TokenType.Plus))
            {
                return ParseUnary();
            }
            if (Match(TokenType.Minus))
            {
                EvalResult operand = ParseUnary();
                return new EvalResult(-operand.Value, operand.IsPercent);
            }

            return ParsePower();
        }

        // Power -> Primary ( ('^' | '**') Unary )? [Right-associative]
        private EvalResult ParsePower()
        {
            EvalResult left = ParsePrimary();

            if (Match(TokenType.Power))
            {
                EvalResult exponent = ParseUnary();
                return new EvalResult(Math.Pow(left.Value, exponent.Value), isPercent: false);
            }

            return left;
        }

        private static bool CanStartPrimary(Token token)
        {
            return token.Type == TokenType.Number ||
                   token.Type == TokenType.Identifier ||
                   token.Type == TokenType.OpenParen;
        }

        // Primary -> ( Number | Constant | FunctionCall | '(' Expression ')' ) ('%')*
        private EvalResult ParsePrimary()
        {
            EvalResult result;

            if (Match(TokenType.Number))
            {
                result = new EvalResult(Previous().NumberValue, isPercent: false);
            }
            else if (Match(TokenType.Identifier))
            {
                string name = Previous().Text ?? string.Empty;

                // Constants
                if (name == "pi")
                {
                    result = new EvalResult(Math.PI, isPercent: false);
                }
                else if (name == "e")
                {
                    result = new EvalResult(Math.E, isPercent: false);
                }
                else if (Check(TokenType.OpenParen))
                {
                    Advance(); // consume '('
                    EvalResult arg = ParseExpression();
                    if (!IsAtEnd())
                    {
                        Consume(TokenType.CloseParen, $"Missing closing parenthesis ')' for function '{name}'.");
                    }

                    result = new EvalResult(EvaluateFunction(name, arg.Value), isPercent: false);
                }
                else
                {
                    throw new FormatException($"Unknown identifier '{name}' or missing parentheses for function call.");
                }
            }
            else if (Match(TokenType.OpenParen))
            {
                result = ParseExpression();
                if (!IsAtEnd())
                {
                    Consume(TokenType.CloseParen, "Missing closing parenthesis ')'.");
                }
            }
            else
            {
                throw new FormatException($"Expected expression at position {Peek().Position}, found '{Peek()}'.");
            }

            // Postfix percentage: e.g. 50% => 0.50, (100 + 50)% => 1.50
            while (Match(TokenType.Percent))
            {
                result = new EvalResult(result.Value / 100.0, isPercent: true);
            }

            return result;
        }

        private static double EvaluateFunction(string name, double arg) => name switch
        {
            "sqrt" => Math.Sqrt(arg),
            "abs" => Math.Abs(arg),
            "round" => Math.Round(arg, MidpointRounding.AwayFromZero),
            _ => throw new FormatException($"Unknown function '{name}'.")
        };
    }

    #endregion
}
