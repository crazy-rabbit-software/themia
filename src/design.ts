import { createStore } from 'solid-js/store';
// [S-21] Owned by this module because this module owns the toggle: every window
// that applies the global design therefore also carries the override rule.
import './styles/reduce-effects.css';
import { invoke } from './ipc.ts';
import { onStorageKey, sharedStorage } from './sync.ts';
import { logger } from './debugLog.ts';
import {
  loadPersisted,
  savePersistedBare,
  objectSchema,
  isString,
  isNumber,
  isBoolean,
  isOneOf,
  type PersistedSchema,
} from './persistedStore.ts';
import type { IntroAnimMode } from './bootOverlay.ts';

const GLOBAL_KEY = 'widget-global-design';
const MIGRATION_KEY = 'widget-design-migration-v2';
const MIGRATION_V3_KEY = 'widget-design-migration-v3';
const MIGRATION_V4_KEY = 'widget-design-migration-v4';
const MIGRATION_V5_KEY = 'widget-design-migration-v5';
const MIGRATION_V6_KEY = 'widget-design-migration-v6';
const MIGRATION_V7_KEY = 'widget-design-migration-v7';
const MIGRATION_V8_KEY = 'widget-design-migration-v8';
const MIGRATION_V9_KEY = 'widget-design-migration-v9';
const MIGRATION_V10_KEY = 'widget-design-migration-v10';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type Language = 'en' | 'de' | 'es' | 'fr' | 'pt' | 'it' | 'pl' | 'ja' | 'zh' | 'ru';

export type WidgetBorderStyle = 'solid' | 'dashed' | 'dotted' | 'none';

// Global font family PRESETS (issue #62). Four curated stacks; each maps to a
// font STACK below. 'system' is the current default Segoe UI stack. All presets
// resolve to fonts that ship with Windows so nothing is fetched (the app CSP
// blocks remote fonts). The persisted `fontFamily` is NOT limited to these — see
// FontFamilyValue / resolveFontStack (issue #64).
export type FontFamily = 'system' | 'sans' | 'serif' | 'mono';
export const FONT_FAMILIES: FontFamily[] = ['system', 'sans', 'serif', 'mono'];

// The persisted font-family value is an OPEN set (issue #64): a preset id OR any
// installed system font FAMILY NAME. It's a plain string, not a closed union,
// because the set of installed fonts is unbounded and machine-specific — the
// honest domain type here is `string`. resolveFontStack() turns it into a
// concrete CSS stack (preset → curated stack; anything else → that family,
// quoted, with a safe fallback tail). This is genuinely open-set, so `string`
// is correct — it is NOT one of the closed sets the config rules require to be
// an enum.
export type FontFamilyValue = FontFamily | (string & {});

// Preset → CSS font stack. 'system' preserves the exact stack that src/App.css
// shipped, so the default is behaviour-preserving.
export const FONT_STACKS: Record<FontFamily, string> = {
  system: "'Segoe UI Variable', 'Segoe UI', -apple-system, BlinkMacSystemFont, system-ui, sans-serif",
  sans: "Arial, 'Helvetica Neue', Helvetica, 'Segoe UI', system-ui, sans-serif",
  serif: "Georgia, Cambria, 'Times New Roman', Times, serif",
  mono: "'Cascadia Mono', Consolas, 'Courier New', ui-monospace, monospace",
};

// A custom family name that would be safe to drop verbatim into a CSS
// font-family value: letters/marks/numbers plus spaces and the punctuation real
// font names use (Segoe UI, Times New Roman, DejaVu Sans, .AppleSystemUIFont).
// Deliberately EXCLUDES the CSS metacharacters (`"' ; { } < > \` and controls)
// that could break out of the property value, so an injected value is refused
// (resolveFontStack falls back to the system stack) rather than sanitised into a
// half-broken declaration.
const SAFE_FONT_NAME = /^[\p{L}\p{M}\p{N} ._\-+&()]+$/u;

/**
 * Map an OPEN fontFamily value (preset id OR installed family name) to a concrete
 * CSS font stack (issue #64).
 *
 *  - A preset id ('system'|'sans'|'serif'|'mono') → its curated stack.
 *  - Any other non-empty value → that family, QUOTED, with the system stack
 *    appended as a fallback tail so a missing/misspelled font still renders.
 *  - Empty/whitespace, or a value carrying CSS-injection metacharacters →
 *    the system stack (a refusal, not a partial sanitise).
 *
 * The value can arrive from a hand-edited localStorage blob or an imported
 * design, so this is a trust boundary: never emit an unquoted or unvalidated
 * custom name into the `--font-family` CSS variable.
 */
export function resolveFontStack(value: FontFamilyValue): string {
  if ((FONT_STACKS as Record<string, string | undefined>)[value] !== undefined) {
    return FONT_STACKS[value as FontFamily];
  }
  const name = String(value ?? '').trim();
  if (!name || !SAFE_FONT_NAME.test(name)) return FONT_STACKS.system;
  return `"${name}", ${FONT_STACKS.system}`;
}

