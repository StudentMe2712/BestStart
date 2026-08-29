using System;
using System.Globalization;
using System.Threading;
using QuickCalc.Services;
using Xunit;

namespace QuickCalc.Tests;

public class MathEvaluatorTests
{
    [Theory]
    [InlineData("2*15", 30)]
    [InlineData("2 * 15", 30)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("5,5 * 2", 11)]
    [InlineData("5.5 * 2", 11)]
    [InlineData("2^8", 256)]
    [InlineData("2 ** 3", 8)]
    [InlineData("-5 + 3", -2)]
    [InlineData("2 * (15 + 7)", 44)]
    [InlineData("10 % 3", 1)]
    [InlineData("sqrt(144)", 12)]
    [InlineData("12 x 12", 144)]
    [InlineData("12 X 12", 144)]
    [InlineData("12 × 12", 144)]
    [InlineData("10 ÷ 4", 2.5)]
    public void Evaluate_StandardExpressions_ReturnsExpectedResult(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData("2*15", "30")]
    [InlineData("2 * 15", "30")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("5,5 * 2", "11")]
    [InlineData("5.5 * 2", "11")]
    [InlineData("2^8", "256")]
    [InlineData("2 ** 3", "8")]
    [InlineData("-5 + 3", "-2")]
    [InlineData("2 * (15 + 7)", "44")]
    [InlineData("10 % 3", "1")]
    [InlineData("sqrt(144)", "12")]
    [InlineData("12 x 12", "144")]
    [InlineData("12 × 12", "144")]
    [InlineData("10 ÷ 4", "2.5")]
    public void TryEvaluate_StandardExpressions_ReturnsExpectedFormattedString(string expression, string expectedFormatted)
    {
        bool success = MathEvaluator.TryEvaluate(expression, out double _, out string? formatted);
        Assert.True(success);
        Assert.Equal(expectedFormatted, formatted);
    }

    [Fact]
    public void Evaluate_PiMultiplication_ReturnsApproximateValue()
    {
        double result = MathEvaluator.Evaluate("pi * 2");
        Assert.InRange(result, 6.2831853, 6.2831854);

        string? formatted = MathEvaluator.EvaluateToString("pi * 2");
        Assert.NotNull(formatted);
        Assert.StartsWith("6.2831853", formatted);
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void Evaluate_UnderVariousCultures_IsCompletelyCultureInvariant(string cultureName)
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Problem to solve check: 2*15 must be 30, never 300
            Assert.Equal(30.0, MathEvaluator.Evaluate("2*15"));
            Assert.Equal("30", MathEvaluator.FormatResult(MathEvaluator.Evaluate("2*15")));

            // Decimal comma and dot checks
            Assert.Equal(11.0, MathEvaluator.Evaluate("5,5 * 2"));
            Assert.Equal(11.0, MathEvaluator.Evaluate("5.5 * 2"));
            Assert.Equal("11", MathEvaluator.FormatResult(MathEvaluator.Evaluate("5,5 * 2")));
            Assert.Equal("11", MathEvaluator.FormatResult(MathEvaluator.Evaluate("5.5 * 2")));

            Assert.Equal(2.5, MathEvaluator.Evaluate("10 / 4"));
            Assert.Equal("2.5", MathEvaluator.FormatResult(MathEvaluator.Evaluate("10 / 4")));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("abs(-42)", 42)]
    [InlineData("abs(42)", 42)]
    [InlineData("abs(-3,14)", 3.14)]
    [InlineData("sin(0)", 0)]
    [InlineData("cos(0)", 1)]
    [InlineData("tan(0)", 0)]
    [InlineData("log(100)", 2)]
    [InlineData("log(1000)", 3)]
    [InlineData("ln(e)", 1)]
    [InlineData("round(2.6)", 3)]
    [InlineData("round(2.4)", 2)]
    [InlineData("round(2.5)", 3)]
    [InlineData("round(-2.5)", -3)]
    [InlineData("floor(3.9)", 3)]
    [InlineData("floor(-3.1)", -4)]
    [InlineData("ceil(3.1)", 4)]
    [InlineData("ceil(-3.9)", -3)]
    [InlineData("ceiling(3.1)", 4)]
    public void Evaluate_ScientificFunctions_ReturnsCorrectValue(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void Evaluate_EulerConstant_ReturnsCorrectValue()
    {
        double result = MathEvaluator.Evaluate("e");
        Assert.Equal(Math.E, result, precision: 10);
    }

    [Fact]
    public void Evaluate_TrigonometricWithPi_ReturnsExpectedValue()
    {
        double result = MathEvaluator.Evaluate("sin(pi / 2)");
        Assert.Equal(1.0, result, precision: 6);

        double cosPi = MathEvaluator.Evaluate("cos(pi)");
        Assert.Equal(-1.0, cosPi, precision: 6);
    }

    [Theory]
    [InlineData("2(3 + 4)", 14)]
    [InlineData("2pi", 6.283185307)]
    [InlineData("(2 + 3)(4 + 5)", 45)]
    [InlineData("2sqrt(9)", 6)]
    [InlineData("3(2)", 6)]
    public void Evaluate_ImplicitMultiplication_CalculatesCorrectly(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 5);
    }

