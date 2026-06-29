import { useState, type KeyboardEvent } from 'react';
import { Plus, Power, Settings, Trash2, X } from 'lucide-react';
import { normalizeDomain, type WmSettings } from '@/lib/settings';
import { Button, IconButton } from '@/components/ui';

// Settings modal for the side panel: master on/off, the on-page note close behavior, and the
// per-domain blacklist that suppresses the in-page UI (so it never overlaps sites like messengers).

export function SettingsPanel({
  settings,
  onChange,
  onClose,
  currentHost,
}: {
  settings: WmSettings;
  onChange: (next: WmSettings) => void;
  onClose: () => void;
  /** Host of the active tab, used by the "disable on this site" shortcut. */
  currentHost?: string;
}) {
  const [domainInput, setDomainInput] = useState('');

  const addDomain = (raw: string) => {
    const d = normalizeDomain(raw);
    if (!d || settings.disabledDomains.includes(d)) {
      setDomainInput('');
      return;
    }
    onChange({ ...settings, disabledDomains: [...settings.disabledDomains, d] });
    setDomainInput('');
  };
  const removeDomain = (d: string) =>
    onChange({ ...settings, disabledDomains: settings.disabledDomains.filter((x) => x !== d) });

  const onInputKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') addDomain(domainInput);
  };

  const host = currentHost ? normalizeDomain(currentHost) : '';
  const hostDisabled = host ? settings.disabledDomains.includes(host) : false;

  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: 60 }}
      className="flex items-start justify-center bg-slate-900/30 p-4 pt-10 backdrop-blur-sm"
      onMouseDown={onClose}
    >
      <div
        className="flex max-h-[80vh] w-[360px] max-w-full flex-col animate-scale-in overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <Settings size={16} strokeWidth={1.75} className="text-accent-600" />
          <h3 className="flex-1 text-[14px] font-semibold text-slate-900">Настройки</h3>
          <IconButton onClick={onClose} aria-label="Закрыть">
            <X size={16} strokeWidth={1.75} />
          </IconButton>
        </div>

        <div className="space-y-4 overflow-y-auto p-4">
          <Toggle
            label="Расширение включено"
            hint="Выключите, чтобы скрыть всю разметку на страницах везде."
            checked={settings.enabled}
            onChange={(v) => onChange({ ...settings, enabled: v })}
          />
          <Toggle
            label="Закрывать заметку кликом вне окна"
            hint="Кнопка «Закрыть» работает всегда; это добавляет закрытие по клику рядом."
            checked={settings.closeNoteOnOutsideClick}
            onChange={(v) => onChange({ ...settings, closeNoteOnOutsideClick: v })}
          />

          <div>
            <h4 className="mb-1 text-[13px] font-semibold text-slate-800">Отключённые сайты</h4>
            <p className="mb-2.5 text-[11px] leading-relaxed text-slate-400">
              На этих доменах разметка не показывается, чтобы не перекрывать интерфейс сайта
              (например, мессенджеры). Изменения применяются после перезагрузки вкладки.
            </p>

            {host && (
              <Button
                variant={hostDisabled ? 'secondary' : 'danger'}
                size="md"
                className="mb-2.5 w-full"
                onClick={() => (hostDisabled ? removeDomain(host) : addDomain(host))}
              >
                <Power size={15} strokeWidth={1.75} />
                {hostDisabled ? `Включить на ${host}` : `Отключить на ${host}`}
              </Button>
            )}

            <ul className="space-y-1.5">
              {settings.disabledDomains.length === 0 && (
                <li className="rounded-lg border border-dashed border-slate-200 px-3 py-2 text-[12px] text-slate-400">
                  Список пуст
                </li>
              )}
              {settings.disabledDomains.map((d) => (
                <li
                  key={d}
                  className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-1.5"
                >
                  <span className="flex-1 truncate text-[12px] text-slate-700">{d}</span>
                  <IconButton
                    variant="danger"
                    title="Убрать из списка"
                    aria-label={`Убрать ${d}`}
                    onClick={() => removeDomain(d)}
                  >
                    <Trash2 size={14} strokeWidth={1.75} />
                  </IconButton>
                </li>
              ))}
            </ul>

            <div className="mt-2.5 flex gap-2">
              <input
                value={domainInput}
                onChange={(e) => setDomainInput(e.target.value)}
                onKeyDown={onInputKey}
                aria-label="Добавить домен в список отключённых"
                placeholder="например, web.whatsapp.com"
                className="min-w-0 flex-1 rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-[12px] text-slate-800 placeholder:text-slate-400 outline-none transition-all focus:border-accent-400 focus:ring-2 focus:ring-accent-500/20"
              />
              <Button variant="secondary" size="md" onClick={() => addDomain(domainInput)}>
                <Plus size={15} strokeWidth={1.75} />
                Добавить
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/** A minimal labelled switch (the shared ui.tsx has only buttons). */
function Toggle({
  label,
  hint,
  checked,
  onChange,
}: {
  label: string;
  hint?: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="flex w-full items-center gap-3 rounded-lg border border-slate-200 bg-white px-3 py-2.5 text-left transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400"
    >
      <span className="flex-1">
        <span className="block text-[13px] font-medium text-slate-800">{label}</span>
        {hint && <span className="mt-0.5 block text-[11px] leading-relaxed text-slate-400">{hint}</span>}
      </span>
      <span
        className={`relative h-5 w-9 shrink-0 rounded-full transition-colors duration-150 ${
          checked ? 'bg-accent-600' : 'bg-slate-300'
        }`}
      >
        <span
          className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-all duration-150 ${
            checked ? 'left-[18px]' : 'left-0.5'
          }`}
        />
      </span>
    </button>
  );
}
