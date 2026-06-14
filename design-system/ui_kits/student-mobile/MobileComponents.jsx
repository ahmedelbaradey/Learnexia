// Learnexia mobile UI primitives — buttons, HUD, cards, etc.
// All styling references CSS variables from /colors_and_type.css.

const lxFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

function HudBar({ streak = 7, hearts = 4, xp = 1240, gems = 42, energy = 180, onEnergy }) {
  return (
    <div style={{
      display: 'flex', gap: 8, alignItems: 'center', padding: '0 16px',
      ...lxFont,
    }}>
      <Pill icon="🔥" value={streak} color="#FB923C" />
      <Pill icon="❤️" value={hearts} color="#FB7185" />
      <Pill icon="⭐" value={xp.toLocaleString()} color="#FACC15" />
      <Pill icon="⚡" value={energy} color="#2DD4BF" onClick={onEnergy} />
    </div>
  );
}

function Pill({ icon, value, color, onClick }) {
  const Tag = onClick ? 'button' : 'div';
  return (
    <Tag onClick={onClick} style={{
      display: 'flex', alignItems: 'center', gap: 6,
      padding: '7px 12px', borderRadius: 9999, border: 'none',
      background: `${color}22`, color, fontWeight: 800, fontSize: 14,
      fontVariantNumeric: 'tabular-nums', fontFamily: 'inherit',
      cursor: onClick ? 'pointer' : 'default',
      boxShadow: onClick ? `0 0 0 1px ${color}55` : 'none',
    }}>
      <span style={{ fontSize: 15 }}>{icon}</span>{value}
    </Tag>
  );
}

function XPBar({ value = 0.65, label }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, ...lxFont }}>
      {label && (
        <div style={{ display: 'flex', justifyContent: 'space-between',
          fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{label}</div>
      )}
      <div style={{
        height: 10, background: '#0F172A', borderRadius: 9999, overflow: 'hidden',
        border: '1px solid rgba(255,255,255,0.06)',
      }}>
        <div style={{
          height: '100%', width: `${value * 100}%`,
          background: 'linear-gradient(90deg,#22C55E,#4F46E5)',
          boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
          transition: 'width 600ms cubic-bezier(0.16,1,0.3,1)',
        }}/>
      </div>
    </div>
  );
}

function PrimaryButton({ children, onClick, variant = 'primary', full = false, style = {} }) {
  const variants = {
    primary:   { bg: '#4F46E5', fg: '#fff',     glow: 'rgba(99,102,241,0.4)' },
    success:   { bg: '#22C55E', fg: '#0F172A',  glow: 'rgba(34,197,94,0.3)' },
    danger:    { bg: '#EF4444', fg: '#fff',     glow: 'rgba(239,68,68,0.35)' },
    secondary: { bg: '#334155', fg: '#F8FAFC',  glow: 'rgba(0,0,0,0.15)' },
    purple:    { bg: '#A855F7', fg: '#fff',     glow: 'rgba(168,85,247,0.4)' },
    ghost:     { bg: 'transparent', fg: '#CBD5E1', glow: 'transparent' },
  };
  const v = variants[variant];
  return (
    <button onClick={onClick} style={{
      height: 52, padding: '0 24px', width: full ? '100%' : undefined,
      borderRadius: 16, border: variant === 'ghost' ? '1px solid rgba(255,255,255,0.16)' : 'none',
      background: v.bg, color: v.fg,
      fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 700, fontSize: 16,
      cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
      boxShadow: variant === 'ghost' ? 'none'
        : `0 4px 12px ${v.glow}, inset 0 1px 0 rgba(255,255,255,0.18)`,
      transition: 'transform 120ms cubic-bezier(0.16,1,0.3,1)',
      ...style,
    }}
    onPointerDown={e => e.currentTarget.style.transform = 'scale(0.95)'}
    onPointerUp={e => e.currentTarget.style.transform = 'scale(1)'}
    onPointerLeave={e => e.currentTarget.style.transform = 'scale(1)'}
    >{children}</button>
  );
}

