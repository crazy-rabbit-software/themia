import { describe, it, expect } from 'vitest';
import { en } from './en.ts';
import { de } from './de.ts';
import { es } from './es.ts';
import { fr } from './fr.ts';
import { pt } from './pt.ts';
import { it as itLocale } from './it.ts';
import { pl } from './pl.ts';
import { ja } from './ja.ts';
import { zh } from './zh.ts';
import { ru } from './ru.ts'

/** Flatten nested message dict into dotted leaf keys ("a.b.c"). */
function leafKeys(obj: unknown, prefix = '', out: string[] = []): string[] {
  if (obj && typeof obj === 'object') {
    for (const k of Object.keys(obj as Record<string, unknown>)) {
      const path = prefix ? `${prefix}.${k}` : k;
      leafKeys((obj as Record<string, unknown>)[k], path, out);
    }
  } else {
    out.push(prefix);
  }
  return out;
}

const enKeys = new Set(leafKeys(en));
const locales = { de, es, fr, pt, it: itLocale, pl, ja, zh, ru };

describe('locale key parity', () => {
  for (const [name, loc] of Object.entries(locales)) {
    it(`${name}: has no keys absent from en (typo/stale guard)`, () => {
      const extra = leafKeys(loc).filter((k) => !enKeys.has(k));
      expect(extra).toEqual([]);
    });
  }

  it('en has a non-trivial number of keys', () => {
    expect(enKeys.size).toBeGreaterThan(100);
  });

  // issue #63 pinned this for zh alone; issue #91 extended it to every locale.
  // Until then the others were deep-partial and silently fell back to English,
  // which is how they drifted to roughly half of en without a failing test —
  // a German user read the whole onboarding guide in English. Completeness is
  // now the contract, enforced here AND by the `: Messages` annotation each
  // locale carries (a missing key is a type error, not a silent fallback).
  for (const [name, loc] of Object.entries(locales)) {
    it(`issue_91 ${name} has the same keyset as en (complete translation)`, () => {
      const keys = new Set(leafKeys(loc));
      const missing = [...enKeys].filter((k) => !keys.has(k));
      expect(missing).toEqual([]);
    });
  }

  // A translation that drops a {token} renders a broken sentence, and key
  // parity cannot see it — the key is present, just wrong.
  const placeholders = (s: string) =>
    [...s.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');
  const enFlat = new Map<string, string>();
  (function walk(o: unknown, prefix = '') {
    if (typeof o === 'string') enFlat.set(prefix, o);
    else if (o && typeof o === 'object') {
      for (const [k, v] of Object.entries(o as Record<string, unknown>)) {
        walk(v, prefix ? `${prefix}.${k}` : k);
      }
    }
  })(en);

  for (const [name, loc] of Object.entries(locales)) {
    it(`issue_91 ${name} keeps every {placeholder} from en`, () => {
      const mismatched: string[] = [];
      (function walk(o: unknown, prefix = '') {
        if (typeof o === 'string') {
          const want = enFlat.get(prefix);
          if (want !== undefined && placeholders(want) !== placeholders(o)) {
            mismatched.push(`${prefix}: en {${placeholders(want)}} vs {${placeholders(o)}}`);
          }
        } else if (o && typeof o === 'object') {
          for (const [k, v] of Object.entries(o as Record<string, unknown>)) {
            walk(v, prefix ? `${prefix}.${k}` : k);
          }
        }
      })(loc);
      expect(mismatched).toEqual([]);
    });
  }
});
