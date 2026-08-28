---
name: tailwind-dashboard-ui
description: Design principles and UI patterns for building high-density, monochrome analytical dashboards with Tailwind CSS.
---

# Tailwind Monochrome Analytical Dashboard Standards

This skill sets design conventions for strict, professional, data-dense analytical user interfaces.

## 1. Color System
- **Base Background:** `#090a0f` (Ultra-deep slate)
- **Cards & Surfaces:** `#12141c` with subtle borders `#1e2230`
- **Typography:** Primary text `#e2e8f0`, secondary `#94a3b8`, muted `#64748b`
- **Accent:** Blue `#3b82f6` (interactive elements, active states)

## 2. Status & Risk Badges
- **AI Viability Score:**
  - `9-10`: Emerald accent badge with subtle glow (`bg-emerald-950/60 text-emerald-300 border-emerald-800/60`)
  - `7-8`: Blue/Cyan badge (`bg-blue-950/60 text-blue-300 border-blue-800/60`)
  - `4-6`: Amber badge (`bg-amber-950/60 text-amber-300 border-amber-800/60`)
  - `1-3`: Slate/Rose badge
- **Scam Risk Probability:**
  - `> 50%`: High Risk Rose/Red badge (`bg-rose-950/70 text-rose-300 border-rose-800/70`)
  - `20-50%`: Warning Amber badge (`bg-amber-950/70 text-amber-300 border-amber-800/70`)
  - `< 20%`: Safe Emerald badge (`bg-emerald-950/50 text-emerald-400 border-emerald-800/50`)

## 3. Layout Conventions
- **Fixed Topbar:** High-level operational counters (Total, Confirmed Trends, Unreviewed, Avg Viability), system status, and manual scan triggers.
- **Sidebar Filters:** Instant filtering with zero page reload (Search, Status tabs, AI score threshold, Scam risk slider, Source selector).
- **Data Grid:** Crisp row borders, hover highlights, one-click review toggles, modal drill-down inspection.
