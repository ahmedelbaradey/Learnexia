// Learnexia — Parent Mobile · Energy · Activity · Settings · Add Child sheet

const pxFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ───────────────────────────────────────────── HELPER ENERGY (parent view)
function PMEnergyScreen({ onAvatar }) {
  const usage = [
    { icon: '💡', n: 38, label: 'Hints', color: '#2DD4BF' },
    { icon: '🔍', n: 12, label: 'Explain', color: '#C4B5FD' },
    { icon: '📖', n: 6,  label: 'Deep', color: '#A5B4FC' },
    { icon: '🎯', n: 4,  label: 'Practice', color: '#FDBA74' },
  ];
  return (
    <PMScreen padTop={14}>
      <PMTopBar sub="Sami · Grade 3" title="Helper Energy" avatar="A" onAvatar={onAvatar}/>
      {/* balance battery */}
      <div style={{ background: 'linear-gradient(135deg,rgba(20,184,166,0.18),rgba(15,23,42,0.4))', border: '1px solid rgba(45,212,191,0.35)', borderRadius: 20, padding: 18, display: 'flex', flexDirection: 'column', gap: 14, marginBottom: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}><span style={{ fontSize: 20, filter: 'drop-shadow(0 0 8px rgba(45,212,191,0.6))' }}>⚡</span><b style={{ fontSize: 15, color: '#F8FAFC' }}>Energy left this month</b></div>
          <div style={{ fontWeight: 900, fontSize: 24, color: '#2DD4BF', fontVariantNumeric: 'tabular-nums' }}>180<span style={{ fontSize: 14, color: '#64748B' }}> / 300</span></div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <div style={{ flex: 1, height: 24, background: '#0F172A', border: '2px solid #14B8A6', borderRadius: 8, padding: 3, overflow: 'hidden' }}><div style={{ height: '100%', width: '60%', background: 'linear-gradient(90deg,#2DD4BF,#14B8A6)', borderRadius: 4 }}/></div>
          <div style={{ width: 5, height: 12, background: '#14B8A6', borderRadius: '0 3px 3px 0' }}/>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 12, color: '#94A3B8' }}>📅 Resets in <b style={{ color: '#CBD5E1' }}>12 days</b> · 20/day cap</span>
        </div>
      </div>
      {/* hearts vs energy reassurance */}
      <PMCard style={{ marginBottom: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div style={{ fontSize: 11, fontWeight: 800, color: '#94A3B8', letterSpacing: '0.06em' }}>TWO SEPARATE METERS</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><span style={{ fontSize: 22 }}>❤️</span><div style={{ fontSize: 13 }}><b style={{ color: '#FB7185' }}>Hearts</b> <span style={{ color: '#94A3B8' }}>= lives in practice</span></div></div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}><span style={{ fontSize: 20 }}>⚡</span><div style={{ fontSize: 13 }}><b style={{ color: '#2DD4BF' }}>Energy</b> <span style={{ color: '#94A3B8' }}>= AI-helper fuel (this page)</span></div></div>
      </PMCard>
      {/* weekly usage */}
      <PMCard style={{ marginBottom: 16 }}>
        <div style={{ fontWeight: 800, fontSize: 14, color: '#F8FAFC', marginBottom: 12 }}>Helpers used this week</div>
        <div style={{ display: 'flex', gap: 8 }}>
          {usage.map(u => (
            <div key={u.label} style={{ flex: 1, padding: 12, background: '#0F172A', borderRadius: 13, textAlign: 'center' }}>
              <div style={{ width: 34, height: 34, borderRadius: 10, background: `${u.color}22`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16, margin: '0 auto 6px' }}>{u.icon}</div>
              <div style={{ fontWeight: 900, fontSize: 18, color: '#F8FAFC' }}>{u.n}</div>
              <div style={{ fontSize: 9, color: '#94A3B8', fontWeight: 700 }}>{u.label}</div>
            </div>
          ))}
        </div>
      </PMCard>
      {/* top-up */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: 14, background: 'linear-gradient(135deg,rgba(45,212,191,0.16),#0F172A)', border: '1px solid rgba(45,212,191,0.3)', borderRadius: 16, marginBottom: 12 }}>
        <span style={{ fontSize: 28 }}>🔋</span>
        <div style={{ flex: 1 }}><div style={{ fontWeight: 900, fontSize: 18, color: '#2DD4BF' }}>+500 ⚡</div><div style={{ fontSize: 11, color: '#94A3B8' }}>Top-up pack · added instantly</div></div>
        <div style={{ fontWeight: 900, fontSize: 18, color: '#F8FAFC' }}>$2.99</div>
      </div>
      <PMButton variant="energy">🔋 Buy 500 credits</PMButton>
      <div style={{ fontSize: 11, color: '#64748B', textAlign: 'center', marginTop: 10 }}>Children never see prices — you manage energy.</div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── ACTIVITY (timeline / notifications)
function PMActivityScreen({ onAvatar }) {
  const items = [
    { t: '2m ago',  icon: '🏆', color: '#FACC15', who: 'Sami', text: 'earned the “Math Whiz” badge' },
    { t: '40m ago', icon: '✅', color: '#22C55E', who: 'Layla', text: 'completed “Letters” lesson · 5/5 correct' },
    { t: '1h ago',  icon: '⚡', color: '#2DD4BF', who: 'Sami', text: 'used 3 AI helpers in a lesson' },
    { t: '3h ago',  icon: '🔥', color: '#FB923C', who: 'Sami', text: 'reached a 7-day streak' },
    { t: 'Yesterday', icon: '⚠️', color: '#EF4444', who: 'Yusuf', text: 'has been inactive for 2 days' },
    { t: 'Yesterday', icon: '⭐', color: '#A855F7', who: 'Layla', text: 'leveled up to Level 4' },
  ];
  return (
    <PMScreen padTop={14}>
      <PMTopBar sub="All children" title="Activity" avatar="A" onAvatar={onAvatar}/>
      {/* filter chips */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16, overflowX: 'auto', paddingBottom: 2 }}>
        {['All', '🏆 Badges', '⚡ Energy', '⚠️ Alerts'].map((f, i) => (
          <div key={f} style={{ flexShrink: 0, padding: '7px 14px', borderRadius: 9999, fontWeight: 700, fontSize: 12, cursor: 'pointer',
            background: i === 0 ? '#4F46E5' : '#1E293B', color: i === 0 ? '#fff' : '#94A3B8', border: '1px solid rgba(255,255,255,0.06)' }}>{f}</div>
        ))}
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {items.map((it, i) => (
          <div key={i} style={{ display: 'flex', gap: 12, padding: 13, background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 16 }}>
            <div style={{ width: 38, height: 38, borderRadius: 12, flexShrink: 0, background: `${it.color}22`, color: it.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 17 }}>{it.icon}</div>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 13.5, color: '#F8FAFC', lineHeight: 1.4 }}><b>{it.who}</b> {it.text}</div>
              <div style={{ fontSize: 11, color: '#64748B', marginTop: 3 }}>{it.t}</div>
            </div>
          </div>
        ))}
      </div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── SETTINGS
