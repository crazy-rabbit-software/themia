import { createSignal, createEffect, createMemo, onMount, onCleanup, Show, Switch, Match, For, type Accessor, type JSX } from 'solid-js';
import type { UpdateInfo } from '../updater.ts';
import { globalDesign, resolvedTheme } from '../design.ts';
import { totalWidgetCount } from '../layout.ts';
import { viewStore, defaultNewDisplayView, setDefaultViewForNewDisplays } from '../views.ts';
import { viewInitials } from '../viewState.ts';
import { licenseState, activateLicense, deactivateLicense, isPro } from '../license.ts';
import { injectDemoViews, removeDemoViews } from '../demo/views.ts';
import { loadSystemFonts } from '../systemFonts.ts';
import { invoke } from '../ipc.ts';
import { parseAutostartError, type AutostartError } from '../autostartError.ts';
import { watchDesktopIcons } from '../desktopIcons.ts';
import { checkForUpdate, installUpdate } from '../updater.ts';
import { ask } from '@tauri-apps/plugin-dialog';
import { Settings, Palette, Crown, Users, Monitor, Moon, Sun, Download, Upload, Bug, Lightbulb, Check, Eye, EyeOff, ScrollText, Trash2, Copy, FolderOpen, Mail, Share2, History, ChevronRight, Square, SquareDashed, SquareDot, Ban, CircleX, TriangleAlert, Info, ListFilter, Blend, Scaling, MoveUp, MoveHorizontal, MoveVertical, FlipVertical, Aperture, Power, Rocket, Terminal, Play, Droplets, BookOpen, Sparkles, SlidersHorizontal } from 'lucide-solid';
import type { LucideIcon } from 'lucide-solid';
import { CHANGELOG } from '../changelog.ts';
import { logs, sources, clearLogs, exportLogsAsText, logger, type LogLevel } from '../debugLog';
import Dropdown, { type DropdownOption } from './Dropdown.tsx';
import { VIEW_ICON_MAP } from './IconPicker.tsx';
import ColorPicker from '../ColorPicker.tsx';
import ToggleSwitch from './ToggleSwitch.tsx';
import AccountManager from './AccountManager.tsx';
import NewsletterRow from './NewsletterRow.tsx';
import { accountStore, loadAccounts, bumpRevision } from '../accountStore.ts';
import { exportSettings, importSettings } from '../importExport.ts';
import { postTransient } from '../sync.ts';
import NumberStepper from './NumberStepper.tsx';
import { gameModeSuspend, setGameModeSuspend, gameModeActive } from '../gameMode.ts';
import { devViewsEnabled, setDevViewsEnabled } from '../dev/labView.ts';
import { t } from '../i18n.ts';
import { formatTime, formatDate } from '../formatters.ts';
import { setGlobalDesign as setGlobalDesignDirect } from '../design.ts';
import { DESIGN_PRESETS, applyDesignPreset, activeDesignPreset, presetPreview, type PresetId } from '../designPresets.ts';
import { setAnalyticsConsent } from '../analytics.ts';
import type { GlobalDesign, WidgetStartupAnimation } from '../design.ts';
import './SettingsModal.css';

const SUPPORT_EMAIL = 'themia@nathaniel-walser.com';
// Locale-agnostic on purpose: the docs site resolves the reader's language from
// the browser and redirects, so a hardcoded /de/docs would fight that.
const DOCS_URL = 'https://themia.app/docs';

const PRESET_ICONS: Record<PresetId, LucideIcon> = {
  default: Sparkles,
  midnight: Moon,
  daylight: Sun,
  glass: Droplets,
  terminal: Terminal,
  paper: BookOpen,
};

// A single preset tile: a mock widget rendered from the preset's own colours
// (presetPreview mirrors applyGlobalDesign's defaulting, so the thumbnail shows
// the same look selecting the tile would apply) plus its name. Selecting it is
// one atomic write ([S-13]); the live-matching tile reads `active` ([S-14]).
function PresetCard(props: { id: PresetId }): JSX.Element {
  const preset = DESIGN_PRESETS.find(p => p.id === props.id)!;
  const pv = presetPreview(preset.values);
  const Icon = PRESET_ICONS[props.id];
  const active = () => activeDesignPreset() === props.id;
  return (
    <button
      type="button"
      class="preset-card"
      classList={{ active: active() }}
      aria-pressed={active()}
      title={t(preset.labelKey)}
      onClick={() => applyDesignPreset(props.id)}
    >
      <span class="preset-thumb" style={{ background: pv.backdrop }}>
        <span
          class="preset-mock"
          style={{
            background: pv.cardBg,
            'border-radius': `${Math.min(pv.radius, 12)}px`,
            'border-style': pv.borderStyle,
            'border-width': `${pv.borderWidth}px`,
            'border-color': pv.borderColor,
            'font-family': pv.fontStack,
          }}
        >
          <span class="preset-mock-header" style={{ background: pv.headerBg }}>
            <span class="preset-mock-dot" style={{ background: pv.primary }} />
            <span class="preset-mock-bar" style={{ background: pv.mutedForeground }} />
          </span>
          <span class="preset-mock-body">
            <span class="preset-mock-aa" style={{ color: pv.foreground }}>Aa</span>
            <span class="preset-mock-lines">
              <span class="preset-mock-line" style={{ background: pv.foreground }} />
              <span class="preset-mock-line short" style={{ background: pv.mutedForeground }} />
              <span class="preset-mock-pill" style={{ background: pv.primary }} />
            </span>
          </span>
        </span>
      </span>
      <span class="preset-name">
        <Icon size={12} strokeWidth={1.5} />
        {t(preset.labelKey)}
      </span>
      <Show when={active()}>
        <span class="preset-check"><Check size={12} strokeWidth={2.5} /></span>
      </Show>
    </button>
  );
}

// The "Custom" tile: a status indicator, NOT an action. It sits after the named
// presets and lights up (active) whenever the live design matches no preset
// (Settings.SPEC.md [S-14]), so a user who has tweaked any owned field sees at a
// glance that they're on their own look. Its thumbnail previews the CURRENT
// design (read live from globalDesign), and it is a <div>, not a <button> —
// there is no "custom" bundle to apply, so it never applies anything on click.
function CustomPresetCard(): JSX.Element {
  const active = () => activeDesignPreset() === null;
  const pv = createMemo(() => presetPreview({ ...globalDesign, theme: resolvedTheme() }));
  return (
    <div
      class="preset-card preset-card-custom"
      classList={{ active: active() }}
      aria-current={active() ? 'true' : undefined}
      title={t('settings.style.preset.custom')}
    >
      <span class="preset-thumb" style={{ background: pv().backdrop }}>
        <span
          class="preset-mock"
          style={{
            background: pv().cardBg,
            'border-radius': `${Math.min(pv().radius, 12)}px`,
            'border-style': pv().borderStyle,
            'border-width': `${pv().borderWidth}px`,
            'border-color': pv().borderColor,
            'font-family': pv().fontStack,
          }}
        >
          <span class="preset-mock-header" style={{ background: pv().headerBg }}>
            <span class="preset-mock-dot" style={{ background: pv().primary }} />
            <span class="preset-mock-bar" style={{ background: pv().mutedForeground }} />
          </span>
          <span class="preset-mock-body">
            <span class="preset-mock-aa" style={{ color: pv().foreground }}>Aa</span>
            <span class="preset-mock-lines">
              <span class="preset-mock-line" style={{ background: pv().foreground }} />
              <span class="preset-mock-line short" style={{ background: pv().mutedForeground }} />
              <span class="preset-mock-pill" style={{ background: pv().primary }} />
            </span>
          </span>
        </span>
      </span>
      <span class="preset-name">
        <SlidersHorizontal size={12} strokeWidth={1.5} />
        {t('settings.style.preset.custom')}
      </span>
      <Show when={active()}>
        <span class="preset-check"><Check size={12} strokeWidth={2.5} /></span>
      </Show>
    </div>
  );
}

