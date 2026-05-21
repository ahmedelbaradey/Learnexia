# Running Multiple Pipelines in Parallel

> How to run more than one story pipeline at once **without** corrupting a shared working tree — while respecting the phase/story dependency order. Companion to [CLAUDE.md](../../CLAUDE.md) (the agent workflow) and the `committer` agent (branch-per-story).

## Golden rules
1. **Dependency order wins.** Never start a story before its prerequisite is **merged**. Parallelize only *independent* siblings.
2. **One working tree = one pipeline.** Two pipelines must never edit the same checkout. Cross-story parallelism uses **git worktrees** (separate checkout per branch).
3. **Serialize shared-file edits.** `Host/Program.cs`, `Learnexia.Modular.sln`, `Claims.GenerateModules()`, and `Directory.Packages.props` are touched by every new module — concurrent edits conflict even across worktrees. One story owns a shared-file change per cycle, or apply it on `main` between merges.
4. **Each worktree runs the full pipeline** (analyzer → planner → … → reviewer → committer) and commits on its own `feat/<StoryID>-<slug>` branch. Integrate to `main` in dependency order.

## Three modes (increasing isolation)

### Mode A — parallel batches within one story (default, free)
The planner already marks independent batches (e.g. `db-migration` ∥ `frontend`, or backend ∥ frontend tracks). The lead dispatches them in one message. No worktrees needed. **Use this always.**

### Mode B — parallel independent stories via worktrees (the real fan-out)
One isolated checkout + branch per story, off the latest `main`:
```bash
# from the main repo root
git worktree add -b feat/P4-02-xp-and-levels ../Learnexia.worktrees/P4-02 main
git worktree add -b feat/P4-05-badges        ../Learnexia.worktrees/P4-05 main
# run a pipeline (this session or a new one) inside each worktree dir, on its branch
```
Integrate in **dependency order** when each is reviewer-approved + committed:
```bash
git switch main
git merge --no-ff feat/P4-02-xp-and-levels
git merge --no-ff feat/P4-05-badges          # resolve shared-file conflicts here
git worktree remove ../Learnexia.worktrees/P4-02
```
- Put worktrees **outside** the main tree (sibling `../Learnexia.worktrees/`) so they're not inside the working copy.
- After merging anything that touches a shared file, **re-run build + reviewer on `main`**.

### Mode C — parallel sessions
Multiple Claude Code windows, each opened on a Mode-B worktree. Max throughput, max coordination cost. Same rules as B.

### Single-agent isolation (built in)
For one isolated agent run, the Agent tool's `isolation: "worktree"` gives that agent its own temporary worktree (auto-cleaned if unchanged). Useful for an experimental spike; not a substitute for Mode B when running a whole pipeline.

## What can run in parallel — decision check
Before fanning out, for each candidate pair ask:
- [ ] Neither depends on the other (check the story "blocked by" notes + the plan).
- [ ] They edit **disjoint** module folders.
- [ ] At most one of them edits a **shared file** (Program.cs / .sln / Claims / Directory.Packages.props) this cycle.
- [ ] Each is its own `feat/<StoryID>` branch + worktree.

If any box is unchecked → **serialize them.**

## Current critical path
**P4-01 (domain-events backbone) is a serializer.** P4-02…P4-08 all consume the event backbone + `UnitOfWorkBehavior` it introduces, so **nothing in Phase 4 parallelizes with P4-01** — it must land and merge first. After P4-01 merges, independent siblings (e.g. P4-02 XP ∥ P4-05 badges) can run in parallel worktrees. Phase 1/2 independent stories can likewise parallelize among themselves once their shared foundation (P1-06 DB, P1-08 design-system, the monorepo skeleton) is in.

## Integration hygiene
- Merge order = dependency order; rebase a worktree branch on the latest `main` before merging if `main` moved.
- Keep shared-file changes small and in their own commit so cross-branch merges are easy.
- The `reviewer` gates each branch; a final build + (for endpoint stories) `api-tester` pass on `main` after the merge wave.
