import React from 'react';
import {
  Flame,
  ShieldAlert,
  ShieldCheck,
  AlertTriangle,
  Rss,
  MessageSquare,
  Radio,
  Globe,
  Sparkles,
  CheckCircle2,
  AlertCircle,
  HelpCircle,
  Bot,
} from 'lucide-react';

interface ScoreBadgeProps {
  score?: number | null;
  showIcon?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

export const ScoreBadge: React.FC<ScoreBadgeProps> = ({
  score,
  showIcon = true,
  size = 'md',
}) => {
  if (score === null || score === undefined) {
    return (
      <span className="inline-flex items-center gap-1 font-mono text-xs text-content-muted bg-app-hover border border-app-border px-2 py-0.5 rounded">
        <HelpCircle className="w-3 h-3 text-content-muted" />
        Н/Д
      </span>
    );
  }

  let colorClasses = '';
  let dotColor = '';

  if (score >= 8) {
    colorClasses = 'bg-status-success/10 text-status-success border-status-success/20';
    dotColor = 'bg-status-success';
  } else if (score >= 5) {
    colorClasses = 'bg-status-warning/10 text-status-warning border-status-warning/20';
    dotColor = 'bg-status-warning';
  } else {
    colorClasses = 'bg-app-hover text-content-muted border-app-border';
    dotColor = 'bg-content-muted';
  }

  const sizeClasses = {
    sm: 'text-[11px] px-1.5 py-0.5 gap-1',
    md: 'text-xs px-2 py-1 gap-1.5',
    lg: 'text-sm px-3 py-1.5 gap-2 font-semibold',
  }[size];

  return (
    <span
      className={`inline-flex items-center font-mono font-medium border rounded transition-all ${sizeClasses} ${colorClasses}`}
      title={`Оценка жизнеспособности ИИ: ${score}/10`}
    >
      {showIcon && (
        <span className={`w-1.5 h-1.5 rounded-full ${dotColor}`} />
      )}
      <span>{score.toFixed(0)}/10</span>
    </span>
  );
};

interface ScamBadgeProps {
  probability?: number | null;
  size?: 'sm' | 'md' | 'lg';
}

export const ScamBadge: React.FC<ScamBadgeProps> = ({
  probability,
  size = 'md',
}) => {
  if (probability === null || probability === undefined) {
    return (
      <span className="inline-flex items-center gap-1 font-mono text-xs text-content-muted bg-app-hover border border-app-border px-2 py-0.5 rounded">
        Без оценки
      </span>
    );
  }

  const sizeClasses = {
    sm: 'text-[11px] px-1.5 py-0.5 gap-1',
    md: 'text-xs px-2 py-1 gap-1.5',
    lg: 'text-sm px-2.5 py-1.5 gap-2',
  }[size];

  if (probability > 50) {
    return (
      <span
        className={`inline-flex items-center font-mono font-medium bg-status-danger/10 text-status-danger border border-status-danger/20 rounded ${sizeClasses}`}
        title={`Высокая вероятность скама: ${probability}%`}
      >
        <ShieldAlert className="w-3.5 h-3.5 text-status-danger flex-shrink-0" />
        <span>РИСК {probability}%</span>
      </span>
    );
  }

  if (probability >= 20) {
    return (
      <span
        className={`inline-flex items-center font-mono font-medium bg-status-warning/10 text-status-warning border border-status-warning/20 rounded ${sizeClasses}`}
        title={`Умеренный риск скама: ${probability}%`}
      >
        <AlertTriangle className="w-3.5 h-3.5 text-status-warning flex-shrink-0" />
        <span>ВНИМ. {probability}%</span>
      </span>
    );
  }

  return (
    <span
      className={`inline-flex items-center font-mono font-medium bg-status-success/10 text-status-success border border-status-success/20 rounded ${sizeClasses}`}
      title={`Низкий риск скама: ${probability}%`}
    >
      <ShieldCheck className="w-3.5 h-3.5 text-status-success flex-shrink-0" />
      <span>ЧИСТО {probability}%</span>
    </span>
  );
};

interface SourceBadgeProps {
  sourceName?: string | null;
  sourceType?: string | null;
}

export const SourceBadge: React.FC<SourceBadgeProps> = ({
  sourceName,
  sourceType,
}) => {
  const type = (sourceType || '').toLowerCase();
  const isAutoDiscovered = type.includes('auto_discovered') || type.includes('auto') || type.includes('discovered');
  const label = sourceName || (isAutoDiscovered ? '🤖 Найдено ИИ' : type || 'Источник');

  let Icon = Globe;

  if (isAutoDiscovered) {
    Icon = Bot;
  } else if (type.includes('rss')) {
    Icon = Rss;
  } else if (type.includes('reddit')) {
    Icon = MessageSquare;
  } else if (type.includes('telegram')) {
    Icon = Radio;
  }

  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-xs font-mono border max-w-[180px] truncate ${
        isAutoDiscovered
          ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
          : 'bg-app-elevated text-content-secondary border border-app-border'
      }`}
      title={`${sourceName || 'Источник'} (${type || 'общий'})`}
    >
      <Icon className={`w-3 h-3 flex-shrink-0 ${isAutoDiscovered ? 'text-emerald-400' : 'text-content-muted'}`} />
      <span className="truncate">{label}</span>
    </span>
  );
};

interface TrendIndicatorProps {
  isTrend: boolean;
  size?: 'sm' | 'md';
}

export const TrendIndicator: React.FC<TrendIndicatorProps> = ({
  isTrend,
  size = 'md',
}) => {
  if (isTrend) {
    return (
      <span
        className={`inline-flex items-center gap-1 font-mono bg-brand/15 text-brand-hover border border-brand/30 font-medium rounded ${
          size === 'sm' ? 'text-[10px] px-1.5 py-0.5' : 'text-xs px-2 py-0.5'
        }`}
      >
        <Flame className="w-3 h-3 text-brand-hover fill-brand/20" />
        ТРЕНД
      </span>
    );
  }

  return (
    <span
      className={`inline-flex items-center gap-1 font-mono text-content-muted bg-app-elevated border border-app-border rounded ${
        size === 'sm' ? 'text-[10px] px-1.5 py-0.5' : 'text-xs px-2 py-0.5'
      }`}
    >
      <Sparkles className="w-3 h-3 opacity-40 text-content-muted" />
      ОБЩЕЕ
    </span>
  );
};

interface ReviewStatusBadgeProps {
  isReviewed: boolean;
}

export const ReviewStatusBadge: React.FC<ReviewStatusBadgeProps> = ({
  isReviewed,
}) => {
  if (isReviewed) {
    return (
      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[11px] font-mono bg-status-success/10 text-status-success border border-status-success/20">
        <CheckCircle2 className="w-3 h-3 text-status-success" />
        ПРОСМОТРЕНО
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[11px] font-mono bg-app-elevated text-content-primary border border-app-border">
      <AlertCircle className="w-3 h-3 text-content-muted" />
      НОВОЕ
    </span>
  );
};
