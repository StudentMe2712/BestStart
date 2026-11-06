using System;
using QuickCalc.Services;
using Xunit;

namespace QuickCalc.Tests;

public class PopularMathOperationsTests
{
    #region User Explicit Number Formatting Examples

    [Fact]
    public void FormatResult_UserExplicitExample_FormatsWithThousandsSpace()
    {
        double value = 276939.0765;
        string formatted = MathEvaluator.FormatResult(value);
        Assert.Equal("276 939.0765", formatted);
    }

    [Fact]
    public void FormatResult_NegativeUserExplicitExample_PreservesMinusAndSpaces()
    {
        double value = -276939.0765;
        string formatted = MathEvaluator.FormatResult(value);
        Assert.Equal("-276 939.0765", formatted);
    }

    [Fact]
    public void FormatResult_NegativeZero_ReturnsZero()
    {
        double value = -0.0;
        string formatted = MathEvaluator.FormatResult(value);
        Assert.Equal("0", formatted);
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.5, "0.5")]
    [InlineData(12.0, "12")]
    [InlineData(100.0, "100")]
    [InlineData(999.0, "999")]
    [InlineData(1000.0, "1 000")]
    [InlineData(10000.0, "10 000")]
    [InlineData(100000.0, "100 000")]
    [InlineData(500000.0, "500 000")]
    [InlineData(1000000.0, "1 000 000")]
    [InlineData(2500000.0, "2 500 000")]
    [InlineData(1000000000.0, "1 000 000 000")]
    [InlineData(-1000000.0, "-1 000 000")]
    [InlineData(-1000000000.0, "-1 000 000 000")]
    public void FormatResult_ThousandsMillionsBillions_FormatsExpectedGrouping(double value, string expected)
    {
        string formatted = MathEvaluator.FormatResult(value);
        Assert.Equal(expected, formatted);
    }

    #endregion

    #region Popular Math Operations: Addition, Subtraction, Multiplication, Division, Percentages

    [Theory]
    // Addition (+)
    [InlineData("2 + 2", 4.0, "4")]
    [InlineData("100 + 50", 150.0, "150")]
    [InlineData("276 000 + 939.0765", 276939.0765, "276 939.0765")]
    [InlineData("276 939.0765 + 1 000", 277939.0765, "277 939.0765")]
    // Subtraction (-)
    [InlineData("100 - 20", 80.0, "80")]
    [InlineData("1 000 000 - 250 000", 750000.0, "750 000")]
    [InlineData("50 - 150", -100.0, "-100")]
    // Multiplication (*, x, etc.)
    [InlineData("12 * 12", 144.0, "144")]
    [InlineData("12 x 12", 144.0, "144")]
    [InlineData("6 * 7", 42.0, "42")]
    [InlineData("2000 * 500", 1000000.0, "1 000 000")]
    // Division (/, :)
    [InlineData("100 / 4", 25.0, "25")]
    [InlineData("100 : 4", 25.0, "25")]
    [InlineData("1 000 000 / 4", 250000.0, "250 000")]
    [InlineData("10 / 4", 2.5, "2.5")]
    // Percentages (%)
    [InlineData("100 + 20%", 120.0, "120")]
    [InlineData("100 - 20%", 80.0, "80")]
    [InlineData("20% от 150", 30.0, "30")]
    [InlineData("20% of 150", 30.0, "30")]
    [InlineData("2500 + 13%", 2825.0, "2 825")]
    [InlineData("1500 - 15%", 1275.0, "1 275")]
    public void EvaluateAndFormat_PopularMathOperations_CalculatesAndFormatsAccurately(
        string expression, double expectedValue, string expectedFormatted)
    {
        bool success = MathEvaluator.TryEvaluate(expression, out double result, out string? formatted);
        Assert.True(success);
        Assert.Equal(expectedValue, result, precision: 4);
        Assert.Equal(expectedFormatted, formatted);
    }

    #endregion

    #region StubMathEvaluator Tests

    [Theory]
    // Addition stubs
    [InlineData("2 + 2", 4.0, "4")]
    [InlineData("276 000 + 939.0765", 276939.0765, "276 939.0765")]
    [InlineData("100 + 50", 150.0, "150")]
    // Subtraction stubs
    [InlineData("1 000 000 - 250 000", 750000.0, "750 000")]
    [InlineData("100 - 20", 80.0, "80")]
    // Multiplication stubs
    [InlineData("12 * 12", 144.0, "144")]
    [InlineData("12 x 12", 144.0, "144")]
    [InlineData("6 * 7", 42.0, "42")]
    // Division stubs
    [InlineData("100 / 4", 25.0, "25")]
    [InlineData("1 000 000 / 4", 250000.0, "250 000")]
    [InlineData("100 : 4", 25.0, "25")]
    // Percentage stubs
    [InlineData("100 + 20%", 120.0, "120")]
    [InlineData("100 - 20%", 80.0, "80")]
    [InlineData("20% от 150", 30.0, "30")]
    [InlineData("20% of 150", 30.0, "30")]
    public void StubMathEvaluator_DefaultStubs_ReturnExpectedValuesAndFormatting(
        string expression, double expectedValue, string expectedFormatted)
    {
        var stubEvaluator = new StubMathEvaluator();

        // Evaluate
        double evaluated = stubEvaluator.Evaluate(expression);
        Assert.Equal(expectedValue, evaluated, precision: 4);

        // TryEvaluate
        bool success = stubEvaluator.TryEvaluate(expression, out double result, out string? formatted);
        Assert.True(success);
        Assert.Equal(expectedValue, result, precision: 4);
        Assert.Equal(expectedFormatted, formatted);
    }

    [Fact]
    public void StubMathEvaluator_RegisterCustomStub_OverridesOrAddsStub()
    {
        var stubEvaluator = new StubMathEvaluator();

        // Custom stub with explicit format
        stubEvaluator.RegisterStub("custom_calc", 999999.0, "999 999 custom");
        bool success = stubEvaluator.TryEvaluate("custom_calc", out double result, out string? formatted);
        Assert.True(success);
        Assert.Equal(999999.0, result);
        Assert.Equal("999 999 custom", formatted);

        // Custom stub without explicit format (uses FormatResult)
        stubEvaluator.RegisterStub("3000 * 2", 6000.0);
        bool success2 = stubEvaluator.TryEvaluate("3000 * 2", out double result2, out string? formatted2);
        Assert.True(success2);
        Assert.Equal(6000.0, result2);
        Assert.Equal("6 000", formatted2);
    }

    [Fact]
    public void StubMathEvaluator_UnregisteredExpression_FallsBackToRealEngine()
    {
        var stubEvaluator = new StubMathEvaluator();

        // "50 * 3" is not in the default stubs dictionary, should fallback to MathEvaluator
        bool success = stubEvaluator.TryEvaluate("50 * 3", out double result, out string? formatted);
        Assert.True(success);
        Assert.Equal(150.0, result);
        Assert.Equal("150", formatted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StubMathEvaluator_InvalidInput_ReturnsFalse(string? input)
    {
        var stubEvaluator = new StubMathEvaluator();
        bool success = stubEvaluator.TryEvaluate(input, out double result, out string? formatted);
        Assert.False(success);
        Assert.Equal(0.0, result);
        Assert.Null(formatted);
    }

    #endregion

    #region DefaultMathEvaluator Tests

    [Fact]
    public void DefaultMathEvaluator_ImplementsIMathEvaluator_DelegatesAccurately()
    {
        IMathEvaluator evaluator = DefaultMathEvaluator.Instance;

        Assert.Equal(44.0, evaluator.Evaluate("2 * (15 + 7)"));
        Assert.True(evaluator.TryEvaluate("100 + 20%", out double res, out string? fmt));
        Assert.Equal(120.0, res);
        Assert.Equal("120", fmt);
        Assert.Equal("276 939.0765", evaluator.FormatResult(276939.0765));
    }

    #endregion
}
