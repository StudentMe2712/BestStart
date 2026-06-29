import { useId } from 'react';
import { Check, Palette } from 'lucide-react';
import { PRESET_COLORS } from '@/lib/types';

// Preset swatches + a native custom-color input. Soft tints are applied at render time, so
// the raw color stored here is the "full strength" reference used for chips and category dots.

export function ColorPicker({
  value,
  onChange,
}: {
  value: string;
  onChange: (color: string) => void;
}) {
  const customId = useId();
  const isPreset = PRESET_COLORS.some((c) => c.value.toLowerCase() === value.toLowerCase());
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {PRESET_COLORS.map((c) => {
        const active = c.value.toLowerCase() === value.toLowerCase();
        return (
          <button
            key={c.value}
            type="button"
            title={c.label}
            aria-label={c.label}
            onClick={() => onChange(c.value)}
            className={`flex h-6 w-6 items-center justify-center rounded-full ring-offset-1 transition-transform duration-150 hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-400 ${
              active ? 'ring-2 ring-slate-400' : ''
            }`}
            style={{ backgroundColor: c.value }}
          >
            {active && <Check size={13} className="text-white drop-shadow" />}
          </button>
        );
      })}
      <label
        htmlFor={customId}
        title="Свой цвет"
        className={`relative flex h-6 w-6 cursor-pointer items-center justify-center rounded-full ring-offset-1 transition-transform duration-150 hover:scale-110 ${
          !isPreset ? 'ring-2 ring-slate-400' : 'ring-1 ring-slate-200'
        }`}
        style={!isPreset ? { backgroundColor: value } : undefined}
      >
        {isPreset ? (
          <Palette size={13} className="text-slate-500" />
        ) : (
          <Check size={13} className="text-white drop-shadow" />
        )}
        <input
          id={customId}
          type="color"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
        />
      </label>
    </div>
  );
}
