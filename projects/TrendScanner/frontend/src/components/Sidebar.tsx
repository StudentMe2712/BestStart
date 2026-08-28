import React from 'react';
import {
  Search,
  SlidersHorizontal,
  RotateCcw,
  Flame,
  ShieldAlert,
  Sparkles,
  Inbox,
  Heart,
  Archive,
  X,
} from 'lucide-react';
import { FilterState } from '../types';

interface SidebarProps {
  filters: FilterState;
  onFilterChange: (newFilters: Partial<FilterState>) => void;
  onResetFilters: () => void;
  activeFilterCount: number;
}

export const Sidebar: React.FC<SidebarProps> = ({
  filters,
  onFilterChange,
  onResetFilters,
  activeFilterCount,
}) => {
  // Determine active view mode
  const activeNav =
    filters.tab === 'database'
      ? 'database'
      : filters.tab === 'liked'
      ? 'liked'
      : 'inbox';

  const navItems = [
    { key: 'inbox', label: 'Входящие', icon: Inbox },
    { key: 'liked', label: 'Избранное', icon: Heart },
    { key: 'database', label: '🗄️ База трендов', icon: Archive },
  ];

  const handleNavClick = (key: string) => {
    if (key === 'inbox') {
      onFilterChange({ tab: 'inbox', status: 'all', skip: 0 });
    } else if (key === 'liked') {
      onFilterChange({ tab: 'liked', status: 'all', skip: 0 });
    } else if (key === 'database') {
      onFilterChange({ tab: 'database', status: 'all', skip: 0 });
    }
  };

  return (
    <aside className="w-full lg:w-64 bg-app-surface border-r border-app-border p-4 flex flex-col gap-4 flex-shrink-0 text-sm font-mono select-none">
      {/* Header */}
      <div className="flex items-center justify-between pb-3 border-b border-app-border">
        <div className="flex items-center gap-2 text-content-primary font-semibold tracking-wide text-xs">
          <SlidersHorizontal className="w-3.5 h-3.5 text-content-muted" />
          <span>НАВИГАЦИЯ</span>
          {activeFilterCount > 0 && (
            <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-app-elevated text-content-secondary border border-app-border">
              {activeFilterCount}
            </span>
          )}
        </div>

        {activeFilterCount > 0 && (
          <button
            type="button"
            onClick={onResetFilters}
            className="flex items-center gap-1 text-xs text-content-muted hover:text-content-primary transition-colors cursor-pointer"
            title="Сбросить все фильтры"
          >
            <RotateCcw className="w-3 h-3" />
            <span>Сброс</span>
          </button>
        )}
      </div>

      {/* 1. Single Main Navigation Block */}
      <div className="flex flex-col gap-1">
        <div className="flex flex-col bg-app-bg p-1 rounded-lg border border-app-border gap-1">
          {navItems.map((item) => {
            const isSelected = activeNav === item.key;
            const Icon = item.icon;
            return (
              <button
                key={item.key}
                type="button"
                onClick={() => handleNavClick(item.key)}
                className={`w-full py-2 px-3 text-xs rounded-md transition-all flex items-center gap-2.5 cursor-pointer ${
                  isSelected
                    ? item.key === 'liked'
                      ? 'bg-status-danger/15 text-status-danger border border-status-danger/30 font-semibold shadow-sm'
                      : item.key === 'inbox'
                      ? 'bg-brand text-white font-semibold shadow-sm'
                      : 'bg-app-elevated text-content-primary border border-app-border font-semibold shadow-sm'
                    : 'text-content-secondary hover:text-content-primary hover:bg-app-elevated border border-transparent'
                }`}
              >
                <Icon
                  className={`w-3.5 h-3.5 ${
                    isSelected && item.key === 'liked'
                      ? 'fill-status-danger/20 text-status-danger'
                      : isSelected && item.key === 'inbox'
                      ? 'text-white'
                      : isSelected
                      ? 'text-content-primary'
                      : 'text-content-muted'
                  }`}
                />
                <span>{item.label}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* 2. Search Query Input */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[11px] font-semibold text-content-muted uppercase tracking-wider flex items-center gap-1.5">
          <Search className="w-3 h-3 text-content-muted" />
          <span>ПОИСК ПО БАЗЕ & ВХОДЯЩИМ</span>
        </label>
        <div className="relative">
          <input
            type="text"
            value={filters.searchQuery}
            onChange={(e) => onFilterChange({ searchQuery: e.target.value, skip: 0 })}
            placeholder="Поиск по теме, тексту, источнику..."
            className="w-full bg-app-bg border border-app-border focus:border-brand rounded-md px-3 py-1.5 text-xs text-content-primary placeholder-content-muted outline-none transition-all pr-7 shadow-inner"
          />
          {filters.searchQuery && (
            <button
              type="button"
              onClick={() => onFilterChange({ searchQuery: '', skip: 0 })}
              className="absolute right-2 top-2 text-content-muted hover:text-content-primary cursor-pointer"
              title="Очистить поиск"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* 3. Confirmed Trends Only Toggle */}
      <div className="flex flex-col gap-1.5">
        <div
          onClick={() => onFilterChange({ onlyTrends: !filters.onlyTrends, skip: 0 })}
          className={`flex items-center justify-between p-2.5 rounded-lg border cursor-pointer select-none transition-all ${
            filters.onlyTrends
              ? 'bg-brand/10 border-brand/40 text-content-primary'
              : 'bg-app-bg border-app-border text-content-secondary hover:border-content-muted'
          }`}
        >
          <div className="flex items-center gap-2 text-xs font-semibold">
            <Flame
              className={`w-3.5 h-3.5 ${
                filters.onlyTrends ? 'text-brand-hover fill-brand/20' : 'text-content-muted'
              }`}
            />
            <span>ТОЛЬКО ТРЕНДЫ</span>
          </div>

          <div
            className={`w-7 h-3.5 rounded-full transition-colors relative flex items-center px-0.5 ${
              filters.onlyTrends ? 'bg-brand' : 'bg-app-elevated border border-app-border'
            }`}
          >
            <div
              className={`w-2.5 h-2.5 rounded-full transition-transform ${
                filters.onlyTrends ? 'translate-x-3.5 bg-white' : 'translate-x-0 bg-content-muted'
              }`}
            />
          </div>
        </div>
      </div>

      {/* 4. Minimum AI Score Filter */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center justify-between">
          <label className="text-[11px] font-semibold text-content-muted uppercase tracking-wider flex items-center gap-1.5">
            <Sparkles className="w-3 h-3 text-content-muted" />
            <span>МИН. СКОР ИИ</span>
          </label>
          <span className="text-[11px] text-content-secondary font-medium">
            {filters.minScore !== null ? `≥ ${filters.minScore}` : 'Любой'}
          </span>
        </div>

        <div className="grid grid-cols-4 bg-app-bg p-1 rounded-lg border border-app-border gap-1">
          {[
            { label: 'Все', value: null },
            { label: '≥ 5', value: 5 },
            { label: '≥ 7', value: 7 },
            { label: '≥ 8', value: 8 },
          ].map((item) => {
            const isSelected = filters.minScore === item.value;
            return (
              <button
                key={item.label}
                type="button"
                onClick={() => onFilterChange({ minScore: item.value, skip: 0 })}
                className={`py-1.5 text-xs rounded-md transition-colors cursor-pointer ${
                  isSelected
                    ? 'bg-brand text-white border border-brand shadow-sm font-semibold'
                    : 'text-content-secondary hover:text-content-primary'
                }`}
              >
                {item.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* 5. Max Scam Risk Filter */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center justify-between">
          <label className="text-[11px] font-semibold text-content-muted uppercase tracking-wider flex items-center gap-1.5">
            <ShieldAlert className="w-3.5 h-3.5 text-content-muted" />
            <span>МАКС. РИСК СКАМА</span>
          </label>
          <span className="text-[11px] text-content-secondary font-medium">
            {filters.maxScam !== null ? `≤ ${filters.maxScam}%` : 'Любой'}
          </span>
        </div>

        <div className="grid grid-cols-3 bg-app-bg p-1 rounded-lg border border-app-border gap-1">
          {[
            { label: 'Все', value: null },
            { label: '≤ 50%', value: 50 },
            { label: '≤ 20%', value: 20 },
          ].map((item) => {
            const isSelected = filters.maxScam === item.value;
            return (
              <button
                key={item.label}
                type="button"
                onClick={() => onFilterChange({ maxScam: item.value, skip: 0 })}
                className={`py-1.5 text-xs rounded-md transition-colors cursor-pointer ${
                  isSelected
                    ? 'bg-brand text-white border border-brand shadow-sm font-semibold'
                    : 'text-content-secondary hover:text-content-primary'
                }`}
              >
                {item.label}
              </button>
            );
          })}
        </div>
      </div>
    </aside>
  );
};
