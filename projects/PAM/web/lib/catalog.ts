/**
 * Каталог 2.0 — кураторская витрина AI-экосистемы для PAM (маркетплейс).
 * Источник идей/звёзд: docs/ai-ecosystem-catalog.md (середина 2026).
 * Рейтинги и «пользователи» — иллюстративные, для витрины.
 */

export type CatalogCategory =
  | "mcp"
  | "agents"
  | "tools"
  | "connectors"
  | "plugins"
  | "pam"

export type Accent =
  | "lime"
  | "purple"
  | "emerald"
  | "blue"
  | "orange"
  | "sky"
  | "amber"
  | "rose"
  | "indigo"
  | "neutral"

export interface CatalogTool {
  slug: string
  name: string
  category: CatalogCategory
  typeLabel: string
  logo: string // монограмма
  accent: Accent
  short: string
  description: string
  tags: string[]
  rating: number
  users: string
  url: string
  docsUrl?: string
  install?: string
  compatibility?: string[]
  featured?: boolean
  popular?: boolean
  isNew?: boolean
  updatedRecently?: boolean
}

// Палитра акцентов: плитка логотипа, градиент featured-карточки, цвет текста.
export const ACCENTS: Record<Accent, { tile: string; grad: string; text: string }> = {
  lime: { tile: "bg-lime-400/15 text-lime-300 border-lime-400/30", grad: "from-lime-600/30 via-lime-900/10", text: "text-lime-300" },
  purple: { tile: "bg-purple-500/15 text-purple-300 border-purple-500/30", grad: "from-purple-700/40 via-purple-900/15", text: "text-purple-300" },
  emerald: { tile: "bg-emerald-500/15 text-emerald-300 border-emerald-500/30", grad: "from-emerald-700/35 via-emerald-900/15", text: "text-emerald-300" },
  blue: { tile: "bg-blue-500/15 text-blue-300 border-blue-500/30", grad: "from-blue-700/40 via-blue-900/15", text: "text-blue-300" },
  orange: { tile: "bg-orange-500/15 text-orange-300 border-orange-500/30", grad: "from-orange-700/35 via-orange-900/15", text: "text-orange-300" },
  sky: { tile: "bg-sky-500/15 text-sky-300 border-sky-500/30", grad: "from-sky-700/35 via-sky-900/15", text: "text-sky-300" },
  amber: { tile: "bg-amber-500/15 text-amber-300 border-amber-500/30", grad: "from-amber-600/35 via-amber-900/15", text: "text-amber-300" },
  rose: { tile: "bg-rose-500/15 text-rose-300 border-rose-500/30", grad: "from-rose-700/35 via-rose-900/15", text: "text-rose-300" },
  indigo: { tile: "bg-indigo-500/15 text-indigo-300 border-indigo-500/30", grad: "from-indigo-700/40 via-indigo-900/15", text: "text-indigo-300" },
  neutral: { tile: "bg-neutral-700/40 text-neutral-200 border-neutral-600/40", grad: "from-neutral-700/40 via-neutral-900/20", text: "text-neutral-200" }
}

export const CATEGORIES: {
  key: CatalogCategory
  label: string
  count: number
}[] = [
  { key: "mcp", label: "MCP-серверы", count: 120 },
  { key: "agents", label: "Агенты и SDK", count: 48 },
  { key: "tools", label: "Инструменты", count: 67 },
  { key: "connectors", label: "Коннекторы", count: 36 },
  { key: "plugins", label: "Плагины / Расширения", count: 29 },
  { key: "pam", label: "Вайбкодинг для PAM", count: 15 }
]

export const CATEGORY_LABEL: Record<CatalogCategory, string> = {
  mcp: "MCP-серверы",
  agents: "Агенты и SDK",
  tools: "Инструменты",
  connectors: "Коннекторы",
  plugins: "Плагины",
  pam: "Вайбкодинг для PAM"
}

