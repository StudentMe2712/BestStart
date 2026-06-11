/**
 * Куратор-выжимка из docs/ai-ecosystem-catalog.md для раздела «Каталог».
 * Полная версия с таблицами/топами — в том markdown-файле в репозитории.
 * Звёзды (~) приблизительные, на середину 2026.
 */
export interface CatalogItem {
  name: string
  desc: string
  url: string
  stars?: string
  tag?: string
}

export interface CatalogSection {
  id: string
  title: string
  blurb?: string
  items: CatalogItem[]
}

export const CATALOG: CatalogSection[] = [
  {
    id: "mcp",
    title: "MCP-серверы",
    blurb:
      "Model Context Protocol — стандарт подключения инструментов/данных к AI. ~10k+ серверов, ~97M загрузок SDK/мес.",
    items: [
      { name: "Context7", desc: "Живые, версионные доки библиотек прямо в контекст", url: "https://github.com/upstash/context7", stars: "~56k", tag: "Knowledge" },
      { name: "Playwright MCP", desc: "Браузерная автоматизация/тесты (Microsoft)", url: "https://github.com/microsoft/playwright-mcp", stars: "~30k", tag: "Browser" },
      { name: "GitHub MCP", desc: "Репозитории, issues, PR, Actions (51 инструмент)", url: "https://github.com/github/github-mcp-server", stars: "~29k", tag: "Dev" },
      { name: "Figma Context", desc: "Дизайн-контекст Figma → код", url: "https://github.com/GLips/Figma-Context-MCP", stars: "~14k", tag: "Dev" },
      { name: "Google GenAI Toolbox", desc: "Мульти-БД сервер (Postgres/BigQuery/Spanner)", url: "https://github.com/googleapis/genai-toolbox", stars: "~14k", tag: "DB" },
      { name: "Chrome DevTools MCP", desc: "Отладка/автоматизация реального Chrome", url: "https://github.com/ChromeDevTools/chrome-devtools-mcp", stars: "~11k", tag: "Browser" },
      { name: "AWS MCP servers", desc: "Официальный набор серверов AWS", url: "https://github.com/awslabs/mcp", stars: "~9k", tag: "Cloud" },
      { name: "Firecrawl", desc: "Краулинг + URL→Markdown для RAG", url: "https://github.com/firecrawl/firecrawl-mcp-server", stars: "~6k", tag: "Search" },
      { name: "Supabase MCP", desc: "БД/auth/edge-функции одним сервером", url: "https://github.com/supabase-community/supabase-mcp", stars: "~2.7k", tag: "DB" },
      { name: "Sentry MCP", desc: "Ошибки/перформанс + Seer root-cause", url: "https://github.com/getsentry/sentry-mcp", stars: "~5k", tag: "Monitoring" }
    ]
  },
  {
    id: "skills",
    title: "Skills / Commands",
    blurb:
      "Переиспользуемые навыки (Claude Agent Skills, GPT actions) и наборы команд для AI-агентов.",
    items: [
      { name: "anthropics/skills", desc: "Официальные Agent Skills (docx/xlsx/pdf/pptx, artifacts)", url: "https://github.com/anthropics/skills", stars: "~146k", tag: "Skills" },
      { name: "awesome-chatgpt-prompts", desc: "Крупнейшая открытая библиотека промптов", url: "https://github.com/f/awesome-chatgpt-prompts", stars: "~143k", tag: "Prompts" },
      { name: "dair-ai Prompt Guide", desc: "Де-факто учебник по промпт-инжинирингу", url: "https://github.com/dair-ai/Prompt-Engineering-Guide", stars: "~74k", tag: "Education" },
      { name: "Fabric", desc: "200+ переиспользуемых AI-паттернов через CLI", url: "https://github.com/danielmiessler/fabric", stars: "~30k", tag: "Prompts" },
      { name: "wshobson/agents", desc: "192 агента / 156 skills / 102 команды (мульти-харнесс)", url: "https://github.com/wshobson/agents", stars: "~14k", tag: "Skills" },
      { name: "VoltAgent subagents", desc: "154+ специализированных subagent'ов", url: "https://github.com/VoltAgent/awesome-claude-code-subagents", stars: "~6k", tag: "Skills" },
      { name: "Claude-Command-Suite", desc: "216+ slash-команд, 54 агента", url: "https://github.com/qdhenry/Claude-Command-Suite", tag: "Commands" }
    ]
  },
  {
    id: "agents",
    title: "Агенты и SDK",
    blurb: "Фреймворки для агентов и SDK для сборки AI-приложений.",
    items: [
      { name: "AutoGPT", desc: "Визуальный билдер автономных агентов + маркетплейс", url: "https://github.com/Significant-Gravitas/AutoGPT", stars: "~182k", tag: "Agent" },
      { name: "browser-use", desc: "Браузерный агент на LLM", url: "https://github.com/browser-use/browser-use", stars: "~92k", tag: "Agent" },
      { name: "OpenHands", desc: "Открытый автономный SWE-агент (Devin-style)", url: "https://github.com/All-Hands-AI/OpenHands", stars: "~70k", tag: "Agent" },
      { name: "LangChain", desc: "Широкий фреймворк для LLM-приложений", url: "https://github.com/langchain-ai/langchain", stars: "~90k", tag: "Framework" },
      { name: "CrewAI", desc: "Ролевые мульти-агентные «команды»", url: "https://www.crewai.com", stars: "~46k", tag: "Framework" },
      { name: "LangGraph", desc: "Граф-оркестрация stateful-агентов", url: "https://github.com/langchain-ai/langgraph", stars: "~25k", tag: "Framework" },
      { name: "Vercel AI SDK", desc: "TS-тулкит для AI-приложений (стриминг, tools)", url: "https://github.com/vercel/ai", stars: "~21k", tag: "SDK" },
      { name: "Pydantic AI", desc: "Типобезопасные Python-агенты", url: "https://ai.pydantic.dev/", stars: "~16.5k", tag: "SDK" }
    ]
  },
  {
    id: "connectors",
    title: "Коннекторы",
    blurb: "Интеграции к внешним системам (часто как MCP-серверы) + агрегаторы.",
    items: [
      { name: "Zapier MCP", desc: "9000+ приложений через один коннектор", url: "https://zapier.com/mcp", tag: "Aggregator" },
      { name: "Composio", desc: "1000+ инструментов, поиск/авторизация/RBAC (OSS-ядро)", url: "https://composio.dev/toolkits", tag: "Aggregator" },
      { name: "n8n", desc: "1200+ нод, визуальные AI-воркфлоу, self-host", url: "https://n8n.io/integrations/", stars: "~170k", tag: "Automation" },
      { name: "Notion", desc: "Доки/БД/wiki — официальный MCP", url: "https://github.com/makenotion/notion-mcp-server", stars: "~4.4k", tag: "PM" },
      { name: "Slack", desc: "Каналы/сообщения/поиск (официальный + reference)", url: "https://github.com/modelcontextprotocol/servers", tag: "Messaging" },
      { name: "Jira / Confluence", desc: "Официальный remote MCP, OAuth", url: "https://www.atlassian.com/platform/remote-mcp-server", tag: "PM" },
      { name: "Stripe Agent Toolkit", desc: "Платежи/клиенты/инвойсы", url: "https://github.com/stripe/agent-toolkit", stars: "~1.5k", tag: "Payments" }
    ]
  },
  {
    id: "tools",
    title: "Инструменты (OSS)",
    blurb: "Локальные раннеры, инференс, шлюзы, билдеры приложений и чат-UI.",
    items: [
      { name: "Ollama", desc: "Стандарт локального запуска LLM (OpenAI-совместимый API)", url: "https://github.com/ollama/ollama", stars: "~164k", tag: "Local" },
      { name: "Open WebUI", desc: "Self-host ChatGPT-подобный UI с RAG", url: "https://github.com/open-webui/open-webui", stars: "~136k", tag: "UI" },
      { name: "Dify", desc: "Платформа LLM-приложений/агентов/воркфлоу", url: "https://github.com/langgenius/dify", stars: "~138k", tag: "Builder" },
      { name: "Langflow", desc: "Визуальный low-code билдер RAG/агентов", url: "https://github.com/langflow-ai/langflow", stars: "~146k", tag: "Builder" },
      { name: "LiteLLM", desc: "Шлюз к 100+ LLM в формате OpenAI", url: "https://github.com/BerriAI/litellm", tag: "Gateway" },
      { name: "vLLM", desc: "Высокопроизводительный инференс LLM", url: "https://github.com/vllm-project/vllm", tag: "Inference" },
      { name: "AnythingLLM", desc: "RAG + агенты + MCP «из коробки»", url: "https://github.com/Mintplex-Labs/anything-llm", tag: "RAG" }
    ]
  },
  {
    id: "plugins",
    title: "Плагины / Расширения",
    blurb: "IDE-ассистенты/агенты и браузерные расширения.",
    items: [
      { name: "GitHub Copilot", desc: "Самый распространённый IDE-ассистент (agent mode)", url: "https://github.com/features/copilot", stars: "~15M dev", tag: "IDE" },
      { name: "Cline", desc: "Открытый IDE-агент (Plan/Act)", url: "https://github.com/cline/cline", stars: "~61k", tag: "IDE" },
      { name: "Continue", desc: "Открытый IDE-ассистент (агентный)", url: "https://github.com/continuedev/continue", stars: "~38k", tag: "IDE" },
      { name: "Aider", desc: "Терминальный AI pair-programmer", url: "https://github.com/Aider-AI/aider", stars: "~62k", tag: "CLI" },
      { name: "Roo Code", desc: "Форк Cline с режимами (Code/Architect/Debug)", url: "https://github.com/RooCodeInc/Roo-Code", tag: "IDE" },
      { name: "Sider AI", desc: "Лучший браузерный AI-сайдбар", url: "https://sider.ai/", stars: "~2M users", tag: "Browser" },
      { name: "Smart Connections", desc: "AI-связи и чат с заметками в Obsidian", url: "https://github.com/brianpetro/obsidian-smart-connections", tag: "Knowledge" }
    ]
  },
  {
    id: "pam",
    title: "Вайбкодинг для PAM",
    blurb:
      "Подборка под наш проект: local-first FastAPI + Next + Postgres/pgvector + Ollama/Groq + RAG + лектор. Что реально полезно при разработке именно PAM.",
    items: [
      { name: "Claude Code", desc: "Агентная CLI, в которой и пишется PAM (skills, hooks, subagents, /security-review)", url: "https://docs.claude.com", tag: "Кодинг" },
      { name: "Context7 MCP", desc: "Свежие доки FastAPI/Next/SQLAlchemy/Prisma в контекст — меньше галлюцинаций по API", url: "https://github.com/upstash/context7", stars: "~56k", tag: "Доки" },
      { name: "Postgres MCP", desc: "Запросы/инспекция схемы к Neon/локальному Postgres прямо из чата", url: "https://github.com/modelcontextprotocol/servers", tag: "БД" },
      { name: "Playwright MCP", desc: "Прогон/проверка веб-UI и расширения без ручного клика", url: "https://github.com/microsoft/playwright-mcp", stars: "~30k", tag: "Тесты" },
      { name: "GitHub MCP", desc: "Ветки/PR/issues PAM из агента (у нас фазовый git-флоу)", url: "https://github.com/github/github-mcp-server", stars: "~29k", tag: "Git" },
      { name: "Ollama", desc: "Уже используем для эмбеддингов (nomic-embed-text); можно и локальная чат-модель вместо Groq", url: "https://github.com/ollama/ollama", stars: "~164k", tag: "Эмбеддинги" },
      { name: "LiteLLM", desc: "Единый шлюз к Groq/Ollama/Claude — упростил бы переключение провайдеров в llm.py", url: "https://github.com/BerriAI/litellm", tag: "LLM-шлюз" },
      { name: "pgvector", desc: "Векторный поиск в Postgres — фундамент нашего RAG (chunks + HNSW)", url: "https://github.com/pgvector/pgvector", tag: "RAG" },
      { name: "AnythingLLM / Open WebUI", desc: "Референс RAG-UX: чанкинг, цитаты источников, MCP — идеи для PAM", url: "https://github.com/Mintplex-Labs/anything-llm", tag: "Референс" },
      { name: "awesome-claude-code", desc: "Индекс skills/hooks/команд для Claude Code — ускоряет разработку PAM", url: "https://github.com/hesreallyhim/awesome-claude-code", tag: "Воркфлоу" }
    ]
  }
]
