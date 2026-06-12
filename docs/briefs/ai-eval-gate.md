# Cross-Cutting Gate — AI Evaluation Dataset

> **Status: STANDING GATE.** This document defines the standing evaluation dataset that must pass before the AI cost-routing tier split is trusted in production. It gates three decisions: (a) the Haiku/Sonnet tier-mix split in `ai-cost-routing.md §7`, (b) safety/toxicity/age-appropriateness checks (Arabic-first), and (c) Arabic pedagogical explanation and hint quality. The gate is implemented in **P6-02** (AI quality evaluation story). No cost-routing tier change that routes real students to Haiku for `Explain` or `Hint` task kinds may be promoted to production until this eval gate passes.
>
> Cross-referenced from: `docs/briefs/ai-cost-routing.md §7`, `docs/briefs/P3-02.md`, `docs/briefs/P3-04.md`, `docs/briefs/P3-05.md`.

## 1. Why This Gate Exists

The approved cost-routing strategy (`docs/briefs/ai-cost-routing.md`) targets a runtime tier mix of approximately 60% Haiku / 35% Sonnet / 5% Opus to make the 199 EGP/month student plan economically viable. The routing table sets Sonnet (`claude-sonnet-4-6`) as the **floor** for `Explain` and `Hint` task kinds precisely because Arabic pedagogical quality at Haiku has not been measured. Dropping below that floor without evidence risks delivering confusing, incorrect, or culturally inappropriate explanations to children.

The illustrative cost savings of routing 60% of calls to Haiku are significant — but those savings are worthless if the explanations degrade student trust or learning outcomes. This gate is the evidence gate: do not trust the optimized tier mix until the eval dataset validates it.

**The gate is not advisory. It is a prerequisite for changing the `Explain` or `Hint` routing floor from Sonnet to Haiku in any environment that serves real students.**

## 2. Three Gated Decisions

### Gate A — Haiku/Sonnet Tier-Mix for Arabic Pedagogy

The routing table in `ai-cost-routing.md §3` currently floors `Explain` and `Hint` at Sonnet. Gate A asks: is Haiku acceptable for Arabic pedagogical explanation and hint quality at the grade levels and subjects in scope (Math, Science, Arabic, English; grades 1–12)?

- **Pass criterion:** Haiku responses on a curated sample of `Explain` and `Hint` prompts (Arabic + English, all four subjects, at least three grade bands: lower/middle/upper) score at or above the acceptability threshold defined by the content team — on a rubric covering pedagogical correctness, age-appropriateness, language quality (fusha vs ammiya appropriateness), and scaffolding effectiveness.
- **If pass:** the routing table may lower the `Explain`/`Hint` floor to Haiku for the passing subject × grade combinations (with config change, not code change).
- **If fail:** the Sonnet floor is maintained. Re-evaluate after prompt-engineering improvements or model upgrades.

### Gate B — Safety: Toxicity and Age-Appropriateness (Arabic-First)

The P3-02 Safety Layer (FR-AI-4 mandatory) relies on a moderation provider/classifier with Arabic coverage. Arabic toxicity models are weaker than English ones across most providers. Gate B validates that the chosen moderation provider actually catches age-inappropriate and toxic content in Arabic at acceptable recall.

- **Pass criterion:** the P3-02 eval set (AC4 in `docs/briefs/P3-02.md`) runs green for Arabic samples — toxic/age-inappropriate Arabic content is blocked with recall above the threshold set by the content/legal team. English samples also pass.
- **If fail:** the moderation provider or classifier must be replaced or augmented. The AI tutor cannot serve Arabic content to children until this gate passes. This is a production launch blocker.
- **Relation to P3-02:** P3-02-BE-8 (the eval-set harness) seeds the initial sample set. P6-02 expands it. This gate formalizes that P3-02's seed eval set must pass before any content reaches a real student.

### Gate C — Arabic Explanation and Hint Quality (Pedagogical Floor)

Even at Sonnet, the quality of Arabic explanations and hints must be verified against the curriculum and grade level. The content team defines the acceptability rubric; the eval dataset provides measurable evidence.

- **Pass criterion:** on a curated sample of `Explain` and `Hint` prompts at Sonnet (baseline), responses score above the acceptability threshold on: (1) factual correctness relative to the seeded curriculum content, (2) age-appropriate language and complexity, (3) hint scaffolding (level 1 nudges without revealing; level 2 is more specific), (4) no hallucinated curriculum facts.
- **If fail:** the system prompt and prompt builder templates (P3-03) must be revised before production launch.

