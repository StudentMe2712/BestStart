# AI Ecosystem Catalog — MCP, Skills, Connectors, Plugins, Agents & Tools (mid‑2026)

> **Scope & method.** Compiled from four parallel research passes across GitHub, Awesome lists, official MCP registries (modelcontextprotocol.io, mcp.so, smithery.ai, glama.ai, mcpmarket.com), the Anthropic & OpenAI ecosystems, Cursor/Windsurf/VS Code marketplaces, Hugging Face, Product Hunt, Reddit and AI‑tool directories. Data is de‑duplicated across sources.
>
> **Caveats.** Star/install counts are **approximate** (marked `~`) and **as‑of ≈ May–June 2026**; they vary by registry and shift weekly. Rankings blend GitHub stars with real‑world adoption/installs (these disagree — e.g. Context7 has more stars, Playwright/GitHub more installs). Vendor‑hosted "remote" servers often have no public repo (shown `— (vendor)`). A few figures are agent‑reported and **not independently verified** (flagged `*`). This is a curated snapshot, not an exhaustive live scrape.

---

## 1. Hierarchical Category Tree

```
AI Ecosystem
├── MCP (Model Context Protocol)
│   ├── Servers
│   │   ├── Databases · Filesystems · APIs · Cloud · Development
│   │   ├── Knowledge Bases · Search · Browser Automation · Monitoring · Communication
│   └── Clients (Claude Desktop/Code, Cursor, Windsurf, VS Code, Cline, Continue, Zed, Goose, LibreChat…)
├── Skills (Coding · Data Analysis · Writing · Research · DevOps · Security · Finance · Marketing · Education)
├── Commands (Development · Agent Management · Context Management · Workflow Automation)
├── Agents (frameworks & autonomous agents)
├── AI SDKs (app/agent-building libraries)
├── Prompt Libraries
├── Connectors / Integrations
│   ├── CRM · ERP · Databases · Cloud Storage
│   ├── Messaging · Email · Project Management · Analytics
│   └── Aggregator gateways (Zapier · Composio · Pipedream · n8n · Make · Merge)
├── Plugins (Productivity · Coding · Search · Automation · Knowledge Management)
├── Extensions (IDE assistants/agents · Browser sidebars/automation)
├── Tools / Open-Source AI Utilities (local runners · inference engines · gateways · app builders · chat UIs)
└── Automation Packs / Workflows (n8n · Zapier · Make templates)
```

**Ecosystem scale (2026):** the Model Context Protocol is now the connective backbone — ~10,000–17,000 community servers across registries, the official `modelcontextprotocol/servers` repo at ~82k★, ~300 new servers/month, and ~97M monthly SDK downloads. MCP was donated to the **Agentic AI Foundation (Linux Foundation)** with Block, OpenAI, AWS, Google and Microsoft as founding members. GitHub Octoverse 2025 counted 4.3M+ AI repos (+178% YoY in LLM projects).

---

## 2. MCP — Model Context Protocol

### 2.1 Servers by category

**Databases**

