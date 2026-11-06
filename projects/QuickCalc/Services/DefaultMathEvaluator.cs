namespace QuickCalc.Services;

/// <summary>
/// Default implementation of IMathEvaluator delegating directly to the static MathEvaluator engine.
/// </summary>
public class DefaultMathEvaluator : IMathEvaluator
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static DefaultMathEvaluator Instance { get; } = new();

    public double Evaluate(string? expression) => MathEvaluator.Evaluate(expression);

    public bool TryEvaluate(string? expression, out double result) =>
        MathEvaluator.TryEvaluate(expression, out result);

    public bool TryEvaluate(string? expression, out double result, out string? formattedResult) =>
        MathEvaluator.TryEvaluate(expression, out result, out formattedResult);

    public string FormatResult(double value) => MathEvaluator.FormatResult(value);
}
