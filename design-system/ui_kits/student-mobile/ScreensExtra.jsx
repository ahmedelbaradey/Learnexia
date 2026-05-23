// Learnexia student mobile — additional screens from the wireframes spec.
// Splash, RoleSelect, GradeSelect, SubjectSelect, League, BadgeCollection,
// Hearts, DailyMission, Profile.

const extraFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ───────────────────────────────────────────── SPLASH
function SplashScreen({ onContinue }) {
  // deterministic sparkle positions so they don't jump on re-render
  const sparkles = React.useMemo(() => Array.from({ length: 14 }, (_, i) => ({
    top: (i * 73) % 100, left: (i * 41 + 17) % 100,
    size: (i % 3) + 3,
    opacity: 0.25 + ((i * 7) % 50) / 100,
  })), []);
  return (
    <div onClick={onContinue} style={{
      width: '100%', height: '100%', position: 'relative',
      background: 'radial-gradient(circle at 50% 45%,#4F3FB0 0%,#3B2C8F 40%,#241B6A 100%)',
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      cursor: 'pointer', ...extraFont, overflow: 'hidden',
    }}>
      {sparkles.map((s, i) => (
        <div key={i} style={{
          position: 'absolute',
          top: `${s.top}%`, left: `${s.left}%`,
          width: s.size, height: s.size, borderRadius: '50%',
          background: '#fff', opacity: s.opacity,
          boxShadow: `0 0 ${s.size * 2}px rgba(255,255,255,${s.opacity})`,
        }}/>
      ))}
      {/* centered content */}
      <div style={{
        position: 'absolute', top: '40%', left: '50%',
        transform: 'translate(-50%, -50%)',
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 18,
      }}>
        <div style={{
          width: 132, height: 132, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(250,204,21,0.35) 0%, rgba(168,85,247,0) 65%)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          animation: 'lxpop 800ms cubic-bezier(0.34,1.56,0.64,1)',
        }}>
          <div style={{
            fontSize: 88, filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.6))',
            animation: 'lxpulse 2.4s ease-in-out infinite',
          }}>🌟</div>
        </div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontWeight: 900, fontSize: 36, color: '#F8FAFC', letterSpacing: '-0.02em' }}>
            Learnexia
          </div>
          <div style={{ fontSize: 14, color: 'rgba(255,255,255,0.7)', marginTop: 8, fontWeight: 500 }}>
            AI Learning Adventure Begins
          </div>
        </div>
      </div>

      {/* loading bar at ~70% */}
      <div style={{
        position: 'absolute', top: '70%', left: 0, right: 0,
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14,
      }}>
        <div style={{ display: 'flex', gap: 6 }}>
          {[0, 1, 2].map(i => (
            <div key={i} style={{
              width: 8, height: 8, borderRadius: '50%',
              background: i === 0 ? '#A855F7' : i === 1 ? '#6366F1' : 'rgba(255,255,255,0.3)',
              animation: `lxdot 1.4s ease-in-out ${i * 0.2}s infinite`,
            }}/>
          ))}
        </div>
        <div style={{
          width: 220, height: 6, borderRadius: 9999,
          background: 'rgba(0,0,0,0.35)', overflow: 'hidden',
        }}>
          <div style={{
            height: '100%', width: '55%',
            background: 'linear-gradient(90deg,#C4B5FD,#818CF8)',
            borderRadius: 9999,
            animation: 'lxload 2.5s ease-in-out infinite',
          }}/>
        </div>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8,
          color: 'rgba(255,255,255,0.7)', fontSize: 15, fontWeight: 500,
        }}>
          Loading… <span style={{ fontSize: 16 }}>⚡</span>
        </div>
      </div>

      {/* footer */}
      <div style={{
        position: 'absolute', bottom: 50, left: 0, right: 0,
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8,
      }}>
        <div style={{
          fontSize: 11, fontWeight: 700, color: 'rgba(255,255,255,0.45)',
          letterSpacing: '0.18em', textTransform: 'uppercase',
        }}>POWERED BY AI</div>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8,
          fontSize: 13, color: 'rgba(255,255,255,0.65)', fontWeight: 500,
        }}>
          <span style={{ color: '#A78BFA' }}>✦</span>
          Gamified Learning
          <span style={{ color: '#A78BFA' }}>✦</span>
        </div>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── ROLE SELECT
