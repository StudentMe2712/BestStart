import React, { useState, useEffect, useRef } from 'react';
import {
  Activity,
  Play,
  Pause,
  RotateCw,
  Inbox,
  Heart,
  Timer,
} from 'lucide-react';
import { SystemStatus } from '../types';

interface TopbarProps {
  systemStatus: SystemStatus | null;
  onScan: () => Promise<void>;
  isScanning: boolean;
  onRefresh: () => void;
  onOpenSources?: () => void;
  isPaused?: boolean;
  onTogglePause?: () => Promise<void>;
  isPausing?: boolean;
}

function formatCountdown(seconds: number): { totalSeconds: number; formatted: string } {
  if (seconds <= 0) {
    return { totalSeconds: 0, formatted: '00:00' };
  }
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;

  const mm = String(minutes).padStart(2, '0');
  const ss = String(secs).padStart(2, '0');

  if (hours > 0) {
    const hh = String(hours).padStart(2, '0');
    return { totalSeconds: seconds, formatted: `${hh}:${mm}:${ss}` };
  }
  return { totalSeconds: seconds, formatted: `${mm}:${ss}` };
}

function calculateTimeLeft(targetTimeStr?: string | null): { totalSeconds: number; formatted: string } {
  if (!targetTimeStr) {
    return { totalSeconds: 0, formatted: '--:--' };
  }
  try {
    const normalized = targetTimeStr.includes('T') ? targetTimeStr : targetTimeStr.replace(' ', 'T') + 'Z';
    const targetDt = new Date(normalized);
    if (isNaN(targetDt.getTime())) {
      const fallbackDt = new Date(targetTimeStr);
      if (isNaN(fallbackDt.getTime())) return { totalSeconds: 0, formatted: '--:--' };
      const diff = Math.max(0, Math.floor((fallbackDt.getTime() - Date.now()) / 1000));
      return formatCountdown(diff);
    }
    const diff = Math.max(0, Math.floor((targetDt.getTime() - Date.now()) / 1000));
    return formatCountdown(diff);
  } catch {
    return { totalSeconds: 0, formatted: '--:--' };
  }
}

function formatLastScanTime(timeStr?: string | null): string {
  if (!timeStr) return 'не проводился';
  try {
    const normalized = timeStr.includes('T') ? timeStr : timeStr.replace(' ', 'T') + 'Z';
    const dt = new Date(normalized);
    if (isNaN(dt.getTime())) {
      const directDt = new Date(timeStr);
      if (isNaN(directDt.getTime())) return timeStr;
      return directDt.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    }

    const now = new Date();
    const isToday = dt.toDateString() === now.toDateString();
    const timePart = dt.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });

    if (isToday) {
      return `${timePart} (сегодня)`;
    }

    const datePart = dt.toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
    return `${timePart} (${datePart})`;
  } catch {
    return timeStr || 'не проводился';
  }
}

