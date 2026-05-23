// Learnexia Parent Dashboard — reusable UI primitives.

const pdFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

function PDSidebar({ active, onChange }) {
  const items = [
    { id: 'children',    label: 'My Children',  icon: '👨‍👩‍👦' },
    { id: 'overview',    label: 'Overview',     icon: '📊' },
    { id: 'reports',     label: 'Reports',      icon: '📈' },
    { id: 'activity',    label: 'Activity',     icon: '⏱️' },
    { id: 'subjects',    label: 'Subjects',     icon: '📚' },
    { id: 'settings',    label: 'Settings',     icon: '⚙️' },
  ];
  return (
    <aside style={{
      width: 240, background: '#0F172A', borderRight: '1px solid rgba(255,255,255,0.06)',
      padding: '24px 16px', display: 'flex', flexDirection: 'column', gap: 24,
      flexShrink: 0, ...pdFont,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '0 8px' }}>
        <img src="../../assets/logo-mark.svg" style={{ width: 36, height: 36 }}/>
        <div style={{ fontWeight: 800, fontSize: 18, color: '#F8FAFC' }}>Learnexia</div>
      </div>

      <div style={{
        background: '#1E293B', borderRadius: 16, padding: 12,
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <div style={{
          width: 36, height: 36, borderRadius: '50%',
          background: 'linear-gradient(135deg,#FB923C,#EF4444)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 16, fontWeight: 800, color: '#fff',
        }}>S</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 700, fontSize: 13, color: '#F8FAFC' }}>Sami</div>
          <div style={{ fontSize: 11, color: '#94A3B8' }}>Grade 3 · Level 12</div>
        </div>
        <div style={{ color: '#94A3B8', fontSize: 16 }}>›</div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {items.map(i => (
          <button key={i.id} onClick={() => onChange(i.id)} style={{
            display: 'flex', alignItems: 'center', gap: 12,
            padding: '10px 12px', borderRadius: 12, border: 'none',
            background: active === i.id ? 'rgba(79,70,229,0.18)' : 'transparent',
            color: active === i.id ? '#A5B4FC' : '#94A3B8',
            fontWeight: active === i.id ? 700 : 500, fontSize: 14,
            cursor: 'pointer', textAlign: 'left', fontFamily: 'inherit',
            transition: 'all 120ms cubic-bezier(0.16,1,0.3,1)',
          }}>
            <span style={{ fontSize: 16 }}>{i.icon}</span>{i.label}
          </button>
        ))}
      </div>

      <div style={{
        marginTop: 'auto', background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)',
        borderRadius: 16, padding: 14,
      }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: '#FACC15', letterSpacing: '0.08em', textTransform: 'uppercase' }}>This week</div>
        <div style={{ fontWeight: 800, fontSize: 20, color: '#F8FAFC', marginTop: 4 }}>+340 XP</div>
        <div style={{ fontSize: 11, color: '#94A3B8' }}>Up 28% from last week</div>
      </div>
    </aside>
  );
}

function PDHeader({ title, sub }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '20px 32px', borderBottom: '1px solid rgba(255,255,255,0.06)',
      ...pdFont,
    }}>
      <div>
        <div style={{ fontWeight: 800, fontSize: 22, color: '#F8FAFC' }}>{title}</div>
        {sub && <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 2 }}>{sub}</div>}
      </div>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
        <select style={{
          background: '#1E293B', color: '#F8FAFC', border: '1px solid rgba(255,255,255,0.1)',
          padding: '8px 12px', borderRadius: 10, fontFamily: 'inherit', fontSize: 13, fontWeight: 600,
        }}>
          <option>This week</option><option>Last week</option><option>This month</option>
        </select>
        <button style={{
          background: '#4F46E5', color: '#fff', border: 'none', padding: '9px 16px',
          borderRadius: 10, fontWeight: 700, fontSize: 13, cursor: 'pointer',
          fontFamily: 'inherit', boxShadow: '0 4px 12px rgba(99,102,241,0.4)',
        }}>Send Report</button>
      </div>
    </div>
  );
}

