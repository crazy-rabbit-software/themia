// The geocoding search behind the Weather and Weather Forecast config Source
// tabs: weather-current [S-13][S-14][S-17], weather-forecast [S-19][S-23].
//
// Both widgets carried a line-for-line copy of this. The pure parts live here
// so they can be tested without a DOM: vitest runs without the Solid JSX
// transform (see src/connect/visibility.test.ts), so LocationSearch.tsx itself
// is only reachable by the source scans at the bottom of this file.

import { describe, it, expect } from 'vitest';
import { readSource } from '../../tests/cssRules.ts';
import { en } from '../locales/en.ts';
import {
  geocodeUrl, parseGeoResults, formatLocationName, formatResultSub, formatCoords,
  hasLocation, GEOCODE_MIN_CHARS, GEOCODE_DEBOUNCE_MS, GEOCODE_RESULT_COUNT,
  type GeoResult,
} from './locationSearch.ts';

const zurich: GeoResult = {
  id: 1, name: 'Zürich', admin1: 'Zurich', country: 'Switzerland',
  latitude: 47.3667, longitude: 8.55,
};

describe('geocodeUrl', () => {
  it('[S-17] targets open-meteo geocoding, asking for the documented result count', () => {
    const url = geocodeUrl('Bern');
    expect(url).toBe(
      'https://geocoding-api.open-meteo.com/v1/search?name=Bern' +
      `&count=${GEOCODE_RESULT_COUNT}&language=en&format=json`,
    );
  });

  it('percent-encodes the query, so a city with a space or & stays one param', () => {
    expect(geocodeUrl('New York')).toContain('name=New%20York');
    expect(geocodeUrl('A&B')).toContain('name=A%26B');
    // The injected value must not be able to introduce a parameter of its own.
    expect(geocodeUrl('x&count=999')).toContain(`&count=${GEOCODE_RESULT_COUNT}&`);
    expect(geocodeUrl('x&count=999')).not.toContain('count=999&count');
  });

  it('the debounce/min-length constants match the spec', () => {
    expect(GEOCODE_DEBOUNCE_MS).toBe(400);
    expect(GEOCODE_MIN_CHARS).toBe(2);
    expect(GEOCODE_RESULT_COUNT).toBe(6);
  });
});

describe('parseGeoResults: the API response is a trust boundary', () => {
  it('keeps well-formed entries', () => {
    const out = parseGeoResults({ results: [zurich] });
    expect(out).toEqual([zurich]);
  });

  it('reads an entry with no admin1/country (both are optional upstream)', () => {
    const bare = { id: 2, name: 'Atlantis', latitude: 0, longitude: 0 };
    expect(parseGeoResults({ results: [bare] })).toEqual([bare]);
  });

  it('drops entries missing a name or coordinates rather than rendering undefined', () => {
    const out = parseGeoResults({
      results: [
        zurich,
        { id: 3, name: 'No coords' },
        { id: 4, latitude: 1, longitude: 2 },
        { id: 5, name: 'String coords', latitude: '1', longitude: '2' },
        { id: 6, name: 'NaN coords', latitude: Number.NaN, longitude: 2 },
      ],
    });
    expect(out).toEqual([zurich]);
  });

  it('drops a non-string admin1/country instead of stringifying it into the label', () => {
    const out = parseGeoResults({
      results: [{ ...zurich, admin1: 42, country: { name: 'CH' } }],
    });
    expect(out).toEqual([{
      id: 1, name: 'Zürich', latitude: 47.3667, longitude: 8.55,
    }]);
  });

  it('reads "absent" (no match) as an empty list, not a failure', () => {
    expect(parseGeoResults({})).toEqual([]);
    expect(parseGeoResults({ results: [] })).toEqual([]);
    // open-meteo omits `results` entirely when nothing matches.
    expect(parseGeoResults({ generationtime_ms: 0.2 })).toEqual([]);
  });

  it('reads a corrupt payload as an empty list rather than throwing', () => {
    expect(parseGeoResults(null)).toEqual([]);
    expect(parseGeoResults(undefined)).toEqual([]);
    expect(parseGeoResults('nope')).toEqual([]);
    expect(parseGeoResults({ results: 'nope' })).toEqual([]);
    expect(parseGeoResults({ results: [null, 7, 'x'] })).toEqual([]);
  });

  it('caps at the requested count even if the API over-delivers', () => {
    const many = Array.from({ length: 20 }, (_, i) => ({ ...zurich, id: i }));
    expect(parseGeoResults({ results: many })).toHaveLength(GEOCODE_RESULT_COUNT);
  });
});

