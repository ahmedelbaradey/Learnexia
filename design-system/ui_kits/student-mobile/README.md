# UI Kit — Learnexia Student Mobile

A hi-fi click-through recreation of the Learnexia student app on iPhone (402×874).

## What's here

- `index.html` — interactive demo. Use the pills at the top to jump screens or tap **Continue** on Home to walk Lesson → Quiz → Reward.
- `Components.jsx` — small reusable primitives: `HudBar`, `Pill`, `XPBar`, `PrimaryButton`, `LessonCard`, `MissionRow`, `AnswerButton`, `TabBar`, `MascotAvatar`, `TutorBubble`.
- `Screens.jsx` — `HomeScreen`, `SkillTreeScreen`, `LessonScreen`, `QuizScreen`, `RewardScreen`.
- `ios-frame.jsx` — iOS device chrome (status bar, dynamic island, home indicator).

## Screens

1. **Home** — HUD (streak / hearts / XP / gems), greeting, hero Continue card, daily quests, league preview.
2. **Skill Tree** — Math · Numbers unit, six skill nodes (complete / active / locked) zig-zagging down the screen.
3. **Lesson** — title, AI tutor bubble with reply chips, visual example using base-10 blocks, *Start Quiz* CTA.
4. **Quiz** — hearts + progress bar, multiple choice (4 answers), reveal state (correct/wrong) with feedback strip.
5. **Reward** — confetti + trophy pop-in, XP / streak / badge stats, *Keep Going* button.

## Caveats

- Pure cosmetic recreation — no real backend, AI, or animations beyond CSS keyframes.
- Mascot is a placeholder owl SVG. Replace with the real Learnexia character.
- Iconography is system emoji. A real icon font / SVG set should replace these.