function PDStatCard({ label, value, delta, accent = '#4F46E5', icon }) {
  const positive = delta && delta.startsWith('+');
  return (
    <div style={{
      background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 20, padding: 18,
      display: 'flex', flexDirection: 'column', gap: 8,
      ...pdFont,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{
          fontSize: 11, fontWeight: 700, color: '#94A3B8',
          textTransform: 'uppercase', letterSpacing: '0.08em',
        }}>{label}</div>
        {icon && (
          <div style={{
            width: 32, height: 32, borderRadius: 10,
            background: `${accent}22`, color: accent,
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15,
          }}>{icon}</div>
        )}
      </div>
      <div style={{ fontWeight: 800, fontSize: 28, color: '#F8FAFC', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
      {delta && (
        <div style={{
          fontSize: 12, fontWeight: 700,
          color: positive ? '#22C55E' : '#EF4444',
        }}>{delta} vs last week</div>
      )}
    </div>
  );
}

function PDActivityChart({ data }) {
  const max = Math.max(...data.map(d => d.xp));
  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', height: 200, ...pdFont }}>
      {data.map((d, i) => (
        <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
          <div style={{
            width: '100%', height: `${(d.xp / max) * 160}px`,
            background: d.today
              ? 'linear-gradient(180deg,#A855F7,#4F46E5)'
              : 'linear-gradient(180deg,#334155,#1E293B)',
            borderRadius: '10px 10px 4px 4px',
            position: 'relative',
            boxShadow: d.today ? '0 6px 18px rgba(99,102,241,0.4)' : 'none',
          }}>
            <div style={{
              position: 'absolute', top: -22, left: '50%', transform: 'translateX(-50%)',
              fontSize: 11, fontWeight: 800, color: d.today ? '#A5B4FC' : '#64748B',
              fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap',
            }}>{d.xp}</div>
          </div>
          <div style={{
            fontSize: 11, fontWeight: 700,
            color: d.today ? '#F8FAFC' : '#94A3B8',
            textTransform: 'uppercase', letterSpacing: '0.06em',
          }}>{d.day}</div>
        </div>
      ))}
    </div>
  );
}

function PDWeakAreas({ items }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, ...pdFont }}>
      {items.map((it, i) => (
        <div key={i} style={{
          display: 'flex', alignItems: 'center', gap: 14,
          padding: '12px 14px', background: '#0F172A',
          borderRadius: 14, border: '1px solid rgba(255,255,255,0.04)',
        }}>
          <div style={{
            width: 38, height: 38, borderRadius: 12,
            background: `${it.color}22`, color: it.color,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 17, flexShrink: 0,
          }}>{it.icon}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontWeight: 700, fontSize: 13, color: '#F8FAFC' }}>{it.topic}</div>
            <div style={{ fontSize: 11, color: '#94A3B8' }}>{it.subject}</div>
          </div>
          <div style={{ width: 120 }}>
            <div style={{
              height: 8, background: '#1E293B', borderRadius: 9999, overflow: 'hidden',
              border: '1px solid rgba(255,255,255,0.04)',
            }}>
              <div style={{
                height: '100%', width: `${it.accuracy}%`,
                background: it.accuracy < 50 ? '#EF4444' : it.accuracy < 70 ? '#F59E0B' : '#22C55E',
              }}/>
            </div>
          </div>
          <div style={{
            fontWeight: 800, fontSize: 13, color: it.color, width: 44, textAlign: 'right',
            fontVariantNumeric: 'tabular-nums',
          }}>{it.accuracy}%</div>
        </div>
      ))}
    </div>
  );
}

function PDPanel({ title, sub, action, children, style = {} }) {
  return (
    <div style={{
      background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 24, padding: 22,
      display: 'flex', flexDirection: 'column', gap: 18,
      ...pdFont, ...style,
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}>
        <div>
          <div style={{ fontWeight: 800, fontSize: 16, color: '#F8FAFC' }}>{title}</div>
          {sub && <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>{sub}</div>}
        </div>
        {action && (
          <button style={{
            background: 'transparent', border: '1px solid rgba(255,255,255,0.12)',
            color: '#A5B4FC', padding: '6px 12px', borderRadius: 9999,
            fontWeight: 700, fontSize: 12, cursor: 'pointer', fontFamily: 'inherit',
          }}>{action}</button>
        )}
      </div>
      {children}
    </div>
  );
}

function PDRecommendation({ icon, title, body, cta, accent = '#4F46E5' }) {
  return (
    <div style={{
      display: 'flex', gap: 14, padding: 14,
      background: '#0F172A', border: '1px solid rgba(255,255,255,0.04)',
      borderRadius: 16, ...pdFont,
    }}>
      <div style={{
        width: 40, height: 40, borderRadius: 12, flexShrink: 0,
        background: `${accent}22`, color: accent,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18,
      }}>{icon}</div>
      <div style={{ flex: 1 }}>
        <div style={{ fontWeight: 700, fontSize: 14, color: '#F8FAFC' }}>{title}</div>
        <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 3, lineHeight: 1.45 }}>{body}</div>
        <button style={{
          marginTop: 10, background: 'transparent', border: 'none',
          color: accent, fontWeight: 700, fontSize: 12,
          cursor: 'pointer', padding: 0, fontFamily: 'inherit',
        }}>{cta} →</button>
      </div>
    </div>
  );
}

Object.assign(window, {
  PDSidebar, PDHeader, PDStatCard, PDActivityChart, PDWeakAreas, PDPanel, PDRecommendation,
});
