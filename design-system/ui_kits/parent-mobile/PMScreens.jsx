// Learnexia — Parent Mobile · core screens.
// Login · Register · My Children · Child Overview · Reports

const psFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ───────────────────────────────────────────── SPLASH
function PMSplashScreen({ onContinue }) {
  const sparkles = React.useMemo(() => Array.from({ length: 14 }, (_, i) => ({
    top: (i * 73) % 100, left: (i * 41 + 17) % 100, size: (i % 3) + 3, opacity: 0.25 + ((i * 7) % 50) / 100,
  })), []);
  return (
    <div onClick={onContinue} style={{
      width: '100%', height: '100%', position: 'relative', cursor: 'pointer', overflow: 'hidden', ...psFont,
      background: 'radial-gradient(circle at 50% 45%,#4F3FB0 0%,#3B2C8F 40%,#241B6A 100%)',
    }}>
      {sparkles.map((s, i) => (
        <div key={i} style={{ position: 'absolute', top: `${s.top}%`, left: `${s.left}%`, width: s.size, height: s.size, borderRadius: '50%', background: '#fff', opacity: s.opacity, boxShadow: `0 0 ${s.size * 2}px rgba(255,255,255,${s.opacity})` }}/>
      ))}
      {[['20%','18%',20],['30%','78%',15],['66%','80%',17]].map((p,i) => (
        <span key={'tw'+i} style={{ position: 'absolute', top: p[0], left: p[1], fontSize: p[2], filter: 'drop-shadow(0 0 6px rgba(196,181,253,0.7))' }}>✨</span>
      ))}
      <div style={{ position: 'absolute', top: '40%', left: '50%', transform: 'translate(-50%, -50%)', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 18 }}>
        <div style={{ width: 132, height: 132, borderRadius: '50%', background: 'radial-gradient(circle, rgba(250,204,21,0.35) 0%, rgba(168,85,247,0) 65%)', display: 'flex', alignItems: 'center', justifyContent: 'center', animation: 'lxpop 800ms cubic-bezier(0.34,1.56,0.64,1)' }}>
          <div style={{ fontSize: 88, filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.6))', animation: 'lxpulse 2.4s ease-in-out infinite' }}>🌟</div>
        </div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontWeight: 900, fontSize: 36, color: '#F8FAFC', letterSpacing: '-0.02em' }}>Learnexia</div>
          <div style={{ fontSize: 14, color: 'rgba(255,255,255,0.7)', marginTop: 8, fontWeight: 500 }}>Parent Companion</div>
        </div>
      </div>
      <div style={{ position: 'absolute', top: '70%', left: 0, right: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14 }}>
        <div style={{ display: 'flex', gap: 6 }}>
          {[0,1,2].map(i => <div key={i} style={{ width: 8, height: 8, borderRadius: '50%', background: i === 0 ? '#A855F7' : i === 1 ? '#6366F1' : 'rgba(255,255,255,0.3)', animation: `lxdot 1.4s ease-in-out ${i * 0.2}s infinite` }}/>)}
        </div>
        <div style={{ width: 220, height: 6, borderRadius: 9999, background: 'rgba(0,0,0,0.35)', overflow: 'hidden' }}>
          <div style={{ height: '100%', width: '55%', background: 'linear-gradient(90deg,#C4B5FD,#818CF8)', borderRadius: 9999, animation: 'lxload 2.5s ease-in-out infinite' }}/>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'rgba(255,255,255,0.7)', fontSize: 15, fontWeight: 500 }}>Loading… <span style={{ fontSize: 16 }}>⚡</span></div>
      </div>
      <div style={{ position: 'absolute', bottom: 50, left: 0, right: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'rgba(255,255,255,0.45)', letterSpacing: '0.18em', textTransform: 'uppercase' }}>POWERED BY AI</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'rgba(255,255,255,0.65)', fontWeight: 500 }}><span style={{ color: '#A78BFA' }}>✦</span> Family Learning <span style={{ color: '#A78BFA' }}>✦</span></div>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── ROLE SELECT
