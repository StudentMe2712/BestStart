# Zenith Commander / Nexus Commander — Specification & Architecture Manual

> **Pivot Note**: Zenith Commander has been evolved and refactored into **Nexus Commander** (Modern Windows Explorer / WinUI style file manager with mouse-first UX).
> See [`nexus_spec.md`](file:///C:/Users/Mila/Desktop/BestStart/projects/ZenithCommander/nexus_spec.md) for full architectural documentation of Nexus Commander.

---

## 1. Pivot Summary: Zenith Commander ➔ Nexus Commander

1. **Branding & Architecture**:
   - Application evolved from legacy Total Commander dual-panel to modern single-pane Windows Explorer / Files App (WinUI).
   - Window Title: **Nexus Commander**.
   - Custom borderless window with `WindowStyle="None"`, `AllowsTransparency="True"`, and modern Title Bar (`🌌 NEXUS COMMANDER`).

2. **Sidebar (Navigation Pane)**:
   - Pinned Quick Access folders (`Desktop`, `Downloads`, `Documents`, `Pictures`, `Music`, `Videos`, `Home`).
   - Logical Drives (`C:`, `D:`, etc.) with free/total storage telemetry and active selection highlight.

3. **Top Command & Navigation Bar**:
   - Navigation controls: `Back (◀)`, `Forward (▶)`, `Up (⬆)`, `Refresh (🔄)`.
   - Interactive Clickable Breadcrumb bar with editable `TextBox` switch mode (`Ctrl+L` / `Alt+D`).
   - Live Search / Substring Filter box (`Ctrl+F`).
   - Action Ribbon Toolbar (`➕ New Folder`, `📋 Copy`, `✂ Cut`, `📥 Paste`, `✏️ Rename`, `🗑️ Delete`, `ℹ️ Properties`, `💻 Terminal`).

4. **Main View (File & Folder List)**:
   - Spacious virtualized `ListView` (`VirtualizingStackPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`).
   - Row height ~36px, semantic glyph icons, modified timestamp, file category type, and human-readable size formatting.
   - Comprehensive Right-Click Context Menus on files/folders and empty background.

5. **Build Status**:
   - Verified with `dotnet build projects/ZenithCommander/ZenithCommander.csproj -c Release` (0 Warnings, 0 Errors).