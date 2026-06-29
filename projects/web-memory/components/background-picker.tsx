import { useRef, useState } from 'react';
import { Check, ImagePlus, Wallpaper, X } from 'lucide-react';
import { BG_PRESETS, type BackgroundConfig } from '@/lib/background';
import { Button, IconButton } from '@/components/ui';

/** Downscale an uploaded image to fit a max dimension and return a JPEG data URL, keeping
 *  stored backgrounds small. The image is rendered with `cover`, so it fills any panel size. */
function fitImageToDataUrl(file: File, maxDim = 1280): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        const scale = Math.min(1, maxDim / Math.max(img.width, img.height));
        const w = Math.max(1, Math.round(img.width * scale));
        const h = Math.max(1, Math.round(img.height * scale));
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          reject(new Error('no 2d context'));
          return;
        }
        ctx.drawImage(img, 0, 0, w, h);
        resolve(canvas.toDataURL('image/jpeg', 0.82));
      };
      img.onerror = () => reject(new Error('image decode failed'));
      img.src = reader.result as string;
    };
    reader.readAsDataURL(file);
  });
}

export function BackgroundPicker({
  value,
  onChange,
  onClose,
  onError,
}: {
  value: BackgroundConfig;
  onChange: (cfg: BackgroundConfig) => void;
  onClose: () => void;
  onError: (msg: string) => void;
}) {
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const onFile = async (file: File) => {
    setBusy(true);
    try {
      const dataUrl = await fitImageToDataUrl(file);
      onChange({ kind: 'image', value: dataUrl });
    } catch (err) {
      console.error('[web-memory] background image failed', err);
      onError('Не удалось загрузить изображение');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      style={{ position: 'fixed', inset: 0, zIndex: 60 }}
      className="flex items-start justify-center bg-slate-900/30 p-4 pt-10 backdrop-blur-sm"
      onMouseDown={onClose}
    >
      <div
        className="w-[360px] max-w-full animate-scale-in overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
          <Wallpaper size={16} strokeWidth={1.75} className="text-accent-600" />
          <h3 className="flex-1 text-[14px] font-semibold text-slate-900">Фон</h3>
          <IconButton onClick={onClose} aria-label="Закрыть">
            <X size={16} strokeWidth={1.75} />
          </IconButton>
        </div>

        <div className="space-y-3 p-4">
          <div className="grid grid-cols-4 gap-2">
            <Swatch
              label="Нет"
              active={value.kind === 'none'}
              onClick={() => onChange({ kind: 'none', value: '' })}
              style={{ background: '#f8fafc' }}
            />
            {BG_PRESETS.map((p) => (
              <Swatch
                key={p.id}
                label={p.label}
                active={value.kind === 'preset' && value.value === p.id}
                onClick={() => onChange({ kind: 'preset', value: p.id })}
                style={{ backgroundImage: p.css }}
              />
            ))}
          </div>

          {value.kind === 'image' && (
            <div className="flex items-center gap-2 rounded-lg border border-slate-200 p-2">
              <span
                className="h-10 w-10 shrink-0 rounded-md bg-slate-100"
                style={{ backgroundImage: `url(${value.value})`, backgroundSize: 'cover', backgroundPosition: 'center' }}
              />
              <span className="flex-1 text-[12px] text-slate-500">Своё изображение</span>
              <Button variant="danger" size="sm" onClick={() => onChange({ kind: 'none', value: '' })}>
                Убрать
              </Button>
            </div>
          )}

          <Button
            variant="secondary"
            size="md"
            className="w-full"
            disabled={busy}
            onClick={() => fileInput.current?.click()}
          >
            <ImagePlus size={15} strokeWidth={1.75} />
            {busy ? 'Загрузка…' : 'Загрузить фото'}
          </Button>
          <input
            ref={fileInput}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void onFile(file);
              e.target.value = '';
            }}
          />
          <p className="text-[11px] leading-relaxed text-slate-400">
            Фото уменьшается и подгоняется под размер панели автоматически.
          </p>
        </div>
      </div>
    </div>
  );
}

function Swatch({
  label,
  active,
  onClick,
  style,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
  style: React.CSSProperties;
}) {
  return (
    <button
      onClick={onClick}
      title={label}
      aria-label={label}
      className={`relative flex h-12 items-center justify-center rounded-lg border transition-transform duration-150 hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400 ${
        active ? 'border-accent-500 ring-2 ring-accent-400' : 'border-slate-200'
      }`}
      style={style}
    >
      {active && (
        <span className="rounded-full bg-white/90 p-0.5 shadow">
          <Check size={13} className="text-accent-600" />
        </span>
      )}
    </button>
  );
}
