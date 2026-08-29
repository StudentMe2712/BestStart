using System;
using System.Collections.Generic;

namespace NexusCommander.Models;

public class NavigationHistory
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private string? _currentPath;

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public string? CurrentPath => _currentPath;

    public void NavigateTo(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
            return;

        if (string.Equals(_currentPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrEmpty(_currentPath))
        {
            _backStack.Push(_currentPath);
        }

        _currentPath = newPath;
        _forwardStack.Clear();
    }

    public string? GoBack()
    {
        if (!CanGoBack) return null;

        if (!string.IsNullOrEmpty(_currentPath))
        {
            _forwardStack.Push(_currentPath);
        }

        _currentPath = _backStack.Pop();
        return _currentPath;
    }

    public string? GoForward()
    {
        if (!CanGoForward) return null;

        if (!string.IsNullOrEmpty(_currentPath))
        {
            _backStack.Push(_currentPath);
        }

        _currentPath = _forwardStack.Pop();
        return _currentPath;
    }

    public void Clear()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        _currentPath = null;
    }
}
