// Learnexia Web — public (pre-auth) pages: Landing, Login, Register

const webFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ────────────────────────────────────────────────────────────── LANDING
function LandingPage({ onLogin, onSignup }) {
  return (
    <div style={{ width: '100%', minHeight: '100%', background: '#0F172A', color: '#F8FAFC', ...webFont }}>
      {/* Sticky nav */}
      <nav style={{
        position: 'sticky', top: 0, zIndex: 10,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '18px 48px',
        background: 'rgba(15,23,42,0.85)', backdropFilter: 'blur(20px)',
        borderBottom: '1px solid rgba(255,255,255,0.05)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <img src="../../assets/logo-mark.svg" style={{ width: 36, height: 36 }}/>
          <div style={{ fontWeight: 900, fontSize: 20 }}>Learnexia</div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 28, fontSize: 14, fontWeight: 600 }}>
          <a style={{ color: '#CBD5E1', cursor: 'pointer' }}>How it works</a>
          <a style={{ color: '#CBD5E1', cursor: 'pointer' }}>Subjects</a>
          <a style={{ color: '#CBD5E1', cursor: 'pointer' }}>For schools</a>
          <a style={{ color: '#CBD5E1', cursor: 'pointer' }}>Pricing</a>
        </div>
        <div style={{ display: 'flex', gap: 10 }}>
          <button onClick={onLogin} style={btnGhost()}>Log in</button>
          <button onClick={onSignup} style={btnPrimary()}>Start free</button>
        </div>
      </nav>

      {/* Hero */}
      <section style={{
        display: 'grid', gridTemplateColumns: '1.1fr 1fr', gap: 48,
        padding: '72px 48px 96px', alignItems: 'center',
        position: 'relative', overflow: 'hidden',
      }}>
        <div style={{
          position: 'absolute', top: -80, left: -80, width: 480, height: 480, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(168,85,247,0.25) 0%, transparent 65%)',
          pointerEvents: 'none',
        }}/>
        <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: 24 }}>
          <div style={{
            alignSelf: 'flex-start', display: 'flex', alignItems: 'center', gap: 8,
            padding: '6px 14px', borderRadius: 9999,
            background: 'rgba(168,85,247,0.15)', color: '#A855F7',
            fontWeight: 800, fontSize: 12, letterSpacing: '0.06em', textTransform: 'uppercase',
            border: '1px solid rgba(168,85,247,0.3)',
          }}>✨ Powered by AI</div>
          <h1 style={{
            margin: 0, fontWeight: 900, fontSize: 64, lineHeight: 1.05, letterSpacing: '-0.03em',
          }}>
            An <span style={{
              background: 'linear-gradient(90deg,#FACC15,#FB923C)',
              WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent', backgroundClip: 'text',
            }}>adventure game</span> your kids will love — that teaches.
          </h1>
          <p style={{ margin: 0, fontSize: 18, lineHeight: 1.55, color: '#CBD5E1', maxWidth: 520 }}>
            Learnexia mixes a personal AI tutor with hearts, streaks, XP and badges.
            Kids learn Math, Science, English and Arabic by playing — you watch them grow.
          </p>
          <div style={{ display: 'flex', gap: 12, marginTop: 8 }}>
            <button onClick={onSignup} style={{ ...btnPrimary(), height: 56, padding: '0 28px', fontSize: 16 }}>
              Create parent account →
            </button>
            <button style={{ ...btnGhost(), height: 56, padding: '0 24px', fontSize: 15 }}>
              <span style={{ marginRight: 8 }}>▶</span> Watch demo (2 min)
            </button>
          </div>
          <div style={{ display: 'flex', gap: 28, marginTop: 16, fontSize: 13, color: '#94A3B8', fontWeight: 600 }}>
            <span>⭐ 4.9 in App Store</span>
            <span>🛡️ COPPA-compliant</span>
            <span>👨‍👩‍👦 Free for first child</span>
          </div>
        </div>

        {/* Phone mock */}
        <div style={{ position: 'relative', display: 'flex', justifyContent: 'center' }}>
          <div style={{
            width: 320, height: 640, borderRadius: 44,
            background: 'linear-gradient(165deg,#A855F7 0%,#4F46E5 50%,#1E293B 100%)',
            border: '8px solid #1a1a1a',
            boxShadow: '0 40px 100px rgba(99,102,241,0.5), 0 0 0 1px rgba(255,255,255,0.05)',
            display: 'flex', flexDirection: 'column', padding: 24, gap: 16,
            transform: 'rotate(-4deg)',
          }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ fontWeight: 900, fontSize: 22 }}>Sami</div>
              <div style={{ padding: '4px 10px', borderRadius: 9999, background: 'rgba(251,146,60,0.2)', color: '#FB923C', fontWeight: 800, fontSize: 12 }}>🔥 7</div>
            </div>
            <div style={{ background: 'rgba(0,0,0,0.3)', borderRadius: 18, padding: 14 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: '#FACC15', letterSpacing: '0.1em', textTransform: 'uppercase' }}>Continue learning</div>
              <div style={{ fontWeight: 900, fontSize: 18, marginTop: 4 }}>Fractions</div>
              <div style={{ height: 6, background: 'rgba(0,0,0,0.4)', borderRadius: 9999, marginTop: 10, overflow: 'hidden' }}>
                <div style={{ height: '100%', width: '60%', background: 'linear-gradient(90deg,#22C55E,#FACC15)' }}/>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              {['🧮','🧪','📖','🇬🇧'].map((e, i) => (
                <div key={i} style={{ background: 'rgba(0,0,0,0.3)', borderRadius: 16, padding: 12, fontSize: 26 }}>{e}</div>
              ))}
            </div>
            <div style={{ marginTop: 'auto', textAlign: 'center', fontSize: 32, animation: 'lxpulse 2s ease-in-out infinite' }}>🌟</div>
          </div>
          {/* floating chips */}
          <div style={{
            position: 'absolute', top: 80, right: -30,
            background: '#22C55E', color: '#0F172A', fontWeight: 800, fontSize: 13,
            padding: '8px 14px', borderRadius: 9999,
            boxShadow: '0 8px 24px rgba(34,197,94,0.4)',
            transform: 'rotate(8deg)',
          }}>+50 XP ⭐</div>
          <div style={{
            position: 'absolute', bottom: 100, left: -40,
            background: 'rgba(15,23,42,0.85)', backdropFilter: 'blur(20px)',
            border: '1px solid rgba(255,255,255,0.1)', color: '#fff',
            fontWeight: 700, fontSize: 12, padding: '10px 14px', borderRadius: 16,
            display: 'flex', alignItems: 'center', gap: 8,
            boxShadow: '0 12px 28px rgba(0,0,0,0.4)',
            transform: 'rotate(-4deg)',
          }}><span style={{ fontSize: 18 }}>🏆</span> New badge!</div>
        </div>
      </section>

      {/* Features */}
      <section style={{ padding: '32px 48px 96px', background: '#0B1020' }}>
        <div style={{ textAlign: 'center', marginBottom: 56 }}>
          <div style={{ fontWeight: 800, fontSize: 12, color: '#A855F7', letterSpacing: '0.12em', textTransform: 'uppercase' }}>Why Learnexia</div>
          <h2 style={{ margin: '8px 0 0', fontWeight: 900, fontSize: 44, letterSpacing: '-0.02em' }}>Built for kids. Trusted by parents.</h2>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
          <Feature icon="🤖" iconBg="rgba(168,85,247,0.15)" color="#A855F7" title="AI tutor that explains" body="Stuck on a problem? Lexi explains it with pictures, examples and patient follow-ups — adapted to your child's grade."/>
          <Feature icon="🎮" iconBg="rgba(251,146,60,0.15)" color="#FB923C" title="Gamified, not gimmicky" body="Streaks, XP, badges and weekly leagues turn practice into a game your child wants to come back to."/>
          <Feature icon="📊" iconBg="rgba(34,197,94,0.15)" color="#22C55E" title="Parents stay in the loop" body="Weekly reports tell you exactly where they're flying and where they need help. No guesswork."/>
          <Feature icon="🌍" iconBg="rgba(56,189,248,0.15)" color="#38BDF8" title="Arabic + English, native" body="Full RTL support, native Arabic content, and bilingual lessons designed by curriculum experts."/>
          <Feature icon="🛡️" iconBg="rgba(250,204,21,0.15)" color="#FACC15" title="Safe and ad-free" body="No ads, no DMs, no data resold. COPPA-compliant from day one. You add your kids, no one else can."/>
          <Feature icon="⚡" iconBg="rgba(79,70,229,0.15)" color="#A5B4FC" title="5 minutes a day works" body="Short, focused lessons are designed to fit the attention span of a 6–14 year old. Big effects, small sessions."/>
        </div>
      </section>

      {/* Subjects band */}
      <section style={{ padding: '64px 48px' }}>
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <h2 style={{ margin: 0, fontWeight: 900, fontSize: 36, letterSpacing: '-0.02em' }}>Four subjects. One adventure.</h2>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 14 }}>
          {[
            { emoji: '🧮', name: 'Math',     color: '#4F46E5', topics: 'Numbers · Fractions · Geometry' },
            { emoji: '🧪', name: 'Science',  color: '#22C55E', topics: 'Plants · States · Space' },
            { emoji: '📖', name: 'Arabic',   color: '#FB923C', topics: 'Reading · Grammar · Poetry' },
            { emoji: '🇬🇧', name: 'English',  color: '#A855F7', topics: 'Phonics · Verbs · Stories' },
          ].map((s, i) => (
            <div key={i} style={{
              background: '#1E293B', borderRadius: 24, padding: 24,
              border: '1px solid rgba(255,255,255,0.06)',
              boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
              display: 'flex', flexDirection: 'column', gap: 8,
            }}>
              <div style={{
                width: 56, height: 56, borderRadius: 18,
                background: `${s.color}22`, color: s.color,
                display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 30,
              }}>{s.emoji}</div>
              <div style={{ fontWeight: 900, fontSize: 22 }}>{s.name}</div>
              <div style={{ fontSize: 13, color: '#94A3B8' }}>{s.topics}</div>
              <div style={{ marginTop: 8, color: s.color, fontWeight: 700, fontSize: 13 }}>Grade 1–6 →</div>
            </div>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section style={{ padding: '32px 48px 96px' }}>
        <div style={{
          background: 'linear-gradient(135deg,#4F46E5 0%,#A855F7 100%)',
          borderRadius: 32, padding: '56px 48px',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 32,
          boxShadow: '0 24px 60px rgba(99,102,241,0.45), inset 0 1px 0 rgba(255,255,255,0.2)',
          position: 'relative', overflow: 'hidden',
        }}>
          <div style={{ position: 'absolute', right: 40, bottom: -40, fontSize: 280, opacity: 0.15 }}>🌟</div>
          <div style={{ position: 'relative' }}>
            <div style={{ fontWeight: 900, fontSize: 36, color: '#fff', letterSpacing: '-0.02em', lineHeight: 1.1 }}>
              Ready to start the adventure?
            </div>
            <div style={{ marginTop: 10, fontSize: 16, color: 'rgba(255,255,255,0.9)' }}>
              Free for your first child · No credit card required
            </div>
          </div>
          <button onClick={onSignup} style={{
            height: 60, padding: '0 32px', borderRadius: 16, border: 'none',
            background: '#fff', color: '#4F46E5',
            fontFamily: 'inherit', fontWeight: 900, fontSize: 17, cursor: 'pointer',
            boxShadow: '0 16px 32px rgba(0,0,0,0.25)',
            whiteSpace: 'nowrap',
          }}>Create parent account →</button>
        </div>
      </section>

      {/* Footer */}
      <footer style={{
        padding: '40px 48px',
        borderTop: '1px solid rgba(255,255,255,0.05)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        color: '#64748B', fontSize: 13,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <img src="../../assets/logo-mark.svg" style={{ width: 28, height: 28, opacity: 0.7 }}/>
          <span>© 2026 Learnexia · Made for curious kids</span>
        </div>
        <div style={{ display: 'flex', gap: 24, fontWeight: 600 }}>
          <a style={{ color: '#94A3B8', cursor: 'pointer' }}>Privacy</a>
          <a style={{ color: '#94A3B8', cursor: 'pointer' }}>Terms</a>
          <a style={{ color: '#94A3B8', cursor: 'pointer' }}>Support</a>
          <a style={{ color: '#94A3B8', cursor: 'pointer' }}>العربية</a>
        </div>
      </footer>
    </div>
  );
}

function Feature({ icon, iconBg, color, title, body }) {
  return (
    <div style={{
      background: '#1E293B', borderRadius: 24, padding: 28,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex', flexDirection: 'column', gap: 14,
    }}>
      <div style={{
        width: 52, height: 52, borderRadius: 16,
        background: iconBg, color,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26,
      }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 20, color: '#F8FAFC' }}>{title}</div>
      <div style={{ fontSize: 14, lineHeight: 1.55, color: '#CBD5E1' }}>{body}</div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────── LOGIN (web)
function LoginWebPage({ onLogin, onRegister, onLanding }) {
  const [role, setRole] = React.useState('parent');
  const [showPw, setShowPw] = React.useState(false);
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const canSubmit = email.includes('@') && pw.length >= 4;

  return (
    <div style={{ minHeight: '100%', display: 'grid', gridTemplateColumns: '1fr 1fr', background: '#0F172A', color: '#F8FAFC', ...webFont }}>
      {/* Left visual panel */}
      <div style={{
        position: 'relative', overflow: 'hidden',
        background: 'linear-gradient(165deg,#4F3FB0 0%,#3B2C8F 50%,#1E1B4B 100%)',
        padding: 56, display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
      }}>
        {[...Array(12)].map((_, i) => (
          <div key={i} style={{
            position: 'absolute', top: `${(i * 73) % 100}%`, left: `${(i * 41) % 100}%`,
            width: ((i % 3) + 3), height: ((i % 3) + 3), borderRadius: '50%',
            background: '#fff', opacity: 0.2 + (i % 4) / 10,
            boxShadow: `0 0 ${(i % 3) * 4 + 6}px rgba(255,255,255,0.5)`,
          }}/>
        ))}
        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer' }} onClick={onLanding}>
          <img src="../../assets/logo-mark.svg" style={{ width: 40, height: 40 }}/>
          <div style={{ fontWeight: 900, fontSize: 22 }}>Learnexia</div>
        </div>
        <div style={{ position: 'relative' }}>
          <div style={{ fontSize: 96, animation: 'lxpulse 2.4s ease-in-out infinite', filter: 'drop-shadow(0 0 24px rgba(250,204,21,0.5))' }}>🌟</div>
          <h1 style={{ margin: '20px 0 0', fontWeight: 900, fontSize: 48, lineHeight: 1.1, letterSpacing: '-0.02em' }}>
            Welcome back to the adventure.
          </h1>
          <p style={{ margin: '14px 0 0', fontSize: 16, color: 'rgba(255,255,255,0.8)', maxWidth: 380 }}>
            Pick up your streak, keep your hearts full, and watch your kids fly through new skills.
          </p>
        </div>
        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 12, color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>
          <span style={{ fontSize: 18 }}>🔥</span> 240,000+ kids learning today
        </div>
      </div>

      {/* Right form */}
      <div style={{ padding: '56px', display: 'flex', flexDirection: 'column', justifyContent: 'center', maxWidth: 520, margin: '0 auto', width: '100%' }}>
        <div style={{ marginBottom: 32 }}>
          <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em' }}>Log in</div>
          <h2 style={{ margin: '8px 0 4px', fontWeight: 900, fontSize: 32, letterSpacing: '-0.02em' }}>Welcome back</h2>
          <p style={{ margin: 0, fontSize: 14, color: '#94A3B8' }}>Log in to keep your streak alive 🔥</p>
        </div>

        <div style={{ display: 'flex', padding: 4, background: '#1E293B', borderRadius: 14, border: '1px solid rgba(255,255,255,0.06)', marginBottom: 18 }}>
          {[
            { id: 'parent', label: 'I\'m a Parent', emoji: '👨‍👩‍👦' },
            { id: 'student', label: 'I\'m a Student', emoji: '🎓' },
          ].map(r => (
            <button key={r.id} onClick={() => setRole(r.id)} style={{
              flex: 1, padding: '12px 12px', borderRadius: 10, border: 'none',
              background: role === r.id ? '#4F46E5' : 'transparent',
              color: role === r.id ? '#fff' : '#94A3B8',
              fontFamily: 'inherit', fontWeight: 700, fontSize: 14, cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
              boxShadow: role === r.id ? '0 4px 12px rgba(99,102,241,0.35)' : 'none',
              transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
            }}><span>{r.emoji}</span>{r.label}</button>
          ))}
        </div>

        <WebField label="Email">
          <input type="email" value={email} onChange={e => setEmail(e.target.value)}
            placeholder={role === 'parent' ? 'parent@email.com' : 'sami@learnexia.com'}
            style={webInputStyle()}/>
        </WebField>

        <WebField label="Password" right={
          <button onClick={() => setShowPw(!showPw)} style={textBtn()}>{showPw ? 'Hide' : 'Show'}</button>
        }>
          <input type={showPw ? 'text' : 'password'} value={pw} onChange={e => setPw(e.target.value)}
            placeholder="••••••••" style={webInputStyle()}/>
        </WebField>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', margin: '4px 0 20px' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13, color: '#CBD5E1' }}>
            <input type="checkbox" style={{ accentColor: '#4F46E5' }}/> Remember me
          </label>
          <button style={textBtn()}>Forgot password?</button>
        </div>

        <button onClick={canSubmit ? onLogin : undefined} disabled={!canSubmit} style={{
          ...btnPrimary(), height: 52, fontSize: 16,
          background: canSubmit ? '#4F46E5' : '#2A2D3E',
          color: canSubmit ? '#fff' : '#64748B',
          cursor: canSubmit ? 'pointer' : 'not-allowed',
          boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
        }}>Log in →</button>

        <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '24px 0' }}>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B' }}>OR CONTINUE WITH</div>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
        </div>

        <div style={{ display: 'flex', gap: 12 }}>
          <WebSocialButton provider="google"/>
          <WebSocialButton provider="apple"/>
          <WebSocialButton provider="microsoft"/>
        </div>

        {role === 'parent' ? (
          <div style={{ textAlign: 'center', marginTop: 28, fontSize: 14, color: '#94A3B8' }}>
            New to Learnexia?{' '}
            <button onClick={onRegister} style={{ ...textBtn(), fontSize: 14, fontWeight: 800 }}>Create parent account</button>
          </div>
        ) : (
          <div style={{
            marginTop: 28, padding: '14px 16px', borderRadius: 14,
            background: 'rgba(245,158,11,0.08)', border: '1px solid rgba(245,158,11,0.25)',
            fontSize: 13, color: '#CBD5E1', textAlign: 'center',
          }}>
            <span style={{ color: '#F59E0B', fontWeight: 800 }}>Need an account?</span> Ask a parent to add you — kids can't self-register.
          </div>
        )}
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────── REGISTER (web)
function RegisterWebPage({ onRegister, onLogin, onLanding }) {
  const [name, setName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const [country, setCountry] = React.useState('SA');
  const [agreed, setAgreed] = React.useState(false);
  const canSubmit = name.trim().length > 1 && email.includes('@') && pw.length >= 6 && agreed;

  return (
    <div style={{ minHeight: '100%', display: 'grid', gridTemplateColumns: '1fr 1fr', background: '#0F172A', color: '#F8FAFC', ...webFont }}>
      <div style={{ padding: '56px', display: 'flex', flexDirection: 'column', justifyContent: 'center', maxWidth: 560, margin: '0 auto', width: '100%' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 32, cursor: 'pointer' }} onClick={onLanding}>
          <img src="../../assets/logo-mark.svg" style={{ width: 36, height: 36 }}/>
          <div style={{ fontWeight: 900, fontSize: 20 }}>Learnexia</div>
        </div>

        <div style={{ marginBottom: 28 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em' }}>Step 1 of 2</div>
            <div style={{ flex: 1, height: 4, background: '#1E293B', borderRadius: 9999, overflow: 'hidden' }}>
              <div style={{ height: '100%', width: '50%', background: 'linear-gradient(90deg,#A855F7,#4F46E5)' }}/>
            </div>
          </div>
          <h2 style={{ margin: '16px 0 4px', fontWeight: 900, fontSize: 32, letterSpacing: '-0.02em' }}>Create your parent account</h2>
          <p style={{ margin: 0, fontSize: 14, color: '#94A3B8' }}>You'll add your children's accounts next.</p>
        </div>

        <div style={{
          display: 'flex', alignItems: 'center', gap: 12, marginBottom: 20,
          padding: '12px 16px', borderRadius: 14,
          background: 'rgba(168,85,247,0.1)', border: '1px solid rgba(168,85,247,0.3)',
        }}>
          <span style={{ fontSize: 26 }}>👨‍👩‍👦</span>
          <div>
            <div style={{ fontWeight: 800, fontSize: 13, color: '#A855F7' }}>Parent / Guardian only</div>
            <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>Children can't self-register. You'll create their accounts in the next step.</div>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 14 }}>
          <WebField label="Full name">
            <input value={name} onChange={e => setName(e.target.value)} placeholder="Ahmed Hassan" style={webInputStyle()}/>
          </WebField>
          <WebField label="Country">
            <select value={country} onChange={e => setCountry(e.target.value)} style={{
              ...webInputStyle(), appearance: 'none', cursor: 'pointer',
              backgroundImage: 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'12\' height=\'8\' viewBox=\'0 0 12 8\'><path fill=\'%2394A3B8\' d=\'M6 8L0 0h12z\'/></svg>")',
              backgroundRepeat: 'no-repeat', backgroundPosition: 'right 14px center', paddingRight: 36,
            }}>
              <option value="SA">🇸🇦 Saudi Arabia</option>
              <option value="AE">🇦🇪 UAE</option>
              <option value="EG">🇪🇬 Egypt</option>
              <option value="JO">🇯🇴 Jordan</option>
              <option value="QA">🇶🇦 Qatar</option>
              <option value="KW">🇰🇼 Kuwait</option>
              <option value="US">🇺🇸 United States</option>
              <option value="GB">🇬🇧 United Kingdom</option>
            </select>
          </WebField>
        </div>

        <WebField label="Email">
          <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="parent@email.com" style={webInputStyle()}/>
        </WebField>

        <WebField label="Password" hint="At least 6 characters">
          <input type="password" value={pw} onChange={e => setPw(e.target.value)} placeholder="••••••••" style={webInputStyle()}/>
        </WebField>

        <label style={{
          display: 'flex', alignItems: 'flex-start', gap: 12,
          padding: '14px 16px', borderRadius: 14, marginTop: 8,
          background: agreed ? 'rgba(34,197,94,0.06)' : '#1E293B',
          border: agreed ? '1px solid rgba(34,197,94,0.3)' : '1px solid rgba(255,255,255,0.06)',
          cursor: 'pointer',
        }}>
          <div style={{
            width: 22, height: 22, borderRadius: 6, flexShrink: 0, marginTop: 2,
            background: agreed ? '#22C55E' : 'transparent',
            border: agreed ? 'none' : '2px solid rgba(255,255,255,0.2)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#0F172A', fontWeight: 900, fontSize: 13,
          }}>{agreed && '✓'}</div>
          <div style={{ fontSize: 13, color: '#CBD5E1', lineHeight: 1.5 }}>
            I'm a parent or legal guardian and I agree to the{' '}
            <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Terms</span> and{' '}
            <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Privacy Policy</span>, including consent to create accounts for my children.
          </div>
          <input type="checkbox" checked={agreed} onChange={e => setAgreed(e.target.checked)} style={{ display: 'none' }}/>
        </label>

        <button onClick={canSubmit ? onRegister : undefined} disabled={!canSubmit} style={{
          ...btnPrimary(), height: 52, fontSize: 16, marginTop: 18,
          background: canSubmit ? '#4F46E5' : '#2A2D3E',
          color: canSubmit ? '#fff' : '#64748B',
          cursor: canSubmit ? 'pointer' : 'not-allowed',
          boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
        }}>Continue → Add Children</button>

        <div style={{ textAlign: 'center', marginTop: 20, fontSize: 14, color: '#94A3B8' }}>
          Already have an account?{' '}
          <button onClick={onLogin} style={{ ...textBtn(), fontSize: 14, fontWeight: 800 }}>Log in</button>
        </div>
      </div>

      {/* Right benefits panel */}
      <div style={{
        position: 'relative', overflow: 'hidden',
        background: 'linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)',
        padding: 56, display: 'flex', flexDirection: 'column', justifyContent: 'center', gap: 28,
      }}>
        <div style={{ fontSize: 96, lineHeight: 1, filter: 'drop-shadow(0 0 24px rgba(250,204,21,0.5))' }}>🎮</div>
        <h2 style={{ margin: 0, fontWeight: 900, fontSize: 40, lineHeight: 1.15, letterSpacing: '-0.02em', maxWidth: 460 }}>
          Set up once. Watch them learn forever.
        </h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14, maxWidth: 460 }}>
          {[
            ['✨', 'AI-powered explanations tailored to each child\'s grade'],
            ['📊', 'Weekly reports show exactly what they\'ve mastered'],
            ['🎯', 'Daily missions keep them coming back without nagging'],
            ['🛡️', 'COPPA-compliant — no ads, no DMs, no data resold'],
          ].map(([emoji, text], i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 14, fontSize: 15, color: 'rgba(255,255,255,0.92)' }}>
              <div style={{
                width: 40, height: 40, borderRadius: 12,
                background: 'rgba(255,255,255,0.1)', backdropFilter: 'blur(8px)',
                display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20, flexShrink: 0,
              }}>{emoji}</div>
              {text}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────────────────── helpers
function WebField({ label, right, hint, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 14 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#CBD5E1', letterSpacing: '0.04em' }}>{label}</div>
        {right}
      </div>
      {children}
      {hint && <div style={{ fontSize: 11, color: '#94A3B8' }}>{hint}</div>}
    </div>
  );
}
function webInputStyle() {
  return {
    height: 48, background: '#1E293B', border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 14, color: '#F8FAFC', fontFamily: 'Poppins, system-ui, sans-serif',
    fontSize: 15, fontWeight: 500, padding: '0 14px', width: '100%', outline: 'none',
  };
}
function btnPrimary() {
  return {
    height: 40, padding: '0 18px', borderRadius: 12, border: 'none',
    background: '#4F46E5', color: '#fff',
    fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 700, fontSize: 14,
    cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
    boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)',
  };
}
function btnGhost() {
  return {
    height: 40, padding: '0 18px', borderRadius: 12,
    background: 'transparent', color: '#CBD5E1',
    border: '1px solid rgba(255,255,255,0.12)',
    fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 600, fontSize: 14,
    cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
  };
}
function textBtn() {
  return {
    background: 'transparent', border: 'none', color: '#A5B4FC',
    fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 600, fontSize: 12,
    cursor: 'pointer', padding: 0,
  };
}
function WebSocialButton({ provider }) {
  const data = {
    google:    { icon: 'G', label: 'Google',    bg: '#fff', fg: '#0F172A' },
    apple:     { icon: '🍎', label: 'Apple',     bg: '#1E293B', fg: '#F8FAFC' },
    microsoft: { icon: '⊞', label: 'Microsoft', bg: '#1E293B', fg: '#F8FAFC' },
  }[provider];
  return (
    <button style={{
      flex: 1, height: 48, borderRadius: 14,
      background: '#1E293B', border: '1px solid rgba(255,255,255,0.08)',
      color: '#F8FAFC', fontFamily: 'Poppins, system-ui, sans-serif', fontWeight: 700, fontSize: 13,
      cursor: 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
    }}>
      <span style={{
        width: 22, height: 22, borderRadius: '50%',
        background: data.bg, color: data.fg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontWeight: 900, fontSize: 13,
      }}>{data.icon}</span>
      {data.label}
    </button>
  );
}

Object.assign(window, {
  LandingPage, LoginWebPage, RegisterWebPage,
  WebField, webInputStyle, btnPrimary, btnGhost, textBtn, WebSocialButton, Feature,
});
