# QuickCalc — Minimalist Floating Calculator Widget

QuickCalc is a sleek, ultra-fast floating calculator widget for Windows built with .NET 8 and WPF. It operates with a global system hotkey (`Alt + Q`), evaluates mathematical expressions in real time using a custom culture-invariant recursive descent parser tailored for everyday calculations, and allows instant copying of calculated results to the clipboard.

---

## 🚀 Features

- **Global Hotkey Toggle**: `Alt + Q` summons or dismisses the calculator from anywhere in Windows.
- **Floating Spotlight-Style UI**: Borderless, semi-transparent window with rounded corners, dark theme (`#252526`), and ambient drop shadow.
- **Complete Russian Localization**:
  - Placeholder: `Введите математическое выражение, напр. 2 * (15 + 7)...`
  - Bottom hint: `Нажмите Enter, чтобы скопировать | Esc — закрыть`
  - Close button tooltip: `Закрыть (Esc)`
  - Error indicator on division by zero or invalid expressions: `= Ошибка`
  - Quick example navigation via `Up` / `Down` arrows in `InputTextBox`:
    `276 939.0765 + 1 000`, `1 000 000 - 250 000`, `12 * 12`, `100 / 4`, `100 + 20%`, `20% от 150`.
- **Space Thousands Grouping (Separators)**:
  - Clean separation of thousands, millions, and billions with spaces while preserving dot for decimal fractions.
  - Examples: `276939.0765` ➔ `276 939.0765`, `-276939.0765` ➔ `-276 939.0765`, `1000000` ➔ `1 000 000`, `0` ➔ `0`.
  - Implemented using `NumberFormatInfo` (`NumberGroupSeparator = " "`, `NumberDecimalSeparator = "."`, `NumberGroupSizes = new[] { 3 }`) with format string `#,##0.############`.
  - Preserves scientific exponential notation for extreme values (`>= 1e15` or `< 1e-6`).
- **Real-Time Evaluation**: Dynamically evaluates mathematical expressions as you type without pressing calculate.
- **Everyday Percentage Semantics (`%`)**:
  - **Postfix Percentage**: `50%` = `0.5`, `100%` = `1`, `5%` = `0.05`
  - **Additive Percentage (Tax / Tip / Markup)**: `100 + 20%` = `120` (`100 + (100 * 0.20)`), `2500 + 13%` = `2 825`, `200 + 5.5%` = `211`
  - **Subtractive Percentage (Discounts)**: `100 - 20%` = `80` (`100 - (100 * 0.20)`), `1500 - 15%` = `1 275`, `200 - 5.5%` = `189`
  - **Multiplicative Percentage**: `100 * 20%` = `20`, `20% * 100` = `20`, `100 x 20%` = `20`
  - **Division by Percentage**: `100 / 20%` = `500`, `100 : 20%` = `500`, `20% / 2` = `0.1`
  - **Chained Percentages**: `100 + 20% - 10%` = `108` (`(120) - 12`), `100 + 10% + 10%` = `121`, `1000 - 20% - 10%` = `720`
  - **Parenthesized Percentages**: `(100 + 50) + 10%` = `165`, `(200 - 50) - 10%` = `135`, `(100 + 20%)` = `120`
  - **Natural Language Aliases**: `20% of 150` = `30`, `20% от 150` = `30`, `50% of 200` = `100`, `15% от 2000` = `300`
  - **Direct Percentage Additions / Subtractions**: `20% + 30%` = `0.5`, `50% - 20%` = `0.3`, `10% + 20% + 30%` = `0.6`
