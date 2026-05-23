// Learnexia Web — in-app pages: My Children, Reports, Settings, Activity, Subjects

const appFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ────────────────────────────────────────────────────────────── MY CHILDREN (web)
function MyChildrenWebPage({ onPick, onAddChild, sidebarActive, onNav }) {
  const children = [
    { id: 1, name: 'Sami',   color: '#FB923C', grade: 3, language: '🇬🇧 English', level: 12, xp: 1240, streak: 7, mastery: 72,  active: true, weakest: 'Fractions' },
    { id: 2, name: 'Layla',  color: '#A855F7', grade: 1, language: '🇸🇦 العربية', level: 4,  xp: 380,  streak: 2, mastery: 45,  active: true, weakest: 'Letters'   },
    { id: 3, name: 'Yusuf',  color: '#38BDF8', grade: 5, language: '🇬🇧 English', level: 18, xp: 2860, streak: 0, mastery: 81,  active: false, weakest: 'Geometry'  },
  ];

  return (
    <AppShell active={sidebarActive} onNav={onNav}>
      <PDHeader title="My Children" sub="3 children linked to your account" />
      <div style={{ flex: 1, overflow: 'auto', padding: 28, display: 'flex', flexDirection: 'column', gap: 20, ...appFont }}>
        {/* Combined hero */}
        <div style={{
          background: 'linear-gradient(135deg,#A855F7 0%,#6366F1 100%)',
          borderRadius: 24, padding: 28,
          display: 'grid', gridTemplateColumns: '1.4fr repeat(4, 1fr)', alignItems: 'center', gap: 20,
          color: '#fff', boxShadow: '0 16px 36px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.18)',
          position: 'relative', overflow: 'hidden',
        }}>
          <div style={{ position: 'absolute', right: -20, top: -20, fontSize: 180, opacity: 0.18, pointerEvents: 'none' }}>👨‍👩‍👦</div>
          <div style={{ position: 'relative' }}>
            <div style={{ fontWeight: 800, fontSize: 12, letterSpacing: '0.12em', textTransform: 'uppercase', opacity: 0.85 }}>This Week · Combined</div>
            <div style={{ fontWeight: 900, fontSize: 28, marginTop: 6, letterSpacing: '-0.02em' }}>Your family is on a roll</div>
            <div style={{ fontSize: 13, marginTop: 6, opacity: 0.85 }}>3 active learners · 18 lessons completed</div>
          </div>
          <HeroStat icon="⭐" value="4,480" label="Total XP"/>
          <HeroStat icon="📚" value="18"    label="Lessons"/>
          <HeroStat icon="🔥" value="9d"    label="Best streak"/>
          <HeroStat icon="🏆" value="5"     label="Badges earned"/>
        </div>

        {/* Toolbar */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontWeight: 800, fontSize: 18, color: '#F8FAFC' }}>Pick a child to view their progress</div>
          <button onClick={onAddChild} style={{ ...btnPrimary(), height: 44, padding: '0 18px' }}>+ Add Child</button>
        </div>

        {/* Cards */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16 }}>
          {children.map(c => <ChildWebCard key={c.id} child={c} onClick={() => onPick(c)}/>)}
          <button onClick={onAddChild} style={{
            background: 'transparent', border: '2px dashed rgba(99,102,241,0.4)',
            borderRadius: 24, padding: 32, minHeight: 260,
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12,
            color: '#A5B4FC', cursor: 'pointer', fontFamily: 'inherit',
            transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
          }}
          onPointerOver={e => { e.currentTarget.style.background = 'rgba(79,70,229,0.06)'; e.currentTarget.style.borderColor = '#4F46E5'; }}
          onPointerOut ={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'rgba(99,102,241,0.4)'; }}>
            <div style={{
              width: 64, height: 64, borderRadius: 20,
              background: 'rgba(79,70,229,0.18)', color: '#A5B4FC',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 32, fontWeight: 800,
            }}>+</div>
            <div style={{ fontWeight: 800, fontSize: 16, color: '#F8FAFC' }}>Add a child</div>
            <div style={{ fontSize: 12, color: '#94A3B8', textAlign: 'center', maxWidth: 200 }}>Set their grade, language, and login email</div>
          </button>
        </div>

        {/* Security strip */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 14,
          padding: '16px 20px', borderRadius: 16,
          background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)',
        }}>
          <div style={{
            width: 40, height: 40, borderRadius: 12,
            background: 'rgba(34,197,94,0.15)', color: '#22C55E',
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20,
          }}>🛡️</div>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#F8FAFC' }}>You're the only parent linked to these accounts</div>
            <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>Each child logs in with their assigned email. Children can't self-register. <span style={{ color: '#A5B4FC', fontWeight: 700, cursor: 'pointer' }}>Manage permissions →</span></div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}