| Name | Description | Repo/URL | OSS/Prop | Free/Paid | Stars (~) | Platforms |
|---|---|---|---|---|---|---|
| PostgreSQL (reference) | Read‑only Postgres + schema introspection | [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers) | OSS | Free | repo ~82k | All clients |
| Supabase | DB/auth/edge‑functions project mgmt | [supabase-community/supabase-mcp](https://github.com/supabase-community/supabase-mcp) | OSS (official‑ish) | Free | ~2.7k | Cursor, Claude, VS Code |
| MySQL | MySQL with access controls | [benborla/mcp-server-mysql](https://github.com/benborla/mcp-server-mysql) | OSS | Free | ~1.8k | Node clients |
| MongoDB (official) | Atlas mgmt + queries | [mongodb-js/mongodb-mcp-server](https://github.com/mongodb-js/mongodb-mcp-server) | Official | Free | ~1k | Any |
| ClickHouse | Analytics queries + schema | [ClickHouse/mcp-clickhouse](https://github.com/ClickHouse/mcp-clickhouse) | Official | Free | ~0.8k | Any |
| Qdrant | Vector search / semantic memory | [qdrant/mcp-server-qdrant](https://github.com/qdrant/mcp-server-qdrant) | Official | Free | ~0.8k | Any |
| Neon | Serverless Postgres branches | [neondatabase-labs/mcp-server-neon](https://github.com/neondatabase-labs/mcp-server-neon) | Official | Free | ~0.6k | Any |
| Redis | KV / search / vector | [redis/mcp-redis](https://github.com/redis/mcp-redis) | Official | Free | ~0.5k | Any |
| Chroma | Vector DB for RAG | [chroma-core/chroma-mcp](https://github.com/chroma-core/chroma-mcp) | Official | Free | ~0.4k | Any |
| Google GenAI Toolbox | Multi‑DB (AlloyDB/BigQuery/Spanner/PG) | [googleapis/genai-toolbox](https://github.com/googleapis/genai-toolbox) | Official | Free | ~13.9k | Any |
| MindsDB | Federated query + predictive layer (most‑starred server) | [mindsdb/mindsdb](https://github.com/mindsdb/mindsdb) | OSS | Free + Cloud | ~39k | Any |
| DBHub | Universal multi‑DB gateway | [bytebase/dbhub](https://github.com/bytebase/dbhub) | OSS | Free | ~1k | Any |
| Snowflake | Cortex AI + SQL orchestration | [Snowflake-Labs/mcp](https://github.com/Snowflake-Labs/mcp) | OSS | Free (acct) | vendor | Claude, Cursor |
| BigQuery | Managed remote MCP | [Google Cloud](https://cloud.google.com/blog/products/data-analytics/using-the-fully-managed-remote-bigquery-mcp-server-to-build-data-ai-agents) | Proprietary | Paid (GCP) | vendor | Claude, ChatGPT |

**Filesystems:** Filesystem (reference, secure local file ops, ~335k installs) · Google Drive (archived ref) · cloud‑file connectors (Box/Dropbox remote MCP). Repo: [servers/src/filesystem](https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem).

**APIs**

| Name | Description | Repo/URL | OSS/Prop | Stars (~) |
|---|---|---|---|---|
| GitHub | Repos/issues/PRs/Actions (51 tools); default on GitHub.com | [github/github-mcp-server](https://github.com/github/github-mcp-server) | Official | ~28.6k |
| Stripe | Payments/customers/invoices | [stripe/agent-toolkit](https://github.com/stripe/agent-toolkit) | Official | ~1.5k |
| Atlassian (community) | Jira + Confluence, 51 tools | [sooperset/mcp-atlassian](https://github.com/sooperset/mcp-atlassian) | OSS | ~4.8k |
| Cloudflare API | DNS/Workers/account | [cloudflare/mcp](https://github.com/cloudflare/mcp) | Official | ~3k |
| Apify | 5,000+ scrapers/actors as tools | [apify/apify-mcp-server](https://github.com/apify/apify-mcp-server) | Official | ~0.5k |
| PayPal | Orders/invoicing | [paypal/agent-toolkit](https://github.com/paypal/agent-toolkit) | Official | ~0.4k |
| Zapier | 8,000+ apps over MCP | [zapier.com/mcp](https://zapier.com/mcp) | Proprietary | — |

**Cloud:** AWS MCP suite (~8.7k, [awslabs/mcp](https://github.com/awslabs/mcp)) · Cloudflare remote suite (13+ servers, ~3k) · Azure MCP (~1.5k, [Azure/azure-mcp](https://github.com/Azure/azure-mcp)) · Kubernetes (~1k, [containers/kubernetes-mcp-server](https://github.com/containers/kubernetes-mcp-server)) · Terraform (~1k, [hashicorp/terraform-mcp-server](https://github.com/hashicorp/terraform-mcp-server)) · GCP GenAI Toolbox (~13.9k).

**Development:** Git (reference) · Figma Context (~14.2k, [GLips/Figma-Context-MCP](https://github.com/GLips/Figma-Context-MCP)) · Unity MCP (~8.1k) · Sequential Thinking (reference reasoning) · Sentry (~5k, error tracking + Seer RCA) · Linear · GitLab.

**Knowledge Bases:** Context7 (~56.3k, live version‑specific docs, [upstash/context7](https://github.com/upstash/context7)) · Notion (~2k+, [makenotion/notion-mcp-server](https://github.com/makenotion/notion-mcp-server)) · Memory (reference KG) · Mem0 / Basic Memory (agent memory) · Obsidian · Graphlit (multi‑source RAG ingest).

**Search:** Firecrawl (~6k, crawl + URL→markdown) · Exa (~2k, neural search) · Perplexity Sonar (~2k) · Tavily (~1k, RAG search) · Omnisearch (~1k, unified) · Brave Search · Kagi · Fetch (reference).

**Browser Automation:** Playwright MCP (~30.4k, [microsoft/playwright-mcp](https://github.com/microsoft/playwright-mcp)) · Chrome DevTools MCP (~11.1k) · ExecuteAutomation Playwright (~4k) · Browserbase/Stagehand (~3k) · Puppeteer (archived).

**Monitoring:** Grafana (~3.1k, 40+ tools) · Sentry (~5k) · Datadog (~0.5k) · Prometheus (~0.5k) · Cloudflare Workers Logs.

**Communication:** Slack (official remote, OAuth, Feb 2026; community korotovsky ~2k) · Discord (SaseQ ~0.2k, 65 tools) · Gmail/Workspace (~1k) · Telegram · Linear.

### 2.2 MCP Clients

| Name | Description | OSS/Prop | Stars (~) | Platforms |
|---|---|---|---|---|
| Claude Desktop / Claude Code | Anthropic's original MCP hosts | Proprietary | — | mac/Win/Linux |
| Cursor | AI‑first IDE, polished MCP UI | Proprietary | — | cross‑platform |
| Windsurf | Agentic IDE (Cascade, parallel agents) | Proprietary | — | cross‑platform |
| VS Code + Copilot | MCP via Copilot Agent mode | Proprietary (OSS core) | — | cross‑platform |
| Cline | OSS autonomous coding agent | OSS | ~61k | VS Code |
| Continue | OSS IDE assistant | OSS | ~37.6k | VS Code, JetBrains |
| Zed | High‑perf editor w/ MCP | OSS | ~60k | mac/Linux/Win |
| Goose | Block's local multi‑agent workspace | OSS | ~46.4k | cross‑platform |
| LibreChat | Self‑hosted multi‑provider chat UI | OSS | ~46k | web/self‑host |
| Cherry Studio | Desktop multi‑provider chat | OSS | ~25k | cross‑platform |
| Roo Code / Kilo Code | Cline forks, agentic coding | OSS | ~22k (Roo) | VS Code |

### 2.3 Top 50 MCP (by popularity / stars / adoption)

1. **Playwright MCP** (~30.4k) — browser automation; #1 by adoption
2. **GitHub MCP** (~28.6k) — official repo/issue/PR/CI; default on GitHub.com
3. **Context7** (~56.3k) — live version‑specific docs; highest raw stars
4. **Figma Context MCP** (~14.2k) — design→code context
5. **Google GenAI Toolbox** (~13.9k) — multi‑DB server
6. **Chrome DevTools MCP** (~11.1k) — real‑Chrome debugging
7. **AWS MCP servers** (~8.7k) — official AWS suite
8. **Unity MCP** (~8.1k) — drive Unity editor
9. **Firecrawl** (~6k) — crawl + extract
10. **Sentry MCP** (~5k) — errors + Seer RCA
11. **Atlassian/Jira (community)** (~4.8k) — Jira+Confluence
12. **ExecuteAutomation Playwright** (~4k) — browser+API
13. **Grafana** (~3.1k) — dashboards/incidents
14. **Cloudflare** (~3k) — edge remote suite
15. **Browserbase/Stagehand** (~3k) — cloud browsers
16. **Supabase** (~2.7k) — DB/auth/edge fns
17. **Exa** (~2k) — neural search
18. **Slack (korotovsky)** (~2k) — smart‑history Slack
19. **Notion (official)** (~2k+) — docs/DBs
20. **Perplexity** (~2k) — Sonar answers
21. **MySQL** (~1.8k) · 22. **Stripe Agent Toolkit** (~1.5k) · 23. **Azure MCP** (~1.5k)
24. **Kubernetes** (~1k) · 25. **Terraform** (~1k) · 26. **DBHub** (~1k) · 27. **Tavily** (~1k) · 28. **Mem0** (~1k) · 29. **Basic Memory** (~1k) · 30. **Gmail** (~1k) · 31. **MongoDB (official)** (~1k) · 32. **Omnisearch** (~1k)
33. **Qdrant** (~0.8k) · 34. **ClickHouse** (~0.8k) · 35. **Neon** (~0.6k) · 36. **Redis** (~0.5k) · 37. **Apify** (~0.5k) · 38. **AKS/Azure K8s** (~0.5k) · 39. **Prometheus** (~0.5k) · 40. **Datadog** (~0.5k) · 41. **Atlassian (official remote)** (~0.5k) · 42. **Chroma** (~0.4k) · 43. **PayPal** (~0.4k) · 44. **Graphlit** (~0.4k) · 45. **MongoDB (kiliczsh)** (~0.3k) · 46. **Kagi** (~0.3k) · 47. **Obsidian** (~0.3k) · 48. **Discord (SaseQ)** (~0.2k) · 49. **Linear** (~0.14k)
50. **Reference cluster** — Filesystem / Fetch / Git / Memory / Sequential Thinking (bundled in `modelcontextprotocol/servers` ~82k; most‑*installed*, Filesystem alone ~335k)

### 2.4 Fast‑growing MCP (2025–2026)
Context7, Playwright MCP, Chrome DevTools MCP, Google GenAI Toolbox, Unity MCP, Cloudflare remote suite (~4× since May'25), Slack official (Feb'26, enterprise uptake), Sentry, Firecrawl, Apify Actors, Mem0/Basic Memory (agent‑memory category), Supabase.

---

## 3. Skills · Commands · Agents · SDKs · Prompt Libraries

### 3.1 Skills (by domain)

| Skill | Domain | Repo/URL | Stars (~) |
|---|---|---|---|
| anthropics/skills (docx, xlsx, pptx, pdf, artifacts-builder, mcp-builder, canvas-design, webapp-testing) | Coding/Writing/Data | [anthropics/skills](https://github.com/anthropics/skills) | ~146k |
| wshobson/agents (192 agents / 156 skills / 102 commands, multi‑harness) | Coding/All | [wshobson/agents](https://github.com/wshobson/agents) | ~14k |
| VoltAgent/awesome-claude-code-subagents (154+ subagents) | Coding/DevOps/Security | [VoltAgent/…subagents](https://github.com/VoltAgent/awesome-claude-code-subagents) | ~6k |
| GPT Actions (OpenAPI tool calling) | Coding | [platform.openai.com](https://platform.openai.com) | — |
| Code Interpreter / Advanced Data Analysis | Data Analysis | [openai.com](https://openai.com) | — |
| content-research-writer (Composio) | Writing | [ComposioHQ/awesome-claude-skills](https://github.com/ComposioHQ/awesome-claude-skills) | list |
| ChatGPT Deep Research | Research | [openai.com](https://openai.com) | — |
| Fabric security patterns (STRIDE, threat reports) | Security | [danielmiessler/fabric](https://github.com/danielmiessler/fabric) | ~30k |
| anthropics/financial-services | Finance | [anthropics/financial-services](https://github.com/anthropics/financial-services) | — |
| anthropics/prompt-eng-interactive-tutorial | Education | […tutorial](https://github.com/anthropics/prompt-eng-interactive-tutorial) | — |
| dair-ai Prompt Engineering Guide | Education | [dair-ai/Prompt-Engineering-Guide](https://github.com/dair-ai/Prompt-Engineering-Guide) | ~74k |

### 3.2 Commands (slash / CLI agent commands)

| Pack / Command | Group | Repo | Notes |
|---|---|---|---|
| Claude Code built‑ins: `/commit` `/review` `/test` `/security-review` | Development | [docs.claude.com](https://docs.claude.com) | first‑party |
| qdhenry/Claude-Command-Suite (216+ commands, 54 agents) | Dev/Workflow | [qdhenry/Claude-Command-Suite](https://github.com/qdhenry/Claude-Command-Suite) | `/orchestration` `/session:handoff` `/semantic` `/memory` `/simulation` `/boundary` `/reasoning` |
| SuperClaude_Framework (19+ commands, 9 personas) | Agent Management | [SuperClaude-Org/SuperClaude_Framework](https://github.com/SuperClaude-Org/SuperClaude_Framework) | behavioral injection |
| davila7/claude-code-templates (1000+ agents/commands/skills/MCP CLI) | All | [davila7/claude-code-templates](https://github.com/davila7/claude-code-templates) | config + monitoring |
| centminmod/my-claude-code-setup (CLAUDE.md memory‑bank) | Context Mgmt | [centminmod/my-claude-code-setup](https://github.com/centminmod/my-claude-code-setup) | starter template |
| Fabric CLI patterns (200+ reusable patterns) | Workflow | [danielmiessler/fabric](https://github.com/danielmiessler/fabric) | ~30k, model‑agnostic |

### 3.3 Agents (frameworks & autonomous agents)

| Name | Description | Repo | Stars (~) |
|---|---|---|---|
| AutoGPT | Visual agent builder + marketplace | [Significant-Gravitas/AutoGPT](https://github.com/Significant-Gravitas/AutoGPT) | ~182k |
| browser-use | LLM‑driven browser automation | [browser-use/browser-use](https://github.com/browser-use/browser-use) | ~92k |
| OpenHands | OSS autonomous SWE (Devin‑style) | [All-Hands-AI/OpenHands](https://github.com/All-Hands-AI/OpenHands) | ~70k |
| MetaGPT | Multi‑role "AI software company" | [FoundationAgents/MetaGPT](https://github.com/FoundationAgents/MetaGPT) | ~50k |
| AutoGen / AG2 | Microsoft event‑driven multi‑agent | [microsoft/autogen](https://github.com/microsoft/autogen) | ~54k |
| CrewAI | Role‑based agent crews | [crewai.com](https://www.crewai.com) | ~46k |
| Agno (ex‑Phidata) | Multi‑agent runtime + control plane | [agno-agi/agno](https://github.com/agno-agi/agno) | ~39k |
| LangGraph | Graph‑based stateful orchestration | [langchain-ai/langgraph](https://github.com/langchain-ai/langgraph) | ~25k |
| Semantic Kernel / MS Agent Framework | Enterprise orchestration | [microsoft/semantic-kernel](https://github.com/microsoft/semantic-kernel) | ~28k |
| OpenAI Agents SDK | Lightweight multi‑agent (ex‑Swarm) | [openai/openai-agents-python](https://github.com/openai/openai-agents-python) | ~26k |
| smolagents (HF) | Code‑first agents | [huggingface/smolagents](https://github.com/huggingface/smolagents) | ~25k |
| Google ADK | Hierarchical agents + A2A | [google/adk-python](https://github.com/google/adk-python) | ~17k |
| Pydantic AI | Type‑safe Python agents | [ai.pydantic.dev](https://ai.pydantic.dev/) | ~16.5k |
| Devin (Cognition) | Proprietary autonomous SWE | [cognition.ai](https://cognition.ai) | — |
| OpenAI AgentKit | Visual Agent Builder + ChatKit | [openai.com/agentkit](https://openai.com/index/introducing-agentkit/) | — |

### 3.4 AI SDKs
Vercel AI SDK (~21k, 20M+ mo. downloads, v6 Agent class) · OpenAI Agents SDK (~26k) · Anthropic SDK / Agent SDK (caching, thinking, tools) · LangChain (~90k+) · LlamaIndex (~36–47k) · Mastra (~22k, TS, v1.0 Jan'26) · Pydantic AI (~16.5k) · Google ADK (~17k) · Semantic Kernel (~28k).

### 3.5 Prompt Libraries
Awesome ChatGPT Prompts / prompts.chat (~143k) · dair‑ai Prompt Engineering Guide (~74k) · Fabric (~30k) · Anthropic Prompt Library (60+ XML templates) · promptingguide.ai · ai-boost/awesome-prompts · anthropics/prompt-eng-interactive-tutorial.

### 3.6 Top 50 Skills (by popularity / adoption)

1. anthropics/skills (~146k) · 2. Awesome ChatGPT Prompts (~143k) · 3. dair‑ai PE Guide (~74k) · 4. Fabric patterns (~30k) · 5. wshobson/agents (~14k) · 6. VoltAgent subagents (~6k) · 7. qdhenry/Claude-Command-Suite · 8. SuperClaude_Framework · 9. davila7/claude-code-templates · 10. artifacts‑builder (Anthropic)
11. docx · 12. xlsx · 13. pdf · 14. pptx · 15. canvas‑design · 16. mcp‑builder · 17. webapp‑testing · 18. code‑reviewer subagent · 19. security‑engineer subagent · 20. devops‑engineer subagent
21. `/commit` · 22. `/review` · 23. `/test` · 24. `/security-review` · 25. content‑research‑writer · 26. Septim Agents Pack (Composio) · 27. GPT Actions · 28. Code Interpreter/ADA · 29. ChatGPT Deep Research · 30. travisvn/awesome-claude-skills
31. ComposioHQ/awesome-claude-skills · 32. anthropics/financial-services · 33. `/session:handoff` · 34. `/semantic` + `/memory` · 35. `/orchestration` · 36. `/simulation` · 37. `/boundary` + `/reasoning` · 38. Fabric security patterns · 39. centminmod memory‑bank · 40. rahulvrane/awesome-claude-agents
41. hesreallyhim/awesome-claude-code · 42. hesreallyhim/a-list-of-claude-code-agents · 43. claude-api skill · 44. prompt-eng-interactive-tutorial · 45. Anthropic Prompt Library · 46. ai-boost/awesome-prompts · 47. Live Artifacts skill · 48. data‑engineer/ml subagents · 49. Septim "Tally" finance · 50. Septim "Ember" marketing

---

## 4. Connectors / Integrations

> Most connectors are now delivered **as MCP servers**. Each app is listed once, noting supporting hosts.

| Domain | Notable connectors |
|---|---|
| **CRM** | Salesforce/Agentforce 360, HubSpot (Claude built‑in), Pipedrive |
| **ERP** | NetSuite (Oracle official MCP), SAP (via Composio/Merge), Workday |
| **Databases** | Postgres, Supabase (~2.7k), MySQL/MariaDB/SQL Server/SQLite (DBHub), MongoDB, Snowflake, BigQuery, MindsDB (~39k) |
| **Cloud Storage** | Google Drive, SharePoint/OneDrive, Dropbox, Box, AWS S3 |
| **Messaging** | Slack, Microsoft Teams, Discord, Telegram |
| **Email** | Gmail, Microsoft Outlook |
| **Project Mgmt** | Jira/Confluence, Linear, Asana, ClickUp (~49 tools), Notion (~4.4k), GitHub Issues, Airtable, Wrike/Smartsheet/Shortcut/Plane |
| **Analytics** | GA4, Mixpanel, Amplitude, PostHog, Pendo, FullStory, Adobe Analytics, Statsig |
| **Dev/Infra** | Playwright (~30k), Stripe (~1.4k), Figma, Sentry, Cloudflare |
| **Aggregators** | Zapier (9k apps), Composio (1000+ tools, OSS core), Pipedream (~11k, 2,800 APIs), n8n (1,200 nodes), Make, Merge.dev, Paragon |

### 4.1 Top 50 Connectors (by adoption)
1. GitHub MCP · 2. Slack · 3. Playwright (~30k) · 4. Google Drive · 5. Notion (~4.4k) · 6. Postgres · 7. MindsDB (~39k) · 8. Jira/Confluence · 9. Salesforce · 10. HubSpot
11. Gmail · 12. Supabase (~2.7k) · 13. Linear · 14. Microsoft Teams · 15. SharePoint/OneDrive · 16. Snowflake · 17. Stripe (~1.4k) · 18. Figma · 19. Outlook · 20. Asana
21. ClickUp · 22. AWS S3 · 23. BigQuery · 24. Sentry · 25. Cloudflare · 26. Airtable · 27. Discord · 28. Telegram · 29. Dropbox · 30. Google Sheets
31. MongoDB · 32. MySQL/MariaDB (DBHub) · 33. GA4 · 34. Amplitude · 35. Mixpanel · 36. Box · 37. NetSuite · 38. Pipedrive · 39. PostHog · 40. Wrike
41. Smartsheet · 42. Shortcut · 43. Plane · 44. SQL Server · 45. SQLite · 46. Adobe Analytics · 47. Pendo/FullStory · 48. Contentsquare (Heap/Hotjar) · 49. SAP · 50. Statsig/LogRocket

---

## 5. Plugins · Extensions · Tools · Automation

### 5.1 Plugins (Productivity · Coding · Search · Automation · Knowledge Mgmt)
Smart Connections (~786k DL, Obsidian) · Text Generator (~1.2M DL, Obsidian) · Obsidian Copilot · Notion AI · Raycast AI / AI Extensions · GPT Store custom GPTs (~159k public: Consensus, Code Copilot, Canva, Data Analyst…).

### 5.2 Extensions (IDE assistants/agents · Browser)

| Name | Type | Repo/URL | Stars/Installs (~) |
|---|---|---|---|
| GitHub Copilot | IDE assistant (agent mode) | [github.com/features/copilot](https://github.com/features/copilot) | ~15M devs |
| Cline | OSS IDE agent (Plan/Act) | [cline/cline](https://github.com/cline/cline) | ~61k★ / ~5M installs |
| Roo Code | Cline fork, multi‑mode | [RooCodeInc/Roo-Code](https://github.com/RooCodeInc/Roo-Code) | large |
| Continue | OSS IDE assistant (agentic) | [continuedev/continue](https://github.com/continuedev/continue) | ~37.6k |
| Codeium / Windsurf ext. | Free completions (OpenAI‑owned) | [windsurf.com](https://windsurf.com/) | millions |
| Tabnine | Privacy/enterprise assistant | [tabnine.com](https://www.tabnine.com/) | ~9M installs |
| JetBrains AI + Junie | Native JetBrains agent | [jetbrains.com/ai](https://www.jetbrains.com/ai/) | bundled |
| Kilo Code | OSS IDE agent | [Kilo-Org/kilocode](https://github.com/Kilo-Org/kilocode) | growing |
| Augment Code | Large‑codebase context | [augmentcode.com](https://www.augmentcode.com/) | growing |
| Sider AI | Browser sidebar (best overall) | [sider.ai](https://sider.ai/) | ~2M+ users |
| Monica | Sidebar + image/video gen | [monica.im](https://monica.im/) | millions |
| Merlin | Sidebar + web search | [getmerlin.in](https://www.getmerlin.in/) | millions |
| HARPA AI | Browser automation workflows | [harpa.ai](https://harpa.ai/) | large |

### 5.3 Tools / Open‑Source AI Utilities

| Name | Type | Repo | Stars (~) |
|---|---|---|---|
| Ollama | Local LLM runner | [ollama/ollama](https://github.com/ollama/ollama) | ~164k |
| Open WebUI | Self‑host chat UI | [open-webui/open-webui](https://github.com/open-webui/open-webui) | ~136k |
| Langflow | Visual agent/RAG builder | [langflow-ai/langflow](https://github.com/langflow-ai/langflow) | ~146k |
| Dify | LLM app/agent platform | [langgenius/dify](https://github.com/langgenius/dify) | ~138k |
| n8n | AI‑native automation | [n8n-io/n8n](https://github.com/n8n-io/n8n) | ~170k |
| GPT4All | Local runner | [nomic-ai/gpt4all](https://github.com/nomic-ai/gpt4all) | ~72k |
| Aider | CLI pair programmer | [Aider-AI/aider](https://github.com/Aider-AI/aider) | ~62k |
| OpenCode | OSS terminal agent | [sst/opencode](https://github.com/sst/opencode) | ~95k |
| Flowise | Visual LLM flow builder | [FlowiseAI/Flowise](https://github.com/FlowiseAI/Flowise) | ~51k |
| LocalAI | Multimodal inference engine | [mudler/LocalAI](https://github.com/mudler/LocalAI) | ~27k |
| vLLM | High‑throughput serving | [vllm-project/vllm](https://github.com/vllm-project/vllm) | very large |
| LiteLLM | 100‑API gateway/SDK | [BerriAI/litellm](https://github.com/BerriAI/litellm) | large |
| AnythingLLM | RAG + agents + MCP | [Mintplex-Labs/anything-llm](https://github.com/Mintplex-Labs/anything-llm) | large |
| Jan | Offline desktop chat | [menloresearch/jan](https://github.com/menloresearch/jan) | large |
| LM Studio | Local‑model desktop GUI | [lmstudio.ai](https://lmstudio.ai/) | — |

### 5.4 Automation Packs / Workflows
n8n AI workflow templates (~6,898 AI templates) · n8n AI Agent nodes 2.0 (70+ AI nodes, LangChain) · Zapier Agents (8,000+ apps) · Make + Maia (NL→scenario) · community "awesome n8n templates".

### 5.5 Top 50 Plugins / Extensions (by installs / popularity)
1. GitHub Copilot (~15M) · 2. Cline (~5M) · 3. Tabnine (~9M) · 4. Codeium/Windsurf · 5. Roo Code · 6. Continue · 7. Sider AI (~2M) · 8. Monica · 9. Merlin · 10. HARPA AI
11. Smart Connections (~786k) · 12. Text Generator (~1.2M) · 13. Obsidian Copilot · 14. Notion AI · 15. Raycast AI · 16. JetBrains AI Assistant · 17. Junie · 18. Kilo Code · 19. Augment Code · 20. Sourcegraph Cody
21. Consensus GPT · 22. Code Copilot GPT · 23. Canva GPT · 24. Data Analyst GPT · 25. Hemingway GPT · 26. Creative Writing Coach GPT · 27. AI Image Generator GPT · 28. Aider (~62k) · 29. OpenCode (~95k) · 30. Claude Code (~113k)
31. AnythingLLM · 32. Open WebUI (~136k) · 33. Perplexity ext. · 34. ChatGPT Sidebar · 35. Grammarly AI · 36. DeepL Write · 37. Wiseone · 38. MaxAI.me · 39. Compose AI · 40. Glasp
41. ChatHub · 42. Typefully AI · 43. Fireflies/Otter · 44. Loom AI · 45. Notion Web Clipper · 46. Raycast Notion ext. · 47. Raycast Obsidian ext. · 48. Superhuman AI · 49. CodeRabbit · 50. Cursor Tab companions

---

## 6. Top 100 Overall (cross‑category, blended popularity)

> Blended ranking of the whole ecosystem by stars **and** adoption. `*` = agent‑reported figure, not independently verified; treat with caution.

1. OpenClaw ~302k\* (local personal‑AI gateway) · 2. AutoGPT ~182k · 3. n8n ~170k · 4. Ollama ~164k · 5. Langflow ~146k · 6. anthropics/skills ~146k · 7. awesome‑chatgpt‑prompts ~143k · 8. Dify ~138k · 9. Open WebUI ~136k · 10. Claude Code ~113k
11. OpenCode ~95k · 12. browser‑use ~92k · 13. LangChain ~90k · 14. dair‑ai PE Guide ~74k · 15. GPT4All ~72k · 16. OpenHands ~70k · 17. Aider ~62k · 18. Cline ~61k · 19. Zed ~60k · 20. Context7 ~56k
21. AutoGen/AG2 ~54k · 22. Flowise ~51k · 23. MetaGPT ~50k · 24. LlamaIndex ~47k · 25. Goose ~46k · 26. LibreChat ~46k · 27. CrewAI ~46k · 28. Agno ~39k · 29. MindsDB ~39k · 30. Continue ~37k
31. Playwright MCP ~30k · 32. Fabric ~30k · 33. GitHub MCP ~28.6k · 34. Semantic Kernel ~28k · 35. LocalAI ~27k · 36. OpenAI Agents SDK ~26k · 37. LangGraph ~25k · 38. smolagents ~25k · 39. Cherry Studio ~25k · 40. Mastra ~22k
41. Roo Code ~22k · 42. Vercel AI SDK ~21k · 43. Google ADK ~17k · 44. Pydantic AI ~16.5k · 45. Figma Context MCP ~14.2k · 46. wshobson/agents ~14k · 47. Google GenAI Toolbox ~13.9k · 48. Chrome DevTools MCP ~11.1k · 49. AWS MCP ~8.7k · 50. Unity MCP ~8.1k
51. GitHub Copilot (~15M devs) · 52. Tabnine (~9M) · 53. Sider AI (~2M) · 54. VoltAgent subagents ~6k · 55. Firecrawl ~6k · 56. Sentry MCP ~5k · 57. Atlassian/Jira MCP ~4.8k · 58. Notion MCP ~4.4k · 59. Grafana MCP ~3.1k · 60. Cloudflare MCP ~3k
61. Browserbase/Stagehand ~3k · 62. Supabase MCP ~2.7k · 63. Exa ~2k · 64. Slack MCP ~2k · 65. Perplexity MCP ~2k · 66. MySQL MCP ~1.8k · 67. Stripe Toolkit ~1.5k · 68. Azure MCP ~1.5k · 69. Cursor (client) · 70. Windsurf (client)
71. VS Code + Copilot (client) · 72. Claude Desktop (client) · 73. ChatGPT / GPT Store (~159k GPTs) · 74. Zapier MCP (9k apps) · 75. Composio (1000+ tools) · 76. Pipedream ~11k · 77. Make.com · 78. Kubernetes MCP ~1k · 79. Terraform MCP ~1k · 80. DBHub ~1k
81. Tavily MCP ~1k · 82. Mem0 ~1k · 83. Smart Connections (~786k DL) · 84. Text Generator (~1.2M DL) · 85. Notion AI · 86. Raycast AI · 87. JetBrains AI / Junie · 88. Kilo Code · 89. Qdrant MCP ~0.8k · 90. ClickHouse MCP ~0.8k
91. Neon MCP ~0.6k · 92. AnythingLLM · 93. Jan · 94. LM Studio · 95. vLLM · 96. LiteLLM · 97. Monica · 98. Merlin · 99. CodeRabbit · 100. Perplexity Comet (agentic browser)

---

## 7. Trends 2025–2026

- **MCP standardization is the backbone** — ~97M monthly SDK downloads, 10,000+ active servers, hundreds of clients; donated to the **Agentic AI Foundation (Linux Foundation)** (Block, OpenAI, AWS, Google, Microsoft founding members).
- **Agentic IDEs win** — shift from autocomplete to autonomous **Plan/Act** & multi‑mode agents (Cline, Roo Code, Junie, Copilot Agent Mode). IDE integration is MCP's fastest adoption vector.
- **Skills & MCP Apps as first‑class interfaces** — reusable "skills" and interactive MCP Apps are being absorbed into the standard.
- **Local‑first & private agents surging** — Ollama (~164k), Open WebUI (~136k), and breakout personal‑agent gateways reflect demand for on‑device, multi‑channel agents.
- **Computer‑use / browser agents mainstream** — Perplexity **Comet** + "Computer" super‑agent automate booking/email/datasets; browser‑use (~92k) as a library.
- **Async / long‑running agents** — MCP upgraded for asynchronous tasks (kick off long jobs, retrieve later).
- **Enterprise hardening** — SSO‑integrated MCP, audit trails, gateway/proxy patterns; rising concern over the expanded attack surface.
- **Automation platforms go AI‑native** — n8n 2.0 (LangChain, 70+ AI nodes, MCP), Zapier Agents (8,000+ apps), Make's Maia.
- **Consolidation** — Cognition→Codeium→Windsurf, then OpenAI acquired Windsurf (~$3B); Sourcegraph Cody went enterprise‑only.
- **Eval/observability maturing** — LiteLLM gateways, logging/budgets, agent tracing as agents reach production.
- **Explosive growth** — Octoverse 2025: 4.3M+ AI repos, +178% YoY LLM projects.

## 8. Fast‑Growing Projects (notable recent growth)

OpenClaw (~302k\*, flagged) · Langflow (~146k) · Dify (~138k) · Claude Code (~113k) · OpenCode (~95k) · browser‑use (~92k) · Cline (~61k / ~5M installs) · n8n (~170k) · Aider (~62k) · Open WebUI (~136k) · Ollama (~164k) · AnythingLLM · LocalAI (~27k) · Roo Code · Kilo Code · Perplexity Comet · Tabnine (Enterprise Context Engine) · Jan · Smart Connections (~786k DL) · Augment Code. In MCP specifically: Context7, Playwright MCP, Chrome DevTools MCP, Cloudflare remote suite, Slack official, Apify Actors, Mem0/Basic Memory.

---

## Sources (selected)

MCP: [modelcontextprotocol.io](https://modelcontextprotocol.io) · [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers) · [punkpeye/awesome-mcp-servers](https://github.com/punkpeye/awesome-mcp-servers) · [tolkonepiu/best-of-mcp-servers](https://github.com/tolkonepiu/best-of-mcp-servers) · [mcpmarket.com/leaderboards](https://mcpmarket.com/leaderboards) · [glama.ai/mcp](https://glama.ai/mcp) · [smithery.ai](https://smithery.ai).
Skills/Agents: [anthropics/skills](https://github.com/anthropics/skills) · [hesreallyhim/awesome-claude-code](https://github.com/hesreallyhim/awesome-claude-code) · [wshobson/agents](https://github.com/wshobson/agents) · [VoltAgent/awesome-claude-code-subagents](https://github.com/VoltAgent/awesome-claude-code-subagents) · [qdhenry/Claude-Command-Suite](https://github.com/qdhenry/Claude-Command-Suite) · [danielmiessler/fabric](https://github.com/danielmiessler/fabric) · [f/awesome-chatgpt-prompts](https://github.com/f/awesome-chatgpt-prompts).
Connectors: [Claude Connectors](https://claude.com/connectors) · [ChatGPT connectors](https://help.openai.com/en/articles/11487775-connectors-in-chatgpt) · [composio.dev/toolkits](https://composio.dev/toolkits) · [zapier.com/mcp](https://zapier.com/mcp) · [pipedream.com](https://pipedream.com/docs/connect/mcp) · [n8n.io/integrations](https://n8n.io/integrations/) · [merge.dev](https://www.merge.dev/).
Tools/Plugins/Trends: [thenewstack.io trends](https://thenewstack.io/5-key-trends-shaping-agentic-development-in-2026/) · [Anthropic MCP](https://www.anthropic.com/news/model-context-protocol) · [nocobase OSS AI 2026](https://www.nocobase.com/en/blog/best-open-source-ai-projects-github-2026) · [HF State of OS Spring 2026](https://huggingface.co/blog/huggingface/state-of-os-hf-spring-2026) · VS Code Marketplace, Raycast Store, GPT Store directories.

*Compiled mid‑2026 from four parallel research passes. Figures approximate; verify live before relying on exact numbers.*
