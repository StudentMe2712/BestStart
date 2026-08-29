# QuickCalc — Minimalist Floating Calculator Widget

QuickCalc is a sleek, ultra-fast floating calculator widget for Windows built with .NET 8 and WPF. It operates with a global system hotkey (`Alt + Q`), evaluates mathematical expressions in real time using a custom culture-invariant recursive descent parser, and allows instant copying of calculated results to the clipboard.

---

## 🚀 Features

- **Global Hotkey Toggle**: `Alt + Q` summons or dismisses the calculator from anywhere in Windows.
- **Floating Spotlight-Style UI**: Borderless, semi-transparent window with rounded corners, dark theme (`#252526`), and ambient drop shadow.
- **Real-Time Evaluation**: Dynamically evaluates mathematical expressions as you type without pressing calculate.
- **Culture-Invariant Engine**: Fully culture-independent parsing; both `.` and `,` are accepted as decimal separators without regional Windows culture issues (e.g. `ru-RU`, `de-DE`, `fr-FR`).
- **Rich Mathematical Syntax**:
  - **Arithmetic Operators**: `+`, `-`, unary `+x` / `-x`, `*`, `/`, `%` (modulo)
  - **Multiplication Aliases**: `*`, `x`, `X`, `×` (e.g. `12 x 12`, `12 × 12`, `2*15`)
  - **Division Aliases**: `/`, `÷` (always floating-point arithmetic, e.g. `10 ÷ 4` = `2.5`)
  - **Power / Exponentiation**: `^` or `**` (e.g. `2^8` = `256`, `2**3` = `8`, `2^3^2` = `512`)
  - **Parentheses**: Arbitrary nested expressions `(...)`
  - **Implicit Multiplication**: e.g. `2(15 + 7)`, `2pi`, `(2+3)(4+5)`, `2sqrt(9)`
  - **Scientific Constants**: `pi` / `PI` (`Math.PI`), `e` / `E` (`Math.E`)
  - **Scientific Functions**: `sqrt(...)`, `abs(...)`, `sin(...)`, `cos(...)`, `tan(...)`, `log(...)` (base 10), `ln(...)` (natural log), `round(...)`, `floor(...)`, `ceil(...)` / `ceiling(...)`
  - **Scientific Notation**: `1e3`, `1.5e-2`, `2.5e+2`
  - **Clean Floating-Point Formatting**: Trims trailing zeros and automatically removes IEEE 754 precision artifacts (`0.1 + 0.2` = `0.3`).
- **Keyboard Shortcuts**:
  - `Enter`: Copies the current result to clipboard and hides the widget.
  - `Esc`: Hides the widget.
  - Mouse Drag: Move the floating widget anywhere on screen.
  - `✕` Close Button: Minimizes / hides widget back to hotkey listening state.

---

## 🛠️ Architecture & Tech Stack

- **Target Framework**: `.NET 8.0 Windows` (`net8.0-windows`)
- **UI Framework**: WPF (Windows Presentation Foundation) with XAML
- **Interoperability**: Win32 API (`user32.dll`) via `HwndSource` hook:
  - `RegisterHotKey` / `UnregisterHotKey` (`Alt + Q`, `MOD_ALT = 0x0001`, `VK_Q = 0x51`)
- **Evaluation Engine (`QuickCalc.Services.MathEvaluator`)**:
  - Custom Lexer and Recursive Descent Parser.
  - Strictly operates under `CultureInfo.InvariantCulture`.
  - Normalizes commas and dots to standard decimals while tokenizing.
  - Handles operator precedence: Primary & Functions > Exponentiation (`^`, `**`) > Unary (`+`, `-`) > Multiplicative (`*`, `/`, `%`, implicit multiply) > Additive (`+`, `-`).
- **Testing Framework**: xUnit with .NET 8 test runner (`QuickCalc.Tests`).

---

## 📂 Project Structure

```
projects/
├── QuickCalc/
│   ├── QuickCalc.csproj          # .NET 8 WPF Project Configuration
│   ├── App.xaml                  # Global styles and resources
│   ├── App.xaml.cs               # Application entry point
│   ├── MainWindow.xaml           # Borderless floating UI layout
│   ├── MainWindow.xaml.cs        # WinAPI hotkey, UI bindings & evaluation integration
│   ├── Services/
│   │   └── MathEvaluator.cs      # Culture-invariant Recursive Descent Math Parser
│   └── quickcalc_spec.md         # Specification, testing guide & checklist
└── QuickCalc.Tests/
    ├── QuickCalc.Tests.csproj    # xUnit Unit Test Project Configuration
    └── MathEvaluatorTests.cs     # Comprehensive test matrix for MathEvaluator
```