function PMSettingsScreen({ onLogout, onAvatar }) {
  const row = (icon, label, right, danger) => (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '14px 14px', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
      <span style={{ fontSize: 17, width: 24, textAlign: 'center' }}>{icon}</span>
      <span style={{ flex: 1, fontSize: 14, fontWeight: 600, color: danger ? '#F87171' : '#F8FAFC' }}>{label}</span>
      {right}
    </div>
  );
  const chev = <span style={{ color: '#64748B', fontSize: 18 }}>›</span>;
  return (
    <PMScreen padTop={14}>
      <PMTopBar sub="Account" title="Settings" avatar="A" onAvatar={onAvatar}/>
      {/* profile card */}
      <PMCard style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 18 }}>
        <div style={{ width: 56, height: 56, borderRadius: '50%', background: 'linear-gradient(135deg,#FB923C,#EF4444)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, fontSize: 24, color: '#fff' }}>A</div>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 800, fontSize: 17, color: '#F8FAFC' }}>Ahmed Hassan</div>
          <div style={{ fontSize: 12, color: '#94A3B8' }}>ahmed@email.com</div>
        </div>
        <button style={{ width: 36, height: 36, borderRadius: 12, background: 'rgba(79,70,229,0.14)', border: '1px solid rgba(99,102,241,0.3)', color: '#A5B4FC', fontSize: 14, cursor: 'pointer' }}>✏️</button>
      </PMCard>
      {/* preferences */}
      <div style={{ fontSize: 11, fontWeight: 800, color: '#64748B', letterSpacing: '0.08em', margin: '0 4px 8px' }}>PREFERENCES</div>
      <div style={{ background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 18, overflow: 'hidden', marginBottom: 18 }}>
        {row('🌍', 'Language', <div style={{ display: 'flex', gap: 6 }}><span style={{ padding: '4px 10px', borderRadius: 9999, background: '#334155', color: '#F8FAFC', fontSize: 12, fontWeight: 700 }}>🇺🇸 EN</span></div>)}
        {row('🌙', 'Theme', <div style={{ display: 'flex', gap: 6 }}><span style={{ padding: '4px 10px', borderRadius: 9999, background: '#334155', color: '#F8FAFC', fontSize: 12, fontWeight: 700 }}>Night</span></div>)}
        {row('🔔', 'Notifications', chev)}
        {row('💳', 'Plan & billing', <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}><span style={{ fontSize: 11, fontWeight: 800, color: '#A5B4FC', background: 'rgba(79,70,229,0.18)', padding: '3px 8px', borderRadius: 9999 }}>Premium</span>{chev}</span>)}
      </div>
      {/* children & safety */}
      <div style={{ fontSize: 11, fontWeight: 800, color: '#64748B', letterSpacing: '0.08em', margin: '0 4px 8px' }}>CHILDREN & SAFETY</div>
      <div style={{ background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 18, overflow: 'hidden', marginBottom: 18 }}>
        {row('👨‍👩‍👦', 'Linked children', <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}><span style={{ fontSize: 13, color: '#94A3B8' }}>3</span>{chev}</span>)}
        {row('🛡️', 'Screen-time limits', chev)}
        {row('🔒', 'Privacy & data', chev)}
      </div>
      <PMButton variant="danger" onClick={onLogout} style={{ height: 48 }}>↪ Log out</PMButton>
      <div style={{ fontSize: 11, color: '#475569', textAlign: 'center', marginTop: 14 }}>Learnexia · v2.4.0</div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── ADD CHILD (bottom sheet)