function PMRoleSelectScreen({ onPick }) {
  const roles = [
    { id: 'parent',  emoji: '👨‍👩‍👦', label: 'Parent', sub: "Track your child's progress" },
    { id: 'student', emoji: '🎓',       label: 'Student', sub: 'Learn, play, level up' },
  ];
  return (
    <PMScreen padTop={70} tabBar={false}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 40, marginBottom: 4 }}>🎮</div>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC' }}>Welcome to Learnexia</div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6 }}>Who's signing in?</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {roles.map(r => (
            <button key={r.id} onClick={() => onPick(r.id)} style={{
              display: 'flex', alignItems: 'center', gap: 16, padding: 18, borderRadius: 20,
              background: '#1E293B', border: '2px solid rgba(255,255,255,0.06)', boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
              fontFamily: 'inherit', cursor: 'pointer', textAlign: 'left', color: '#F8FAFC',
            }}
            onPointerOver={e => { e.currentTarget.style.borderColor = '#4F46E5'; e.currentTarget.style.background = '#243349'; }}
            onPointerOut={e => { e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)'; e.currentTarget.style.background = '#1E293B'; }}>
              <div style={{ width: 56, height: 56, borderRadius: 16, background: 'rgba(79,70,229,0.18)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28 }}>{r.emoji}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 800, fontSize: 17 }}>{r.label}</div>
                <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>{r.sub}</div>
              </div>
              <div style={{ color: '#A5B4FC', fontSize: 22 }}>›</div>
            </button>
          ))}
        </div>
        <div style={{ textAlign: 'center', fontSize: 12, color: '#64748B', lineHeight: 1.5 }}>Children log in with the email a parent assigned — they never create their own account.</div>
      </div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── LOGIN (matches student-mobile, no role toggle)
function PMLoginScreen({ onLogin, onRegister, onBack, role = 'parent' }) {
  const isStudent = role === 'student';
  return (
    <div style={{ width: '100%', height: '100%', position: 'relative', background: '#0F172A', overflow: 'auto', ...psFont }}>
      <div style={{ position: 'absolute', top: -180, left: '50%', transform: 'translateX(-50%)', width: 460, height: 360, borderRadius: '50%', background: 'radial-gradient(circle, rgba(168,85,247,0.35) 0%, rgba(168,85,247,0) 65%)', pointerEvents: 'none' }}/>
      <div style={{ position: 'relative', padding: '64px 24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
        {onBack && <button onClick={onBack} style={{ position: 'absolute', top: 64, left: 24, width: 40, height: 40, borderRadius: 12, background: '#1E293B', border: '1px solid rgba(255,255,255,0.08)', color: '#CBD5E1', fontSize: 18, cursor: 'pointer' }}>‹</button>}
        <div style={{ textAlign: 'center', marginBottom: 4 }}>
          <div style={{ width: 72, height: 72, borderRadius: 22, margin: '0 auto 12px', background: 'linear-gradient(135deg,#A855F7,#6366F1)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 12px 32px rgba(168,85,247,0.4), inset 0 2px 0 rgba(255,255,255,0.18)' }}>
            <span style={{ fontSize: 34, filter: 'drop-shadow(0 0 8px rgba(250,204,21,0.6))' }}>🌟</span>
          </div>
          <div style={{ fontWeight: 900, fontSize: 27, color: '#F8FAFC' }}>Welcome back</div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6 }}>
            {isStudent ? 'Log in to keep your streak alive 🔥' : "Sign in to follow your children's progress"}
          </div>
        </div>
        {/* role badge (chosen on previous screen) */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, padding: '8px 14px', background: '#15161D', borderRadius: 12, border: '1px solid rgba(255,255,255,0.06)' }}>
          <span style={{ fontSize: 16 }}>{isStudent ? '🎓' : '👨‍👩‍👦'}</span>
          <span style={{ fontSize: 13, fontWeight: 700, color: '#CBD5E1' }}>Signing in as {isStudent ? 'Student' : 'Parent'}</span>
          {onBack && <button onClick={onBack} style={{ background: 'transparent', border: 'none', color: '#A5B4FC', fontWeight: 700, fontSize: 12, cursor: 'pointer', fontFamily: 'inherit', padding: 0, marginLeft: 4 }}>Change</button>}
        </div>
        <PMField label="Email" icon="✉️"><input type="email" defaultValue={isStudent ? 'sami@learnexia.com' : 'ahmed@email.com'} style={pmInput}/></PMField>
        <PMField label="Password" icon="🔒" right={<span style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 600 }}>Show</span>}>
          <input type="password" defaultValue="••••••••" style={pmInput}/>
        </PMField>
        <div style={{ textAlign: 'right', marginTop: -8 }}><span style={{ fontSize: 13, color: '#A5B4FC', fontWeight: 600 }}>Forgot password?</span></div>
        <PMButton onClick={onLogin}>Log in →</PMButton>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '2px 0' }}>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B' }}>OR</div>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
        </div>
        <div style={{ display: 'flex', gap: 10 }}>
          <PMButton variant="ghost" style={{ height: 48, fontSize: 14 }}>G  Google</PMButton>
          <PMButton variant="ghost" style={{ height: 48, fontSize: 14 }}>  Apple</PMButton>
        </div>
        {!isStudent && (
          <div style={{ textAlign: 'center', fontSize: 14, color: '#94A3B8' }}>
            New here? <span onClick={onRegister} style={{ color: '#A5B4FC', fontWeight: 800, cursor: 'pointer' }}>Create account</span>
          </div>
        )}
        {isStudent && (
          <div style={{ textAlign: 'center', fontSize: 13, color: '#64748B', marginTop: 4, padding: '12px 14px', borderRadius: 12, background: 'rgba(245,158,11,0.06)', border: '1px solid rgba(245,158,11,0.18)' }}>
            <span style={{ color: '#F59E0B', fontWeight: 700 }}>Need an account?</span> Ask a parent to add you.
          </div>
        )}
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── REGISTER (parent)
function PMRegisterScreen({ onRegister, onLogin }) {
  return (
    <PMScreen padTop={70} tabBar={false}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div style={{ marginBottom: 2 }}>
          <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC' }}>Create parent account</div>
          <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 6, lineHeight: 1.5 }}>You'll add your children's accounts in the next step.</div>
        </div>
        <PMField label="Full name" icon="👤"><input defaultValue="Ahmed Hassan" style={pmInput}/></PMField>
        <PMField label="Email" icon="✉️"><input type="email" defaultValue="ahmed@email.com" style={pmInput}/></PMField>
        <PMField label="Password" icon="🔒"><input type="password" defaultValue="parent123" style={pmInput}/></PMField>
        <PMField label="Country" icon="🌍">
          <div style={{ ...pmInput, display: 'flex', alignItems: 'center' }}>🇪🇬 Egypt</div>
        </PMField>
        <label style={{ display: 'flex', alignItems: 'flex-start', gap: 12, padding: '12px 14px', borderRadius: 14, background: 'rgba(34,197,94,0.06)', border: '1px solid rgba(34,197,94,0.25)' }}>
          <div style={{ width: 24, height: 24, borderRadius: 7, flexShrink: 0, marginTop: 1, background: '#22C55E', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#0F172A', fontWeight: 900, fontSize: 14 }}>✓</div>
          <div style={{ flex: 1, fontSize: 12.5, color: '#CBD5E1', lineHeight: 1.5 }}>I'm a parent or legal guardian and agree to the <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Terms</span> and <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Privacy Policy</span>.</div>
        </label>
        <PMButton onClick={onRegister}>Continue → Add children</PMButton>
        <div style={{ textAlign: 'center', fontSize: 14, color: '#94A3B8' }}>
          Have an account? <span onClick={onLogin} style={{ color: '#A5B4FC', fontWeight: 800, cursor: 'pointer' }}>Log in</span>
        </div>
      </div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── MY CHILDREN
function PMChildrenScreen({ onPick, onAddChild, onAvatar }) {
  return (
    <PMScreen padTop={14}>
      <PMTopBar sub="👨‍👩‍👦 Parent · Ahmed" title="My Children" avatar="A" onAvatar={onAvatar}/>
      {/* family summary */}
      <div style={{ background: 'linear-gradient(135deg,#A855F7,#6366F1)', borderRadius: 20, padding: 18, color: '#fff', boxShadow: '0 8px 24px rgba(99,102,241,0.35)', marginBottom: 18 }}>
        <div style={{ fontSize: 11, fontWeight: 800, letterSpacing: '0.12em', opacity: 0.85, marginBottom: 10 }}>THIS WEEK · ALL CHILDREN</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <PMFamStat icon="⭐" value="4,480" label="Total XP"/>
          <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.2)' }}/>
          <PMFamStat icon="📚" value="18" label="Lessons"/>
          <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.2)' }}/>
          <PMFamStat icon="🔥" value="6/7" label="Active"/>
        </div>
      </div>
      <PMSectionTitle>3 children linked</PMSectionTitle>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {PM_CHILDREN.map(c => <PMChildRow key={c.id} child={c} onClick={() => onPick(c)} onEdit={onAddChild}/>)}
      </div>
      {/* add child CTA */}
      <button onClick={onAddChild} style={{
        marginTop: 12, width: '100%', padding: 18, borderRadius: 20, background: 'transparent',
        border: '2px dashed rgba(99,102,241,0.4)', display: 'flex', alignItems: 'center', gap: 14, cursor: 'pointer', ...psFont,
      }}>
        <div style={{ width: 48, height: 48, borderRadius: 14, background: 'rgba(79,70,229,0.18)', color: '#A5B4FC', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26, fontWeight: 800 }}>+</div>
        <div style={{ flex: 1, textAlign: 'left' }}>
          <div style={{ fontWeight: 800, fontSize: 16, color: '#F8FAFC' }}>Add a child</div>
          <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>Set their grade, language & login email</div>
        </div>
      </button>
    </PMScreen>
  );
}