// One option list for BOTH intro-style dropdowns (startup [S-15] and view
// transition [S-24]): the styles are the same closed enum, only the moment
// they play differs. A function (not a const) so the labels re-read t() after
// a language change.
function introAnimOptions(): DropdownOption[] {
  return [
    { label: t('settings.style.startupAnim.none'), value: 'none', icon: () => <Ban size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.fade'), value: 'fade', icon: () => <Blend size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.scale'), value: 'scale', icon: () => <Scaling size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.slide'), value: 'slide', icon: () => <MoveUp size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.sides'), value: 'sides', icon: () => <MoveHorizontal size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.topbottom'), value: 'topbottom', icon: () => <MoveVertical size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.flip'), value: 'flip', icon: () => <FlipVertical size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.blur'), value: 'blur', icon: () => <Aperture size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.hyperspace'), value: 'hyperspace', icon: () => <Rocket size={13} strokeWidth={1.5} /> },
    { label: t('settings.style.startupAnim.hacker'), value: 'hacker', icon: () => <Terminal size={13} strokeWidth={1.5} /> },
  ];
}

// One animation subsection ([S-15]/[S-16]/[S-24]): identical controls for the
// startup, unlock and view-transition intros: a style dropdown, a duration
// stepper with a ▶ preview button, and (startup only, via `children`) the
// matching startup sound. The dropdown change and the duration change both
// preview live (the `mode` selects which section the widget windows replay),
// so the user sees the picked style cascade in.
function AnimSection(p: {
  mode: 'startup' | 'unlock' | 'transition';
  anim: () => WidgetStartupAnimation;
  setAnim: (v: WidgetStartupAnimation) => void;
  ms: () => number;
  setMs: (v: number) => void;
  children?: JSX.Element;
}) {
  const preview = () => postTransient({ type: 'startup-anim-replay', mode: p.mode });
  return (
    <>
      <div class="design-row">
        <div class="design-left">
          <span class="design-label">{t('settings.style.animRowLabel')}</span>
          <span class="design-desc">{t(`settings.style.${p.mode}AnimDesc`)}</span>
        </div>
        <Dropdown
          value={p.anim()}
          options={introAnimOptions()}
          onChange={(v) => {
            p.setAnim(v as WidgetStartupAnimation);
            // Preview the newly picked style as a full replay ([S-16]); 'none'
            // has nothing to show.
            if (v !== 'none') preview();
          }}
        />
      </div>
      <Show when={p.anim() !== 'none'}>
        <div class="design-row">
          <div class="design-left">
            <span class="design-label">{t('settings.style.animSpeed.label')}</span>
            <span class="design-desc">{t('settings.style.animSpeed.desc')}</span>
          </div>
          <div class="anim-speed-controls">
            <NumberStepper
              value={p.ms()}
              min={100}
              max={5000}
              step={100}
              unit="ms"
              onChange={(v) => { p.setMs(v); preview(); }}
            />
            <button
              class="design-btn"
              title={t('settings.style.animPlay')}
              onClick={() => preview()}
            ><Play size={13} strokeWidth={1.5} /></button>
          </div>
        </div>
        {p.children}
      </Show>
    </>
  );
}

// Parse an ISO YYYY-MM-DD as a local-time date (avoids UTC-midnight shift) and
// render it in the user's locale, e.g. "April 22, 2026" / "22. April 2026".
function formatChangelogDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  if (!y || !m || !d) return iso;
  return formatDate(new Date(y, m - 1, d), { year: 'numeric', month: 'long', day: 'numeric' });
}

export type SettingsPage = 'general' | 'style' | 'animations' | 'license' | 'accounts' | 'updates' | 'logs';

// Tree nodes for the App Settings group. Read inside a reactive scope so the
// Logs node appears/disappears with Developer Mode.
export function navItems(): { page: SettingsPage; label: string; icon: LucideIcon }[] {
  const items: { page: SettingsPage; label: string; icon: LucideIcon }[] = [
    { page: 'general', label: t('settings.nav.general'), icon: Settings },
    { page: 'style', label: t('settings.nav.style'), icon: Palette },
    { page: 'animations', label: t('settings.nav.animations'), icon: Sparkles },
    { page: 'accounts', label: t('settings.nav.accounts'), icon: Users },
    { page: 'license', label: t('settings.nav.license'), icon: Crown },
    { page: 'updates', label: t('settings.nav.updates'), icon: History },
  ];
  if (globalDesign.devMode) items.push({ page: 'logs', label: t('settings.nav.logs'), icon: ScrollText });
  return items;
}

interface SettingsBodyProps {
  activePage: Accessor<SettingsPage>;
  setActivePage: (page: SettingsPage) => void;
  setGlobalDesign: <K extends keyof GlobalDesign>(key: K, value: GlobalDesign[K]) => void;
  autostart: Accessor<boolean>;
  setAutostart: (v: boolean) => void;
  autoCheckUpdates: Accessor<boolean>;
  setAutoCheckUpdates: (v: boolean) => void;
  analyticsEnabled: Accessor<boolean>;
  setAnalyticsEnabled: (v: boolean) => void;
  winLabel: string;
  appVersion: Accessor<string>;
  updateAvail: Accessor<UpdateInfo | null>;
  setUpdateAvail: (v: UpdateInfo | null) => void;
  updateStatus: Accessor<'idle' | 'downloading' | 'done' | 'error'>;
  setUpdateStatus: (v: 'idle' | 'downloading' | 'done' | 'error') => void;
}

