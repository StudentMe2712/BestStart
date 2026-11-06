namespace QuickCalc.Services;

/// <summary>
/// Interface for mathematical expression evaluation and result formatting.
/// </summary>
public interface IMathEvaluator
{
    /// <summary>
    /// Evaluates a mathematical expression string and returns the double result.
    /// </summary>
    double Evaluate(string? expression);

    /// <summary>
    /// Safely attempts to evaluate a mathematical expression.
    /// </summary>
    bool TryEvaluate(string? expression, out double result);

    /// <summary>
    /// Safely attempts to evaluate an expression and returns both the numeric result and formatted string.
    /// </summary>
    bool TryEvaluate(string? expression, out double result, out string? formattedResult);

    /// <summary>
    /// Formats a double value into a clean human-readable string with space thousands grouping.
    /// </summary>
    string FormatResult(double value);
}