function PMFamStat({ icon, value, label }) {
  return (
    <div style={{ flex: 1, textAlign: 'center' }}>
      <div style={{ fontSize: 18 }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 20 }}>{value}</div>
      <div style={{ fontSize: 10, fontWeight: 700, opacity: 0.85, marginTop: 2 }}>{label}</div>
    </div>
  );
}

// ───────────────────────────────────────────── CHILD OVERVIEW (dashboard)
function PMChildOverview({ child, onBack, onReports, onEnergy }) {
  const c = child || PM_CHILDREN[0];
  const week = [
    { label: 'M', v: 45 }, { label: 'T', v: 80 }, { label: 'W', v: 30 },
    { label: 'T', v: 95 }, { label: 'F', v: 50 }, { label: 'S', v: 70 }, { label: 'S', v: 110, hi: true },
  ];
  return (
    <PMScreen padTop={14}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 18 }}>
        <button onClick={onBack} style={{ width: 40, height: 40, borderRadius: 12, background: '#1E293B', border: '1px solid rgba(255,255,255,0.08)', color: '#CBD5E1', fontSize: 18, cursor: 'pointer' }}>‹</button>
        <div style={{ width: 46, height: 46, borderRadius: '50%', background: c.color, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, fontSize: 19 }}>{c.initial}</div>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 900, fontSize: 22, color: '#F8FAFC' }}>{c.name}'s progress</div>
          <div style={{ fontSize: 12, color: '#94A3B8' }}>Grade {c.grade} · Level {c.level} · {c.active ? 'Active today' : 'Inactive'}</div>
        </div>
      </div>
      {/* KPI grid */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        <PMStat icon="⏱️" value="3h 24m" label="Time" color="#38BDF8" delta="+42m"/>
        <PMStat icon="⭐" value="480" label="XP earned" color="#FACC15" delta="+28%"/>
        <PMStat icon="✅" value="14" label="Lessons" color="#22C55E" delta="+3"/>
      </div>
      {/* activity chart */}
      <PMCard style={{ marginBottom: 16 }}>
        <PMSectionTitle action="Export">Daily activity</PMSectionTitle>
        <div style={{ fontSize: 11, color: '#94A3B8', marginTop: -6 }}>XP earned per day</div>
        <PMBarChart data={week}/>
      </PMCard>
      {/* subject mastery */}
      <PMCard style={{ marginBottom: 16 }}>
        <div style={{ fontWeight: 800, fontSize: 15, color: '#F8FAFC', marginBottom: 4 }}>Subject mastery</div>
        <div style={{ fontSize: 11, color: '#94A3B8', marginBottom: 14 }}>Last 7 days</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <PMMasteryRow subject="Math" pct={72} color="#A855F7"/>
          <PMMasteryRow subject="Reading" pct={65} color="#FB923C"/>
          <PMMasteryRow subject="Science" pct={58} color="#22C55E"/>
          <PMMasteryRow subject="Art" pct={84} color="#38BDF8"/>
        </div>
      </PMCard>
      {/* quick links */}
      <div style={{ display: 'flex', gap: 10 }}>
        <PMButton variant="ghost" onClick={onReports} style={{ height: 48, fontSize: 14 }}>📈 Full report</PMButton>
        <PMButton variant="energy" onClick={onEnergy} style={{ height: 48, fontSize: 14 }}>⚡ Energy</PMButton>
      </div>
    </PMScreen>
  );
}

