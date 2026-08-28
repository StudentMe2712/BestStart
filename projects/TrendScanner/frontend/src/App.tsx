import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { apiClient } from './api/client';
import { FilterState, Source, SystemStatus, Trend } from './types';
import { Topbar } from './components/Topbar';
import { Sidebar } from './components/Sidebar';
import { TrendsGrid } from './components/TrendsGrid';
import { TrendDetailModal } from './components/TrendDetailModal';
import { SourcesModal } from './components/SourcesModal';
import { CheckCircle2, AlertCircle, Info, X } from 'lucide-react';

interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error' | 'info';
}

const DEFAULT_FILTERS: FilterState = {
  tab: 'inbox',
  status: 'all',
  minScore: null,
  maxScam: null,
  sourceId: null,
  onlyTrends: false,
  searchQuery: '',
  skip: 0,
  limit: 50,
};

export const App: React.FC = () => {
  // Application Data States
  const [trends, setTrends] = useState<Trend[]>([]);
  const [sources, setSources] = useState<Source[]>([]);
  const [systemStatus, setSystemStatus] = useState<SystemStatus | null>(null);

  // Filter & Pagination State
  const [filters, setFilters] = useState<FilterState>(DEFAULT_FILTERS);

  // UI / Modal States
  const [loading, setLoading] = useState<boolean>(true);
  const [isScanning, setIsScanning] = useState<boolean>(false);
  const [selectedTrend, setSelectedTrend] = useState<Trend | null>(null);
  const [isSourcesModalOpen, setIsSourcesModalOpen] = useState<boolean>(false);
  const [toast, setToast] = useState<Toast | null>(null);

  // Show toast notification
  const showToast = useCallback(
    (message: string, type: 'success' | 'error' | 'info' = 'info') => {
      const id = Date.now();
      setToast({ id, message, type });
      setTimeout(() => {
        setToast((current) => (current?.id === id ? null : current));
      }, 5000);
    },
    []
  );

  // Fetch System Status
  const fetchStatus = useCallback(async () => {
    try {
      const status = await apiClient.getSystemStatus();
      setSystemStatus(status);
    } catch (err: any) {
      console.error('Не удалось загрузить статус системы:', err);
    }
  }, []);

  // Fetch Sources List
  const fetchSources = useCallback(async () => {
    try {
      const data = await apiClient.getSources(false);
      setSources(data);
    } catch (err: any) {
      console.error('Не удалось загрузить список источников:', err);
    }
  }, []);

  // Fetch Trends with Inbox Zero tabs
  const fetchTrends = useCallback(async () => {
    try {
      setLoading(true);
      const data = await apiClient.getTrends({
        tab: filters.tab,
        skip: filters.skip,
        limit: filters.limit,
        minScore: filters.minScore,
        maxScam: filters.maxScam,
        status: filters.status,
        sourceId: filters.sourceId,
        onlyTrends: filters.onlyTrends,
        searchQuery: filters.searchQuery,
      });
      setTrends(data);
    } catch (err: any) {
      console.error('Ошибка при получении трендов:', err);
      showToast(err?.message || 'Ошибка загрузки трендов с бэкенда', 'error');
    } finally {
      setLoading(false);
    }
  }, [
    filters.tab,
    filters.skip,
    filters.limit,
    filters.minScore,
    filters.maxScam,
    filters.status,
    filters.sourceId,
    filters.onlyTrends,
    filters.searchQuery,
    showToast,
  ]);

  // Initial Load & Polling
  useEffect(() => {
    fetchStatus();
    fetchSources();
    fetchTrends();

    // Auto-refresh status periodically
    const interval = setInterval(() => {
      fetchStatus();
    }, 30000);

    return () => clearInterval(interval);
  }, [fetchStatus, fetchSources, fetchTrends]);

  // Client-side search filtering on title/summary/content
  const filteredTrends = useMemo(() => {
    if (!filters.searchQuery.trim()) {
      return trends;
    }
    const q = filters.searchQuery.toLowerCase();
    return trends.filter((item) => {
      const nameMatch = item.trend_name?.toLowerCase().includes(q);
      const summaryMatch = item.ai_summary?.toLowerCase().includes(q);
      const textMatch = item.original_text.toLowerCase().includes(q);
      const sourceMatch = item.source_name?.toLowerCase().includes(q);
      return nameMatch || summaryMatch || textMatch || sourceMatch;
    });
  }, [trends, filters.searchQuery]);

  // Calculate active filter count for badge
  const activeFilterCount = useMemo(() => {
    let count = 0;
    if (filters.tab !== 'inbox') count++;
    if (filters.status !== 'all') count++;
    if (filters.minScore !== null) count++;
    if (filters.maxScam !== null) count++;
    if (filters.onlyTrends) count++;
    if (filters.searchQuery.trim() !== '') count++;
    return count;
  }, [filters]);

  // Handle Filter Change
  const handleFilterChange = (newFilters: Partial<FilterState>) => {
    setFilters((prev) => ({ ...prev, ...newFilters }));
  };

  // Reset Filters
  const handleResetFilters = () => {
    setFilters(DEFAULT_FILTERS);
  };

  // Trigger Manual Scan
  const handleScan = async () => {
    if (isScanning) return;
    try {
      setIsScanning(true);
      showToast('Запуск пайплайна скрапинга по всем активным источникам...', 'info');
      const response = await apiClient.triggerManualScan();

      if (response.status === 'busy') {
        showToast('Пайплайн уже выполняет цикл сканирования.', 'info');
      } else if (response.errors && response.errors.length > 0) {
        showToast(
          `Сканирование завершено. Найдено новых трендов: ${response.new_trends_found} (${response.errors.length} предупреждений)`,
          'info'
        );
      } else {
        showToast(
          `Сканирование успешно! Опрошено источников: ${response.scanned_sources}, новых трендов: ${response.new_trends_found}`,
          'success'
        );
      }

      // Refresh data
      await Promise.all([fetchTrends(), fetchStatus(), fetchSources()]);
    } catch (err: any) {
      showToast(err?.message || 'Сбой при запуске сканирования', 'error');
    } finally {
      setIsScanning(false);
    }
  };

  // RLHF Feedback (Likes +1, Dislikes -1, Neutral 0) with Optimistic UI
  const handleFeedback = async (trend: Trend, score: number) => {
    // Optimistic local update
    if (filters.tab === 'inbox' && (score === 1 || score === -1)) {
      setTrends((prev) => prev.filter((t) => t.id !== trend.id));
    } else if (filters.tab === 'liked' && score !== 1) {
      setTrends((prev) => prev.filter((t) => t.id !== trend.id));
    } else {
      // In 'database' or 'all' tab: update user_feedback and is_liked in place
      setTrends((prev) =>
        prev.map((t) =>
          t.id === trend.id
            ? { ...t, user_feedback: score, is_liked: score === 1 }
            : t
        )
      );
    }

    if (selectedTrend?.id === trend.id) {
      setSelectedTrend((prev) =>
        prev ? { ...prev, user_feedback: score, is_liked: score === 1 } : null
      );
    }

    try {
      await apiClient.setFeedback(trend.id, score);
      const toastText =
        score === 1
          ? `Запись #${trend.id} сохранена в «Избранное» (RLHF +1).`
          : score === -1
          ? `Запись #${trend.id} скрыта с оценкой «Дизлайк» (RLHF -1).`
          : `Оценка тренда #${trend.id} сброшена.`;
      showToast(toastText, 'success');
      fetchStatus();
    } catch (err: any) {
      showToast(err?.message || 'Не удалось сохранить оценку тренда', 'error');
      fetchTrends();
    }
  };

  // Toggle Like / Favorite for compatibility
  const handleToggleLike = async (trend: Trend) => {
    const isCurrentlyLiked =
      trend.user_feedback === 1 || (trend.user_feedback === undefined && !!trend.is_liked);
    await handleFeedback(trend, isCurrentlyLiked ? 0 : 1);
  };

  // Toggle Review Status
  const handleToggleReview = async (trend: Trend) => {
    const nextStatus = !trend.is_reviewed;
    try {
      await apiClient.reviewTrend(trend.id, nextStatus);

      // Update state locally
      setTrends((prev) =>
        prev.map((t) => (t.id === trend.id ? { ...t, is_reviewed: nextStatus } : t))
      );

      if (selectedTrend?.id === trend.id) {
        setSelectedTrend((prev) => (prev ? { ...prev, is_reviewed: nextStatus } : null));
      }

      showToast(
        `Запись #${trend.id} помечена как ${nextStatus ? '«Просмотрено»' : '«Новое»'}.`,
        'success'
      );
      fetchStatus();
    } catch (err: any) {
      showToast(err?.message || 'Не удалось обновить статус просмотра', 'error');
    }
  };

  // Delete Trend
  const handleDeleteTrend = async (trendId: number) => {
    try {
      await apiClient.deleteTrend(trendId);
      setTrends((prev) => prev.filter((t) => t.id !== trendId));
      if (selectedTrend?.id === trendId) {
        setSelectedTrend(null);
      }
      showToast(`Запись #${trendId} удалена.`, 'info');
      fetchStatus();
    } catch (err: any) {
      showToast(err?.message || 'Не удалось удалить запись', 'error');
    }
  };

  // Pagination Handlers
  const handlePageChange = (newPage: number) => {
    const skip = (newPage - 1) * filters.limit;
    handleFilterChange({ skip });
  };

  const currentPage = Math.floor(filters.skip / filters.limit) + 1;
  const hasMore = trends.length === filters.limit;

  return (
    <div className="min-h-screen bg-app-bg text-content-primary flex flex-col font-sans selection:bg-brand selection:text-white">
      {/* Top Navigation Bar */}
      <Topbar
        systemStatus={systemStatus}
        onScan={handleScan}
        isScanning={isScanning}
        onRefresh={() => {
          fetchTrends();
          fetchStatus();
          fetchSources();
        }}
        onOpenSources={() => {
          fetchSources();
          setIsSourcesModalOpen(true);
        }}
      />

      {/* Main Content Layout */}
      <div className="flex-1 flex flex-col lg:flex-row min-h-0">
        {/* Filter Sidebar */}
        <Sidebar
          filters={filters}
          onFilterChange={handleFilterChange}
          onResetFilters={handleResetFilters}
          activeFilterCount={activeFilterCount}
        />

        {/* Trends Grid */}
        <main className="flex-1 flex flex-col min-w-0 bg-app-bg">
          <TrendsGrid
            trends={filteredTrends}
            loading={loading}
            currentTab={filters.tab}
            onSelectTrend={(trend) => setSelectedTrend(trend)}
            onToggleReview={handleToggleReview}
            onToggleLike={handleToggleLike}
            onFeedback={handleFeedback}
            onDeleteTrend={handleDeleteTrend}
            currentPage={currentPage}
            pageSize={filters.limit}
            hasMore={hasMore}
            onPageChange={handlePageChange}
          />
        </main>
      </div>

      {/* Detail Modal */}
      <TrendDetailModal
        trend={selectedTrend}
        isOpen={Boolean(selectedTrend)}
        onClose={() => setSelectedTrend(null)}
        onToggleReview={handleToggleReview}
        onFeedback={handleFeedback}
        onDeleteTrend={handleDeleteTrend}
      />

      {/* Sources Monitoring Modal */}
      <SourcesModal
        isOpen={isSourcesModalOpen}
        onClose={() => setIsSourcesModalOpen(false)}
        sources={sources}
        onAddSource={async (newSrc) => {
          await apiClient.createSource(newSrc);
          showToast(`Источник "${newSrc.name}" успешно добавлен в Радар!`, 'success');
          fetchSources();
          fetchStatus();
        }}
        onToggleActive={async (srcId, currentActive) => {
          await apiClient.updateSource(srcId, { is_active: !currentActive });
          fetchSources();
          fetchStatus();
        }}
        onDeleteSource={async (srcId) => {
          await apiClient.deleteSource(srcId);
          showToast(`Источник #${srcId} удален.`, 'info');
          fetchSources();
          fetchStatus();
        }}
        onScan={handleScan}
        isScanning={isScanning}
      />

      {/* Toast Notification Container */}
      {toast && (
        <div className="fixed bottom-5 right-5 z-50 animate-slideUp font-mono">
          <div className="flex items-center gap-2.5 px-4 py-3 rounded-lg border shadow-2xl backdrop-blur-md text-xs bg-app-elevated border-app-border text-content-primary">
            {toast.type === 'success' && <CheckCircle2 className="w-4 h-4 text-status-success" />}
            {toast.type === 'error' && <AlertCircle className="w-4 h-4 text-status-danger" />}
            {toast.type === 'info' && <Info className="w-4 h-4 text-brand-hover" />}

            <span className="font-sans font-medium">{toast.message}</span>

            <button
              type="button"
              onClick={() => setToast(null)}
              className="ml-2 text-content-muted hover:text-content-primary transition-colors cursor-pointer"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default App;