function LessonCard({ tag, title, meta, progress, state = 'active', onClick }) {
  const stateStyles = {
    active:   { border: '2px solid #4F46E5', shadow: '0 8px 24px rgba(99,102,241,0.25)', opacity: 1 },
    completed:{ border: '1px solid rgba(34,197,94,0.3)', shadow: '0 4px 12px rgba(0,0,0,0.15)', opacity: 1 },
    locked:   { border: '1px solid rgba(255,255,255,0.06)', shadow: 'none', opacity: 0.55 },
  }[state];
  return (
    <div onClick={state === 'locked' ? undefined : onClick} style={{
      background: '#1E293B', borderRadius: 20, padding: 18,
      display: 'flex', flexDirection: 'column', gap: 10,
      cursor: state === 'locked' ? 'not-allowed' : 'pointer',
      position: 'relative',
      ...stateStyles, ...lxFont,
    }}>
      <div style={{
        alignSelf: 'flex-start', padding: '4px 10px', borderRadius: 9999,
        fontWeight: 700, fontSize: 10, letterSpacing: '0.08em', textTransform: 'uppercase',
        color: state === 'completed' ? '#22C55E' : '#A5B4FC',
        background: state === 'completed' ? 'rgba(34,197,94,0.18)' : 'rgba(79,70,229,0.2)',
      }}>{tag}</div>
      <div style={{ fontWeight: 800, fontSize: 18, color: '#F8FAFC', lineHeight: 1.2 }}>{title}</div>
      {meta && <div style={{ fontSize: 12, color: '#94A3B8' }}>{meta}</div>}
      {progress !== undefined && (
        <div style={{
          height: 6, background: '#0F172A', borderRadius: 9999, overflow: 'hidden',
          marginTop: 2,
        }}>
          <div style={{
            height: '100%', width: `${progress * 100}%`,
            background: state === 'completed' ? '#22C55E' : 'linear-gradient(90deg,#4F46E5,#A855F7)',
          }}/>
        </div>
      )}
      {state === 'locked' && (
        <div style={{ position: 'absolute', top: 14, right: 16, fontSize: 18 }}>🔒</div>
      )}
    </div>
  );
}

function MissionRow({ icon, iconBg, title, sub, value, total, reward, done }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14,
      background: '#1E293B', borderRadius: 20, padding: '14px 16px',
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)', ...lxFont,
    }}>
      <div style={{
        width: 44, height: 44, borderRadius: 14,
        background: iconBg, display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 20, flexShrink: 0,
      }}>{done ? '✓' : icon}</div>
      <div style={{ flex: 1 }}>
        <div style={{ fontWeight: 700, fontSize: 14, color: '#F8FAFC' }}>{title}</div>
        <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 2 }}>{sub}</div>
        <div style={{
          height: 5, background: '#0F172A', borderRadius: 9999, marginTop: 8,
          overflow: 'hidden',
        }}>
          <div style={{
            height: '100%', width: `${(value / total) * 100}%`,
            background: done ? '#22C55E' : 'linear-gradient(90deg,#22C55E,#4F46E5)',
          }}/>
        </div>
      </div>
      <div style={{
        background: done ? 'rgba(34,197,94,0.18)' : 'rgba(245,158,11,0.18)',
        color: done ? '#22C55E' : '#F59E0B',
        padding: '6px 10px', borderRadius: 9999,
        fontWeight: 800, fontSize: 13, whiteSpace: 'nowrap',
      }}>⭐ +{reward}</div>
    </div>
  );
}

function AnswerButton({ children, state = 'default', keyLetter, onClick }) {
  const styles = {
    default:  { border: '2px solid rgba(255,255,255,0.08)', bg: '#1E293B',           fg: '#F8FAFC' },
    selected: { border: '2px solid #4F46E5',                bg: 'rgba(79,70,229,0.15)', fg: '#F8FAFC' },
    correct:  { border: '2px solid #22C55E',                bg: 'rgba(34,197,94,0.15)', fg: '#22C55E' },
    wrong:    { border: '2px solid #EF4444',                bg: 'rgba(239,68,68,0.15)', fg: '#EF4444' },
  }[state];
  return (
    <button onClick={onClick} style={{
      ...styles, ...lxFont,
      borderRadius: 16, padding: '14px 16px',
      background: styles.bg, color: styles.fg,
      fontWeight: 600, fontSize: 16,
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      cursor: 'pointer', textAlign: 'left',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
    }}>
      <span>{children}{state === 'correct' && ' ✓'}{state === 'wrong' && ' ✗'}</span>
      <span style={{
        fontFamily: 'ui-monospace, monospace', fontSize: 11, color: '#94A3B8',
        background: 'rgba(255,255,255,0.06)', padding: '2px 7px', borderRadius: 6,
      }}>{keyLetter}</span>
    </button>
  );
}

function TabBar({ active, onChange }) {
  const tabs = [
    { id: 'home',    icon: '🏠', label: 'Home' },
    { id: 'skills',  icon: '🌳', label: 'Skills' },
    { id: 'mission', icon: '🎯', label: 'Quests' },
    { id: 'league',  icon: '🏆', label: 'League' },
    { id: 'profile', icon: '👤', label: 'Me' },
  ];
  return (
    <div style={{
      position: 'absolute', left: 12, right: 12, bottom: 38,
      height: 64, borderRadius: 22,
      background: 'rgba(15,23,42,0.75)',
      backdropFilter: 'blur(20px)', WebkitBackdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.08)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-around',
      boxShadow: '0 8px 32px rgba(0,0,0,0.5)',
      ...lxFont,
    }}>
      {tabs.map(t => (
        <button key={t.id} onClick={() => onChange(t.id)} style={{
          background: 'transparent', border: 'none', cursor: 'pointer',
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2,
          color: active === t.id ? '#A5B4FC' : '#64748B',
          fontWeight: 700, fontSize: 10, padding: '4px 8px',
        }}>
          <div style={{ fontSize: 22, filter: active === t.id ? 'none' : 'grayscale(0.6) opacity(0.7)' }}>{t.icon}</div>
          {t.label}
        </button>
      ))}
    </div>
  );
}