// ───────────────────────────────────────────── REPORTS
function PMReportsScreen({ onAvatar }) {
  return (
    <PMScreen padTop={14}>
      <PMTopBar sub="Sami · Grade 3" title="Reports" avatar="A" onAvatar={onAvatar}/>
      {/* weekly summary banner */}
      <div style={{ background: 'linear-gradient(135deg,rgba(34,197,94,0.18),#1E293B)', border: '1px solid rgba(34,197,94,0.3)', borderRadius: 20, padding: 18, marginBottom: 16 }}>
        <div style={{ fontSize: 11, fontWeight: 800, color: '#22C55E', letterSpacing: '0.1em' }}>WEEK OF NOV 18 – 24</div>
        <div style={{ fontWeight: 900, fontSize: 20, color: '#F8FAFC', marginTop: 6 }}>Sami had a strong week 🎉</div>
        <div style={{ fontSize: 13, color: '#CBD5E1', marginTop: 4, lineHeight: 1.5 }}>+480 XP earned · 14 lessons · accuracy up 9 points.</div>
      </div>
      {/* areas to focus */}
      <PMSectionTitle action="See all">Areas to focus on</PMSectionTitle>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 18 }}>
        <PMFocusRow icon="➖" color="#EF4444" title="Subtraction with borrowing" subject="Math" pct={42}/>
        <PMFocusRow icon="🔤" color="#F59E0B" title="Long vowels" subject="Reading" pct={58}/>
        <PMFocusRow icon="✖️" color="#22C55E" title="Times tables" subject="Math" pct={78}/>
      </div>
      {/* recommendations */}
      <PMSectionTitle>Recommendations from Lexi</PMSectionTitle>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        <PMRecRow icon="🎯" color="#4F46E5" title="Practice subtraction 5 min/day" body="Short daily reps lift accuracy from 42% in about a week." cta="Plan it"/>
        <PMRecRow icon="📖" color="#A855F7" title="Read aloud on Fridays" body="Sami reads 28% faster with a grown-up." cta="Schedule"/>
        <PMRecRow icon="🎉" color="#FACC15" title="Celebrate the 7-day streak" body="A weekend reward keeps motivation high." cta="Send praise"/>
      </div>
      <PMButton style={{ marginTop: 18 }}>📤 Send report to email</PMButton>
    </PMScreen>
  );
}

function PMRecRow({ icon, color, title, body, cta }) {
  return (
    <div style={{ display: 'flex', gap: 12, padding: 14, background: '#1E293B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 16 }}>
      <div style={{ width: 40, height: 40, borderRadius: 12, flexShrink: 0, background: `${color}22`, color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18 }}>{icon}</div>
      <div style={{ flex: 1 }}>
        <div style={{ fontWeight: 700, fontSize: 13, color: '#F8FAFC' }}>{title}</div>
        <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 3, lineHeight: 1.5 }}>{body}</div>
        <button style={{ marginTop: 10, background: 'transparent', border: 'none', color, fontFamily: 'inherit', fontWeight: 700, fontSize: 12, cursor: 'pointer', padding: 0 }}>{cta} →</button>
      </div>
    </div>
  );
}

Object.assign(window, {
  PMSplashScreen, PMRoleSelectScreen, PMLoginScreen, PMRegisterScreen, PMChildrenScreen, PMChildOverview, PMReportsScreen,
});
