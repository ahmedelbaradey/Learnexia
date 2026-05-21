// Learnexia mobile UI primitives — buttons, HUD, cards, etc.
// All styling references CSS variables from /colors_and_type.css.

const lxFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

function HudBar({ streak = 7, hearts = 4, xp = 1240, gems = 42 }) {
  return (
    <div style={{
      display: 'flex', gap: 8, alignItems: 'center', padding: '0 16px',
      ...lxFont,
    }}>
      <Pill icon="🔥" value={streak} color="#FB923C" />
      <Pill icon="❤️" value={hearts} color="#FB7185" />
      <Pill icon="⭐" value={xp.toLocaleString()} color="#FACC15" />
      <Pill icon="💎" value={gems} color="#38BDF8" />
    </div>
  );
}

function Pill({ icon, value, color }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 6,
      padding: '7px 12px', borderRadius: 9999,
      background: `${color}22`, color, fontWeight: 800, fontSize: 14,
      fontVariantNumeric: 'tabular-nums',
    }}>
      <span style={{ fontSize: 15 }}>{icon}</span>{value}
    </div>
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
          background: 'linear-gradient(90deg,#FACC15,#FB923C)',
          boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
          transition: 'width 600ms cubic-bezier(0.16,1,0.3,1)',
        }}/>
      </div>
    </div>
  );
}

function PrimaryButton({ children, onClick, variant = 'primary', full = false, style = {} }) {
  const variants = {
    primary:   { bg: '#4F46E5', fg: '#fff',     glow: 'rgba(99,102,241,0.45)' },
    success:   { bg: '#22C55E', fg: '#0F172A',  glow: 'rgba(34,197,94,0.35)' },
    danger:    { bg: '#EF4444', fg: '#fff',     glow: 'rgba(239,68,68,0.4)' },
    secondary: { bg: '#334155', fg: '#F8FAFC',  glow: 'rgba(0,0,0,0.3)' },
    ghost:     { bg: 'transparent', fg: '#CBD5E1', glow: 'transparent' },
  };
  const v = variants[variant];
  return (
    <button onClick={onClick} style={{
      height: 52, padding: '0 24px', width: full ? '100%' : undefined,
      borderRadius: 9999, border: variant === 'ghost' ? '1px solid rgba(255,255,255,0.16)' : 'none',
      background: v.bg, color: v.fg,
      fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 700, fontSize: 16,
      cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
      boxShadow: variant === 'ghost' ? 'none'
        : `0 6px 18px ${v.glow}, inset 0 1px 0 rgba(255,255,255,0.2)`,
      transition: 'transform 120ms cubic-bezier(0.16,1,0.3,1)',
      ...style,
    }}
    onPointerDown={e => e.currentTarget.style.transform = 'scale(0.96)'}
    onPointerUp={e => e.currentTarget.style.transform = 'scale(1)'}
    onPointerLeave={e => e.currentTarget.style.transform = 'scale(1)'}
    >{children}</button>
  );
}

function LessonCard({ tag, title, meta, progress, state = 'active', onClick }) {
  const stateStyles = {
    active:   { border: '2px solid #4F46E5', shadow: '0 8px 24px rgba(99,102,241,0.3)', opacity: 1 },
    completed:{ border: '1px solid rgba(34,197,94,0.3)', shadow: '0 4px 16px rgba(0,0,0,0.3)', opacity: 1 },
    locked:   { border: '1px solid rgba(255,255,255,0.06)', shadow: 'none', opacity: 0.55 },
  }[state];
  return (
    <div onClick={state === 'locked' ? undefined : onClick} style={{
      background: '#1E293B', borderRadius: 24, padding: 18,
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
      border: '1px solid rgba(255,255,255,0.06)', ...lxFont,
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
            background: done ? '#22C55E' : 'linear-gradient(90deg,#FACC15,#FB923C)',
          }}/>
        </div>
      </div>
      <div style={{
        background: done ? 'rgba(34,197,94,0.18)' : 'rgba(250,204,21,0.15)',
        color: done ? '#22C55E' : '#FACC15',
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
      <img src="../../assets/mascot-owl.svg" style={{ width: size * 0.85, height: size * 0.85 }}/>
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

Object.assign(window, {
  HudBar, Pill, XPBar, PrimaryButton, LessonCard, MissionRow,
  AnswerButton, TabBar, MascotAvatar, TutorBubble,
});
