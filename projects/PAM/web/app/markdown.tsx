"use client"

import { useState, type ReactNode } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter"
import { oneDark } from "react-syntax-highlighter/dist/esm/styles/prism"

/** Извлечь текст из произвольных children react-markdown (для кнопки «копировать»). */
function nodeText(node: ReactNode): string {
  if (node == null || node === false) return ""
  if (typeof node === "string" || typeof node === "number") return String(node)
  if (Array.isArray(node)) return node.map(nodeText).join("")
  // React element с children
  const el = node as { props?: { children?: ReactNode } }
  if (el.props?.children) return nodeText(el.props.children)
  return ""
}

function CodeBlock({ language, code }: { language: string; code: string }) {
  const [copied, setCopied] = useState(false)
  async function copy() {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* clipboard may be unavailable */
    }
  }
  return (
    <div className="my-3 rounded-lg overflow-hidden border border-neutral-800 bg-[#0b0b0f]">
      <div className="flex items-center justify-between px-3 py-1.5 bg-neutral-900/70 border-b border-neutral-800">
        <span className="text-[10px] uppercase tracking-widest text-neutral-500">
          {language || "code"}
        </span>
        <button
          type="button"
          onClick={copy}
          className="text-[10px] uppercase tracking-wider text-neutral-400 hover:text-lime-400 transition-colors">
          {copied ? "скопировано ✓" : "копировать"}
        </button>
      </div>
      <SyntaxHighlighter
        language={language || "text"}
        style={oneDark}
        PreTag="div"
        customStyle={{
          margin: 0,
          background: "transparent",
          padding: "0.85rem 1rem",
          fontSize: "0.8rem",
          lineHeight: 1.55
        }}
        codeTagProps={{
          style: { fontFamily: "var(--font-mono, ui-monospace, monospace)" }
        }}>
        {code}
      </SyntaxHighlighter>
    </div>
  )
}

/** ChatGPT-подобный рендер ответа: markdown + код-блоки с подсветкой и копированием.
 *
 * variant="reader" — книжная типографика для длинного исходного материала:
 * отступ первой строки абзаца, больше воздуха между абзацами. */
export default function Markdown({
  children,
  variant = "default"
}: {
  children: string
  variant?: "default" | "reader"
}) {
  const readerType =
    variant === "reader"
      ? "prose-p:[text-indent:1.25rem] prose-p:my-3.5 prose-p:leading-7 " +
        "prose-headings:mt-6 prose-li:my-1"
      : "prose-p:leading-relaxed prose-li:my-0.5"
  return (
    <div
      className={`prose prose-invert prose-sm max-w-none font-sans
                 prose-headings:font-semibold
                 prose-pre:bg-transparent prose-pre:p-0 prose-pre:my-0
                 prose-code:before:content-none prose-code:after:content-none
                 prose-a:text-lime-400 prose-a:no-underline hover:prose-a:underline
                 prose-strong:text-neutral-100 prose-th:text-neutral-300 ${readerType}`}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          // pre — простой проброс: контейнер кода даёт сам CodeBlock (без вложенного <pre>).
          pre({ children }) {
            return <>{children}</>
          },
          // Решаем inline-vs-block прямо здесь: блок = есть language-* или перенос строки.
          code({ className, children, ...props }) {
            const text = nodeText(children)
            const match = /language-(\w+)/.exec(className || "")
            const isBlock = !!match || text.includes("\n")
            if (isBlock) {
              return <CodeBlock language={match?.[1] || ""} code={text.replace(/\n$/, "")} />
            }
            return (
              <code
                className="px-1.5 py-0.5 rounded bg-neutral-800 text-lime-300 text-[0.85em] font-mono"
                {...props}>
                {children}
              </code>
            )
          },
          a({ href, children }) {
            return (
              <a href={href} target="_blank" rel="noopener noreferrer">
                {children}
              </a>
            )
          },
          // markitdown (Word/PDF) часто отдаёт картинки с пустым src —
          // именно это роняло консоль предупреждением React про пустой src.
          // Пустые не рендерим вовсе; реальные — аккуратно стилизуем и прячем
          // битые по onError.
          img({ src, alt }) {
            if (typeof src !== "string" || !src.trim()) return null
            return (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={src}
                alt={alt || ""}
                className="rounded-lg max-w-full h-auto my-3 border border-neutral-800"
                onError={(e) => {
                  e.currentTarget.style.display = "none"
                }}
              />
            )
          },
          table({ children }) {
            return (
              <div className="overflow-x-auto my-3">
                <table className="w-full">{children}</table>
              </div>
            )
          }
        }}>
        {children}
      </ReactMarkdown>
    </div>
  )
}
