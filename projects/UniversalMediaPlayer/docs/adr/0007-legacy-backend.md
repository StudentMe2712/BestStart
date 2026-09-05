# ADR 0007: Policy for Secondary / Legacy Backends (MPC-BE / DirectShow)

- **Status:** Accepted
- **Date:** 2026-09-05
- **Deciders:** UniversalMediaPlayer Architecture Team

---

## 1. Context

The project requirements specify wide compatibility across modern, legacy, and exotic media formats. MPC-BE is referenced as a benchmark for Windows DirectShow playback and legacy container handling.

However:
1. MPC-BE and MPC Video Renderer are licensed strictly under **GNU General Public License v3.0 (GPLv3)**.
2. DirectShow is a 25-year-old COM-based architecture requiring registry registration or complex private graph creation (`IGraphBuilder`).
3. Modern `libmpv` leverages FFmpeg demuxers and decoders, covering virtually every legacy format (DivX, XviD, Indeo, Cinepak, RealMedia, MPEG-1/2) without DirectShow filters.

We must define a strict architectural policy regarding if, when, and how MPC-BE or DirectShow could ever be introduced.

---

## 2. Decision

1. **Default Single-Backend Stance:**
   - Universal Media Player will launch with **`libmpv` as its sole playback engine**.
   - No secondary DirectShow or MPC-BE backend will be implemented unless verifiable, reproducible compatibility failures are documented in `docs/test-matrix.md` where libmpv fails and DirectShow succeeds.

2. **Isolation Barrier (Strict GPLv3 Compliance):**
   - If a scenario arises where MPC-BE functionality is genuinely required (e.g. specialized DirectShow hardware capture cards or legacy TV tuners):
     - **Direct in-process linking is STRICTLY FORBIDDEN.**
     - Any integration must operate strictly **out-of-process** via an isolated IPC bridge (command-line launcher or named-pipe proxy) to preserve license boundaries.

3. **Empirical Gate:**
   - Any proposal to introduce a secondary engine must present:
     - A failing test case in `tests/media/problematic/`.
     - An approved ADR proving why libmpv configuration adjustments (e.g. custom demuxer flags) cannot resolve the issue.

---

## 3. Consequences

### Positive:
- Avoids monumental overengineering and code bloat (no DirectShow graph builder maintenance, no COM filter merit debugging).
- Keeps the application codebase clean, modern, and legally unencumbered.

### Negative:
- None for standard media playback, as libmpv provides equivalent or superior format support.
