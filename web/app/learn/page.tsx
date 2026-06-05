"use client"

import { useEffect, useRef, useState } from "react"

import {
  listSources,
  ingestArticle,
  ingestYoutube,
  isYoutubeUrl,
  uploadFile,
  ingestText,
  deleteSource,
  generateCourse,
  getCourse,
  type ContentSource,
  type Course
} from "../../lib/api"
import { getCache, setCache } from "../../lib/cache"
import RefreshButton from "../refresh-button"

export default function LearnPage() {
  const [sources, setSources] = useState<ContentSource[]>(
    () => getCache<ContentSource[]>("sources") ?? []
  )
  const [loading, setLoading] = useState(
    () => getCache<ContentSource[]>("sources") === undefined
  )
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const [url, setUrl] = useState("")
  const [adding, setAdding] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)
  const [text, setText] = useState("")

  const [selected, setSelected] = useState<ContentSource | null>(null)
  const [course, setCourse] = useState<Course | null>(null)
  const [courseBusy, setCourseBusy] = useState(false)

  useEffect(() => {
    setLoading(true)
    listSources()
      .then((d) => {
        setSources(d)
        setCache("sources", d)
      })
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [tick])

  async function addUrl() {
    const u = url.trim()
    if (!u) return
    setAdding(true)
    setError(null)
    try {
      await (isYoutubeUrl(u) ? ingestYoutube(u) : ingestArticle(u))
      setUrl("")
      setTick((t) => t + 1)
    } catch (e) {
      setError(String(e))
    } finally {
      setAdding(false)
    }
  }

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]
    if (!f) return
    setAdding(true)
    setError(null)
    try {
      await uploadFile(f)
      setTick((t) => t + 1)
    } catch (err) {
      setError(String(err))
    } finally {
      setAdding(false)
      if (fileRef.current) fileRef.current.value = ""
    }
  }

  async function addText() {
    const t = text.trim()
    if (!t) return
    setAdding(true)
    setError(null)
    try {
      await ingestText(t)
      setText("")
      setTick((n) => n + 1)
    } catch (err) {
      setError(String(err))
    } finally {
      setAdding(false)
    }
  }

  async function remove(id: string) {
    try {
      await deleteSource(id)
      if (selected?.id === id) {
        setSelected(null)
        setCourse(null)
      }
      setSources((prev) => prev.filter((s) => s.id !== id))
    } catch (e) {
      setError(String(e))
    }
  }

  async function open(src: ContentSource) {
    setSelected(src)
    setCourse(null)
    setError(null)
    try {
      setCourse(await getCourse(src.id))
    } catch (e) {
      setError(String(e))
    }
  }

  async function generate() {
    if (!selected) return
    setCourseBusy(true)
    setError(null)
    try {
      setCourse(await generateCourse(selected.id))
    } catch (e) {
      setError(String(e))
    } finally {
      setCourseBusy(false)
    }
  }

  return (
    <main className="max-w-5xl mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// лектор
        </div>
        <div className="flex items-center justify-between gap-3">
          <h1 className="text-3xl font-semibold">Личный лектор</h1>
          <RefreshButton onClick={() => setTick((t) => t + 1)} busy={loading} />
        </div>
        <p className="text-neutral-400 mt-2 text-sm font-sans">
          Добавь материал — ссылку на статью или YouTube, файл
          (PDF/DOCX/TXT/MD/HTML) или вставь текст. PAM извлечёт его, добавит в
          память и сможет собрать по нему персональный мини-курс с тестом.
        </p>
      </header>

      {/* Добавление материала */}
      <section className="mb-8 space-y-3">
        <div className="flex gap-2">
          <input
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && addUrl()}
            placeholder="https://… статья или ссылка на YouTube"
            className="flex-1 bg-neutral-900 border border-neutral-800 rounded-md px-3 py-2 text-sm font-sans focus:outline-none focus:border-lime-400/50"
          />
          <button
            onClick={addUrl}
            disabled={adding || !url.trim()}
            className="text-sm px-4 py-2 rounded-md border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
            {adding
              ? "добавляю…"
              : url.trim() && isYoutubeUrl(url)
                ? "добавить видео"
                : "добавить статью"}
          </button>
        </div>
        <div className="flex items-center gap-3 text-sm font-sans text-neutral-400">
          <span>или загрузи файл:</span>
          <input
            ref={fileRef}
            type="file"
            accept=".pdf,.docx,.txt,.md,.markdown,.html,.htm,.csv,.json,.log,.rst"
            onChange={onFile}
            disabled={adding}
            className="text-xs file:mr-3 file:py-1.5 file:px-3 file:rounded-md file:border file:border-neutral-700 file:bg-transparent file:text-neutral-200 file:cursor-pointer hover:file:border-lime-400/50"
          />
          <span className="text-[10px] text-neutral-600">
            PDF · DOCX · TXT · MD · HTML
          </span>
        </div>

        {/* Вставить текст напрямую */}
        <div className="space-y-2">
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="…или вставь текст сюда, чтобы добавить его в память"
            rows={3}
            className="w-full bg-neutral-900 border border-neutral-800 rounded-md px-3 py-2 text-sm font-sans resize-y focus:outline-none focus:border-lime-400/50"
          />
          <div className="flex justify-end">
            <button
              onClick={addText}
              disabled={adding || !text.trim()}
              className="text-sm px-4 py-2 rounded-md border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
              {adding ? "добавляю…" : "добавить текст"}
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="text-red-400 text-sm font-sans mb-4">Ошибка: {error}</div>
      )}

      <div className="grid md:grid-cols-[320px_1fr] gap-6">
        {/* Список материалов */}
        <section>
          <h2 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
            материалы
          </h2>
          {loading && sources.length === 0 ? (
            <div className="text-neutral-500 text-sm py-8">// загрузка…</div>
          ) : sources.length === 0 ? (
            <div className="text-neutral-500 text-sm py-8 border border-dashed border-neutral-800 text-center font-sans">
              Пусто. Добавь первую статью или PDF выше.
            </div>
          ) : (
            <div className="space-y-2">
              {sources.map((s) => (
                <button
                  key={s.id}
                  onClick={() => open(s)}
                  className={`w-full text-left border rounded-sm p-3 transition-colors ${
                    selected?.id === s.id
                      ? "border-lime-400/50 bg-neutral-900"
                      : "border-neutral-800 hover:border-neutral-700"
                  }`}>
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 border border-neutral-700 rounded text-neutral-400">
                      {s.kind}
                    </span>
                    <StatusBadge status={s.status} />
                  </div>
                  <div className="text-sm font-sans truncate">
                    {s.title || s.url || "(без названия)"}
                  </div>
                  <div className="flex items-center justify-between mt-1">
                    <span className="text-[10px] text-neutral-600 tabular-nums">
                      {s.char_count.toLocaleString("ru")} симв.
                    </span>
                    <span
                      role="button"
                      tabIndex={0}
                      onClick={(e) => {
                        e.stopPropagation()
                        remove(s.id)
                      }}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          e.stopPropagation()
                          remove(s.id)
                        }
                      }}
                      className="text-[10px] uppercase tracking-wider text-neutral-500 hover:text-red-400 cursor-pointer">
                      удалить
                    </span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </section>

        {/* Курс */}
        <section>
          {!selected ? (
            <div className="text-neutral-500 text-sm py-12 border border-dashed border-neutral-800 text-center font-sans">
              Выбери материал слева, чтобы открыть или сгенерировать курс.
            </div>
          ) : (
            <CoursePanel
              source={selected}
              course={course}
              busy={courseBusy}
              onGenerate={generate}
            />
          )}
        </section>
      </div>
    </main>
  )
}

