import { createMemo } from 'solid-js';
import { invoke } from './ipc.ts';
import { interpolate } from './i18nFormat.ts';
import { globalDesign, setGlobalDesign, type Language } from './design.ts';
import { en } from './locales/en.ts';
import { de } from './locales/de.ts';
import { es } from './locales/es.ts';
import { fr } from './locales/fr.ts';
import { pt } from './locales/pt.ts';
import { it } from './locales/it.ts';
import { pl } from './locales/pl.ts';
import { ja } from './locales/ja.ts';
import { zh } from './locales/zh.ts';
import { ru } from './locales/ru.ts';

const dictionaries: Record<Language, Record<string, string>> = {
  en: flatten(en),
  de: flatten(de),
  es: flatten(es),
  fr: flatten(fr),
  pt: flatten(pt),
  it: flatten(it),
  pl: flatten(pl),
  ja: flatten(ja),
  zh: flatten(zh),
  ru: flatten(ru),
};

/** Flatten nested dict into "a.b.c" → "value" map. */
function flatten(obj: Record<string, unknown>, prefix = '', out: Record<string, string> = {}): Record<string, string> {
  for (const k of Object.keys(obj)) {
    const v = obj[k];
    const path = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object') flatten(v as Record<string, unknown>, path, out);
    else out[path] = String(v);
  }
  return out;
}

export function lang(): Language {
  return globalDesign.language ?? 'en';
}

const missingLogged = new Set<string>();

/** Reactive translation. Reads `globalDesign.language` so consumers in JSX
 *  re-render when it flips. Interpolates {name} placeholders from `params`. */
export function t(key: string, params?: Record<string, string | number>): string {
  const l = lang();
  const dict = dictionaries[l] ?? dictionaries.en;
  let s = dict[key] ?? dictionaries.en[key];
  if (s === undefined) {
    if (!missingLogged.has(key)) {
      missingLogged.add(key);
      console.warn(`[i18n] missing key: ${key}`);
    }
    return key;
  }
  return interpolate(s, params);
}

/** Translate `key`, falling back to `fallback` when the key is missing from
 *  every locale. `t()` returns the key itself on a miss, so a bare
 *  `t(key) || fallback` is unreachable (the key is truthy); this checks the miss
 *  explicitly. Used by the widget picker so a widget without a
 *  `widget.<type>.label` key shows its registry `label` instead of the raw key. */
export function tOr(key: string, fallback: string, params?: Record<string, string | number>): string {
  const s = t(key, params);
  return s === key ? fallback : s;
}

/** Memo wrapper so reactivity tracks `lang()`. Use inside JSX:
 *    <span>{tm('settings.title')}</span>  */
export function tm(key: string, params?: Record<string, string | number>) {
  return createMemo(() => t(key, params));
}

/** Map a BCP-47-ish system locale string to a supported UI language. Pure and
 *  exported so the mapping is unit-testable (issue #63: zh-CN → 'zh'). Matches
 *  on the leading subtag; anything unrecognised falls back to English. */
export function detectLanguage(sysLocale: string | null | undefined): Language {
  const s = sysLocale ?? '';
  return (
    /^de/i.test(s) ? 'de' :
    /^es/i.test(s) ? 'es' :
    /^fr/i.test(s) ? 'fr' :
    /^pt/i.test(s) ? 'pt' :
    /^it/i.test(s) ? 'it' :
    /^pl/i.test(s) ? 'pl' :
    /^ja/i.test(s) ? 'ja' :
    /^zh/i.test(s) ? 'zh' :
    /^ru/i.test(s) ? 'ru' :
    'en'
  );
}

/** First-run system locale detection. Called from main.tsx before render. */
export async function maybeInitLanguageFromSystem(): Promise<void> {
  try {
    const raw = localStorage.getItem('widget-global-design');
    if (raw) {
      const parsed = JSON.parse(raw);
      if (parsed.language) return;
    }
    const sysLocale = await invoke('get_system_locale');
    const next = detectLanguage(sysLocale);
    if (next !== globalDesign.language) setGlobalDesign('language', next);
  } catch { /* best-effort */ }
}