// Startup intro animation style for widgets (src/SPEC.md [S-15]). 'none' = off.
// 'sides' / 'topbottom' ([S-28]) slide the view in as two rigid sheets, per
// axis; 'hyperspace' ([S-16]) additionally plays a one-shot full-screen overlay
// at boot; 'hacker' ([S-23]) is per-widget. (A 'hyperspace-extreme' style was
// merged into 'hyperspace'; the v9 migration remaps any persisted value.)
export type WidgetStartupAnimation = 'none' | 'fade' | 'scale' | 'slide' | 'sides' | 'topbottom' | 'flip' | 'blur' | 'hyperspace' | 'hacker';
export const WIDGET_STARTUP_ANIMATIONS: WidgetStartupAnimation[] =
  ['none', 'fade', 'scale', 'slide', 'sides', 'topbottom', 'flip', 'blur', 'hyperspace', 'hacker'];

export interface GlobalDesign {
  theme: 'dark' | 'light' | 'system';
  uiScale: number;
  // Global font (issue #62/#64). fontFamily is an OPEN string: a preset id OR any
  // installed system-font family name, mapped to a CSS stack by resolveFontStack
  // in applyGlobalDesign; fontScale multiplies the root font-size. Both cascade
  // from :root, so widgets pick up the family (they use `font-family: inherit`);
  // font SIZE is limited by widgets that hardcode px (see applyGlobalDesign).
  fontFamily: FontFamilyValue;
  fontScale: number;
  demoMode: boolean;
  devMode: boolean;
  language: Language;
  desktopIconsVisible: boolean;
  // SSRF policy bypass. When false (default/secure), the Rust backend blocks
  // outbound requests from user-supplied URLs/hosts that resolve to private,
  // loopback or link-local addresses. Users running self-hosted CalDAV / RSS /
  // GitHub Enterprise / IMAP on their LAN can enable this to permit them.
  allowPrivateNetworkAccess: boolean;
  // [LOG-04] (src-tauri/SPEC.md, Logging pipeline) Master switch for the log
  // pipeline. OFF by default: no records persisted to disk, no webview
  // broadcast, no plugin IPC from the frontend. Dev-only toggle in Settings >
  // Developer; pushed to the Rust gate (set_logging_enabled) on startup and on
  // change. Debug builds log regardless, so `npm run dev` stays unaffected.
  loggingEnabled: boolean;
  // Snap-to-grid aid for widget drag/resize/placement (src/SPEC.md [S-06]).
  gridSnap: boolean;
  // [S-21] Performance escape hatch: drop every `backdrop-filter` in the app.
  // The glass look costs a full-screen blur per surface on the compositor,
  // which is the single most expensive thing Themia asks a GPU to do; on
  // integrated graphics and older drivers it is also a source of driver-level
  // flakiness. Off by default — the glass look is the design — but a user on
  // weak hardware can turn it off and keep everything else. Deliberately NOT
  // part of a design preset, for the same reason uiScale/fontScale/animations
  // are not: it is the user's hardware choice, not a look.
  reduceEffects: boolean;
  // Staggered intro animation style for widgets on startup (src/SPEC.md [S-15]).
  widgetStartupAnimation: WidgetStartupAnimation;
  // Intro style for VIEW SWITCHES, independent of the startup style
  // (src/SPEC.md [S-24]). Overlay styles play only their per-widget intro here;
  // the full-screen boot overlay stays once-per-boot ([S-16]/[S-23]).
  widgetTransitionAnimation: WidgetStartupAnimation;
  // Duration of each widget's STARTUP intro animation, in ms (src/SPEC.md
  // [S-15]); also drives the boot-overlay timings ([S-16]/[S-23]).
  widgetStartupAnimationMs: number;
  // Duration of each widget's VIEW-SWITCH intro animation, in ms, independent
  // of the startup duration (src/SPEC.md [S-24]).
  widgetTransitionAnimationMs: number;
  // Intro style for SCREEN UNLOCK, independent of the startup style (src/SPEC.md
  // [S-16]). Plays its per-widget intro (overlay too, for hyperspace) when the
  // session is unlocked; 'none' disables the unlock replay. A v8 migration seeds
  // it from the legacy widgetStartupAnimationOnUnlock boolean.
  widgetUnlockAnimation: WidgetStartupAnimation;
  // Duration of each widget's UNLOCK intro animation, in ms, independent of the
  // startup duration (src/SPEC.md [S-16]).
  widgetUnlockAnimationMs: number;
  // Play a procedural startup sound that matches the startup animation, on boot
  // and the settings preview (src/SPEC.md [S-26]). Opt-in (default off);
  // suppressed under prefers-reduced-motion with the animation.
  widgetStartupSound: boolean;
  // Gap between widgets, in px. Global (was per-view).
  widgetGap: number;
  // Widget appearance, global since the per-view design model was removed.
  widgetBgColor: string;   // hex, '' = theme default
  widgetBgOpacity: number; // 0..1
  // Header surface, independent of the widget body so a header can be a
  // different colour or more/less opaque than the body it sits on.
  headerBgColor: string;   // hex, '' = theme default
  headerBgOpacity: number; // 0..1
  primaryColor: string;    // hex, '' = theme default
  cornerRadius: number;    // 0..24 px
  widgetBorderStyle: WidgetBorderStyle;
  widgetBorderWidth: number; // 0..8 px
  widgetBorderColor: string; // hex, '' = theme --border default
  widgetBorderOpacity: number; // 0..1
}