function RoleSelectScreen({ onPick }) {
  const roles = [
    { id: 'student', emoji: '🎓', label: 'Student', sub: 'Learn, play, level up' },
    { id: 'teacher', emoji: '👨‍🏫', label: 'Teacher', sub: 'Manage classes' },
    { id: 'parent',  emoji: '👨‍👩‍👦', label: 'Parent', sub: "Track your child's progress" },
  ];
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 20px 16px', display: 'flex', flexDirection: 'column', gap: 24, ...extraFont }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 40, marginBottom: 4 }}>🎮</div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC' }}>Welcome to Learnexia</div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6 }}>Pick the one that's you</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {roles.map(r => (
            <button key={r.id} onClick={() => onPick(r.id)} style={{
              display: 'flex', alignItems: 'center', gap: 16,
              padding: 18, borderRadius: 20,
              background: '#1E293B', border: '2px solid rgba(255,255,255,0.06)',
              boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
              fontFamily: 'inherit', cursor: 'pointer', textAlign: 'left',
              color: '#F8FAFC', transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
            }}
            onPointerOver={e => { e.currentTarget.style.borderColor = '#4F46E5'; e.currentTarget.style.background = '#243349'; }}
            onPointerOut={e => { e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'; e.currentTarget.style.background = '#1E293B'; }}>
              <div style={{
                width: 56, height: 56, borderRadius: 16,
                background: 'rgba(79,70,229,0.18)',
                display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28,
              }}>{r.emoji}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 800, fontSize: 17 }}>{r.label}</div>
                <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>{r.sub}</div>
              </div>
              <div style={{ color: '#A5B4FC', fontSize: 22 }}>›</div>
            </button>
          ))}
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── GRADE SELECT
function GradeSelectScreen({ onPick }) {
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 20px 16px', display: 'flex', flexDirection: 'column', gap: 22, ...extraFont }}>
        <div>
          <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.1em' }}>Step 1 of 2</div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC', marginTop: 4 }}>What grade are you in?</div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>We'll adjust the lessons for you</div>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2,1fr)', gap: 10 }}>
          {[1, 2, 3, 4, 5, 6].map(g => (
            <button key={g} onClick={() => onPick(g)} style={{
              padding: '20px 16px', borderRadius: 20,
              background: g === 3 ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#1E293B',
              border: g === 3 ? 'none' : '2px solid rgba(255,255,255,0.06)',
              boxShadow: g === 3 ? '0 8px 24px rgba(99,102,241,0.4)' : '0 4px 12px rgba(0,0,0,0.15)',
              color: '#F8FAFC', cursor: 'pointer', fontFamily: 'inherit',
              display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 6,
            }}>
              <div style={{ fontSize: 28 }}>{['🌱','🌿','🌳','🌲','🍃','🌴'][g - 1]}</div>
              <div style={{ fontWeight: 800, fontSize: 18 }}>Grade {g}</div>
              <div style={{ fontSize: 11, color: g === 3 ? 'rgba(255,255,255,0.85)' : '#94A3B8' }}>
                Ages {5 + g}–{6 + g}
              </div>
            </button>
          ))}
        </div>
        <PrimaryButton full onClick={() => onPick(3)}>Start Learning →</PrimaryButton>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── SUBJECT SELECT