function ChildWebCard({ child, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: '#1E293B', borderRadius: 24, padding: 24,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      cursor: 'pointer',
      display: 'flex', flexDirection: 'column', gap: 18,
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
    }}
    onPointerOver={e => { e.currentTarget.style.transform = 'translateY(-2px)'; e.currentTarget.style.boxShadow = '0 8px 24px rgba(0,0,0,0.25)'; }}
    onPointerOut ={e => { e.currentTarget.style.transform = 'translateY(0)';     e.currentTarget.style.boxShadow = '0 4px 12px rgba(0,0,0,0.15)'; }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
        <div style={{
          width: 64, height: 64, borderRadius: '50%',
          background: child.color, color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontWeight: 900, fontSize: 26,
          boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2), 0 6px 16px rgba(0,0,0,0.25)',
        }}>{child.name[0]}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 900, fontSize: 22, color: '#F8FAFC', lineHeight: 1 }}>{child.name}</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6, flexWrap: 'wrap' }}>
            <span style={{ padding: '2px 8px', borderRadius: 9999, background: 'rgba(79,70,229,0.18)', color: '#A5B4FC', fontWeight: 800, fontSize: 11 }}>Grade {child.grade}</span>
            <span style={{ fontSize: 12, color: '#94A3B8' }}>{child.language}</span>
          </div>
        </div>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 4,
          fontSize: 11, fontWeight: 700,
          color: child.active ? '#22C55E' : '#64748B',
        }}>
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: child.active ? '#22C55E' : '#64748B',
            boxShadow: child.active ? '0 0 6px rgba(34,197,94,0.6)' : 'none',
          }}/>
          {child.active ? 'Active today' : 'Inactive'}
        </div>
      </div>

      <div style={{ display: 'flex', gap: 14 }}>
        <ChildKPI icon="🧠" value={`Lv ${child.level}`} label="Level" color="#A855F7"/>
        <ChildKPI icon="⭐" value={child.xp.toLocaleString()} label="XP" color="#FACC15"/>
        <ChildKPI icon="🔥" value={`${child.streak}d`} label="Streak" color="#FB923C"/>
      </div>

      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
          <span style={{ fontSize: 11, fontWeight: 700, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Mastery</span>
          <span style={{ fontSize: 12, fontWeight: 800, color: '#F8FAFC' }}>{child.mastery}%</span>
        </div>
        <div style={{ height: 8, background: '#0F172A', borderRadius: 9999, overflow: 'hidden' }}>
          <div style={{ height: '100%', width: `${child.mastery}%`, background: 'linear-gradient(90deg,#22C55E,#4F46E5)' }}/>
        </div>
      </div>

      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        paddingTop: 14, borderTop: '1px solid rgba(255,255,255,0.05)',
        fontSize: 12, color: '#CBD5E1',
      }}>
        <span><span style={{ color: '#94A3B8' }}>Weakest:</span> <b>{child.weakest}</b></span>
        <span style={{ color: '#A5B4FC', fontWeight: 800 }}>View dashboard →</span>
      </div>
    </div>
  );
}