export const TOOLS: CatalogTool[] = [
  // ── Featured / MCP ──────────────────────────────────────────────────────
  {
    slug: "context7", name: "Context7 MCP", category: "mcp", typeLabel: "MCP", logo: "C7", accent: "purple",
    short: "Живая, актуальная документация и библиотеки прямо в контекст.",
    description: "Context7 подтягивает свежую, версионную документацию библиотек и фреймворков прямо в контекст модели — меньше галлюцинаций по API, всегда актуальные примеры. Особенно полезно при работе с быстро меняющимися фреймворками (Next.js, FastAPI, SQLAlchemy).",
    tags: ["Документация", "Контекст"], rating: 4.9, users: "1.2k", url: "https://github.com/upstash/context7",
    install: "npx -y @upstash/context7-mcp", compatibility: ["Claude Code", "Cursor", "Windsurf", "VS Code"],
    featured: true, updatedRecently: true
  },
  {
    slug: "playwright-mcp", name: "Playwright MCP", category: "mcp", typeLabel: "MCP", logo: "PW", accent: "emerald",
    short: "Браузерная автоматизация и тесты через AI (Microsoft).",
    description: "Playwright MCP даёт агенту управлять настоящим браузером: навигация, клики, заполнение форм, скриншоты, проверка UI. Идеально для автоматического прогона веб-интерфейсов и E2E-проверок без ручного клика.",
    tags: ["Браузер", "Автоматизация"], rating: 4.8, users: "986", url: "https://github.com/microsoft/playwright-mcp",
    install: "npx -y @playwright/mcp@latest", compatibility: ["Claude Code", "Cursor", "VS Code"],
    featured: true
  },
  {
    slug: "github-mcp", name: "GitHub MCP", category: "mcp", typeLabel: "MCP", logo: "GH", accent: "blue",
    short: "Репозитории, поиск, PR, Actions — с AI-интеграцией.",
    description: "Официальный GitHub MCP: 50+ инструментов для работы с репозиториями, issues, pull-request'ами, ветками и Actions прямо из агента. Подходит для фазового git-флоу.",
    tags: ["Git", "Разработка"], rating: 4.7, users: "854", url: "https://github.com/github/github-mcp-server",
    install: "docker run -i ghcr.io/github/github-mcp-server", compatibility: ["Claude Code", "Cursor", "VS Code"],
    featured: true, updatedRecently: true
  },
  {
    slug: "figma-context", name: "Figma Context", category: "mcp", typeLabel: "MCP", logo: "Fi", accent: "orange",
    short: "Дизайн в контексте: Figma + AI = код.",
    description: "Figma Context MCP передаёт агенту структуру и стили макета из Figma, чтобы генерировать вёрстку максимально близко к дизайну. Мост между дизайнером и кодом.",
    tags: ["Дизайн", "UI/UX"], rating: 4.6, users: "792", url: "https://github.com/GLips/Figma-Context-MCP",
    install: "npx -y figma-developer-mcp", compatibility: ["Claude Code", "Cursor", "Windsurf"],
    featured: true
  },
  {
    slug: "supabase-mcp", name: "Supabase MCP", category: "mcp", typeLabel: "MCP", logo: "SB", accent: "emerald",
    short: "БД, auth и edge-функции одним сервером.",
    description: "Supabase MCP даёт доступ к базе, аутентификации и edge-функциям проекта Supabase из агента: запросы, миграции, инспекция схемы.",
    tags: ["БД", "Backend"], rating: 4.5, users: "640", url: "https://github.com/supabase-community/supabase-mcp",
    install: "npx -y @supabase/mcp-server-supabase", compatibility: ["Claude Code", "Cursor"],
    featured: true, popular: true
  },
  {
    slug: "firecrawl", name: "Firecrawl", category: "tools", typeLabel: "SEARCH", logo: "🔥", accent: "rose",
    short: "Краулинг + URL→Markdown для RAG.",
    description: "Firecrawl превращает сайты в чистый Markdown, готовый для RAG: обходит страницы, чистит шум, отдаёт структурированный текст. Основа для наполнения памяти из веба.",
    tags: ["Краулинг", "RAG"], rating: 4.6, users: "712", url: "https://github.com/firecrawl/firecrawl-mcp-server",
    install: "npx -y firecrawl-mcp", compatibility: ["Claude Code", "Cursor"],
    featured: true
  },

  // ── Популярные MCP ──────────────────────────────────────────────────────
  {
    slug: "google-genai-toolbox", name: "Google GenAI Toolbox", category: "mcp", typeLabel: "MCP", logo: "G", accent: "blue",
    short: "Пакет инструментов для генерации с Gemini.",
    description: "Мульti-БД сервер от Google (Postgres / BigQuery / Spanner) и набор инструментов для генеративных сценариев с Gemini.",
    tags: ["БД", "Gemini"], rating: 4.6, users: "624", url: "https://github.com/googleapis/genai-toolbox",
    compatibility: ["Claude Code", "Gemini CLI"], popular: true
  },
  {
    slug: "chrome-devtools-mcp", name: "Chrome DevTools MCP", category: "mcp", typeLabel: "MCP", logo: "Ch", accent: "amber",
    short: "Отладка и автоматизация реального Chrome.",
    description: "Доступ к протоколу Chrome DevTools: инспекция, сеть, консоль, перфоманс — для отладки и автоматизации реального браузера.",
    tags: ["Браузер", "Отладка"], rating: 4.5, users: "512", url: "https://github.com/ChromeDevTools/chrome-devtools-mcp",
    compatibility: ["Claude Code", "Cursor"], popular: true
  },
  {
    slug: "aws-mcp", name: "AWS MCP Servers", category: "mcp", typeLabel: "MCP", logo: "AWS", accent: "orange",
    short: "Официальный набор серверов AWS.",
    description: "Набор официальных MCP-серверов AWS: доступ к сервисам, документации и инфраструктуре из агента.",
    tags: ["Cloud", "AWS"], rating: 4.4, users: "431", url: "https://github.com/awslabs/mcp",
    compatibility: ["Claude Code"], popular: true
  },
  {
    slug: "filesystem-mcp", name: "Filesystem MCP", category: "mcp", typeLabel: "MCP", logo: "FS", accent: "sky",
    short: "Безопасный доступ к файловой системе.",
    description: "Reference-сервер для безопасного чтения/записи файлов в заданных каталогах — базовый кирпичик для агентов, работающих с локальными файлами.",
    tags: ["Файлы", "Reference"], rating: 4.4, users: "398", url: "https://github.com/modelcontextprotocol/servers",
    install: "npx -y @modelcontextprotocol/server-filesystem", compatibility: ["Claude Code", "Cursor", "VS Code"], popular: true
  },
  {
    slug: "postgres-mcp", name: "PostgreSQL MCP", category: "mcp", typeLabel: "MCP", logo: "Pg", accent: "indigo",
    short: "Работа с PostgreSQL через естественный язык.",
    description: "Запросы и инспекция схемы PostgreSQL из чата: читай данные, проверяй структуру, отлаживай запросы. В PAM удобно для локальной/Neon-базы.",
    tags: ["БД", "SQL"], rating: 4.3, users: "376", url: "https://github.com/modelcontextprotocol/servers",
    install: "npx -y @modelcontextprotocol/server-postgres", compatibility: ["Claude Code", "Cursor"], popular: true
  },

  // ── Инструменты ─────────────────────────────────────────────────────────
  {
    slug: "notion-mcp", name: "Notion MCP", category: "tools", typeLabel: "MCP", logo: "N", accent: "neutral",
    short: "Чтение и запись в Notion через AI.",
    description: "Официальный Notion MCP: страницы, базы данных и wiki доступны агенту для чтения и записи. Память и заметки под рукой у модели.",
    tags: ["Заметки", "PM"], rating: 4.2, users: "284", url: "https://github.com/makenotion/notion-mcp-server",
    compatibility: ["Claude Code", "Cursor"]
  },
  {
    slug: "slack-mcp", name: "Slack MCP", category: "tools", typeLabel: "MCP", logo: "Sl", accent: "rose",
    short: "Интеграция со Slack: каналы, сообщения, поиск.",
    description: "Доступ к каналам, сообщениям и поиску Slack из агента — уведомления, сводки, ответы в тредах.",
    tags: ["Мессенджер", "Коммуникация"], rating: 4.3, users: "256", url: "https://github.com/modelcontextprotocol/servers",
    compatibility: ["Claude Code"]
  },
  {
    slug: "pinecone-mcp", name: "Pinecone MCP", category: "tools", typeLabel: "MCP", logo: "Pc", accent: "blue",
    short: "Векторный поиск и работа с Pinecone.",
    description: "Управление векторным индексом Pinecone из агента: upsert, поиск по сходству, метаданные — для RAG-сценариев на управляемой векторной БД.",
    tags: ["Вектор", "RAG"], rating: 4.2, users: "198", url: "https://www.pinecone.io/",
    compatibility: ["Claude Code"]
  },
  {
    slug: "exa-search", name: "Exa Search", category: "tools", typeLabel: "SEARCH", logo: "Ex", accent: "sky",
    short: "Поиск в интернете с фокусом на AI.",
    description: "Семантический веб-поиск Exa, спроектированный под LLM: находит релевантные источники и отдаёт чистый контент для дальнейшей обработки.",
    tags: ["Поиск", "Веб"], rating: 4.4, users: "221", url: "https://exa.ai/",
    compatibility: ["Claude Code", "Cursor"]
  },

  // ── Новые поступления ───────────────────────────────────────────────────
  {
    slug: "deepl-mcp", name: "DeepL MCP", category: "mcp", typeLabel: "MCP", logo: "DL", accent: "blue",
    short: "Перевод и локализация.",
    description: "Качественный машинный перевод DeepL из агента — локализация контента и интерфейсов «на лету».",
    tags: ["Перевод", "Локализация"], rating: 4.2, users: "96", url: "https://www.deepl.com/",
    compatibility: ["Claude Code"], isNew: true
  },
  {
    slug: "openapi-mcp", name: "OpenAPI MCP", category: "mcp", typeLabel: "MCP", logo: "{}", accent: "emerald",
    short: "Работа с OpenAPI / Swagger.",
    description: "Подключает любой REST API по его OpenAPI/Swagger-спеке как набор инструментов агента — без ручного написания обёрток.",
    tags: ["API", "Интеграция"], rating: 4.1, users: "84", url: "https://github.com/modelcontextprotocol/servers",
    compatibility: ["Claude Code", "Cursor"], isNew: true
  },
  {
    slug: "sentry-mcp", name: "Sentry MCP", category: "mcp", typeLabel: "MCP", logo: "Se", accent: "purple",
    short: "Мониторинг и ошибки.",
    description: "Доступ к ошибкам, перформансу и root-cause-анализу Sentry из агента — быстрее находить и чинить регрессии.",
    tags: ["Мониторинг", "Ошибки"], rating: 4.1, users: "73", url: "https://github.com/getsentry/sentry-mcp",
    compatibility: ["Claude Code"], isNew: true
  },
  {
    slug: "elastic-mcp", name: "Elastic MCP", category: "mcp", typeLabel: "MCP", logo: "El", accent: "amber",
    short: "Поиск и аналитика с Elastic.",
    description: "Полнотекстовый поиск и аналитика Elasticsearch из агента: запросы, агрегации, инспекция индексов.",
    tags: ["Поиск", "Аналитика"], rating: 4.0, users: "61", url: "https://www.elastic.co/",
    compatibility: ["Claude Code"], isNew: true
  },
  {
    slug: "stripe-mcp", name: "Stripe MCP", category: "mcp", typeLabel: "MCP", logo: "St", accent: "indigo",
    short: "Платежи и биллинг.",
    description: "Stripe Agent Toolkit: платежи, клиенты, инвойсы и подписки доступны агенту для автоматизации биллинга.",
    tags: ["Платежи", "Биллинг"], rating: 4.2, users: "112", url: "https://github.com/stripe/agent-toolkit",
    compatibility: ["Claude Code"], isNew: true
  },

  // ── Агенты и SDK ────────────────────────────────────────────────────────
  {
    slug: "autogpt", name: "AutoGPT", category: "agents", typeLabel: "Агент", logo: "AG", accent: "purple",
    short: "Визуальный билдер автономных агентов + маркетплейс.",
    description: "Платформа для сборки автономных агентов с визуальным конструктором и маркетплейсом готовых сценариев.",
    tags: ["Агент", "No-code"], rating: 4.5, users: "1.1k", url: "https://github.com/Significant-Gravitas/AutoGPT",
    compatibility: ["Standalone"]
  },
  {
    slug: "browser-use", name: "browser-use", category: "agents", typeLabel: "Агент", logo: "BU", accent: "sky",
    short: "Браузерный агент на LLM.",
    description: "Даёт LLM управлять браузером для выполнения задач: навигация, формы, извлечение данных — программно из Python.",
    tags: ["Браузер", "Агент"], rating: 4.4, users: "880", url: "https://github.com/browser-use/browser-use",
    compatibility: ["Python"], updatedRecently: true
  },
  {
    slug: "openhands", name: "OpenHands", category: "agents", typeLabel: "Агент", logo: "OH", accent: "amber",
    short: "Открытый автономный SWE-агент (Devin-style).",
    description: "Автономный инженерный агент: читает задачу, правит код, запускает тесты, открывает PR. Open-source альтернатива Devin.",
    tags: ["SWE", "Агент"], rating: 4.3, users: "640", url: "https://github.com/All-Hands-AI/OpenHands",
    compatibility: ["Standalone"]
  },
  {
    slug: "langchain", name: "LangChain", category: "agents", typeLabel: "Framework", logo: "LC", accent: "emerald",
    short: "Широкий фреймворк для LLM-приложений.",
    description: "Популярный фреймворк для построения LLM-приложений: цепочки, инструменты, память, ретриверы и интеграции.",
    tags: ["Framework", "SDK"], rating: 4.2, users: "1.5k", url: "https://github.com/langchain-ai/langchain",
    compatibility: ["Python", "JS/TS"]
  },
  {
    slug: "crewai", name: "CrewAI", category: "agents", typeLabel: "Framework", logo: "Cr", accent: "rose",
    short: "Ролевые мульти-агентные «команды».",
    description: "Оркестрация команд агентов с ролями и задачами: агенты сотрудничают, делегируют и доводят задачу до результата.",
    tags: ["Мульти-агент", "Framework"], rating: 4.3, users: "560", url: "https://www.crewai.com",
    compatibility: ["Python"]
  },
  {
    slug: "vercel-ai-sdk", name: "Vercel AI SDK", category: "agents", typeLabel: "SDK", logo: "▲", accent: "neutral",
    short: "TS-тулкит для AI-приложений (стриминг, tools).",
    description: "TypeScript-SDK для AI-приложений: унифицированный стриминг, вызов инструментов, поддержка многих провайдеров. Хорошо ложится на Next.js.",
    tags: ["SDK", "TypeScript"], rating: 4.6, users: "1.3k", url: "https://github.com/vercel/ai",
    compatibility: ["JS/TS", "Next.js"], updatedRecently: true
  },

  // ── Коннекторы ──────────────────────────────────────────────────────────
  {
    slug: "zapier-mcp", name: "Zapier MCP", category: "connectors", typeLabel: "Коннектор", logo: "Za", accent: "orange",
    short: "9000+ приложений через один коннектор.",
    description: "Подключает тысячи приложений Zapier к агенту как инструменты — самый широкий охват интеграций одним сервером.",
    tags: ["Агрегатор", "Интеграции"], rating: 4.3, users: "520", url: "https://zapier.com/mcp",
    compatibility: ["Claude Code", "Cursor"]
  },
  {
    slug: "composio", name: "Composio", category: "connectors", typeLabel: "Коннектор", logo: "Co", accent: "indigo",
    short: "1000+ инструментов с авторизацией и RBAC.",
    description: "Каталог из 1000+ инструментов с управляемой авторизацией, поиском и RBAC. OSS-ядро, удобно для продакшен-агентов.",
    tags: ["Агрегатор", "Auth"], rating: 4.4, users: "470", url: "https://composio.dev/toolkits",
    compatibility: ["Claude Code", "Python"]
  },
  {
    slug: "n8n", name: "n8n", category: "connectors", typeLabel: "Automation", logo: "n8", accent: "rose",
    short: "1200+ нод, визуальные AI-воркфлоу, self-host.",
    description: "Визуальная платформа автоматизации с 1200+ нодами и AI-воркфлоу. Self-host — в PAM крутится локально рядом с приложением.",
    tags: ["Автоматизация", "Self-host"], rating: 4.5, users: "1.4k", url: "https://n8n.io/integrations/",
    compatibility: ["Self-host", "Docker"], updatedRecently: true
  },

  // ── Плагины / Расширения ────────────────────────────────────────────────
  {
    slug: "github-copilot", name: "GitHub Copilot", category: "plugins", typeLabel: "IDE", logo: "Co", accent: "neutral",
    short: "Самый распространённый IDE-ассистент (agent mode).",
    description: "AI-ассистент в IDE: автодополнение, чат и агентный режим. Самый массовый инструмент среди разработчиков.",
    tags: ["IDE", "Автодополнение"], rating: 4.4, users: "2.0k", url: "https://github.com/features/copilot",
    compatibility: ["VS Code", "JetBrains"]
  },
  {
    slug: "cline", name: "Cline", category: "plugins", typeLabel: "IDE", logo: "Cl", accent: "sky",
    short: "Открытый IDE-агент (Plan/Act).",
    description: "Open-source агент для VS Code с режимами Plan/Act: планирует изменения, правит файлы, запускает команды.",
    tags: ["IDE", "Агент"], rating: 4.5, users: "910", url: "https://github.com/cline/cline",
    compatibility: ["VS Code"], updatedRecently: true
  },
  {
    slug: "continue", name: "Continue", category: "plugins", typeLabel: "IDE", logo: "Cn", accent: "emerald",
    short: "Открытый IDE-ассистент (агентный).",
    description: "Настраиваемый open-source ассистент для VS Code и JetBrains: чат, автодополнение, кастомные модели и контекст.",
    tags: ["IDE", "Open-source"], rating: 4.3, users: "560", url: "https://github.com/continuedev/continue",
    compatibility: ["VS Code", "JetBrains"]
  },
  {
    slug: "aider", name: "Aider", category: "plugins", typeLabel: "CLI", logo: "Ai", accent: "amber",
    short: "Терминальный AI pair-programmer.",
    description: "Парное программирование в терминале: Aider правит код в git-репозитории, делает коммиты и держит контекст проекта.",
    tags: ["CLI", "Git"], rating: 4.4, users: "780", url: "https://github.com/Aider-AI/aider",
    compatibility: ["CLI", "Git"]
  },

  // ── Вайбкодинг для PAM ──────────────────────────────────────────────────
  {
    slug: "claude-code", name: "Claude Code", category: "pam", typeLabel: "Кодинг", logo: "CC", accent: "lime",
    short: "Агентная CLI, в которой и пишется PAM.",
    description: "Агентная среда разработки от Anthropic: skills, hooks, subagents, code-review. В ней и ведётся разработка PAM.",
    tags: ["Кодинг", "Агент"], rating: 4.8, users: "1.6k", url: "https://docs.claude.com",
    compatibility: ["CLI", "VS Code", "JetBrains"], featured: false, updatedRecently: true
  },
  {
    slug: "pgvector", name: "pgvector", category: "pam", typeLabel: "RAG", logo: "▢", accent: "indigo",
    short: "Векторный поиск в Postgres — фундамент RAG.",
    description: "Расширение Postgres для векторного поиска (HNSW/IVFFlat). В PAM — основа RAG: чанки + эмбеддинги в одной базе.",
    tags: ["Вектор", "Postgres"], rating: 4.6, users: "—", url: "https://github.com/pgvector/pgvector",
    compatibility: ["PostgreSQL"]
  },
  {
    slug: "litellm", name: "LiteLLM", category: "pam", typeLabel: "LLM-шлюз", logo: "Li", accent: "sky",
    short: "Единый шлюз к Groq / Ollama / Claude.",
    description: "Прокси/SDK к 100+ LLM в формате OpenAI. Упростил бы переключение провайдеров в llm.py PAM (Groq ↔ Ollama ↔ OpenRouter).",
    tags: ["Шлюз", "LLM"], rating: 4.4, users: "—", url: "https://github.com/BerriAI/litellm",
    compatibility: ["Python", "Proxy"]
  },
  {
    slug: "ollama", name: "Ollama", category: "pam", typeLabel: "Local", logo: "Ol", accent: "neutral",
    short: "Стандарт локального запуска LLM.",
    description: "Локальный запуск LLM и эмбеддингов с OpenAI-совместимым API. В PAM уже используется для эмбеддингов (nomic-embed-text).",
    tags: ["Локально", "Эмбеддинги"], rating: 4.7, users: "2.1k", url: "https://github.com/ollama/ollama",
    compatibility: ["Local", "Docker"], updatedRecently: true
  }
]