## 3. Eval Dataset Design

### Composition (minimum viable set for P6-02)

| Dimension | Minimum samples |
|---|---|
| Subjects | Math, Science, Arabic, English (4 subjects) |
| Grade bands | Lower (1–4), Middle (5–8), Upper (9–12) |
| Languages | Arabic (primary), English |
| Task kinds | `Explain` (concept explanation), `Hint` level 1, `Hint` level 2, `Hint` level 3 |
| Safety | Known-toxic samples (ar + en), known-age-inappropriate samples (ar + en), known-safe samples |

### Sample construction

- **Safe + appropriate samples:** curated concept explanations and hints drawn from the seeded curriculum corpus — expected verdict: Pass.
- **Toxic/age-inappropriate samples:** manually crafted prompts that should trigger the safety filter — expected verdict: Block.
- **Haiku vs Sonnet pairs:** the same prompt run against both models; diff scored by a human rater or a trusted Opus judge.
- **Arabic-specific samples:** all `Explain` and `Hint` samples must have Arabic-language versions. The safety samples must include Arabic-language toxic content.

### Storage and CI integration

- The eval dataset is version-controlled alongside the backend (e.g. `tests/EvalDatasets/AiEvalSamples.json` or a similar path).
- The CI pipeline runs the eval harness (P3-02-BE-8 seed harness; P6-02 expands it) and reports pass/fail per gate.
- Eval results are stored in `docs/qc/ai-eval/` per run.

## 4. Gate Flow

```
P3-02 (Safety Layer) ships
   ↓
P3-02-BE-8 eval-set harness seeded with minimal safe/unsafe samples (AR + EN)
   ↓
Gate B (safety/toxicity AR+EN) must pass before any AI content reaches real students
   ↓
P6-02 (AI quality eval story) expands the dataset
   ↓
Gate A (Haiku/Sonnet tier-mix) and Gate C (Arabic explanation quality at Sonnet)
   ↓
Only after Gate A passes → routing table floor may be lowered to Haiku (config change)
Only after Gate C passes → Sonnet floor confirmed as the production baseline
```

**Gate B is a launch blocker.** Gates A and C are cost-optimization gates — the system can run at Sonnet floor indefinitely; they unlock cost savings.

## 5. Relation to Existing Stories

| Story | Relation to this gate |
|---|---|
| **P3-01 (AI Gateway + model routing)** | Implements `AiModelRouter`; routing table floor is set to Sonnet for `Explain`/`Hint` pending this gate |
| **P3-02 (Safety Layer)** | Owns Gate B; P3-02-BE-8 seeds the eval harness |
| **P3-04 (Explain Concept)** | Runtime `Explain` calls use Sonnet floor pending Gate A |
| **P3-05 (Hints + Why-Wrong)** | Runtime `Hint`/`WhyWrong` calls use Sonnet floor pending Gate A |
| **P6-02 (AI Quality Eval)** | Implements and expands all three gates; this document is the spec P6-02 executes against |
| **ai-cost-routing.md** | Gates A+C determine when §7 tier-mix recommendation can move from illustrative to production-deployed |

## 6. Open Questions

| # | Question | Recommendation |
|---|---|---|
| EG-1 | Who owns the acceptability rubric for Gates A and C — content team, product, or AI lead? | Content team defines thresholds; product signs off; AI lead verifies automation. Resolve before P6-02 dispatch. |
| EG-2 | Automated judge (Opus as judge-model) vs human-only scoring for the Haiku vs Sonnet comparison? | Hybrid: Opus-as-judge for initial screening, human spot-check on the Arabic samples. |
| EG-3 | What is the minimum acceptable recall for Gate B (Arabic toxicity)? | Propose ≥95% recall at ≤5% false-positive rate; content/legal team to confirm. |
| EG-4 | How often does the eval dataset refresh (when new curriculum content is added)? | On each major curriculum content update; the CI gate re-runs automatically. |

## Sources

- `docs/briefs/ai-cost-routing.md` — the tier-mix strategy and §7 eval-validation requirement
- `docs/briefs/P3-02.md` — Safety Layer (FR-AI-4 mandatory), P3-02-BE-8 seed eval harness, Arabic quality risk (R2)
- `docs/briefs/P3-04.md` — Sonnet floor for `Explain` task kind
- `docs/briefs/P3-05.md` — Sonnet floor for `Hint` and `WhyWrong` task kinds
