# BestStart Workspace Constitution

## Core Principles

### I. Spec-Driven Development (NON-NEGOTIABLE)
All new and existing projects within BestStart must be specified, planned, and implemented using github/spec-kit (Spec-Driven Development). Code is an artifact derived from specifications (spec.md), technical architecture plans (plan.md), and atomic tasks (	asks.md) stored in specs/. No implementation begins without a structured specification.

### II. Simplicity & Surgical Precision (Karpathy Baseline)
1. **Think Before Coding**: Analyze requirements, identify trade-offs, and clarify ambiguities before touching code.
2. **Simplicity First**: Write the minimum necessary code. Zero speculative features, zero unnecessary abstractions.
3. **Surgical Changes**: Touch only what is required for the specific task; preserve unrelated code, docstrings, and formatting.
4. **Goal-Driven Verification**: Every feature and user story must define verifiable acceptance criteria and validation steps.

### III. Independent Testability & Quality Gates
1. **Prioritized User Stories**: Requirements must be broken into independently testable user stories (P1 MVP, P2, P3).
2. **Test-First Bug Fixing**: For bug triage and resolution, reproduce the defect with a failing test first, apply the fix, and verify zero regressions (speckit-bug-assess -> speckit-bug-fix -> speckit-bug-test).
3. **Continuous Convergence**: Use speckit-converge to audit implementation completeness against the spec and plan.

### IV. Docker-First Isolation
Every application must be containerized (docker-compose.yml, Dockerfile, .env) with auto-allocated free host ports (APP_PORT, DB_PORT) to prevent host pollution and cross-project port collision.

### V. Continuous Learning (Lessons Protocol)
Every non-obvious bug, architectural mistake, or debugging insight must be recorded into the project-specific and/or global LESSONS.md using the standard format (Problem, Root Cause, Fix, Rule, Scope).

## Development Lifecycle & Standards

1. **Phase 0: Governance & Specification** — Ratify constitution, create specs/<NNN-feature>/spec.md with prioritized user stories.
2. **Phase 1: Architecture & Technical Planning** — Produce plan.md, 
esearch.md, data-model.md, and contracts/.
3. **Phase 2: Task Breakdown** — Generate structured 	asks.md with Setup, Foundational, and User Story increments.
4. **Phase 3: Execution & Verification** — Implement tasks incrementally, verifying each user story independently.
5. **Phase 4: Convergence Audit** — Run speckit-converge to verify 100% specification parity, appending any remaining gap tasks.

## Governance
This Constitution is the highest architectural authority in the BestStart workspace. All autonomous agents, subagents, and developers must adhere to its principles. Deviations or added architectural complexity must be formally justified in the Implementation Plan.

**Version**: 1.0.0 | **Ratified**: 2026-09-02 | **Scope**: BestStart Global Standard
