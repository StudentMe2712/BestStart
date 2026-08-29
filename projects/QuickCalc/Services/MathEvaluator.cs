using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QuickCalc.Services;

/// <summary>
/// A culture-independent, robust recursive descent math expression evaluator.
/// Always treats both '.' and ',' as decimal separators and parses numbers using CultureInfo.InvariantCulture.
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
        Modulo,
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
                else if (c == '×' || c == '\u00D7')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Multiply, startPos));
                }
                else if (c == '/' || c == '÷' || c == '\u00F7')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Divide, startPos));
                }
                else if (c == '%')
                {
                    Advance();
                    tokens.Add(new Token(TokenType.Modulo, startPos));
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

            // Check for scientific notation exponent: e.g. 1e5, 1.5e-3, 2E+10
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

            return new Token(TokenType.Identifier, startPos, text: word.ToLowerInvariant());
        }
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

            double result = ParseExpression();

            if (!IsAtEnd())
            {
                throw new FormatException($"Unexpected token '{Peek()}' at position {Peek().Position}.");
            }

            return result;
        }

        // Expression -> Additive
        private double ParseExpression()
        {
            return ParseAdditive();
        }

        // Additive -> Multiplicative ( ('+' | '-') Multiplicative )*
        private double ParseAdditive()
        {
            double left = ParseMultiplicative();

            while (Match(TokenType.Plus, TokenType.Minus))
            {
                TokenType op = Previous().Type;
                double right = ParseMultiplicative();

                if (op == TokenType.Plus)
                {
                    left += right;
                }
                else
                {
                    left -= right;
                }
            }

            return left;
        }

        // Multiplicative -> Unary ( ('*' | '/' | '%') Unary | [implicit multiply] Primary )*
        private double ParseMultiplicative()
        {
            double left = ParseUnary();

            while (true)
            {
                if (Match(TokenType.Multiply, TokenType.Divide, TokenType.Modulo))
                {
                    TokenType op = Previous().Type;
                    double right = ParseUnary();

                    if (op == TokenType.Multiply)
                    {
                        left *= right;
                    }
                    else if (op == TokenType.Divide)
                    {
                        left /= right;
                    }
                    else // Modulo
                    {
                        left %= right;
                    }
                }
                else if (CanStartPrimary(Peek()))
                {
                    // Implicit multiplication (e.g. 2(3+4), 2pi, (2)(3), 2sqrt(9))
                    double right = ParsePower();
                    left *= right;
                }
                else
                {
                    break;
                }
            }

            return left;
        }

        // Unary -> ('+' | '-') Unary | Power
        private double ParseUnary()
        {
            if (Match(TokenType.Plus))
            {
                return ParseUnary();
            }
            if (Match(TokenType.Minus))
            {
                return -ParseUnary();
            }

            return ParsePower();
        }

        // Power -> Primary ( ('^' | '**') Unary )? [Right-associative]
        private double ParsePower()
        {
            double left = ParsePrimary();

            if (Match(TokenType.Power))
            {
                double exponent = ParseUnary();
                return Math.Pow(left, exponent);
            }

            return left;
        }

        private static bool CanStartPrimary(Token token)
        {
            return token.Type == TokenType.Number ||
                   token.Type == TokenType.Identifier ||
                   token.Type == TokenType.OpenParen;
        }

        // Primary -> Number | Constant | FunctionCall | '(' Expression ')'
        private double ParsePrimary()
        {
            if (Match(TokenType.Number))
            {
                return Previous().NumberValue;
            }

            if (Match(TokenType.Identifier))
            {
                string name = Previous().Text ?? string.Empty;

                // Constants
                if (name == "pi")
                {
                    return Math.PI;
                }
                if (name == "e")
                {
                    return Math.E;
                }

                // Functions
                if (Check(TokenType.OpenParen))
                {
                    Advance(); // consume '('
                    double arg = ParseExpression();
                    Consume(TokenType.CloseParen, $"Missing closing parenthesis ')' for function '{name}'.");

                    return EvaluateFunction(name, arg);
                }

                throw new FormatException($"Unknown identifier '{name}' or missing parentheses for function call.");
            }

            if (Match(TokenType.OpenParen))
            {
                double expr = ParseExpression();
                Consume(TokenType.CloseParen, "Missing closing parenthesis ')'.");
                return expr;
            }

            throw new FormatException($"Expected expression at position {Peek().Position}, found '{Peek()}'.");
        }

        private static double EvaluateFunction(string name, double arg) => name switch
        {
            "sqrt" => Math.Sqrt(arg),
            "abs" => Math.Abs(arg),
            "sin" => Math.Sin(arg),
            "cos" => Math.Cos(arg),
            "tan" => Math.Tan(arg),
            "log" => Math.Log10(arg),
            "ln" => Math.Log(arg),
            "round" => Math.Round(arg, MidpointRounding.AwayFromZero),
            "floor" => Math.Floor(arg),
            "ceil" or "ceiling" => Math.Ceiling(arg),
            _ => throw new FormatException($"Unknown function '{name}'.")
        };
    }

    #endregion
}
