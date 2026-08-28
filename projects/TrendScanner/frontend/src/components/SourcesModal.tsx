import React, { useState } from 'react';
import {
  X,
  Server,
  Plus,
  Trash2,
  ExternalLink,
  RotateCw,
  Rss,
  MessageSquare,
  Globe,
  CheckCircle,
  AlertTriangle,
  Play,
  Layers,
  Send,
  Zap,
  Bot,
} from 'lucide-react';
import { Source, SourceCreate } from '../types';

interface SourcesModalProps {
  isOpen: boolean;
  onClose: () => void;
  sources: Source[];
  onAddSource?: (source: SourceCreate) => Promise<void>;
  onToggleActive?: (sourceId: number, currentActive: boolean) => Promise<void>;
  onDeleteSource?: (sourceId: number) => Promise<void>;
  onScan: () => Promise<void>;
  isScanning: boolean;
}

export const SourcesModal: React.FC<SourcesModalProps> = ({
  isOpen,
  onClose,
  sources,
  onAddSource,
  onToggleActive,
  onDeleteSource,
  onScan,
  isScanning,
}) => {
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [sourceType, setSourceType] = useState('telegram');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<'all' | 'telegram' | 'spa' | 'reddit' | 'rss' | 'auto_discovered'>('all');

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !url.trim()) {
      setErrorMsg('Пожалуйста, укажите название и URL / @канал источника.');
      return;
    }

    if (!onAddSource) return;

    try {
      setIsSubmitting(true);
      setErrorMsg(null);
      await onAddSource({
        name: name.trim(),
        url: url.trim(),
        source_type: sourceType,
        is_active: true,
      });
      setName('');
      setUrl('');
      setSourceType('telegram');
    } catch (err: any) {
      setErrorMsg(err?.message || 'Не удалось добавить источник');
    } finally {
      setIsSubmitting(false);
    }
  };

  const getSourceIcon = (type: string) => {
    const t = type.toLowerCase();
    if (t.includes('auto_discovered') || t.includes('auto') || t.includes('discovered')) {
      return <Bot className="w-3.5 h-3.5 text-emerald-400" />;
    }
    if (t.includes('telegram')) return <Send className="w-3.5 h-3.5 text-brand-hover" />;
    if (t.includes('spa') || t.includes('playwright')) return <Zap className="w-3.5 h-3.5 text-brand-hover" />;
    if (t.includes('reddit')) return <MessageSquare className="w-3.5 h-3.5 text-status-warning" />;
    if (t.includes('rss')) return <Rss className="w-3.5 h-3.5 text-status-warning" />;
    return <Globe className="w-3.5 h-3.5 text-content-muted" />;
  };

  const getSourceCategory = (type: string): 'telegram' | 'spa' | 'reddit' | 'rss' | 'auto_discovered' => {
    const t = type.toLowerCase();
    if (t.includes('auto_discovered') || t.includes('auto') || t.includes('discovered')) return 'auto_discovered';
    if (t.includes('telegram')) return 'telegram';
    if (t.includes('spa') || t.includes('playwright') || t.includes('advanced')) return 'spa';
    if (t.includes('reddit')) return 'reddit';
    return 'rss';
  };

  const filteredSources = sources.filter((s) => {
    if (selectedCategory === 'all') return true;
    return getSourceCategory(s.source_type) === selectedCategory;
  });

  const telegramCount = sources.filter((s) => getSourceCategory(s.source_type) === 'telegram').length;
  const spaCount = sources.filter((s) => getSourceCategory(s.source_type) === 'spa').length;
  const redditCount = sources.filter((s) => getSourceCategory(s.source_type) === 'reddit').length;
  const rssCount = sources.filter((s) => getSourceCategory(s.source_type) === 'rss').length;
  const autoDiscoveredCount = sources.filter((s) => getSourceCategory(s.source_type) === 'auto_discovered').length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-fadeIn">
      <div
        className="bg-app-elevated border border-app-border rounded-xl w-full max-w-4xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden text-content-primary font-sans"
        role="dialog"
        aria-modal="true"
      >
        {/* Header */}
        <div className="bg-app-surface px-6 py-4 border-b border-app-border flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-app-elevated border border-app-border flex items-center justify-center">
              <Server className="w-4 h-4 text-status-success" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-content-primary font-mono tracking-wide">
                МОНИТОРИНГ ИСТОЧНИКОВ РАДАРА
              </h2>
              <p className="text-xs text-content-muted font-mono">
                {sources.filter((s) => s.is_active).length} активных площадок из {sources.length} подключенных
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-lg bg-app-elevated hover:bg-app-hover border border-app-border text-content-muted hover:text-content-primary transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6">
          {/* Category Filter Tabs */}
          <div className="flex items-center gap-2 overflow-x-auto pb-1 font-mono text-xs">
            <button
              type="button"
              onClick={() => setSelectedCategory('all')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'all'
                  ? 'bg-brand text-white border-brand font-semibold shadow-sm shadow-brand/20'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <Layers className="w-3.5 h-3.5" />
              <span>Все ({sources.length})</span>
            </button>

            <button
              type="button"
              onClick={() => setSelectedCategory('auto_discovered')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'auto_discovered'
                  ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40 font-semibold shadow-sm shadow-emerald-500/10'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <Bot className="w-3.5 h-3.5 text-emerald-400" />
              <span>🤖 Найдено ИИ ({autoDiscoveredCount})</span>
            </button>

            <button
              type="button"
              onClick={() => setSelectedCategory('telegram')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'telegram'
                  ? 'bg-brand/20 text-content-primary border-brand/40 font-semibold'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <Send className="w-3.5 h-3.5 text-brand-hover" />
              <span>Telegram ({telegramCount})</span>
            </button>

            <button
              type="button"
              onClick={() => setSelectedCategory('spa')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'spa'
                  ? 'bg-brand/20 text-content-primary border-brand/40 font-semibold'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <Zap className="w-3.5 h-3.5 text-brand-hover" />
              <span>SPA & JS ({spaCount})</span>
            </button>

            <button
              type="button"
              onClick={() => setSelectedCategory('reddit')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'reddit'
                  ? 'bg-brand/20 text-content-primary border-brand/40 font-semibold'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <MessageSquare className="w-3.5 h-3.5 text-status-warning" />
              <span>Reddit ({redditCount})</span>
            </button>

            <button
              type="button"
              onClick={() => setSelectedCategory('rss')}
              className={`px-3 py-1.5 rounded-lg border transition-all flex items-center gap-1.5 cursor-pointer ${
                selectedCategory === 'rss'
                  ? 'bg-brand/20 text-content-primary border-brand/40 font-semibold'
                  : 'bg-app-surface border-app-border text-content-secondary hover:text-content-primary'
              }`}
            >
              <Rss className="w-3.5 h-3.5 text-status-warning" />
              <span>RSS / Atom ({rssCount})</span>
            </button>
          </div>

          {/* Add Source Form */}
          {onAddSource && (
            <form
              onSubmit={handleSubmit}
              className="bg-app-surface border border-app-border rounded-lg p-4 flex flex-col gap-3 font-mono"
            >
              <div className="text-xs font-medium text-content-primary flex items-center gap-1.5 pb-2 border-b border-app-border">
                <Plus className="w-4 h-4 text-brand-hover" />
                <span>ДОБАВИТЬ НОВЫЙ ИСТОЧНИК В РАДАР</span>
              </div>

              {errorMsg && (
                <div className="flex items-center gap-2 p-2 rounded-lg bg-status-danger/10 border border-status-danger/20 text-status-danger text-xs">
                  <AlertTriangle className="w-4 h-4 flex-shrink-0 text-status-danger" />
                  <span>{errorMsg}</span>
                </div>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-12 gap-3">
                <div className="sm:col-span-4 flex flex-col gap-1">
                  <label className="text-[11px] text-content-muted font-medium">НАЗВАНИЕ ИСТОЧНИКА</label>
                  <input
                    type="text"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="напр. Telegram: AI Trends"
                    className="bg-app-bg border border-app-border focus:border-brand rounded-lg px-3 py-1.5 text-xs text-content-primary placeholder-content-muted outline-none"
                    required
                  />
                </div>

                <div className="sm:col-span-3 flex flex-col gap-1">
                  <label className="text-[11px] text-content-muted font-medium">ТИП ПАРСЕРА</label>
                  <select
                    value={sourceType}
                    onChange={(e) => setSourceType(e.target.value)}
                    className="bg-app-bg border border-app-border focus:border-brand rounded-lg px-3 py-1.5 text-xs text-content-primary outline-none cursor-pointer"
                  >
                    <option value="telegram">Telegram канал (@handle / URL)</option>
                    <option value="playwright_spa">Playwright SPA (Headless Chrome)</option>
                    <option value="rss">RSS / Atom лента</option>
                    <option value="reddit">Reddit сабреддит (JSON)</option>
                    <option value="auto_discovered">🤖 Найдено ИИ (Auto-Discovered)</option>
                  </select>
                </div>

                <div className="sm:col-span-5 flex flex-col gap-1">
                  <label className="text-[11px] text-content-muted font-medium">URL / ИМЯ КАНАЛА</label>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={url}
                      onChange={(e) => setUrl(e.target.value)}
                      placeholder="https://t.me/tech_trends"
                      className="flex-1 bg-app-bg border border-app-border focus:border-brand rounded-lg px-3 py-1.5 text-xs text-content-primary placeholder-content-muted outline-none"
                      required
                    />
                    <button
                      type="submit"
                      disabled={isSubmitting}
                      className="px-3.5 py-1.5 rounded-lg bg-brand hover:bg-brand-hover text-white font-semibold text-xs shadow-sm shadow-brand/20 flex items-center gap-1 transition-colors disabled:opacity-50 cursor-pointer"
                    >
                      {isSubmitting ? (
                        <RotateCw className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Plus className="w-3.5 h-3.5" />
                      )}
                      <span>Добавить</span>
                    </button>
                  </div>
                </div>
              </div>
            </form>
          )}

          {/* Sources List Table */}
          <div className="flex flex-col gap-2 font-mono">
            <div className="text-xs font-medium text-content-muted flex items-center justify-between">
              <span>ПОДКЛЮЧЕННЫЕ ПЛОЩАДКИ ({filteredSources.length})</span>
            </div>

            <div className="border border-app-border rounded-lg overflow-hidden bg-app-surface">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-app-surface border-b border-app-border text-[11px] text-content-muted">
                  <tr>
                    <th className="py-2.5 px-3 w-16 text-center font-medium">Статус</th>
                    <th className="py-2.5 px-3 w-48 font-medium">Название</th>
                    <th className="py-2.5 px-3 w-36 font-medium">Тип</th>
                    <th className="py-2.5 px-3 font-medium">URL эндпоинт</th>
                    <th className="py-2.5 px-3 w-36 font-medium">Посл. скан</th>
                    {onDeleteSource && <th className="py-2.5 px-3 w-16 text-right font-medium">Удалить</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-app-border">
                  {filteredSources.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-8 text-center text-content-muted">
                        Источники данной категории не найдены.
                      </td>
                    </tr>
                  ) : (
                    filteredSources.map((source) => (
                      <tr
                        key={source.id}
                        className={`hover:bg-app-hover transition-colors ${
                          !source.is_active ? 'opacity-50 bg-app-bg' : ''
                        }`}
                      >
                        {/* Toggle active */}
                        <td className="py-2.5 px-3 text-center">
                          {onToggleActive ? (
                            <button
                              type="button"
                              onClick={() => onToggleActive(source.id, source.is_active)}
                              className={`w-7 h-4 rounded-full transition-colors relative inline-flex items-center px-0.5 cursor-pointer ${
                                source.is_active ? 'bg-status-success' : 'bg-app-elevated border border-app-border'
                              }`}
                              title={source.is_active ? 'Активен (кликните для отключения)' : 'Отключен (кликните для включения)'}
                            >
                              <span
                                className={`w-3 h-3 rounded-full transition-transform ${
                                  source.is_active ? 'translate-x-3 bg-white' : 'translate-x-0 bg-content-muted'
                                }`}
                              />
                            </button>
                          ) : (
                            <span className="w-2 h-2 rounded-full bg-status-success inline-block" />
                          )}
                        </td>

                        {/* Name */}
                        <td className="py-2.5 px-3 font-medium text-content-primary">
                          {source.name}
                        </td>

                        {/* Type */}
                        <td className="py-2.5 px-3">
                          {source.source_type === 'auto_discovered' ? (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-emerald-500/10 border border-emerald-500/20 text-[11px] text-emerald-400 font-medium" title="Автоматически найден ИИ">
                              <Bot className="w-3.5 h-3.5 text-emerald-400" />
                              <span>🤖 Найдено ИИ</span>
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-app-elevated border border-app-border text-[11px] text-content-secondary">
                              {getSourceIcon(source.source_type)}
                              <span>{source.source_type}</span>
                            </span>
                          )}
                        </td>

                        {/* URL */}
                        <td className="py-2.5 px-3 max-w-xs truncate text-content-muted">
                          <a
                            href={source.url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="hover:text-content-primary inline-flex items-center gap-1 hover:underline truncate"
                          >
                            <span className="truncate">{source.url}</span>
                            <ExternalLink className="w-3 h-3 flex-shrink-0" />
                          </a>
                        </td>

                        {/* Last Scanned */}
                        <td className="py-2.5 px-3 text-[11px] text-content-muted">
                          {source.last_scanned ? source.last_scanned : 'Ожидает'}
                        </td>

                        {/* Delete */}
                        {onDeleteSource && (
                          <td className="py-2.5 px-3 text-right">
                            <button
                              type="button"
                              onClick={async () => {
                                if (
                                  window.confirm(
                                    `Удалить источник "${source.name}" и все связанные тренды?`
                                  )
                                ) {
                                  await onDeleteSource(source.id);
                                }
                              }}
                              className="p-1 text-content-muted hover:text-status-danger hover:bg-status-danger/10 rounded transition-colors cursor-pointer"
                              title="Удалить источник"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </td>
                        )}
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="bg-app-surface px-6 py-3.5 border-t border-app-border flex items-center justify-between font-mono text-xs">
          <div className="text-content-muted flex items-center gap-2">
            <CheckCircle className="w-4 h-4 text-status-success" />
            <span>Все {sources.length} источников работают синхронно в фоне через APScheduler.</span>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg bg-app-elevated hover:bg-app-hover border border-app-border text-content-primary font-medium transition-colors cursor-pointer"
            >
              Закрыть
            </button>

            <button
              type="button"
              onClick={async () => {
                await onScan();
              }}
              disabled={isScanning}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg bg-brand hover:bg-brand-hover text-white font-semibold shadow-sm shadow-brand/20 disabled:opacity-50 transition-colors cursor-pointer"
            >
              {isScanning ? (
                <RotateCw className="w-4 h-4 animate-spin text-white" />
              ) : (
                <Play className="w-4 h-4 fill-white text-white" />
              )}
              <span>Запустить сканирование</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
