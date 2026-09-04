<#
.SYNOPSIS
    Инспектирует дерево Microsoft UI Automation для указанного процесса или окна и выводит JSON.
.EXAMPLE
    ./skills/inspect-ui.ps1 -ProcessName "notepad" -MaxDepth 3
#>
[CmdletBinding()]
param(
    [string]$ProcessName = "",
    [int]$ProcessId = 0,
    [string]$WindowTitle = "",
    [int]$MaxDepth = 3
)

Add-Type -AssemblyName UIAutomationClient -ErrorAction SilentlyContinue
Add-Type -AssemblyName UIAutomationTypes -ErrorAction SilentlyContinue

$rootElement = $null

if ($ProcessId -gt 0) {
    $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($proc -and $proc.MainWindowHandle -ne 0) {
        $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    }
} elseif ($ProcessName) {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc -and $proc.MainWindowHandle -ne 0) {
        $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    }
}

if (-not $rootElement) {
    $rootElement = [System.Windows.Automation.AutomationElement]::RootElement
}

function Dump-Element($el, $depth) {
    if (-not $el -or $depth -gt $MaxDepth) { return $null }

    $name = ""
    $type = "Unknown"
    $autoId = ""
    $class = ""
    $bounds = $null

    try { $name = $el.Current.Name } catch {}
    try { $type = $el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "") } catch {}
    try { $autoId = $el.Current.AutomationId } catch {}
    try { $class = $el.Current.ClassName } catch {}
    try {
        $r = $el.Current.BoundingRectangle
        if (-not $r.IsEmpty) {
            $bounds = [PSCustomObject]@{
                X = $r.X; Y = $r.Y; Width = $r.Width; Height = $r.Height
            }
        }
    } catch {}

    $childrenList = @()
    if ($depth -lt $MaxDepth) {
        try {
            $condition = [System.Windows.Automation.Condition]::TrueCondition
            $children = $el.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
            foreach ($child in $children) {
                $childObj = Dump-Element $child ($depth + 1)
                if ($childObj) { $childrenList += $childObj }
            }
        } catch {}
    }

    return [PSCustomObject]@{
        Name = $name
        ControlType = $type
        AutomationId = $autoId
        ClassName = $class
        Bounds = $bounds
        Children = $childrenList
    }
}

$tree = Dump-Element $rootElement 0
$tree | ConvertTo-Json -Depth ($MaxDepth + 2)
