"use client"

import { useEffect, useMemo, useState } from "react"
import Link from "next/link"
import { useParams, useRouter } from "next/navigation"

import {
  getCourse,
  getSource,
  generateCourse,
  deleteSource,
  reformatSource,
  sourceFileUrl,
  type Course,
  type ContentSourceDetail
} from "../../../lib/api"
import {
  COURSE_STATUSES,
  getCourseStatus,
  setCourseStatus,
  type CourseStatus
} from "../../../lib/course-status"
import {
  youtubeId,
  kindOf,
  KIND_LABEL,
  fmtDate,
  cleanSourceMarkdown,
  PlayIcon
} from "../../../lib/material-ui"
import Markdown from "../../markdown"

type Tab = "summary" | "extra" | "quiz"

function plural(n: number, one: string, few: string, many: string) {
  const m10 = n % 10
  const m100 = n % 100
  if (m10 === 1 && m100 !== 11) return one
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return few
  return many
}

export default function CourseReaderPage() {
  const router = useRouter()
  const { id } = useParams<{ id: string }>()

  const [course, setCourse] = useState<Course | null>(null)
  const [source, setSource] = useState<ContentSourceDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const [activeChapter, setActiveChapter] = useState(0)
  const [tab, setTab] = useState<Tab>("summary")
  const [status, setStatus] = useState<CourseStatus>("new")
  const [regen, setRegen] = useState(false)

  // «Исходный материал»: AI-причёсанная версия + переключатель оригинал/форматировано.
  const [formatted, setFormatted] = useState<string | null>(null)
  const [showFormatted, setShowFormatted] = useState(false)
  const [reformatting, setReformatting] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    Promise.allSettled([getCourse(id), getSource(id)]).then(([c, s]) => {
      if (cancelled) return
      if (c.status === "fulfilled") setCourse(c.value)
      if (s.status === "fulfilled") setSource(s.value)
      if (c.status === "rejected" && s.status === "rejected")
        setError("Не удалось загрузить курс")
      setLoading(false)
    })
    return () => {
      cancelled = true
    }
  }, [id, tick])

  useEffect(() => {
    setStatus(getCourseStatus(id))
  }, [id])

  // Подхватываем уже сгенерированную ранее «причёсанную» версию (кэш на бэке).
  useEffect(() => {
    setFormatted(source?.formatted_text ?? null)
    setShowFormatted(!!source?.formatted_text)
  }, [source])

  async function improveReadability() {
    if (formatted) {
      setShowFormatted((s) => !s) // уже есть — просто переключаем вид
      return
    }
    setReformatting(true)
    setError(null)
    try {
      const t = await reformatSource(id)
      setFormatted(t)
      setShowFormatted(true)
    } catch (e) {
      setError(String(e))
    } finally {
      setReformatting(false)
    }
  }

  function pickStatus(s: CourseStatus) {
    setStatus(s)
    setCourseStatus(id, s)
  }

  async function regenerate() {
    setRegen(true)
    setError(null)
    try {
      const c = await generateCourse(id)
      setCourse(c)
      setActiveChapter(0)
      setTab("summary")
    } catch (e) {
      setError(String(e))
    } finally {
      setRegen(false)
    }
  }

  async function removeCourse() {
    if (
      !window.confirm(
        "Удалить материал и курс? Материал будет удалён целиком вместе с курсом. Действие необратимо."
      )
    )
      return
    try {
      await deleteSource(id)
      router.push("/learn")
    } catch (e) {
      setError(String(e))
    }
  }

  const modules = course?.data.modules ?? []
  const quiz = course?.data.quiz ?? []
  const chapter = Math.min(activeChapter, Math.max(modules.length - 1, 0))
  const mod = modules[chapter]
  const kind = source ? kindOf(source) : "text"
  const ytId = kind === "youtube" ? youtubeId(source?.url ?? null) : null
  // Нативный предпросмотр оригинала — пока только PDF (браузер рендерит сам).
  const hasPdfPreview = !!source?.has_file && !!source?.mime?.includes("pdf")

  const brief = useMemo(() => {
    const raw = mod?.lessons?.find((l) => l.content?.trim())?.content || ""
    const line =
      raw
        .replace(/[#*`>_]/g, "")
        .split("\n")
        .map((s) => s.trim())
        .find(Boolean) || ""
    return line.length > 180 ? line.slice(0, 180) + "…" : line
  }, [mod])

  if (loading) {
    return (
      <main className="max-w-[1700px] mx-auto px-6 py-10">
        <div className="text-neutral-500 text-sm font-sans">// загружаю курс…</div>
      </main>
    )
  }

  if (!course) {
    return (
      <main className="max-w-2xl mx-auto px-6 py-16 text-center">
        <Link
          href="/learn"
          className="text-sm text-neutral-400 hover:text-lime-400 transition-colors">
          ← Назад
        </Link>
        <div className="mt-8 border border-dashed border-neutral-800 rounded-xl p-10">
          <p className="text-neutral-300 font-sans">
            Для этого материала ещё нет курса.
          </p>
          {error && (
            <p className="text-red-400 text-sm font-sans mt-2">{error}</p>
          )}
          {source && source.status === "extracted" && (
            <button
              onClick={regenerate}
              disabled={regen}
              className="mt-5 px-5 py-2.5 rounded-lg bg-lime-400 text-neutral-950 font-medium text-sm hover:bg-lime-300 disabled:opacity-50 transition-colors">
              {regen ? "Создаю курс…" : "Создать курс"}
            </button>
          )}
        </div>
      </main>
    )
  }

  return (
    <main className="max-w-[1700px] mx-auto px-6 py-8">
      <div className="flex flex-col lg:flex-row gap-6 items-start">
        {/* ── ЛЕВАЯ: содержание ────────────────────────────────────────── */}
        <aside className="w-full lg:w-[340px] shrink-0 lg:sticky lg:top-20 space-y-4">
          <Link
            href="/learn"
            className="inline-flex items-center gap-2 text-sm text-neutral-400 hover:text-lime-400 transition-colors">
            ← Назад
          </Link>

          <div>
            <h1 className="text-xl font-semibold leading-snug">
              {course.title || source?.title || "Курс"}
            </h1>
            <div className="mt-2 text-[12px] text-neutral-500 font-sans flex items-center gap-1.5 flex-wrap">
              <span>{KIND_LABEL[kind]}</span>
              <span>· {modules.length} {plural(modules.length, "глава", "главы", "глав")}</span>
              <span>· {quiz.length} {plural(quiz.length, "тест", "теста", "тестов")}</span>
            </div>
          </div>

          <div>
            <h2 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
              Содержание курса
            </h2>
            <div className="space-y-2">
              {modules.map((m, i) => {
                const active = i === chapter
                return (
                  <button
                    key={i}
                    onClick={() => {
                      setActiveChapter(i)
                      setTab("summary")
                    }}
                    className={`w-full text-left rounded-lg border p-3 transition-colors ${
                      active
                        ? "border-lime-400/40 bg-neutral-900"
                        : "border-neutral-800 hover:border-neutral-700"
                    }`}>
                    <div className="flex items-start gap-2.5">
                      <span
                        className={`text-[11px] tabular-nums mt-0.5 ${
                          active ? "text-lime-400" : "text-neutral-600"
                        }`}>
                        {String(i + 1).padStart(2, "0")}
                      </span>
                      <span
                        className={`text-sm font-sans leading-snug ${
                          active ? "text-neutral-100" : "text-neutral-300"
                        }`}>
                        {m.title}
                      </span>
                    </div>
                  </button>
                )
              })}
            </div>
          </div>
        </aside>

        {/* ── ЦЕНТР: контент ───────────────────────────────────────────── */}
        <section className="flex-1 min-w-0">
          {ytId ? (
            <YoutubeHero ytId={ytId} url={source?.url ?? null} />
          ) : hasPdfPreview ? (
            <PdfHero
              src={sourceFileUrl(id)}
              title={source?.title ?? null}
            />
          ) : null}

          <div className="mb-5">
            <div className="text-xs uppercase tracking-widest text-lime-400 mb-1">
              Глава {chapter + 1} из {modules.length}
            </div>
            <h2 className="text-2xl font-semibold">{mod?.title || "—"}</h2>
            {brief && (
              <p className="text-neutral-400 text-sm font-sans mt-2 max-w-[900px]">
                {brief}
              </p>
            )}
          </div>

          <div className="flex items-center gap-1 border-b border-neutral-800 mb-6">
            {(
              [
                ["summary", "Конспект"],
                ["extra", "Дополнительно"],
                ["quiz", "Тест"]
              ] as [Tab, string][]
            ).map(([key, label]) => (
              <button
                key={key}
                onClick={() => setTab(key)}
                className={`px-4 py-2.5 text-sm font-sans -mb-px border-b-2 transition-colors ${
                  tab === key
                    ? "border-lime-400 text-lime-400"
                    : "border-transparent text-neutral-400 hover:text-neutral-200"
                }`}>
                {label}
                {key === "quiz" && quiz.length > 0 && (
                  <span className="ml-1.5 text-[11px] text-neutral-600">
                    {quiz.length}
                  </span>
                )}
              </button>
            ))}
          </div>

          <div className="max-w-[900px]">
            {tab === "summary" &&
              (mod?.lessons?.length ? (
                <div className="space-y-7">
                  {mod.lessons.map((l, li) => (
                    <article key={li}>
                      <h3 className="text-base font-semibold font-sans mb-2">
                        {l.title}
                      </h3>
                      <Markdown>{l.content || ""}</Markdown>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="text-neutral-500 text-sm font-sans">
                  В этой главе нет конспекта.
                </p>
              ))}

            {tab === "extra" && (
              <div className="space-y-8">
                {course.data.summary && (
                  <section>
                    <h3 className="text-sm uppercase tracking-wider text-neutral-500 mb-2">
                      О чём этот материал
                    </h3>
                    <Markdown>{course.data.summary}</Markdown>
                  </section>
                )}
                {course.level && (
                  <div className="text-sm font-sans text-neutral-400">
                    Уровень:{" "}
                    <span className="text-lime-400">{course.level}</span>
                  </div>
                )}
                <section>
                  <div className="flex items-center justify-between gap-3 mb-2">
                    <h3 className="text-sm uppercase tracking-wider text-neutral-500">
                      Исходный материал
                    </h3>
                    {source?.text && (
                      <button
                        onClick={improveReadability}
                        disabled={reformatting}
                        title="Разбить по смыслу на главы и абзацы (ИИ)"
                        className="shrink-0 text-xs font-sans px-3 py-1.5 rounded-lg border border-neutral-700 text-neutral-300 hover:text-lime-400 hover:border-lime-400/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                        {reformatting
                          ? "Улучшаю…"
                          : formatted
                            ? showFormatted
                              ? "Показать оригинал"
                              : "Показать форматированное"
                            : "Улучшить читаемость ✨"}
                      </button>
                    )}
                  </div>
                  {source?.text ? (
                    <div className="max-h-[600px] overflow-y-auto rounded-lg border border-neutral-800 bg-neutral-900/40 px-5 py-4">
                      <Markdown variant="reader">
                        {showFormatted && formatted
                          ? formatted
                          : cleanSourceMarkdown(source.text)}
                      </Markdown>
                    </div>
                  ) : (
                    <p className="text-neutral-500 text-sm font-sans">
                      Текст источника недоступен.
                    </p>
                  )}
                </section>
              </div>
            )}

            {tab === "quiz" && <Quiz questions={quiz} />}
          </div>
        </section>

        {/* ── ПРАВАЯ: информация и действия ────────────────────────────── */}
        <aside className="w-full lg:w-[300px] shrink-0 lg:sticky lg:top-20 space-y-4">
          {error && (
            <div className="text-red-400 text-sm font-sans">{error}</div>
          )}

          <Card title="О курсе">
            <InfoRow label="Источник" value={source?.title || source?.url || "—"} />
            <InfoRow label="Дата создания" value={fmtDate(course.created_at)} />
            <InfoRow label="Тип материала" value={KIND_LABEL[kind]} />
            <InfoRow label="Глав" value={String(modules.length)} />
            <InfoRow label="Вопросов" value={String(quiz.length)} />
          </Card>

          <Card title="Статус курса">
            <div className="space-y-1.5">
              {COURSE_STATUSES.map((s) => {
                const active = status === s.key
                return (
                  <button
                    key={s.key}
                    onClick={() => pickStatus(s.key)}
                    className={`w-full flex items-center gap-2.5 text-sm font-sans px-3 py-2 rounded-lg border transition-colors ${
                      active
                        ? "border-lime-400/40 bg-neutral-900 text-neutral-100"
                        : "border-transparent text-neutral-400 hover:bg-neutral-900/60"
                    }`}>
                    <span className={`w-2.5 h-2.5 rounded-full ${s.dot}`} />
                    {s.label}
                    {active && <span className="ml-auto text-lime-400">✓</span>}
                  </button>
                )
              })}
            </div>
          </Card>

          <Card title="Действия">
            <div className="space-y-1.5">
              <ActionRow onClick={regenerate} disabled={regen}>
                {regen ? "Пересоздаю…" : "Пересоздать курс"}
              </ActionRow>
              {source?.url ? (
                <a
                  href={source.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="block w-full text-left text-sm font-sans px-3 py-2 rounded-lg text-neutral-300 hover:bg-neutral-900 transition-colors">
                  Открыть исходный материал ↗
                </a>
              ) : (
                <ActionRow disabled>Открыть исходный материал</ActionRow>
              )}
              <ActionRow
                onClick={() => setTab("quiz")}
                disabled={quiz.length === 0}>
                Пройти тест
              </ActionRow>
              <ActionRow onClick={removeCourse} danger>
                Удалить материал и курс
              </ActionRow>
            </div>
          </Card>
        </aside>
      </div>
    </main>
  )
}

function YoutubeHero({ ytId, url }: { ytId: string; url: string | null }) {
  const [src, setSrc] = useState(`https://i.ytimg.com/vi/${ytId}/maxresdefault.jpg`)
  const [playing, setPlaying] = useState(false)

  // Лениво грузим плеер только по клику (приватность youtube-nocookie + не
  // тянем iframe, пока пользователь не захотел смотреть).
  if (playing) {
    return (
      <div
        className="relative w-full rounded-xl overflow-hidden mb-6 bg-black"
        style={{ height: 420 }}>
        <iframe
          className="h-full w-full"
          src={`https://www.youtube-nocookie.com/embed/${ytId}?autoplay=1&rel=0`}
          title="YouTube"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
          allowFullScreen
        />
      </div>
    )
  }

  return (
    <div
      onClick={() => setPlaying(true)}
      className="group relative w-full rounded-xl overflow-hidden mb-6 bg-neutral-900 cursor-pointer"
      style={{ height: 420 }}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={src}
        alt=""
        className="h-full w-full object-cover"
        onError={() => setSrc(`https://i.ytimg.com/vi/${ytId}/hqdefault.jpg`)}
      />
      <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
      <span className="absolute inset-0 grid place-items-center">
        <span className="grid place-items-center w-16 h-16 rounded-full bg-black/55 text-white group-hover:bg-red-600 transition-colors">
          <PlayIcon />
        </span>
      </span>
      {url && (
        <a
          href={url}
          target="_blank"
          rel="noopener noreferrer"
          onClick={(e) => e.stopPropagation()}
          className="absolute bottom-3 right-3 text-xs font-sans px-3 py-1.5 rounded-lg bg-black/60 text-white hover:bg-black/80 transition-colors">
          Смотреть на YouTube ↗
        </a>
      )}
    </div>
  )
}

function PdfHero({ src, title }: { src: string; title: string | null }) {
  return (
    <div className="w-full rounded-xl overflow-hidden mb-6 border border-neutral-800 bg-neutral-900">
      <div className="flex items-center justify-between gap-3 px-4 py-2 border-b border-neutral-800">
        <span className="text-xs uppercase tracking-widest text-neutral-500">
          Предпросмотр · PDF
        </span>
        <a
          href={src}
          target="_blank"
          rel="noopener noreferrer"
          className="text-xs font-sans text-neutral-400 hover:text-lime-400 transition-colors">
          Открыть в новой вкладке ↗
        </a>
      </div>
      <iframe
        src={src}
        title={title || "PDF"}
        className="w-full bg-neutral-950"
        style={{ height: 600 }}
      />
    </div>
  )
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-neutral-800 bg-neutral-950 p-4">
      <h3 className="text-xs uppercase tracking-widest text-neutral-500 mb-3">
        {title}
      </h3>
      {children}
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-3 py-1.5 text-sm font-sans border-b border-neutral-900 last:border-0">
      <span className="text-neutral-500 shrink-0">{label}</span>
      <span className="text-neutral-200 text-right truncate">{value}</span>
    </div>
  )
}

function ActionRow({
  children,
  onClick,
  danger = false,
  disabled = false
}: {
  children: React.ReactNode
  onClick?: () => void
  danger?: boolean
  disabled?: boolean
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`w-full text-left text-sm font-sans px-3 py-2 rounded-lg transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
        danger
          ? "text-red-400 hover:bg-red-400/10"
          : "text-neutral-300 hover:bg-neutral-900"
      }`}>
      {children}
    </button>
  )
}

function Quiz({ questions }: { questions: Course["data"]["quiz"] }) {
  const [answers, setAnswers] = useState<Record<number, number>>({})
  if (!questions?.length) {
    return (
      <p className="text-neutral-500 text-sm font-sans">
        Для этого курса нет теста.
      </p>
    )
  }
  return (
    <div className="space-y-6">
      <p className="text-[12px] text-neutral-500 font-sans">
        Тест — для самопроверки. Проходить необязательно, на статус курса он не
        влияет.
      </p>
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
                    onClick={() => setAnswers((p) => ({ ...p, [qi]: oi }))}
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
  )
}
