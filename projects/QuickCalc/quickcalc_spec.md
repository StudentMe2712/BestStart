# QuickCalc — Minimalist Floating Calculator Widget

QuickCalc is a sleek, ultra-fast floating calculator widget for Windows built with .NET 8 and WPF. It operates with a global system hotkey (`Alt + Q`), evaluates mathematical expressions in real time, and allows instant copying of calculated results to the clipboard.

---

## 🚀 Features

- **Global Hotkey Toggle**: `Alt + Q` summons or dismisses the calculator from anywhere in Windows.
- **Floating Spotlight-Style UI**: Borderless, semi-transparent window with rounded corners, dark theme (`#252526`), and ambient drop shadow.
- **Real-Time Evaluation**: Dynamically evaluates standard math expressions as you type without pressing calculate.
- **Flexible Syntax Support**:
  - Operators: `+`, `-`, `*`, `/`, `%` (modulo), `(`, `)`
  - Multiplication aliases: `x`, `X`, `×` (e.g. `2x3`, `5 * 4`)
  - Division aliases: `/`, `÷` (e.g. `10 ÷ 4`)
  - Decimal separators: `.` and `,` (e.g. `3,14 + 2`)
  - Automatic float promotion to prevent integer truncation (`10 / 4` evaluates to `2.5`).
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
- **Evaluation Engine**: `System.Data.DataTable.Compute` with regex pre-processing and float literal normalization.

---

## 📂 Project Structure

```
projects/QuickCalc/
├── QuickCalc.csproj          # .NET 8 WPF Project Configuration
├── App.xaml                  # Global styles and resources
├── App.xaml.cs               # Application entry point
├── MainWindow.xaml           # Borderless floating UI layout
├── MainWindow.xaml.cs        # WinAPI hotkey, math parser & UI logic
└── quickcalc_spec.md         # Specification, testing guide & checklist
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
- [x] Implement `MainWindow.xaml.cs`:
  - [x] WinAPI global hotkey registration (`Alt + Q`) on `OnSourceInitialized` via `HwndSource.AddHook`.
  - [x] Window visibility & focus toggle on hotkey activation.
  - [x] Real-time math evaluation on `InputTextBox.TextChanged` using `DataTable.Compute`.
  - [x] Float division normalization, decimal conversion (comma to dot), and operator aliases (`x`, `×`, `÷`).
  - [x] Enter key to copy result to clipboard and hide window.
  - [x] Escape key and Close button to hide window.
  - [x] Border left-click dragging support via `DragMove()`.
  - [x] Safe unregistration of hotkey in `OnClosed`.
- [x] Build and compile verification (`dotnet build`) with 0 errors and 0 warnings.

---

## 🧪 Testing & Verification Guide

### 1. Build the Project
Run the following command from the repository root:
```bash
dotnet build projects/QuickCalc/QuickCalc.csproj
```
Ensure build output reports **0 Errors** and **0 Warnings**.

### 2. Run the Application
Launch the application:
```bash
dotnet run --project projects/QuickCalc/QuickCalc.csproj
```

### 3. Verify Scenarios
1. **Initial Display & Focus**:
   - The dark floating widget appears centered on the screen.
   - The input textbox is automatically focused with the placeholder `"Type a math expression, e.g. 2 * (15 + 7)..."`.
2. **Real-Time Math Evaluation**:
   - Type `2 * (15 + 7)` ➔ Result updates in real time to `= 44`.
   - Type `10 / 4` ➔ Result displays `= 2.5` (floating-point division verified).
   - Type `5,5 * 2` ➔ Result displays `= 11` (comma treated as decimal point).
   - Type `12 x 12` or `12 × 12` ➔ Result displays `= 144` (`x`/`×` alias verified).
   - Type invalid/incomplete math like `15 + ` ➔ Result gracefully displays `= ...` without error dialogues.
3. **Clipboard & Dismissal**:
   - With a valid expression (e.g. `25 * 4` = `100`), press `Enter`.
   - The window hides automatically.
   - Paste (`Ctrl + V`) in any text editor ➔ `100` is pasted.
4. **Global Hotkey (`Alt + Q`)**:
   - While QuickCalc is hidden, press `Alt + Q` anywhere in Windows.
   - The widget instantly appears, foregrounds, and focuses the input with text pre-selected for quick replacement.
   - Press `Alt + Q` again while active ➔ Widget hides.
5. **Escape Key & Window Drag**:
   - Press `Esc` ➔ Widget hides.
   - Click and drag anywhere on the widget border ➔ Widget moves smoothly across the screen.
