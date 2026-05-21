---
name: enhance
description: Rewrite a user-supplied prompt so it's clearer, more specific, and more likely to get a good result from an LLM. Use this skill whenever the user asks to "enhance this prompt", "improve this prompt", "make this prompt better", "rewrite this prompt", "optimize this prompt", "polish this prompt", "fix my prompt", "tighten this prompt", or pastes a prompt and asks something like "how can I make this better" or "what would you change". Also use when the user shares a prompt that worked badly and wants help fixing it. Do NOT use for writing a prompt from scratch when no draft exists — in that case just write it. Do NOT use for tasks that aren't prompts (essays, emails, code), even if the user says "improve this".
---

# Enhance a Prompt

Take a user's draft prompt and rewrite it so an LLM is more likely to produce what they actually want. The goal is a sharper version of *their* prompt — not a different prompt, and not a 10x longer one stuffed with boilerplate.

## Core principles

**Preserve intent.** The user wrote this prompt for a reason. Figure out what they're trying to get back from the model, and make the prompt better at producing *that*. Don't drift the goal because a different goal would be easier to prompt for.

**Match the scale.** A casual one-liner should come back as a tighter one-liner, maybe two. A serious system prompt for a production app gets a serious overhaul. Don't return a 400-word mega-prompt when the user typed 12 words.

**Specificity beats verbosity.** Adding "You are a world-class expert with 30 years of experience" rarely helps. Adding "limit to 5 bullets, each under 20 words" often does. Cut filler, add precision.

**Flag assumptions, don't hide them.** If the prompt is ambiguous and the enhancement requires a guess about the user's intent, say so briefly after the rewrite — don't bury the guess inside the prompt as if it were fact.

## Diagnostic pass

Before rewriting, read the prompt and ask internally:

- **Task** — Is what the model should *do* stated clearly? (Summarize? Generate? Compare? Decide?)
- **Input** — Is it clear what the model is operating on? Where will it come from?
- **Output** — Format, length, structure? Should it be JSON, a list, prose, a table?
- **Audience** — Who's the output for? An expert reader and a beginner need different prompts.
- **Constraints** — Tone, style, what to include, what to avoid, length limits.
- **Context** — Background the model needs that the user assumed but didn't state.
- **Success criteria** — How would the user know a good answer when they saw one?
- **Examples** — Would one or two examples of input → desired output pin this down?
- **Reasoning** — For multi-step or judgment-heavy tasks, should the model think through it before answering?

Most prompts are missing 2–4 of these. Fix the missing ones. Don't fix the ones that are already fine.

## Enhancement levers

Reach for these in roughly this order of bang-for-buck:

1. **Sharpen the task verb.** "Help me with X" → "Write X" / "Summarize X" / "Compare X and Y on Z dimensions."
2. **Specify the output shape.** Format, length, structure. "A 3-sentence answer." "A markdown table with columns Name, Pro, Con." "Return JSON with keys `summary` and `risks`."
3. **Add the missing context.** Domain, audience, prior work, what the user has already tried.
4. **Constrain.** "Avoid jargon." "Use British spelling." "No preamble — start with the answer."
5. **Show, don't just tell.** One or two examples often replace a paragraph of instructions. Use them when the format is unusual or the style is hard to describe.
6. **Decompose multi-step tasks.** If the prompt asks the model to do three things, number them. If reasoning matters, ask it to think step-by-step before producing the final answer.
7. **Role only when it pays.** A role helps when it genuinely narrows the model's behavior ("Act as a copy editor — fix grammar but preserve voice"). Skip it for generic "You are a helpful expert"-style framings.

## What not to do

- Don't pad with ceremonial framing ("As an AI language model, you are tasked with…"). Models don't need it; it just wastes tokens.
- Don't invent specifics the user didn't give. If they didn't say "300 words," don't put "300 words" in the rewrite — ask, or leave length open.
- Don't bury the user's actual question under a wall of meta-instructions.
- Don't strip the user's voice if the prompt is for creative writing. If they wrote "make it snappy and a bit unhinged," keep that energy.
- Don't add fake examples. Bad examples are worse than no examples.
- Don't over-engineer. If the original prompt is already good, say so and make minimal tweaks rather than rewriting for the sake of rewriting.