// GitHub-звёзды (приблизительно, на снимок каталога) — для сортировки и
// отображения. 0 = нет публичного репозитория (продукт/хостед-сервис).
export const STARS: Record<string, number> = {
  autogpt: 182000, n8n: 170000, ollama: 164000, langchain: 100000,
  "browser-use": 92000, openhands: 70000, aider: 62000, cline: 61000,
  context7: 56000, crewai: 46000, continue: 38000, "playwright-mcp": 30000,
  "github-mcp": 29000, composio: 15000, litellm: 15000, "figma-context": 14000,
  "google-genai-toolbox": 14000, "vercel-ai-sdk": 14000, pgvector: 13000,
  "chrome-devtools-mcp": 11000, "aws-mcp": 9000, "filesystem-mcp": 8000,
  "postgres-mcp": 8000, firecrawl: 6000, "sentry-mcp": 5000, "slack-mcp": 5000,
  "notion-mcp": 4400, "supabase-mcp": 2700, "exa-search": 1800, "stripe-mcp": 1500,
  "pinecone-mcp": 1200, "openapi-mcp": 900, "elastic-mcp": 700, "deepl-mcp": 600,
  "zapier-mcp": 0, "github-copilot": 0, "claude-code": 0
}

// GitHub-аккаунт для иконки, когда url ведёт не на github.com.
const ICON_ORG: Record<string, string> = {
  n8n: "n8n-io",
  crewai: "crewAIInc",
  "claude-code": "anthropics",
  composio: "ComposioHQ",
  "pinecone-mcp": "pinecone-io",
  "exa-search": "exa-labs"
}

