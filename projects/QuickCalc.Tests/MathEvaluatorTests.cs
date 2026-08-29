using System;
using System.Globalization;
using System.Threading;
using QuickCalc.Services;
using Xunit;

namespace QuickCalc.Tests;

public class MathEvaluatorTests
{
    #region Basic Arithmetic & Everyday Operator Aliases

    [Theory]
    [InlineData("2*15", 30)]
    [InlineData("2 * 15", 30)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 ÷ 4", 2.5)]
    [InlineData("100 : 4", 25)]
    [InlineData("100 \\ 4", 25)]
    [InlineData("12 x 12", 144)]
    [InlineData("12 X 12", 144)]
    [InlineData("12 × 12", 144)]
    [InlineData("12 • 12", 144)]
    [InlineData("12 ∙ 12", 144)]
    [InlineData("12 · 12", 144)]
    [InlineData("12 ⋅ 12", 144)]
    [InlineData("2^3", 8)]
    [InlineData("10^2", 100)]
    [InlineData("2^8", 256)]
    [InlineData("2 ** 3", 8)]
    [InlineData("-5 + 3", -2)]
    [InlineData("+5 + 3", 8)]
    [InlineData("2 * (15 + 7)", 44)]
    public void Evaluate_EverydayOperatorAliases_ReturnsExpectedResult(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    #endregion

    #region Decimal and Thousands Formatting

    [Theory]
    [InlineData("5,5 * 2", 11)]
    [InlineData("5.5 * 2", 11)]
    [InlineData("2,5 + 7,5", 10)]
    [InlineData("2.5 + 7.5", 10)]
    [InlineData(",5 * 4", 2)]
    [InlineData(".5 * 4", 2)]
    [InlineData("1 000 000 + 500 000", 1500000)]
    [InlineData("10_000 * 2", 20000)]
    [InlineData("1 000 000,50 + 0,50", 1000001)]
    [InlineData("1 000.50 + 0.50", 1000001 - 999000)] // 1001
    [InlineData("2 500 + 13%", 2825)]
    public void Evaluate_NumberFormattingAndGrouping_CalculatesAccurately(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    #endregion

    #region Everyday Percentage Semantics

    [Theory]
    // Postfix percentages
    [InlineData("50%", 0.5)]
    [InlineData("100%", 1.0)]
    [InlineData("5%", 0.05)]
    [InlineData("0.5%", 0.005)]
    [InlineData("0,5%", 0.005)]
    [InlineData("50%%", 0.005)]
    // Additive percentages (tax, markup, tip)
    [InlineData("100 + 20%", 120)]
    [InlineData("2500 + 13%", 2825)]
    [InlineData("200 + 5.5%", 211)]
    [InlineData("200 + 5,5%", 211)]
    // Subtractive percentages (discounts)
    [InlineData("100 - 20%", 80)]
    [InlineData("1500 - 15%", 1275)]
    [InlineData("200 - 5.5%", 189)]
    [InlineData("200 - 5,5%", 189)]
    // Multiplicative percentages
    [InlineData("100 * 20%", 20)]
    [InlineData("20% * 100", 20)]
    [InlineData("100 x 20%", 20)]
    [InlineData("100 × 20%", 20)]
    // Division by percentage
    [InlineData("100 / 20%", 500)]
    [InlineData("100 : 20%", 500)]
    [InlineData("100 ÷ 20%", 500)]
    [InlineData("20% / 2", 0.1)]
    // Chained percentages
    [InlineData("100 + 20% - 10%", 108)]
    [InlineData("100 + 10% + 10%", 121)]
    [InlineData("1000 - 20% - 10%", 720)]
    // Parenthesized percentages
    [InlineData("(100 + 50) + 10%", 165)]
    [InlineData("(200 - 50) - 10%", 135)]
    [InlineData("(100 + 20%)", 120)]
    [InlineData("(20% + 30%) * 100", 50)]
    // Direct percentage additions / subtractions (no preceding base)
    [InlineData("20% + 30%", 0.5)]
    [InlineData("50% - 20%", 0.3)]
    [InlineData("10% + 20% + 30%", 0.6)]
    // Natural language aliases: of / от
    [InlineData("20% of 150", 30)]
    [InlineData("20% OF 150", 30)]
    [InlineData("20% от 150", 30)]
    [InlineData("20% ОТ 150", 30)]
    [InlineData("50% of 200", 100)]
    [InlineData("15% от 2000", 300)]
    [InlineData("20% of (100 + 50)", 30)]
    public void Evaluate_PercentageSemantics_CalculatesExpectedResult(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    #endregion

    #region Auto-Closing Unbalanced Parentheses

    [Theory]
    [InlineData("(10 + 5", 15)]
    [InlineData("2 * (10 + 5", 30)]
    [InlineData("((10 + 5) * 2", 30)]
    [InlineData("(2 + 3) * (4 + 5", 45)]
    [InlineData("sqrt(144", 12)]
    [InlineData("abs(-42", 42)]
    [InlineData("round(2.6", 3)]
    [InlineData("(100 + 50 + 10%", 165)]
    public void Evaluate_AutoClosingParentheses_EvaluatesCleanly(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 6);
    }

    #endregion

    #region Implicit Multiplication & Functions

    [Theory]
    [InlineData("2(3 + 4)", 14)]
    [InlineData("(2 + 3)(4 + 5)", 45)]
    [InlineData("2sqrt(9)", 6)]
    [InlineData("3(2)", 6)]
    [InlineData("2pi", 6.283185307)]
    [InlineData("sqrt(144)", 12)]
    [InlineData("abs(-42)", 42)]
    [InlineData("abs(42)", 42)]
    [InlineData("abs(-3,14)", 3.14)]
    [InlineData("round(2.6)", 3)]
    [InlineData("round(2.4)", 2)]
    [InlineData("round(2.5)", 3)]
    [InlineData("round(-2.5)", -3)]
    public void Evaluate_ImplicitMultiplicationAndSimpleFunctions_ReturnsCorrectValue(string expression, double expected)
    {
        double result = MathEvaluator.Evaluate(expression);
        Assert.Equal(expected, result, precision: 5);
    }

    [Fact]
    public void Evaluate_Constants_ReturnsCorrectValues()
    {
        Assert.Equal(Math.PI, MathEvaluator.Evaluate("pi"), precision: 10);
        Assert.Equal(Math.PI, MathEvaluator.Evaluate("PI"), precision: 10);
        Assert.Equal(Math.E, MathEvaluator.Evaluate("e"), precision: 10);
        Assert.Equal(Math.E, MathEvaluator.Evaluate("E"), precision: 10);
    }

    #endregion

    #region Culture Invariance

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

            // Problem to solve: 2*15 must be 30, never 300
            Assert.Equal(30.0, MathEvaluator.Evaluate("2*15"));
            Assert.Equal("30", MathEvaluator.FormatResult(MathEvaluator.Evaluate("2*15")));

            // Comma & dot decimals
            Assert.Equal(11.0, MathEvaluator.Evaluate("5,5 * 2"));
            Assert.Equal(11.0, MathEvaluator.Evaluate("5.5 * 2"));
            Assert.Equal("11", MathEvaluator.FormatResult(MathEvaluator.Evaluate("5,5 * 2")));
            Assert.Equal("11", MathEvaluator.FormatResult(MathEvaluator.Evaluate("5.5 * 2")));

            Assert.Equal(2.5, MathEvaluator.Evaluate("10 / 4"));
            Assert.Equal(2.5, MathEvaluator.Evaluate("10 : 4"));
            Assert.Equal("2.5", MathEvaluator.FormatResult(MathEvaluator.Evaluate("10 / 4")));

            // Percentages
            Assert.Equal(120.0, MathEvaluator.Evaluate("100 + 20%"));
            Assert.Equal("120", MathEvaluator.FormatResult(MathEvaluator.Evaluate("100 + 20%")));
            Assert.Equal(80.0, MathEvaluator.Evaluate("100 - 20%"));
            Assert.Equal("80", MathEvaluator.FormatResult(MathEvaluator.Evaluate("100 - 20%")));
            Assert.Equal(30.0, MathEvaluator.Evaluate("20% от 150"));
            Assert.Equal("30", MathEvaluator.FormatResult(MathEvaluator.Evaluate("20% of 150")));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    #endregion

    #region String Formatting & Precision

    [Theory]
    [InlineData("2*15", "30")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("100 + 20%", "120")]
    [InlineData("2500 + 13%", "2825")]
    [InlineData("1500 - 15%", "1275")]
    [InlineData("100 + 20% - 10%", "108")]
    [InlineData("20% of 150", "30")]
    [InlineData("20% от 150", "30")]
    [InlineData("50%", "0.5")]
    [InlineData("1 000 000 + 500 000", "1500000")]
    [InlineData("10_000 * 2", "20000")]
    [InlineData("100 : 4", "25")]
    [InlineData("12 x 12", "144")]
    [InlineData("(10 + 5", "15")]
    public void TryEvaluate_EverydayExpressions_ReturnsExpectedFormattedString(string expression, string expectedFormatted)
    {
        bool success = MathEvaluator.TryEvaluate(expression, out double _, out string? formatted);
        Assert.True(success);
        Assert.Equal(expectedFormatted, formatted);
    }

    [Fact]
    public void Evaluate_FloatingPointPrecisionFix_FormatsCleanly()
    {
        // In IEEE 754, 0.1 + 0.2 is 0.30000000000000004
        double result = MathEvaluator.Evaluate("0.1 + 0.2");
        string formatted = MathEvaluator.FormatResult(result);
        Assert.Equal("0.3", formatted);
    }

    #endregion

    #region Error & Incomplete Expression Handling

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2 +")]
    [InlineData("2 *")]
    [InlineData("sqrt(")]
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

    #endregion
}