const DEFAULT_GLOBAL: GlobalDesign = {
  theme: 'dark',
  uiScale: 1,
  fontFamily: 'system',
  fontScale: 1,
  demoMode: false,
  devMode: false,
  language: 'en',
  desktopIconsVisible: true,
  allowPrivateNetworkAccess: false,
  loggingEnabled: false,
  gridSnap: true,
  reduceEffects: false,
  widgetStartupAnimation: 'scale',
  widgetTransitionAnimation: 'scale',
  widgetStartupAnimationMs: 500,
  widgetTransitionAnimationMs: 500,
  widgetUnlockAnimation: 'scale',
  widgetUnlockAnimationMs: 500,
  widgetStartupSound: false,
  widgetGap: 12,
  widgetBgColor: '',
  widgetBgOpacity: 0.90,
  headerBgColor: '',
  headerBgOpacity: 0.90,
  primaryColor: '',
  cornerRadius: 8,
  // No widget border by default: width 0 paints nothing, so the shipped look is
  // borderless. Style stays 'solid' (the corrupt-value fallback, design.test.ts)
  // so a user who raises the width gets a solid border, not none.
  widgetBorderStyle: 'solid',
  widgetBorderWidth: 0,
  widgetBorderColor: '',
  widgetBorderOpacity: 1,
};

const GLOBAL_FIELDS = Object.keys(DEFAULT_GLOBAL) as (keyof GlobalDesign)[];

// Schema'd persistence (1.12). Adds the per-field TYPE validation the old
// `raw[k] ?? default` load lacked (a corrupt `uiScale:"x"` now falls back to the
// default instead of poisoning the theme). Persisted BARE (no version envelope)
// because importExport / i18n / layout read `widget-global-design` directly as a
// plain blob, so bare keeps them working while we still validate on load.
// Exported for characterization tests; per-field validation is what keeps a
// corrupt value (e.g. uiScale:"oops") from poisoning the whole design store.
export const globalDesignSchema: PersistedSchema<GlobalDesign> = objectSchema<GlobalDesign>({
  version: 1,
  defaults: () => ({ ...DEFAULT_GLOBAL }),
  fields: {
    theme: isOneOf('dark', 'light', 'system'),
    uiScale: isNumber,
    // OPEN set (issue #64): a preset id OR any installed family name. Validated
    // as a plain string; resolveFontStack refuses unsafe values at apply time.
    fontFamily: isString,
    fontScale: isNumber,
    demoMode: isBoolean,
    devMode: isBoolean,
    language: isOneOf('en', 'de', 'es', 'fr', 'pt', 'it', 'pl', 'ja', 'zh', 'ru'),
    desktopIconsVisible: isBoolean,
    allowPrivateNetworkAccess: isBoolean,
    loggingEnabled: isBoolean,
    gridSnap: isBoolean,
    reduceEffects: isBoolean,
    widgetStartupAnimation: isOneOf('none', 'fade', 'scale', 'slide', 'sides', 'topbottom', 'flip', 'blur', 'hyperspace', 'hacker'),
    widgetTransitionAnimation: isOneOf('none', 'fade', 'scale', 'slide', 'sides', 'topbottom', 'flip', 'blur', 'hyperspace', 'hacker'),
    widgetStartupAnimationMs: isNumber,
    widgetTransitionAnimationMs: isNumber,
    widgetUnlockAnimation: isOneOf('none', 'fade', 'scale', 'slide', 'sides', 'topbottom', 'flip', 'blur', 'hyperspace', 'hacker'),
    widgetUnlockAnimationMs: isNumber,
    widgetStartupSound: isBoolean,
    widgetGap: isNumber,
    widgetBgColor: isString,
    widgetBgOpacity: isNumber,
    headerBgColor: isString,
    headerBgOpacity: isNumber,
    primaryColor: isString,
    cornerRadius: isNumber,
    widgetBorderStyle: isOneOf('solid', 'dashed', 'dotted', 'none'),
    widgetBorderWidth: isNumber,
    widgetBorderColor: isString,
    widgetBorderOpacity: isNumber,
  },
});

