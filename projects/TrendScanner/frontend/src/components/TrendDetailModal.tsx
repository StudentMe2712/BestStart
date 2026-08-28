import React, { useState, useEffect } from 'react';
import {
  X,
  ExternalLink,
  CheckCircle2,
  Copy,
  Check,
  Flame,
  Calendar,
  Layers,
  Terminal,
  Trash2,
  Sparkles,
  Loader2,
  FileText,
  AlertTriangle,
  BookOpen,
  Heart,
  ThumbsDown,
} from 'lucide-react';
import { Trend } from '../types';
import { ScoreBadge, ScamBadge, SourceBadge, TrendIndicator } from './Badges';
import { apiClient } from '../api/client';

interface TrendDetailModalProps {
  trend: Trend | null;
  isOpen: boolean;
  onClose: () => void;
  onToggleReview: (trend: Trend) => Promise<void>;
  onFeedback?: (trend: Trend, score: number) => Promise<void>;
  onDeleteTrend: (trendId: number) => Promise<void>;
}

export const TrendDetailModal: React.FC<TrendDetailModalProps> = ({
  trend,
  isOpen,
  onClose,
  onToggleReview,
  onFeedback,
  onDeleteTrend,
}) => {
  const [copiedOriginal, setCopiedOriginal] = useState(false);
  const [copiedSummary, setCopiedSummary] = useState(false);
  const [copiedReport, setCopiedReport] = useState(false);
  const [detailedReport, setDetailedReport] = useState<string | null>(null);
  const [isGeneratingReport, setIsGeneratingReport] = useState(false);
  const [isDeepSearching, setIsDeepSearching] = useState(false);
  const [vaultNotification, setVaultNotification] = useState<string | null>(null);
  const [vaultFileName, setVaultFileName] = useState<string | null>(null);
  const [reportError, setReportError] = useState<string | null>(null);

  useEffect(() => {
    if (trend) {
      setDetailedReport(trend.detailed_report || null);
      setReportError(null);
      setVaultNotification(null);
      setVaultFileName(null);
      setIsGeneratingReport(false);
      setIsDeepSearching(false);
    }
  }, [trend]);

  if (!isOpen || !trend) return null;

  const handleCopy = (text: string, type: 'original' | 'summary' | 'report') => {
    navigator.clipboard.writeText(text);
    if (type === 'original') {
      setCopiedOriginal(true);
      setTimeout(() => setCopiedOriginal(false), 2000);
    } else if (type === 'summary') {
      setCopiedSummary(true);
      setTimeout(() => setCopiedSummary(false), 2000);
    } else {
      setCopiedReport(true);
      setTimeout(() => setCopiedReport(false), 2000);
    }
  };

  const handleGenerateReport = async () => {
    if (!trend) return;
    setIsGeneratingReport(true);
    setReportError(null);
    try {
      const res = await apiClient.generateTrendReport(trend.id);
      setDetailedReport(res.detailed_report);
      trend.detailed_report = res.detailed_report;
    } catch (err: any) {
      setReportError(err?.message || 'Не удалось сгенерировать аналитический отчет.');
    } finally {
      setIsGeneratingReport(false);
    }
  };

  const handleDeepResearch = async () => {
    if (!trend) return;
    setIsDeepSearching(true);
    setReportError(null);
    setVaultNotification(null);
    try {
      const res = await apiClient.runDeepResearch(trend.id);
      setVaultNotification('Файл сохранен в Vault');
      setVaultFileName(res.file_name);
      if (res.detailed_report) {
        setDetailedReport(res.detailed_report);
        trend.detailed_report = res.detailed_report;
      }
    } catch (err: any) {
      setReportError(err?.message || 'Не удалось выполнить анализ конкурентов.');
    } finally {
      setIsDeepSearching(false);
    }
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return 'Неизвестно';
    try {
      return new Date(dateStr).toLocaleString('ru-RU', {
        dateStyle: 'medium',
        timeStyle: 'short',
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-fadeIn">
      {/* Modal Container */}
      <div
        className="bg-app-elevated border border-app-border rounded-xl w-full max-w-4xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden text-content-primary font-sans"
        role="dialog"
        aria-modal="true"
      >
        {/* Modal Header */}
        <div className="bg-app-surface px-6 py-4 border-b border-app-border flex items-start justify-between gap-4">
          <div className="flex flex-col gap-1.5 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <TrendIndicator isTrend={trend.is_trend} size="md" />
              <span className="font-mono text-xs text-content-muted">ID #{trend.id}</span>
              {trend.mention_count && trend.mention_count > 1 && (
                <span className="bg-brand/15 text-brand-hover border border-brand/30 text-xs px-2 py-0.5 rounded font-mono font-medium">
                  {trend.mention_count} упоминаний
                </span>
              )}
              <SourceBadge sourceName={trend.source_name} sourceType={trend.source_type} />
            </div>

            <h2 className="text-xl font-bold text-content-primary tracking-tight leading-snug">
              {trend.trend_name || 'Неопределенная тема'}
            </h2>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-lg bg-app-elevated hover:bg-app-hover border border-app-border text-content-muted hover:text-content-primary transition-colors flex-shrink-0 cursor-pointer"
            aria-label="Закрыть модальное окно"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body (Scrollable) */}
        <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6">
          {/* Key Metrics Row */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 font-mono">
            {/* AI Score */}
            <div className="bg-app-surface border border-app-border p-3 rounded-lg flex flex-col gap-1">
              <span className="text-[11px] text-content-muted flex items-center gap-1">
                <Sparkles className="w-3 h-3 text-content-muted" />
                ОЦЕНКА ИИ
              </span>
              <div>
                <ScoreBadge score={trend.ai_score} size="lg" />
              </div>
            </div>

            {/* Scam Risk */}
            <div className="bg-app-surface border border-app-border p-3 rounded-lg flex flex-col gap-1">
              <span className="text-[11px] text-content-muted flex items-center gap-1">
                <Flame className="w-3 h-3 text-content-muted" />
                РИСК СКАМА
              </span>
              <div>
                <ScamBadge probability={trend.scam_probability} size="lg" />
              </div>
            </div>

            {/* Review Status */}
            <div className="bg-app-surface border border-app-border p-3 rounded-lg flex flex-col gap-1">
              <span className="text-[11px] text-content-muted flex items-center gap-1">
                <CheckCircle2 className="w-3 h-3 text-content-muted" />
                СТАТУС ПРОСМОТРА
              </span>
              <div className="mt-1">
                {trend.is_reviewed ? (
                  <span className="text-xs font-medium text-status-success bg-status-success/10 border border-status-success/20 px-2 py-0.5 rounded inline-block">
                    ПРОСМОТРЕНО
                  </span>
                ) : (
                  <span className="text-xs font-medium text-content-primary bg-app-elevated border border-app-border px-2 py-0.5 rounded inline-block">
                    НОВОЕ
                  </span>
                )}
              </div>
            </div>

            {/* Ingestion Time */}
            <div className="bg-app-surface border border-app-border p-3 rounded-lg flex flex-col gap-1">
              <span className="text-[11px] text-content-muted flex items-center gap-1">
                <Calendar className="w-3 h-3 text-content-muted" />
                ВРЕМЯ СБОРА
              </span>
              <div className="text-xs text-content-primary mt-1 font-mono">
                {formatDate(trend.parsed_date)}
              </div>
            </div>
          </div>

          {/* AI Executive Summary Card */}
          <div className="bg-app-surface border border-app-border rounded-lg p-4 flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <h3 className="text-xs font-mono font-medium text-content-primary flex items-center gap-1.5">
                <Sparkles className="w-4 h-4 text-brand-hover" />
                АНАЛИТИЧЕСКАЯ ВЫЖИМКА И КЛАССИФИКАЦИЯ ИИ
              </h3>

              {trend.ai_summary && (
                <button
                  type="button"
                  onClick={() => handleCopy(trend.ai_summary || '', 'summary')}
                  className="flex items-center gap-1 text-[11px] font-mono text-content-muted hover:text-content-primary transition-colors cursor-pointer"
                >
                  {copiedSummary ? (
                    <>
                      <Check className="w-3 h-3 text-status-success" />
                      <span className="text-status-success">Скопировано</span>
                    </>
                  ) : (
                    <>
                      <Copy className="w-3 h-3" />
                      <span>Копировать выжимку</span>
                    </>
                  )}
                </button>
              )}
            </div>

            <div className="bg-app-bg border border-app-border p-3.5 rounded-lg text-sm text-content-primary leading-relaxed font-sans">
              {trend.ai_summary ? (
                <p>{trend.ai_summary}</p>
              ) : (
                <p className="text-content-muted italic">ИИ-выжимка отсутствует для этой записи.</p>
              )}
            </div>
          </div>

          {/* Deep Analytical Report & Competitor Intelligence Section (Obsidian Vault) */}
          <div className="bg-app-surface border border-app-border rounded-lg p-4 flex flex-col gap-3">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <h3 className="text-xs font-mono font-semibold text-content-primary flex items-center gap-1.5">
                <FileText className="w-4 h-4 text-brand-hover" />
                ГЛУБОКИЙ АНАЛИЗ & OBSIDIAN VAULT
              </h3>

              <div className="flex items-center gap-2">
                {/* Obsidian Deep Research Action Button */}
                <button
                  type="button"
                  onClick={handleDeepResearch}
                  disabled={isDeepSearching || isGeneratingReport}
                  className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg transition-all shadow-sm ${
                    isDeepSearching
                      ? 'bg-app-hover text-content-muted cursor-not-allowed border border-app-border'
                      : 'bg-brand hover:bg-brand-hover text-white shadow-brand/20 cursor-pointer'
                  }`}
                  title="Найти 5 конкурентов в Web и сохранить заметку в Obsidian Vault"
                >
                  {isDeepSearching ? (
                    <>
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      <span>Поиск конкурентов & Vault...</span>
                    </>
                  ) : (
                    <>
                      <BookOpen className="w-3.5 h-3.5" />
                      <span>Анализ конкурентов (Obsidian)</span>
                    </>
                  )}
                </button>

                {detailedReport && (
                  <button
                    type="button"
                    onClick={() => handleCopy(detailedReport, 'report')}
                    className="flex items-center gap-1 text-[11px] font-mono text-content-muted hover:text-content-primary transition-colors cursor-pointer"
                  >
                    {copiedReport ? (
                      <>
                        <Check className="w-3 h-3 text-status-success" />
                        <span className="text-status-success">Скопировано</span>
                      </>
                    ) : (
                      <>
                        <Copy className="w-3 h-3" />
                        <span>Копировать отчет</span>
                      </>
                    )}
                  </button>
                )}
              </div>
            </div>

            {/* Vault Save Success Notification */}
            {vaultNotification && vaultFileName && (
              <div className="p-3 bg-status-success/10 border border-status-success/20 rounded-lg text-xs text-status-success flex items-center justify-between gap-2 animate-fadeIn">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 flex-shrink-0 text-status-success" />
                  <span>
                    {vaultNotification}: <strong className="font-mono text-content-primary underline">{vaultFileName}</strong>
                  </span>
                </div>
                <span className="font-mono text-[11px] text-content-muted">📁 TrendScanner_Vault/02_Trends/</span>
              </div>
            )}

            {reportError && (
              <div className="p-3 bg-status-danger/10 border border-status-danger/20 rounded-lg text-xs text-status-danger flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 flex-shrink-0" />
                <span>{reportError}</span>
              </div>
            )}

            {detailedReport ? (
              <div className="bg-app-bg border border-app-border p-4 rounded-lg text-sm text-content-primary font-sans leading-relaxed whitespace-pre-wrap space-y-2 select-text">
                {detailedReport}
              </div>
            ) : (
              <div className="bg-app-bg border border-dashed border-app-border p-6 rounded-lg flex flex-col items-center justify-center text-center gap-3">
                <div className="w-10 h-10 rounded-full bg-app-elevated border border-app-border flex items-center justify-center text-content-muted">
                  <FileText className="w-5 h-5" />
                </div>
                <div className="max-w-md">
                  <p className="text-xs text-content-primary font-medium">Аналитический отчет и анализ конкурентов еще не сгенерированы</p>
                  <p className="text-[11px] text-content-muted mt-1">
                    ИИ проведет глубокий поиск конкурентов в Сети, проанализирует слабые места, цены и сохранит готовую Markdown-карточку с [[Wikilinks]] в ваш Obsidian Vault.
                  </p>
                </div>
                <div className="flex items-center gap-2 flex-wrap justify-center mt-1">
                  <button
                    type="button"
                    onClick={handleDeepResearch}
                    disabled={isDeepSearching}
                    className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all shadow-sm ${
                      isDeepSearching
                        ? 'bg-app-hover text-content-muted cursor-not-allowed border border-app-border'
                        : 'bg-brand hover:bg-brand-hover text-white shadow-brand/20 cursor-pointer'
                    }`}
                  >
                    {isDeepSearching ? (
                      <>
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        <span>Поиск в Web & сохранение в Vault...</span>
                      </>
                    ) : (
                      <>
                        <BookOpen className="w-3.5 h-3.5" />
                        <span>Анализ конкурентов (Obsidian)</span>
                      </>
                    )}
                  </button>

                  <button
                    type="button"
                    onClick={handleGenerateReport}
                    disabled={isGeneratingReport}
                    className="flex items-center gap-2 px-3.5 py-2 rounded-lg text-xs font-medium bg-app-elevated hover:bg-app-hover border border-app-border text-content-primary transition-colors cursor-pointer"
                  >
                    {isGeneratingReport ? (
                      <>
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        <span>Генерация MVP-плана...</span>
                      </>
                    ) : (
                      <>
                        <Sparkles className="w-3.5 h-3.5 text-brand-hover" />
                        <span>Сгенерировать MVP план</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Original Scraped Raw Content */}
          <div className="bg-app-surface border border-app-border rounded-lg p-4 flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <h3 className="text-xs font-mono font-medium text-content-primary flex items-center gap-1.5">
                <Terminal className="w-4 h-4 text-content-muted" />
                СЫРОЙ ТЕКСТ ИСТОЧНИКА
              </h3>

              <button
                type="button"
                onClick={() => handleCopy(trend.original_text, 'original')}
                className="flex items-center gap-1 text-[11px] font-mono text-content-muted hover:text-content-primary transition-colors cursor-pointer"
              >
                {copiedOriginal ? (
                  <>
                    <Check className="w-3 h-3 text-status-success" />
                    <span className="text-status-success">Скопировано</span>
                  </>
                ) : (
                  <>
                    <Copy className="w-3 h-3" />
                    <span>Копировать текст</span>
                  </>
                )}
              </button>
            </div>

            <div className="bg-app-bg border border-app-border p-3.5 rounded-lg max-h-60 overflow-y-auto font-mono text-xs text-content-secondary whitespace-pre-wrap break-words leading-relaxed select-text">
              {trend.original_text}
            </div>
          </div>

          {/* Source & Deduplication Metadata */}
          <div className="bg-app-surface border border-app-border rounded-lg p-4 text-xs font-mono text-content-muted flex flex-col gap-1.5">
            <div className="flex items-center gap-1.5 text-content-primary font-medium mb-0.5">
              <Layers className="w-3.5 h-3.5 text-content-muted" />
              <span>ИСТОЧНИК И АУДИТ ДАННЫХ</span>
            </div>

            {trend.source_url && (
              <div className="flex items-start gap-2">
                <span className="text-content-muted w-24 flex-shrink-0">URL:</span>
                <a
                  href={trend.source_url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-content-secondary hover:text-content-primary underline underline-offset-2 break-all inline-flex items-center gap-1"
                >
                  <span>{trend.source_url}</span>
                  <ExternalLink className="w-3 h-3 flex-shrink-0" />
                </a>
              </div>
            )}

            {trend.content_hash && (
              <div className="flex items-start gap-2">
                <span className="text-content-muted w-24 flex-shrink-0">ХЕШ (SHA256):</span>
                <span className="text-content-muted break-all font-mono">{trend.content_hash}</span>
              </div>
            )}
          </div>
        </div>

        {/* Modal Footer */}
        <div className="bg-app-surface px-6 py-3.5 border-t border-app-border flex items-center justify-between gap-3 font-mono text-xs">
          {/* Left: Delete */}
          <button
            type="button"
            onClick={async () => {
              if (window.confirm(`Удалить запись #${trend.id}?`)) {
                await onDeleteTrend(trend.id);
                onClose();
              }
            }}
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-status-danger/10 hover:bg-status-danger/20 border border-status-danger/20 text-status-danger transition-colors cursor-pointer"
          >
            <Trash2 className="w-3.5 h-3.5" />
            <span>Удалить</span>
          </button>

          {/* Right: Actions */}
          <div className="flex items-center gap-2 flex-wrap">
            {/* Dislike / RLHF -1 */}
            <button
              type="button"
              onClick={async () => {
                const isCurrentlyDisliked = trend.user_feedback === -1;
                const nextScore = isCurrentlyDisliked ? 0 : -1;
                if (onFeedback) {
                  await onFeedback(trend, nextScore);
                }
              }}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-lg font-medium transition-all cursor-pointer border ${
                trend.user_feedback === -1
                  ? 'bg-amber-500/20 text-amber-400 border-amber-500/40 shadow-sm shadow-amber-500/20'
                  : 'bg-app-elevated hover:bg-app-hover border-app-border text-content-secondary hover:text-amber-500'
              }`}
              title={trend.user_feedback === -1 ? 'Убрать дизлайк (RLHF -1)' : 'Дизлайк / Мусор (RLHF -1)'}
            >
              <ThumbsDown
                className={`w-3.5 h-3.5 ${
                  trend.user_feedback === -1 ? 'fill-amber-500/30 text-amber-400' : ''
                }`}
              />
              <span>{trend.user_feedback === -1 ? 'Дизлайк (-1)' : 'Дизлайк (-1)'}</span>
            </button>

            {/* Like / RLHF +1 */}
            <button
              type="button"
              onClick={async () => {
                const isCurrentlyLiked =
                  trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked);
                const nextScore = isCurrentlyLiked ? 0 : 1;
                if (onFeedback) {
                  await onFeedback(trend, nextScore);
                }
              }}
              className={`flex items-center gap-1.5 px-3.5 py-2 rounded-lg font-medium transition-all cursor-pointer border ${
                trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked)
                  ? 'bg-status-danger/20 text-status-danger border-status-danger/40 shadow-sm shadow-status-danger/20 font-semibold'
                  : 'bg-app-elevated hover:bg-app-hover border-app-border text-content-secondary hover:text-status-danger'
              }`}
              title={
                trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked)
                  ? 'Убрать лайк (RLHF +1)'
                  : 'Лайк (RLHF +1)'
              }
            >
              <Heart
                className={`w-3.5 h-3.5 ${
                  trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked)
                    ? 'fill-status-danger text-status-danger'
                    : ''
                }`}
              />
              <span>
                {trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked)
                  ? 'В избранном (+1)'
                  : 'Лайк (+1)'}
              </span>
            </button>

            {trend.source_url && (
              <a
                href={trend.source_url}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-app-elevated hover:bg-app-hover border border-app-border text-content-primary transition-colors cursor-pointer"
              >
                <ExternalLink className="w-3.5 h-3.5 text-content-muted" />
                <span>Оригинал</span>
              </a>
            )}

            <button
              type="button"
              onClick={async () => {
                await onToggleReview(trend);
              }}
              className={`flex items-center gap-1.5 px-4 py-2 rounded-lg font-medium transition-all cursor-pointer ${
                trend.is_reviewed
                  ? 'bg-app-elevated hover:bg-app-hover border border-app-border text-content-primary'
                  : 'bg-brand hover:bg-brand-hover text-white shadow-sm shadow-brand/20'
              }`}
            >
              <CheckCircle2 className="w-4 h-4" />
              <span>{trend.is_reviewed ? 'Снять отметку' : 'Пометить прочитанным'}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
