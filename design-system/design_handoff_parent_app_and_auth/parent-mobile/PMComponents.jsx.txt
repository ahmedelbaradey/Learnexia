// Learnexia — Parent Mobile app · shared UI primitives.
// Parent-facing companion app: monitor children, read reports, manage Helper Energy.
// Dark navy theme, Poppins. Mirrors the Parent Web dashboard for a 402×874 phone.

const pmFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// Linked children — same family as the web dashboard.
const PM_CHILDREN = [
  { id: 'sami',  name: 'Sami',  initial: 'S', color: '#FB923C', grade: 3, lang: '🇬🇧', level: 12, xp: 1240, streak: 7, mastery: 72, active: true,  weakest: 'Fractions', energy: 180 },
  { id: 'layla', name: 'Layla', initial: 'L', color: '#A855F7', grade: 1, lang: '🇪🇬', level: 4,  xp: 380,  streak: 2, mastery: 45, active: true,  weakest: 'Letters',   energy: 240 },
  { id: 'yusuf', name: 'Yusuf', initial: 'Y', color: '#38BDF8', grade: 5, lang: '🇬🇧', level: 18, xp: 2860, streak: 0, mastery: 81, active: false, weakest: 'Geometry',  energy: 90 },
];

// ───────────────────────────────────────────── screen shell (scrollable body inside phone)
function PMScreen({ children, padTop = 0, bg = '#0F172A', tabBar = true }) {
  return (
    <div style={{ position: 'absolute', inset: 0, background: bg, ...pmFont }}>
      <div style={{
        position: 'absolute', inset: 0, overflowY: 'auto',
        padding: `${padTop}px 16px ${tabBar ? 104 : 28}px`,
      }}>
        {children}
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── top app bar (greeting + avatar)
function PMTopBar({ title, sub, avatar, onAvatar, right }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, paddingTop: 6, marginBottom: 18 }}>
      <div style={{ minWidth: 0 }}>
        {sub && <div style={{ fontSize: 12, color: '#94A3B8', fontWeight: 600 }}>{sub}</div>}
        <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC', lineHeight: 1.1 }}>{title}</div>
      </div>
      {right !== undefined ? right : (
        <button onClick={onAvatar} style={{
          width: 44, height: 44, borderRadius: '50%', flexShrink: 0, border: '2px solid rgba(255,255,255,0.12)',
          background: 'linear-gradient(135deg,#A855F7,#6366F1)', color: '#fff',
          fontFamily: 'inherit', fontWeight: 800, fontSize: 17, cursor: 'pointer',
        }}>{avatar || 'A'}</button>
      )}
    </div>
  );
}

// ───────────────────────────────────────────── bottom tab bar (glass)
function PMTabBar({ active, onChange }) {
  const tabs = [
    { id: 'children', icon: '👨‍👩‍👦', label: 'Children' },
    { id: 'reports',  icon: '📈',       label: 'Reports' },
    { id: 'energy',   icon: '⚡',       label: 'Energy' },
    { id: 'activity', icon: '🔔',       label: 'Activity' },
    { id: 'settings', icon: '⚙️',       label: 'Settings' },
  ];
  return (
    <div style={{
      position: 'absolute', left: 12, right: 12, bottom: 24, height: 66, borderRadius: 22,
      background: 'rgba(15,23,42,0.82)', backdropFilter: 'blur(20px)', WebkitBackdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.08)', boxShadow: '0 8px 32px rgba(0,0,0,0.5)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-around', zIndex: 40, ...pmFont,
    }}>
      {tabs.map(t => {
        const on = active === t.id;
        return (
          <button key={t.id} onClick={() => onChange(t.id)} style={{
            background: 'transparent', border: 'none', cursor: 'pointer',
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3,
            color: on ? '#A5B4FC' : '#64748B', fontWeight: 700, fontSize: 10, padding: '4px 6px',
          }}>
            <span style={{ fontSize: 21, filter: on ? 'none' : 'grayscale(0.6) opacity(0.7)' }}>{t.icon}</span>
            {t.label}
          </button>
        );
      })}
    </div>
  );
}

// ───────────────────────────────────────────── panel / card
function PMCard({ children, style, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 20,
      padding: 16, boxShadow: '0 4px 12px rgba(0,0,0,0.15)', ...style,
    }}>{children}</div>
  );
}