// ---------------------------------------------------------------------------
// One-shot migrations
//
//   v2: collapse the legacy all-in-one `widget-global-design` blob down to just
//       the global fields (visual fields used to live here in v1).
//   v3: design is no longer per-view, so wipe every `widget-design*` key.
//       Visual fields are reset to defaults (they fall back via loadGlobal()).
//   v4: the header surface was split off from the widget body. Seed the new
//       header fields from the user's existing widget background so widgets
//       look identical until the header is customised (header == body).
//   v5: widgetStartupAnimation became an enum (was a boolean). Map the legacy
//       boolean → enum: true → 'scale' (the original effect), false → 'none'.
//       Without this the boolean would fail the new isOneOf guard and a user
//       who'd turned the animation OFF would silently get it back on.
//   v6: the intro style split into startup vs view-transition ([S-24]). Seed
//       the new widgetTransitionAnimation from the user's existing startup
//       style. Before the split, ONE setting drove both, so a view switch
//       must keep animating exactly as it did.
//   v7: the intro DURATION split the same way ([S-24]). Seed the new
//       widgetTransitionAnimationMs from the previously shared
//       widgetStartupAnimationMs so view-switch speed is unchanged.
//   v8: the replay-on-unlock BOOLEAN became an independent unlock animation +
//       duration ([S-16]). Before the split, unlock REPLAYED the startup
//       animation when the boolean was on (default on). Seed the new
//       widgetUnlockAnimation / widgetUnlockAnimationMs from the startup values
//       so the behavior is preserved; a boolean OFF maps to 'none' (no replay).
//   v9: the 'hyperspace' (standard) and 'hyperspace-extreme' styles collapsed
//       into ONE 'hyperspace' (the violent flavour). Remap every persisted
//       'hyperspace-extreme' (startup / transition / unlock) → 'hyperspace' so
//       a user who picked it keeps a hyperspace warp, not the schema default.
// ---------------------------------------------------------------------------

(function migrateV2() {
  if (localStorage.getItem(MIGRATION_KEY)) return;
  try {
    const legacyRaw = localStorage.getItem(GLOBAL_KEY);
    const legacy = (legacyRaw ? JSON.parse(legacyRaw) : {}) as Record<string, unknown>;
    const nextGlobal: Record<string, unknown> = {};
    for (const k of GLOBAL_FIELDS) {
      nextGlobal[k] = legacy[k] ?? DEFAULT_GLOBAL[k];
    }
    localStorage.setItem(GLOBAL_KEY, JSON.stringify(nextGlobal));
    localStorage.setItem(MIGRATION_KEY, '1');
  } catch { /* migration is best-effort */ }
})();

(function migrateV3() {
  if (localStorage.getItem(MIGRATION_V3_KEY)) return;
  try {
    const stale: string[] = [];
    for (let i = 0; i < localStorage.length; i++) {
      const k = localStorage.key(i);
      if (!k) continue;
      if (k === MIGRATION_KEY || k === MIGRATION_V3_KEY) continue;
      if (k === 'widget-design' || k.startsWith('widget-design-')) stale.push(k);
    }
    stale.forEach(k => localStorage.removeItem(k));
    localStorage.setItem(MIGRATION_V3_KEY, '1');
  } catch { /* migration is best-effort */ }
})();

(function migrateV4() {
  if (localStorage.getItem(MIGRATION_V4_KEY)) return;
  try {
    const raw = localStorage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      // Seed the header surface from the existing widget surface only when the
      // user hasn't already got header values, so an upgrade is a no-op visual
      // change. Absent widget fields fall back to the shared defaults.
      if (!('headerBgColor' in blob)) {
        blob.headerBgColor = blob.widgetBgColor ?? DEFAULT_GLOBAL.headerBgColor;
      }
      if (!('headerBgOpacity' in blob)) {
        blob.headerBgOpacity = blob.widgetBgOpacity ?? DEFAULT_GLOBAL.headerBgOpacity;
      }
      localStorage.setItem(GLOBAL_KEY, JSON.stringify(blob));
    }
    localStorage.setItem(MIGRATION_V4_KEY, '1');
  } catch { /* migration is best-effort */ }
})();

(function migrateV5() {
  if (localStorage.getItem(MIGRATION_V5_KEY)) return;
  try {
    const raw = localStorage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      const v = blob.widgetStartupAnimation;
      if (typeof v === 'boolean') {
        blob.widgetStartupAnimation = v ? 'scale' : 'none';
        localStorage.setItem(GLOBAL_KEY, JSON.stringify(blob));
      }
    }
    localStorage.setItem(MIGRATION_V5_KEY, '1');
  } catch { /* migration is best-effort */ }
})();

// Exported for tests ([S-24]); runs against localStorage at module init below,
// AFTER v5 so a legacy boolean has already been mapped to the enum.
export function migrateV6TransitionAnim(storage: Pick<Storage, 'getItem' | 'setItem'>): void {
  if (storage.getItem(MIGRATION_V6_KEY)) return;
  try {
    const raw = storage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      const v = blob.widgetStartupAnimation;
      if (!('widgetTransitionAnimation' in blob) &&
          typeof v === 'string' && (WIDGET_STARTUP_ANIMATIONS as string[]).includes(v)) {
        blob.widgetTransitionAnimation = v;
        storage.setItem(GLOBAL_KEY, JSON.stringify(blob));
      }
    }
    storage.setItem(MIGRATION_V6_KEY, '1');
  } catch { /* migration is best-effort */ }
}
migrateV6TransitionAnim(localStorage);

// Exported for tests ([S-24]); runs against localStorage at module init below.
export function migrateV7TransitionAnimMs(storage: Pick<Storage, 'getItem' | 'setItem'>): void {
  if (storage.getItem(MIGRATION_V7_KEY)) return;
  try {
    const raw = storage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      const v = blob.widgetStartupAnimationMs;
      if (!('widgetTransitionAnimationMs' in blob) && typeof v === 'number' && Number.isFinite(v)) {
        blob.widgetTransitionAnimationMs = v;
        storage.setItem(GLOBAL_KEY, JSON.stringify(blob));
      }
    }
    storage.setItem(MIGRATION_V7_KEY, '1');
  } catch { /* migration is best-effort */ }
}
migrateV7TransitionAnimMs(localStorage);

