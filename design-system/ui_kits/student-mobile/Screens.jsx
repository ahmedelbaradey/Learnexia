// Learnexia student mobile — individual screens.
// All assume a 402×874 iPhone canvas behind them.

const screenFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

function ScreenShell({ children, scroll = true, padTop = 60, padBottom = 120 }) {
  return (
    <div style={{
      width: '100%', height: '100%',
      paddingTop: padTop, paddingBottom: padBottom,
      overflow: scroll ? 'auto' : 'hidden',
      ...screenFont,
    }}>{children}</div>
  );
}

// ───────────────────────────────────────────── HOME
function HomeScreen({ onContinue, onMission, onEnergy }) {
  return (
    <ScreenShell>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* HUD + greeting */}
        <div>
          <HudBar streak={7} hearts={4} xp={1240} energy={180} onEnergy={onEnergy}/>
        </div>
        <div style={{ padding: '0 16px', display: 'flex', alignItems: 'center', gap: 12 }}>
          <MascotAvatar size={56}/>
          <div>
            <div style={{ fontSize: 12, color: '#94A3B8', fontWeight: 600 }}>Welcome back,</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: '#F8FAFC', lineHeight: 1.1 }}>Sami!</div>
          </div>
        </div>

        {/* Continue Learning — horizontal subject cards */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12, padding: '0 20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontWeight: 900, fontSize: 22, color: '#F8FAFC' }}>Continue Learning</span>
              <span style={{ fontSize: 20 }}>📚</span>
            </div>
            <button style={{
              background: 'transparent', border: 'none', color: '#94A3B8',
              fontWeight: 600, fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
            }}>See all</button>
          </div>
          <div style={{
            display: 'flex', gap: 12, overflowX: 'auto', paddingBottom: 4,
            scrollbarWidth: 'none', WebkitOverflowScrolling: 'touch',
            padding: '0 16px 4px',
          }}>
            <SubjectCard emoji="🧮" subject="Math"    topic="Fractions" pct={60}/>
            <SubjectCard emoji="🧪" subject="Science" topic="Plants"    pct={35}/>
            <SubjectCard emoji="🇬🇧" subject="English" topic="Verbs"     pct={48}/>
            <SubjectCard emoji="📖" subject="Arabic"  topic="Reading"   pct={72}/>
          </div>
        </div>

        {/* Daily mission */}
        <div style={{ padding: '0 16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <div style={{ fontWeight: 800, fontSize: 16, color: '#F8FAFC' }}>Daily Quests</div>
            <button onClick={onMission} style={{
              background: 'transparent', border: 'none', color: '#A5B4FC',
              fontWeight: 700, fontSize: 12, cursor: 'pointer', fontFamily: 'inherit',
            }}>See all →</button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <MissionRow icon="🎯" iconBg="rgba(79,70,229,0.2)" title="Get 10 right in a row" sub="6 of 10" value={6} total={10} reward={50}/>
            <MissionRow icon="🔥" iconBg="rgba(251,146,60,0.2)" title="Practice 3 days in a row" sub="2 of 3" value={2} total={3} reward={30}/>
          </div>
        </div>

        {/* League preview */}
        <div style={{ padding: '0 16px' }}>
          <div style={{
            background: '#1E293B', borderRadius: 20, padding: 16,
            border: '1px solid rgba(255,255,255,0.06)',
            display: 'flex', alignItems: 'center', gap: 14,
          }}>
            <div style={{
              width: 52, height: 52, borderRadius: '50%',
              background: 'radial-gradient(circle at 30% 30%,#FBBF24,#B45309)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 26, boxShadow: '0 6px 16px rgba(180,83,9,0.5)',
            }}>🏆</div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 800, fontSize: 14, color: '#F8FAFC' }}>Bronze League</div>
              <div style={{ fontSize: 12, color: '#94A3B8' }}>Rank #4 · 240 XP to promotion</div>
            </div>
            <div style={{ color: '#94A3B8', fontSize: 20 }}>›</div>
          </div>
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── SKILL TREE
function SkillTreeScreen({ onStart }) {
  const nodes = [
    { state: 'complete', label: 'Numbers',  stars: 3 },
    { state: 'complete', label: 'Counting', stars: 3 },
    { state: 'complete', label: 'Compare',  stars: 2 },
    { state: 'active',   label: 'Addition', stars: 0 },
    { state: 'locked',   label: 'Subtract', stars: 0 },
    { state: 'locked',   label: 'Fractions',stars: 0 },
  ];
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 12px' }}>
        <div style={{ fontWeight: 800, fontSize: 26, color: '#F8FAFC' }}>Math · Numbers</div>
        <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 2 }}>Unit 2 of 8 · Mastery 45%</div>
      </div>
      <div style={{
        padding: '24px 16px', display: 'flex', flexDirection: 'column', gap: 24,
        alignItems: 'center', position: 'relative',
      }}>
        {nodes.map((n, i) => (
          <SkillNode key={i} {...n} index={i} onClick={n.state === 'active' ? onStart : undefined}/>
        ))}
      </div>
    </ScreenShell>
  );
}