function SubjectSelectScreen({ onPick }) {
  const subjects = [
    { id: 'math',    emoji: '🧮', label: 'Math',           color: '#4F46E5', progress: 0.45 },
    { id: 'science', emoji: '🧪', label: 'Science',        color: '#22C55E', progress: 0.28 },
    { id: 'arabic',  emoji: '📖', label: 'Arabic',         color: '#FB923C', progress: 0.62 },
    { id: 'english', emoji: '🇬🇧', label: 'English',        color: '#A855F7', progress: 0.51 },
    { id: 'social',  emoji: '🌍', label: 'Social Studies', color: '#38BDF8', progress: 0.14 },
  ];
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 20px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...extraFont }}>
        <div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC' }}>Choose a Subject</div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>Tap any subject to keep learning</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {subjects.map(s => (
            <button key={s.id} onClick={() => onPick(s.id)} style={{
              padding: 16, borderRadius: 20,
              background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)',
              boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
              display: 'flex', alignItems: 'center', gap: 14,
              cursor: 'pointer', fontFamily: 'inherit', color: '#F8FAFC',
            }}>
              <div style={{
                width: 52, height: 52, borderRadius: 16,
                background: `${s.color}22`, color: s.color,
                display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26,
              }}>{s.emoji}</div>
              <div style={{ flex: 1, textAlign: 'left' }}>
                <div style={{ fontWeight: 800, fontSize: 16 }}>{s.label}</div>
                <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 4, marginBottom: 6 }}>
                  {Math.round(s.progress * 100)}% mastered
                </div>
                <div style={{ height: 6, background: '#0F172A', borderRadius: 9999, overflow: 'hidden' }}>
                  <div style={{ height: '100%', width: `${s.progress * 100}%`, background: s.color }}/>
                </div>
              </div>
              <div style={{ color: '#94A3B8', fontSize: 18 }}>›</div>
            </button>
          ))}
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── LEAGUE
function LeagueScreen() {
  const players = [
    { rank: 1, name: 'Ahmed',  xp: 2450, you: false, avatar: '#FB923C' },
    { rank: 2, name: 'Sara',   xp: 2380, you: false, avatar: '#A855F7' },
    { rank: 3, name: 'Layla',  xp: 2210, you: false, avatar: '#22C55E' },
    { rank: 4, name: 'Yusuf',  xp: 2050, you: false, avatar: '#38BDF8' },
    { rank: 5, name: 'Omar',   xp: 1980, you: false, avatar: '#FB7185' },
    { rank: 6, name: 'Maya',   xp: 1920, you: false, avatar: '#FACC15' },
    { rank: 7, name: 'Sami',   xp: 1240, you: true,  avatar: '#EF4444' },
    { rank: 8, name: 'Hassan', xp: 1180, you: false, avatar: '#A855F7' },
  ];
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...extraFont }}>
        {/* league header */}
        <div style={{
          background: 'linear-gradient(135deg,#FBBF24,#B45309)',
          borderRadius: 24, padding: '20px 18px',
          boxShadow: '0 8px 24px rgba(180,83,9,0.4)',
          display: 'flex', alignItems: 'center', gap: 14,
        }}>
          <div style={{
            width: 56, height: 56, borderRadius: '50%',
            background: 'rgba(255,255,255,0.2)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 32, boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2)',
          }}>🏆</div>
          <div style={{ flex: 1, color: '#fff' }}>
            <div style={{ fontWeight: 900, fontSize: 20 }}>Bronze League</div>
            <div style={{ fontSize: 12, opacity: 0.9 }}>3 days left · Top 10 promote</div>
          </div>
        </div>

        {/* rankings */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {players.map(p => (
            <div key={p.rank} style={{
              display: 'flex', alignItems: 'center', gap: 12,
              padding: '12px 14px', borderRadius: 16,
              background: p.you ? 'rgba(79,70,229,0.18)' : '#1E293B',
              border: p.you ? '2px solid #4F46E5' : '1px solid rgba(255,255,255,0.04)',
              boxShadow: p.you ? '0 8px 24px rgba(99,102,241,0.25)' : 'none',
            }}>
              <div style={{
                width: 28, textAlign: 'center', fontWeight: 800, fontSize: 14,
                color: p.rank <= 3 ? '#FACC15' : '#94A3B8',
                fontVariantNumeric: 'tabular-nums',
              }}>{p.rank}</div>
              <div style={{
                width: 38, height: 38, borderRadius: '50%',
                background: p.avatar, color: '#fff',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontWeight: 800, fontSize: 14,
                boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.15)',
              }}>{p.name[0]}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 700, fontSize: 14, color: p.you ? '#F8FAFC' : '#CBD5E1' }}>
                  {p.name} {p.you && <span style={{ color: '#A5B4FC', fontSize: 11, fontWeight: 600 }}>YOU</span>}
                </div>
              </div>
              <div style={{
                fontWeight: 800, fontSize: 14, color: '#FACC15',
                fontVariantNumeric: 'tabular-nums',
              }}>{p.xp.toLocaleString()} XP</div>
            </div>
          ))}
        </div>

        {/* promotion zone divider */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '0 4px' }}>
          <div style={{ flex: 1, height: 1, background: 'rgba(34,197,94,0.3)' }}/>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#22C55E', textTransform: 'uppercase', letterSpacing: '0.08em' }}>
            ↑ Promotion Zone
          </div>
          <div style={{ flex: 1, height: 1, background: 'rgba(34,197,94,0.3)' }}/>
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── BADGE COLLECTION
function BadgeCollectionScreen() {
  const badges = [
    { tier: 'bronze',    earned: true,  name: 'First Steps',   sub: 'Earned' },
    { tier: 'bronze',    earned: true,  name: 'Quick Reader',  sub: 'Earned' },
    { tier: 'silver',    earned: true,  name: 'Quiz Master',   sub: 'Earned' },
    { tier: 'silver',    earned: true,  name: 'Streak Hero',   sub: '7 days' },
    { tier: 'gold',      earned: true,  name: 'Math Wizard',   sub: 'Earned' },
    { tier: 'gold',      earned: false, name: 'Word Master',   sub: 'Locked' },
    { tier: 'gold',      earned: false, name: 'Science Pro',   sub: 'Locked' },
    { tier: 'legendary', earned: false, name: 'All-Star',      sub: 'Top 1%' },
  ];
  const discFor = (tier, earned) => {
    if (!earned) return { bg: '#334155', color: '#64748B', shadow: 'inset 0 1px 0 rgba(255,255,255,0.06)' };
    return {
      bronze:    { bg: 'radial-gradient(circle at 30% 30%,#FCD9B5,#B45309)', color: '#fff', shadow: '0 8px 20px rgba(180,83,9,0.45)' },
      silver:    { bg: 'radial-gradient(circle at 30% 30%,#F1F5F9,#94A3B8)', color: '#0F172A', shadow: '0 8px 20px rgba(148,163,184,0.35)' },
      gold:      { bg: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)', color: '#0F172A', shadow: '0 8px 20px rgba(245,158,11,0.45)' },
      legendary: { bg: 'radial-gradient(circle at 30% 30%,#FBCFE8,#A855F7 55%,#4F46E5)', color: '#fff', shadow: '0 0 24px rgba(168,85,247,0.6)' },
    }[tier];
  };
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...extraFont }}>
        <div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC' }}>🏆 Badges</div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>5 of 8 earned · Collect them all</div>
        </div>

        {/* stats strip */}
        <div style={{
          display: 'flex', gap: 8,
          background: '#1E293B', borderRadius: 16, padding: 12,
          border: '1px solid rgba(255,255,255,0.06)',
        }}>
          {[
            { label: 'Bronze', val: 2, color: '#B45309' },
            { label: 'Silver', val: 2, color: '#94A3B8' },
            { label: 'Gold',   val: 1, color: '#F59E0B' },
            { label: 'Legend', val: 0, color: '#A855F7' },
          ].map(s => (
            <div key={s.label} style={{ flex: 1, textAlign: 'center' }}>
              <div style={{ fontWeight: 900, fontSize: 20, color: s.color, fontVariantNumeric: 'tabular-nums' }}>{s.val}</div>
              <div style={{ fontSize: 10, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700, marginTop: 2 }}>{s.label}</div>
            </div>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 12 }}>
          {badges.map((b, i) => {
            const d = discFor(b.tier, b.earned);
            const isLegendary = b.tier === 'legendary';
            return (
              <div key={i} style={{
                display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                padding: 14, borderRadius: 20,
                background: isLegendary && b.earned ? 'rgba(168,85,247,0.12)' : '#1E293B',
                border: isLegendary ? '1px solid rgba(168,85,247,0.3)' : '1px solid rgba(255,255,255,0.04)',
              }}>
                <div style={{
                  width: 64, height: 64, borderRadius: '50%',
                  background: d.bg, color: d.color, boxShadow: d.shadow,
                  display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26,
                }}>{b.earned ? (isLegendary ? '👑' : { bronze: '🥉', silver: '🥈', gold: '🥇' }[b.tier]) : '🔒'}</div>
                <div style={{ fontWeight: 700, fontSize: 12, color: b.earned ? '#F8FAFC' : '#64748B', textAlign: 'center', lineHeight: 1.2 }}>{b.name}</div>
                <div style={{ fontSize: 9, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700 }}>{b.sub}</div>
              </div>
            );
          })}
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── HEARTS
function HeartsScreen({ onPractice }) {
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 20px 16px', display: 'flex', flexDirection: 'column', gap: 22, ...extraFont }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontWeight: 900, fontSize: 28, color: '#F8FAFC' }}>Your Hearts</div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6 }}>You lose one for each wrong answer</div>
        </div>
        <div style={{
          background: 'radial-gradient(circle at 50% 50%,rgba(251,113,133,0.18),transparent 70%)',
          padding: '32px 16px', borderRadius: 24,
          display: 'flex', justifyContent: 'center', gap: 14,
        }}>
          {[1, 2, 3, 4, 5].map(i => (
            <div key={i} style={{
              fontSize: 44,
              filter: i <= 3 ? 'drop-shadow(0 0 12px rgba(251,113,133,0.6))' : 'grayscale(1) opacity(0.3)',
              animation: i === 3 ? 'lxpulse 1.5s ease-in-out infinite' : 'none',
            }}>❤️</div>
          ))}
        </div>
        <div style={{
          background: 'rgba(245,158,11,0.15)', border: '1px solid rgba(245,158,11,0.3)',
          borderRadius: 16, padding: 14,
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <div style={{ fontSize: 22 }}>⏰</div>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#F59E0B' }}>Next heart in 23 min</div>
            <div style={{ fontSize: 11, color: '#FBBF24' }}>Or earn some by practicing</div>
          </div>
        </div>
        <PrimaryButton full variant="purple" onClick={onPractice}>Practice Mode (no hearts)</PrimaryButton>
        <button style={{
          background: 'transparent', border: 'none', color: '#A5B4FC',
          fontFamily: 'inherit', fontWeight: 700, fontSize: 13, padding: 8, cursor: 'pointer',
        }}>💎 Refill with 10 gems</button>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── DAILY MISSION (full)
function DailyMissionScreen() {
  const missions = [
    { icon: '✓', iconBg: 'rgba(34,197,94,0.2)', title: 'Complete 1 lesson',        sub: 'Done',     value: 1,  total: 1,  reward: 20, done: true },
    { icon: '🎯', iconBg: 'rgba(79,70,229,0.2)', title: 'Get 10 questions right',   sub: '6 of 10',  value: 6,  total: 10, reward: 50, done: false },
    { icon: '🔥', iconBg: 'rgba(251,146,60,0.2)', title: 'Practice 3 days in a row', sub: '2 of 3',   value: 2,  total: 3,  reward: 30, done: false },
    { icon: '🧠', iconBg: 'rgba(168,85,247,0.2)', title: 'Ask Lexi a question',     sub: 'Not yet',  value: 0,  total: 1,  reward: 25, done: false },
  ];
  const done = missions.filter(m => m.done).length;
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...extraFont }}>
        <div>
          <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.1em' }}>🎯 Today's Mission</div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC', marginTop: 4 }}>{done} of {missions.length} done</div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>Resets at midnight</div>
        </div>

        {/* Big reward hero */}
        <div style={{
          background: 'linear-gradient(135deg,#F59E0B,#EF4444)',
          borderRadius: 24, padding: '18px 20px',
          display: 'flex', alignItems: 'center', gap: 14,
          boxShadow: '0 8px 24px rgba(245,158,11,0.35)',
        }}>
          <div style={{
            width: 60, height: 60, borderRadius: '50%',
            background: 'rgba(255,255,255,0.25)',
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 30,
          }}>🎁</div>
          <div style={{ flex: 1, color: '#fff' }}>
            <div style={{ fontWeight: 900, fontSize: 22, fontVariantNumeric: 'tabular-nums' }}>+125 XP</div>
            <div style={{ fontSize: 12, opacity: 0.9 }}>Plus a Silver badge if you finish all</div>
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {missions.map((m, i) => <MissionRow key={i} {...m}/>)}
        </div>
      </div>
    </ScreenShell>
  );
}

// ───────────────────────────────────────────── PROFILE
function ProfileScreen() {
  return (
    <ScreenShell padTop={70}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...extraFont }}>
        {/* Hero */}
        <div style={{
          background: 'linear-gradient(135deg,#A855F7,#6366F1)',
          borderRadius: 24, padding: 22,
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12,
          boxShadow: '0 8px 24px rgba(99,102,241,0.35)',
        }}>
          <div style={{
            width: 88, height: 88, borderRadius: '50%',
            background: 'linear-gradient(135deg,#FB923C,#EF4444)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 900, fontSize: 36, color: '#fff',
            boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2), 0 0 0 4px rgba(255,255,255,0.2)',
          }}>S</div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontWeight: 900, fontSize: 22, color: '#fff' }}>Sami</div>
            <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.85)', marginTop: 2 }}>Grade 3 · Joined Sep 2025</div>
          </div>
          <div style={{
            background: 'rgba(255,255,255,0.2)', padding: '6px 14px', borderRadius: 9999,
            fontWeight: 800, fontSize: 13, color: '#fff',
          }}>Level 12</div>
        </div>

        {/* Stats grid */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2,1fr)', gap: 10 }}>
          <StatTile icon="⭐" value="1,240" label="Total XP"     color="#FACC15"/>
          <StatTile icon="🔥" value="7"      label="Day streak"  color="#FB923C"/>
          <StatTile icon="🏆" value="5"      label="Badges"      color="#F59E0B"/>
          <StatTile icon="📚" value="34"     label="Lessons"     color="#22C55E"/>
        </div>

        {/* Recent badges */}
        <div style={{
          background: '#1E293B', borderRadius: 20, padding: 18,
          border: '1px solid rgba(255,255,255,0.06)',
          boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: '#F8FAFC' }}>Recent Badges</div>
            <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 700 }}>See all →</div>
          </div>
          <div style={{ display: 'flex', gap: 16, justifyContent: 'space-around' }}>
            {[
              { tier: 'gold',   icon: '🥇', label: 'Math Wizard' },
              { tier: 'silver', icon: '🥈', label: 'Quiz Master' },
              { tier: 'silver', icon: '🥈', label: 'Streak Hero' },
              { tier: 'bronze', icon: '🥉', label: 'First Quiz' },
            ].map((b, i) => {
              const bg = { gold: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)',
                           silver: 'radial-gradient(circle at 30% 30%,#F1F5F9,#94A3B8)',
                           bronze: 'radial-gradient(circle at 30% 30%,#FCD9B5,#B45309)' }[b.tier];
              return (
                <div key={i} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                  <div style={{
                    width: 48, height: 48, borderRadius: '50%',
                    background: bg, fontSize: 20,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2)',
                  }}>{b.icon}</div>
                  <div style={{ fontSize: 10, color: '#94A3B8', textAlign: 'center', fontWeight: 600, lineHeight: 1.2, maxWidth: 60 }}>{b.label}</div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Level progress */}
        <div style={{
          background: '#1E293B', borderRadius: 20, padding: 18,
          border: '1px solid rgba(255,255,255,0.06)',
          boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 10 }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: '#F8FAFC' }}>Level 12 → 13</div>
            <div style={{ fontWeight: 800, fontSize: 13, color: '#FACC15', fontVariantNumeric: 'tabular-nums' }}>820 / 1000 XP</div>
          </div>
          <div style={{ height: 10, background: '#0F172A', borderRadius: 9999, overflow: 'hidden', border: '1px solid rgba(255,255,255,0.06)' }}>
            <div style={{ height: '100%', width: '82%', background: 'linear-gradient(90deg,#22C55E,#4F46E5)' }}/>
          </div>
          <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 8 }}>180 XP to next level</div>
        </div>
      </div>
    </ScreenShell>
  );
}