- **Everyday Operator Aliases & Formatting**:
  - **Division Aliases**: `/`, `÷`, `:`, `\` (e.g. `100 : 4 = 25`, `100 ÷ 4 = 25`, `100 \ 4 = 25`)
  - **Multiplication Aliases**: `*`, `x`, `X`, `×`, `•`, `∙`, `·`, `⋅` (e.g. `12 x 12 = 144`, `12 • 12 = 144`)
  - **Power / Exponentiation**: `^` or `**` (e.g. `2^3 = 8`, `10^2 = 100`, `2^8 = 256`)
  - **Thousands Separators & Spaces in Numbers**: `1 000 000 + 500 000 = 1 500 000`, `10_000 * 2 = 20 000`, `2 500 + 13% = 2 825`
  - **Culture-Invariant Decimal Separators**: Both `.` and `,` are accepted seamlessly (`2,5 + 7,5 = 10`, `5,5 * 2 = 11`) without regional Windows culture issues (`ru-RU`, `de-DE`, `fr-FR`).
  - **Auto-Closing Unbalanced Parentheses on Live Evaluation**: `(10 + 5` ➔ `15`, `2 * (10 + 5` ➔ `30`, `sqrt(144` ➔ `12`, `(100 + 50 + 10%` ➔ `165`
- **Lightweight Everyday Engine**:
  - Streamlined engine focused on everyday arithmetic and finance calculations.
  - Complex unused functions (`sin`, `cos`, `tan`, `log`, `ln`, `ceil`, `floor`) removed for instant evaluation speed.
  - Clean floating-point formatting removing IEEE 754 precision artifacts (`0.1 + 0.2` = `0.3`).
  - Simple utilities retained: `sqrt(...)`, `abs(...)`, `round(...)`, and constants `pi`, `e`.
- **Keyboard Shortcuts**:
  - `Enter`: Copies the current result to clipboard and hides the widget.
  - `Esc`: Hides the widget.
  - `Up` / `Down`: Cycles through popular operation sample expressions.
  - Mouse Drag: Move the floating widget anywhere on screen.
  - `✕` Close Button: Minimizes / hides widget back to hotkey listening state.

---

## 🛠️ Architecture & Tech Stack

- **Target Framework**: `.NET 8.0 Windows` (`net8.0-windows`)
- **UI Framework**: WPF (Windows Presentation Foundation) with XAML
- **Interoperability**: Win32 API (`user32.dll`) via `HwndSource` hook:
  - `RegisterHotKey` / `UnregisterHotKey` (`Alt + Q`, `MOD_ALT = 0x0001`, `VK_Q = 0x51`)
- **Evaluation Architecture**:
  - **`IMathEvaluator`**: Interface defining `Evaluate(string?)`, `TryEvaluate(...)`, and `FormatResult(double)`.
  - **`DefaultMathEvaluator`**: Singleton implementation delegating to the static `MathEvaluator` engine.
  - **`StubMathEvaluator`**: Test stub implementation pre-configured with popular operation stubs (`+`, `-`, `*`, `/`, `%`), support for `RegisterStub(...)`, and automatic fallback to `DefaultMathEvaluator`.
  - **`MainWindow`**: Supports dependency injection of `IMathEvaluator` via constructor `MainWindow(IMathEvaluator evaluator)` with fallback to `DefaultMathEvaluator.Instance`.
- **Evaluation Engine (`QuickCalc.Services.MathEvaluator`)**:
  - Custom Lexer and Recursive Descent Parser with percentage-aware evaluation state.
  - Strictly operates under `CultureInfo.InvariantCulture`.
  - Normalizes commas and dots to standard decimals while tokenizing.
  - Handles number grouping / thousands separators (spaces, underscores).
  - Operator precedence: Primary & Postfix (`%`) > Exponentiation (`^`, `**`) > Unary (`+`, `-`) > Multiplicative (`*`, `/`, `÷`, `:`, `\`, `x`, `•`, `of`, `от`, implicit multiply) > Additive (`+`, `-`, `+ %`, `- %`).
- **Testing Framework**: xUnit with .NET 8 test runner (`QuickCalc.Tests`).

---

## 📂 Project Structure

```
projects/
├── QuickCalc/
│   ├── QuickCalc.csproj          # .NET 8 WPF Project Configuration
│   ├── App.xaml                  # Global styles and resources
│   ├── App.xaml.cs               # Application entry point
│   ├── MainWindow.xaml           # Borderless floating UI layout (Russian localized)
│   ├── MainWindow.xaml.cs        # WinAPI hotkey, UI bindings, DI & evaluation integration
│   ├── Services/
│   │   ├── IMathEvaluator.cs     # Abstraction interface for evaluation and formatting
│   │   ├── DefaultMathEvaluator.cs # Default IMathEvaluator implementation
│   │   ├── StubMathEvaluator.cs  # Test stub pre-loaded with popular operations (+, -, *, /, %)
│   │   └── MathEvaluator.cs      # Core Everyday Math & Percentage Recursive Descent Parser
│   └── quickcalc_spec.md         # Specification, testing guide & checklist
└── QuickCalc.Tests/
    ├── QuickCalc.Tests.csproj    # xUnit Unit Test Project Configuration
    ├── MathEvaluatorTests.cs     # Comprehensive test matrix for MathEvaluator (122 tests)
    └── PopularMathOperationsTests.cs # Popular operations, thousands grouping & StubMathEvaluator tests (59 tests)
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
  - [x] Full Russian localization for placeholder, hints, close button tooltip, and error status (`= Ошибка`).
  - [x] Arrow Up/Down navigation for sample expressions.
- [x] Implement `MathEvaluator.cs`:
  - [x] Everyday percentage semantics: postfix `50%`, additive `100 + 20% = 120`, subtractive `100 - 20% = 80`, multiplicative `100 * 20% = 20`, division `100 / 20% = 500`, chained `100 + 20% - 10% = 108`, parenthesized `(100 + 50) + 10% = 165`.
  - [x] Natural language percentage aliases: `20% of 150 = 30`, `20% от 150 = 30`.
  - [x] Direct percentage additions/subtractions without base: `20% + 30% = 0.5`.
  - [x] Everyday operator aliases: division (`/`, `÷`, `:`, `\`), multiplication (`*`, `x`, `X`, `×`, `•`, `∙`, `·`, `⋅`), exponentiation (`^`, `**`).
  - [x] Thousands separators and spaces in numbers (`1 000 000 + 500 000`, `10_000 * 2`, `2 500 + 13%`).
  - [x] Culture independence (`CultureInfo.InvariantCulture`) with comma and dot decimals (`2,5 + 7,5 = 10`).
  - [x] Space thousands grouping in `FormatResult`: `276939.0765` ➔ `276 939.0765`.
  - [x] Auto-closing unbalanced parentheses on live typing (`(10 + 5` ➔ `15`, `2 * (10 + 5` ➔ `30`).
  - [x] Simplified engine: removed unused heavy functions (`sin`, `cos`, `tan`, `log`, `ln`, `ceil`, `floor`), retained `sqrt`, `abs`, `round`, `pi`, `e`.
- [x] Implement Evaluator Abstraction & Stubs:
  - [x] `IMathEvaluator` interface.
  - [x] `DefaultMathEvaluator` singleton delegator.
  - [x] `StubMathEvaluator` test stub with default popular operations and custom registration.
- [x] Integrate `IMathEvaluator` in `MainWindow.xaml.cs`:
  - [x] Constructor dependency injection.
  - [x] WinAPI global hotkey registration (`Alt + Q`) on `OnSourceInitialized` via `HwndSource.AddHook`.
  - [x] Real-time calculation on `InputTextBox.TextChanged`.
  - [x] Up/Down sample switching.
  - [x] Enter key to copy result to clipboard and hide window (ignores "Error" and "Ошибка").
  - [x] Escape key and Close button to hide window.
  - [x] Border left-click dragging support via `DragMove()`.
- [x] Unit Tests (`QuickCalc.Tests`):
  - [x] 122 tests in `MathEvaluatorTests.cs`.
  - [x] 59 tests in `PopularMathOperationsTests.cs`.
  - [x] 100% Green test execution (181 passed, 0 failed, 0 skipped, 0 warnings).

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
Expected output: **181 passed, 0 failed, 0 skipped**.

### 3. Key Everyday Math Test Matrix

| Expression | Expected Result | Verified Feature |
| :--- | :--- | :--- |
| `276 939.0765 + 1 000` | `277 939.0765` | Space thousands grouping + decimal addition |
| `276 000 + 939.0765` | `276 939.0765` | User explicit sample test |
| `1 000 000 - 250 000` | `750 000` | Large number subtraction with space formatting |
| `1 000 000 000` | `1 000 000 000` | Billions space formatting |
| `50%` | `0.5` | Standalone / postfix percentage |
| `100 + 20%` | `120` | Additive percentage (tax/tip/markup: 100 + 20) |
| `2500 + 13%` | `2 825` | Everyday income tax calculation with space formatting |
| `100 - 20%` | `80` | Subtractive percentage (discounts: 100 - 20) |
| `1500 - 15%` | `1 275` | Everyday retail discount with space formatting |
| `100 * 20%` | `20` | Multiplicative percentage |
| `100 / 20%` | `500` | Division by percentage |
| `100 + 20% - 10%` | `108` | Chained percentage operations (`120 - 12`) |
| `(100 + 50) + 10%` | `165` | Parenthesized expression with percentage markup |
| `20% of 150` | `30` | English natural language percentage |
| `20% от 150` | `30` | Russian natural language percentage |
| `20% + 30%` | `0.5` | Direct percentage addition without base |
| `1 000 000 + 500 000` | `1 500 000` | Space thousands separators in input and output |
| `10_000 * 2` | `20 000` | Underscore thousands separators in input |
| `100 : 4` | `25` | European colon division alias |
| `100 ÷ 4` | `25` | Unicode division sign alias |
| `100 \ 4` | `25` | Backslash division alias |
| `12 x 12` | `144` | Letter `x` multiplication alias |
| `12 • 12` | `144` | Bullet multiplication alias |
| `2,5 + 7,5` | `10` | Comma decimal separator |
| `(10 + 5` | `15` | Auto-closing unclosed parentheses on live evaluation |
| `2 * (10 + 5` | `30` | Auto-closing nested expression |
| `2^3` | `8` | Exponentiation operator |
| `Thread.CurrentCulture = "ru-RU"` | `2*15 = 30` | Full culture invariance |

### 4. Interactive Application Verification
Launch the application:
```bash
dotnet run --project projects/QuickCalc/QuickCalc.csproj
```
1. Press `Alt + Q` to summon the widget.
2. Note the Russian placeholder: `Введите математическое выражение, напр. 2 * (15 + 7)...`
3. Press `Down` or `Up` arrows to cycle through popular operation sample expressions:
   - `276 939.0765 + 1 000` ➔ Instant `= 277 939.0765`
   - `1 000 000 - 250 000` ➔ Instant `= 750 000`
   - `12 * 12` ➔ Instant `= 144`
   - `100 / 4` ➔ Instant `= 25`
   - `100 + 20%` ➔ Instant `= 120`
   - `20% от 150` ➔ Instant `= 30`
4. Type `10 / 0` ➔ Instant `= Ошибка`.
5. Press `Enter` on a valid result ➔ Window closes, formatted result is copied to clipboard.
