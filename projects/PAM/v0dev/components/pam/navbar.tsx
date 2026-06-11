"use client";

import { useState } from "react";
import { Menu, X } from "lucide-react";

const tabs = [
  { id: "chat", label: "Чат", active: true },
  { id: "history", label: "История", active: false },
  { id: "favorites", label: "Избранное", active: false },
  { id: "lecturer", label: "Лектор", active: false, soon: true },
];

interface NavbarProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
  onMenuToggle: () => void;
  isSidebarOpen: boolean;
}

export function Navbar({
  activeTab,
  onTabChange,
  onMenuToggle,
  isSidebarOpen,
}: NavbarProps) {
  return (
    <header className="sticky top-0 z-50 flex h-14 items-center justify-between border-b border-border bg-background/95 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      {/* Left: Brand */}
      <div className="flex items-center gap-3">
        <button
          className="flex size-8 items-center justify-center rounded-md hover:bg-muted md:hidden"
          onClick={onMenuToggle}
          aria-label={isSidebarOpen ? "Закрыть меню" : "Открыть меню"}
        >
          {isSidebarOpen ? (
            <X className="size-5 text-muted-foreground" />
          ) : (
            <Menu className="size-5 text-muted-foreground" />
          )}
        </button>
        <div className="flex items-center gap-2">
          <div className="flex size-6 items-center justify-center rounded bg-primary">
            <span className="font-mono text-xs font-bold text-primary-foreground">
              P
            </span>
          </div>
          <div className="flex flex-col">
            <span className="font-mono text-sm font-semibold tracking-tight text-foreground">
              PAM
            </span>
            <span className="hidden font-mono text-[10px] uppercase tracking-wider text-muted-foreground sm:block">
              personal_ai_memory
            </span>
          </div>
        </div>
      </div>

      {/* Right: Tabs */}
      <nav className="flex items-center gap-1">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => !tab.soon && onTabChange(tab.id)}
            disabled={tab.soon}
            className={`relative flex items-center gap-1.5 rounded-md px-3 py-1.5 font-mono text-sm transition-colors ${
              activeTab === tab.id
                ? "bg-primary/10 text-primary"
                : tab.soon
                  ? "cursor-not-allowed text-muted-foreground/50"
                  : "text-muted-foreground hover:bg-muted hover:text-foreground"
            }`}
          >
            {tab.label}
            {tab.soon && (
              <span className="rounded bg-muted px-1.5 py-0.5 font-mono text-[10px] uppercase text-muted-foreground">
                скоро
              </span>
            )}
          </button>
        ))}
      </nav>
    </header>
  );
}
