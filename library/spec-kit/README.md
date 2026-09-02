# 📐 GitHub Spec-Kit — Spec-Driven Development (SDD) Standard

> **Repository Standard for BestStart**  
> Based on [github/spec-kit](https://github.com/github/spec-kit)

Spec-Kit establishes a rigorous, deterministic, and repeatable **Spec-Driven Development (SDD)** workflow across the entire BestStart ecosystem.

---

## 🎯 Core Philosophy

In Spec-Driven Development:
1. **Specs are the Source of Truth**: Code is an artifact derived from specifications, not the other way around.
2. **Constitutional Governance**: Project principles, architectural constraints, and quality standards are codified in the project constitution (`constitution.md`) and non-negotiable.
3. **Structured Progression**: Requirements move through a deterministic pipeline:  
   `Constitution` ➔ `Specify` ➔ `Clarify` ➔ `Plan` ➔ `Tasks` ➔ `Analyze` ➔ `Implement` ➔ `Converge`.
4. **Append-Only Convergence**: Discrepancies between code and spec are resolved by appending traceable convergence tasks, preserving execution history.

---

## 📁 Standard Directory Structure

Every project utilizing spec-kit follows this layout:

`	ext
projects/<project-name>/
├── .specify/                         # Spec-kit configuration and engine
│   ├── memory/
│   │   └── constitution.md           # Project Constitution & Governance
│   ├── templates/                    # Local schema templates
│   │   ├── spec-template.md
│   │   ├── plan-template.md
│   │   ├── tasks-template.md
│   │   ├── constitution-template.md
│   │   └── checklist-template.md
│   ├── scripts/powershell/           # Execution scripts
│   ├── extensions/                   # Active extensions (bug, assess, git)
│   └── workflows/                    # Automation workflows
├── specs/                            # Feature specifications & plans
│   └── 001-feature-name/
│       ├── spec.md                   # Feature requirements & user stories (P1, P2...)
│       ├── plan.md                   # Architectural & technical design
│       ├── research.md               # Technical research & decisions (Phase 0)
│       ├── data-model.md             # Entities & schemas (Phase 1)
│       ├── contracts/                # API contracts / interfaces (Phase 1)
│       ├── quickstart.md             # Verification & test steps (Phase 1)
│       └── tasks.md                  # Prioritized, actionable task checklist
├── .claude/
│   └── skills/                       # Invocable agent skills
│       ├── speckit-specify/
│       ├── speckit-plan/
│       ├── speckit-tasks/
│       ├── speckit-implement/
│       └── ...
├── CLAUDE.md                         # Project instructions & guidelines
└── src/ (or app/)                    # Implementation code
`

---

## 🔄 Core SDD Lifecycle

`mermaid
flowchart LR
    C[Constitution] --> S[Specify]
    S --> CL[Clarify]
    CL --> P[Plan]
    P --> CHK[Checklist]
    CHK --> T[Tasks]
    T --> A[Analyze]
    A --> I[Implement]
    I --> CV[Converge]
    CV -- Unmet Work Found --> I
    CV -- Clean Match --> Done((Complete))
`

### 1. Constitution (/speckit-constitution)
- Defines fundamental project principles, non-negotiable constraints, tech stack standards, and governance.
- Stored at .specify/memory/constitution.md.
- All plans, specifications, and implementations are verified against this constitution.

### 2. Specify (/speckit-specify <description>)
- Creates feature specification under specs/<NNN-feature-name>/spec.md.
- Focuses purely on **WHAT** and **WHY**, strictly avoiding premature implementation details.
- Organizes requirements into prioritized, independently testable User Stories (P1, P2, P3), functional requirements (FR-001), and measurable success criteria (SC-001).

### 3. Clarify (/speckit-clarify) *(Optional Enhancement)*
- Scans spec.md for ambiguities, unspoken assumptions, or underspecified edge cases.
- Conducts structured inquiry to de-risk the spec before technical planning.

### 4. Plan (/speckit-plan)
- Converts spec.md into concrete technical architecture in specs/<NNN-feature-name>/plan.md.
- Performs Constitution checks, technology selections, data modeling (data-model.md), interface/API definitions (contracts/), and verification paths (quickstart.md).

### 5. Checklist (/speckit-checklist) *(Optional Enhancement)*
- Generates domain-specific quality assurance checklists to validate completeness and consistency.

### 6. Tasks (/speckit-tasks)
- Deconstructs the plan and specification into atomic, verifiable tasks in 	asks.md.
- Categorized into:
  - **Phase 1: Setup** (Project setup, dependencies)
  - **Phase 2: Foundational** (Database schemas, core routing, blocking prerequisites)
  - **Phase 3+: User Story Increments** (US1 [P1], US2 [P2], etc.) — each independently verifiable
  - **Phase N: Polish & Cross-Cutting Concerns**
- Flags parallelizable tasks with [P].

### 7. Analyze (/speckit-analyze) *(Optional Enhancement)*
- Performs comprehensive cross-artifact consistency verification between spec.md, plan.md, 	asks.md, and the codebase.

### 8. Implement (/speckit-implement)
- Systematically executes tasks from 	asks.md in dependency order.
- Enforces strict compliance with constitution and spec boundaries.

### 9. Converge (/speckit-converge)
- Audits the current codebase against spec.md, plan.md, and 	asks.md.
- Detects unmet acceptance criteria, missing edge case handlers, or incomplete tasks.
- Appends remaining work as a new ## Phase N: Convergence section in 	asks.md for /speckit-implement to resolve.

---

## 🛠 Extension Workflows

### 🐛 Bug Triage & Fix Pipeline
Structured process for diagnosing and fixing defects without regressions:
1. **/speckit-bug-assess**: Reproduces the bug, isolates root causes, documents environmental triggers, and determines severity.
2. **/speckit-bug-fix**: Applies surgical code changes guided by test-driven development (Red ➔ Green ➔ Refactor).
3. **/speckit-bug-test**: Validates fix with automated regression suites, boundary tests, and ensures no collateral regressions.

### 💡 Idea Assessment Pipeline
Structured product discovery before writing formal feature specs:
1. **/speckit-assess-intake**: Captures raw ideas, user feedback, or market opportunities.
2. **/speckit-assess-shape**: Explores potential solutions, constraints, and user personas.
3. **/speckit-assess-research**: Technical and market feasibility study.
4. **/speckit-assess-define**: Structures scope, initial requirements, and value proposition.
5. **/speckit-assess-decide**: Go/No-Go decision matrix; if Go, seamlessly hands off to /speckit-specify.

---

## 💻 CLI Tooling (specify-cli)

The CLI provides automated scaffolding, health checking, and management:

`powershell
# Check installation and prerequisites
specify check

# Scaffold spec-kit into an existing directory
specify init --here --force --non-interactive --integration claude --extension bug --extension assess --extension git

# Check CLI version
specify version
`

---

## 📦 BestStart Integration

All newly scaffolded projects in BestStart via ./scripts/new-project.ps1 automatically receive full Spec-Kit support.
Existing projects can pull spec-kit tooling via:
`powershell
./scripts/add-tools.ps1 -Project <project_name> -Skills spec-kit -Commands speckit
`