function StatusBadge({ status }: { status: ContentSource["status"] }) {
  const map: Record<string, string> = {
    extracted: "text-lime-400 border-lime-400/40",
    pending: "text-yellow-400 border-yellow-400/40",
    failed: "text-red-400 border-red-400/40"
  }
  const label: Record<string, string> = {
    extracted: "готов",
    pending: "обработка",
    failed: "ошибка"
  }
  return (
    <span
      className={`text-[9px] uppercase tracking-wider border rounded px-1 py-px ${
        map[status] || "text-neutral-500 border-neutral-700"
      }`}>
      {label[status] || status}
    </span>
  )
}

function CoursePanel({
  source,
  course,
  busy,
  onGenerate
}: {
  source: ContentSource
  course: Course | null
  busy: boolean
  onGenerate: () => void
}) {
  const canGenerate = source.status === "extracted"
  return (
    <div>
      <div className="flex items-center justify-between gap-3 mb-4">
        <h2 className="text-lg font-semibold truncate">
          {course?.title || source.title || source.url || "Материал"}
        </h2>
        <button
          onClick={onGenerate}
          disabled={busy || !canGenerate}
          title={canGenerate ? "" : "у материала нет извлечённого текста"}
          className="shrink-0 text-sm px-4 py-2 rounded-md border border-neutral-700 text-neutral-200 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
          {busy
            ? "генерирую курс…"
            : course
              ? "перегенерировать"
              : "сгенерировать курс"}
        </button>
      </div>

      {busy && (
        <div className="text-neutral-500 text-sm py-8 font-sans">
          // собираю курс под твой уровень, это может занять несколько секунд…
        </div>
      )}

      {!busy && !course && (
        <div className="text-neutral-500 text-sm py-8 border border-dashed border-neutral-800 text-center font-sans">
          Курса ещё нет. Нажми «сгенерировать курс».
        </div>
      )}

      {!busy && course && <CourseView course={course} />}
    </div>
  )
}