describe('formatLocationName', () => {
  it('[S-14] joins name, admin1 and country', () => {
    expect(formatLocationName(zurich)).toBe('Zürich, Zurich, Switzerland');
  });

  it('skips the parts the result does not carry', () => {
    expect(formatLocationName({ ...zurich, admin1: undefined })).toBe('Zürich, Switzerland');
    expect(formatLocationName({ id: 1, name: 'Atlantis', latitude: 0, longitude: 0 }))
      .toBe('Atlantis');
  });
});

describe('formatResultSub', () => {
  it('is the region line under the result name: admin1 and country only', () => {
    expect(formatResultSub(zurich)).toBe('Zurich, Switzerland');
    expect(formatResultSub({ ...zurich, admin1: undefined })).toBe('Switzerland');
    expect(formatResultSub({ id: 1, name: 'Atlantis', latitude: 0, longitude: 0 })).toBe('');
  });
});

describe('hasLocation', () => {
  it('is true only when both coordinates are real numbers', () => {
    expect(hasLocation(47.3667, 8.55)).toBe(true);
    expect(hasLocation(null, null)).toBe(false);
    expect(hasLocation(undefined, undefined)).toBe(false);
    expect(hasLocation(47.3667, null)).toBe(false);
    expect(hasLocation(null, 8.55)).toBe(false);
    expect(hasLocation(Number.NaN, 8.55)).toBe(false);
  });

  it('regression: a coordinate of 0 is a location, not an empty config', () => {
    // Both configs asked `lat() && lon()`, so a widget saved on the equator or
    // the prime meridian reopened with its city gone and the search box back.
    expect(hasLocation(0, 0)).toBe(true);
    expect(hasLocation(51.4779, 0)).toBe(true);  // Greenwich
    expect(hasLocation(0, 32.5)).toBe(true);     // Lake Victoria
  });
});

describe('formatCoords', () => {
  it('is the fallback label when a saved location has no name', () => {
    expect(formatCoords(47.3667, 8.55)).toBe('47.367, 8.550');
  });

  it('keeps three decimals for a negative/zero coordinate', () => {
    expect(formatCoords(-0.1276, 0)).toBe('-0.128, 0.000');
  });
});

// ── The extraction itself ────────────────────────────────────────────────────
//
// Source scans, because vitest cannot compile the JSX: these pin that the copy
// is gone and cannot quietly grow back, and that the audit findings the merge
// fixed stay fixed.

const CONFIGS = [
  'widgets/online/weather-current/WeatherCurrentConfig.tsx',
  'widgets/online/weather-forecast/WeatherForecastConfig.tsx',
  // Sun & Moon joined them when its coordinate steppers were replaced by the
  // shared picker: nobody knows their own latitude off the top of their head.
  'widgets/others/sun-moon/SunMoonConfig.tsx',
];

describe('LocationSearch is the one copy of the geocoding search', () => {
  it.each(CONFIGS)('%s delegates to the shared component', (f) => {
    const src = readSource(f);
    expect(src).toMatch(/import LocationSearch from '.*components\/LocationSearch\.tsx'/);
    expect(src).toContain('<LocationSearch');
  });

  it.each(CONFIGS)('%s no longer carries a second copy of the search', (f) => {
    const src = readSource(f);
    // The signals, the debounce, the fetch and the pick handler: the whole
    // block was duplicated line-for-line across these two files.
    expect(src, 'geocoding fetch').not.toContain('geocoding-api.open-meteo.com');
    expect(src, 'search state').not.toMatch(/setSearchResults|setSearchErr|setSearching/);
    expect(src, 'debounce').not.toContain('debounceTimer');
    expect(src, 'result type').not.toContain('interface GeoResult');
  });

  it.each(CONFIGS)('%s has no stylesheet of its own left to drift', (f) => {
    // Both were identical to LocationSearch.css apart from the class prefix.
    expect(readSource(f)).not.toMatch(/import '\.\/Weather\w+Config\.css'/);
  });
});