// Exported for tests ([S-16]); runs against localStorage at module init below,
// AFTER v5/v6/v7 so the startup style/duration it seeds from are already enums.
export function migrateV8UnlockAnim(storage: Pick<Storage, 'getItem' | 'setItem'>): void {
  if (storage.getItem(MIGRATION_V8_KEY)) return;
  try {
    const raw = storage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      if (!('widgetUnlockAnimation' in blob)) {
        // The legacy boolean defaulted ON, so an ABSENT value means "replay".
        if (blob.widgetStartupAnimationOnUnlock === false) {
          blob.widgetUnlockAnimation = 'none';
        } else {
          const sa = blob.widgetStartupAnimation;
          if (typeof sa === 'string' && (WIDGET_STARTUP_ANIMATIONS as string[]).includes(sa)) {
            blob.widgetUnlockAnimation = sa;
          }
          const sm = blob.widgetStartupAnimationMs;
          if (!('widgetUnlockAnimationMs' in blob) && typeof sm === 'number' && Number.isFinite(sm)) {
            blob.widgetUnlockAnimationMs = sm;
          }
        }
        storage.setItem(GLOBAL_KEY, JSON.stringify(blob));
      }
    }
    storage.setItem(MIGRATION_V8_KEY, '1');
  } catch { /* migration is best-effort */ }
}
migrateV8UnlockAnim(localStorage);

// Exported for tests ([S-16]); runs against localStorage at module init below.
export function migrateV9DropExtreme(storage: Pick<Storage, 'getItem' | 'setItem'>): void {
  if (storage.getItem(MIGRATION_V9_KEY)) return;
  try {
    const raw = storage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      let changed = false;
      for (const k of ['widgetStartupAnimation', 'widgetTransitionAnimation', 'widgetUnlockAnimation'] as const) {
        if (blob[k] === 'hyperspace-extreme') { blob[k] = 'hyperspace'; changed = true; }
      }
      if (changed) storage.setItem(GLOBAL_KEY, JSON.stringify(blob));
    }
    storage.setItem(MIGRATION_V9_KEY, '1');
  } catch { /* migration is best-effort */ }
}
migrateV9DropExtreme(localStorage);

// [DI-06] Claim the one-shot repair of a pre-0.15 desktop icon hide. Returns
// true at most ONCE per profile, and only when the stored setting is `false` —
// i.e. only when Themia is the one that hid the icons with the old
// `ShowWindow(SW_HIDE)`, which Explorer never learned about. A hide the USER
// made from the desktop context menu is already the shell's own and is
// indistinguishable from ours by inspection, so it must not be repaired.
//
// The marker is written on EVERY call, including the `false` ones: the check
// itself is what is consumed, otherwise a later legitimate shell-owned hide
// (which looks identical from here) would be "repaired" into a toggle that
// shows the icons the user just hid. Exported for tests ([S-16]); the caller
// performs the IPC so this stays a pure storage decision.
export function claimLegacyDesktopIconRepair(storage: Pick<Storage, 'getItem' | 'setItem'>): boolean {
  if (storage.getItem(MIGRATION_V10_KEY)) return false;
  let hiddenByThemia = false;
  try {
    const raw = storage.getItem(GLOBAL_KEY);
    if (raw) {
      const blob = JSON.parse(raw) as Record<string, unknown>;
      hiddenByThemia = blob.desktopIconsVisible === false;
    }
  } catch {
    // ignore: a corrupt blob tells us nothing about who hid the icons, and
    // guessing here would un-hide a desktop the user asked to keep clean.
  }
  storage.setItem(MIGRATION_V10_KEY, '1');
  return hiddenByThemia;
}

if (claimLegacyDesktopIconRepair(localStorage)) {
  invoke('repair_legacy_desktop_icon_hide')
    .catch((e) => logger.warn('design', `repair_legacy_desktop_icon_hide failed: ${e}`));
}

// ---------------------------------------------------------------------------
// Global design store (singleton)
// ---------------------------------------------------------------------------

function loadGlobal(): GlobalDesign {
  return loadPersisted(GLOBAL_KEY, globalDesignSchema, localStorage, {
    onCorrupt: (m) => logger.error('design', m),
  });
}

export const [globalDesign, setGlobalDesignState] = createStore<GlobalDesign>(loadGlobal());

// Persist ONE design field by merging it onto the latest value read back from
// storage, never the whole in-memory blob. WebView2's cross-window `storage`
// events are unreliable (sync.ts mirrors writes over a BroadcastChannel for
// exactly this reason), so a window can hold a STALE copy of a field another
// window just changed. Writing the full in-memory blob would silently revert
// that field, e.g. Developer Mode enabled in the config window, then clobbered
// back to false when a widget window later saves an unrelated design tweak, so
// devMode looks like it "resets" on the next launch. Read-modify-write keeps
// every sibling field at its on-disk value, so a change made in one window
// survives a save from another and across restarts.
function persistGlobalDesignField<K extends keyof GlobalDesign>(key: K, value: GlobalDesign[K]): void {
  const latest = loadGlobal();
  latest[key] = value;
  savePersistedBare(GLOBAL_KEY, latest, sharedStorage, {
    onError: (m) => logger.error('design', m),
  });
}