function CourseView({ course }: { course: Course }) {
  const d = course.data
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 flex-wrap">
        {course.level && (
          <span className="text-[10px] uppercase tracking-wider px-2 py-0.5 border border-lime-400/40 text-lime-400 rounded">
            уровень: {course.level}
          </span>
        )}
      </div>
      {d.summary && (
        <p className="text-sm font-sans text-neutral-300">{d.summary}</p>
      )}

      {d.modules?.map((m, mi) => (
        <div key={mi} className="border border-neutral-800 rounded-sm p-4">
          <h3 className="text-sm uppercase tracking-wider text-lime-400 mb-3">
            Модуль {mi + 1}. {m.title}
          </h3>
          <div className="space-y-4">
            {m.lessons?.map((l, li) => (
              <div key={li}>
                <div className="text-sm font-semibold font-sans mb-1">
                  {l.title}
                </div>
                <p className="text-sm font-sans text-neutral-300 whitespace-pre-wrap">
                  {l.content}
                </p>
              </div>
            ))}
          </div>
        </div>
      ))}

      {d.quiz?.length > 0 && <Quiz questions={d.quiz} />}
    </div>
  )
}

function Quiz({ questions }: { questions: Course["data"]["quiz"] }) {
  const [answers, setAnswers] = useState<Record<number, number>>({})
  return (
    <div className="border border-neutral-800 rounded-sm p-4">
      <h3 className="text-sm uppercase tracking-wider text-lime-400 mb-3">
        Тест
      </h3>
      <div className="space-y-5">
        {questions.map((q, qi) => {
          const chosen = answers[qi]
          const answered = chosen !== undefined
          return (
            <div key={qi}>
              <div className="text-sm font-sans font-semibold mb-2">
                {qi + 1}. {q.question}
              </div>
              <div className="space-y-1.5">
                {q.options.map((opt, oi) => {
                  const isCorrect = oi === q.answer_index
                  const isChosen = oi === chosen
                  let cls = "border-neutral-800 hover:border-neutral-700"
                  if (answered && isCorrect)
                    cls = "border-lime-400/60 bg-lime-400/10 text-lime-300"
                  else if (answered && isChosen && !isCorrect)
                    cls = "border-red-400/60 bg-red-400/10 text-red-300"
                  return (
                    <button
                      key={oi}
                      disabled={answered}
                      onClick={() =>
                        setAnswers((p) => ({ ...p, [qi]: oi }))
                      }
                      className={`w-full text-left text-sm font-sans px-3 py-2 rounded-md border transition-colors disabled:cursor-default ${cls}`}>
                      {opt}
                    </button>
                  )
                })}
              </div>
              {answered && q.explanation && (
                <p className="mt-2 text-xs font-sans text-neutral-400 border-l-2 border-neutral-800 pl-2">
                  {q.explanation}
                </p>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