function SkillNode({ state, label, stars, index, onClick }) {
  const offset = [-60, -20, 30, 60, 20, -40][index % 6];
  const styles = {
    complete: { bg: 'radial-gradient(circle at 30% 30%,#86EFAC,#22C55E)', shadow: '0 8px 20px rgba(34,197,94,0.45)', icon: '✓' },
    active:   { bg: 'radial-gradient(circle at 30% 30%,#A5B4FC,#4F46E5)', shadow: '0 0 32px rgba(99,102,241,0.7)', icon: '✏️' },
    locked:   { bg: '#334155', shadow: 'inset 0 1px 0 rgba(255,255,255,0.06)', icon: '🔒' },
  }[state];
  return (
    <div onClick={onClick} style={{
      transform: `translateX(${offset}px)`,
      display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
      cursor: state === 'active' ? 'pointer' : 'default',
    }}>
      <div style={{
        width: 80, height: 80, borderRadius: '50%',
        background: styles.bg, boxShadow: styles.shadow,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 32, color: state === 'locked' ? '#64748B' : '#fff',
        animation: state === 'active' ? 'lxpulse 2s ease-in-out infinite' : 'none',
      }}>{styles.icon}</div>
      <div style={{
        fontWeight: 700, fontSize: 13,
        color: state === 'locked' ? '#64748B' : '#F8FAFC',
      }}>{label}</div>
      {state === 'complete' && (
        <div style={{ fontSize: 11, color: '#FACC15' }}>{'⭐'.repeat(stars)}</div>
      )}
    </div>
  );
}