function MascotAvatar({ size = 64 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: '50%',
      background: 'linear-gradient(135deg,#A78BFA,#6366F1)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      boxShadow: '0 8px 20px rgba(99,102,241,0.4)', flexShrink: 0,
    }}>
      <img src={(window.__resources && window.__resources.mascotOwl) || "../../assets/mascot-owl.svg"} style={{ width: size * 0.85, height: size * 0.85 }}/>
    </div>
  );
}

function TutorBubble({ children, chips = [], onChip }) {
  return (
    <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', ...lxFont }}>
      <MascotAvatar size={52}/>
      <div style={{
        background: 'rgba(15,23,42,0.75)',
        backdropFilter: 'blur(20px)',
        WebkitBackdropFilter: 'blur(20px)',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 20, borderBottomLeftRadius: 4,
        padding: '14px 16px', flex: 1,
      }}>
        <div style={{
          fontWeight: 800, fontSize: 10, color: '#A5B4FC',
          textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 4,
        }}>Lexi · AI Tutor</div>
        <div style={{ fontSize: 14, lineHeight: 1.5, color: '#F8FAFC' }}>{children}</div>
        {chips.length > 0 && (
          <div style={{ display: 'flex', gap: 6, marginTop: 10, flexWrap: 'wrap' }}>
            {chips.map((c, i) => (
              <button key={i} onClick={() => onChip && onChip(c)} style={{
                fontSize: 12, fontWeight: 600, color: '#A5B4FC',
                background: 'rgba(79,70,229,0.18)', border: '1px solid rgba(99,102,241,0.3)',
                padding: '5px 10px', borderRadius: 9999, cursor: 'pointer',
                fontFamily: 'inherit',
              }}>{c}</button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function TodaysMission({ reward = 150, tasks, onStart }) {
  const items = tasks || [
    { label: 'Answer 3 questions', done: true },
    { label: 'Complete one quiz', done: false },
    { label: 'Review the Fractions lesson', done: false },
  ];
  return (
    <div style={{
      background: '#15161D', borderRadius: 24, padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      display: 'flex', flexDirection: 'column', gap: 14, ...lxFont,
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 22 }}>🎯</span>
            <span style={{ fontWeight: 900, fontSize: 20, color: '#F8FAFC' }}>Today's Mission</span>
          </div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>Complete all to earn rewards</div>
        </div>
        <div style={{
          padding: '6px 14px', borderRadius: 9999, background: 'rgba(255,255,255,0.04)',
          border: '1px solid rgba(255,255,255,0.08)', fontWeight: 800, fontSize: 14, color: '#FACC15',
          whiteSpace: 'nowrap',
        }}>+{reward} XP</div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {items.map((t, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', borderRadius: 16,
            background: t.done ? 'rgba(34,197,94,0.08)' : '#0B0C12',
            border: t.done ? '1px solid rgba(34,197,94,0.2)' : '1px solid rgba(255,255,255,0.04)',
          }}>
            {t.done ? (
              <div style={{ width: 28, height: 28, borderRadius: '50%', background: '#22C55E', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontWeight: 900 }}>✓</div>
            ) : (
              <div style={{ width: 28, height: 28, borderRadius: '50%', border: '2px solid rgba(255,255,255,0.18)' }}/>
            )}
            <div style={{ flex: 1, fontWeight: 600, fontSize: 15, color: t.done ? '#F8FAFC' : '#CBD5E1' }}>{t.label}</div>
            <div style={{ fontSize: 13, fontWeight: 700, color: t.done ? '#22C55E' : 'rgba(255,255,255,0.22)' }}>{t.done ? 'Done' : '○'}</div>
          </div>
        ))}
      </div>
      <button onClick={onStart} style={{
        height: 52, borderRadius: 16, border: 'none', cursor: 'pointer',
        background: '#22C55E', color: '#06210F', fontFamily: 'inherit', fontWeight: 800, fontSize: 16,
        boxShadow: '0 6px 16px rgba(34,197,94,0.35)',
      }}>▶ Start mission</button>
    </div>
  );
}

Object.assign(window, {
  HudBar, Pill, XPBar, PrimaryButton, LessonCard, MissionRow, TodaysMission,
  AnswerButton, TabBar, MascotAvatar, TutorBubble,
});
