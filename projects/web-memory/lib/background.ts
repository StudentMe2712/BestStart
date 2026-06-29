// Side-panel background customization (presets + an uploaded photo). Stored in storage.local
// so it persists across sessions; the uploaded photo is downscaled before saving (see picker).

export type BackgroundKind = 'none' | 'preset' | 'image';

export interface BackgroundConfig {
  kind: BackgroundKind;
  /** Preset id (kind 'preset') or a data URL (kind 'image'). */
  value: string;
}

export const DEFAULT_BACKGROUND: BackgroundConfig = { kind: 'none', value: '' };

export interface BackgroundPreset {
  id: string;
  label: string;
  css: string; // a CSS background value (gradient)
  dark?: boolean;
}

export const BG_PRESETS: BackgroundPreset[] = [
  { id: 'slate', label: 'Слейт', css: 'linear-gradient(160deg, #f8fafc, #e9eef5)' },
  { id: 'dawn', label: 'Рассвет', css: 'linear-gradient(160deg, #fff7ed, #ffe4e6)' },
  { id: 'ocean', label: 'Океан', css: 'linear-gradient(160deg, #ecfeff, #dbeafe)' },
  { id: 'mint', label: 'Мята', css: 'linear-gradient(160deg, #ecfdf5, #d1fae5)' },
  { id: 'lavender', label: 'Лаванда', css: 'linear-gradient(160deg, #f5f3ff, #ede9fe)' },
  { id: 'sand', label: 'Песок', css: 'linear-gradient(160deg, #fefce8, #fef3c7)' },
];

export function presetCss(id: string): string {
  return BG_PRESETS.find((p) => p.id === id)?.css ?? '';
}

const STORAGE_KEY = 'wm:background';

export async function loadBackground(): Promise<BackgroundConfig> {
  const v = (await browser.storage.local.get(STORAGE_KEY))[STORAGE_KEY] as
    | BackgroundConfig
    | undefined;
  return v ?? DEFAULT_BACKGROUND;
}

export async function saveBackground(cfg: BackgroundConfig): Promise<void> {
  await browser.storage.local.set({ [STORAGE_KEY]: cfg });
}