// ───────────────────────────────────────────── LESSON
function LessonScreen({ onAsk, onStart }) {
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div>
          <div style={{
            fontWeight: 700, fontSize: 10, color: '#A5B4FC',
            textTransform: 'uppercase', letterSpacing: '0.1em',
          }}>Math · Numbers · Lesson 3</div>
          <div style={{ fontWeight: 800, fontSize: 26, color: '#F8FAFC', marginTop: 4 }}>Compare Bigger<br/>& Smaller</div>
        </div>

        <TutorBubble chips={['Yes, show me', 'Give a hint', 'Skip']} onChip={onAsk}>
          When we compare two numbers, the one with more <b style={{ color: '#FACC15' }}>tens</b> is bigger. Want me to show you with blocks?
        </TutorBubble>

        {/* Visual example */}
        <div style={{
          background: '#1E293B', borderRadius: 20, padding: 18,
          border: '1px solid rgba(255,255,255,0.06)',
          display: 'flex', flexDirection: 'column', gap: 12,
        }}>
          <div style={{ fontWeight: 700, fontSize: 13, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Example</div>
          <div style={{ display: 'flex', gap: 20, justifyContent: 'center' }}>
            <NumberBlocks tens={2} ones={7} color="#4F46E5"/>
            <div style={{ alignSelf: 'center', fontSize: 28, fontWeight: 800, color: '#FACC15' }}>&lt;</div>
            <NumberBlocks tens={5} ones={4} color="#22C55E"/>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-around', fontWeight: 800, fontSize: 22, color: '#F8FAFC' }}>
            <div>27</div><div>54</div>
          </div>
        </div>

        <PrimaryButton full onClick={onStart}>Start Quiz · 5 questions</PrimaryButton>
      </div>
    </ScreenShell>
  );
}

function NumberBlocks({ tens, ones, color }) {
  return (
    <div style={{ display: 'flex', gap: 6 }}>
      {Array.from({ length: tens }).map((_, i) => (
        <div key={'t' + i} style={{
          width: 10, height: 50, background: color, borderRadius: 3,
          boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
        }}/>
      ))}
      <div style={{ display: 'flex', flexDirection: 'column-reverse', gap: 2, flexWrap: 'wrap', height: 50 }}>
        {Array.from({ length: ones }).map((_, i) => (
          <div key={'o' + i} style={{ width: 8, height: 8, background: color, borderRadius: 2 }}/>
        ))}
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── QUIZ
function QuizScreen({ onComplete }) {
  const [selected, setSelected] = React.useState(null);
  const [revealed, setRevealed] = React.useState(false);
  const answers = [
    { id: 'A', label: '2', correct: false },
    { id: 'B', label: '4', correct: true },
    { id: 'C', label: '6', correct: false },
  ];
  const check = () => setRevealed(true);
  const next = () => onComplete();
  const answeredCorrect = revealed && answers.find(a => a.id === selected)?.correct;
  return (
    <ScreenShell padTop={56}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header: Quiz Time + hearts */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontWeight: 900, fontSize: 22, color: '#F8FAFC' }}>Quiz Time</span>
            <span style={{ fontSize: 20 }}>⚡</span>
          </div>
          <div style={{ display: 'flex', gap: 4 }}>
            {[1, 2, 3, 4, 5].map(i => (
              <span key={i} style={{
                fontSize: 18,
                filter: i <= 5 ? 'drop-shadow(0 0 4px rgba(239,68,68,0.5))' : 'grayscale(1) opacity(0.3)',
              }}>❤️</span>
            ))}
          </div>
        </div>

        {/* Question + progress */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
            <div style={{ fontWeight: 700, fontSize: 15, color: '#CBD5E1' }}>Question 1 / 5</div>
            <div style={{ fontWeight: 700, fontSize: 15, color: '#CBD5E1', fontVariantNumeric: 'tabular-nums' }}>20%</div>
          </div>
          <div style={{ height: 6, background: '#1E2030', borderRadius: 9999, overflow: 'hidden' }}>
            <div style={{ height: '100%', width: '20%', background: 'linear-gradient(90deg,#F59E0B,#EF4444)' }}/>
          </div>
        </div>

        {/* Question card */}
        <div style={{
          background: '#1A1C26', borderRadius: 20, padding: '20px 18px 28px',
          border: '1px solid rgba(255,255,255,0.04)',
          boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 8,
            fontSize: 12, fontWeight: 700, color: '#94A3B8',
            letterSpacing: '0.12em', textTransform: 'uppercase',
            marginBottom: 18,
          }}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#94A3B8" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9.5 2A2.5 2.5 0 0 1 12 4.5v15a2.5 2.5 0 0 1-4.96.44 2.5 2.5 0 0 1-2.96-3.08 3 3 0 0 1-.34-5.58 2.5 2.5 0 0 1 1.32-4.24 2.5 2.5 0 0 1 1.98-3A2.5 2.5 0 0 1 9.5 2Z"/>
              <path d="M14.5 2A2.5 2.5 0 0 0 12 4.5v15a2.5 2.5 0 0 0 4.96.44 2.5 2.5 0 0 0 2.96-3.08 3 3 0 0 0 .34-5.58 2.5 2.5 0 0 0-1.32-4.24 2.5 2.5 0 0 0-1.98-3A2.5 2.5 0 0 0 14.5 2Z"/>
            </svg>
            Math · Fractions
          </div>
          <div style={{
            fontWeight: 900, fontSize: 26, color: '#F8FAFC',
            textAlign: 'center', lineHeight: 1.2,
          }}>What is 1/2 of 8?</div>
        </div>

        {/* Answers — indigo left-border style */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {answers.map(a => {
            let state = 'default';
            if (revealed) {
              if (a.correct) state = 'correct';
              else if (selected === a.id) state = 'wrong';
            } else if (selected === a.id) state = 'selected';
            return <QuizAnswer key={a.id} answer={a} state={state}
              onClick={() => !revealed && setSelected(a.id)} />;
          })}
        </div>

        {/* XP Earned line */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
          fontWeight: 700, fontSize: 16, color: '#F59E0B',
        }}>
          XP Earned: <span style={{ color: '#FACC15', fontWeight: 800 }}>+20</span>
          <span style={{ fontSize: 18, filter: 'drop-shadow(0 0 6px rgba(250,204,21,0.5))' }}>⭐</span>
        </div>

        {/* Submit / Continue */}
        {!revealed
          ? <button onClick={selected ? check : undefined} disabled={!selected} style={{
              height: 56, borderRadius: 16, border: 'none',
              background: selected ? '#4F46E5' : '#2A2D3E',
              color: selected ? '#fff' : '#64748B',
              fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
              cursor: selected ? 'pointer' : 'not-allowed',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
              boxShadow: selected ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
            }}>Submit Answer →</button>
          : <button onClick={next} style={{
              height: 56, borderRadius: 16, border: 'none',
              background: answeredCorrect ? '#22C55E' : '#4F46E5',
              color: '#fff',
              fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
              cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
              boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)',
            }}>Continue →</button>}
      </div>
    </ScreenShell>
  );
}

function QuizAnswer({ answer, state, onClick }) {
  const styles = {
    default:  { border: '#4F46E5',   bg: '#1A1C26', dotBg: '#0F1018', dotFg: '#A5B4FC' },
    selected: { border: '#A855F7',   bg: 'rgba(168,85,247,0.08)', dotBg: '#A855F7', dotFg: '#fff' },
    correct:  { border: '#22C55E',   bg: 'rgba(34,197,94,0.10)',  dotBg: '#22C55E', dotFg: '#0F172A' },
    wrong:    { border: '#EF4444',   bg: 'rgba(239,68,68,0.10)',  dotBg: '#EF4444', dotFg: '#fff' },
  }[state];
  return (
    <button onClick={onClick} style={{
      display: 'flex', alignItems: 'center', gap: 14,
      padding: '16px 18px 16px 14px',
      background: styles.bg,
      borderRadius: 16,
      border: 'none',
      borderLeft: `4px solid ${styles.border}`,
      fontFamily: 'inherit', cursor: 'pointer', textAlign: 'left',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
      animation: state === 'wrong' ? 'lxshake 0.3s' : 'none',
    }}>
      <div style={{
        width: 36, height: 36, borderRadius: '50%',
        background: styles.dotBg, color: styles.dotFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontWeight: 800, fontSize: 15, flexShrink: 0,
      }}>{answer.id}</div>
      <div style={{ flex: 1, fontWeight: 800, fontSize: 20, color: '#F8FAFC' }}>{answer.label}</div>
      {state === 'correct' && <span style={{ color: '#22C55E', fontSize: 22 }}>✓</span>}
      {state === 'wrong' && <span style={{ color: '#EF4444', fontSize: 22 }}>✗</span>}
    </button>
  );
}

// ───────────────────────────────────────────── REWARD
function RewardScreen({ onDone }) {
  return (
    <div style={{
      width: '100%', height: '100%', position: 'relative',
      background: 'radial-gradient(circle at 50% 35%,rgba(79,70,229,0.55),#0F172A 70%)',
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      gap: 18, padding: '60px 24px 80px', ...screenFont,
    }}>
      {/* confetti */}
      {Array.from({ length: 20 }).map((_, i) => (
        <div key={i} style={{
          position: 'absolute',
          top: `${10 + Math.random() * 60}%`,
          left: `${Math.random() * 100}%`,
          width: 8, height: 12,
          background: ['#FACC15','#FB7185','#22C55E','#38BDF8','#A855F7'][i % 5],
          borderRadius: 2,
          transform: `rotate(${Math.random() * 360}deg)`,
          opacity: 0.85,
        }}/>
      ))}
      <div style={{
        width: 120, height: 120, borderRadius: '50%',
        background: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 60, boxShadow: '0 0 60px rgba(250,204,21,0.7), inset 0 -8px 16px rgba(0,0,0,0.2)',
        animation: 'lxpop 700ms cubic-bezier(0.34,1.56,0.64,1)',
      }}>🏆</div>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontWeight: 900, fontSize: 32, color: '#F8FAFC' }}>Lesson Complete!</div>
        <div style={{ fontSize: 14, color: '#CBD5E1', marginTop: 4 }}>Streak protected · 5 of 5 correct</div>
      </div>
      <div style={{
        background: 'rgba(30,41,59,0.85)', backdropFilter: 'blur(20px)',
        border: '1px solid rgba(255,255,255,0.12)',
        borderRadius: 24, padding: '18px 24px',
        display: 'flex', gap: 22, alignItems: 'center',
        boxShadow: '0 16px 36px rgba(0,0,0,0.5)',
      }}>
        <Stat icon="⭐" value="+50" label="XP" color="#FACC15"/>
        <Stat icon="🔥" value="8 days" label="Streak" color="#FB923C"/>
        <Stat icon="🏆" value="+1" label="Badge" color="#FBBF24"/>
      </div>
      <PrimaryButton onClick={onDone} variant="primary" style={{ marginTop: 6, minWidth: 200 }}>Keep Going</PrimaryButton>
    </div>
  );
}

