import type { CaptureMode } from '@/lib/messages';
import type { Memory } from '@/lib/types';

// A tiny event bus so the content-script entry (which owns the runtime message listener
// and re-applies highlights) can talk to the React UI mounted in the shadow root.

export type BusEventMap = {
  capture: CaptureMode;
  'element-note': undefined;
  memories: Memory[];
  'memory-removed': string;
};

export const bus = new EventTarget();

export function emit<K extends keyof BusEventMap>(type: K, detail: BusEventMap[K]): void {
  bus.dispatchEvent(new CustomEvent(type, { detail }));
}

export function on<K extends keyof BusEventMap>(
  type: K,
  handler: (detail: BusEventMap[K]) => void,
): () => void {
  const listener = (e: Event) => handler((e as CustomEvent<BusEventMap[K]>).detail);
  bus.addEventListener(type, listener);
  return () => bus.removeEventListener(type, listener);
}