function ChildKPI({ icon, value, label, color }) {
  return (
    <div style={{ flex: 1, padding: '10px 12px', background: '#0F172A', borderRadius: 14, border: '1px solid rgba(255,255,255,0.04)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        <span style={{ fontSize: 14 }}>{icon}</span>
        <span style={{ fontWeight: 900, fontSize: 16, color, fontVariantNumeric: 'tabular-nums' }}>{value}</span>
      </div>
      <div style={{ fontSize: 10, fontWeight: 700, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em', marginTop: 2 }}>{label}</div>
    </div>
  );
}

function HeroStat({ icon, value, label }) {
  return (
    <div style={{ textAlign: 'center', position: 'relative' }}>
      <div style={{ fontSize: 22, marginBottom: 4 }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 28, color: '#fff', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
      <div style={{ fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.08em', opacity: 0.85, marginTop: 4 }}>{label}</div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────── REPORTS
function ReportsWebPage({ sidebarActive, onNav }) {
  const monthData = [
    { day: '1', xp: 45 }, { day: '2', xp: 60 }, { day: '3', xp: 90 }, { day: '4', xp: 30 }, { day: '5', xp: 80 },
    { day: '6', xp: 70 }, { day: '7', xp: 100 }, { day: '8', xp: 50 }, { day: '9', xp: 85 }, { day: '10', xp: 95 },
    { day: '11', xp: 40 }, { day: '12', xp: 110 }, { day: '13', xp: 70 }, { day: '14', xp: 60 }, { day: '15', xp: 0 },
    { day: '16', xp: 75 }, { day: '17', xp: 90 }, { day: '18', xp: 110 }, { day: '19', xp: 50 }, { day: '20', xp: 130, today: true },
  ];
  return (
    <AppShell active={sidebarActive} onNav={onNav}>
      <PDHeader title="Sami's reports" sub="Detailed monthly breakdown · Switch child in header" />
      <div style={{ flex: 1, overflow: 'auto', padding: 28, display: 'flex', flexDirection: 'column', gap: 20, ...appFont }}>

        {/* KPI strip */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
          <PDStatCard label="Time learning"  value="14h 12m" delta="+38%" accent="#4F46E5" icon="⏱️"/>
          <PDStatCard label="XP earned"      value="2,180"   delta="+22%" accent="#FACC15" icon="⭐"/>
          <PDStatCard label="Lessons mastered" value="42"    delta="+9"   accent="#22C55E" icon="✓"/>
          <PDStatCard label="Avg. accuracy"  value="84%"     delta="+6%"  accent="#A855F7" icon="🎯"/>
        </div>

        {/* Big activity chart */}
        <PDPanel title="Last 20 days · XP earned" sub="Today highlighted in indigo" action="Export CSV">
          <div style={{ display: 'flex', gap: 6, alignItems: 'flex-end', height: 220 }}>
            {monthData.map((d, i) => {
              const max = 130;
              return (
                <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                  <div style={{
                    width: '100%', height: `${(d.xp / max) * 180}px`,
                    minHeight: d.xp === 0 ? 4 : 8,
                    background: d.today ? 'linear-gradient(180deg,#A855F7,#4F46E5)'
                      : d.xp === 0 ? '#1E293B'
                      : 'linear-gradient(180deg,#334155,#1E293B)',
                    borderRadius: '6px 6px 3px 3px',
                    boxShadow: d.today ? '0 6px 18px rgba(99,102,241,0.4)' : 'none',
                  }}/>
                  <div style={{
                    fontSize: 10, fontWeight: 700,
                    color: d.today ? '#A5B4FC' : '#64748B',
                    fontVariantNumeric: 'tabular-nums',
                  }}>{d.day}</div>
                </div>
              );
            })}
          </div>
        </PDPanel>

        {/* Two column */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>
          <PDPanel title="Skills mastery" sub="Mastery levels across subjects">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              {[
                { name: 'Math',     pct: 72, lessons: 14, color: '#4F46E5' },
                { name: 'Reading',  pct: 65, lessons: 8,  color: '#A855F7' },
                { name: 'Science',  pct: 58, lessons: 6,  color: '#22C55E' },
                { name: 'English',  pct: 81, lessons: 12, color: '#FB923C' },
                { name: 'Arabic',   pct: 48, lessons: 4,  color: '#38BDF8' },
              ].map(s => (
                <div key={s.name} style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 700 }}>
                    <span style={{ color: '#F8FAFC' }}>{s.name}</span>
                    <span style={{ color: '#94A3B8' }}>{s.lessons} lessons · <span style={{ color: s.color, fontWeight: 800 }}>{s.pct}%</span></span>
                  </div>
                  <div style={{ height: 10, background: '#0F172A', borderRadius: 9999, overflow: 'hidden' }}>
                    <div style={{ height: '100%', width: `${s.pct}%`, background: s.color }}/>
                  </div>
                </div>
              ))}
            </div>
          </PDPanel>

          <PDPanel title="Time of day" sub="When Sami learns best">
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 6, height: 200, paddingBottom: 20 }}>
              {[
                ['6a', 5], ['8a', 15], ['10a', 45], ['12p', 30], ['2p', 60], ['4p', 95], ['6p', 70], ['8p', 25],
              ].map(([label, v], i) => (
                <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
                  <div style={{ fontSize: 10, fontWeight: 700, color: '#94A3B8', fontVariantNumeric: 'tabular-nums' }}>{v}m</div>
                  <div style={{
                    width: '100%', height: `${(v / 100) * 160}px`,
                    background: v >= 90 ? 'linear-gradient(180deg,#FB923C,#EF4444)' : v >= 60 ? '#A855F7' : '#334155',
                    borderRadius: '6px 6px 3px 3px',
                    boxShadow: v >= 90 ? '0 4px 14px rgba(251,146,60,0.35)' : 'none',
                  }}/>
                  <div style={{ fontSize: 11, fontWeight: 700, color: v >= 90 ? '#F8FAFC' : '#94A3B8' }}>{label}</div>
                </div>
              ))}
            </div>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '10px 12px', background: 'rgba(251,146,60,0.08)', borderRadius: 12,
              border: '1px solid rgba(251,146,60,0.2)',
              fontSize: 13, color: '#FB923C', fontWeight: 700,
            }}>💡 Peak focus is 4–5pm — great time for new material</div>
          </PDPanel>
        </div>

        {/* Weak areas detail */}
        <PDPanel title="Areas to focus on" sub="Topics where Sami is still building confidence">
          <PDWeakAreas items={[
            { topic: 'Subtraction with borrowing', subject: 'Math',     icon: '➖', color: '#EF4444', accuracy: 42 },
            { topic: 'Long vowels',                subject: 'Reading',  icon: '🔤', color: '#F59E0B', accuracy: 58 },
            { topic: 'States of matter',           subject: 'Science',  icon: '🧪', color: '#F59E0B', accuracy: 64 },
            { topic: 'Multiplication tables',      subject: 'Math',     icon: '✕',  color: '#22C55E', accuracy: 78 },
          ]}/>
        </PDPanel>
      </div>
    </AppShell>
  );
}

// ────────────────────────────────────────────────────────────── SETTINGS
function SettingsWebPage({ sidebarActive, onNav }) {
  const [tab, setTab] = React.useState('profile');
  return (
    <AppShell active={sidebarActive} onNav={onNav}>
      <PDHeader title="Settings" sub="Manage your account and preferences"/>
      <div style={{ flex: 1, overflow: 'auto', padding: 28, display: 'grid', gridTemplateColumns: '220px 1fr', gap: 24, ...appFont }}>
        {/* tab nav */}
        <nav style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {[
            ['profile',       '👤 Profile'],
            ['notifications', '🔔 Notifications'],
            ['linked',        '👨‍👩‍👦 Linked children'],
            ['security',      '🛡️ Security'],
            ['plan',          '💎 Plan & billing'],
            ['language',      '🌍 Language & region'],
          ].map(([id, label]) => (
            <button key={id} onClick={() => setTab(id)} style={{
              textAlign: 'left', padding: '10px 14px', borderRadius: 12, border: 'none',
              background: tab === id ? 'rgba(79,70,229,0.18)' : 'transparent',
              color: tab === id ? '#A5B4FC' : '#94A3B8',
              fontWeight: tab === id ? 800 : 600, fontSize: 14,
              cursor: 'pointer', fontFamily: 'inherit',
            }}>{label}</button>
          ))}
        </nav>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          {tab === 'profile' && <SettingsProfile/>}
          {tab === 'notifications' && <SettingsNotifications/>}
          {tab === 'linked' && <SettingsLinked/>}
          {tab === 'security' && <SettingsSecurity/>}
          {tab === 'plan' && <SettingsPlan/>}
          {tab === 'language' && <SettingsLanguage/>}
        </div>
      </div>
    </AppShell>
  );
}

function SettingsProfile() {
  return (
    <PDPanel title="Profile" sub="This is how Learnexia knows you">
      <div style={{ display: 'flex', alignItems: 'center', gap: 18 }}>
        <div style={{
          width: 84, height: 84, borderRadius: '50%',
          background: 'linear-gradient(135deg,#FB923C,#EF4444)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontWeight: 900, fontSize: 32, color: '#fff',
        }}>A</div>
        <div style={{ display: 'flex', gap: 10 }}>
          <button style={btnPrimary()}>Upload photo</button>
          <button style={btnGhost()}>Remove</button>
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <WebField label="Full name"><input style={webInputStyle()} defaultValue="Ahmed Hassan"/></WebField>
        <WebField label="Email"><input type="email" style={webInputStyle()} defaultValue="ahmed@email.com"/></WebField>
        <WebField label="Phone"><input style={webInputStyle()} defaultValue="+966 50 123 4567"/></WebField>
        <WebField label="Country">
          <select style={webInputStyle()} defaultValue="SA">
            <option value="SA">🇸🇦 Saudi Arabia</option>
            <option value="AE">🇦🇪 UAE</option>
          </select>
        </WebField>
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, paddingTop: 6 }}>
        <button style={btnGhost()}>Cancel</button>
        <button style={btnPrimary()}>Save changes</button>
      </div>
    </PDPanel>
  );
}

function SettingsNotifications() {
  const [prefs, setPrefs] = React.useState({
    weekly: true, streak: true, lessonReminders: true, marketing: false,
    childReports: true, lowHearts: false,
  });
  const toggle = k => setPrefs({ ...prefs, [k]: !prefs[k] });
  const rows = [
    ['weekly',          'Weekly progress reports',  'Sent every Sunday morning'],
    ['streak',          'Streak risk alerts',       'Ping me if a child\'s streak is about to break'],
    ['lessonReminders', 'Daily lesson reminders',   'Gentle nudge for each child at their best time'],
    ['childReports',    'Child milestones',         'Level-ups, new badges, league promotions'],
    ['lowHearts',       'Low hearts warning',       'When a child runs out of hearts during practice'],
    ['marketing',       'Tips & product updates',   'Helpful articles and new features'],
  ];
  return (
    <PDPanel title="Notifications" sub="Choose what we email you about">
      {rows.map(([key, title, sub]) => (
        <div key={key} style={{
          display: 'flex', alignItems: 'center', gap: 14, justifyContent: 'space-between',
          padding: '14px 0', borderTop: '1px solid rgba(255,255,255,0.05)',
        }}>
          <div>
            <div style={{ fontWeight: 700, fontSize: 14, color: '#F8FAFC' }}>{title}</div>
            <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>{sub}</div>
          </div>
          <Toggle on={prefs[key]} onChange={() => toggle(key)}/>
        </div>
      ))}
    </PDPanel>
  );
}

function SettingsLinked() {
  const children = [
    { name: 'Sami',  email: 'sami@learnexia.com',  grade: 3, language: 'EN', color: '#FB923C' },
    { name: 'Layla', email: 'layla@learnexia.com', grade: 1, language: 'AR', color: '#A855F7' },
    { name: 'Yusuf', email: 'yusuf@learnexia.com', grade: 5, language: 'EN', color: '#38BDF8' },
  ];
  return (
    <PDPanel title="Linked children" sub="Manage who's on your account" action="+ Add child">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {children.map((c, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 14,
            padding: '14px 16px', background: '#0F172A', borderRadius: 14,
            border: '1px solid rgba(255,255,255,0.04)',
          }}>
            <div style={{
              width: 44, height: 44, borderRadius: '50%',
              background: c.color, color: '#fff',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontWeight: 900, fontSize: 16,
            }}>{c.name[0]}</div>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span style={{ fontWeight: 800, fontSize: 14, color: '#F8FAFC' }}>{c.name}</span>
                <span style={{ padding: '2px 7px', borderRadius: 9999, background: 'rgba(79,70,229,0.18)', color: '#A5B4FC', fontWeight: 800, fontSize: 10 }}>Grade {c.grade}</span>
                <span style={{ padding: '2px 7px', borderRadius: 9999, background: 'rgba(255,255,255,0.06)', color: '#94A3B8', fontWeight: 700, fontSize: 10 }}>{c.language}</span>
              </div>
              <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 3 }}>{c.email}</div>
            </div>
            <button style={{ ...btnGhost(), height: 36, padding: '0 14px', fontSize: 13 }}>Edit</button>
            <button style={{
              ...btnGhost(), height: 36, padding: '0 14px', fontSize: 13,
              color: '#EF4444', borderColor: 'rgba(239,68,68,0.3)',
            }}>Remove</button>
          </div>
        ))}
      </div>
    </PDPanel>
  );
}