export const Topbar: React.FC<TopbarProps> = ({
  systemStatus,
  onScan,
  isScanning,
  onRefresh,
  onOpenSources,
  isPaused,
  onTogglePause,
  isPausing,
}) => {
  const stats = systemStatus?.stats;
  const isOperational = systemStatus?.status === 'operational';
  const activeSourcesCount = systemStatus?.active_sources_count ?? 0;
  const pendingAiCount =
    systemStatus?.pending_ai_count ?? systemStatus?.stats?.pending_ai_count ?? 0;
  const lastScanFormatted = formatLastScanTime(systemStatus?.last_scan_time);

  const isScannerPaused = isPaused ?? systemStatus?.is_paused ?? systemStatus?.scheduler?.is_paused ?? false;

  const nextScanTarget = systemStatus?.next_scan_time || systemStatus?.scheduler?.next_run_time;
  const [countdownText, setCountdownText] = useState<string>(() => calculateTimeLeft(nextScanTarget).formatted);
  const prevTargetRef = useRef(nextScanTarget);

  useEffect(() => {
    prevTargetRef.current = nextScanTarget;

    const updateCountdown = () => {
      const { totalSeconds, formatted } = calculateTimeLeft(nextScanTarget);
      setCountdownText(formatted);
      if (totalSeconds === 0 && nextScanTarget) {
        onRefresh();
      }
    };

    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);

    return () => clearInterval(interval);
  }, [nextScanTarget, onRefresh]);

  const inboxCount = stats?.inbox_count ?? (stats?.total_count ? stats.total_count - (stats.liked_count || 0) : 0);
  const likedCount = stats?.liked_count ?? 0;

  return (
    <header className="bg-app-surface border-b border-app-border px-4 py-2.5 sticky top-0 z-30 font-sans">
      <div className="max-w-7xl mx-auto flex flex-col md:flex-row md:items-center md:justify-between gap-3">
        
        {/* Left: Brand & Radar Status */}
        <div className="flex items-center gap-4 flex-wrap">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-md bg-app-elevated border border-app-border flex items-center justify-center text-content-primary flex-shrink-0">
              <Activity className="w-4 h-4 text-brand-hover" />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-bold tracking-tight text-content-primary font-mono">
                TREND<span className="text-brand-hover font-semibold">SCANNER</span>
              </span>
              <span className="px-1.5 py-0.5 text-[10px] font-mono font-medium rounded bg-app-elevated text-content-muted border border-app-border">
                v1.0
              </span>
            </div>
          </div>

          {/* Status line: Radar • In Queue • Last Scan • Next Scan / Paused */}
          <div className="flex items-center gap-2 text-xs md:text-sm text-content-muted font-mono flex-wrap">
            <button
              type="button"
              onClick={onOpenSources}
              className="inline-flex items-center gap-1.5 hover:text-content-secondary transition-colors cursor-pointer group"
              title="Открыть список источников"
            >
              <span
                className={`w-1.5 h-1.5 rounded-full ${
                  isScanning
                    ? 'bg-status-warning animate-ping'
                    : isOperational
                    ? 'bg-status-success'
                    : 'bg-status-danger'
                }`}
              />
              <span>
                Радар: <strong className="text-content-secondary group-hover:text-content-primary font-semibold">{activeSourcesCount}</strong> источников
              </span>
            </button>

            <span className="text-app-border">•</span>

            <span>
              В очереди ИИ: <strong className="text-content-secondary font-semibold">{pendingAiCount}</strong>
            </span>

            <span className="text-app-border">•</span>

            <span>
              Посл. скан: <strong className="text-content-secondary font-medium">{lastScanFormatted}</strong>
            </span>

            <span className="text-app-border">•</span>

            {isScannerPaused ? (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20">
                <span>⏸ Скан на паузе</span>
              </span>
            ) : (
              <span className="inline-flex items-center gap-1">
                <Timer className="w-3.5 h-3.5 text-brand-hover inline" />
                <span>Следующий скан через: <strong className="text-content-primary font-semibold">{countdownText}</strong></span>
              </span>
            )}
          </div>
        </div>

        {/* Right: Useful Counters & Actions */}
        <div className="flex items-center gap-3 self-end md:self-auto font-mono">
          {/* Inbox Counter */}
          <div
            className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-app-elevated border border-app-border text-xs text-content-secondary"
            title="Непросмотренные входящие тренды"
          >
            <Inbox className="w-3.5 h-3.5 text-content-muted" />
            <span>Входящие:</span>
            <strong className="text-content-primary">{inboxCount}</strong>
          </div>

          {/* Liked Counter */}
          <div
            className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-status-danger/10 border border-status-danger/20 text-xs text-status-danger"
            title="Сохраненные избранные тренды"
          >
            <Heart className="w-3.5 h-3.5 fill-status-danger/20 text-status-danger" />
            <span>Избранное:</span>
            <strong className="text-status-danger font-semibold">{likedCount}</strong>
          </div>

          {/* Refresh Icon Button */}
          <button
            type="button"
            onClick={onRefresh}
            className="p-1.5 rounded-md bg-app-elevated hover:bg-app-hover border border-app-border text-content-muted hover:text-content-primary transition-colors text-xs cursor-pointer"
            title="Обновить данные"
            aria-label="Обновить данные"
          >
            <RotateCw className={`w-3.5 h-3.5 ${isScanning ? 'animate-spin text-content-primary' : ''}`} />
          </button>

          {/* Pause / Resume Button */}
          <button
            type="button"
            onClick={onTogglePause}
            disabled={isPausing || isScanning}
            title={isScannerPaused ? "Возобновить автоматическое сканирование" : "Приостановить автоматический сканер"}
            className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-md transition-all border ${
              isPausing || isScanning
                ? 'bg-app-elevated text-content-muted border-app-border cursor-not-allowed'
                : isScannerPaused
                ? 'bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 border-amber-500/30 cursor-pointer shadow-sm'
                : 'bg-app-elevated hover:bg-app-hover text-content-secondary hover:text-content-primary border-app-border cursor-pointer shadow-sm'
            }`}
          >
            {isPausing ? (
              <>
                <RotateCw className="w-3.5 h-3.5 animate-spin text-amber-400" />
                <span>{isScannerPaused ? 'Возобновление...' : 'Пауза...'}</span>
              </>
            ) : isScannerPaused ? (
              <>
                <Play className="w-3.5 h-3.5 fill-amber-400 text-amber-400" />
                <span>▶️ Возобновить</span>
              </>
            ) : (
              <>
                <Pause className="w-3.5 h-3.5 fill-current text-content-secondary" />
                <span>⏸ Пауза</span>
              </>
            )}
          </button>

          {/* AI Brand Scan Button */}
          <button
            type="button"
            onClick={onScan}
            disabled={isScanning}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold rounded-md transition-all shadow-sm ${
              isScanning
                ? 'bg-app-hover text-content-muted border border-app-border cursor-not-allowed'
                : 'bg-brand hover:bg-brand-hover text-white active:scale-95 cursor-pointer shadow-brand/20'
            }`}
          >
            {isScanning ? (
              <>
                <RotateCw className="w-3.5 h-3.5 animate-spin text-white" />
                <span>Сканирование...</span>
              </>
            ) : (
              <>
                <Play className="w-3.5 h-3.5 fill-white text-white" />
                <span>Сканировать</span>
              </>
            )}
          </button>
        </div>

      </div>
    </header>
  );
};
