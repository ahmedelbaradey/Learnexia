# Learnexia Skills — Index

This folder treats the design system as a set of **invokable skills**. Each skill is a self-contained playbook for one common task.

| Skill | When to use |
|---|---|
| **[learnexia-design](../SKILL.md)** | The root skill — handles any Learnexia-branded design ask. Reads `README.md`, the tokens, and the UI kits. |

## Sub-skills (task playbooks)

These are listed inside `SKILL.md` and can be invoked individually:

1. **Build a new mobile screen** — recipe for adding to the student app
2. **Build a new web dashboard page** — recipe for the parent web app
3. **Build a marketing page** — landing/external-facing surface
4. **Make a screen Arabic (RTL)** — full translation + RTL flip checklist
5. **Pick the right atomic component** — naming map for the `preview/` folder
6. **Generate slides or marketing assets** — brand application beyond the product
7. **Add a tweak / feature flag** — for interactive prototypes

## When invoked

The skill reads `README.md` for context, then drills into whichever files match the task. Output is always either:

- **HTML artifact** (a working preview, deck, or mock)
- **Production code** (JSX/CSS that drops into a real codebase)

## Cross-compatibility

This skill is structured to work as an Agent Skill if downloaded into Claude Code. The frontmatter (`name`, `description`, `user-invocable: true`) matches the Agent Skills spec.