describe('LocationSearch obeys the settings design system', () => {
  const tsx = () => readSource('src/components/LocationSearch.tsx');
  // Comments stripped: the file's header documents the classes this merge
  // deleted BY NAME, and the point of these assertions is that no rule
  // declares them -- not that the prose can't mention them.
  const css = () => readSource('src/components/LocationSearch.css').replace(/\/\*[\s\S]*?\*\//g, '');

  it('[S-08] every user-visible string goes through t()', () => {
    const src = tsx();
    for (const english of ['No locations found.', 'Search failed.', 'Search city', '>Change<']) {
      expect(src, `hardcoded "${english}"`).not.toContain(english);
    }
    expect(src).toContain("t('locationSearch.placeholder')");
    expect(src).toContain("t('locationSearch.searching')");
    expect(src).toContain("t('locationSearch.noResults')");
    expect(src).toContain("t('locationSearch.failed')");
    expect(src).toContain("t('common.change')");
  });

  it('[S-09] no module-level const freezes a translated label', () => {
    // The component reads t() inside the JSX, so the labels re-read on a
    // language change. Guarded repo-wide by settingsControls.test.ts; asserted
    // here because this file is new.
    expect(tsx()).not.toMatch(/^const\s+\w+\s*=\s*[[{][\s\S]*?\bt\(/m);
  });

  it('the ellipsis is the … character, not three dots', () => {
    for (const v of [en.locationSearch.placeholder, en.locationSearch.searching]) {
      expect(v).toContain('…');
      expect(v).not.toContain('...');
    }
    expect(tsx()).not.toContain("'...'");
  });

  it('[S-11] reuses the documented button roles instead of re-declaring them', () => {
    // `.clear-btn` was the cancel button with different numbers; `.search-btn`
    // was an ad-hoc sixth style, and the button it styled was a second way to
    // trigger the search the input already runs on its own.
    expect(tsx()).toContain('class="btn-secondary"');
    expect(css()).not.toContain('.clear-btn');
    expect(css()).not.toContain('.search-btn');
    expect(tsx()).not.toContain('search-btn');
  });

  it('a failed search reads as an error; no match reads as an empty state', () => {
    const src = tsx();
    // `.err-text` painted BOTH --muted-foreground, so a failure looked like a
    // hint. Only the failure is destructive now: it carries the error modifier,
    // the plain hint does not. (Both live inside the floating panel -- [S-27].)
    expect(src).toMatch(/class="locsearch-hint locsearch-hint-error">\{t\('locationSearch\.failed'\)\}/);
    expect(src).toMatch(/class="locsearch-hint">\{t\('locationSearch\.noResults'\)\}/);
    expect(css()).toMatch(/\.locsearch-hint-error\s*\{[^}]*var\(--destructive\)/);
    expect(css()).not.toContain('.err-text');
  });

  it('[S-27] the hints and results float in an overlay, not the config flow', () => {
    const src = tsx();
    // Portal into the shared popup layer, wearing the app's one dropdown chrome
    // (fixed + --z-dropdown), so the results overlay the sections below the
    // input instead of pushing them down. Same path Dropdown uses.
    expect(src).toContain("import { Portal } from 'solid-js/web'");
    expect(src).toMatch(/import \{ popupMount \} from '.*popupLayer\.ts'/);
    expect(src).toMatch(/<Portal mount=\{popupMount\(\)\}>/);
    expect(src).toContain('class="dropdown-panel locsearch-panel"');
    // Dismissable: a click outside the input and panel closes it.
    expect(src).toContain('handleClickOutside');
  });

  it('the selected city is a .design-row, not a bespoke row shape', () => {
    expect(tsx()).toContain('class="design-row"');
    expect(css()).not.toContain('.current-loc');
    expect(css()).not.toContain('.loc-name');
  });

  it('[S-05] no stylesheet rule hardcodes the accent', () => {
    expect(css()).not.toMatch(/#60cdff|#0078d4/i);
  });

  it('[S-18] every class it owns is prefixed, so none can collide under .cfg', () => {
    const selectors = [...css().replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/^([.@][^{]+)\{/gm)]
      .map(m => m[1].trim());
    expect(selectors.length).toBeGreaterThan(3);
    for (const sel of selectors) {
      expect(sel, `${sel} is not scoped to this component`)
        .toMatch(/^\.(location-search|locsearch-)/);
    }
  });
});

describe('locationSearch i18n keys ship in every locale', () => {
  const KEYS = ['placeholder', 'searching', 'noResults', 'failed'] as const;
  const LOCALES = ['en', 'de', 'es', 'fr', 'it', 'ja', 'pl', 'pt', 'zh', 'ru'];

  // Read as source: importing all nine would duplicate locales.test.ts, which
  // already owns key parity. This only pins that none was skipped.
  it.each(LOCALES)('%s declares the whole block', (loc) => {
    const src = readSource(`src/locales/${loc}.ts`);
    const block = src.match(/\n {2}locationSearch: \{([\s\S]*?)\n {2}\},/);
    expect(block, `${loc} has no locationSearch block`).not.toBeNull();
    for (const k of KEYS) expect(block![1], `${loc}.${k}`).toContain(`${k}: '`);
  });

  it.each(LOCALES.filter(l => l !== 'en'))('%s is actually translated, not copied English', (loc) => {
    const block = readSource(`src/locales/${loc}.ts`)
      .match(/\n {2}locationSearch: \{([\s\S]*?)\n {2}\},/)![1];
    expect(block).not.toContain('No locations found.');
    expect(block).not.toContain('Search failed.');
    expect(block).not.toContain('Search city…');
  });
});
