#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
BestStart Core Router & Dynamic Orchestrator
--------------------------------------------
Dynamic intelligent task router for the BestStart AI ecosystem.
Analyzes developer prompts, detects language/framework/platform intents,
selects optimal skills and MCP servers, generates dynamic configuration,
and produces context-injected prompts for high-precision code generation.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple, Set

# Ensure UTF-8 output on Windows consoles
if sys.platform == "win32":
    try:
        if sys.stdout and hasattr(sys.stdout, "reconfigure"):
            sys.stdout.reconfigure(encoding="utf-8")
        if sys.stderr and hasattr(sys.stderr, "reconfigure"):
            sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

try:
    import yaml  # type: ignore
except ImportError:
    yaml = None


# ============================================================================
# Domain Models & Skill / MCP Definitions
# ============================================================================

@dataclass
class SkillDefinition:
    id: str
    name: str
    description: str
    patterns: List[str]
    keywords: List[str]
    associated_mcps: List[str]
    excerpt: str


@dataclass
class MCPDefinition:
    id: str
    name: str
    description: str
    patterns: List[str]
    keywords: List[str]
    config: Dict[str, Any]
    default_enabled: bool = False


# ============================================================================
# Builtin Skill & MCP Catalogs
# ============================================================================

BUILTIN_SKILLS: Dict[str, SkillDefinition] = {
    "csharp_wpf.skill": SkillDefinition(
        id="csharp_wpf.skill",
        name="C# / WPF / .NET Enterprise Desktop Architecture",
        description="Architecture and guidelines for C#, .NET 8/9, WPF, XAML, MVVM, Dependency Injection, and desktop UI.",
        patterns=[
            r"(?<![a-zA-Z0-9_])c#(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])csharp(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])wpf(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])\.net(?:\s*(?:8|9|core|framework))?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])dotnet(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])xaml(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])mvvm(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])linq(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])nuget(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])asp\.net(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])winforms(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])entity\s*framework(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])ef\s*core(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])communitytoolkit(?:\.mvvm)?(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])си\s*шарп(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])шарп(?:е|ом|а|у)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])впф(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])дотнет(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "c#", "csharp", "wpf", ".net", "dotnet", "xaml", "mvvm", "linq",
            "nuget", "asp.net", "winforms", "entity framework", "ef core",
            "си шарп", "шарп", "впф", "дотнет", "communitytoolkit"
        ],
        associated_mcps=["context7"],
        excerpt="""# Skill: C# / WPF / .NET Standards & Best Practices
- **Framework Target**: Target modern .NET (.NET 8 or .NET 9 LTS/STS).
- **Pattern**: Strict MVVM (Model-View-ViewModel) using CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`).
- **UI & Bindings**: Use compiled bindings `x:Bind` or standard `Binding` with explicit `Mode` and `UpdateSourceTrigger=PropertyChanged`.
- **Async Execution**: Avoid UI blocking; use `async/await` and marshal back via `DispatcherQueue` or `App.Current.Dispatcher` when necessary.
- **Dependency Injection**: Utilize `Microsoft.Extensions.DependencyInjection` for IoC container and view-model lifecycle management.
- **Resource Management**: Dispose unmanaged resources and event handlers via `IDisposable` to prevent memory leaks in long-lived desktop apps.
"""
    ),

    "cpp_native.skill": SkillDefinition(
        id="cpp_native.skill",
        name="C++ Native / Systems / Concurrency / Memory Safety",
        description="Modern C++ (C++20/C++23) standards, RAII, zero-cost abstractions, multi-threading, and memory management.",
        patterns=[
            r"(?<![a-zA-Z0-9_])c\+\+(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])cpp(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])native(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])memory\s*leak(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])smart\s*pointers?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])std::",
            r"(?<![a-zA-Z0-9_])raii(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])concurrency(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])multithreading(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])boost(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])cmake(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])valgrind(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])gdb(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])msvc(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])clang(?:\+\+)?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])unique_ptr(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])shared_ptr(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])lock-free(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])си\s*плюс\s*плюс(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])плюсы(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])многопоточность(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])умные\s*указатели(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])утечки\s*памяти(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "c++", "cpp", "native", "memory leak", "smart pointer", "smart pointers",
            "std::", "raii", "concurrency", "multithreading", "boost", "cmake",
            "valgrind", "gdb", "msvc", "clang", "unique_ptr", "shared_ptr",
            "lock-free", "си плюс плюс", "плюсы", "многопоточность", "умные указатели"
        ],
        associated_mcps=["context7"],
        excerpt="""# Skill: Modern C++ Native & Memory Guidelines
- **Standard**: Prefer C++20 / C++23. Adhere to the C++ Core Guidelines.
- **Resource Management**: Strict RAII. Never use raw `new`/`delete`. Prefer `std::unique_ptr` and `std::shared_ptr`.
- **Concurrency**: Use `std::jthread`, `std::stop_token`, mutexes (`std::scoped_lock`), and lock-free primitives where appropriate.
- **Performance**: Zero-cost abstractions, move semantics (`std::move`, `std::forward`), cache locality, and `constexpr` evaluation.
- **Safety**: AddressSanitizer (ASan), ThreadSanitizer (TSan), and Valgrind verification for leak-free execution.
- **Build System**: Clean modular CMake targets (`target_include_directories`, `target_link_libraries`).
"""
    ),

    "c_system.skill": SkillDefinition(
        id="c_system.skill",
        name="C System / Win32 / POSIX / Low-Level Programming",
        description="C system programming, Win32 API, POSIX compliance, raw pointers, syscalls, and low-level drivers.",
        patterns=[
            r"(?<![a-zA-Z0-9_])win32(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])winapi(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])posix(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])raw\s*pointers?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])malloc(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])free\s*\(",
            r"(?<![a-zA-Z0-9_])syscalls?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])mmap(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])ioctl(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])pthreads?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])c99(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])c11(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])c23(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])kernel\s*driver(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])low-level\s*c(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_#+])c\s+(?:system|programming|language|code|driver|библиотек|код|систем)",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_#+])си\s+(?:систем|язык|программирован|указател|вызов)",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])чист(?:ый|ом|ого)\s+си(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])язык(?:е|а)?\s+си(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])вин32(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])позикс(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])сырые\s*указатели(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])системные\s*вызовы(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])системный\s*драйвер(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "win32", "winapi", "posix", "raw pointer", "raw pointers",
            "malloc", "syscall", "syscalls", "mmap", "ioctl", "pthread", "pthreads",
            "c99", "c11", "c23", "kernel driver", "чистый си", "язык си",
            "вин32", "позикс", "сырые указатели", "системные вызовы", "системный драйвер"
        ],
        associated_mcps=["context7"],
        excerpt="""# Skill: C System & Low-Level Programming Standards
- **Standard**: C11 or C23 compliance with strict compiler warnings (`-Wall -Wextra -Wpedantic -Werror` / `/W4 /WX`).
- **Memory Safety**: Explicit allocation tracking, buffer overflow prevention (`snprintf`, bounds checking), zero-out on free.
- **Platform APIs**: Safe encapsulation of Win32 API handles (`HANDLE`, `CloseHandle`, `GetLastError`) and POSIX file descriptors (`errno`, `mmap`, `pthreads`).
- **Error Propagation**: Standard return codes (`0` on success, negative or custom enum on failure) and structured cleanup (`goto cleanup` pattern).
- **Concurrency & I/O**: Safe atomics (`stdatomic.h`), non-blocking sockets, and platform-specific asynchronous I/O (IOCP / epoll).
"""
    ),

    "python_fastapi.skill": SkillDefinition(
        id="python_fastapi.skill",
        name="Python / FastAPI / Async Services & Resilient Scrapers",
        description="FastAPI Clean Architecture, Pydantic v2 validation, async pipelines, resilient web scraping, and anti-blocking.",
        patterns=[
            r"(?<![a-zA-Z0-9_])python(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])fastapi(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])scraper(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])scraping(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])web\s*api(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])rest\s*api(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])uvicorn(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])pydantic(?:\s*v2)?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])beautifulsoup(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])bs4(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])selenium(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])playwright(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])scrapy(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])firecrawl(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])микросервис(?:ы|а|ов)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])парсер(?:ы|а|ов|ом)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])парсинг(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])питон(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])пайтон(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])скрейпер(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])скрейпинг(?:\w*)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])веб-?апи(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "python", "fastapi", "scraper", "scraping", "web api", "rest api",
            "uvicorn", "pydantic", "beautifulsoup", "bs4", "selenium", "playwright",
            "scrapy", "firecrawl", "микросервис", "парсер", "парсинг", "питон",
            "пайтон", "скрейпер", "скрейпинг", "веб апи", "веб-апи"
        ],
        associated_mcps=["playwright", "firecrawl", "context7"],
        excerpt="""# Skill: Python & FastAPI Clean Architecture Guidelines
- **Architecture**: Layered design (`api/` routes, `core/` config, `models/` Pydantic schemas, `services/` business logic, `db/` persistence).
- **Async Execution**: `async def` route handlers, non-blocking HTTP clients (`httpx.AsyncClient`), async DB sessions (`SQLAlchemy async` / `tortoise`).
- **Validation**: Pydantic v2 schemas using `ConfigDict(from_attributes=True)` and strict field validations.
- **Scraping & Anti-Block**: Configured User-Agents, exponential backoff, rate limiting, and headless browser automation via Playwright/Firecrawl.
- **Resilience**: Lifespan context handlers, global structured exception handlers, typed error responses.
"""
    ),

    "1c_enterprise.skill": SkillDefinition(
        id="1c_enterprise.skill",
        name="1C:Enterprise / BSL / Extensions / Trade Management (УТ)",
        description="1C:Enterprise (1С:Предприятие 8.3), BSL language, configuration extensions (.cfe), documents, registers, and catalogs.",
        patterns=[
            r"(?<![a-zA-Z0-9_])1c(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])1[сС](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])1c[:\s]*enterprise(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])1[сС][:\s]*предприятие(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])ут(?:1[0-2])?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])управление\s*торговлей(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])расширени[ея](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])счетнаоплату(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])счет\s*на\s*оплату(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])счёт\s*на\s*оплату(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])документ(?:ы|а|ов)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])bsl(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])бсп(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])конфигураци[яи](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])регистр(?:\s*сведений|\s*накопления)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])справочник(?:и)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])внешн(?:яя|ие)\s*обработк[аи](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])проведени[ея]\s*документа(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"\.cfe\b",
            r"\.epf\b",
            r"\.erf\b",
        ],
        keywords=[
            "1c", "1с", "1c enterprise", "1с предприятие", "1с:предприятие", "ут",
            "управление торговлей", "расширение", "счетнаоплату", "счет на оплату",
            "счёт на оплату", "документ", "bsl", "бсп", "конфигурация 1с",
            "регистр сведений", "справочник", "внешняя обработка", "epf", "cfe"
        ],
        associated_mcps=["context7"],
        excerpt="""# Skill: 1C:Enterprise 8.3 & BSL Architecture Guidelines
- **Platform**: 1C:Enterprise 8.3 platform standards (Управляемые формы, Клиент-Серверное разделение).
- **Context Execution**: Strict separation of execution directives (`&НаКлиенте`, `&НаСервере`, `&НаСервереБезКонтекста`).
- **Extensions (.cfe)**: Non-intrusive modifications using extensions with `&Вместо`, `&Перед`, `&После` interceptors.
- **Transactions & Locking**: Safe transaction locks (`БлокировкаДанных`) inside `НачатьТранзакцию()` / `ЗафиксироватьТранзакцию()`.
- **Query Optimization**: Strict query standards: specify fields explicitly (avoid `*`), index register dimensions, use parameters.
- **Standard Library**: Utilize BSP (Библиотека Стандартных Подсистем) mechanisms for common business functions.
"""
    ),

    "osint_telemetry.skill": SkillDefinition(
        id="osint_telemetry.skill",
        name="OSINT & Telemetry / Aiogram 3 / Telegram Reconnaissance",
        description="OSINT reconnaissance, Aiogram 3.x Telegram bot frameworks, GHunt footprinting, and structured telemetry ingestion.",
        patterns=[
            r"(?<![a-zA-Z0-9_])osint(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])ghunt(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])aiogram(?:\s*3)?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])telegram(?:\s*bot)?(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])телеграм(?:\s*бот)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])telemetry(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])телеметри[яи](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])intelligence(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])investigation(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])(?:тг|telegram|телеграм)[\s_-]*бот(?:а|ом|ы|ов)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])recon(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])socmint(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])shodan(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])whois(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])metadata(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])geoint(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])разведк[аи](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])деанон(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])сбор\s*данных(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "osint", "ghunt", "aiogram", "telegram", "телеграм", "telemetry",
            "телеметрия", "intelligence", "investigation", "recon", "socmint",
            "shodan", "whois", "metadata", "geoint", "разведка", "деанон"
        ],
        associated_mcps=["context7"],
        excerpt="""# Skill: OSINT, Aiogram 3 & Telemetry Pipeline Guidelines
- **Bot Framework**: Use modern Aiogram 3.x with structured `Router`, `Dispatcher`, and middleware pipelines.
- **Reconnaissance & OSINT**: Safe and rate-limited modular tools (GHunt, Whois, DNS lookup, public Telegram channel scrapers).
- **Telemetry Processing**: Event-driven streaming, schema validation, and structured ingestion into time-series or relational stores.
- **Security & Privacy**: Strict API key rotation, credential sanitization in logs, rate limiting to avoid provider bans.
- **Async Concurrency**: Thread-pool offloading for blocking CPU/network tasks and persistent state stores.
"""
    ),
}