export function setGlobalDesign<K extends keyof GlobalDesign>(key: K, value: GlobalDesign[K]): void {
  setGlobalDesignState({ [key]: value } as Partial<GlobalDesign>);
  persistGlobalDesignField(key, value);
  applyGlobalDesign();
}

/**
 * Set SEVERAL design fields as one read-modify-write and one re-apply.
 *
 * Applying a design preset changes 13 fields at once. Doing that as 13
 * setGlobalDesign() calls would be 13 storage round-trips, 13 cross-window
 * broadcasts and 13 full palette re-applies — and, worse, would leave the
 * design half-applied between them, which another window (or this one's own
 * `onStorageKey` listener) can observe as a flash of a design that is neither
 * the old one nor the new one.
 *
 * Same read-modify-write discipline as persistGlobalDesignField: sibling fields
 * are merged onto the value read back from storage, never onto a possibly-stale
 * in-memory blob, so a field another window changed is not silently reverted.
 */
export function setGlobalDesignFields(patch: Partial<GlobalDesign>): void {
  if (Object.keys(patch).length === 0) return;
  setGlobalDesignState(patch);
  const latest = loadGlobal();
  Object.assign(latest, patch);
  savePersistedBare(GLOBAL_KEY, latest, sharedStorage, {
    onError: (m) => logger.error('design', m),
  });
  applyGlobalDesign();
}

onStorageKey(GLOBAL_KEY, () => {
  setGlobalDesignState(loadGlobal());
  applyGlobalDesign();
});

export function resolvedTheme(): 'dark' | 'light' {
  if (globalDesign.theme === 'system') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return globalDesign.theme;
}

// ---------------------------------------------------------------------------
// Intro mode → design key resolvers ([S-24]/[S-16])
//
// One place that maps the live intro mode to its style + duration field, so the
// grid, the boot overlay and the replay wiring never duplicate the ternary.
// Reactive reads: call inside a tracking scope to re-pick on a design change.
// ---------------------------------------------------------------------------

export function introAnimFor(mode: IntroAnimMode): WidgetStartupAnimation {
  return mode === 'transition' ? globalDesign.widgetTransitionAnimation
    : mode === 'unlock' ? globalDesign.widgetUnlockAnimation
    : globalDesign.widgetStartupAnimation;
}

export function introMsFor(mode: IntroAnimMode): number {
  return mode === 'transition' ? globalDesign.widgetTransitionAnimationMs
    : mode === 'unlock' ? globalDesign.widgetUnlockAnimationMs
    : globalDesign.widgetStartupAnimationMs;
}

