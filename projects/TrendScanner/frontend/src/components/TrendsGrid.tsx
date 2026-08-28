import React from 'react';
import {
  ExternalLink,
  Eye,
  CheckCircle2,
  Circle,
  Clock,
  Inbox,
  Database,
  Heart,
  ChevronLeft,
  ChevronRight,
  Trash2,
  Sparkles,
} from 'lucide-react';
import { Trend } from '../types';
import { ScoreBadge, ScamBadge, SourceBadge, TrendIndicator } from './Badges';

interface TrendsGridProps {
  trends: Trend[];
  loading: boolean;
  currentTab?: 'inbox' | 'liked' | 'database' | 'all' | string;
  onSelectTrend: (trend: Trend) => void;
  onToggleReview: (trend: Trend) => Promise<void>;
  onToggleLike: (trend: Trend) => Promise<void>;
  onDeleteTrend: (trendId: number) => Promise<void>;
  currentPage: number;
  pageSize: number;
  hasMore: boolean;
  onPageChange: (page: number) => void;
}

export const TrendsGrid: React.FC<TrendsGridProps> = ({
  trends,
  loading,
  currentTab = 'inbox',
  onSelectTrend,
  onToggleReview,
  onToggleLike,
  onDeleteTrend,
  currentPage,
  pageSize,
  hasMore,
  onPageChange,
}) => {
  // Format timestamp helper in Russian locale
  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return '—';
    try {
      const dt = new Date(dateStr);
      if (isNaN(dt.getTime())) return dateStr;
      return dt.toLocaleString('ru-RU', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <div className="flex-1 flex flex-col min-w-0 bg-app-bg overflow-hidden">
      {/* Table Container */}
      <div className="flex-1 overflow-x-auto overflow-y-auto">
        <table className="w-full text-left border-collapse font-sans text-xs">
          {/* Table Header */}
          <thead className="bg-app-surface sticky top-0 z-10 border-b border-app-border font-sans select-none">
            <tr>
              <th className="py-3 px-4 w-32 text-xs font-medium text-content-muted">Время / Статус</th>
              <th className="py-3 px-4 w-36 text-xs font-medium text-content-muted">Источник</th>
              <th className="py-3 px-4 min-w-[280px] text-xs font-medium text-content-muted">Тренд и Выжимка ИИ</th>
              <th className="py-3 px-4 w-28 text-center text-xs font-medium text-content-muted">Скор ИИ</th>
              <th className="py-3 px-4 w-32 text-center text-xs font-medium text-content-muted">Риск скама</th>
              <th className="py-3 px-4 w-32 text-right text-xs font-medium text-content-muted">Действия</th>
            </tr>
          </thead>

          {/* Table Body */}
          <tbody className="divide-y divide-app-border">
            {loading && trends.length === 0 ? (
              // Loading Skeletons
              Array.from({ length: 8 }).map((_, idx) => (
                <tr key={idx} className="animate-pulse bg-app-surface/30 border-b border-app-border">
                  <td className="py-3.5 px-4 align-middle">
                    <div className="h-3.5 bg-app-elevated rounded w-20 mb-1" />
                    <div className="h-3 bg-app-surface rounded w-12" />
                  </td>
                  <td className="py-3.5 px-4 align-middle">
                    <div className="h-5 bg-app-elevated rounded w-24" />
                  </td>
                  <td className="py-3.5 px-4 align-middle">
                    <div className="h-4 bg-app-elevated rounded w-3/4 mb-1.5" />
                    <div className="h-3 bg-app-surface rounded w-full" />
                  </td>
                  <td className="py-3.5 px-4 align-middle text-center">
                    <div className="h-5 bg-app-elevated rounded w-14 mx-auto" />
                  </td>
                  <td className="py-3.5 px-4 align-middle text-center">
                    <div className="h-5 bg-app-elevated rounded w-16 mx-auto" />
                  </td>
                  <td className="py-3.5 px-4 align-middle text-right">
                    <div className="h-6 bg-app-elevated rounded w-20 ml-auto" />
                  </td>
                </tr>
              ))
            ) : trends.length === 0 ? (
              // Empty State tailored to currentTab
              <tr>
                <td colSpan={6} className="py-16 text-center text-content-muted font-mono">
                  <div className="flex flex-col items-center justify-center gap-2">
                    {currentTab === 'database' ? (
                      <>
                        <Database className="w-10 h-10 text-content-muted stroke-[1.5]" />
                        <div className="text-sm text-content-primary font-medium">База трендов пуста</div>
                        <div className="text-xs text-content-muted max-w-md leading-relaxed">
                          В Базе трендов пока нет сохраненных исторических сканов. Они появятся здесь после следующего цикла сканирования.
                        </div>
                      </>
                    ) : currentTab === 'liked' ? (
                      <>
                        <Heart className="w-10 h-10 text-content-muted stroke-[1.5]" />
                        <div className="text-sm text-content-primary font-medium">Избранное пусто</div>
                        <div className="text-xs text-content-muted max-w-md leading-relaxed">
                          У вас пока нет сохраненных трендов. Отмечайте интересные записи сердечком во Входящих или Базе трендов.
                        </div>
                      </>
                    ) : currentTab === 'inbox' ? (
                      <>
                        <Inbox className="w-10 h-10 text-content-muted stroke-[1.5]" />
                        <div className="text-sm text-content-primary font-medium">Входящие пусты (Inbox Zero 🎉)</div>
                        <div className="text-xs text-content-muted max-w-md leading-relaxed">
                          Все тренды обработаны или перенесены в избранное. Новые записи появятся при следующем сканировании.
                        </div>
                      </>
                    ) : (
                      <>
                        <Inbox className="w-10 h-10 text-content-muted stroke-[1.5]" />
                        <div className="text-sm text-content-primary font-medium">Тренды не найдены</div>
                        <div className="text-xs text-content-muted max-w-sm">
                          Измените параметры фильтров или запустите сканирование в верхней панели.
                        </div>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ) : (
              // Trends Rows
              trends.map((trend) => {
                const isConfirmed = trend.is_trend;
                const isReviewed = trend.is_reviewed;

                return (
                  <tr
                    key={trend.id}
                    className={`group hover:bg-app-hover transition-colors border-b border-app-border ${
                      isReviewed ? 'opacity-70 hover:opacity-100 bg-app-bg' : 'bg-app-bg/60'
                    }`}
                  >
                    {/* Time & Review Status */}
                    <td className="py-3.5 px-4 align-middle">
                      <div className="flex flex-col gap-1">
                        <span className="flex items-center gap-1 font-mono text-[11px] text-content-muted">
                          <Clock className="w-3 h-3 text-content-muted flex-shrink-0" />
                          <span>{formatDate(trend.parsed_date)}</span>
                        </span>
                        
                        <div className="flex items-center gap-1">
                          {isReviewed ? (
                            <span className="inline-flex items-center gap-1 text-[10px] font-mono text-status-success bg-status-success/10 px-1.5 py-0.5 rounded border border-status-success/20">
                              <CheckCircle2 className="w-2.5 h-2.5" />
                              ПРОСМОТРЕНО
                            </span>
                          ) : trend.is_new ? (
                            <span className="inline-flex items-center gap-1 text-[10px] font-mono text-brand-hover bg-brand/10 px-1.5 py-0.5 rounded border border-brand/30 font-semibold animate-pulse">
                              <Sparkles className="w-2.5 h-2.5 text-brand-hover" />
                              СВЕЖИЙ
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-[10px] font-mono text-content-primary bg-app-elevated px-1.5 py-0.5 rounded border border-app-border">
                              <Circle className="w-2 h-2 fill-content-muted text-content-muted" />
                              НОВОЕ
                            </span>
                          )}
                        </div>
                      </div>
                    </td>

                    {/* Source */}
                    <td className="py-3.5 px-4 align-middle">
                      <SourceBadge
                        sourceName={trend.source_name}
                        sourceType={trend.source_type}
                      />
                    </td>

                    {/* Trend Name & AI Summary */}
                    <td className="py-3.5 px-4 align-middle">
                      <div className="flex flex-col gap-1">
                        <div className="flex items-center gap-2 flex-wrap">
                          <TrendIndicator isTrend={isConfirmed} size="sm" />
                          <button
                            type="button"
                            onClick={() => onSelectTrend(trend)}
                            className="text-left font-medium text-content-primary hover:text-brand-hover transition-colors text-sm group-hover:underline underline-offset-2 leading-snug cursor-pointer"
                          >
                            {trend.trend_name || 'Неопределенная тема'}
                          </button>
                        </div>

                        {/* Summary / Original Excerpt */}
                        <p className="text-content-secondary text-xs line-clamp-2 leading-relaxed">
                          {trend.ai_summary || trend.original_text}
                        </p>
                      </div>
                    </td>

                    {/* AI Score */}
                    <td className="py-3.5 px-4 align-middle text-center">
                      <ScoreBadge score={trend.ai_score} size="sm" />
                    </td>

                    {/* Scam Risk */}
                    <td className="py-3.5 px-4 align-middle text-center">
                      <ScamBadge probability={trend.scam_probability} size="sm" />
                    </td>

                    {/* Actions */}
                    <td className="py-3.5 px-4 align-middle text-right">
                      <div className="flex items-center justify-end gap-2">
                        {/* Toggle Like / Inbox Zero */}
                        <button
                          type="button"
                          onClick={() => onToggleLike(trend)}
                          className={`p-1 transition-colors cursor-pointer ${
                            trend.is_liked
                              ? 'text-status-danger hover:text-status-danger'
                              : 'text-content-muted hover:text-status-danger'
                          }`}
                          title={trend.is_liked ? 'Убрать из понравившегося' : 'Понравилось (сохранить в избранное)'}
                        >
                          <Heart
                            className={`w-4 h-4 transition-transform active:scale-125 ${
                              trend.is_liked ? 'fill-status-danger text-status-danger' : ''
                            }`}
                          />
                        </button>

                        {/* Toggle Reviewed */}
                        <button
                          type="button"
                          onClick={() => onToggleReview(trend)}
                          className={`p-1 transition-colors cursor-pointer ${
                            isReviewed
                              ? 'text-status-success hover:text-status-success'
                              : 'text-content-muted hover:text-content-primary'
                          }`}
                          title={isReviewed ? 'Сделать непросмотренным' : 'Пометить как прочитанное'}
                        >
                          <CheckCircle2
                            className={`w-4 h-4 ${isReviewed ? 'fill-status-success/20' : ''}`}
                          />
                        </button>

                        {/* View Details */}
                        <button
                          type="button"
                          onClick={() => onSelectTrend(trend)}
                          className="p-1 text-content-muted hover:text-brand-hover transition-colors cursor-pointer"
                          title="Подробный анализ ИИ"
                        >
                          <Eye className="w-4 h-4" />
                        </button>

                        {/* Open External Link */}
                        {trend.source_url && (
                          <a
                            href={trend.source_url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="p-1 text-content-muted hover:text-content-primary transition-colors"
                            title="Открыть оригинальный пост"
                          >
                            <ExternalLink className="w-4 h-4" />
                          </a>
                        )}

                        {/* Delete */}
                        <button
                          type="button"
                          onClick={() => onDeleteTrend(trend.id)}
                          className="p-1 text-content-muted hover:text-status-danger transition-colors cursor-pointer"
                          title="Удалить запись"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <div className="bg-app-surface border-t border-app-border px-4 py-2.5 flex items-center justify-between font-mono text-xs text-content-muted select-none">
        <div className="flex items-center gap-2">
          <span>
            Страница <strong className="text-content-primary">{currentPage}</strong>
          </span>
          <span className="text-app-border">•</span>
          <span>Показано {trends.length} записей (лимит {pageSize})</span>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            disabled={currentPage <= 1 || loading}
            onClick={() => onPageChange(currentPage - 1)}
            className="flex items-center gap-1 px-2.5 py-1 rounded-md bg-app-elevated hover:bg-app-hover disabled:opacity-40 disabled:hover:bg-app-elevated border border-app-border text-content-primary transition-colors cursor-pointer disabled:cursor-not-allowed"
          >
            <ChevronLeft className="w-3.5 h-3.5" />
            <span>Назад</span>
          </button>

          <button
            type="button"
            disabled={!hasMore || loading}
            onClick={() => onPageChange(currentPage + 1)}
            className="flex items-center gap-1 px-2.5 py-1 rounded-md bg-app-elevated hover:bg-app-hover disabled:opacity-40 disabled:hover:bg-app-elevated border border-app-border text-content-primary transition-colors cursor-pointer disabled:cursor-not-allowed"
          >
            <span>Вперед</span>
            <ChevronRight className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
};
