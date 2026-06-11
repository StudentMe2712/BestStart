"use client";

import { useRef, useEffect } from "react";
import { ScrollArea } from "@/components/ui/scroll-area";

export interface Message {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: string[];
}

interface MessageListProps {
  messages: Message[];
  isTyping: boolean;
}

function TypingIndicator() {
  return (
    <div className="flex items-start gap-3">
      <div className="flex size-7 shrink-0 items-center justify-center rounded bg-primary">
        <span className="font-mono text-xs font-bold text-primary-foreground">
          P
        </span>
      </div>
      <div className="flex items-center gap-1 rounded-lg bg-muted px-4 py-3">
        <div className="typing-dot size-2 rounded-full bg-primary" />
        <div className="typing-dot size-2 rounded-full bg-primary" />
        <div className="typing-dot size-2 rounded-full bg-primary" />
      </div>
    </div>
  );
}

function UserMessage({ content }: { content: string }) {
  return (
    <div className="flex justify-end">
      <div className="max-w-[80%] rounded-2xl rounded-br-sm bg-neutral-800 px-4 py-2.5">
        <p className="whitespace-pre-wrap text-sm text-foreground">{content}</p>
      </div>
    </div>
  );
}

function AssistantMessage({
  content,
  sources,
}: {
  content: string;
  sources?: string[];
}) {
  return (
    <div className="flex items-start gap-3">
      <div className="flex size-7 shrink-0 items-center justify-center rounded bg-primary">
        <span className="font-mono text-xs font-bold text-primary-foreground">
          P
        </span>
      </div>
      <div className="flex max-w-[80%] flex-col gap-2">
        <div className="prose prose-invert prose-sm max-w-none">
          <p className="whitespace-pre-wrap text-sm leading-relaxed text-foreground">
            {content}
          </p>
        </div>
        {sources && sources.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="font-mono text-xs text-muted-foreground">
              память:
            </span>
            {sources.map((source, index) => (
              <span
                key={index}
                className="rounded border border-border bg-muted px-2 py-0.5 font-mono text-xs text-secondary"
              >
                {source}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function EmptyState() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center px-4 py-16">
      <div className="mb-4 flex size-16 items-center justify-center rounded-xl bg-primary">
        <span className="font-mono text-2xl font-bold text-primary-foreground">
          P
        </span>
      </div>
      <h2 className="mb-2 font-mono text-xl font-semibold text-foreground">
        Чат с твоей памятью
      </h2>
      <p className="max-w-md text-center text-sm leading-relaxed text-muted-foreground">
        PAM помнит твои разговоры с ChatGPT, Claude и Gemini. Задай вопрос —
        получи ответ на основе своей истории общения с AI.
      </p>
    </div>
  );
}

export function MessageList({ messages, isTyping }: MessageListProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, isTyping]);

  if (messages.length === 0 && !isTyping) {
    return <EmptyState />;
  }

  return (
    <ScrollArea className="flex-1" ref={scrollRef}>
      <div className="mx-auto flex max-w-3xl flex-col gap-6 px-4 py-6">
        {messages.map((message) =>
          message.role === "user" ? (
            <UserMessage key={message.id} content={message.content} />
          ) : (
            <AssistantMessage
              key={message.id}
              content={message.content}
              sources={message.sources}
            />
          )
        )}
        {isTyping && <TypingIndicator />}
        <div ref={bottomRef} />
      </div>
    </ScrollArea>
  );
}