function SettingsSecurity() {
  return (
    <>
      <PDPanel title="Password" sub="Last changed 3 months ago">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
          <WebField label="Current"><input type="password" style={webInputStyle()} placeholder="••••••••"/></WebField>
          <WebField label="New"><input type="password" style={webInputStyle()} placeholder="••••••••"/></WebField>
          <WebField label="Confirm"><input type="password" style={webInputStyle()} placeholder="••••••••"/></WebField>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}><button style={btnPrimary()}>Update password</button></div>
      </PDPanel>
      <PDPanel title="Two-factor authentication" sub="Add an extra layer of security">
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: 16, background: '#0F172A', borderRadius: 14,
        }}>
          <div>
            <div style={{ fontWeight: 800, fontSize: 14, color: '#F8FAFC' }}>SMS authentication</div>
            <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>+966 50 ••• 4567</div>
          </div>
          <span style={{ padding: '4px 10px', borderRadius: 9999, background: 'rgba(34,197,94,0.18)', color: '#22C55E', fontWeight: 800, fontSize: 11 }}>Enabled</span>
        </div>
      </PDPanel>
    </>
  );
}

function SettingsPlan() {
  return (
    <PDPanel title="Plan & billing" sub="You're on the Family plan">
      <div style={{
        display: 'flex', alignItems: 'center', gap: 18,
        padding: 20, borderRadius: 16,
        background: 'linear-gradient(135deg,#A855F7,#6366F1)',
        color: '#fff',
      }}>
        <div style={{ fontSize: 40 }}>💎</div>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 900, fontSize: 20 }}>Family · 3 children</div>
          <div style={{ fontSize: 13, opacity: 0.85, marginTop: 2 }}>Renews Dec 15, 2026 · $14.99 / month</div>
        </div>
        <button style={{ ...btnPrimary(), background: '#fff', color: '#4F46E5', boxShadow: 'none' }}>Manage</button>
      </div>
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 14, marginTop: 14,
      }}>
        {[
          ['Apr 2026', '$14.99', 'Paid'],
          ['May 2026', '$14.99', 'Paid'],
          ['Jun 2026', '$14.99', 'Paid'],
        ].map(([m, a, s], i) => (
          <div key={i} style={{ padding: 14, background: '#0F172A', borderRadius: 12, border: '1px solid rgba(255,255,255,0.04)' }}>
            <div style={{ fontSize: 12, color: '#94A3B8' }}>{m}</div>
            <div style={{ fontWeight: 800, fontSize: 18, color: '#F8FAFC', marginTop: 2 }}>{a}</div>
            <div style={{ fontSize: 11, fontWeight: 800, color: '#22C55E', textTransform: 'uppercase', letterSpacing: '0.06em', marginTop: 4 }}>{s}</div>
          </div>
        ))}
      </div>
    </PDPanel>
  );
}

