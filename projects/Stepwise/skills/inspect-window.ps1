<#
.SYNOPSIS
    Инспектирует активное (или найденное по PID) окно Windows и возвращает структурированный JSON.
.EXAMPLE
    ./skills/inspect-window.ps1
#>
[CmdletBinding()]
param(
    [int]$ProcessId = 0
)

$code = @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class WindowInspectorNative
{
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@

Add-Type -TypeDefinition $code -ErrorAction SilentlyContinue

$hwnd = [WindowInspectorNative]::GetForegroundWindow()

$pidOut = 0
[WindowInspectorNative]::GetWindowThreadProcessId($hwnd, [ref]$pidOut) | Out-Null

$len = [WindowInspectorNative]::GetWindowTextLength($hwnd)
$sb = New-Object System.Text.StringBuilder ($len + 1)
[WindowInspectorNative]::GetWindowText($hwnd, $sb, $sb.Capacity) | Out-Null
$title = $sb.ToString()

$rect = New-Object WindowInspectorNative+RECT
[WindowInspectorNative]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

$procName = "Unknown"
try {
    $procName = (Get-Process -Id $pidOut -ErrorAction SilentlyContinue).ProcessName
} catch {}

$result = [PSCustomObject]@{
    Hwnd = $hwnd.ToInt64()
    ProcessId = [int]$pidOut
    ProcessName = $procName
    WindowTitle = $title
    Bounds = [PSCustomObject]@{
        Left = $rect.Left
        Top = $rect.Top
        Right = $rect.Right
        Bottom = $rect.Bottom
        Width = ($rect.Right - $rect.Left)
        Height = ($rect.Bottom - $rect.Top)
    }
}

$result | ConvertTo-Json
