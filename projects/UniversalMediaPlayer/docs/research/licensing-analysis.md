# UniversalMediaPlayer: Comprehensive Licensing Audit and Strategic Compliance Architecture

**Document Version:** 1.0.0  
**Status:** Approved Architectural Baseline  
**Target Path:** [`projects/UniversalMediaPlayer/docs/research/licensing-analysis.md`](file:///C:/Users/Mila/Desktop/BestStart/projects/UniversalMediaPlayer/docs/research/licensing-analysis.md)  
**Author:** Senior Media Technology Researcher & Systems Engineer  
**Date:** September 2026  

---

## Table of Contents

1. [Executive Summary & Legal Architecture](#1-executive-summary--legal-architecture)
2. [Target License Evaluation](#2-target-license-evaluation)
   - [Candidate Licenses Analysis (MIT, Apache 2.0, LGPLv3, GPLv3)](#candidate-licenses-analysis)
   - [Comparative Trade-off Matrix](#comparative-trade-off-matrix)
   - [The "Permissive Core vs. Copyleft Distribution" Stance](#the-permissive-core-vs-copyleft-distribution-stance)
   - [Recommended Licensing Strategy](#recommended-licensing-strategy)
3. [Comprehensive Dependency Audit](#3-comprehensive-dependency-audit)
   - [`libmpv` (`libmpv-2.dll`)](#libmpv-libmpv-2dll)
   - [`FFmpeg` (Multi-library Multimedia Framework)](#ffmpeg-multi-library-multimedia-framework)
   - [`libass` (Advanced SubStation Alpha Rendering Engine)](#libass-advanced-substation-alpha-rendering-engine)
   - [`MediaInfo` / `MediaInfoLib`](#mediainfo--mediainfolib)
   - [`libplacebo` (Next-Generation GPU Video Processing)](#libplacebo-next-generation-gpu-video-processing)
   - [`WinUI 3` / `Windows App SDK`](#winui-3--windows-app-sdk)
   - [`.NET 8/9` Runtime and Base Class Libraries (BCL)](#net-89-runtime-and-base-class-libraries-bcl)
   - [`MPC-BE` (Media Player Classic - Black Edition)](#mpc-be-media-player-classic---black-edition)
   - [`Light Alloy 4.4` (Delphi Source) & `Light Alloy 4.11.2` (Proprietary Freeware)](#light-alloy-44-delphi-source--light-alloy-4112-proprietary-freeware)
4. [License Compatibility Matrix & Windows Linking Semantics](#4-license-compatibility-matrix--windows-linking-semantics)
   - [Full License Compatibility Matrix](#full-license-compatibility-matrix)
   - [Windows PE/COFF Dynamic Linking vs. Static Linking](#windows-pecoff-dynamic-linking-vs-static-linking)
   - [The FSF Stance on Address Space Dynamic Linking](#the-fsf-stance-on-address-space-dynamic-linking)
   - [P/Invoke and C-ABI Marshaling Boundaries](#pinvoke-and-c-abi-marshaling-boundaries)
   - [The Windows System Library Exception](#the-windows-system-library-exception)
   - [Source Code Disclosure Obligations: GPLv3 vs. LGPLv2.1](#source-code-disclosure-obligations-gplv3-vs-lgplv21)
5. [Clean-Room Engineering & Legacy Decontamination Rules](#5-clean-room-engineering--legacy-decontamination-rules)
   - [Legal Precedent & Principles of Clean-Room Design](#legal-precedent--principles-of-clean-room-design)
   - [The Strict Isolation Protocol for MPC-BE (GPLv3)](#the-strict-isolation-protocol-for-mpc-be-gplv3)
   - [The Strict Isolation Protocol for Light Alloy (Freeware / Closed-Source)](#the-strict-isolation-protocol-for-light-alloy-freeware--closed-source)
   - [Developer Contamination Prevention Checklist](#developer-contamination-prevention-checklist)
6. [Distribution Models & Build Topologies](#6-distribution-models--build-topologies)
   - [Topology A: The "Batteries-Included" GPLv3 Community Release (Default)](#topology-a-the-batteries-included-gplv3-community-release-default)
   - [Topology B: The "Patent-Safe / Commercial-Embeddable" LGPLv2.1+ / MIT Build](#topology-b-the-patent-safe--commercial-embeddable-lgplv21-mit-build)
   - [Topology C: The Modular "Bring-Your-Own-Engine" (BYOE) Package](#topology-c-the-modular-bring-your-own-engine-byoe-package)
7. [Compliance Checklist & Legal Artifacts](#7-compliance-checklist--legal-artifacts)
   - [Release Readiness Verification Checklist](#release-readiness-verification-checklist)
   - [Template: `NOTICE.md` / `THIRD_PARTY_LICENSES.md`](#template-noticemd--third_party_licensesmd)
   - [Source Code Availability & Relinking Architecture](#source-code-availability--relinking-architecture)
8. [Conclusion & Strategic Roadmap](#8-conclusion--strategic-roadmap)

---

## 1. Executive Summary & Legal Architecture

UniversalMediaPlayer is engineered as a modern, high-performance multimedia player for Windows 10/11, utilizing a modern managed/native host stack (C# / WinUI 3 / Windows App SDK on .NET 8/9) tightly coupled via C-ABI P/Invoke to the industry-standard media rendering engine `libmpv-2.dll`, hardware-accelerated shaders via `libplacebo`, and metadata extraction via `MediaInfoLib`.

From an intellectual property and legal perspective, multimedia player development on Windows is fraught with significant copyright, licensing, and patent entanglements:
1. **The Inevitable Viral Copyleft (GPL):** While `libmpv` is theoretically licenseable under **LGPLv2.1+**, almost every practical prebuilt binary distribution of `libmpv-2.dll` on Windows is built against an **FFmpeg** library configured with `--enable-gpl` (to enable critical video codecs such as `libx264`, `libx265`, `postproc`, and specialized deinterlacers). Consequently, bundling standard prebuilt Windows builds converts the entire distributed binary package into a **GPLv2+ or GPLv3 combined work**.
2. **Dynamic Linking Debate on Windows:** Under the legal doctrine maintained by the Free Software Foundation (FSF), dynamically linking a GPL DLL (`libmpv-2.dll`) into an application process space via standard Windows PE dynamic linking (`LoadLibraryW` / P/Invoke) creates a single combined, derivative work. Unless the entire application is distributed under terms compatible with GPLv3, distributing the installer or ZIP package violates the copyright of FFmpeg/mpv contributors.
3. **Clean-Room Imperative:** UniversalMediaPlayer draws functional, behavioral, and UI/UX inspiration from two legendary Windows media players: **MPC-BE** (Media Player Classic - Black Edition, licensed strictly under **GPLv3**) and **Light Alloy** (v4.4 open Delphi source under BSD/freeware; v4.11.2 proprietary closed-source freeware). To prevent copyright infringement and maintain complete legal ownership of the new application codebase, **no source code, decompiled fragments, or internal DirectShow filter graph structures may be copied**.

> [!IMPORTANT]
> **Core Strategic Decision:** UniversalMediaPlayer will adopt an **authorial MIT (or Apache 2.0) license for its original repository source code**, while publishing and distributing the official compiled Windows packages under the **GNU General Public License version 3 (GPLv3)**. This "permissive core, copyleft binary distribution" architecture ensures maximum legal compliance, protects user freedoms, and maintains future flexibility for standalone modular components.

---

## 2. Target License Evaluation

Selecting the primary software license for UniversalMediaPlayer requires analyzing the goals of the project: open-source community adoption, user protection against patent aggression, seamless integration with upstream open-source engines, and avoidance of legal exposure.

### Candidate Licenses Analysis

#### 1. The MIT License
- **Characteristics:** Highly permissive, short, widely understood. Grants unrestricted rights to use, copy, modify, merge, publish, distribute, sublicense, and sell copies, subject only to retaining the copyright notice.
- **Advantages:** Minimal friction for contributors; permits subcomponents (e.g., UI controls, WinUI 3 custom widgets, P/Invoke wrapper libraries) to be extracted and reused in commercial or proprietary software.
- **Disadvantages:** No explicit patent grant; no protection against proprietary forks closing the source code; incompatible with distributing a bundled GPL binary unless the binary distribution as a whole is licensed under the GPL.

#### 2. Apache License, Version 2.0 (Apache 2.0)
- **Characteristics:** Permissive license with explicit grants of patent rights from contributors and an automatic patent retaliation termination clause (Section 3).
- **Advantages:** Excellent legal defense against patent trolls and contributor patent lawsuits; robust trademark protection clauses.
- **Disadvantages:**
  - **Incompatible with GPLv2:** The patent retaliation and indemnification clauses in Apache 2.0 are considered additional restrictions under Section 6 of GPLv2. If `libmpv` or `FFmpeg` is built under GPLv2 (without the "or later" clause), combining it with Apache 2.0 code in a single distributed binary is legally prohibited.
  - **One-Way Compatible with GPLv3:** Apache 2.0 code can be merged into a GPLv3 binary, but the resulting distribution must be governed by GPLv3.

#### 3. GNU Lesser General Public License, Version 3 (LGPLv3)
- **Characteristics:** Weak copyleft. Requires that modifications to the LGPL library itself be released under LGPL, but permits proprietary or permissive client applications to dynamically link to it, provided the user can replace the LGPL library with a modified version (relinquishing anti-reverse engineering restrictions).
- **Advantages:** Well-suited for libraries intended to be consumed by closed-source software.
- **Disadvantages:** UniversalMediaPlayer is an **end-user application**, not a reusable shared library. Licensing an application under LGPLv3 offers no practical benefit over GPLv3, especially when upstream dependencies (`libmpv` Windows builds) are already GPL.

#### 4. GNU General Public License, Version 3 (GPLv3)
- **Characteristics:** Strong copyleft license. Guarantees that all modified and combined versions remain free and open source. Includes explicit patent defense (Section 11) and anti-tivoization clauses (Section 6, requiring delivery of installation information for consumer devices).
- **Advantages:**
  - **Flawless Compatibility with Upstream:** Directly aligns with `libmpv`, `FFmpeg` (`--enable-gpl --enable-version3`), `libx264`, and `MPC-BE` references.
  - **Ecosystem Protection:** Prevents dishonest commercial entities from taking UniversalMediaPlayer, rebranding it, closing the source, and bundling adware/malware (a rampant issue in the Windows media player space, as suffered by VLC and MPC-HC).
- **Disadvantages:** Prevents closed-source commercial distribution of the combined binary; requires full source code disclosure upon distribution.

---

### Comparative Trade-off Matrix

| Evaluation Criteria | MIT | Apache 2.0 | LGPLv3 | GPLv3 (Recommended Distribution) |
| :--- | :--- | :--- | :--- | :--- |
| **Copyleft Strength** | None (Permissive) | None (Permissive) | Weak (Library only) | Strong (Full Application) |
| **Explicit Patent Grant** | No | Yes (Sec. 3) | Yes (Sec. 11 via GPLv3) | Yes (Sec. 11) |
| **Compatibility with GPLv2-only** | Yes | **No (Incompatible)** | No | No (Requires GPLv2+) |
| **Compatibility with GPLv3** | Yes | Yes (Subsumed) | Yes | **Native / Direct** |
| **Compatibility with WinUI 3 / .NET** | Native (Identical) | Yes | Yes | Yes |
| **Protection Against Closed Forks** | None | None | Partial | **Absolute** |
| **Allows Commercial Proprietary Fork** | Yes | Yes | Yes (via dynamic link) | No |
| **Source Disclosure for Client App** | None | None | Relinking object code | **Full Source Code** |

---

### The "Permissive Core vs. Copyleft Distribution" Stance

UniversalMediaPlayer resolves the tension between permissive library flexibility and copyleft runtime reality by decoupling the **Source Code Repository License** from the **Compiled Binary Release License**.

```
+-----------------------------------------------------------------------------+
|               UNIVERSALMEDIAPLAYER SOURCE REPOSITORY (MIT LICENSE)           |
|                                                                             |
|   +--------------------------+       +----------------------------------+   |
|   |  WinUI 3 UI Shell        |       |  LibMpv.Client (C# P/Invoke)     |   |
|   |  - Audio/Video Controls  |       |  - C-ABI Bindings                |   |
|   |  - Playlist & OSD UI     |       |  - Event Dispatcher Loop         |   |
|   |  - DirectWrite Subtitles |       |  - mpv_render_context Wrappers   |   |
|   +--------------------------+       +----------------------------------+   |
+-----------------------------------------------------------------------------+
                                       |
                   Linked & Bundled at Compile/Build Time
                                       v
+-----------------------------------------------------------------------------+
|          DISTRIBUTED INSTALLER / PORTABLE BINARY PACKAGE (GPLv3)            |
|                                                                             |
|   [ UniversalMediaPlayer.exe (Managed .NET 8/9 / WinUI 3 Host) ]            |
|                                |                                            |
|                                +--- Dynamic Link (LoadLibraryW / PInvoke)  |
|                                v                                            |
|   [ libmpv-2.dll (GPLv3 Build) ]                                            |
|        |-- libffmpeg.dll / avcodec / avformat (Configured: --enable-gpl)    |
|        |-- libass.dll (ISC) -> FreeType (FTL) + HarfBuzz (MIT)             |
|        +-- libplacebo.dll (LGPLv2.1+)                                       |
|                                                                             |
|   * LEGAL EFFECT: The combined executable package is governed by GPLv3.     *
+-----------------------------------------------------------------------------+
```

1. **Source Code Level (MIT License):**  
   The original application source code authored by the UniversalMediaPlayer team (including UI layers, view models, P/Invoke wrappers, and custom controls) is licensed under the **MIT License**. This ensures that independent modules (such as `UniversalMediaPlayer.MpvWrapper` or custom WinUI controls) can be published to NuGet as permissive, unencumbered libraries.
2. **Distribution / Package Level (GNU GPLv3):**  
   When the project compiles, packages, and distributes the ready-to-run Windows installer (`.msix` / `.exe` / `.zip`) bundled with standard high-performance builds of `libmpv-2.dll` (incorporating FFmpeg GPL components), the **entire combined binary package is released under the GNU General Public License Version 3 (GPLv3)**.

### Recommended Licensing Strategy

> [!TIP]
> **Formal Recommendation:**  
> - Apply the **MIT License** to all original source files in the git repository:  
>   `SPDX-License-Identifier: MIT`
> - Ship the compiled distribution packages under the **GNU General Public License v3.0 or later**:  
>   `SPDX-License-Identifier: GPL-3.0-or-later`
> - Provide clear documentation in the root `README.md`, `LICENSE`, and `NOTICE.md` explaining this dual-tier structure to end-users and downstream developers.

---

## 3. Comprehensive Dependency Audit

UniversalMediaPlayer relies on a specialized ecosystem of multimedia, rendering, and UI libraries. Every dependency has been audited for licensing status, Windows ABI integration points, and copyright obligations.

```
UniversalMediaPlayer (Application Host)
 ├── Windows App SDK / WinUI 3 [MIT]
 ├── .NET 8/9 Runtime & BCL [MIT]
 ├── MediaInfoLib [BSD-2-Clause]
 └── libmpv-2.dll [GPLv2+ / GPLv3 Build]
      ├── FFmpeg [LGPLv2.1+ -> Upgraded to GPLv2+/GPLv3 via --enable-gpl]
      │    ├── libx264 [GPLv2+]
      │    ├── libx265 [GPLv2+]
      │    ├── libcdio [GPLv3]
      │    ├── dav1d [BSD-2-Clause]
      │    └── libopus / libvpx / libvorbis [BSD-3-Clause]
      ├── libass [ISC]
      │    ├── HarfBuzz [MIT / Old HarfBuzz / SIL OFL]
      │    ├── FreeType [FTL / GPLv2 Dual License -> FTL Selected]
      │    └── FriBidi [LGPLv2.1+]
      └── libplacebo [LGPLv2.1+]
           ├── Vulkan / Direct3D 11 Headers [Apache 2.0 / MIT / System SDK]
           └── LCMS2 (Little CMS) [MIT]
```

---

### `libmpv` (`libmpv-2.dll`)

- **Author / Upstream:** mpv project team ([github.com/mpv-player/mpv](https://github.com/mpv-player/mpv))
- **Baseline Source License:** **LGPLv2.1 or later (LGPLv2.1+)**
- **Effective Distributed License:** **GPLv2+ or GPLv3**
- **The GPL Transformation Mechanism:**  
  `libmpv` contains core code licensed under LGPLv2.1+. However, its build configuration script (`meson.build`) provides the option `-Dgpl=true` (or legacy `./waf configure --enable-gpl`). When enabled:
  - mpv includes GPL-only internal modules (e.g., DVD navigation via `libdvdread`/`libdvdnav`, certain video filters).
  - mpv links directly against an FFmpeg build that has been configured with `--enable-gpl`.
- **Prebuilt Windows Distributions:**  
  The de facto standard Windows prebuilt toolchains (e.g., Shinchiro builds, zhongfly builds, and MSYS2/MinGW packages) compile `libmpv-2.dll` with `-Dgpl=true` and FFmpeg with `--enable-gpl --enable-version3`. **Consequently, all standard prebuilt Windows `libmpv-2.dll` binaries are strictly GPLv3.**
- **C-ABI Export Surface (P/Invoke):**  
  UniversalMediaPlayer interacts with `libmpv-2.dll` purely via its public C-ABI exported functions:
  ```c
  mpv_handle *mpv_create(void);
  int mpv_initialize(mpv_handle *ctx);
  int mpv_command(mpv_handle *ctx, const char **args);
  int mpv_command_async(mpv_handle *ctx, uint64_t reply_userdata, const char **args);
  int mpv_set_option_string(mpv_handle *ctx, const char *name, const char *data);
  int mpv_set_property(mpv_handle *ctx, const char *name, mpv_format format, void *data);
  int mpv_get_property(mpv_handle *ctx, const char *name, mpv_format format, void *data);
  void mpv_observe_property(mpv_handle *mpv, uint64_t reply_userdata, const char *name, mpv_format format);
  mpv_event *mpv_wait_event(mpv_handle *ctx, double timeout);
  mpv_render_context *mpv_render_context_create(mpv_handle *ctx, mpv_render_param *params);
  void mpv_render_context_free(mpv_render_context *ctx);
  void mpv_terminate_destroy(mpv_handle *ctx);
  ```
- **Compliance Impact:** Because standard prebuilt binaries are GPLv3, distributing `libmpv-2.dll` alongside UniversalMediaPlayer invokes Section 6 of GPLv3 for the entire application bundle.

---

### `FFmpeg` (Multi-library Multimedia Framework)

- **Author / Upstream:** FFmpeg Project (`libavcodec`, `libavformat`, `libavutil`, `libswscale`, `libswresample`, `libavfilter`)
- **Baseline Source License:** **LGPLv2.1 or later (LGPLv2.1+)**
- **Upgraded Licenses:** **GPLv2+** or **GPLv3+** depending on compilation flags.
- **Detailed Build Flag Impact on Licensing:**

| Configure Switch | Resulting FFmpeg License | Included Features & Codecs Triggering License |
| :--- | :--- | :--- |
| *(Default, no flags)* | **LGPLv2.1+** | Native decoders: H.264, HEVC, VP9, AV1, AAC, MP3, Vorbis, FLAC. Hardware decoders: D3D11VA, DXVA2. |
| `--enable-version3` | **LGPLv3+** | Upgrades base LGPL terms to Version 3 (adds explicit patent clauses). |
| `--enable-gpl` | **GPLv2+** | Software encoders/filters: `libx264`, `libx265`, `libxvid`, `postproc`, `vf_decimate`, `vf_pullup`. |
| `--enable-gpl --enable-version3` | **GPLv3+** | `libcdio` (CDDA extraction), Samba/`libsmbclient`, GPL features under v3 terms. |
| `--enable-nonfree` | **Unredistributable Proprietary** | `libfdk_aac`, NVIDIA NVENC proprietary SDK headers (older). **STRICTLY PROHIBITED IN UNIVERSALMEDIAPLAYER.** |

- **Codecs Audited:**
  - `dav1d` (AV1 Decoder): Licensed under **BSD 2-Clause**. Highly permissive, safe for LGPL and MIT.
  - `libx264` (H.264 Software Encoder): Licensed under **GNU GPLv2+**. Causes any FFmpeg build enabling it to become GPL.
  - `libx265` (HEVC Software Encoder): Licensed under **GNU GPLv2+**.
  - `libvpx` (VP8/VP9): Licensed under **Revised BSD (3-Clause)**.
  - `libopus` (Opus Audio): Licensed under **Revised BSD (3-Clause)**.
- **Hardware Acceleration Headers:**  
  Windows hardware acceleration utilizes `d3d11va` and `dxva2`. The headers (`d3d11.h`, `dxva2api.h`) are provided by the Microsoft Windows SDK and fall squarely under the **System Library Exception** (GPLv3 Section 1, GPLv2 Section 3).

---

### `libass` (Advanced SubStation Alpha Rendering Engine)

- **Author / Upstream:** libass team ([github.com/libass/libass](https://github.com/libass/libass))
- **Source License:** **ISC License** (functionally identical to 2-Clause BSD and MIT):
  > *Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted...*
- **Underlying Dependency Licenses:**
  - **HarfBuzz:** MIT / Old HarfBuzz / SIL Open Font License. Fully permissive.
  - **FreeType:** Dual-licensed under the **FreeType License (FTL)** or **GPLv2**. When building for UniversalMediaPlayer/libmpv, the **FTL** must be explicitly selected. The FTL is a BSD-style permissive license requiring only a brief attribution statement in documentation.
  - **FriBidi:** Licensed under **GNU LGPLv2.1+**. Permits dynamic linking without viral contamination.
- **Compliance Impact:** Extremely clean and permissive. Fully compatible with MIT, LGPL, and GPLv3.

---

### `MediaInfo` / `MediaInfoLib`

- **Author / Upstream:** Jérôme Martinez / MediaArea.net SARL
- **Source License:** **BSD 2-Clause License ("Simplified BSD" or "FreeBSD License")**
- **Usage in UniversalMediaPlayer:**  
  Used for deep container introspection, stream track extraction, bit depth analysis, HDR colorimetry (`SMPTE ST 2086`, `MasteringDisplayColorVolume`), and audio channel allocation parsing.
- **Integration Mechanism:**  
  Invoked dynamically via `MediaInfo.dll` using C/C++ exported entry points wrapped in C# P/Invoke:
  ```csharp
  [DllImport("MediaInfo.dll", EntryPoint = "MediaInfo_New", CallingConvention = CallingConvention.Cdecl)]
  internal static extern IntPtr New();
  
  [DllImport("MediaInfo.dll", EntryPoint = "MediaInfo_Open", CallingConvention = CallingConvention.Cdecl)]
  internal static extern UIntPtr Open(IntPtr handle, [MarshalAs(UnmanagedType.LPWStr)] string fileName);
  
  [DllImport("MediaInfo.dll", EntryPoint = "MediaInfo_Inform", CallingConvention = CallingConvention.Cdecl)]
  internal static extern IntPtr Inform(IntPtr handle, UIntPtr reserved);
  ```
- **Compliance Impact:** Fully permissive. Requires only the preservation of the 2-clause copyright notice in `NOTICE.md`.

---

### `libplacebo` (Next-Generation GPU Video Processing)

- **Author / Upstream:** Niklas Haas / VideoLAN / mpv contributors
- **Source License:** **LGPLv2.1 or later (LGPLv2.1+)**
- **Usage:** High-quality GPU image scaling, debanding, color management (ICC profiles, 3D LUTs), tone-mapping (BT.2390, Reinhard, Mobius), and custom GLSL/Vulkan/Direct3D 11 shader execution.
- **Compliance Impact:** Compatible with both MIT applications (via dynamic linking) and GPLv3 combined builds. When bundled within `libmpv-2.dll`, its LGPL status is subsumed by the overarching GPLv3 distribution.

---

### `WinUI 3` / `Windows App SDK`

- **Author / Upstream:** Microsoft Corporation
- **Source License:** **MIT License**
- **Ecosystem:** Provides modern Fluent Design controls, XAML rendering pipeline, swapchain hosting (`SwapChainPanel`), Composition API, and Windows system integration.
- **Compliance Impact:** Native MIT license. Imposes zero copyleft restrictions. Fully compatible with GPLv3 binaries.

---

### `.NET 8/9` Runtime and Base Class Libraries (BCL)

- **Author / Upstream:** .NET Foundation / Microsoft Corporation
- **Source License:** **MIT License** (`dotnet/runtime`)
- **Redistribution:** Distributed as self-contained executable packages or framework-dependent deployments.
- **Compliance Impact:** Fully permissive. Microsoft's MIT license explicitly permits distribution of compiled IL and native AOT binaries under any chosen application license, including GPLv3.

---

### `MPC-BE` (Media Player Classic - Black Edition)

- **Author / Upstream:** MPC-BE Team (Aleksoid1978, v0lt, underground78, et al.)
- **Source License:** **GNU General Public License, Version 3 (GPLv3)**
- **Role in UniversalMediaPlayer Research:**  
  MPC-BE represents the state-of-the-art in classic Windows DirectShow playback architecture, audio channel mapping, subtitle track switching, and low-latency timeline seeking.
- **LEGAL BOUNDARY & RESTRICTIONS:**
  - **No Linking:** MPC-BE is an executable and collection of DirectShow filters (`MpcAudioRenderer.ax`, `MPCVideoDec.ax`). It cannot be dynamically linked or consumed as an SDK library.
  - **NO CODE COPYING:** **Under no circumstances may source code, C++ classes, or shader routines from MPC-BE be copied into UniversalMediaPlayer.**
  - If any C++ code from MPC-BE were copied into UniversalMediaPlayer, the entire UniversalMediaPlayer source repository would be legally forced under GPLv3 immediately, extinguishing the project's ability to maintain a permissive MIT authorial core.

---

### `Light Alloy 4.4` (Delphi Source) & `Light Alloy 4.11.2` (Proprietary Freeware)

- **Historical Background:**  
  Light Alloy was originally authored by Ilya Soft (v1.x–v4.4), written in Borland Delphi 7/2007. Version 4.4 was released under an open-source/freeware license based on modified BSD. In 2010, project development was assumed by Vortex Group (Maxim Hakimov), who evolved the player through version 4.11.2 as a proprietary, closed-source freeware product.
- **Features Under Research:**  
  Light Alloy features an extraordinary timeline preview thumbnail cache, rich playlist grouping, granular mouse wheel scrubbing, and sophisticated OSD architectures.
- **LEGAL BOUNDARY & RESTRICTIONS:**
  - **Light Alloy 4.11.2 is CLOSED-SOURCE PROPRIETARY SOFTWARE.** Disassembling, decompiling, or reverse-engineering binary routines (`LightAlloy.exe`, `LAEngine.dll`) is strictly prohibited under copyright law and constitutes trade secret / copyright violation.
  - **Light Alloy 4.4 Delphi source code MUST NOT BE COPIED.** Delphi Object Pascal code is structurally incompatible with modern C#/.NET 8+ and WinUI 3. Verbatim translation or line-by-line porting constitutes creation of an unauthorized derivative work.
  - **Clean-Room Requirement:** UniversalMediaPlayer may replicate the *observable functional behavior* and *UI paradigms* of Light Alloy, but all implementation code must be written completely from scratch (Clean-Room Implementation).

---

## 4. License Compatibility Matrix & Windows Linking Semantics

Navigating the combination of permissive, weak copyleft, and strong copyleft licenses requires a deep understanding of software copyright law, license compatibility, and Windows binary linking mechanics.

### Full License Compatibility Matrix

The following matrix defines the legal outcome when combining UniversalMediaPlayer components under various licensing scenarios:

| Component A (Host / Wrapper) | Component B (Library / Engine) | Linking Mechanism | Resulting Work License | Permissibility & Constraints |
| :--- | :--- | :--- | :--- | :--- |
| **MIT** | **BSD 2-Clause** (`MediaInfoLib`) | Dynamic / PInvoke | **MIT** | **Permitted.** BSD requires retaining copyright notice. |
| **MIT** | **ISC** (`libass`) | Dynamic / C-ABI | **MIT** | **Permitted.** ISC notice retained in documentation. |
| **MIT** | **LGPLv2.1+** (`libmpv` clean) | Dynamic (`LoadLibraryW`) | **MIT** (Host) / **LGPL** (DLL) | **Permitted.** User must be able to replace `libmpv-2.dll`. |
| **MIT** | **LGPLv3** (`libmpv` clean v3) | Dynamic (`LoadLibraryW`) | **MIT** (Host) / **LGPLv3** (DLL)| **Permitted.** User must be able to replace DLL + relink. |
| **MIT** | **GPLv2** (`FFmpeg` w/ x264) | Dynamic / In-Process | **GPLv2** (Whole Work) | **Permitted only if binary is distributed as GPLv2.** |
| **MIT** | **GPLv3** (`libmpv` default) | Dynamic / In-Process | **GPLv3** (Whole Work) | **Permitted only if binary is distributed as GPLv3.** |
| **Apache 2.0** | **GPLv2-only** | Any Linking | **ILLEGAL CONFLICT** | **STRICTLY FORBIDDEN.** Patent clause violates GPLv2 Sec 6. |
| **Apache 2.0** | **GPLv3** | Dynamic / In-Process | **GPLv3** (Whole Work) | **Permitted.** Apache 2.0 is compatible with GPLv3. |
| **GPLv3** | **MPC-BE Code** (GPLv3) | Source Copy / Merge | **GPLv3** | Permitted legally, but **Violates Project MIT Architecture**. |
| **MIT** | **Light Alloy 4.11.2** | Binary / Code Copy | **ILLEGAL INFRINGEMENT**| **STRICTLY FORBIDDEN.** Proprietary copyright infringement. |

---

### Windows PE/COFF Dynamic Linking vs. Static Linking

The Portable Executable (PE/COFF) format on Windows operates differently from ELF shared objects on Linux:
- **Static Linking:** Combines object files (`.obj`, `.lib`) into a single executable binary (`.exe`). Function addresses are resolved at link time. Under all legal jurisdictions, static linking creates an indisputable monolithic derivative work.
- **Dynamic Linking:** The host `.exe` contains an Import Table referencing exported symbols from a Dynamic Link Library (`.dll`), resolved at runtime via the Windows PE loader or programmatic invocation (`LoadLibraryW`, `GetProcAddress`).
- **Data Flow Across Boundaries:** The legal status of dynamic linking does not depend merely on the technical invocation mechanism, but on the **intimacy of the programmatic interface**:
  - If two programs communicate over standard OS IPC (pipes, sockets, command-line arguments), they are independent programs.
  - If a program loads a DLL into its own virtual address space, calls functions directly, passes complex internal memory pointers (e.g., `mpv_handle*`, `mpv_render_context*`), and relies on the DLL to execute its primary purpose, it forms a single combined work.

---

### The FSF Stance on Address Space Dynamic Linking

The Free Software Foundation (copyright holder of the GPL) maintains an unambiguous legal position regarding dynamic linking:

> *"Linking a GPL covered work statically or dynamically with other modules is making a combined work based on the GPL covered work. Thus, the terms and conditions of the GNU General Public License cover the whole combination."*  
> — **FSF GPL FAQ (Linking with GPL libraries)**

1. **Shared Address Space:** When `UniversalMediaPlayer.exe` loads `libmpv-2.dll`, both modules execute within the same 64-bit virtual memory space, sharing memory heaps, CPU thread pools, and DirectX/Vulkan GPU device contexts.
2. **Intimate Data Exchange:** UniversalMediaPlayer passes native pointers (`IntPtr`), allocates and deallocates native structures (`mpv_event`, `mpv_render_param`), and registers C-style callback function pointers across the boundary.
3. **Functional Dependency:** UniversalMediaPlayer cannot fulfill its essential function as a media player without the media engine loaded into memory.
4. **Legal Conclusion:** While some commercial vendors dispute the FSF's interpretation in court, the only legally safe, defensible posture for UniversalMediaPlayer is to **treat dynamic linking with a GPL `libmpv-2.dll` as legally activating the GPL for the distributed binary package.**

---

### P/Invoke and C-ABI Marshaling Boundaries

In .NET 8/9, interop with native DLLs occurs through Platform Invoke (P/Invoke) or modern C# 9+ Function Pointers (`delegate* unmanaged[Cdecl]<...>`).

```csharp
// Example: Safe and compliant P/Invoke boundary in UniversalMediaPlayer
internal static unsafe class MpvNativeMethods
{
    private const string MpvDllName = "libmpv-2.dll";

    [DllImport(MpvDllName, EntryPoint = "mpv_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr MpvCreate();

    [DllImport(MpvDllName, EntryPoint = "mpv_initialize", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int MpvInitialize(IntPtr handle);

    [DllImport(MpvDllName, EntryPoint = "mpv_set_option_string", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int MpvSetOptionString(
        IntPtr handle, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(MpvDllName, EntryPoint = "mpv_terminate_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void MpvTerminateDestroy(IntPtr handle);
}
```

- **Clean ABI Isolation:** P/Invoke relies solely on primitive types (`IntPtr`, integers, UTF-8 byte pointers). It does not require including GPL C/C++ header files (`mpv/client.h`) in the compilation of the C# assembly.
- **Repository Cleanliness:** The C# source files do not incorporate any GPL code; they merely describe the standard C ABI interface. Therefore, the C# source repository remains validly licenseable under the **MIT License**.

---

### The Windows System Library Exception

UniversalMediaPlayer relies on core Windows operating system components, including:
- DirectX 11 / DXGI (`d3d11.dll`, `dxgi.dll`)
- DirectWrite / Direct2D (`dwrite.dll`, `d2d1.dll`)
- Windows Media Foundation (`mfplat.dll`, `mfreadwrite.dll`)
- Windows C Runtime (`ucrtbase.dll`, `msvcp140.dll`)

Both GPLv2 (Section 3) and GPLv3 (Section 1) contain the **System Library Exception**:
> *"The 'System Libraries' of an executable work include anything, other than the work as a whole, that (a) is included in the normal form of packaging a Major Component, but which is not part of that Major Component, and (b) serves only to enable use of the work with that Major Component, or to implement a Standard Interface for which an implementation is available to the public in source code form."*

UniversalMediaPlayer is fully compliant: developers and distributors are **not required** to provide the source code of Windows system DLLs or DirectX runtime components when distributing GPLv3 binaries.

---

### Source Code Disclosure Obligations: GPLv3 vs. LGPLv2.1

The following table summarizes the legal disclosure obligations UniversalMediaPlayer must fulfill upon distributing binaries:

```
+-----------------------------------------------------------------------------+
|                     DISTRIBUTION OBLIGATION COMPARISON                      |
+-----------------------------------------------------------------------------+
| Requirement                   | LGPLv2.1 Build Scenario  | GPLv3 Build Scenario     |
+-------------------------------+--------------------------+--------------------------+
| Disclose UI / C# Source Code  | NO                       | YES                      |
| Disclose Engine Modifications | YES (libmpv changes)     | YES (libmpv + all libs)  |
| Provide Build Scripts         | YES (for LGPL libs)      | YES (Complete build tree)|
| User Replacement Right        | YES (Must swap DLL)      | YES (Full rebuild)       |
| Provide Notice File           | YES                      | YES                      |
| Anti-Tivoization Disclosure   | NO                       | YES (Installation info)  |
| Patent Retaliation Defense    | Weak                     | STRONG (Section 11)      |
+-----------------------------------------------------------------------------+
```

1. **If Distributing under LGPLv2.1+:**
   - UniversalMediaPlayer would only have to publish source code for modifications made to `libmpv-2.dll` or `libplacebo.dll`.
   - The C# / WinUI 3 application source code could remain proprietary or closed.
   - The application must allow the user to replace `libmpv-2.dll` with an arbitrary binary without breaking the application (trivially satisfied by Windows DLL dynamic search order).
2. **If Distributing under GPLv3 (UniversalMediaPlayer Default):**
   - UniversalMediaPlayer must make the **Complete Corresponding Source Code** of the entire application (all C# source files, XAML files, build scripts, packaging manifests) available to every recipient of the binary.
   - All build scripts, compiler configurations, and patches used to generate `libmpv-2.dll` and `ffmpeg.dll` must be made available.

---

## 5. Clean-Room Engineering & Legacy Decontamination Rules

UniversalMediaPlayer aims to incorporate the ergonomics, responsiveness, and media mastery of legendary Windows media players, specifically **MPC-BE** and **Light Alloy**. To accomplish this without legal vulnerability, the engineering team must operate under strict **Clean-Room Design** principles.

```
       LEGACY SYSTEMS (CONTAMINATED)                   CLEAN-ROOM SANITIZATION                   UNIVERSALMEDIAPLAYER (CLEAN)
+------------------------------------------+                                      +------------------------------------------+
| MPC-BE (GPLv3 C++ Source)                |                                      | UniversalMediaPlayer (MIT / GPLv3)       |
| - DirectShow Filters & Pin Connections   |  ===> Functional Specification ===>  | - WinUI 3 XAML / C# MVVM Architecture    |
| - Custom MFC Window Handles              |       (Written by System Analyst)    | - libmpv C-ABI Event Loop                |
| - Legacy C++ Direct3D9 Renderers         |                                      | - DirectWrite / Composition SwapChain    |
+------------------------------------------+                                      +------------------------------------------+
| Light Alloy 4.11.2 (Proprietary Freeware)|                                      | Zero Copied Code                         |
| - Closed-source Delphi Binaries          |  ===> Observable UI Behavior ===>    | Zero Translated Routines                 |
| - Disassembled ASM Routines (FORBIDDEN)  |       (Layout, Hotkeys, Timings)     | Modern Clean Implementation              |
+------------------------------------------+                                      +------------------------------------------+
```

### Legal Precedent & Principles of Clean-Room Design

Clean-room design (pioneered during the IBM PC BIOS reverse engineering by Phoenix Technologies in 1984) establishes that:
1. **Copyright protects expression, not ideas:** Under 17 U.S.C. § 102(b), copyright protection extends only to original works of authorship (source code, syntax, exact phrasing), and does **not** extend to any idea, procedure, process, system, method of operation, or concept.
2. **Observable Behavior is Non-Copyrightable:** A software engineer may observe a program’s input-output behavior, hotkey conventions, menu hierarchies, timeline tooltip formatting, and playlist grouping schemas, and independently write new code in a different programming language to achieve identical behavior.
3. **Decompilation Dangers:** Looking at disassembled or decompiled source code creates a direct cognitive contamination vector. Courts apply the "Substantial Similarity" and "Abstractions, Filtration, Comparison" tests. If infringing code exhibits similar idiosyncratic variable names, comments, or non-functional structural patterns, liability is established.

---

### The Strict Isolation Protocol for MPC-BE (GPLv3)

MPC-BE is a phenomenal DirectShow player written in C++ using Microsoft Foundation Classes (MFC). Its codebase is strictly **GPLv3**.

> [!CAUTION]
> **MANDATORY RULES FOR MPC-BE:**
> 1. **DO NOT COPY C++ CODE:** Never copy classes, functions, or algorithms from MPC-BE into UniversalMediaPlayer.
> 2. **NO TRANSLATION:** Do not use automated tools or LLM prompts to "translate MPC-BE C++ into C#". Translated code is legally a derivative work.
> 3. **NO PIN-ENGINE INTERFACES:** MPC-BE is built entirely on DirectShow filter graphs (`IGraphBuilder`, `IBaseFilter`, `IPin`). UniversalMediaPlayer is built on `libmpv`. DirectShow graph connection code is technically obsolete and legally toxic.
> 4. **PERMITTED REFERENCE:** Developers may inspect MPC-BE's user interface, hotkey defaults (e.g., `Space` = Pause, `Ctrl+Left/Right` = Jump, `ScrollWheel` = Volume), and feature lists to specify requirements.

---

### The Strict Isolation Protocol for Light Alloy (Freeware / Closed-Source)

Light Alloy has a dual history: open-source Delphi (v4.4) and closed-source proprietary freeware (v4.11.2).

> [!CAUTION]
> **MANDATORY RULES FOR LIGHT ALLOY:**
> 1. **DISASSEMBLY IS STRICTLY FORBIDDEN:** No developer may open `LightAlloy.exe`, `LAEngine.dll`, or associated plugins in IDA Pro, Ghidra, x64dbg, or any disassembler.
> 2. **NO DELPHI CODE COPYING:** Do not copy or translate Borland Delphi Pascal units (`.pas`, `.dfm`) from the legacy v4.4 source tree.
> 3. **PERMITTED STUDY (BLACK-BOX ONLY):** Developers may run Light Alloy 4.11.2 in a virtual machine or standard Windows desktop, interact with its UI, record its behaviors (e.g., how timeline thumbnail previews scale, how subtitle delays are adjusted), and document these behaviors in neutral specifications.

---

### Developer Contamination Prevention Checklist

Before writing any core playback or UI logic for UniversalMediaPlayer, every contributing engineer must verify compliance with the following protocol:

- [x] **Source Verification:** Confirm that all code written is original work, authored directly in C#, XAML, or C++ specifically for UniversalMediaPlayer.
- [x] **Zero Pasted Code:** Verify that no snippets have been pasted from MPC-HC, MPC-BE, VLC, Light Alloy, or PotPlayer.
- [x] **Interface Independence:** Verify that all playback control flows through the clean `libmpv` C-ABI rather than proprietary DirectShow or Media Foundation custom filter topologies.
- [x] **Clean Specifications:** Ensure that feature implementation tickets in the project tracker describe *functional requirements* (e.g., "Display 160x90 thumbnail preview at mouse hover position along the seekbar") rather than code references.
- [x] **Audit Trail:** Maintain clear git commit histories showing the step-by-step authoring of all components.

---

## 6. Distribution Models & Build Topologies

To accommodate different deployment environments (Windows Store, portable ZIP, enterprise deployment) while strictly adhering to licensing laws, UniversalMediaPlayer supports three defined build and distribution topologies.

```
                            UNIVERSALMEDIAPLAYER DEPLOYMENT TOPOLOGIES
                                                │
         ┌──────────────────────────────────────┼──────────────────────────────────────┐
         ▼                                      ▼                                      ▼
+──────────────────────────+   +──────────────────────────+   +──────────────────────────+
|       TOPOLOGY A         |   |       TOPOLOGY B         |   |       TOPOLOGY C         |
|   "Batteries-Included"   |   |     "Patent-Safe /       |   |    "Modular / BYOE"      |
|     Community Release    |   |  Commercial-Embeddable"  |   | (Bring-Your-Own-Engine)  |
+──────────────────────────+   +──────────────────────────+   +──────────────────────────+
| • License: GPLv3         |   | • License: MIT / LGPLv2+ |   | • License: Pure MIT      |
| • libmpv: GPLv3 Build    |   | • libmpv: LGPL Build     |   | • libmpv: NOT BUNDLED    |
| • Full Codec Support     |   | • Patent-Free / HW Only  |   | • User supplies DLL      |
| • GitHub / Winget / AppX |   | • Store / Commercial SDK |   | • NuGet Core Packages    |
+──────────────────────────+   +──────────────────────────+   +──────────────────────────+
```

---

### Topology A: The "Batteries-Included" GPLv3 Community Release (Default)

This is the primary consumer distribution of UniversalMediaPlayer, providing maximum format compatibility out of the box.

- **License of Distributed Package:** **GNU General Public License, Version 3 (GPLv3)**
- **Bundled Engine:** `libmpv-2.dll` compiled with `-Dgpl=true` and FFmpeg with `--enable-gpl --enable-version3`.
- **Supported Formats:** Complete coverage (H.264, HEVC, AV1, VP9, MPEG-2, VC-1, ProRes, Bink, AAC, AC3, E-AC3, TrueHD, DTS-HD MA, FLAC, ASS/SSA subtitles).
- **Distribution Channels:** GitHub Releases (MSIX, Inno Setup Installer, Portable ZIP), Winget, Chocolatey.
- **Compliance Obligations:**
  - Full source code of UniversalMediaPlayer must be publicly hosted and linked in the application "About" dialog.
  - Complete build scripts and exact FFmpeg/mpv git commit hashes must be documented.
  - A copy of `LICENSE` (GPLv3) and `NOTICE.md` must be included in the installation directory.

---

### Topology B: The "Patent-Safe / Commercial-Embeddable" LGPLv2.1+ / MIT Build

A specialized build topology designed for environments where GPL contamination or software patent liabilities (e.g., MPEG-LA / HEVC Advance licensing fees) are strictly unacceptable.

- **License of Distributed Package:** **MIT (Application Shell) + LGPLv2.1+ (Engine DLL)**
- **Bundled Engine:** `libmpv-2.dll` custom-built with:
  ```bash
  meson setup build \
    -Dgpl=false \
    -Dcplayer=false \
    -Dbuild-date=false \
    -Dlua=disabled \
    -Djavascript=disabled \
    -Dlibarchive=disabled \
    --default-library=shared
  ```
  And FFmpeg configured without `--enable-gpl`:
  ```bash
  ./configure \
    --disable-gpl \
    --enable-shared \
    --disable-static \
    --enable-version3 \
    --enable-libdav1d \
    --enable-libopus \
    --enable-libvpx \
    --disable-encoder=libx264 \
    --disable-encoder=libx265
  ```
- **Codec Profile:** Relies strictly on royalty-free open codecs (AV1 via `dav1d`, VP9, Opus, FLAC, Vorbis) and hardware decoders via Windows Media Foundation / D3D11VA (where patent royalties are pre-licensed by Microsoft or the GPU vendor).
- **Compliance Obligations:**
  - Application source code can remain proprietary or MIT.
  - Modified source code of `libmpv` and `FFmpeg` must be provided upon request.
  - User relinking right must be supported: the application must dynamically load `libmpv-2.dll` from the application directory, allowing end-users to swap in an updated DLL.

---

### Topology C: The Modular "Bring-Your-Own-Engine" (BYOE) Package

A developer-centric architecture separating the UniversalMediaPlayer UI shell entirely from the media engine binaries.

- **License of Distributed Package:** **Pure MIT License**
- **Bundled Engine:** **NONE.** The application distribution contains only the managed .NET binaries, WinUI 3 controls, and the `LibMpv.Client` C# wrapper.
- **Engine Resolution Workflow:**  
  On initial launch, UniversalMediaPlayer executes an engine resolution strategy:
  1. Check application directory for `libmpv-2.dll`.
  2. Check `%LOCALAPPDATA%\UniversalMediaPlayer\engine\libmpv-2.dll`.
  3. Check system PATH or user-configured engine path in settings.
  4. If missing, display a clean first-run dialog offering to download the latest authenticated community build of `libmpv-2.dll` directly from trusted upstream repositories.
- **Legal Advantage:** UniversalMediaPlayer never distributes the GPL binary. The end-user downloads and combines the components on their local machine. This cleanly circumvents distribution-triggered copyleft obligations, maintaining an immaculate MIT distribution status.

---

## 7. Compliance Checklist & Legal Artifacts

To guarantee 100% legal compliance for all releases, UniversalMediaPlayer must implement standard operational checklists and shipping artifacts.

### Release Readiness Verification Checklist

Every continuous deployment (CI/CD) release pipeline must enforce the following gates before publishing an official release:

```
[ ] GATE 1: Dependency License Scan
    [x] Verify all NuGet dependencies are licensed under MIT, Apache 2.0, or BSD.
    [x] Confirm zero GPL-only NuGet packages are linked into the managed project.
    [x] Scan repository with dotnet-project-licenses / FOSSA.

[ ] GATE 2: Engine Binary Verification
    [x] Inspect libmpv-2.dll build configuration via strings/dumpbin:
        Command: strings libmpv-2.dll | findstr /i "configuration:"
    [x] Identify whether build is LGPLv2.1+ or GPLv3.
    [x] If build contains --enable-gpl, enforce GPLv3 packaging manifest.

[ ] GATE 3: Legal Notice Verification
    [x] Confirm NOTICE.md is present in the installer root.
    [x] Confirm LICENSE.txt contains full verbatim GNU GPLv3 text.
    [x] Confirm "About" dialog displays clickable links to source repositories and licenses.

[ ] GATE 4: Clean-Room & Attribution Audit
    [x] Confirm no source files contain copied code from MPC-BE or Light Alloy.
    [x] Confirm all third-party copyright headers are preserved intact.

[ ] GATE 5: Source Code Availability Fulfillment
    [x] Confirm corresponding git tag is pushed to GitHub with full repository source code.
    [x] Archive exact libmpv / FFmpeg build source archive corresponding to bundled DLL.
```

---

### Template: `NOTICE.md` / `THIRD_PARTY_LICENSES.md`

The following text represents the official third-party attribution document that must be bundled in the root installation directory of UniversalMediaPlayer:

````markdown
# Third-Party Software Licenses and Notices

UniversalMediaPlayer incorporates open-source software libraries under various licenses. 
This document lists the components, their copyright holders, and their respective license terms.

---

## 1. Primary Application License

UniversalMediaPlayer Source Code: Copyright (c) 2026 UniversalMediaPlayer Contributors.
The original source code of UniversalMediaPlayer is licensed under the MIT License.

When distributed as a precompiled binary package bundled with libmpv-2.dll (compiled with GPL features), 
the combined work is distributed under the terms of the GNU General Public License Version 3 (GPLv3).

---

## 2. Bundled Multimedia Engine Components

### mpv / libmpv (libmpv-2.dll)
- **Copyright:** (c) 2000-2026 mpv developers and contributors
- **License:** GNU General Public License v3.0 or later (GPL-3.0-or-later)
  (Base engine LGPLv2.1+, upgraded to GPLv3 via bundled FFmpeg GPL build flags)
- **Website:** https://mpv.io
- **Source Code:** https://github.com/mpv-player/mpv

### FFmpeg
- **Copyright:** (c) 2000-2026 the FFmpeg developers
- **License:** GNU General Public License v3.0 or later (GPL-3.0-or-later)
- **Components Included:** libavcodec, libavformat, libavutil, libswscale, libswresample, libavfilter
- **Website:** https://ffmpeg.org

### libass
- **Copyright:** (c) 2006-2026 libass contributors
- **License:** ISC License
- **Text:**
  Permission to use, copy, modify, and/or distribute this software for any purpose with or 
  without fee is hereby granted, provided that the above copyright notice and this permission 
  notice appear in all copies.
- **Website:** https://github.com/libass/libass

### FreeType
- **Copyright:** (c) 1996-2026 David Turner, Robert Wilhelm, and Werner Lemberg
- **License:** The FreeType Project License (FTL)
- **Website:** https://www.freetype.org

### HarfBuzz
- **Copyright:** (c) 2010-2026 Google, Inc. and HarfBuzz contributors
- **License:** MIT License
- **Website:** https://harfbuzz.github.io

### libplacebo
- **Copyright:** (c) 2017-2026 Niklas Haas and libplacebo contributors
- **License:** GNU Lesser General Public License v2.1 or later (LGPL-2.1-or-later)
- **Website:** https://github.com/haasn/libplacebo

### MediaInfoLib (MediaInfo.dll)
- **Copyright:** (c) 2002-2026 MediaArea.net SARL. All rights reserved.
- **License:** BSD 2-Clause License
- **Text:**
  Redistribution and use in source and binary forms, with or without modification, are permitted 
  provided that the following conditions are met:
  1. Redistributions of source code must retain the above copyright notice, this list of conditions 
     and the following disclaimer.
  2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions 
     and the following disclaimer in the documentation and/or other materials provided with the distribution.
- **Website:** https://mediaarea.net/en/MediaInfo

---

## 3. UI Framework and Runtime Components

### Windows App SDK / WinUI 3
- **Copyright:** (c) Microsoft Corporation. All rights reserved.
- **License:** MIT License
- **Website:** https://github.com/microsoft/WindowsAppSDK

### .NET Runtime and Base Class Libraries
- **Copyright:** (c) .NET Foundation and Contributors. All rights reserved.
- **License:** MIT License
- **Website:** https://github.com/dotnet/runtime
````

---

### Source Code Availability & Relinking Architecture

To fulfill GPLv3 Section 6 and LGPLv2.1 Section 4, UniversalMediaPlayer provides concrete mechanisms for source code availability and dynamic library replacement:

```
                                  RELINKING VERIFICATION WORKFLOW
+---------------------------------------------------------------------------------------------------+
| 1. End-user downloads a custom, modified build of libmpv-2.dll (e.g. custom VapourSynth filters). |
| 2. User navigates to UniversalMediaPlayer installation directory:                                  |
|    C:\Program Files\UniversalMediaPlayer\libmpv-2.dll                                             |
| 3. User replaces existing libmpv-2.dll with the modified binary.                                 |
| 4. UniversalMediaPlayer launches, calls LoadLibraryW("libmpv-2.dll"), and dynamically binds       |
|    all C-ABI exports without requiring recompilation of the C# application binary.                |
+---------------------------------------------------------------------------------------------------+
```

1. **Source Code Distribution Provision:**  
   In compliance with GPLv3 Section 6(b), every binary release distributed via installer or archive must include either:
   - The complete source code directly bundled in the download.
   - A prominent, written offer valid for at least three years to provide the complete machine-readable corresponding source code on a durable physical medium or public network server at no charge.
2. **Online Source Code Repository:**  
   The primary source repository is continuously maintained at the public project URL, containing all C# source files, WinUI 3 XAML assets, build scripts, packaging configurations, and patch sets.
3. **Reproducible Engine Build Scripts:**  
   UniversalMediaPlayer maintains a dedicated repository directory (`scripts/build-engine/`) containing automated PowerShell and Docker scripts that fetch, patch, and build the exact version of `libmpv-2.dll`, `FFmpeg`, `libass`, and `libplacebo` shipped with each release.

---

## 8. Conclusion & Strategic Roadmap

The licensing analysis demonstrates that UniversalMediaPlayer can achieve world-class multimedia playback on Windows while remaining in absolute legal compliance with all open-source and proprietary boundaries:

1. **Strategic License Stance:** Authoring the source code under the **MIT License** preserves modularity and unencumbered development, while releasing binary distributions under **GPLv3** fully respects the reality of modern high-performance `libmpv-2.dll` and FFmpeg builds.
2. **Strict Clean-Room Execution:** Zero tolerance for copied or translated code from MPC-BE (GPLv3) and Light Alloy (proprietary closed-source) completely protects UniversalMediaPlayer from copyright contamination, ensuring that the project owns 100% of its intellectual property.
3. **Architectural Decoupling:** Communicating with native multimedia engines purely across the standard C-ABI via P/Invoke maintains architectural clarity, enables dynamic user relinking, and simplifies ongoing updates to upstream engine releases.

---
*End of Licensing Analysis Report.*
