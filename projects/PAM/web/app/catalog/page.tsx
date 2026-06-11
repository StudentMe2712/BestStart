import { CATALOG } from "../../lib/catalog"

export const metadata = {
  title: "Каталог AI-экосистемы — PAM"
}

export default function CatalogPage() {
  return (
    <main className="max-w-5xl mx-auto px-6 py-8">
      <header className="border-b border-neutral-800 pb-6 mb-6">
        <div className="text-xs uppercase tracking-widest text-lime-400 mb-2">
          /// каталог
        </div>
        <h1 className="text-3xl font-semibold">AI-экосистема</h1>
        <p className="text-neutral-400 mt-2 text-sm font-sans max-w-2xl">
          Кураторская выжимка по MCP, навыкам, агентам, коннекторам, плагинам и
          инструментам (середина 2026). Полная версия с таблицами и топами —{" "}
          <code className="text-lime-300">docs/ai-ecosystem-catalog.md</code> в
          репозитории. Звёзды (~) приблизительные.
        </p>
      </header>

      <div className="grid md:grid-cols-[170px_1fr] gap-8">
        {/* якорная навигация */}
        <nav className="hidden md:block">
          <div className="sticky top-20 space-y-1 text-sm">
            {CATALOG.map((s) => (
              <a
                key={s.id}
                href={`#${s.id}`}
                className={`block px-2 py-1 rounded-md transition-colors ${
                  s.id === "pam"
                    ? "text-lime-400 hover:bg-neutral-900"
                    : "text-neutral-400 hover:text-neutral-100 hover:bg-neutral-900"
                }`}>
                {s.title}
              </a>
            ))}
          </div>
        </nav>

        {/* секции */}
        <div className="space-y-10 min-w-0">
          {CATALOG.map((s) => (
            <section key={s.id} id={s.id} className="scroll-mt-20">
              <h2
                className={`text-lg font-semibold mb-1 ${
                  s.id === "pam" ? "text-lime-400" : ""
                }`}>
                {s.title}
              </h2>
              {s.blurb && (
                <p className="text-neutral-500 text-sm font-sans mb-3">
                  {s.blurb}
                </p>
              )}
              <div className="space-y-2">
                {s.items.map((it) => (
                  <a
                    key={it.name}
                    href={it.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-start gap-3 border border-neutral-800 rounded-sm p-3 hover:border-lime-400/40 transition-colors group">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-semibold group-hover:text-lime-400 transition-colors">
                          {it.name}
                        </span>
                        {it.tag && (
                          <span className="text-[9px] uppercase tracking-wider px-1.5 py-px border border-neutral-700 rounded text-neutral-500">
                            {it.tag}
                          </span>
                        )}
                      </div>
                      <p className="text-sm text-neutral-400 font-sans mt-0.5">
                        {it.desc}
                      </p>
                    </div>
                    {it.stars && (
                      <span className="shrink-0 text-[11px] tabular-nums text-neutral-600 mt-0.5">
                        ★ {it.stars}
                      </span>
                    )}
                  </a>
                ))}
              </div>
            </section>
          ))}
        </div>
      </div>
    </main>
  )
}