    [Theory]
    [InlineData("2 ^ 3 ^ 2", 512)] // Right-associative: 2^(3^2) = 2^9 = 512
    [InlineData("(2 ^ 3) ^ 2", 64)]
    [InlineData("2 * 3 ^ 2", 18)]   // Power has higher precedence than multiply: 2 * 9 = 18
    [InlineData("10 - 4 + 2", 8)]   // Left-to-right addition/subtraction
    [InlineData("10 % 3 * 2", 2)]   // Left-to-right modulo/multiplication
    [InlineData("-2 ^ 2", -4)]      // -(2^2) = -4
    [InlineData("(-2) ^ 2", 4)]
    public void Evaluate_PrecedenceAndAssociativity_CalculatesCorrectly(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData("1e3", 1000)]
    [InlineData("1.5e2", 150)]
    [InlineData("1,5e2", 150)]
    [InlineData("2e-1", 0.2)]
    [InlineData("2.5e+2", 250)]
    public void Evaluate_ScientificNotation_ParsesCorrectly(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData("SQRT(144)", 12)]
    [InlineData("Sqrt(144)", 12)]
    [InlineData("PI", Math.PI)]
    [InlineData("Pi", Math.PI)]
    [InlineData("E", Math.E)]
    [InlineData("SIN(0)", 0)]
    [InlineData("COS(0)", 1)]
    [InlineData("12 X 12", 144)]
    [InlineData("12 x 12", 144)]
    public void Evaluate_CaseInsensitivity_ReturnsCorrectResult(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void Evaluate_FloatingPointPrecisionFix_FormatsCleanly()
    {
        // In IEEE 754, 0.1 + 0.2 is 0.30000000000000004
        double result = MathEvaluator.Evaluate("0.1 + 0.2");
        string formatted = MathEvaluator.FormatResult(result);
        Assert.Equal("0.3", formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2 +")]
    [InlineData("2 *")]
    [InlineData("sqrt(")]
    [InlineData("(2 + 3")]
    [InlineData("unknown(5)")]
    [InlineData("++")]
    [InlineData("@#$")]
    public void TryEvaluate_InvalidOrIncompleteInput_ReturnsFalse(string? input)
    {
        bool success = MathEvaluator.TryEvaluate(input, out double result, out string? formatted);
        Assert.False(success);
        Assert.Null(formatted);
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsPositiveOrNegativeInfinity()
    {
        double result = MathEvaluator.Evaluate("10 / 0");
        Assert.True(double.IsInfinity(result));
    }
}