BUILTIN_MCPS: Dict[str, MCPDefinition] = {
    "sequential-thinking": MCPDefinition(
        id="sequential-thinking",
        name="Sequential Thinking Reasoning Engine",
        description="Structured multi-step reasoning, architectural decomposition, and complex logic.",
        patterns=[
            r"(?<![a-zA-Z0-9_])complex\s*logic(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])architecture(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])multi-?step(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])reasoning(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])архитектура(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])сложная\s*логика(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])пошагов(?:ое|ый|ая)(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])проектирование(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "complex logic", "architecture", "multi-step", "reasoning",
            "sequential", "архитектура", "сложная логика", "пошагово", "проектирование"
        ],
        config={
            "command": "npx",
            "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
        },
        default_enabled=True  # Included by default for all tasks
    ),

    "context7": MCPDefinition(
        id="context7",
        name="Context7 Up-to-date Docs & Large Codebase Context",
        description="Live official library documentation, semantic code search, and hallucination reduction.",
        patterns=[
            r"(?<![a-zA-Z0-9_])context7(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])semantic\s*search(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])large\s*codebase(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])codebase\s*context(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])docs(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])documentation(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])library\s*docs(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])поиск\s*по\s*кодовой\s*базе(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])документаци[яи](?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "context7", "semantic search", "large codebase", "codebase context",
            "docs", "documentation", "library docs", "поиск по кодовой базе", "документация"
        ],
        config={
            "command": "cmd",
            "args": ["/c", "npx", "-y", "@upstash/context7-mcp"]
        },
        default_enabled=False
    ),

    "playwright": MCPDefinition(
        id="playwright",
        name="Playwright Browser Automation & E2E Testing",
        description="Headless browser control, DOM interaction, page scraping, and end-to-end testing.",
        patterns=[
            r"(?<![a-zA-Z0-9_])playwright(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])browser(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])e2e(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])scraping(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])scraper(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])парсер(?:ы|а|ов)?(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])парсинг(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])браузер(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])автоматизация\s*браузера(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "playwright", "browser", "e2e", "scraping", "scraper",
            "парсер", "парсинг", "браузер"
        ],
        config={
            "command": "cmd",
            "args": ["/c", "npx", "-y", "@playwright/mcp@latest"]
        },
        default_enabled=False
    ),

    "firecrawl": MCPDefinition(
        id="firecrawl",
        name="Firecrawl Deep Web Ingestion & Markdown Extraction",
        description="Extract clean structured markdown and crawl multi-page web resources.",
        patterns=[
            r"(?<![a-zA-Z0-9_])firecrawl(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])crawl(?:er|ing)?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])web\s*scraping(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])краулер(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])сбор\s*сайтов(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "firecrawl", "crawler", "crawl", "web scraping", "краулер"
        ],
        config={
            "command": "cmd",
            "args": ["/c", "npx", "-y", "firecrawl-mcp"],
            "env": {"FIRECRAWL_API_KEY": "${FIRECRAWL_API_KEY}"}
        },
        default_enabled=False
    ),

    "chrome-devtools": MCPDefinition(
        id="chrome-devtools",
        name="Chrome DevTools Frontend & DOM Inspection",
        description="Inspect frontend DOM, live CSS styling, console errors, React components, and web UI.",
        patterns=[
            r"(?<![a-zA-Z0-9_])chrome-?devtools(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])devtools(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])dom(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])css(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])react(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])frontend(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])front-end(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])web\s*ui(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])ui(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])интерфейс(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])фронтенд(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])верстк[аи](?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])стили(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "chrome-devtools", "devtools", "dom", "css", "react", "frontend",
            "front-end", "web ui", "ui", "интерфейс", "фронтенд", "верстка"
        ],
        config={
            "command": "cmd",
            "args": ["/c", "npx", "-y", "chrome-devtools-mcp"]
        },
        default_enabled=False
    ),

    "github-mcp": MCPDefinition(
        id="github-mcp",
        name="GitHub Official Integration",
        description="GitHub repository search, pull request management, issue tracking, and CI/CD inspection.",
        patterns=[
            r"(?<![a-zA-Z0-9_])github(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])repo(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])repository(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])issues?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])pr(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])pull\s*requests?(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])git\s*repo(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])репозиторий(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])пуллреквест(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])ветк[аи](?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=[
            "github", "repo", "repository", "issue", "issues", "pr",
            "pull request", "git repo", "репозиторий", "пуллреквест"
        ],
        config={
            "type": "http",
            "url": "https://api.githubcopilot.com/mcp/",
            "headers": {"Authorization": "Bearer ${GITHUB_PERSONAL_ACCESS_TOKEN}"}
        },
        default_enabled=False
    ),

    "sqlite": MCPDefinition(
        id="sqlite",
        name="SQLite Database Inspector",
        description="Inspect local SQLite database schemas, query tables, and optimize indices.",
        patterns=[
            r"(?<![a-zA-Z0-9_])sqlite(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])sqlite3(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])database(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])база\s*данных(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])бд(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=["sqlite", "sqlite3", "database", "база данных", "бд"],
        config={
            "command": "uvx",
            "args": ["mcp-server-sqlite"]
        },
        default_enabled=False
    ),

    "memory": MCPDefinition(
        id="memory",
        name="Knowledge Graph Persistent Memory",
        description="Long-term entity and relation persistence across development sessions.",
        patterns=[
            r"(?<![a-zA-Z0-9_])memory(?![a-zA-Z0-9_])",
            r"(?<![a-zA-Z0-9_])knowledge\s*graph(?![a-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])память(?![а-яА-ЯёЁa-zA-Z0-9_])",
            r"(?<![а-яА-ЯёЁa-zA-Z0-9_])граф\s*знаний(?![а-яА-ЯёЁa-zA-Z0-9_])",
        ],
        keywords=["memory", "knowledge graph", "память", "граф знаний"],
        config={
            "command": "cmd",
            "args": ["/c", "npx", "-y", "@modelcontextprotocol/server-memory"]
        },
        default_enabled=False
    ),
}


# ============================================================================
# Smart Router Engine
# ============================================================================

class SmartRouter:
    """
    Intelligent routing and context orchestration engine.
    Matches developer prompts to skills and MCPs, generates configuration files,
    and constructs context-injected prompts for AI code generation.
    """

    def __init__(self, workspace_root: Optional[str] = None):
        self.workspace_root = Path(workspace_root) if workspace_root else Path(__file__).parent.resolve()
        self.skills: Dict[str, SkillDefinition] = dict(BUILTIN_SKILLS)
        self.mcps: Dict[str, MCPDefinition] = dict(BUILTIN_MCPS)
        self._load_local_skills()

    def _load_local_skills(self) -> None:
        """Dynamically load and enrich skills located in workspace skills directories."""
        skills_dir = self.workspace_root / "skills"
        if not skills_dir.exists():
            return

        for item in skills_dir.iterdir():
            skill_id = item.name
            skill_file = item / "SKILL.md" if item.is_dir() else (item if item.suffix in [".skill", ".md"] or "." not in item.name else None)
            if skill_file and skill_file.is_file():
                try:
                    content = skill_file.read_text(encoding="utf-8")
                    name = skill_id
                    description = f"Local workspace skill: {skill_id}"
                    
                    # Extract frontmatter if available
                    fm_match = re.search(r"^---\s*\n(.*?)\n---\s*\n(.*)$", content, re.DOTALL)
                    body = content
                    if fm_match:
                        fm_text, body = fm_match.groups()
                        for line in fm_text.splitlines():
                            if line.startswith("name:"):
                                name = line.split(":", 1)[1].strip()
                            elif line.startswith("description:"):
                                description = line.split(":", 1)[1].strip()

                    # Enrich existing builtin skill with full disk excerpt
                    if skill_id in self.skills:
                        if body.strip():
                            self.skills[skill_id].excerpt = body.strip()
                        if description:
                            self.skills[skill_id].description = description
                    else:
                        self.skills[skill_id] = SkillDefinition(
                            id=skill_id,
                            name=name,
                            description=description,
                            patterns=[rf"(?<![a-zA-Z0-9_]){re.escape(skill_id)}(?![a-zA-Z0-9_])"],
                            keywords=[skill_id],
                            associated_mcps=["sequential-thinking"],
                            excerpt=body.strip()
                        )
                except Exception:
                    pass

    def match_skills(self, prompt: str) -> List[str]:
        """
        Matches a prompt to relevant skills using regex patterns, keywords, and semantic tokens.
        Returns a list of matched skill IDs.
        """
        normalized_prompt = prompt.lower()
        matched: List[str] = []

        for skill_id, skill in self.skills.items():
            matched_flag = False

            # Test regex patterns
            for pattern in skill.patterns:
                if re.search(pattern, normalized_prompt, re.IGNORECASE):
                    matched.append(skill_id)
                    matched_flag = True
                    break

            if matched_flag:
                continue

            # Test keywords with safe boundary matching
            for kw in skill.keywords:
                kw_clean = kw.lower().strip()
                escaped_kw = re.escape(kw_clean)
                pattern = rf"(?<![а-яА-ЯёЁa-zA-Z0-9_]){escaped_kw}(?![а-яА-ЯёЁa-zA-Z0-9_])"
                if re.search(pattern, normalized_prompt, re.IGNORECASE):
                    matched.append(skill_id)
                    break

        return matched

    def match_mcps(self, prompt: str, matched_skills: Optional[List[str]] = None) -> List[str]:
        """
        Matches a prompt to relevant MCP servers, taking into account matched skills and defaults.
        Sequential-thinking is included by default for robust multi-step reasoning.
        """
        normalized_prompt = prompt.lower()
        matched: Set[str] = set()

        # 1. Include default MCPs (e.g. sequential-thinking)
        for mcp_id, mcp in self.mcps.items():
            if mcp.default_enabled:
                matched.add(mcp_id)

        # 2. Add MCPs associated with matched skills
        if matched_skills:
            for s_id in matched_skills:
                skill = self.skills.get(s_id)
                if skill:
                    for m_id in skill.associated_mcps:
                        if m_id in self.mcps:
                            matched.add(m_id)

        # 3. Direct MCP pattern & keyword matching
        for mcp_id, mcp in self.mcps.items():
            if mcp_id in matched:
                continue

            # Check patterns
            for pattern in mcp.patterns:
                if re.search(pattern, normalized_prompt, re.IGNORECASE):
                    matched.add(mcp_id)
                    break

            if mcp_id in matched:
                continue

            # Check keywords with safe boundaries
            for kw in mcp.keywords:
                kw_clean = kw.lower().strip()
                escaped_kw = re.escape(kw_clean)
                pattern = rf"(?<![а-яА-ЯёЁa-zA-Z0-9_]){escaped_kw}(?![а-яА-ЯёЁa-zA-Z0-9_])"
                if re.search(pattern, normalized_prompt, re.IGNORECASE):
                    matched.add(mcp_id)
                    break

        # Deterministic ordering: sequential-thinking first, then alphabetical
        return sorted(list(matched), key=lambda x: (x != "sequential-thinking", x))

    def generate_mcp_config(self, selected_mcps: List[str]) -> Dict[str, Any]:
        """
        Generates dynamic MCP config JSON dict containing only the active servers.
        """
        servers: Dict[str, Any] = {}
        for mcp_id in selected_mcps:
            mcp = self.mcps.get(mcp_id)
            if mcp:
                servers[mcp_id] = mcp.config
        return {"mcpServers": servers}

    def generate_antigravity_yaml(
        self,
        skills: List[str],
        mcps: List[str],
        prompt: str,
        context_data: Optional[Dict[str, Any]] = None
    ) -> str:
        """
        Generates dynamic .antigravity.yaml runtime configuration string.
        """
        data = {
            "version": "2.0",
            "generator": "BestStart SmartRouter",
            "task": {
                "prompt": prompt,
                "active_skills": skills,
                "active_mcps": mcps,
            },
            "runtime": {
                "routing_strategy": "dynamic_semantic_regex",
                "auto_inject": True,
                "context_injection_enabled": True,
            },
            "environment": {
                "target_platform": "multi-stack",
                "isolation": "docker-first",
            }
        }
        if context_data:
            data["metadata"] = context_data

        if yaml is not None:
            return yaml.dump(data, sort_keys=False, allow_unicode=True)
        
        # Fallback YAML serializer if PyYAML is not installed
        lines = [
            "# BestStart Dynamic Runtime Config",
            f"version: \"{data['version']}\"",
            f"generator: \"{data['generator']}\"",
            "task:",
            f"  prompt: {json.dumps(prompt, ensure_ascii=False)}",
            "  active_skills:",
        ]
        for s in skills:
            lines.append(f"    - {s}")
        lines.append("  active_mcps:")
        for m in mcps:
            lines.append(f"    - {m}")
        lines.append("runtime:")
        lines.append("  routing_strategy: \"dynamic_semantic_regex\"")
        lines.append("  auto_inject: true")
        lines.append("  context_injection_enabled: true")
        return "\n".join(lines) + "\n"

    def build_prompt_injection(self, skills: List[str], original_prompt: str) -> str:
        """
        Constructs a formatted context injection block containing relevant skill excerpts.
        """
        if not skills:
            return original_prompt

        injection_blocks: List[str] = []
        for s_id in skills:
            skill = self.skills.get(s_id)
            if skill and skill.excerpt:
                injection_blocks.append(
                    f"<!-- Skill Directives: {skill.id} ({skill.name}) -->\n{skill.excerpt.strip()}"
                )

        combined_excerpts = "\n\n".join(injection_blocks)

        formatted_prompt = (
            f"<injected_context>\n"
            f"{combined_excerpts}\n"
            f"</injected_context>\n\n"
            f"<user_task>\n"
            f"{original_prompt.strip()}\n"
            f"</user_task>"
        )
        return formatted_prompt

    def analyze(self, prompt: str) -> Tuple[List[str], List[str], Dict[str, Any]]:
        """
        Main routing function: analyzes prompt, matches skills and MCPs,
        generates dynamic configuration and prompt injection.
        """
        matched_skills = self.match_skills(prompt)
        matched_mcps = self.match_mcps(prompt, matched_skills)
        injected_prompt = self.build_prompt_injection(matched_skills, prompt)
        mcp_config = self.generate_mcp_config(matched_mcps)
        antigravity_yaml = self.generate_antigravity_yaml(matched_skills, matched_mcps, prompt)

        runtime_context: Dict[str, Any] = {
            "prompt": prompt,
            "skills": matched_skills,
            "mcps": matched_mcps,
            "injected_prompt": injected_prompt,
            "mcp_config": mcp_config,
            "antigravity_yaml": antigravity_yaml,
            "skill_count": len(matched_skills),
            "mcp_count": len(matched_mcps),
        }

        return matched_skills, matched_mcps, runtime_context

    def export_mcp_config(self, filepath: str, selected_mcps: List[str]) -> None:
        """Exports dynamic MCP JSON to a file."""
        config = self.generate_mcp_config(selected_mcps)
        target_path = Path(filepath)
        target_path.parent.mkdir(parents=True, exist_ok=True)
        target_path.write_text(json.dumps(config, indent=2, ensure_ascii=False), encoding="utf-8")

    def export_antigravity_yaml(
        self,
        filepath: str,
        skills: List[str],
        mcps: List[str],
        prompt: str
    ) -> None:
        """Exports .antigravity.yaml to a file."""
        content = self.generate_antigravity_yaml(skills, mcps, prompt)
        target_path = Path(filepath)
        target_path.parent.mkdir(parents=True, exist_ok=True)
        target_path.write_text(content, encoding="utf-8")

    def list_skills(self) -> List[Dict[str, Any]]:
        """Returns catalog of all registered skills."""
        return [
            {
                "id": s.id,
                "name": s.name,
                "description": s.description,
                "keywords": s.keywords,
                "associated_mcps": s.associated_mcps
            }
            for s in self.skills.values()
        ]

    def list_mcps(self) -> List[Dict[str, Any]]:
        """Returns catalog of all registered MCP servers."""
        return [
            {
                "id": m.id,
                "name": m.name,
                "description": m.description,
                "default_enabled": m.default_enabled,
                "config": m.config
            }
            for m in self.mcps.values()
        ]


# ============================================================================
# Public Module Interface
# ============================================================================

def analyze_task_and_inject(prompt: str) -> Tuple[List[str], List[str], Dict[str, Any]]:
    """
    Public standard entrypoint for dynamic prompt routing and injection.
    
    Returns:
        tuple (matched_skills, matched_mcps, runtime_context)
    """
    router = SmartRouter()
    return router.analyze(prompt)


# ============================================================================
# CLI Interface & Diagnostics
# ============================================================================

def run_self_test() -> int:
    """Executes a diagnostic smoke test for the smart router."""
    print("=" * 60)
    print("Running SmartRouter Internal Self-Test...")
    print("=" * 60)

    test_cases = [
        ("FastAPI microservice", "Создай микросервис на FastAPI и парсер", ["python_fastapi.skill"], ["sequential-thinking", "playwright", "firecrawl"]),
        ("C# WPF App", "Разработай десктопное приложение на C# WPF с XAML и MVVM", ["csharp_wpf.skill"], ["sequential-thinking", "context7"]),
        ("1C Extension", "Создай расширение для 1С:Предприятие УТ и документ СчетНаОплату", ["1c_enterprise.skill"], ["sequential-thinking", "context7"]),
        ("C++ Thread Pool", "Implement a low-latency thread pool in C++20 with std::jthread and smart pointers", ["cpp_native.skill"], ["sequential-thinking", "context7"]),
        ("C Win32 Driver", "Напиши системный драйвер на чистом Си с Win32 API и raw pointers", ["c_system.skill"], ["sequential-thinking", "context7"]),
        ("OSINT Bot", "Разработай OSINT бота на Aiogram для Telegram с GHunt", ["osint_telemetry.skill"], ["sequential-thinking", "context7"]),
    ]

    router = SmartRouter()
    passed = 0

    for name, prompt, exp_skills, exp_mcps in test_cases:
        skills, mcps, ctx = router.analyze(prompt)
        skills_ok = all(s in skills for s in exp_skills)
        mcps_ok = all(m in mcps for m in exp_mcps)
        status = "PASSED" if (skills_ok and mcps_ok) else "FAILED"
        print(f"[{status}] {name}")
        print(f"  Prompt: {prompt}")
        print(f"  Skills: {skills} (Expected: {exp_skills})")
        print(f"  MCPs  : {mcps} (Expected: {exp_mcps})")
        if skills_ok and mcps_ok:
            passed += 1
        print("-" * 60)

    print(f"Self-test results: {passed}/{len(test_cases)} passed.")
    return 0 if passed == len(test_cases) else 1


def main() -> None:
    parser = argparse.ArgumentParser(
        description="BestStart Smart Router & Task Orchestrator",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""Examples:
  python core_router.py --prompt "Создай микросервис на FastAPI и парсер"
  python core_router.py --prompt "C# WPF app" --export-mcp dynamic_mcp.json
  python core_router.py --prompt "1C Enterprise УТ" --export-yaml .antigravity.yaml
  python core_router.py --list-skills
  python core_router.py --list-mcps
  python core_router.py --test
"""
    )

    parser.add_argument("--prompt", "-p", type=str, help="Developer prompt to analyze and route")
    parser.add_argument("--export-mcp", type=str, help="Export dynamic MCP config JSON to specified path")
    parser.add_argument("--export-yaml", type=str, help="Export dynamic .antigravity.yaml to specified path")
    parser.add_argument("--list-skills", action="store_true", help="List all available skills and trigger keywords")
    parser.add_argument("--list-mcps", action="store_true", help="List all available MCP servers")
    parser.add_argument("--json", action="store_true", help="Output analysis result in JSON format")
    parser.add_argument("--test", action="store_true", help="Run diagnostic self-tests")

    args = parser.parse_args()
    router = SmartRouter()

    if args.test:
        sys.exit(run_self_test())

    if args.list_skills:
        print("=" * 70)
        print("BESTSTART SKILL CATALOG")
        print("=" * 70)
        for s in router.list_skills():
            print(f"ID         : {s['id']}")
            print(f"Name       : {s['name']}")
            print(f"Description: {s['description']}")
            print(f"Keywords   : {', '.join(s['keywords'][:8])}...")
            print(f"Associated : {', '.join(s['associated_mcps'])}")
            print("-" * 70)
        return

    if args.list_mcps:
        print("=" * 70)
        print("BESTSTART MCP SERVERS CATALOG")
        print("=" * 70)
        for m in router.list_mcps():
            print(f"ID         : {m['id']} {'[DEFAULT]' if m['default_enabled'] else ''}")
            print(f"Name       : {m['name']}")
            print(f"Description: {m['description']}")
            print(f"Config     : {json.dumps(m['config'])}")
            print("-" * 70)
        return

    if not args.prompt:
        parser.print_help()
        sys.exit(1)

    # Analyze task
    skills, mcps, context = router.analyze(args.prompt)

    if args.export_mcp:
        router.export_mcp_config(args.export_mcp, mcps)
        print(f"Dynamic MCP config exported to: {args.export_mcp}")

    if args.export_yaml:
        router.export_antigravity_yaml(args.export_yaml, skills, mcps, args.prompt)
        print(f"Dynamic Antigravity YAML exported to: {args.export_yaml}")

    if args.json:
        print(json.dumps(context, indent=2, ensure_ascii=False))
        return

    # Formatted standard console report
    print("=" * 70)
    print("BESTSTART SMART ROUTER - TASK ANALYSIS")
    print("=" * 70)
    print(f"Input Prompt:\n  \"{args.prompt}\"\n")
    print(f"Matched Skills ({len(skills)}):")
    for s in skills:
        info = router.skills.get(s)
        name = info.name if info else s
        print(f"  - {s} [{name}]")
    if not skills:
        print("  - None (Generic developer context applied)")

    print(f"\nActive MCP Servers ({len(mcps)}):")
    for m in mcps:
        info = router.mcps.get(m)
        desc = info.description if info else ""
        print(f"  - {m}: {desc}")

    print("\n" + "=" * 70)
    print("DYNAMIC MCP CONFIGURATION (JSON)")
    print("=" * 70)
    print(json.dumps(context["mcp_config"], indent=2))

    print("\n" + "=" * 70)
    print("INJECTED PROMPT CONTEXT (PREVIEW)")
    print("=" * 70)
    print(context["injected_prompt"])
    print("=" * 70)


if __name__ == "__main__":
    main()