function StatTile({ icon, value, label, color }) {
  return (
    <div style={{
      background: '#1E293B', borderRadius: 20, padding: 16,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex', alignItems: 'center', gap: 12,
    }}>
      <div style={{
        width: 40, height: 40, borderRadius: 12,
        background: `${color}22`, color,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20,
      }}>{icon}</div>
      <div>
        <div style={{ fontWeight: 900, fontSize: 20, color: '#F8FAFC', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
        <div style={{ fontSize: 10, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 700, marginTop: 3 }}>{label}</div>
      </div>
    </div>
  );
}

Object.assign(window, {
  SplashScreen, RoleSelectScreen, GradeSelectScreen, SubjectSelectScreen,
  LeagueScreen, BadgeCollectionScreen, HeartsScreen, DailyMissionScreen, ProfileScreen,
  MissionCompletedScreen,
});

// ───────────────────────────────────────────── MISSION COMPLETED
function MissionCompletedScreen({ onContinue, onChallenge }) {
  return (
    <div style={{
      width: '100%', height: '100%', position: 'relative',
      background: '#0A0B11',
      display: 'flex', flexDirection: 'column',
      padding: '60px 20px 32px',
      ...extraFont, overflow: 'auto',
    }}>
      {/* Title */}
      <div style={{ textAlign: 'center', marginBottom: 24 }}>
        <div style={{
          fontSize: 84, marginBottom: 12,
          animation: 'lxpop 800ms cubic-bezier(0.34,1.56,0.64,1)',
        }}>🎉</div>
        <div style={{
          fontWeight: 900, fontSize: 36, color: '#F8FAFC',
          letterSpacing: '-0.02em', lineHeight: 1.1,
        }}>Mission Completed!</div>
        <div style={{
          fontSize: 15, color: '#94A3B8',
          marginTop: 10, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
        }}>
          You crushed today's challenge <span style={{ fontSize: 17 }}>💪</span>
        </div>
      </div>

      {/* Big reward card with orange glow */}
      <div style={{
        background: '#15161D', borderRadius: 24, padding: '24px 20px 20px',
        border: '1px solid rgba(245,158,11,0.5)',
        boxShadow: '0 0 0 1px rgba(245,158,11,0.3), 0 0 48px rgba(245,158,11,0.25)',
        marginBottom: 28,
        animation: 'lxglow 2.4s ease-in-out infinite',
      }}>
        <div style={{
          fontSize: 13, fontWeight: 800, color: '#94A3B8',
          textAlign: 'center', letterSpacing: '0.12em', textTransform: 'uppercase',
          marginBottom: 6,
        }}>Total Reward</div>
        <div style={{
          fontWeight: 900, fontSize: 56, textAlign: 'center', lineHeight: 1,
          marginBottom: 22,
          background: 'linear-gradient(90deg,#F59E0B,#EF4444)',
          WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
          backgroundClip: 'text',
          fontVariantNumeric: 'tabular-nums',
        }}>+120 XP</div>

        {/* Badge pill */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 12,
          padding: '14px 16px', borderRadius: 16,
          background: 'rgba(168,85,247,0.08)',
          border: '1px solid rgba(168,85,247,0.3)',
          marginBottom: 10,
        }}>
          <span style={{ fontSize: 22 }}>🏆</span>
          <div style={{ flex: 1, fontWeight: 800, fontSize: 16, color: '#A855F7' }}>
            New Badge: Math Explorer
          </div>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#A855F7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 3-1.9 5.8a2 2 0 0 0 0 1.4L12 13l1.9-5.8a2 2 0 0 0 0-1.4L12 3Z"/>
            <path d="M5 3v4"/><path d="M19 17v4"/><path d="M3 5h4"/><path d="M17 19h4"/>
          </svg>
        </div>

        {/* Streak pill */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 12,
          padding: '14px 16px', borderRadius: 16,
          background: 'rgba(245,158,11,0.08)',
          border: '1px solid rgba(245,158,11,0.3)',
        }}>
          <span style={{ fontSize: 22 }}>🔥</span>
          <div style={{ flex: 1, fontWeight: 800, fontSize: 16, color: '#F59E0B' }}>
            Streak: 8 Days
          </div>
          <span style={{ fontSize: 18 }}>👌</span>
        </div>
      </div>

      {/* Level progress */}
      <div style={{ marginBottom: 32 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#94A3B8" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="22 7 13.5 15.5 8.5 10.5 2 17"/><polyline points="16 7 22 7 22 13"/>
            </svg>
            <span style={{ fontWeight: 700, fontSize: 17, color: '#CBD5E1' }}>Level 5 → 6</span>
          </div>
          <div style={{ fontWeight: 700, fontSize: 17, color: '#CBD5E1', fontVariantNumeric: 'tabular-nums' }}>
            850 / 1000 XP
          </div>
        </div>
        <div style={{ height: 12, background: '#1E2030', borderRadius: 9999, overflow: 'hidden' }}>
          <div style={{
            height: '100%', width: '85%',
            background: 'linear-gradient(90deg,#22C55E,#4F46E5)',
            boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
            borderRadius: 9999,
            transition: 'width 1.2s cubic-bezier(0.16,1,0.3,1)',
          }}/>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 13, color: '#94A3B8' }}>
          <span>85% to next level</span>
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>150 XP left</span>
        </div>
      </div>

      {/* Spacer to push buttons down */}
      <div style={{ flex: 1, minHeight: 20 }}/>

      {/* Buttons */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <button onClick={onContinue} style={{
          height: 60, borderRadius: 18, border: 'none',
          background: '#4F46E5', color: '#fff',
          fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
          boxShadow: '0 6px 20px rgba(99,102,241,0.45), inset 0 1px 0 rgba(255,255,255,0.2)',
        }}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2Z"/>
            <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7Z"/>
          </svg>
          Continue Learning →
        </button>
        <button onClick={onChallenge} style={{
          height: 60, borderRadius: 18,
          background: 'transparent', color: '#A5B4FC',
          border: '1.5px solid #4F46E5',
          fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
        }}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#A5B4FC" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14.5 17.5 3 6V3h3l11.5 11.5"/><path d="M13 19l6-6"/><path d="M16 16l4 4"/>
            <path d="M19 21l2-2"/><path d="M14.5 6.5 21 13"/>
            <path d="M21 3v3l-3.5 3.5"/>
          </svg>
          Play Challenge
        </button>
      </div>
    </div>
  );
}
