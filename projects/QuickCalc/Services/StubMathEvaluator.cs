using System;
using System.Collections.Generic;

namespace QuickCalc.Services;

/// <summary>
/// Test stub implementation of IMathEvaluator pre-configured with popular math operation stubs
/// (+, -, *, /, %) with support for registering custom stubs and graceful fallback to MathEvaluator.
/// </summary>
public class StubMathEvaluator : IMathEvaluator
{
    private readonly Dictionary<string, (double Value, string? FormattedResult)> _stubs =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IMathEvaluator _fallback;

    public StubMathEvaluator(IMathEvaluator? fallback = null)
    {
        _fallback = fallback ?? DefaultMathEvaluator.Instance;
        InitializeDefaultStubs();
    }

    private void InitializeDefaultStubs()
    {
        // Сложение
        RegisterStub("2 + 2", 4);
        RegisterStub("276 000 + 939.0765", 276939.0765);
        RegisterStub("100 + 50", 150);

        // Вычитание
        RegisterStub("1 000 000 - 250 000", 750000);
        RegisterStub("100 - 20", 80);

        // Умножение
        RegisterStub("12 * 12", 144);
        RegisterStub("12 x 12", 144);
        RegisterStub("6 * 7", 42);

        // Деление
        RegisterStub("100 / 4", 25);
        RegisterStub("1 000 000 / 4", 250000);
        RegisterStub("100 : 4", 25);

        // Проценты
        RegisterStub("100 + 20%", 120);
        RegisterStub("100 - 20%", 80);
        RegisterStub("20% от 150", 30);
        RegisterStub("20% of 150", 30);
    }

    /// <summary>
    /// Registers or overrides a test stub for a specific expression.
    /// </summary>
    public void RegisterStub(string expression, double value, string? formattedResult = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression cannot be null or empty.", nameof(expression));
        }

        _stubs[expression.Trim()] = (value, formattedResult);
    }

    public double Evaluate(string? expression)
    {
        if (!string.IsNullOrWhiteSpace(expression) && _stubs.TryGetValue(expression.Trim(), out var stub))
        {
            return stub.Value;
        }

        return _fallback.Evaluate(expression);
    }

    public bool TryEvaluate(string? expression, out double result)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            result = 0;
            return false;
        }

        if (_stubs.TryGetValue(expression.Trim(), out var stub))
        {
            result = stub.Value;
            return true;
        }

        return _fallback.TryEvaluate(expression, out result);
    }

    public bool TryEvaluate(string? expression, out double result, out string? formattedResult)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            result = 0;
            formattedResult = null;
            return false;
        }

        if (_stubs.TryGetValue(expression.Trim(), out var stub))
        {
            result = stub.Value;
            formattedResult = stub.FormattedResult ?? FormatResult(stub.Value);
            return true;
        }

        return _fallback.TryEvaluate(expression, out result, out formattedResult);
    }

    public string FormatResult(double value) => _fallback.FormatResult(value);
}