function Stat({ icon, value, label, color }) {
  return (
    <div style={{ textAlign: 'center', minWidth: 70 }}>
      <div style={{ fontSize: 24, marginBottom: 2 }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 18, color, fontVariantNumeric: 'tabular-nums' }}>{value}</div>
      <div style={{ fontSize: 10, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700, marginTop: 2 }}>{label}</div>
    </div>
  );
}

function SubjectCard({ emoji, subject, topic, pct }) {
  return (
    <div style={{
      flex: '0 0 160px', minWidth: 160,
      background: '#1E293B', borderRadius: 20, padding: 14,
      border: '1px solid rgba(99,102,241,0.45)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex', flexDirection: 'column', gap: 8,
      cursor: 'pointer',
    }}>
      <div style={{ fontSize: 32 }}>{emoji}</div>
      <div style={{ fontWeight: 900, fontSize: 19, color: '#F8FAFC', lineHeight: 1 }}>{subject}</div>
      <div style={{ fontSize: 12, color: '#94A3B8' }}>
        {topic} · <span style={{ color: '#CBD5E1', fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{pct}%</span>
      </div>
      <div style={{ height: 4, background: '#0F172A', borderRadius: 9999, overflow: 'hidden', marginTop: 2 }}>
        <div style={{ height: '100%', width: `${pct}%`, background: 'linear-gradient(90deg,#22C55E,#4F46E5)' }}/>
      </div>
    </div>
  );
}

Object.assign(window, {
  HomeScreen, SkillTreeScreen, LessonScreen, QuizScreen, RewardScreen, ScreenShell,
  SubjectCard,
});