---

## ✅ Implementation Checklist

- [x] Create project directory and setup `QuickCalc.csproj` targeting `net8.0-windows` with WPF enabled.
- [x] Configure `App.xaml` and `App.xaml.cs` with custom styles and resources.
- [x] Design `MainWindow.xaml`:
  - [x] Borderless window (`WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"`).
  - [x] Dark rounded border (`CornerRadius="12"`, `#252526` background, `#3E3E42` border brush, drop shadow).
  - [x] Input row: Large `TextBox` (FontSize 22), placeholder text, custom caret, and minimalist close button.
  - [x] Result row: Real-time formatted result `TextBlock` (FontSize 18, `#4EC9B0`) and hotkey tips.
- [x] Implement `MathEvaluator.cs`:
  - [x] Lexer tokenizing numbers, aliases (`x`, `×`, `÷`, `**`), operators, constants, and functions.
  - [x] Recursive descent parser for additive, multiplicative, unary, power, and primary expressions.
  - [x] Complete culture independence (`CultureInfo.InvariantCulture`), resolving the Russian Windows `2*15 = 300` bug.
  - [x] Clean float formatting and trailing zero removal.
- [x] Integrate `MathEvaluator` in `MainWindow.xaml.cs`:
  - [x] WinAPI global hotkey registration (`Alt + Q`) on `OnSourceInitialized` via `HwndSource.AddHook`.
  - [x] Real-time calculation on `InputTextBox.TextChanged`.
  - [x] Enter key to copy result to clipboard and hide window.
  - [x] Escape key and Close button to hide window.
  - [x] Border left-click dragging support via `DragMove()`.
- [x] Create Unit Tests (`QuickCalc.Tests`):
  - [x] Standard operations, operator precedence, culture invariance (`ru-RU`, `de-DE`, `fr-FR`, `tr-TR`).
  - [x] Scientific functions, constants, exponentiation, aliases, implicit multiplication, error handling.
- [x] 100% Green test execution with `dotnet test`.

---

## 🧪 Testing & Verification Guide

### 1. Build the Projects
From repository root:
```bash
dotnet build projects/QuickCalc/QuickCalc.csproj
dotnet build projects/QuickCalc.Tests/QuickCalc.Tests.csproj
```
Verify output reports **0 Errors** and **0 Warnings**.

### 2. Run the Unit Test Suite
Execute the xUnit test runner:
```bash
dotnet test projects/QuickCalc.Tests/QuickCalc.Tests.csproj
```
Expected output: **93 passed, 0 failed, 0 skipped**.

### 3. Key Test Matrix

| Expression | Expected Result | Verified Feature |
| :--- | :--- | :--- |
| `2*15` | `30` | Culture-invariant multiplication (ru-RU bug fixed) |
| `2 * 15` | `30` | Spaced multiplication |
| `10 / 4` | `2.5` | Floating-point division |
| `5,5 * 2` | `11` | Comma as decimal separator |
| `5.5 * 2` | `11` | Dot as decimal separator |
| `2^8` | `256` | Exponentiation operator (`^`) |
| `2 ** 3` | `8` | Exponentiation alias (`**`) |
| `-5 + 3` | `-2` | Unary negation |
| `2 * (15 + 7)` | `44` | Parenthesized sub-expressions |
| `10 % 3` | `1` | Modulo remainder |
| `sqrt(144)` | `12` | Square root function |
| `12 x 12` | `144` | Letter `x` multiplication alias |
| `12 × 12` | `144` | Unicode `×` multiplication alias |
| `10 ÷ 4` | `2.5` | Unicode `÷` division alias |
| `pi * 2` | `6.2831853...` | Mathematical constant `pi` |
| `ln(e)` | `1` | Natural logarithm and Euler's constant `e` |
| `2(3 + 4)` | `14` | Implicit multiplication with parentheses |
| `2 ^ 3 ^ 2` | `512` | Right-associative exponentiation |
| `Thread.CurrentCulture = "ru-RU"` | `2*15 = 30` | System culture resilience |

### 4. Interactive Application Verification
Launch the application:
```bash
dotnet run --project projects/QuickCalc/QuickCalc.csproj
```
1. Press `Alt + Q` to summon the widget.
2. Type `2*15` ➔ Instant `= 30`.
3. Type `5,5 * 2` ➔ Instant `= 11`.
4. Press `Enter` ➔ Window closes, `30` or `11` is copied to clipboard.