function PMSectionTitle({ children, action, onAction }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', margin: '4px 2px 10px' }}>
      <div style={{ fontWeight: 800, fontSize: 17, color: '#F8FAFC' }}>{children}</div>
      {action && <button onClick={onAction} style={{ background: 'transparent', border: 'none', color: '#A5B4FC', fontWeight: 700, fontSize: 12, cursor: 'pointer', fontFamily: 'inherit' }}>{action}</button>}
    </div>
  );
}

// ───────────────────────────────────────────── KPI stat tile
function PMStat({ icon, value, label, color = '#A5B4FC', delta }) {
  return (
    <div style={{ flex: 1, padding: '12px 10px', background: '#0F172A', borderRadius: 14, border: '1px solid rgba(255,255,255,0.04)' }}>
      <div style={{ width: 30, height: 30, borderRadius: 9, background: `${color}22`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15, marginBottom: 8 }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 19, color: '#F8FAFC', fontVariantNumeric: 'tabular-nums' }}>{value}</div>
      <div style={{ fontSize: 10, fontWeight: 700, color: '#94A3B8' }}>{label}</div>
      {delta && <div style={{ fontSize: 10, color: '#22C55E', marginTop: 2, fontWeight: 700 }}>{delta}</div>}
    </div>
  );
}

// ───────────────────────────────────────────── child row (My Children list)
function PMChildRow({ child, onClick, onEdit }) {
  return (
    <div onClick={onClick} role="button" style={{
      background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 20, padding: 16,
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)', cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 14, ...pmFont,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 52, height: 52, borderRadius: '50%', background: child.color, color: '#fff', flexShrink: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, fontSize: 22,
          boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.18), 0 4px 12px rgba(0,0,0,0.2)',
        }}>{child.initial}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 900, fontSize: 18, color: '#F8FAFC' }}>{child.name}</span>
            <span style={{ padding: '2px 8px', borderRadius: 9999, background: 'rgba(79,70,229,0.18)', color: '#A5B4FC', fontWeight: 800, fontSize: 11 }}>Grade {child.grade}</span>
            <span style={{ fontSize: 13 }}>{child.lang}</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 11, color: '#94A3B8', marginTop: 4, fontWeight: 600 }}>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: child.active ? '#22C55E' : '#64748B', boxShadow: child.active ? '0 0 6px rgba(34,197,94,0.6)' : 'none' }}/>
            {child.active ? 'Active today' : 'Inactive'}
          </div>
        </div>
        <button onClick={(e) => { e.stopPropagation(); onEdit && onEdit(child); }} aria-label="Edit child" style={{
          width: 36, height: 36, borderRadius: 12, flexShrink: 0, background: 'rgba(79,70,229,0.14)',
          border: '1px solid rgba(99,102,241,0.3)', color: '#A5B4FC', fontSize: 15, cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>✏️</button>
      </div>
      <div style={{ display: 'flex', gap: 8 }}>
        <PMMiniStat icon="🧠" value={`Lv ${child.level}`} color="#A855F7"/>
        <PMMiniStat icon="⭐" value={child.xp.toLocaleString()} color="#FACC15"/>
        <PMMiniStat icon="🔥" value={`${child.streak}d`} color="#FB923C"/>
        <PMMiniStat icon="⚡" value={child.energy} color="#2DD4BF"/>
      </div>
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 5 }}>
          <span style={{ fontSize: 10, fontWeight: 700, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Mastery</span>
          <span style={{ fontSize: 11, fontWeight: 800, color: '#F8FAFC' }}>{child.mastery}%</span>
        </div>
        <div style={{ height: 7, background: '#0F172A', borderRadius: 9999, overflow: 'hidden' }}>
          <div style={{ height: '100%', width: `${child.mastery}%`, background: 'linear-gradient(90deg,#22C55E,#4F46E5)' }}/>
        </div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 12, borderTop: '1px solid rgba(255,255,255,0.05)', fontSize: 12, color: '#CBD5E1' }}>
        <span><span style={{ color: '#94A3B8' }}>Weakest:</span> <b>{child.weakest}</b></span>
        <span style={{ color: '#A5B4FC', fontWeight: 800 }}>View progress →</span>
      </div>
    </div>
  );
}

