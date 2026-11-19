export const meta = {
  name: 'widget-audit',
  description: 'Render every widget in every state, run the machine checks, then judge every kind against every owner rule with independent reviewers and a refute pass',
  whenToUse: 'The closing check of any change to widget layout, a widget kind, the kit or the gap check. A widget change is done only when this returns pass: true.',
  phases: [
    { title: 'Render', detail: 'build, gap check, item check, renders per kind/state/size incl. hover' },
    { title: 'Review', detail: 'every kind x every rule in docs/WIDGET-RULES.md, a verdict each' },
    { title: 'Refute', detail: 'each failing verdict re-measured by a skeptic' },
    { title: 'Coverage', detail: 'missing kinds/states/rules and exemptions the owner never granted' },
  ],
}

// args: { repo: '<checkout to audit>', out: '<scratch dir for renders and reports>', exe?: '<Debug Themia.exe>', kinds?: ['calendar', ...] }
const A = args || {}
if (!A.repo || !A.out) throw new Error('widget-audit needs args.repo and args.out')
const RULES = `${A.repo}/docs/WIDGET-RULES.md`
const HYGIENE = 'Build only with --artifacts-path under args.out and -nodeReuse:false. Every Themia.exe run has a hard timeout and is killed when it overruns; never start Themia.exe without a render flag; never touch %APPDATA%\\Themia or %LOCALAPPDATA%\\Programs\\Themia. Leave no process running when you return.'

const RENDER_SCHEMA = {
  type: 'object',
  properties: {
    exe: { type: 'string' },
    kinds: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: { type: 'string' },
          states: { type: 'array', items: { type: 'string' } },
          pngs: { type: 'array', items: { type: 'string' } },
          hoverPngs: { type: 'array', items: { type: 'string' } },
        },
        required: ['kind', 'states', 'pngs', 'hoverPngs'],
      },
    },
    machine: {
      type: 'array',
      items: {
        type: 'object',
        properties: { kind: { type: 'string' }, state: { type: 'string' }, size: { type: 'string' }, check: { type: 'string' }, detail: { type: 'string' } },
        required: ['kind', 'check', 'detail'],
      },
    },
    missing: { type: 'array', items: { type: 'string' } },
  },
  required: ['exe', 'kinds', 'machine', 'missing'],
}

const VERDICTS_SCHEMA = {
  type: 'object',
  properties: {
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: { type: 'string' },
          rule: { type: 'string' },
          verdict: { type: 'string', enum: ['pass', 'fail', 'na', 'unverified'] },
          png: { type: 'string' },
          state: { type: 'string' },
          size: { type: 'string' },
          evidence: { type: 'string' },
        },
        required: ['kind', 'rule', 'verdict', 'evidence'],
      },
    },
  },
  required: ['verdicts'],
}

const REFUTE_SCHEMA = {
  type: 'object',
  properties: {
    results: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: { type: 'string' },
          rule: { type: 'string' },
          real: { type: 'boolean' },
          measured: { type: 'string' },
        },
        required: ['kind', 'rule', 'real', 'measured'],
      },
    },
  },
  required: ['results'],
}

const COVERAGE_SCHEMA = {
  type: 'object',
  properties: {
    gaps: { type: 'array', items: { type: 'string' } },
    ungrantedExemptions: { type: 'array', items: { type: 'string' } },
  },
  required: ['gaps', 'ungrantedExemptions'],
}

phase('Render')
const rendered = await agent(
  `Prepare the widget audit of the Themia checkout ${A.repo}. Read its CLAUDE.md render section and ${RULES}.
1. Debug executable: ${A.exe ? `use ${A.exe}` : `build Themia.App Debug into ${A.out}/build`}.
2. Kinds: every folder src/Widgets/Themia.Widget.* (lower-camel kind id)${A.kinds ? `, only these: ${A.kinds.join(', ')}` : ''}.
3. Run \`python scripts/gap-check.py --exe <exe> --out ${A.out}/gap${A.kinds ? ` --kinds ${A.kinds.join(',')}` : ''}\` and, if it exists, scripts/item-check.py the same way (item boxes from the render's layout dump: edge to edge, no gaps or overlaps, whole grid cells, nothing above the first item). Every finding either script prints goes into "machine". A check script that does not exist, or a render option the rules need that does not exist (a layout dump; a hover/pressed render for WR-12/WR-13), goes into "missing" with what is missing.
4. For each kind, list the PNGs the gap check wrote (every state, the range and every desktop size). Also render, per kind, its sample state at 10, 13 and 20 grid cells tall (the cell is 10 DIP at the render's scale) with --widget-png into ${A.out}/cells, and, where the render offers a hover or pressed state, the same sample under the pointer into ${A.out}/hover. Return absolute paths.
Do not judge anything yourself. ${HYGIENE}`,
  { phase: 'Render', schema: RENDER_SCHEMA, label: 'render + machine checks' },
)
if (!rendered) throw new Error('render stage died')
log(`${rendered.kinds.length} kinds rendered; ${rendered.machine.length} machine findings; ${rendered.missing.length} missing checks/renders`)

// Batches of kinds, so each reviewer opens every picture of its kinds.
const BATCH = 10
const batches = []
for (let i = 0; i < rendered.kinds.length; i += BATCH) batches.push(rendered.kinds.slice(i, i + BATCH))

