# PAM — запуск/продолжение на втором ПК (handoff нового сеанса)

На втором ПК стоит **этот же** репозиторий (BestStart), но более старая версия.
Память Claude Code между машинами **не синхронизируется** (она в `~/.claude`,
не в git) — поэтому источник правды для нового сеанса: `CLAUDE.md` + `TODO.md` +
этот файл.

---

## 1. Промпт для Claude Code (скопируй целиком и вставь в новый чат на 2-м ПК)

```
Возобновляем работу над проектом PAM (projects/PAM) в репозитории BestStart на
втором ПК — здесь СТАРАЯ версия checkout'а, и память с другой машины сюда не
приехала. Сделай по порядку:

1. В корне репозитория выполни `git pull` (fast-forward). Убедись, что HEAD ==
   origin/main.
2. Прочитай projects/PAM/CLAUDE.md и projects/PAM/TODO.md — это источник правды
   (что за продукт, архитектура, что сделано, что осталось).
3. Проверь разовую настройку машины (git её не переносит):
   - запущен Docker Desktop;
   - есть файл projects/PAM/backend/.env с GROQ_API_KEY (скопируй с первого ПК
     или создай — см. раздел 2 в SECOND_PC_PROMPT.md);
   - (для RAG-памяти) на хосте поднят Ollama с моделью nomic-embed-text;
   - свободны порты 3000 / 8000 / 5432.
4. Из projects/PAM подними стек: `docker compose up -d --build`
   (Dockerfile и compose изменились — пересборка ОБЯЗАТЕЛЬНА). Миграции
   (alembic upgrade head) применяются автоматически при старте backend.
5. Проверь: `docker compose exec backend pytest -q` (ожидается 16 passed);
   открой http://localhost:8000/docs и http://localhost:3000.
6. Кратко отчитайся о состоянии (что готово / что в очереди по TODO.md) и жди
   мою следующую задачу. НЕ начинай Спринт 3 и НЕ удаляй ничего (#9/#11) без
   моего явного «продолжай».
```

---

## 2. Разовая настройка машины (git это не переносит)

- **Docker Desktop** — установлен и запущен.
- **`projects/PAM/backend/.env`** (gitignored — создать вручную). Минимум:
  ```env
  GROQ_API_KEY=<ключ с groq.com>
  # опционально:
  # OPENROUTER_API_KEY=<ключ openrouter.ai>   # для «тяжёлых» запросов (hybrid)
  # LLM_PROVIDER=groq                          # groq | openrouter | ollama | hybrid
  ```
  Проще всего — скопировать `backend/.env` с первого ПК (там уже есть ключи).
  `DATABASE_URL` и `OLLAMA_URL` задаются в docker-compose.yml — в .env их не надо.
- **Ollama (для эмбеддингов/RAG-памяти)** — на хосте: установить Ollama, затем
  `ollama pull nomic-embed-text`. Backend ходит к нему через
  `host.docker.internal:11434`. Без Ollama чат всё равно отвечает, но БЕЗ
  подмешивания памяти (ретрив вернёт пусто).
- **Порты 3000 / 8000 / 5432** должны быть свободны (их публикует compose).
- **(опц.) полный риг скиллов/правил Claude Code** — если нужны те же skills и
  глобальные правила: отдельно клонировать `claude-code-skills-1c` и создать
  NTFS-junction'ы в `~/.claude` (см. память env-topology). Для самой разработки
  PAM это не обязательно.

---

## 3. Поднять стек

```bash
cd projects/PAM
docker compose up -d --build      # app + db + web (+ extension builder)
docker compose logs -f backend    # следить за стартом / alembic
```
- backend: http://localhost:8000 (Swagger `/docs`)
- web: http://localhost:3000
- db (pgvector): localhost:5432, `pam`/`pam`/`pam`

Веб-контейнер при первом старте делает `npm install` (кешируется в volume) —
первый запуск дольше. На Windows правки фронта не всегда подхватываются HMR:
`docker compose restart web`.

---

## 4. Проверка, что всё живо

```bash
docker compose exec backend pytest -q          # ожидается: 16 passed
curl -s http://localhost:8000/openapi.json | findstr /C:"/chat"   # роуты на месте
```
Открой http://localhost:3000 — должна быть страница чата (Glass-стиль: тёмный фон,
мягкий lime-glow по центру, стеклянная панель ввода и кнопка «Новый чат»).

---

## 5. Где мы сейчас (состояние) + что дальше

**Продукт:** локальный «личный AI с памятью» = чат с памятью (RAG + вложения с
AI-распознаванием) + «Лектор» (материалы → курсы с тестом, превью PDF/YouTube,
AI-реформат исходного текста). Полное описание — `projects/PAM/CLAUDE.md`.

**Сделано (в git, последние коммиты):**
- Лектор: читаемый исходный материал + AI-реформат, PDF/YouTube превью, фикс img.
- Чат: Glass-редизайн + вложения (документы→markitdown, изображения→Groq vision).
- Ревью-фиксы (анти-инъекция имени файла, лимиты вложений, idempotent reformat).
- **Спринт 1:** pytest-харнес (16 тестов), дедуп хелперов, обновлён CLAUDE.md.
- **Спринт 2:** контекст-чипы чата реально скоупят ретрив; «Запомнить файл»
  (вложение → ContentSource, доступно поиску и Лектору).

**Очередь — полный бэклог в `projects/PAM/TODO.md`:**
- **Спринт 3 — фичи (`P2`):** #4 true-multimodal ответ по картинке, #7 экспорт
  курса (PDF/MD), #5 превью Word/Excel, #6 фоновый реформат больших документов.
- **Развилки (нужно решение keep/remove):** #9 `normalizers.py` (мёртвый код),
  #11 `extension/contents/gemini.ts` (стаб захвата Gemini).

**Поведенческий нюанс:** в чате чип «Материалы» по умолчанию ВЫКЛ → по умолчанию
подмешиваются только разговоры+профиль, не материалы Лектора. Если надо иначе —
поменять дефолт `use_materials` в `backend/app/routes/chat.py` (ChatIn).

---

## 6. Грабли

- **Windows + Docker bind-mount:** правки фронта → `docker compose restart web`;
  правки кода backend подхватываются `uvicorn --reload`; смена зависимостей
  backend → `docker compose build backend && docker compose up -d backend`.
- **Миграции — только через alembic** (`docker compose exec backend alembic ...`),
  не руками. Применяются автоматически при старте backend.
- **PDF-превью** есть только у PDF, загруженных ПОСЛЕ появления фичи (у старых
  строк нет сохранённых байт) — это норма.
- Если `git pull` ругается на локальные правки — на этой машине не должно быть
  своих изменений; при конфликте звать владельца, не делать форс.