function PMMiniStat({ icon, value, color }) {
  return (
    <div style={{ flex: 1, padding: '8px 6px', background: '#0F172A', borderRadius: 12, textAlign: 'center' }}>
      <div style={{ fontSize: 13 }}>{icon}</div>
      <div style={{ fontWeight: 800, fontSize: 13, color, fontVariantNumeric: 'tabular-nums', marginTop: 2 }}>{value}</div>
    </div>
  );
}

// ───────────────────────────────────────────── weekly bar chart
function PMBarChart({ data, max }) {
  const top = max || Math.max(...data.map(d => d.v));
  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 6, height: 120, paddingTop: 16 }}>
      {data.map((d, i) => {
        const h = Math.max(6, (d.v / top) * 96);
        return (
          <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
            <div style={{ fontSize: 9, fontWeight: 800, color: '#CBD5E1', fontVariantNumeric: 'tabular-nums' }}>{d.v}</div>
            <div style={{ width: '70%', height: h, borderRadius: '6px 6px 3px 3px', background: d.hi ? 'linear-gradient(180deg,#A855F7,#6366F1)' : '#334155' }}/>
            <div style={{ fontSize: 9, fontWeight: 700, color: '#64748B' }}>{d.label}</div>
          </div>
        );
      })}
    </div>
  );
}

// ───────────────────────────────────────────── mastery row (subject + %)
function PMMasteryRow({ subject, pct, color }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#F8FAFC' }}>{subject}</span>
        <span style={{ fontSize: 12, fontWeight: 800, color }}>{pct}%</span>
      </div>
      <div style={{ height: 7, background: '#0F172A', borderRadius: 9999, overflow: 'hidden' }}>
        <div style={{ height: '100%', width: `${pct}%`, background: color }}/>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── weak-area / focus row
function PMFocusRow({ icon, color, title, subject, pct }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '11px 13px', background: '#0F172A', borderRadius: 14 }}>
      <div style={{ width: 36, height: 36, borderRadius: 11, background: `${color}22`, color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16, flexShrink: 0 }}>{icon}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 700, fontSize: 13, color: '#F8FAFC' }}>{title}</div>
        <div style={{ fontSize: 11, color: '#94A3B8' }}>{subject}</div>
      </div>
      <div style={{ width: 80 }}>
        <div style={{ height: 7, background: '#1E293B', borderRadius: 9999, overflow: 'hidden' }}><div style={{ height: '100%', width: `${pct}%`, background: color }}/></div>
      </div>
      <div style={{ fontWeight: 800, fontSize: 13, color, width: 38, textAlign: 'right' }}>{pct}%</div>
    </div>
  );
}

// ───────────────────────────────────────────── primary button
function PMButton({ children, onClick, variant = 'primary', style }) {
  const v = {
    primary: { background: '#4F46E5', color: '#fff', boxShadow: '0 6px 16px rgba(99,102,241,0.4)' },
    energy:  { background: 'linear-gradient(135deg,#2DD4BF,#14B8A6)', color: '#06302B' },
    ghost:   { background: 'transparent', color: '#CBD5E1', border: '1px solid rgba(255,255,255,0.14)' },
    danger:  { background: 'transparent', color: '#F87171', border: '1px solid rgba(239,68,68,0.3)' },
  }[variant];
  return (
    <button onClick={onClick} style={{
      height: 52, borderRadius: 16, border: 'none', cursor: 'pointer', width: '100%',
      fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 800, fontSize: 16, ...v, ...style,
    }}>{children}</button>
  );
}

// ───────────────────────────────────────────── auth field
function PMField({ label, icon, right, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#CBD5E1', display: 'flex', alignItems: 'center', gap: 6 }}>
          {icon && <span style={{ fontSize: 13 }}>{icon}</span>}{label}
        </div>
        {right}
      </div>
      {children}
    </div>
  );
}

const pmInput = {
  height: 52, background: '#15161D', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 14,
  color: '#F8FAFC', fontFamily: 'Poppins, system-ui, sans-serif', fontSize: 15, fontWeight: 500,
  padding: '0 14px', width: '100%', outline: 'none', boxSizing: 'border-box',
};

Object.assign(window, {
  pmFont, PM_CHILDREN, PMScreen, PMTopBar, PMTabBar, PMCard, PMSectionTitle,
  PMStat, PMChildRow, PMMiniStat, PMBarChart, PMMasteryRow, PMFocusRow, PMButton, PMField, pmInput,
});