export const starsOf = (t: CatalogTool) => STARS[t.slug] ?? 0
export const byStars = (a: CatalogTool, b: CatalogTool) => starsOf(b) - starsOf(a)

export const fmtStars = (n: number) =>
  n >= 1000 ? (n / 1000).toFixed(n >= 10000 ? 0 : 1).replace(/\.0$/, "") + "k" : String(n)

/** Иконка инструмента: аватар GitHub-аккаунта (если есть репозиторий), иначе null. */
export function iconUrl(t: CatalogTool): string | null {
  const org = ICON_ORG[t.slug] ?? t.url.match(/github\.com\/([^/]+)/)?.[1]
  return org ? `https://github.com/${org}.png?size=80` : null
}

export const getTool = (slug: string) => TOOLS.find((t) => t.slug === slug)
export const featuredTools = () => TOOLS.filter((t) => t.featured)
export const popularMcp = () => TOOLS.filter((t) => t.category === "mcp" && t.popular)
export const toolsList = () => TOOLS.filter((t) => t.category === "tools")
export const newTools = () => TOOLS.filter((t) => t.isNew)
export const updatedTools = () => TOOLS.filter((t) => t.updatedRecently)
export const countByCategory = (cat: CatalogCategory) =>
  TOOLS.filter((t) => t.category === cat).length
