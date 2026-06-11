"use client";

import { useState, useCallback } from "react";
import { Navbar } from "./navbar";
import { ChatSidebar, type Chat } from "./chat-sidebar";
import { MessageList, type Message } from "./message-list";
import { ChatInput } from "./chat-input";

// Mock data for demonstration
const mockChats: Chat[] = [
  { id: "1", title: "Про архитектуру React-приложения", updatedAt: new Date() },
  { id: "2", title: "Как настроить TypeScript", updatedAt: new Date() },
  { id: "3", title: "Лучшие практики API дизайна", updatedAt: new Date() },
  { id: "4", title: "Оптимизация производительности", updatedAt: new Date() },
];

const mockMessages: Message[] = [
  {
    id: "1",
    role: "user",
    content: "Напомни, что мы обсуждали про state management в React?",
  },
  {
    id: "2",
    role: "assistant",
    content:
      "В прошлых разговорах ты сравнивал разные подходы к управлению состоянием:\n\n1. **Redux** — ты использовал его в крупном проекте, но отмечал избыточность бойлерплейта\n\n2. **Zustand** — понравился за простоту API и минимальный размер бандла\n\n3. **React Query** — выбрал для серверного состояния, отметил удобную работу с кешем\n\nВ итоге ты остановился на комбинации Zustand + React Query для нового проекта.",
    sources: ["chatgpt_2024-01-15", "claude_2024-01-20", "chatgpt_2024-02-01"],
  },
  {
    id: "3",
    role: "user",
    content: "А какой подход я выбрал для форм?",
  },
  {
    id: "4",
    role: "assistant",
    content:
      "Ты тестировал React Hook Form и Formik. В итоге выбрал **React Hook Form** из-за лучшей производительности и меньшего количества ререндеров. Особенно тебе понравилась интеграция с Zod для валидации схем.",
    sources: ["claude_2024-01-25"],
  },
];

export function ChatApp() {
  const [activeTab, setActiveTab] = useState("chat");
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [chats] = useState<Chat[]>(mockChats);
  const [activeChatId, setActiveChatId] = useState<string | null>("1");
  const [messages, setMessages] = useState<Message[]>(mockMessages);
  const [inputValue, setInputValue] = useState("");
  const [isTyping, setIsTyping] = useState(false);

  const handleNewChat = useCallback(() => {
    setActiveChatId(null);
    setMessages([]);
    setIsSidebarOpen(false);
  }, []);

  const handleChatSelect = useCallback((chatId: string) => {
    setActiveChatId(chatId);
    // In real app, load messages for this chat
    setMessages(mockMessages);
    setIsSidebarOpen(false);
  }, []);

  const handleSendMessage = useCallback(() => {
    if (!inputValue.trim()) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      role: "user",
      content: inputValue.trim(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInputValue("");
    setIsTyping(true);

    // Simulate AI response
    setTimeout(() => {
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: "assistant",
        content:
          "Это демо-ответ. В реальном приложении здесь будет ответ от API с использованием твоей памяти разговоров.",
        sources: ["chatgpt_demo", "claude_demo"],
      };
      setMessages((prev) => [...prev, assistantMessage]);
      setIsTyping(false);
    }, 1500);
  }, [inputValue]);

  const handleAttach = useCallback(() => {
    // Placeholder for file attachment
    console.log("Attach file clicked");
  }, []);

  return (
    <div className="flex h-screen flex-col bg-background">
      <Navbar
        activeTab={activeTab}
        onTabChange={setActiveTab}
        onMenuToggle={() => setIsSidebarOpen(!isSidebarOpen)}
        isSidebarOpen={isSidebarOpen}
      />

      <div className="flex flex-1 overflow-hidden">
        <ChatSidebar
          chats={chats}
          activeChatId={activeChatId}
          onChatSelect={handleChatSelect}
          onNewChat={handleNewChat}
          isOpen={isSidebarOpen}
        />

        {/* Overlay for mobile sidebar */}
        {isSidebarOpen && (
          <div
            className="fixed inset-0 z-30 bg-black/50 md:hidden"
            onClick={() => setIsSidebarOpen(false)}
            aria-hidden="true"
          />
        )}

        {/* Main Chat Area */}
        <main className="flex flex-1 flex-col overflow-hidden">
          <MessageList messages={messages} isTyping={isTyping} />
          <ChatInput
            value={inputValue}
            onChange={setInputValue}
            onSubmit={handleSendMessage}
            onAttach={handleAttach}
            disabled={isTyping}
          />
        </main>
      </div>
    </div>
  );
}