const reviewed = await pipeline(
  batches,
  (batch, _b, i) => agent(
    `You audit Themia widgets against the owner's rules in ${RULES} (read it first, every rule WR-1..WR-13). Be the owner looking for what is wrong: assume every widget is broken until its pictures prove otherwise. Your own sense of "close enough" does not count; the rule's words do.
Kinds and their pictures (open EVERY png with the Read tool; they are drawn at 1.5 px per DIP; the grid cell is 10 DIP):
${batch.map(k => `- ${k.kind} (states: ${k.states.join(', ') || 'default'})\n  ${k.pngs.concat(k.hoverPngs).join('\n  ')}`).join('\n')}
Machine findings already known for these kinds (confirm them in the picture, and look for what they missed):
${rendered.machine.filter(m => batch.some(k => k.kind === m.kind)).map(m => `- ${m.kind} ${m.state || ''} ${m.size || ''} ${m.check}: ${m.detail}`).join('\n') || '- none'}
For EVERY kind give a verdict for EVERY rule WR-1..WR-13 (13 verdicts per kind, no rule skipped): pass, fail, na (the rule cannot apply to this kind, say why), or unverified (no picture shows it, e.g. no hover render). A fail names the png, state, size and what you see, measured: where it matters, measure with python + PIL (card edge = alpha, item band/ink edges) instead of eyeballing, and give the DIP numbers. Typical misses to look for: items standing inside a padding band instead of reaching the card's left/right edges (WR-3); a gap over the first item (WR-1); gaps between rows (WR-2); rows of unequal or non-whole-cell height at 10/13/20 cells (WR-6); a status line in place of the face when there is no data (WR-10); a reading and caption drifting apart (WR-7).`,
    { phase: 'Review', schema: VERDICTS_SCHEMA, label: `review ${i + 1}/${batches.length}: ${batch.map(k => k.kind).join(', ')}` },
  ),
  (review, batch, i) => {
    if (!review) return null
    const fails = review.verdicts.filter(v => v.verdict === 'fail')
    if (!fails.length) return { verdicts: review.verdicts, refuted: [] }
    return agent(
      `A reviewer says these Themia widgets break the owner's rules (${RULES}). Try to REFUTE each: open the png, measure it with python + PIL (card edges by alpha, item bands and ink by colour), and decide whether the rule's words are really broken. Default to real=true when the picture is ambiguous: the owner judges by these pictures.
${fails.map(f => `- ${f.kind} ${f.rule} (${f.state || ''} ${f.size || ''}) ${f.png || ''}: ${f.evidence}`).join('\n')}
Return one result per item, with what you measured.`,
      { phase: 'Refute', schema: REFUTE_SCHEMA, label: `refute ${i + 1}/${batches.length}` },
    ).then(r => ({ verdicts: review.verdicts, refuted: r ? r.results.filter(x => !x.real) : [] }))
  },
)

const verdicts = []
reviewed.forEach((r, i) => {
  if (!r) { log(`review batch ${i + 1} died: ${batches[i].map(k => k.kind).join(', ')} not judged`); return }
  const refuted = new Set(r.refuted.map(x => `${x.kind}|${x.rule}`))
  r.verdicts.forEach(v => verdicts.push(v.verdict === 'fail' && refuted.has(`${v.kind}|${v.rule}`) ? { ...v, verdict: 'pass', evidence: `refuted: ${v.evidence}` } : v))
})

phase('Coverage')
const ruleIds = Array.from({ length: 13 }, (_, i) => `WR-${i + 1}`)
const judged = new Set(verdicts.map(v => `${v.kind}|${v.rule}`))
const holes = []
rendered.kinds.forEach(k => ruleIds.forEach(r => { if (!judged.has(`${k.kind}|${r}`)) holes.push(`${k.kind} ${r}: no verdict`) }))
const coverage = await agent(
  `Completeness critic for the Themia widget audit of ${A.repo}. Rules: ${RULES}.
1. Read scripts/gap-check.py (and scripts/item-check.py if present) and every exemption, allowance or tolerance they encode (EXCEPT, CENTRED, LISTS, STRETCH, RESERVED, DRAWN, insets, slack, side allowances, …), and the SPEC entries they cite. List every one the owner did not grant in words in ${RULES} ("Exemptions granted by the owner" or a rule's own quote) as ungrantedExemptions, saying which rule it weakens. An allowance that keeps items inside a padding band is one.
2. Gaps in coverage: kinds in src/Widgets with no renders, states a kind offers that were not rendered, rules marked unverified (${verdicts.filter(v => v.verdict === 'unverified').map(v => `${v.kind} ${v.rule}`).join('; ') || 'none'}), and these holes: ${holes.join('; ') || 'none'}. Missing checks reported by the render stage: ${rendered.missing.join('; ') || 'none'}.`,
  { phase: 'Coverage', schema: COVERAGE_SCHEMA, label: 'completeness critic' },
)

const failures = verdicts.filter(v => v.verdict === 'fail')
const unverified = verdicts.filter(v => v.verdict === 'unverified')
const pass = failures.length === 0 && unverified.length === 0 && rendered.machine.length === 0 && rendered.missing.length === 0
  && holes.length === 0 && (!coverage || (coverage.gaps.length === 0 && coverage.ungrantedExemptions.length === 0))
return {
  pass,
  kinds: rendered.kinds.length,
  failures,
  unverified,
  machine: rendered.machine,
  missing: rendered.missing,
  holes,
  coverage,
  byKind: Object.fromEntries(rendered.kinds.map(k => [k.kind, verdicts.filter(v => v.kind === k.kind && v.verdict === 'fail').map(v => v.rule)])),
}