function PMAddChildSheet({ open, onClose }) {
  const [photo, setPhoto] = React.useState(null);
  const [color, setColor] = React.useState('#A855F7');
  const [name, setName] = React.useState('Layla');
  const [grade, setGrade] = React.useState(1);
  const [lang, setLang] = React.useState('ar');
  const fileRef = React.useRef(null);
  if (!open) return null;
  const onFile = (e) => { const f = e.target.files && e.target.files[0]; if (f) setPhoto(URL.createObjectURL(f)); };
  return (
    <div onClick={onClose} style={{ position: 'absolute', inset: 0, zIndex: 200, background: 'rgba(5,8,22,0.7)', display: 'flex', alignItems: 'flex-end', justifyContent: 'center', ...pxFont }}>
      <div onClick={e => e.stopPropagation()} style={{ width: '100%', background: '#15161D', borderRadius: '28px 28px 0 0', border: '1px solid rgba(255,255,255,0.08)', borderBottom: 'none', boxShadow: '0 -24px 64px rgba(0,0,0,0.55)', padding: '12px 20px 32px', animation: 'lxsheet 320ms cubic-bezier(0.16,1,0.3,1)', maxHeight: '90%', overflowY: 'auto' }}>
        <div style={{ width: 40, height: 5, borderRadius: 9999, background: 'rgba(255,255,255,0.18)', margin: '4px auto 18px' }}/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 20 }}>
          <div style={{ width: 48, height: 48, borderRadius: 14, background: 'linear-gradient(135deg,#A855F7,#6366F1)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24 }}>👶</div>
          <div><div style={{ fontWeight: 800, fontSize: 20, color: '#F8FAFC' }}>Add a child</div><div style={{ fontSize: 13, color: '#94A3B8', marginTop: 2 }}>They'll log in with the email you set</div></div>
        </div>
        <input ref={fileRef} type="file" accept="image/*" onChange={onFile} style={{ display: 'none' }}/>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ width: 64, height: 64, borderRadius: '50%', background: photo ? `url(${photo}) center/cover` : color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, fontSize: 26, color: '#fff', flexShrink: 0, position: 'relative' }}>{!photo && (name.trim()[0] || 'L').toUpperCase()}<div style={{ position: 'absolute', bottom: -2, right: -2, width: 24, height: 24, borderRadius: '50%', background: '#4F46E5', border: '2px solid #15161D', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11 }}>📷</div></div>
            <div onClick={() => fileRef.current && fileRef.current.click()} style={{ flex: 1, border: '1.5px dashed rgba(99,102,241,0.45)', borderRadius: 14, padding: '12px 14px', display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', background: 'rgba(79,70,229,0.05)' }}>
              <span style={{ fontSize: 20 }}>⬆️</span><div><div style={{ fontWeight: 700, fontSize: 13, color: '#A5B4FC' }}>{photo ? 'Change photo' : 'Upload a photo'}</div><div style={{ fontSize: 11, color: '#94A3B8' }}>PNG or JPG · or pick a color</div></div>
            </div>
          </div>
          <PMField label="Child's name"><input value={name} onChange={e => setName(e.target.value)} style={pmInput}/></PMField>
          <PMField label="Login email"><input defaultValue="layla@learnexia.com" style={{ ...pmInput, borderColor: '#4F46E5', boxShadow: '0 0 0 3px rgba(99,102,241,0.2)' }}/></PMField>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>Grade</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6,1fr)', gap: 6 }}>
              {[1,2,3,4,5,6].map(g => {
                const on = grade === g;
                return (
                  <div key={g} onClick={() => setGrade(g)} style={{ height: 46, borderRadius: 12, cursor: 'pointer', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1, background: on ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#0B0C12', border: on ? 'none' : '1px solid rgba(255,255,255,0.1)', boxShadow: on ? '0 4px 12px rgba(99,102,241,0.4)' : 'none' }}>
                    <span style={{ fontSize: 15 }}>{['🌱','🌿','🌳','🌲','🍃','🌴'][g-1]}</span>
                    <span style={{ fontWeight: 800, fontSize: 11, color: on ? '#fff' : '#94A3B8' }}>{g}</span>
                  </div>
                );
              })}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>Language</label>
            <div style={{ display: 'flex', gap: 8 }}>
              {[{ id: 'ar', flag: '🇪🇬', label: 'AR' }, { id: 'en', flag: '🇺🇸', label: 'EN' }].map(l => {
                const on = lang === l.id;
                return (
                  <div key={l.id} onClick={() => setLang(l.id)} style={{ flex: 1, height: 48, borderRadius: 12, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, background: on ? 'rgba(79,70,229,0.18)' : '#0B0C12', border: on ? '1.5px solid #4F46E5' : '1px solid rgba(255,255,255,0.1)' }}>
                    <span style={{ fontSize: 18 }}>{l.flag}</span>
                    <span style={{ fontWeight: 700, fontSize: 14, color: on ? '#F8FAFC' : '#94A3B8' }}>{l.label}</span>
                  </div>
                );
              })}
            </div>
          </div>
          <PMButton onClick={onClose} style={{ marginTop: 4 }}>Add {name.trim() || 'child'} →</PMButton>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, {
  PMEnergyScreen, PMActivityScreen, PMSettingsScreen, PMAddChildSheet,
});