## When to ask before rewriting

Default to rewriting. But ask 1–3 targeted questions first if:

- The intent is genuinely unclear and any rewrite would be a coin flip on what the user meant.
- The prompt references a file, context, or audience that wasn't shared, and the answer changes meaningfully depending on what those are.
- The user is building something serious (system prompt for an app, evals, a workflow) where guessing wrong wastes their time.

For a casual "make this better," just do it. Don't turn every enhancement into an interrogation.

## Output format

Respond in this order:

1. **The enhanced prompt**, in a fenced code block so the user can copy it cleanly. Nothing else inside the block — no commentary, no "Here's the prompt:" header.
2. **A short "What changed" note** below it — 2–5 bullets max, each one short. Tell the user *why* each change should help, not just *what* changed. Skip this section if the changes were trivial (typo fixes, etc.) — just say "minor polish."
3. **Flagged assumptions**, if any. One line each. E.g., "Assumed you want the output in English — say the word if not."
4. **Optional follow-up question**, only if there's a high-value clarification that would unlock a meaningfully better version.

Keep the whole response tight. The user wants the prompt, not an essay about prompts.

## Examples

### Example 1: Vague creative request

**Input prompt from user:**
> write me a poem about my dog

**Good response:**

```
Write a short poem (8–16 lines) about my dog. Before writing, ask me for:
- the dog's name, breed, and age
- one or two specific moments or quirks I love about them
- the tone I want (tender, funny, bittersweet, silly)

Then write the poem using concrete sensory details from what I tell you — avoid generic "wagging tail / loyal friend" phrases.
```

**What changed:**
- Added a length range so the model doesn't return a haiku or an epic.
- Made the model collect specifics before writing — generic dog poems are the failure mode here.
- Explicitly banned the clichés that this prompt would otherwise produce.

### Example 2: Already-decent technical prompt

**Input prompt from user:**
> Review this Python function and suggest improvements. Focus on readability and performance.

**Good response:**

```
Review the Python function below. Suggest improvements in two sections:

1. Readability — naming, structure, comments, idiomatic style. Cite specific lines.
2. Performance — algorithmic complexity, unnecessary work, memory. Note the rough impact (e.g., "O(n²) → O(n)") where relevant.

If the function looks fine in some respect, say so rather than inventing nitpicks. End with a one-paragraph summary of the most important change.

Function:
[paste code here]
```

**What changed:**
- Split into two sections so the model doesn't blend the two concerns.
- Added "say so rather than inventing nitpicks" — fixes a common failure mode where LLMs invent issues to seem useful.
- Added a slot for the code so the user remembers to paste it.

### Example 3: Bloated prompt that needs trimming

**Input prompt from user:**
> You are an expert world-class senior software architect with 30 years of experience in distributed systems, microservices, cloud computing, and enterprise architecture. Your task is to help me think through and design a system. I would like you to be thorough, detailed, comprehensive, accurate, and helpful. Please help me design a URL shortener.

**Good response:**

```
Design a URL shortener. Cover:
- API surface (endpoints, request/response shapes)
- Storage choice and schema
- ID generation strategy and collision handling
- Read/write scaling assumptions and how the design handles them

Assume ~10M URLs and ~1k requests/sec at peak. Flag any assumption you'd want to confirm before building.
```

**What changed:**
- Dropped the "world-class 30-year-expert" framing — it doesn't change output quality.
- Replaced "be thorough, detailed, comprehensive" (which the model can't act on) with the actual sections you want covered.
- Added concrete scale numbers so the model designs for a real point instead of hedging.

---

That's the skill. Read the prompt, diagnose what's missing, fix only what needs fixing, return a clean enhanced version with a short note on the reasoning.




