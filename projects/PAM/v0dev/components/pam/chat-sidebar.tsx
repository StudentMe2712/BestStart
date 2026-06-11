"use client";

import { Plus, MessageSquare } from "lucide-react";
import { ScrollArea } from "@/components/ui/scroll-area";

export interface Chat {
  id: string;
  title: string;
  updatedAt: Date;
}

interface ChatSidebarProps {
  chats: Chat[];
  activeChatId: string | null;
  onChatSelect: (chatId: string) => void;
  onNewChat: () => void;
  isOpen: boolean;
}

export function ChatSidebar({
  chats,
  activeChatId,
  onChatSelect,
  onNewChat,
  isOpen,
}: ChatSidebarProps) {
  return (
    <aside
      className={`fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border bg-sidebar pt-14 transition-transform duration-200 md:relative md:translate-x-0 md:pt-0 ${
        isOpen ? "translate-x-0" : "-translate-x-full"
      }`}
    >
      {/* New Chat Button */}
      <div className="p-3">
        <button
          onClick={onNewChat}
          className="flex w-full items-center gap-2 rounded-lg border border-dashed border-border bg-transparent px-3 py-2.5 font-mono text-sm text-muted-foreground transition-colors hover:border-primary hover:text-primary"
        >
          <Plus className="size-4" />
          <span>+ Новый чат</span>
        </button>
      </div>

      {/* Chats Label */}
      <div className="px-4 py-2">
        <span className="font-mono text-xs uppercase tracking-wider text-muted-foreground">
          чаты
        </span>
      </div>

      {/* Chat List */}
      <ScrollArea className="flex-1 px-2">
        <div className="flex flex-col gap-1 pb-4">
          {chats.length === 0 ? (
            <div className="px-2 py-8 text-center">
              <MessageSquare className="mx-auto mb-2 size-8 text-muted-foreground/50" />
              <p className="text-sm text-muted-foreground">Нет чатов</p>
            </div>
          ) : (
            chats.map((chat) => (
              <button
                key={chat.id}
                onClick={() => onChatSelect(chat.id)}
                className={`flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm transition-colors ${
                  activeChatId === chat.id
                    ? "bg-sidebar-accent text-sidebar-foreground"
                    : "text-muted-foreground hover:bg-sidebar-accent/50 hover:text-sidebar-foreground"
                }`}
              >
                <MessageSquare className="size-4 shrink-0" />
                <span className="truncate">{chat.title}</span>
              </button>
            ))
          )}
        </div>
      </ScrollArea>
    </aside>
  );
}