// ---------------------------------------------------------------------------
// Colour conversion (issue #44)
//
// A hex chosen in Settings is applied to `--card`/`--header-bg`/etc. as an
// `rgba(r,g,b,a)` string. The conversion itself is LOSSLESS: `#171717` →
// `rgb(23,23,23)` exactly; the reported "got #111111" is the semi-transparent
// glass widget composited over the desktop at opacity < 1, not a parse bug.
//
// applyGlobalDesign() used to inline `parseInt(hex.slice(1,3),16)` (×3) four
// times. That inline parser had a real latent defect at the trust boundary:
// a non-canonical stored hex (shorthand `#fff`, a missing `#`, or junk)
// silently produced NaN or entirely wrong channels (`rgba(NaN,NaN,NaN,a)` is
// invalid CSS → the widget paints with NO fill, i.e. the colour genuinely does
// not match the one set). The ColorPicker emits canonical lowercase #rrggbb,
// but import/export, cross-window sync and the v4 header-seed migration can all
// carry non-canonical values. One validated helper closes that gap.
//
// Parse a hex colour to its [r,g,b] channels (0..255). Accepts canonical
// #rrggbb, shorthand #rgb, a missing '#', and any case; returns null for an
// unparseable value (so the caller decides the fallback, never silent NaN).
// Exported for the issue_44 characterization tests.
export function hexToRgb(hex: string): [number, number, number] | null {
  let h = (hex ?? '').trim().replace(/^#/, '').toLowerCase();
  if (/^[0-9a-f]{3}$/.test(h)) h = h.replace(/(.)/g, '$1$1'); // #rgb → #rrggbb
  if (!/^[0-9a-f]{6}$/.test(h)) return null;
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}

// Build an `rgba(r,g,b,a)` string from a hex colour. An unparseable hex falls
// back to a defined opaque-at-given-alpha black (never `rgba(NaN,…)`, which is
// invalid CSS → the widget would paint with no fill). The caller supplies a
// theme default for the ''-means-default case, so reaching the fallback implies
// a genuinely corrupt stored value.
// Exported for the issue_44 characterization tests.
export function hexToRgba(hex: string, alpha: number): string {
  const rgb = hexToRgb(hex);
  if (!rgb) {
    logger.warn('design', `hexToRgba: unparseable colour ${JSON.stringify(hex)} → black fallback`);
    return `rgba(0,0,0,${alpha})`;
  }
  return `rgba(${rgb[0]},${rgb[1]},${rgb[2]},${alpha})`;
}

// ---------------------------------------------------------------------------
// CSS variable application
//
// applyGlobalDesign() writes the full theme palette + widget appearance to
// :root. Widgets and the config window inherit these via the CSS cascade. It
// is called once at module init and after every globalDesign change.
// ---------------------------------------------------------------------------

export function applyGlobalDesign(): void {
  const el = document.documentElement;
  const theme = resolvedTheme();
  const isDark = theme === 'dark';

  if (isDark) {
    el.style.setProperty('--card-hover',        'rgba(255, 255, 255, 0.06)');
    el.style.setProperty('--border',            'rgba(255, 255, 255, 0.08)');
    el.style.setProperty('--foreground',        'rgba(255, 255, 255, 0.886)');
    el.style.setProperty('--muted-foreground',  'rgba(255, 255, 255, 0.544)');
    el.style.setProperty('--subtle-foreground', 'rgba(255, 255, 255, 0.36)');
    el.style.setProperty('--ring',              'rgba(255, 255, 255, 0.2)');
    el.style.setProperty('--popover-bg',        'rgba(26, 26, 28, 0.97)');
    el.style.setProperty('--config-bg',         'rgb(26, 26, 28)');
    el.style.setProperty('--overlay-hover',     'rgba(255, 255, 255, 0.08)');
    el.style.setProperty('--overlay-subtle',    'rgba(255, 255, 255, 0.05)');
    el.style.setProperty('--overlay-border',    'rgba(255, 255, 255, 0.1)');
    // Plate behind a third party's favicon: those are drawn for a browser tab,
    // so they assume a light background and a dark mark would vanish here. Not
    // pure white — that glares against a dark surface. [S-47]
    el.style.setProperty('--brand-plate',        'rgba(250, 250, 250, 0.94)');
    el.style.setProperty('--brand-plate-border', 'rgba(255, 255, 255, 0.14)');
    el.style.setProperty('--success',           '#4ade80');
    el.style.setProperty('--warning',           '#fbbf24');
    el.style.setProperty('--danger-subtle-bg',  'rgba(248, 113, 113, 0.14)');
  } else {
    el.style.setProperty('--card-hover',        'rgba(0, 0, 0, 0.04)');
    el.style.setProperty('--border',            'rgba(0, 0, 0, 0.08)');
    el.style.setProperty('--foreground',        'rgba(0, 0, 0, 0.87)');
    el.style.setProperty('--muted-foreground',  'rgba(0, 0, 0, 0.54)');
    el.style.setProperty('--subtle-foreground', 'rgba(0, 0, 0, 0.36)');
    el.style.setProperty('--ring',              'rgba(0, 0, 0, 0.2)');
    el.style.setProperty('--popover-bg',        'rgba(255, 255, 255, 0.97)');
    el.style.setProperty('--config-bg',         'rgb(255, 255, 255)');
    el.style.setProperty('--overlay-hover',     'rgba(0, 0, 0, 0.06)');
    el.style.setProperty('--overlay-subtle',    'rgba(0, 0, 0, 0.04)');
    el.style.setProperty('--overlay-border',    'rgba(0, 0, 0, 0.1)');
    // Same plate on a light surface: white, where the hairline border is what
    // separates it from the card behind it. [S-47]
    el.style.setProperty('--brand-plate',        'rgba(255, 255, 255, 1)');
    el.style.setProperty('--brand-plate-border', 'rgba(0, 0, 0, 0.1)');
    el.style.setProperty('--success',           '#16a34a');
    el.style.setProperty('--warning',           '#d97706');
    el.style.setProperty('--danger-subtle-bg',  'rgba(239, 68, 68, 0.1)');
  }

  // Config window: keep the OPAQUE root background in sync with the theme.
  // index.html paints `documentElement.style.background` INLINE on the config
  // window's first frame (before this module runs) to avoid a flash. An inline
  // style outranks the `:root { background: var(--config-bg) }` rule, so the
  // value set at window-open would otherwise win forever — open Settings while
  // dark, pick a light preset, and the stale dark inline background stays under
  // the now-dark text (dark-on-dark, unusable). Repaint it here on every theme
  // change so it always matches. Widget windows have no `config-window` class
  // and are left transparent (they show the desktop through).
  if (document.body?.classList.contains('config-window')) {
    el.style.background = isDark ? 'rgb(26, 26, 28)' : 'rgb(255, 255, 255)';
  }

  el.style.setProperty('--destructive', '#ef4444');
  el.style.setProperty('--item-gap',    '0px');
  el.style.setProperty('--widget-gap',  `${globalDesign.widgetGap}px`);
  // NOTE: --widget-intro-duration is NOT set here. The intro duration is
  // mode-dependent ([S-24] startup vs transition), so WidgetGrid owns it,
  // set inline on the grid root from the mode-picked design key.

  // General UI scale. Drives the config window (`body.config-window`) and the
  // widget window's `.popup-layer` via CSS; it does NOT scale widgets. The
  // widget window's content is scaled separately by `--monitor-scale`.
  el.style.setProperty('--ui-scale', String(globalDesign.uiScale));

  // Global font (issue #62/#64). --font-family maps the OPEN fontFamily value
  // (preset id OR installed family name) to a concrete CSS stack via
  // resolveFontStack (which quotes a custom family and refuses injection);
  // App.css sets the root `font-family` from it and widgets inherit (they use
  // `font-family: inherit`), so the family cascades app-wide. --font-scale
  // multiplies the root font-size (App.css `html`); text in `rem`/`em` or
  // inheriting the base size scales, but widgets that hardcode px font-sizes do
  // NOT (a known limitation, tracked for a later px→scalable pass).
  el.style.setProperty('--font-family', resolveFontStack(globalDesign.fontFamily));
  el.style.setProperty('--font-scale', String(globalDesign.fontScale));

  // Widget appearance, global (was per-view before the design refactor).
  const wc = globalDesign.widgetBgColor || (isDark ? '#1e1e1e' : '#ffffff');
  el.style.setProperty('--card', hexToRgba(wc, globalDesign.widgetBgOpacity));

  // [S-21] One switch for every backdrop-filter in the app. Set as an attribute
  // on the same element the CSS variables go on, so the override rule in
  // App.css can turn the whole class of effects off in one place instead of
  // each of the ~20 surfaces opting out. `blur(0px)` would NOT do: a zero-radius
  // filter still promotes the surface to its own compositing layer and still
  // costs a per-frame readback, which is the expense we are removing.
  if (globalDesign.reduceEffects) {
    el.setAttribute('data-reduce-effects', '');
  } else {
    el.removeAttribute('data-reduce-effects');
  }

  // Backdrop-filter blur tracks opacity so the slider feels right; at low
  // opacity the blurred backdrop would otherwise keep painting through and
  // make widgets look opaque even at near-zero card alpha.
  el.style.setProperty('--widget-blur', `${Math.round(globalDesign.widgetBgOpacity * 60)}px`);

  // Header surface: same recipe as --card, but driven by the independent
  // header fields so the header can differ from the body it sits on. Defaults
  // mirror the body (see migrateV4) so an upgrade is visually a no-op.
  const hc = globalDesign.headerBgColor || (isDark ? '#1e1e1e' : '#ffffff');
  el.style.setProperty('--header-bg', hexToRgba(hc, globalDesign.headerBgOpacity));
  el.style.setProperty('--header-blur', `${Math.round(globalDesign.headerBgOpacity * 60)}px`);

  const pc = globalDesign.primaryColor || (isDark ? '#60cdff' : '#0078d4');
  const [pr, pg, pb] = hexToRgb(pc) ?? hexToRgb(isDark ? '#60cdff' : '#0078d4')!;
  el.style.setProperty('--primary', pc);
  el.style.setProperty('--primary-subtle-bg',     `rgba(${pr},${pg},${pb},${isDark ? 0.12 : 0.1})`);
  el.style.setProperty('--primary-subtle-border', `rgba(${pr},${pg},${pb},${isDark ? 0.35 : 0.3})`);
  // Stronger fill reserved for item SELECTION (folder / shortcut icons), so a
  // selected icon reads clearly without recolouring every other subtle-primary
  // surface that shares --primary-subtle-bg.
  el.style.setProperty('--primary-selection-bg',  `rgba(${pr},${pg},${pb},${isDark ? 0.26 : 0.18})`);
  // Text/icon colour for accent-FILLED surfaces (e.g. the primary "Leave edit
  // mode" button). The accent is usually light (cyan/blue), where white text
  // fails contrast and reads as a washed-out white blob, so pick black or
  // white by the accent's luminance instead of always using --foreground.
  const primaryLum = (0.2126 * pr + 0.7152 * pg + 0.0722 * pb) / 255;
  el.style.setProperty('--primary-foreground', primaryLum > 0.6 ? 'rgba(0,0,0,0.88)' : '#ffffff');

  el.style.setProperty('--radius', `${globalDesign.cornerRadius}px`);

  el.style.setProperty('--widget-border-style', globalDesign.widgetBorderStyle);
  el.style.setProperty('--widget-border-width', `${globalDesign.widgetBorderWidth}px`);
  const bc = globalDesign.widgetBorderColor || (isDark ? '#ffffff' : '#000000');
  el.style.setProperty('--widget-border-color', hexToRgba(bc, globalDesign.widgetBorderOpacity));

  // [DI-04] `desktopIconsVisible` is deliberately NOT pushed here. Desktop icon
  // visibility is Explorer's own persisted setting now, so this function — which
  // re-runs on every unrelated design change — must not re-assert a stale stored
  // value over a change the user made from the desktop context menu. The stored
  // field is a first-paint cache for the toggle; SettingsModal adopts the live
  // state on mount and pushes only on user change.
}

// Apply the palette once at module init so :root has theme vars before any
// component renders.
applyGlobalDesign();