function SettingsLanguage() {
  return (
    <PDPanel title="Language & region" sub="Affects your dashboard, not your children's apps">
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <WebField label="Display language">
          <select style={webInputStyle()} defaultValue="en">
            <option value="en">🇬🇧 English</option>
            <option value="ar">🇸🇦 العربية</option>
          </select>
        </WebField>
        <WebField label="Time zone">
          <select style={webInputStyle()} defaultValue="ksa">
            <option value="ksa">Saudi Arabia (GMT+3)</option>
            <option value="uae">UAE (GMT+4)</option>
            <option value="utc">UTC</option>
          </select>
        </WebField>
        <WebField label="Date format">
          <select style={webInputStyle()} defaultValue="dmy">
            <option value="dmy">DD/MM/YYYY</option>
            <option value="mdy">MM/DD/YYYY</option>
            <option value="ymd">YYYY-MM-DD</option>
          </select>
        </WebField>
        <WebField label="Week starts on">
          <select style={webInputStyle()} defaultValue="sun">
            <option value="sun">Sunday</option>
            <option value="mon">Monday</option>
          </select>
        </WebField>
      </div>
    </PDPanel>
  );
}

function Toggle({ on, onChange }) {
  return (
    <button onClick={onChange} style={{
      width: 44, height: 26, borderRadius: 9999, border: 'none',
      background: on ? '#4F46E5' : '#334155',
      position: 'relative', cursor: 'pointer', flexShrink: 0,
      transition: 'background 180ms',
      boxShadow: on ? '0 0 12px rgba(99,102,241,0.4)' : 'none',
    }}>
      <span style={{
        position: 'absolute', top: 3, left: on ? 21 : 3,
        width: 20, height: 20, borderRadius: '50%', background: '#fff',
        boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
        transition: 'left 180ms cubic-bezier(0.16,1,0.3,1)',
      }}/>
    </button>
  );
}

// ────────────────────────────────────────────────────────────── App shell with sidebar
function AppShell({ active, onNav, children }) {
  return (
    <div style={{ display: 'flex', minHeight: 820, background: '#0F172A', color: '#F8FAFC', ...appFont }}>
      <PDSidebar active={active} onChange={onNav}/>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        {children}
      </div>
    </div>
  );
}

Object.assign(window, {
  MyChildrenWebPage, ReportsWebPage, SettingsWebPage, AppShell,
});
