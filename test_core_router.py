#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
BestStart Core Router - Comprehensive Test Suite
------------------------------------------------
Unit and integration tests for SmartRouter, analyze_task_and_inject,
multi-stack keyword & regex matching, dynamic MCP configs, and YAML exports.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

# Add project root to sys.path
PROJECT_ROOT = Path(__file__).parent.resolve()
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

import core_router
from core_router import SmartRouter, analyze_task_and_inject

try:
    import yaml
except ImportError:
    yaml = None


class TestCoreRouter(unittest.TestCase):
    """Test suite covering all routing, matching, injection, and export functions."""

    def setUp(self):
        self.router = SmartRouter(workspace_root=str(PROJECT_ROOT))

    def test_pure_python_fastapi_task(self):
        """Test routing for pure Python FastAPI and scraper tasks."""
        prompts = [
            "Создай микросервис на FastAPI и парсер",
            "Build an async FastAPI service with Pydantic v2 schemas and scraper anti-blocking",
            "Напиши REST API бэкенд на Python FastAPI с uvicorn и beautifulsoup скрейпером",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "python_fastapi.skill",
                skills,
                f"Failed to match python_fastapi.skill for prompt: {prompt}"
            )
            self.assertIn(
                "sequential-thinking",
                mcps,
                f"sequential-thinking must be active for: {prompt}"
            )
            # Scraper or web api should trigger playwright / firecrawl
            self.assertTrue(
                "playwright" in mcps or "firecrawl" in mcps or "context7" in mcps,
                f"Expected scraping or doc MCPs in: {mcps}"
            )
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertIn("FastAPI", ctx["injected_prompt"])

    def test_csharp_wpf_task(self):
        """Test routing for C# / WPF / .NET enterprise desktop tasks."""
        prompts = [
            "Разработай десктопное приложение на C# WPF с XAML и MVVM",
            "Build a modern .NET 8 WPF application with CommunityToolkit.Mvvm and compiled bindings",
            "Создай приложение на CSharp WPF с Entity Framework Core и Dependency Injection",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "csharp_wpf.skill",
                skills,
                f"Failed to match csharp_wpf.skill for prompt: {prompt}"
            )
            self.assertIn("sequential-thinking", mcps)
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertTrue(
                "C#" in ctx["injected_prompt"] or "WPF" in ctx["injected_prompt"],
                "Injected context must contain C# or WPF directives"
            )

    def test_1c_enterprise_task(self):
        """Test routing for 1C:Enterprise (1С:Предприятие) tasks."""
        prompts = [
            "Создай расширение для 1С:Предприятие УТ и документ СчетНаОплату",
            "Разработай расширение .cfe для 1C:Enterprise Управление Торговлей с неинвазивным перехватом &Вместо",
            "Напиши внешнюю обработку epf для 1С с запросом к регистру сведений",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "1c_enterprise.skill",
                skills,
                f"Failed to match 1c_enterprise.skill for prompt: {prompt}"
            )
            self.assertIn("sequential-thinking", mcps)
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertTrue(
                "1C:Enterprise" in ctx["injected_prompt"] or "1С:Предприятие" in ctx["injected_prompt"] or "BSL" in ctx["injected_prompt"],
                "Injected context must contain 1C Enterprise directives"
            )

    def test_cpp_native_task(self):
        """Test routing for modern C++ native and concurrency tasks."""
        prompts = [
            "Implement a low-latency thread pool in C++20 with std::jthread, RAII and smart pointers",
            "Оптимизируй многопоточность на C++ с lock-free структурами и проверкой на утечки памяти в Valgrind",
            "Напиши модуль на cpp с CMake, boost и shared_ptr без memory leak",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "cpp_native.skill",
                skills,
                f"Failed to match cpp_native.skill for prompt: {prompt}"
            )
            self.assertIn("sequential-thinking", mcps)
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertTrue(
                "C++" in ctx["injected_prompt"] or "RAII" in ctx["injected_prompt"],
                "Injected context must contain C++ native directives"
            )

    def test_c_system_task(self):
        """Test routing for C system-level programming and Win32/POSIX tasks."""
        prompts = [
            "Напиши системный драйвер на чистом Си с Win32 API и raw pointers",
            "Implement a low-level C system utility using POSIX syscalls, malloc, and raw pointers",
            "Разработай модуль на языке Си с использованием Win32 API, pthreads и ручным управлением памятью",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "c_system.skill",
                skills,
                f"Failed to match c_system.skill for prompt: {prompt}"
            )
            self.assertIn("sequential-thinking", mcps)
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertTrue(
                "C System" in ctx["injected_prompt"] or "Win32" in ctx["injected_prompt"],
                "Injected context must contain C system directives"
            )

    def test_osint_telemetry_task(self):
        """Test routing for OSINT and telemetry reconnaissance tasks."""
        prompts = [
            "Разработай OSINT бота на Aiogram для Telegram с GHunt и пайплайном телеметрии",
            "Build an OSINT investigation pipeline with Aiogram 3 Telegram bot and reconnaissance metadata ingestion",
            "Создай сервис телеметрии и сбора данных с telegram ботом на aiogram",
        ]
        for prompt in prompts:
            skills, mcps, ctx = self.router.analyze(prompt)
            self.assertIn(
                "osint_telemetry.skill",
                skills,
                f"Failed to match osint_telemetry.skill for prompt: {prompt}"
            )
            self.assertIn("sequential-thinking", mcps)
            self.assertIn("<injected_context>", ctx["injected_prompt"])
            self.assertTrue(
                "OSINT" in ctx["injected_prompt"] or "Aiogram" in ctx["injected_prompt"],
                "Injected context must contain OSINT directives"
            )

    def test_complex_multistack_task(self):
        """Test routing for multi-stack tasks combining FastAPI, Playwright, React UI, and Sequential thinking."""
        prompt = (
            "Develop a full-stack system: FastAPI backend with Playwright scraper and Firecrawl, "
            "React frontend UI with CSS DevTools inspection, and complex multi-step architecture reasoning."
        )
        skills, mcps, ctx = self.router.analyze(prompt)

        # Matched skills
        self.assertIn("python_fastapi.skill", skills)

        # Matched MCPs
        self.assertIn("sequential-thinking", mcps)
        self.assertIn("playwright", mcps)
        self.assertIn("firecrawl", mcps)
        self.assertIn("chrome-devtools", mcps)

        # Context injection
        self.assertIn("<injected_context>", ctx["injected_prompt"])
        self.assertIn("<user_task>", ctx["injected_prompt"])
        self.assertIn(prompt.strip(), ctx["injected_prompt"])

        # MCP config validation
        mcp_servers = ctx["mcp_config"]["mcpServers"]
        self.assertIn("sequential-thinking", mcp_servers)
        self.assertIn("playwright", mcp_servers)
        self.assertIn("firecrawl", mcp_servers)
        self.assertIn("chrome-devtools", mcp_servers)

    def test_default_fallbacks(self):
        """Test that generic prompts fall back gracefully while keeping sequential-thinking."""
        generic_prompt = "Hello world, help me structure my project thoughts."
        skills, mcps, ctx = self.router.analyze(generic_prompt)

        # Default sequential thinking should always be enabled
        self.assertIn("sequential-thinking", mcps)
        self.assertEqual(len(skills), 0)
        self.assertEqual(ctx["injected_prompt"], generic_prompt)

        # MCP config contains at least sequential-thinking
        self.assertIn("sequential-thinking", ctx["mcp_config"]["mcpServers"])

    def test_config_exports_and_antigravity_yaml(self):
        """Test export functions for MCP JSON and .antigravity.yaml."""
        prompt = "FastAPI scraper with Playwright"
        skills, mcps, ctx = self.router.analyze(prompt)

        with tempfile.TemporaryDirectory() as tmpdir:
            mcp_file = Path(tmpdir) / "dynamic_mcp.json"
            yaml_file = Path(tmpdir) / ".antigravity.yaml"

            # Export MCP
            self.router.export_mcp_config(str(mcp_file), mcps)
            self.assertTrue(mcp_file.exists())
            with open(mcp_file, "r", encoding="utf-8") as f:
                loaded_mcp = json.load(f)
            self.assertIn("mcpServers", loaded_mcp)
            self.assertIn("sequential-thinking", loaded_mcp["mcpServers"])
            self.assertIn("playwright", loaded_mcp["mcpServers"])

            # Export YAML
            self.router.export_antigravity_yaml(str(yaml_file), skills, mcps, prompt)
            self.assertTrue(yaml_file.exists())
            yaml_text = yaml_file.read_text(encoding="utf-8")
            self.assertIn("version:", yaml_text)
            self.assertIn("active_skills:", yaml_text)
            self.assertIn("active_mcps:", yaml_text)

            if yaml is not None:
                parsed_yaml = yaml.safe_load(yaml_text)
                self.assertEqual(parsed_yaml["version"], "2.0")
                self.assertIn("python_fastapi.skill", parsed_yaml["task"]["active_skills"])
                self.assertIn("sequential-thinking", parsed_yaml["task"]["active_mcps"])

    def test_analyze_task_and_inject_function(self):
        """Test the top-level helper function analyze_task_and_inject."""
        prompt = "Создай микросервис на FastAPI и парсер"
        skills, mcps, ctx = analyze_task_and_inject(prompt)

        self.assertIsInstance(skills, list)
        self.assertIsInstance(mcps, list)
        self.assertIsInstance(ctx, dict)
        self.assertIn("python_fastapi.skill", skills)
        self.assertIn("sequential-thinking", mcps)
        self.assertIn("injected_prompt", ctx)
        self.assertIn("mcp_config", ctx)
        self.assertIn("antigravity_yaml", ctx)

    def test_list_skills_and_mcps(self):
        """Test list_skills and list_mcps methods."""
        skills_list = self.router.list_skills()
        mcps_list = self.router.list_mcps()

        self.assertGreaterEqual(len(skills_list), 6)
        self.assertGreaterEqual(len(mcps_list), 7)

        skill_ids = [s["id"] for s in skills_list]
        self.assertIn("csharp_wpf.skill", skill_ids)
        self.assertIn("cpp_native.skill", skill_ids)
        self.assertIn("c_system.skill", skill_ids)
        self.assertIn("python_fastapi.skill", skill_ids)
        self.assertIn("1c_enterprise.skill", skill_ids)
        self.assertIn("osint_telemetry.skill", skill_ids)

        mcp_ids = [m["id"] for m in mcps_list]
        self.assertIn("sequential-thinking", mcp_ids)
        self.assertIn("context7", mcp_ids)
        self.assertIn("playwright", mcp_ids)
        self.assertIn("firecrawl", mcp_ids)
        self.assertIn("chrome-devtools", mcp_ids)
        self.assertIn("github-mcp", mcp_ids)

    def test_cli_execution(self):
        """Test executing core_router.py via CLI subprocessing."""
        cmd = [sys.executable, str(PROJECT_ROOT / "core_router.py"), "--test"]
        res = subprocess.run(cmd, capture_output=True, text=True, cwd=str(PROJECT_ROOT))
        self.assertEqual(res.returncode, 0, f"CLI --test failed: {res.stderr}")
        self.assertIn("Self-test results: 6/6 passed", res.stdout)

        # Test CLI JSON output
        cmd_json = [
            sys.executable,
            str(PROJECT_ROOT / "core_router.py"),
            "--prompt", "C# WPF app with XAML",
            "--json"
        ]
        res_json = subprocess.run(cmd_json, capture_output=True, text=True, cwd=str(PROJECT_ROOT))
        self.assertEqual(res_json.returncode, 0, f"CLI --json failed: {res_json.stderr}")
        data = json.loads(res_json.stdout)
        self.assertIn("csharp_wpf.skill", data["skills"])
        self.assertIn("sequential-thinking", data["mcps"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