// The App Settings content panel for the unified config window. Renders the
// General / Accounts / License / Logs pages; navigation is driven by the
// ConfigTree, so `activePage` is a prop rather than internal state.
export default function SettingsBody(props: SettingsBodyProps) {
  const activePage = props.activePage;

  const updateAvail = () => props.updateAvail();
  const setUpdateAvail = props.setUpdateAvail;
  const updateStatus = () => props.updateStatus();
  const setUpdateStatus = props.setUpdateStatus;
  const [checkingUpdate, setCheckingUpdate] = createSignal(false);
  const [checkedOnce, setCheckedOnce] = createSignal(!!props.updateAvail());
  const [updateError, setUpdateError] = createSignal<string | null>(null);
  // [AS-05] Last failed "Launch on startup" flip, shown inline under the row
  // until the next successful flip. Transient UI state, not persisted: the
  // remedy is acted on outside Themia, so re-opening Settings should show a
  // clean toggle to try again rather than a stale warning.
  const [autostartError, setAutostartError] = createSignal<AutostartError | null>(null);
  const [licenseKeyInput, setLicenseKeyInput] = createSignal('');
  const [licenseKeyCopied, setLicenseKeyCopied] = createSignal(false);
  const [licenseKeyVisible, setLicenseKeyVisible] = createSignal(false);
  const [licenseKeyRaw, setLicenseKeyRaw] = createSignal<string | null>(null);
  // Changelog: only the newest entry is expanded by default; older entries collapse.
  const [expandedChangelog, setExpandedChangelog] = createSignal<Set<string>>(
    new Set(CHANGELOG.length > 0 ? [CHANGELOG[0].version] : []),
  );
  function toggleChangelog(version: string) {
    setExpandedChangelog(prev => {
      const next = new Set(prev);
      if (next.has(version)) next.delete(version); else next.add(version);
      return next;
    });
  }
  const CHANGELOG_COLLAPSED_COUNT = 3;
  const [showAllChangelog, setShowAllChangelog] = createSignal(false);
  const visibleChangelog = () =>
    showAllChangelog() ? CHANGELOG : CHANGELOG.slice(0, CHANGELOG_COLLAPSED_COUNT);

  // `licenseState.key` is masked by the backend (****-****-****-XXXX). The
  // raw key lives in the on-disk cache and is only fetched on demand when
  // the user explicitly reveals or copies it.
  async function fetchRawLicenseKey(): Promise<string | null> {
    const cached = licenseKeyRaw();
    if (cached) return cached;
    try {
      const k = await invoke('license_get_key');
      if (k) setLicenseKeyRaw(k);
      return k ?? null;
    } catch { return null; }
  }

  async function handleCopyLicenseKey() {
    const raw = await fetchRawLicenseKey();
    if (!raw) return;
    try {
      await navigator.clipboard.writeText(raw);
      setLicenseKeyCopied(true);
      setTimeout(() => setLicenseKeyCopied(false), 1500);
    } catch { /* ignore */ }
  }

  const devtoolsKey = () => `themia-devtools-open-${props.winLabel}`;
  const [devtoolsOpen, setDevtoolsOpen] = createSignal(localStorage.getItem(devtoolsKey()) === 'true');

  // Installed system fonts for the font picker (issue #64). Loaded once, lazily;
  // empty until it resolves (and on non-Windows / failure), so the picker shows
  // the four curated presets immediately and grows the full list when it lands.
  const [systemFonts, setSystemFonts] = createSignal<string[]>([]);

  // Font-picker options: the four curated presets first, then every installed
  // family. Labels via t() so a language switch re-reads the preset names; the
  // Dropdown itself filters this list as the user searches.
  const fontOptions = createMemo<DropdownOption[]>(() => [
    { label: t('settings.font.system'), value: 'system' },
    { label: t('settings.font.sans'), value: 'sans' },
    { label: t('settings.font.serif'), value: 'serif' },
    { label: t('settings.font.mono'), value: 'mono' },
    ...systemFonts().map((f) => ({ label: f, value: f })),
  ]);

  onMount(() => {
    // Load accounts for the Accounts tab
    loadAccounts();
    // Installed fonts for the font picker (issue #64); best-effort, non-blocking.
    loadSystemFonts().then(setSystemFonts);
    // If App already checked and found an update, mark as checked
    if (props.updateAvail()) setCheckedOnce(true);
    // [AS-06] Dry-run the autostart permission so the warning is already there
    // when the user arrives, rather than only after a click that does nothing
    // visible (#69/#79). The toggle stays enabled either way: the remedy is to
    // relaunch elevated, and the user must be able to retry without reopening
    // Settings. A click's own result always wins over this — success clears the
    // warning, a real failure replaces it.
    invoke('autostart_can_configure')
      .then(() => setAutostartError(null))
      .catch((e) => {
        const err = parseAutostartError(e);
        logger.warn('settings', `autostart_can_configure failed [${err.kind}]: ${err.message}`);
        setAutostartError(err);
      });
  });

  // [DI-04] Adopt the live desktop icon state. Explorer owns this setting, so
  // the user may have changed it from the desktop context menu (or in an
  // earlier run) since we stored it; the toggle must show reality, not a stale
  // cache. The watcher re-reads on focus/visibility — the config window stays
  // open across such a change, so a mount-only read would sit lying about it.
  const desktopIcons = watchDesktopIcons({
    read: () => invoke('get_desktop_icons'),
    stored: () => globalDesign.desktopIconsVisible,
    apply: (visible) => props.setGlobalDesign('desktopIconsVisible', visible),
    onError: (e) => logger.warn('settings', `get_desktop_icons failed: ${e}`),
  });
  onCleanup(() => desktopIcons.dispose());
  // General is the only page carrying the toggle: poll while it is up (which
  // refreshes immediately on arrival), stop on the way out.
  createEffect(() => desktopIcons.poll(activePage() === 'general'));

  // If Developer Mode is turned off while on the Logs page, fall back to General.
  createEffect(() => {
    if (!globalDesign.devMode && activePage() === 'logs') props.setActivePage('general');
  });

  // [LOG-02] Tell the backend while the Logs page is visible: in release
  // builds the Webview log target only forwards INFO/DEBUG records while at
  // least one viewer is open (WARN+ always flows). Balanced open/close;
  // onCleanup covers closing the modal while still on the Logs page.
  let logStreamOpen = false;
  const setLogStream = (open: boolean) => {
    if (open === logStreamOpen) return;
    logStreamOpen = open;
    invoke('set_log_stream', { open })
      .catch(() => { /* ignore: streaming gate is best-effort, never break the UI */ });
  };
  createEffect(() => setLogStream(activePage() === 'logs'));
  onCleanup(() => setLogStream(false));

  const [logFilter, setLogFilter] = createSignal<string>('');
  const [logLevel, setLogLevel] = createSignal<'all' | LogLevel>('all');
  const [logAutoScroll, setLogAutoScroll] = createSignal(true);
  let logContainerEl: HTMLDivElement | undefined;

  const LEVEL_RANK: Record<LogLevel, number> = { error: 4, warn: 3, info: 2, debug: 1 };

  const filteredLogs = () => {
    const f = logFilter();
    const lv = logLevel();
    const min = lv === 'all' ? 0 : LEVEL_RANK[lv];
    const all = logs();
    return all.filter(e =>
      (!f || e.source === f) &&
      (!min || LEVEL_RANK[e.level] >= min)
    );
  };

  createEffect(() => {
    filteredLogs();
    const onLogs = activePage() === 'logs';
    if (!onLogs || !logAutoScroll()) return;
    requestAnimationFrame(() => {
      if (logContainerEl) logContainerEl.scrollTop = logContainerEl.scrollHeight;
    });
  });

  function handleLogScroll() {
    if (!logContainerEl) return;
    const { scrollTop, scrollHeight, clientHeight } = logContainerEl;
    setLogAutoScroll(scrollHeight - scrollTop - clientHeight < 40);
  }

  function formatTs(ts: number): string {
    const d = new Date(ts);
    return formatTime(d, { hour12: false }) + '.' + String(d.getMilliseconds()).padStart(3, '0');
  }

  function handleCopyLogs() {
    const lv = logLevel();
    const text = exportLogsAsText(logFilter() || undefined, lv === 'all' ? undefined : lv);
    navigator.clipboard.writeText(text).catch(() => {});
  }

  // [LOG-08] The buffer above covers this session only; the rotated files are
  // what goes on a bug report, so send the user to them rather than writing a
  // second copy of what is already on disk.
  function handleOpenLogFolder() {
    invoke('open_log_folder')
      .catch((e) => logger.error('settings', `open_log_folder failed: ${e}`));
  }

  return (
    <div class="settings-content cfg">
      <Switch>
        <Match when={activePage() === 'general'}>
          {/* ── General ── */}
          <span class="section-label">{t('settings.section.general')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.language.label')}</span>
              <span class="design-desc">{t('settings.language.desc')}</span>
            </div>
            <Dropdown
              value={globalDesign.language}
              searchable
              searchPlaceholder={t('settings.language.search')}
              options={[
                { label: t('settings.language.english'), value: 'en' },
                { label: t('settings.language.german'), value: 'de' },
                { label: t('settings.language.spanish'), value: 'es' },
                { label: t('settings.language.french'), value: 'fr' },
                { label: t('settings.language.portuguese'), value: 'pt' },
                { label: t('settings.language.italian'), value: 'it' },
                { label: t('settings.language.polish'), value: 'pl' },
                { label: t('settings.language.japanese'), value: 'ja' },
                { label: t('settings.language.chinese'), value: 'zh' },
                { label: t('settings.language.russian'), value: 'ru' },
              ]}
              onChange={(v) => setGlobalDesignDirect('language', v as 'en' | 'de' | 'es' | 'fr' | 'pt' | 'it' | 'pl' | 'ja' | 'zh' | 'ru')}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.autostart.label')}</span>
              <span class="design-desc">{t('settings.autostart.desc')}</span>
            </div>
            <ToggleSwitch
              checked={props.autostart()}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={async (next) => {
                try {
                  if (next) { await invoke('autostart_enable'); props.setAutostart(true); }
                  else { await invoke('autostart_disable'); props.setAutostart(false); }
                  setAutostartError(null);
                } catch (e) {
                  // [AS-05] The toggle refuses to move on failure, so the reason
                  // is shown INLINE under the row rather than as a toast: the
                  // remedy ("run Themia as administrator") is a step taken
                  // outside the app, and a message that auto-dismisses while the
                  // user reads it is what made #69/#79 look like a dead control.
                  const err = parseAutostartError(e);
                  logger.warn('settings', `autostart_${next ? 'enable' : 'disable'} failed [${err.kind}]: ${err.message}`);
                  setAutostartError(err);
                }
              }}
            />
          </div>
          <Show when={autostartError()}>
            {(err) => (
              <div class="cfg-callout" role="alert">
                <TriangleAlert class="cfg-callout-icon" size={14} strokeWidth={1.5} />
                <div class="cfg-callout-text">
                  <span class="cfg-callout-title">
                    {err().kind === 'elevationRequired'
                      ? t('settings.autostart.adminNeeded.title')
                      : t('settings.autostart.failed.title')}
                  </span>
                  <span class="cfg-callout-desc">
                    {err().kind === 'elevationRequired'
                      ? t('settings.autostart.adminNeeded.desc')
                      : t('settings.autostart.failed.desc')}
                  </span>
                  {/* The raw schtasks/IO diagnostic, kept under the advice so a
                      bug report can quote it. Never the ONLY thing shown: on its
                      own it tells the user something broke and nothing to do
                      about it, which is what #69/#79 already felt like. */}
                  <Show when={err().message}>
                    <span class="cfg-callout-detail">{err().message}</span>
                  </Show>
                </div>
              </div>
            )}
          </Show>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.autoUpdates.label')}</span>
              <span class="design-desc">{t('settings.autoUpdates.desc')}</span>
            </div>
            <ToggleSwitch
              checked={props.autoCheckUpdates()}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => {
                props.setAutoCheckUpdates(next);
                localStorage.setItem('themia-auto-check-updates', String(next));
              }}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.defaultView.label')}</span>
              <span class="design-desc">{t('settings.defaultView.desc')}</span>
            </div>
            <Dropdown
              value={defaultNewDisplayView()}
              options={viewStore.list.map((s): DropdownOption => ({
                label: s.name,
                value: s.id,
                icon: s.icon && VIEW_ICON_MAP[s.icon]
                  ? () => VIEW_ICON_MAP[s.icon!](14)
                  : () => <span class="view-dropdown-letters">{viewInitials(s.name)}</span>,
              }))}
              onChange={(v) => setDefaultViewForNewDisplays(v)}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.desktopIcons.label')}</span>
              <span class="design-desc">{t('settings.desktopIcons.desc')}</span>
            </div>
            <ToggleSwitch
              checked={globalDesign.desktopIconsVisible}
              onLabel={t('toggle.shown')}
              offLabel={t('toggle.hidden')}
              onChange={(next) => {
                props.setGlobalDesign('desktopIconsVisible', next);
                // [DI-01]/[DI-03] Only place that posts the shell toggle. The
                // change is Explorer's own persisted setting from here on: it
                // survives quitting Themia, explorer restarting and reboot, and
                // nothing re-asserts the stored value.
                invoke('set_desktop_icons', { visible: next })
                  .catch((e) => logger.warn('settings', `set_desktop_icons failed: ${e}`));
              }}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">
                {t('settings.gameMode.label')}
                <Show when={gameModeSuspend() && gameModeActive()}>
                  <span style={{ 'margin-left': '6px', color: 'var(--primary)', 'font-size': '10px' }}>{t('settings.gameMode.activeNow')}</span>
                </Show>
              </span>
              <span class="design-desc">{t('settings.gameMode.desc')}</span>
            </div>
            <ToggleSwitch
              checked={gameModeSuspend()}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => setGameModeSuspend(next)}
            />
          </div>
          {/* [S-21] Performance escape hatch, next to game mode because it
              belongs to the same "make Themia cost less right now" group. */}
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.reduceEffects.label')}</span>
              <span class="design-desc">{t('settings.reduceEffects.desc')}</span>
            </div>
            <ToggleSwitch
              checked={globalDesign.reduceEffects}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => props.setGlobalDesign('reduceEffects', next)}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.privateNetwork.label')}</span>
              <span class="design-desc">{t('settings.privateNetwork.desc')}</span>
            </div>
            <ToggleSwitch
              checked={globalDesign.allowPrivateNetworkAccess}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => props.setGlobalDesign('allowPrivateNetworkAccess', next)}
            />
          </div>
          <div class="divider" />
          <span class="section-label">{t('settings.section.data')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.analytics.label')}</span>
              <span class="design-desc">{t('settings.analytics.desc')}</span>
            </div>
            <ToggleSwitch
              checked={props.analyticsEnabled()}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => {
                props.setAnalyticsEnabled(next);
                // The same command the first-run prompt calls: one decision,
                // one code path. The backend hands back a signal only when a
                // consent that was actually given is withdrawn, which is what
                // authorises exactly one app_opt_out before analytics goes
                // quiet ([S-07]).
                setAnalyticsConsent(next)
                  .catch(e => logger.error('analytics', `analytics_set_consent failed: ${e}`));
              }}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.exportSettings.label')}</span>
              <span class="design-desc">{t('settings.exportSettings.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => exportSettings().catch(e => logger.error('settings', `Export failed: ${e}`))}>
              <Download size={13} strokeWidth={1.5} /> {t('settings.exportSettings.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.importSettings.label')}</span>
              <span class="design-desc">{t('settings.importSettings.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => importSettings().catch(e => logger.error('settings', `Import failed: ${e}`))}>
              <Upload size={13} strokeWidth={1.5} /> {t('settings.importSettings.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.resetAll.label')}</span>
              <span class="design-desc">{t('settings.resetAll.desc')}</span>
            </div>
            <button
              class="btn-danger"
              onClick={async () => {
                // Test hook: Tauri 2 freezes `__TAURI_INTERNALS__.invoke` so
                // e2e specs can't intercept `ask()` at the IPC level. A
                // `window.__settingsHooks.askReset` setter under webdriver
                // lets tests pre-decide accept/reject.
                const hook = typeof window !== 'undefined'
                  ? (window as Window & { __settingsHooks?: { askReset?: () => Promise<boolean> } }).__settingsHooks
                  : null;
                const ok = hook?.askReset
                  ? await hook.askReset()
                  : await ask(t('settings.resetAll.confirm'), {
                      title: t('settings.resetAll.label'),
                      kind: 'warning',
                    });
                if (!ok) return;
                localStorage.clear();
                window.location.reload();
              }}
            >
              <Trash2 size={13} strokeWidth={1.5} /> {t('settings.resetAll.action')}
            </button>
          </div>

          <div class="divider" />
          <span class="section-label">{t('settings.section.application')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.quit.label')}</span>
              <span class="design-desc">{t('settings.quit.desc')}</span>
            </div>
            <button
              class="btn-danger"
              onClick={async () => {
                // Mirror the Reset hook: Tauri 2 freezes the IPC `invoke` so
                // e2e specs can't intercept `ask()`. A `window.__settingsHooks
                // .askQuit` setter under webdriver lets tests pre-decide
                // accept/reject without a real dialog.
                const hook = typeof window !== 'undefined'
                  ? (window as Window & { __settingsHooks?: { askQuit?: () => Promise<boolean> } }).__settingsHooks
                  : null;
                const ok = hook?.askQuit
                  ? await hook.askQuit()
                  : await ask(t('settings.quit.confirm'), {
                      title: t('settings.quit.label'),
                      kind: 'warning',
                    });
                if (!ok) return;
                // `quit_app` calls AppHandle::exit, the process tears down and
                // this promise never resolves, so log a rejection only if the
                // command itself errors before exit.
                invoke('quit_app').catch(e => logger.error('settings', `Quit failed: ${e}`));
              }}
            >
              <Power size={13} strokeWidth={1.5} /> {t('settings.quit.action')}
            </button>
          </div>

          <div class="divider" />
          <span class="section-label">{t('settings.section.help')}</span>
          {/* First row in Help: the reference for every widget and screen lives
              on the site, so offer it before "report a bug". */}
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.docs.label')}</span>
              <span class="design-desc">{t('settings.docs.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: DOCS_URL })}>
              <BookOpen size={13} strokeWidth={1.5} /> {t('settings.docs.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.bug.label')}</span>
              <span class="design-desc">{t('settings.bug.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: 'https://github.com/crazy-rabbit-software/themia/issues/new?template=bug-report.yml' })}>
              <Bug size={13} strokeWidth={1.5} /> {t('settings.bug.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.feature.label')}</span>
              <span class="design-desc">{t('settings.feature.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: 'https://github.com/crazy-rabbit-software/themia/issues/new?template=widget-request.yml' })}>
              <Lightbulb size={13} strokeWidth={1.5} /> {t('settings.feature.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.support.label')}</span>
              <span class="design-desc">{t('settings.support.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: `mailto:${SUPPORT_EMAIL}` })}>
              <Mail size={13} strokeWidth={1.5} /> {t('settings.support.action')}
            </button>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.share.label')}</span>
              <span class="design-desc">{t('settings.share.desc')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: 'https://reddit.com/r/themia/submit' })}>
              <Share2 size={13} strokeWidth={1.5} /> {t('settings.share.action')}
            </button>
          </div>

          {/* ── Developer (kept last) ── */}
          <div class="divider" />
          <span class="section-label">{t('settings.section.developer')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.devMode.label')}</span>
              <span class="design-desc">{t('settings.devMode.desc')}</span>
            </div>
            <ToggleSwitch
              checked={globalDesign.devMode}
              onLabel={t('toggle.enabled')}
              offLabel={t('toggle.disabled')}
              onChange={(next) => {
                props.setGlobalDesign('devMode', next);
                if (!next && devtoolsOpen()) {
                  setDevtoolsOpen(false);
                  localStorage.removeItem(devtoolsKey());
                  invoke('close_devtools', { label: props.winLabel });
                }
                // [LOG-05] Logging is a dev-only feature: leaving Developer
                // Mode fully disables it (same contract as DevTools above).
                if (!next && globalDesign.loggingEnabled) {
                  props.setGlobalDesign('loggingEnabled', false);
                }
              }}
            />
          </div>
          <Show when={globalDesign.devMode}>
            <div class="design-row">
              <div class="design-left">
                <span class="design-label">{t('settings.logging.label')}</span>
                <span class="design-desc">{t('settings.logging.desc')}</span>
              </div>
              <ToggleSwitch
                checked={globalDesign.loggingEnabled}
                onLabel={t('toggle.enabled')}
                offLabel={t('toggle.disabled')}
                onChange={(next) => props.setGlobalDesign('loggingEnabled', next)}
              />
            </div>
            <div class="design-row">
              <div class="design-left">
                <span class="design-label">{t('settings.devtools.label')}</span>
                <span class="design-desc">{t('settings.devtools.desc')}</span>
              </div>
              <ToggleSwitch
                checked={devtoolsOpen()}
                onLabel={t('toggle.open')}
                offLabel={t('toggle.closed')}
                onChange={(next) => {
                  setDevtoolsOpen(next);
                  if (next) {
                    localStorage.setItem(devtoolsKey(), 'true');
                    invoke('show_devtools', { label: props.winLabel });
                  } else {
                    localStorage.removeItem(devtoolsKey());
                    invoke('close_devtools', { label: props.winLabel });
                  }
                }}
              />
            </div>
            <div class="design-row">
              <div class="design-left">
                <span class="design-label">{t('settings.devViews.label')}</span>
                <span class="design-desc">{t('settings.devViews.desc')}</span>
              </div>
              <ToggleSwitch
                checked={devViewsEnabled()}
                onLabel={t('toggle.enabled')}
                offLabel={t('toggle.disabled')}
                onChange={(next) => setDevViewsEnabled(next)}
              />
            </div>
            <div class="design-row">
              <div class="design-left">
                <span class="design-label">{t('settings.demo.label')}</span>
                <span class="design-desc">{t('settings.demo.desc')}</span>
              </div>
              <ToggleSwitch
                checked={globalDesign.demoMode}
                onLabel={t('toggle.enabled')}
                offLabel={t('toggle.disabled')}
                onChange={(next) => {
                  if (next) injectDemoViews();
                  else removeDemoViews();
                  props.setGlobalDesign('demoMode', next);
                }}
              />
            </div>
          </Show>
        </Match>

        <Match when={activePage() === 'style'}>
          {/* ── Style ── */}
          <span class="section-label">{t('settings.section.appearance')}</span>
          {/* Design preset ([S-12]..[S-15]). Each tile previews the look and,
              when picked, overrides the 13 appearance fields below in a single
              write; touching any of them afterwards drops the picker to Custom.
              Deliberately does NOT own UI scale, font size or the animation
              settings (their own page) — those are the user's accessibility and
              motion choices, not part of a look. */}
          <div class="design-row stacked">
            <div class="design-left">
              <span class="design-label">{t('settings.style.preset.label')}</span>
              <span class="design-desc">{t('settings.style.preset.desc')}</span>
            </div>
            <div class="preset-grid">
              <For each={DESIGN_PRESETS}>{(p) => <PresetCard id={p.id} />}</For>
              <CustomPresetCard />
            </div>
          </div>
          <div class="divider" />
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.theme.label')}</span>
              <span class="design-desc">{t('settings.theme.desc')}</span>
            </div>
            <div class="theme-seg">
              <button class={globalDesign.theme === 'system' ? 'active' : ''} onClick={() => props.setGlobalDesign('theme', 'system')}><Monitor size={13} strokeWidth={1.5} /> {t('settings.theme.system')}</button>
              <button class={globalDesign.theme === 'dark' ? 'active' : ''} onClick={() => props.setGlobalDesign('theme', 'dark')}><Moon size={13} strokeWidth={1.5} /> {t('settings.theme.dark')}</button>
              <button class={globalDesign.theme === 'light' ? 'active' : ''} onClick={() => props.setGlobalDesign('theme', 'light')}><Sun size={13} strokeWidth={1.5} /> {t('settings.theme.light')}</button>
            </div>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.primaryColor.label')}</span>
              <span class="design-desc">{t('settings.style.primaryColor.desc')}</span>
            </div>
            <div class="color-wrap">
              <Show when={globalDesign.primaryColor}>
                <button class="btn-reset" onClick={() => props.setGlobalDesign('primaryColor', '')}>{t('settings.style.reset')}</button>
              </Show>
              <ColorPicker
                value={globalDesign.primaryColor || (resolvedTheme() === 'dark' ? '#60cdff' : '#0078d4')}
                onChange={(hex) => props.setGlobalDesign('primaryColor', hex)} />
            </div>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.uiScale.label')}</span>
              <span class="design-desc">{t('settings.uiScale.desc')}</span>
            </div>
            <NumberStepper
              value={Math.round(globalDesign.uiScale * 100)}
              min={50}
              max={200}
              step={10}
              unit="%"
              onChange={(v) => props.setGlobalDesign('uiScale', v / 100)}
            />
          </div>
          {/* Global font (issue #62): family (closed set) + size scale. */}
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.font.familyLabel')}</span>
              <span class="design-desc">{t('settings.font.familyDesc')}</span>
            </div>
            <Dropdown
              value={globalDesign.fontFamily}
              options={fontOptions()}
              searchable
              searchPlaceholder={t('settings.font.searchPlaceholder')}
              onChange={(v) => props.setGlobalDesign('fontFamily', v)}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.font.sizeLabel')}</span>
              <span class="design-desc">{t('settings.font.sizeDesc')}</span>
            </div>
            <NumberStepper
              value={Math.round(globalDesign.fontScale * 100)}
              min={80}
              max={150}
              step={5}
              unit="%"
              onChange={(v) => props.setGlobalDesign('fontScale', v / 100)}
            />
          </div>
          <div class="divider" />
          <span class="section-label">{t('settings.style.widget')}</span>

          {/* ── Body surface ── */}
          <span class="subsection-label">{t('settings.style.bodySection')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.bgLabel')}</span>
              <span class="design-desc">{t('settings.style.widgetBg.desc')}</span>
            </div>
            <div class="color-wrap">
              <Show when={globalDesign.widgetBgColor}>
                <button class="btn-reset" onClick={() => props.setGlobalDesign('widgetBgColor', '')}>{t('settings.style.reset')}</button>
              </Show>
              <ColorPicker
                value={globalDesign.widgetBgColor || (resolvedTheme() === 'dark' ? '#1e1e1e' : '#ffffff')}
                onChange={(hex) => props.setGlobalDesign('widgetBgColor', hex)} />
            </div>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.opacityLabel')}</span>
              <span class="design-desc">{t('settings.style.widgetOpacity.desc')}</span>
            </div>
            <NumberStepper
              value={Math.round(globalDesign.widgetBgOpacity * 100)}
              min={0}
              max={100}
              step={5}
              unit="%"
              onChange={(v) => props.setGlobalDesign('widgetBgOpacity', v / 100)}
            />
          </div>

          {/* ── Header surface ── */}
          <span class="subsection-label">{t('settings.style.headerSection')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.bgLabel')}</span>
              <span class="design-desc">{t('settings.style.headerBg.desc')}</span>
            </div>
            <div class="color-wrap">
              <Show when={globalDesign.headerBgColor}>
                <button class="btn-reset" onClick={() => props.setGlobalDesign('headerBgColor', '')}>{t('settings.style.reset')}</button>
              </Show>
              <ColorPicker
                value={globalDesign.headerBgColor || (resolvedTheme() === 'dark' ? '#1e1e1e' : '#ffffff')}
                onChange={(hex) => props.setGlobalDesign('headerBgColor', hex)} />
            </div>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.opacityLabel')}</span>
              <span class="design-desc">{t('settings.style.headerOpacity.desc')}</span>
            </div>
            <NumberStepper
              value={Math.round(globalDesign.headerBgOpacity * 100)}
              min={0}
              max={100}
              step={5}
              unit="%"
              onChange={(v) => props.setGlobalDesign('headerBgOpacity', v / 100)}
            />
          </div>

          {/* ── Border ── */}
          <span class="subsection-label">{t('settings.style.border')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.borderStyle.label')}</span>
              <span class="design-desc">{t('settings.style.borderStyle.desc')}</span>
            </div>
            <Dropdown
              value={globalDesign.widgetBorderStyle}
              options={[
                { label: t('settings.style.solid'), value: 'solid', icon: () => <Square size={13} strokeWidth={1.5} /> },
                { label: t('settings.style.dashed'), value: 'dashed', icon: () => <SquareDashed size={13} strokeWidth={1.5} /> },
                { label: t('settings.style.dotted'), value: 'dotted', icon: () => <SquareDot size={13} strokeWidth={1.5} /> },
                { label: t('settings.style.none'), value: 'none', icon: () => <Ban size={13} strokeWidth={1.5} /> },
              ]}
              onChange={(v) => props.setGlobalDesign('widgetBorderStyle', v as GlobalDesign['widgetBorderStyle'])}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.borderWidth.label')}</span>
              <span class="design-desc">{t('settings.style.borderWidth.desc')}</span>
            </div>
            <NumberStepper
              value={globalDesign.widgetBorderWidth}
              min={0}
              max={8}
              step={1}
              unit="px"
              onChange={(v) => props.setGlobalDesign('widgetBorderWidth', v)}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.borderColor.label')}</span>
              <span class="design-desc">{t('settings.style.borderColor.desc')}</span>
            </div>
            <div class="color-wrap">
              <Show when={globalDesign.widgetBorderColor}>
                <button class="btn-reset" onClick={() => props.setGlobalDesign('widgetBorderColor', '')}>{t('settings.style.reset')}</button>
              </Show>
              <ColorPicker
                value={globalDesign.widgetBorderColor || (resolvedTheme() === 'dark' ? '#ffffff' : '#000000')}
                onChange={(hex) => props.setGlobalDesign('widgetBorderColor', hex)} />
            </div>
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.opacityLabel')}</span>
              <span class="design-desc">{t('settings.style.borderOpacity.desc')}</span>
            </div>
            <NumberStepper
              value={Math.round(globalDesign.widgetBorderOpacity * 100)}
              min={0}
              max={100}
              step={5}
              unit="%"
              onChange={(v) => props.setGlobalDesign('widgetBorderOpacity', v / 100)}
            />
          </div>

          {/* ── Shape & spacing ── */}
          <span class="subsection-label">{t('settings.style.shapeSection')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.cornerRadius.label')}</span>
              <span class="design-desc">{t('settings.style.cornerRadius.desc')}</span>
            </div>
            <NumberStepper
              value={globalDesign.cornerRadius}
              min={0}
              max={24}
              step={2}
              unit="px"
              onChange={(v) => props.setGlobalDesign('cornerRadius', v)}
            />
          </div>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.style.widgetGap.label')}</span>
              <span class="design-desc">{t('settings.style.widgetGap.desc')}</span>
            </div>
            <NumberStepper
              value={globalDesign.widgetGap}
              min={0}
              max={48}
              step={4}
              unit="px"
              onChange={(v) => props.setGlobalDesign('widgetGap', v)}
            />
          </div>

        </Match>

        <Match when={activePage() === 'animations'}>
          {/* ── Animations ([S-15]/[S-16]/[S-24]) — their own page, listed below
                 Style. Startup, unlock and view-transition intros, each with the
                 SAME controls: a style, a duration and a ▶ preview; only Startup
                 also carries the matching sound ([S-26]). Deliberately NOT part
                 of a design preset — motion is the user's accessibility choice. */}
          <span class="section-label">{t('settings.style.animationsSection')}</span>

          <span class="subsection-label">{t('settings.style.startupSection')}</span>
          <AnimSection
            mode="startup"
            anim={() => globalDesign.widgetStartupAnimation}
            setAnim={(v) => props.setGlobalDesign('widgetStartupAnimation', v)}
            ms={() => globalDesign.widgetStartupAnimationMs}
            setMs={(v) => props.setGlobalDesign('widgetStartupAnimationMs', v)}
          >
            {/* Matching startup sound ([S-26]), startup only, so it lives in
                the Startup section's children. */}
            <div class="design-row">
              <div class="design-left">
                <span class="design-label">{t('settings.style.startupSound.label')}</span>
                <span class="design-desc">{t('settings.style.startupSound.desc')}</span>
              </div>
              <ToggleSwitch
                checked={globalDesign.widgetStartupSound}
                onLabel={t('toggle.on')}
                offLabel={t('toggle.off')}
                onChange={(next) => {
                  props.setGlobalDesign('widgetStartupSound', next);
                  // Preview the sound when turning it ON (the replay carries the
                  // animation; the widget window plays the matching cue).
                  if (next) postTransient({ type: 'startup-anim-replay', mode: 'startup' });
                }}
              />
            </div>
          </AnimSection>

          <span class="subsection-label">{t('settings.style.unlockSection')}</span>
          <AnimSection
            mode="unlock"
            anim={() => globalDesign.widgetUnlockAnimation}
            setAnim={(v) => props.setGlobalDesign('widgetUnlockAnimation', v)}
            ms={() => globalDesign.widgetUnlockAnimationMs}
            setMs={(v) => props.setGlobalDesign('widgetUnlockAnimationMs', v)}
          />

          <span class="subsection-label">{t('settings.style.transitionSection')}</span>
          <AnimSection
            mode="transition"
            anim={() => globalDesign.widgetTransitionAnimation}
            setAnim={(v) => props.setGlobalDesign('widgetTransitionAnimation', v)}
            ms={() => globalDesign.widgetTransitionAnimationMs}
            setMs={(v) => props.setGlobalDesign('widgetTransitionAnimationMs', v)}
          />
        </Match>

        <Match when={activePage() === 'license'}>
          {/* ── License ── */}
          <span class="section-label">{t('settings.section.license')}</span>

          {/* Zone A: Status hero card */}
          <div class={`license-hero${isPro() ? ' pro' : ' free'}`}>
            <Show when={isPro()} fallback={
              <>
                <div class="license-hero-meta">
                  <span class="license-hero-title">
                    <Crown size={14} strokeWidth={1.5} /> {t('settings.license.heroFreeTitle')}
                  </span>
                  <span class="license-hero-sub">
                    {t('settings.license.heroUsageSummary', { widgets: totalWidgetCount(), views: viewStore.list.length })}
                  </span>
                </div>
                <div class="license-hero-cta">
                  <button
                    class="btn-secondary upgrade-btn"
                    onClick={() => invoke('open_url', { url: 'https://themia.lemonsqueezy.com/checkout/buy/cd2c2eda-81fd-454c-8d8e-7b6831ee6e8c' })}
                  ><Crown size={13} strokeWidth={1.5} /> {t('settings.license.upgradeCta')}</button>
                </div>
              </>
            }>
              <div class="license-hero-meta">
                <span class="license-hero-title">
                  <Crown size={14} strokeWidth={1.5} /> {t('settings.license.heroProTitle')}
                  <span class="license-status-pill"><span class="license-status-dot" /> {t('settings.license.heroProActive')}</span>
                </span>
                <Show when={licenseState.activated && licenseState.key}>
                  <div class="license-hero-key">
                    <span class="license-key-val">{licenseKeyVisible() ? (licenseKeyRaw() ?? licenseState.key) : (licenseState.key ?? '').replace(/./g, '•')}</span>
                    <button
                      class="btn-secondary license-copy-btn"
                      onClick={async () => {
                        if (!licenseKeyVisible()) await fetchRawLicenseKey();
                        setLicenseKeyVisible(v => !v);
                      }}
                      title={licenseKeyVisible() ? t('settings.license.hideKey') : t('settings.license.showKey')}
                    >
                      <Show when={licenseKeyVisible()} fallback={<Eye size={13} strokeWidth={1.5} />}>
                        <EyeOff size={13} strokeWidth={1.5} />
                      </Show>
                    </button>
                    <button
                      class="btn-secondary license-copy-btn"
                      onClick={handleCopyLicenseKey}
                      title={t('settings.license.copyKey')}
                    >
                      <Show when={licenseKeyCopied()} fallback={<Copy size={13} strokeWidth={1.5} />}>
                        <Check size={13} strokeWidth={1.5} />
                      </Show>
                    </button>
                  </div>
                </Show>
              </div>
              <div class="license-hero-cta">
                <button
                  class="btn-secondary"
                  disabled={licenseState.loading}
                  onClick={() => deactivateLicense()}
                >{licenseState.loading ? '...' : t('settings.license.deactivateBtn')}</button>
              </div>
            </Show>
            <Show when={!isPro() && (totalWidgetCount() >= 10 || viewStore.list.length >= 2)}>
              <div class="license-limit-warning">
                {totalWidgetCount() >= 10 ? t('settings.license.widgetLimitWarn') : t('settings.license.viewLimitWarn')} {t('settings.license.upgradeMsg')}
              </div>
            </Show>
          </div>

          {/* Quota, always visible */}
          <div class="license-quota">
            <div class="license-quota-stat" classList={{ full: !isPro() && totalWidgetCount() >= 10 }}>
              <span class="license-quota-label">{t('settings.license.quotaWidgets')}</span>
              <span class="license-quota-num">
                <span class="license-quota-cur">{totalWidgetCount()}</span>
                <span class="license-quota-sep"> / </span>
                <span class="license-quota-max">{isPro() ? '∞' : '10'}</span>
              </span>
              <Show when={!isPro()}>
                <div class="license-usage-bar">
                  <div class="license-usage-fill"
                       style={{ width: `${Math.min(100, totalWidgetCount() / 10 * 100)}%` }}
                       classList={{ full: totalWidgetCount() >= 10 }} />
                </div>
              </Show>
            </div>
            <div class="license-quota-stat" classList={{ full: !isPro() && viewStore.list.length >= 2 }}>
              <span class="license-quota-label">{t('settings.license.quotaViews')}</span>
              <span class="license-quota-num">
                <span class="license-quota-cur">{viewStore.list.length}</span>
                <span class="license-quota-sep"> / </span>
                <span class="license-quota-max">{isPro() ? '∞' : '2'}</span>
              </span>
              <Show when={!isPro()}>
                <div class="license-usage-bar">
                  <div class="license-usage-fill"
                       style={{ width: `${Math.min(100, viewStore.list.length / 2 * 100)}%` }}
                       classList={{ full: viewStore.list.length >= 2 }} />
                </div>
              </Show>
            </div>
          </div>

          {/* Zone B: Plan comparison (always shown) */}
          <div class="license-tiers">
            <div class={`license-tier${!isPro() ? ' current' : ''}`}>
              <Show when={!isPro()}>
                <span class="license-tier-tag neutral">{t('settings.license.currentPlan')}</span>
              </Show>
              <div class="license-tier-head">
                <span class="license-tier-name">{t('settings.license.free')}</span>
              </div>
              <div class="license-tier-price">$0</div>
              <ul class="license-tier-features">
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.upTo10')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.upTo2')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.allWidgets')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.freeUpdates')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.communitySupport')}</li>
              </ul>
            </div>
            <div class={`license-tier featured${isPro() ? ' current' : ''}`}>
              <span class="license-tier-tag">
                {isPro() ? t('settings.license.currentPlan') : t('settings.license.recommendedTag')}
              </span>
              <div class="license-tier-head">
                <span class="license-tier-name"><Crown size={14} strokeWidth={1.5} /> {t('settings.license.pro')}</span>
              </div>
              <div class="license-tier-price">{t('settings.license.oneTime')}</div>
              <ul class="license-tier-features">
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.unlimitedWidgets')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.unlimitedViews')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.allWidgets')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.futureUpdates')}</li>
                <li><Check size={12} strokeWidth={2} /> {t('settings.license.prioritySupport')}</li>
              </ul>
              <Show when={!isPro()}>
                <button
                  class="btn-secondary upgrade-btn license-tier-cta"
                  onClick={() => invoke('open_url', { url: 'https://themia.lemonsqueezy.com/checkout/buy/cd2c2eda-81fd-454c-8d8e-7b6831ee6e8c' })}
                ><Crown size={13} strokeWidth={1.5} /> {t('settings.license.upgradeCta')}</button>
              </Show>
            </div>
          </div>

          {/* Zone C: Activation card (Free users only) */}
          <Show when={!isPro()}>
            <div class="license-activate-card">
              <div class="license-activate-meta">
                <span class="license-activate-title">{t('settings.license.activateCardTitle')}</span>
                <span class="license-activate-desc">{t('settings.license.activateCardDesc')}</span>
              </div>
              <div class="license-activate">
                <input
                  type="text"
                  id="license-key-input"
                  class="license-input"
                  placeholder={t('settings.license.activatePlaceholder')}
                  value={licenseKeyInput()}
                  onInput={e => setLicenseKeyInput(e.target.value)}
                  onKeyDown={async e => {
                    if (e.key === 'Enter' && licenseKeyInput().trim()) {
                      await activateLicense(licenseKeyInput().trim());
                      setLicenseKeyInput('');
                    }
                  }}
                />
                <button
                  class="btn-primary"
                  disabled={licenseState.loading || !licenseKeyInput().trim()}
                  onClick={async () => {
                    await activateLicense(licenseKeyInput().trim());
                    setLicenseKeyInput('');
                  }}
                >{licenseState.loading ? '...' : t('settings.license.activateBtn')}</button>
              </div>
              <Show when={licenseState.error}>
                <span class="license-error">{licenseState.error}</span>
              </Show>
            </div>
          </Show>
          <Show when={isPro() && licenseState.error}>
            <div class="design-row">
              <span class="license-error">{licenseState.error}</span>
            </div>
          </Show>

          <div class="divider" />
          <span class="section-label">{t('settings.about.support')}</span>
          <div class="design-row">
            <div class="design-left">
              <span class="design-label">{t('settings.about.support')}</span>
            </div>
            <button class="btn-secondary" onClick={() => invoke('open_url', { url: `mailto:${SUPPORT_EMAIL}` })}
                    title={t('settings.about.supportTitle')}>
              <Mail size={13} strokeWidth={1.5} /> {SUPPORT_EMAIL}
            </button>
          </div>
        </Match>

        <Match when={activePage() === 'updates'}>
          {/* ── Updates ── */}
          <div class="update-row" classList={{ 'has-update': !!updateAvail(), 'error': !updateAvail() && checkedOnce() && !checkingUpdate() && !!updateError() }}>
            <div class="update-info">
              <span class="update-version">Themia v{props.appVersion()}</span>
              <span class="update-status">
                <Show when={updateAvail()}>
                  <Show when={updateStatus() === 'idle'}>
                    {t('settings.about.updateReady', { version: updateAvail()!.version })}
                  </Show>
                  <Show when={updateStatus() === 'downloading'}>
                    {t('settings.about.downloading')}
                  </Show>
                  <Show when={updateStatus() === 'done'}>
                    {t('settings.about.restartToFinish')}
                  </Show>
                  <Show when={updateStatus() === 'error'}>
                    {t('settings.about.updateFailed')}
                  </Show>
                </Show>
                <Show when={!updateAvail() && checkingUpdate()}>
                  {t('settings.about.checking')}
                </Show>
                <Show when={!updateAvail() && !checkingUpdate() && checkedOnce() && !updateError()}>
                  {t('settings.about.upToDateShort')}
                </Show>
                <Show when={!updateAvail() && !checkingUpdate() && checkedOnce() && updateError()}>
                  {t('settings.about.checkFailed')}
                </Show>
                <Show when={!updateAvail() && !checkingUpdate() && !checkedOnce()}>
                  {t('settings.about.notChecked')}
                </Show>
              </span>
            </div>
            <div class="update-action">
              <Show when={updateAvail() && updateStatus() === 'idle'}>
                <button class="btn-primary" onClick={async () => {
                  setUpdateStatus('downloading');
                  try {
                    await installUpdate();
                    setUpdateStatus('done');
                  } catch { setUpdateStatus('error'); }
                }}><Download size={13} strokeWidth={1.5} /> {t('settings.about.installVersion', { version: updateAvail()!.version })}</button>
              </Show>
              <Show when={!updateAvail()}>
                <button class="btn-secondary" disabled={checkingUpdate()} onClick={async () => {
                  setCheckingUpdate(true);
                  setUpdateError(null);
                  try {
                    const update = await checkForUpdate();
                    if (update) setUpdateAvail(update);
                  } catch (e) { setUpdateError(String(e)); }
                  setCheckingUpdate(false);
                  setCheckedOnce(true);
                }}>{t('settings.about.checkForUpdates')}</button>
              </Show>
            </div>
          </div>

          <div class="divider" />
          <span class="section-label">{t('settings.section.updates')}</span>
          <NewsletterRow />

          <div class="divider" />
          <span class="section-label">{t('settings.section.changelog')}</span>
          <For each={visibleChangelog()}>{(entry) => {
            const isOpen = () => expandedChangelog().has(entry.version);
            return (
              <div class="changelog-entry" classList={{ open: isOpen() }}>
                <div class="changelog-row">
                  <button type="button" class="changelog-head" onClick={() => toggleChangelog(entry.version)}>
                    <ChevronRight class="changelog-chevron" size={13} strokeWidth={2} />
                    <div class="changelog-head-text">
                      <div class="changelog-head-line">
                        <span class="changelog-version">v{entry.version}</span>
                        <span class="changelog-date">{formatChangelogDate(entry.date)}</span>
                      </div>
                      <Show when={entry.summary}>
                        <span class="changelog-summary">{entry.summary}</span>
                      </Show>
                    </div>
                  </button>
                  <button
                    type="button"
                    class="changelog-download"
                    title={t('settings.changelog.download')}
                    aria-label={t('settings.changelog.download')}
                    onClick={(e) => {
                      e.stopPropagation();
                      invoke('open_url', {
                        url: `https://github.com/crazy-rabbit-software/themia/releases/tag/v${entry.version}`,
                      }).catch(() => {});
                    }}
                  >
                    <Download size={13} strokeWidth={1.5} />
                  </button>
                </div>
                <Show when={isOpen()}>
                  <div class="changelog-body">
                    <Show when={entry.added && entry.added.length > 0}>
                      <span class="changelog-kind">{t('settings.changelog.new')}</span>
                      <ul class="changelog-list">
                        <For each={entry.added}>{(line) => <li>{line}</li>}</For>
                      </ul>
                    </Show>
                    <Show when={entry.fixed && entry.fixed.length > 0}>
                      <span class="changelog-kind">{t('settings.changelog.fixed')}</span>
                      <ul class="changelog-list">
                        <For each={entry.fixed}>{(line) => <li>{line}</li>}</For>
                      </ul>
                    </Show>
                    <Show when={(!entry.added || entry.added.length === 0) && (!entry.fixed || entry.fixed.length === 0)}>
                      <span class="changelog-empty">{t('settings.changelog.empty')}</span>
                    </Show>
                  </div>
                </Show>
              </div>
            );
          }}</For>
          <Show when={CHANGELOG.length > CHANGELOG_COLLAPSED_COUNT}>
            <button
              type="button"
              class="changelog-toggle-all"
              onClick={() => setShowAllChangelog(v => !v)}
            >
              {showAllChangelog()
                ? t('settings.changelog.showLess')
                : t('settings.changelog.showMore', { count: CHANGELOG.length - CHANGELOG_COLLAPSED_COUNT })}
            </button>
          </Show>
        </Match>

        <Match when={activePage() === 'accounts'}>
          <AccountManager
            accounts={() => accountStore.accounts}
            onMutated={async () => { await loadAccounts(); bumpRevision(); }}
          />
        </Match>

        <Match when={activePage() === 'logs'}>
          <div class="logs-page">
            <div class="logs-header">
              <span class="section-label" style={{ margin: '0' }}>{t('settings.section.debugLogs')}</span>
              <div class="logs-actions">
                <Dropdown
                  value={logFilter()}
                  options={[{ label: t('settings.logs.allSources'), value: '' }, ...[...sources()].sort().map(s => ({ label: s, value: s }))]}
                  onChange={setLogFilter}
                  class="logs-filter"
                />
                <Dropdown
                  value={logLevel()}
                  options={[
                    { label: t('settings.logs.allLevels'), value: 'all',   icon: () => <ListFilter size={13} strokeWidth={1.5} /> },
                    { label: t('settings.logs.error'),     value: 'error', icon: () => <CircleX size={13} strokeWidth={1.5} /> },
                    { label: t('settings.logs.warn'),      value: 'warn',  icon: () => <TriangleAlert size={13} strokeWidth={1.5} /> },
                    { label: t('settings.logs.info'),      value: 'info',  icon: () => <Info size={13} strokeWidth={1.5} /> },
                    { label: t('settings.logs.debug'),     value: 'debug', icon: () => <Bug size={13} strokeWidth={1.5} /> },
                  ]}
                  onChange={(v) => setLogLevel(v as 'all' | LogLevel)}
                  class="logs-filter"
                />
                <button class="logs-btn" onClick={handleCopyLogs} title={t('settings.logs.copyToClipboard')}><Copy size={13} strokeWidth={1.5} /></button>
                <button class="logs-btn" onClick={handleOpenLogFolder} title={t('settings.logs.openFolder')}><FolderOpen size={13} strokeWidth={1.5} /></button>
                <button class="logs-btn logs-btn-danger" onClick={clearLogs} title={t('settings.logs.clearLogs')}><Trash2 size={13} strokeWidth={1.5} /></button>
              </div>
            </div>
            {/* [LOG-05] Logging is opt-in, so point at the master switch when
                the page can only ever show an empty list. Debug builds log
                regardless of the toggle, so no hint there. */}
            <Show when={!globalDesign.loggingEnabled && !import.meta.env.DEV}>
              <span class="design-desc">{t('settings.logs.disabledHint')}</span>
            </Show>
            <div class="logs-count">{t('settings.logs.entries', { count: filteredLogs().length })}</div>
            <div class="logs-container cfg-scrollbar" ref={logContainerEl} onScroll={handleLogScroll}>
              <For each={filteredLogs()}>
                {(entry) => (
                  <div class={`log-line log-line-${entry.level}`}>
                    <span class="log-ts">{formatTs(entry.ts)}</span>
                    <span class={`log-level log-level-${entry.level}`}>{entry.level}</span>
                    <span class="log-source" title={entry.source}>{entry.source}</span>
                    <span class="log-msg">{entry.msg}</span>
                  </div>
                )}
              </For>
            </div>
          </div>
        </Match>
      </Switch>
    </div>
  );
}
