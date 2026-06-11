// Learnexia — Auth + Parent My Children screens.
// Per spec (P1-03, P1-04): parent registers → adds children with assigned login email,
// grade (1-6), language (AR/EN), country. Children only log in, never self-register.

const authFont = { fontFamily: 'Poppins, system-ui, sans-serif' };

// ───────────────────────────────────────────── LOGIN
function LoginScreen({ onLogin, onRegister }) {
  const [role, setRole] = React.useState('parent'); // 'parent' | 'student'
  const [showPw, setShowPw] = React.useState(false);
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const canSubmit = email.includes('@') && pw.length >= 4;

  return (
    <div style={{
      width: '100%', height: '100%', position: 'relative',
      background: '#0F172A',
      ...authFont, overflow: 'auto',
    }}>
      {/* soft top glow */}
      <div style={{
        position: 'absolute', top: -180, left: '50%', transform: 'translateX(-50%)',
        width: 460, height: 360, borderRadius: '50%',
        background: 'radial-gradient(circle, rgba(168,85,247,0.35) 0%, rgba(168,85,247,0) 65%)',
        pointerEvents: 'none',
      }}/>

      <div style={{ position: 'relative', padding: '70px 24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
        {/* Brand */}
        <div style={{ textAlign: 'center', marginBottom: 8 }}>
          <div style={{
            width: 72, height: 72, borderRadius: 22, margin: '0 auto 12px',
            background: 'linear-gradient(135deg,#A855F7,#6366F1)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 12px 32px rgba(168,85,247,0.4), inset 0 2px 0 rgba(255,255,255,0.18)',
          }}>
            <span style={{ fontSize: 36, filter: 'drop-shadow(0 0 8px rgba(250,204,21,0.6))' }}>🌟</span>
          </div>
          <div style={{ fontWeight: 900, fontSize: 28, color: '#F8FAFC', letterSpacing: '-0.02em' }}>Welcome back</div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6 }}>Log in to keep your streak alive 🔥</div>
        </div>

        {/* Role toggle */}
        <div style={{
          display: 'flex', padding: 4, background: '#15161D', borderRadius: 14,
          border: '1px solid rgba(255,255,255,0.06)',
        }}>
          {[
            { id: 'parent',  label: 'Parent',  emoji: '👨‍👩‍👦' },
            { id: 'student', label: 'Student', emoji: '🎓' },
          ].map(r => (
            <button key={r.id} onClick={() => setRole(r.id)} style={{
              flex: 1, padding: '10px 12px', borderRadius: 10, border: 'none',
              background: role === r.id ? '#4F46E5' : 'transparent',
              color: role === r.id ? '#fff' : '#94A3B8',
              fontFamily: 'inherit', fontWeight: 700, fontSize: 14, cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
              boxShadow: role === r.id ? '0 4px 12px rgba(99,102,241,0.35)' : 'none',
              transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
            }}>
              <span>{r.emoji}</span>{r.label}
            </button>
          ))}
        </div>

        {/* Email */}
        <AuthField label="Email" icon="✉️">
          <input
            type="email"
            placeholder={role === 'parent' ? 'parent@email.com' : 'sami@learnexia.com'}
            value={email}
            onChange={e => setEmail(e.target.value)}
            style={authInputStyle()}
          />
        </AuthField>

        {/* Password */}
        <AuthField label="Password" icon="🔒"
          right={
            <button onClick={() => setShowPw(!showPw)} style={{
              background: 'transparent', border: 'none', color: '#A5B4FC',
              fontFamily: 'inherit', fontWeight: 600, fontSize: 12, cursor: 'pointer', padding: 0,
            }}>{showPw ? 'Hide' : 'Show'}</button>
          }>
          <input
            type={showPw ? 'text' : 'password'}
            placeholder="••••••••"
            value={pw}
            onChange={e => setPw(e.target.value)}
            style={authInputStyle()}
          />
        </AuthField>

        {/* Forgot */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: -8 }}>
          <button style={{
            background: 'transparent', border: 'none', color: '#A5B4FC',
            fontFamily: 'inherit', fontWeight: 600, fontSize: 13, cursor: 'pointer', padding: 4,
          }}>Forgot password?</button>
        </div>

        {/* Submit */}
        <button onClick={canSubmit ? onLogin : undefined} disabled={!canSubmit} style={{
          height: 56, borderRadius: 16, border: 'none',
          background: canSubmit ? '#4F46E5' : '#2A2D3E',
          color: canSubmit ? '#fff' : '#64748B',
          fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
          cursor: canSubmit ? 'pointer' : 'not-allowed',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
          boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
          transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
        }}>
          Log In →
        </button>

        {/* Divider */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '4px 0' }}>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B' }}>OR</div>
          <div style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.08)' }}/>
        </div>

        {/* Social */}
        <div style={{ display: 'flex', gap: 10 }}>
          <SocialButton label="Google" icon="G"/>
          <SocialButton label="Apple"  icon="" appleIcon/>
        </div>

        {/* Register prompt — parent only */}
        {role === 'parent' && (
          <div style={{
            textAlign: 'center', fontSize: 14, color: '#94A3B8', marginTop: 8,
          }}>
            New here?{' '}
            <button onClick={onRegister} style={{
              background: 'transparent', border: 'none', color: '#A5B4FC',
              fontFamily: 'inherit', fontWeight: 800, fontSize: 14, cursor: 'pointer', padding: 0,
            }}>Create parent account</button>
          </div>
        )}
        {role === 'student' && (
          <div style={{
            textAlign: 'center', fontSize: 13, color: '#64748B', marginTop: 8,
            padding: '12px 14px', borderRadius: 12,
            background: 'rgba(245,158,11,0.06)', border: '1px solid rgba(245,158,11,0.18)',
          }}>
            <span style={{ color: '#F59E0B', fontWeight: 700 }}>Need an account?</span> Ask a parent to add you.
          </div>
        )}
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── REGISTER (parent-only)
function RegisterScreen({ onRegister, onLogin }) {
  const [name, setName]   = React.useState('');
  const [email, setEmail] = React.useState('');
  const [pw, setPw]       = React.useState('');
  const [country, setCountry] = React.useState('SA');
  const [agreed, setAgreed]   = React.useState(false);
  const pwStrength = scorePw(pw);
  const canSubmit = name.trim().length > 1 && email.includes('@') && pw.length >= 6 && agreed;

  return (
    <div style={{ width: '100%', height: '100%', background: '#0F172A', ...authFont, overflow: 'auto' }}>
      <div style={{ padding: '70px 24px 32px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div>
          <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em' }}>Step 1 of 2</div>
          <div style={{ fontWeight: 900, fontSize: 28, color: '#F8FAFC', marginTop: 6, letterSpacing: '-0.01em' }}>
            Create parent account
          </div>
          <div style={{ fontSize: 14, color: '#94A3B8', marginTop: 6, lineHeight: 1.5 }}>
            You'll add your children's accounts in the next step.
          </div>
        </div>

        {/* Parent badge */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 12,
          padding: '12px 14px', borderRadius: 14,
          background: 'rgba(168,85,247,0.1)', border: '1px solid rgba(168,85,247,0.3)',
        }}>
          <span style={{ fontSize: 26 }}>👨‍👩‍👦</span>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: 13, color: '#A855F7' }}>Parent / Guardian</div>
            <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 2 }}>Only parents can register. Kids get accounts added by you.</div>
          </div>
        </div>

        {/* Form */}
        <AuthField label="Full name" icon="👤">
          <input value={name} onChange={e => setName(e.target.value)}
            placeholder="e.g. Ahmed Hassan" style={authInputStyle()}/>
        </AuthField>

        <AuthField label="Email" icon="✉️">
          <input type="email" value={email} onChange={e => setEmail(e.target.value)}
            placeholder="parent@email.com" style={authInputStyle()}/>
        </AuthField>

        <AuthField label="Password" icon="🔒">
          <input type="password" value={pw} onChange={e => setPw(e.target.value)}
            placeholder="At least 6 characters" style={authInputStyle()}/>
          {pw.length > 0 && (
            <div style={{ display: 'flex', gap: 4, marginTop: 8 }}>
              {[0, 1, 2, 3].map(i => (
                <div key={i} style={{
                  flex: 1, height: 4, borderRadius: 9999,
                  background: i < pwStrength.score
                    ? pwStrength.color : 'rgba(255,255,255,0.08)',
                  transition: 'background 200ms',
                }}/>
              ))}
              <div style={{ fontSize: 11, fontWeight: 700, color: pwStrength.color, minWidth: 50, textAlign: 'right' }}>
                {pwStrength.label}
              </div>
            </div>
          )}
        </AuthField>

        <AuthField label="Country" icon="🌍">
          <select value={country} onChange={e => setCountry(e.target.value)} style={{
            ...authInputStyle(), appearance: 'none',
            backgroundImage: 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'12\' height=\'8\' viewBox=\'0 0 12 8\'><path fill=\'%2394A3B8\' d=\'M6 8L0 0h12z\'/></svg>")',
            backgroundRepeat: 'no-repeat', backgroundPosition: 'right 14px center',
            paddingRight: 36, cursor: 'pointer',
          }}>
            <option value="SA">🇸🇦 Saudi Arabia</option>
            <option value="AE">🇦🇪 United Arab Emirates</option>
            <option value="EG">🇪🇬 Egypt</option>
            <option value="JO">🇯🇴 Jordan</option>
            <option value="QA">🇶🇦 Qatar</option>
            <option value="KW">🇰🇼 Kuwait</option>
            <option value="OM">🇴🇲 Oman</option>
            <option value="BH">🇧🇭 Bahrain</option>
            <option value="US">🇺🇸 United States</option>
            <option value="GB">🇬🇧 United Kingdom</option>
          </select>
        </AuthField>

        {/* Consent */}
        <label style={{
          display: 'flex', alignItems: 'flex-start', gap: 12,
          padding: '12px 14px', borderRadius: 14,
          background: agreed ? 'rgba(34,197,94,0.06)' : '#15161D',
          border: agreed ? '1px solid rgba(34,197,94,0.25)' : '1px solid rgba(255,255,255,0.06)',
          cursor: 'pointer', transition: 'all 180ms',
        }}>
          <div style={{
            width: 24, height: 24, borderRadius: 7, flexShrink: 0, marginTop: 2,
            background: agreed ? '#22C55E' : 'transparent',
            border: agreed ? 'none' : '2px solid rgba(255,255,255,0.2)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#0F172A', fontWeight: 900, fontSize: 14,
          }}>{agreed && '✓'}</div>
          <div style={{ flex: 1, fontSize: 13, color: '#CBD5E1', lineHeight: 1.5 }}>
            I'm a parent or legal guardian and I agree to the{' '}
            <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Terms</span> and{' '}
            <span style={{ color: '#A5B4FC', fontWeight: 700 }}>Privacy Policy</span>, including consent to create accounts for my children.
          </div>
          <input type="checkbox" checked={agreed} onChange={e => setAgreed(e.target.checked)} style={{ display: 'none' }}/>
        </label>

        {/* Submit */}
        <button onClick={canSubmit ? onRegister : undefined} disabled={!canSubmit} style={{
          height: 56, borderRadius: 16, border: 'none',
          background: canSubmit ? '#4F46E5' : '#2A2D3E',
          color: canSubmit ? '#fff' : '#64748B',
          fontFamily: 'inherit', fontWeight: 800, fontSize: 17,
          cursor: canSubmit ? 'pointer' : 'not-allowed',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
          boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
          marginTop: 4,
        }}>
          Continue → Add Children
        </button>

        <div style={{ textAlign: 'center', fontSize: 14, color: '#94A3B8' }}>
          Already have an account?{' '}
          <button onClick={onLogin} style={{
            background: 'transparent', border: 'none', color: '#A5B4FC',
            fontFamily: 'inherit', fontWeight: 800, fontSize: 14, cursor: 'pointer', padding: 0,
          }}>Log in</button>
        </div>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────── MY CHILDREN (parent)
function MyChildrenScreen({ onAddChild, onPick }) {
  const children = [
    {
      id: 1, name: 'Sami',   initial: 'S', color: '#FB923C',
      grade: 3, language: 'en', country: 'SA',
      email: 'sami@learnexia.com',
      level: 12, xp: 1240, streak: 7, status: 'active',
    },
    {
      id: 2, name: 'Layla',  initial: 'L', color: '#A855F7',
      grade: 1, language: 'ar', country: 'SA',
      email: 'layla@learnexia.com',
      level: 4, xp: 380, streak: 2, status: 'active',
    },
  ];

  return (
    <ScreenShell padTop={56}>
      <div style={{ padding: '0 16px 16px', display: 'flex', flexDirection: 'column', gap: 18, ...authFont }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}>
          <div>
            <div style={{ fontSize: 12, color: '#A5B4FC', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em' }}>
              👨‍👩‍👦 Parent · Ahmed
            </div>
            <div style={{ fontWeight: 900, fontSize: 26, color: '#F8FAFC', marginTop: 4 }}>
              My Children
            </div>
            <div style={{ fontSize: 13, color: '#94A3B8', marginTop: 4 }}>
              {children.length} {children.length === 1 ? 'child' : 'children'} linked to your account
            </div>
          </div>
          <button onClick={onAddChild} style={{
            width: 44, height: 44, borderRadius: 14, border: 'none',
            background: '#4F46E5', color: '#fff',
            fontFamily: 'inherit', fontWeight: 800, fontSize: 22,
            cursor: 'pointer', flexShrink: 0,
            boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)',
          }}>+</button>
        </div>

        {/* Combined weekly summary */}
        <div style={{
          background: 'linear-gradient(135deg,#A855F7,#6366F1)',
          borderRadius: 20, padding: 18,
          boxShadow: '0 8px 24px rgba(99,102,241,0.35)',
          color: '#fff',
        }}>
          <div style={{
            fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em',
            opacity: 0.85, marginBottom: 8,
          }}>This Week · All Children</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <SummaryStat label="Total XP" value="820" icon="⭐"/>
            <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.2)' }}/>
            <SummaryStat label="Lessons"  value="18"  icon="📚"/>
            <div style={{ width: 1, alignSelf: 'stretch', background: 'rgba(255,255,255,0.2)' }}/>
            <SummaryStat label="Active"   value="6/7" icon="🔥"/>
          </div>
        </div>

        {/* Children */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {children.map(c => <ChildCard key={c.id} child={c} onClick={() => onPick(c)}/>)}
        </div>

        {/* Add child CTA */}
        <button onClick={onAddChild} style={{
          padding: 20, borderRadius: 20,
          background: 'transparent', border: '2px dashed rgba(99,102,241,0.4)',
          display: 'flex', alignItems: 'center', gap: 14,
          cursor: 'pointer', fontFamily: 'inherit', color: '#A5B4FC',
          transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
        }}
        onPointerOver={e => { e.currentTarget.style.background = 'rgba(79,70,229,0.08)'; e.currentTarget.style.borderColor = '#4F46E5'; }}
        onPointerOut ={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'rgba(99,102,241,0.4)'; }}>
          <div style={{
            width: 48, height: 48, borderRadius: 14,
            background: 'rgba(79,70,229,0.18)', color: '#A5B4FC',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 26, fontWeight: 800, flexShrink: 0,
          }}>+</div>
          <div style={{ flex: 1, textAlign: 'left' }}>
            <div style={{ fontWeight: 800, fontSize: 16, color: '#F8FAFC' }}>Add a child</div>
            <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>Set their grade, language, and login email</div>
          </div>
        </button>

        {/* Footer info */}
        <div style={{
          display: 'flex', alignItems: 'flex-start', gap: 10,
          padding: '12px 14px', borderRadius: 14,
          background: '#15161D', border: '1px solid rgba(255,255,255,0.05)',
          fontSize: 12, color: '#94A3B8', lineHeight: 1.5,
        }}>
          <span style={{ fontSize: 16 }}>🔒</span>
          <span>
            You can see only your own children's progress. Each child logs in with the email you assigned.
          </span>
        </div>
      </div>
    </ScreenShell>
  );
}

function ChildCard({ child, onClick }) {
  const langLabel = { en: '🇬🇧 English', ar: '🇸🇦 العربية' }[child.language] || child.language;
  return (
    <button onClick={onClick} style={{
      background: '#15161D', borderRadius: 20, padding: 16,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex', flexDirection: 'column', gap: 14,
      cursor: 'pointer', fontFamily: 'inherit', color: '#F8FAFC',
      width: '100%', textAlign: 'left',
    }}>
      {/* row 1 */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 52, height: 52, borderRadius: '50%',
          background: child.color, color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontWeight: 900, fontSize: 22,
          boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.18), 0 4px 12px rgba(0,0,0,0.2)',
          flexShrink: 0,
        }}>{child.initial}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 900, fontSize: 18, color: '#F8FAFC' }}>{child.name}</span>
            <span style={{
              padding: '2px 8px', borderRadius: 9999,
              background: 'rgba(79,70,229,0.18)', color: '#A5B4FC',
              fontWeight: 800, fontSize: 11, letterSpacing: '0.04em',
            }}>Grade {child.grade}</span>
          </div>
          <div style={{
            fontSize: 12, color: '#94A3B8', marginTop: 4,
            overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
          }}>{child.email}</div>
        </div>
        <div style={{ color: '#94A3B8', fontSize: 22 }}>›</div>
      </div>

      {/* row 2 — stats */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
        <ChildStat icon="🧠" value={`Lv ${child.level}`} color="#A855F7"/>
        <ChildStat icon="⭐" value={child.xp.toLocaleString()} color="#FACC15"/>
        <ChildStat icon="🔥" value={`${child.streak}d`} color="#FB923C"/>
        <div style={{
          marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 4,
          fontSize: 11, color: '#94A3B8', fontWeight: 600,
        }}>
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: child.status === 'active' ? '#22C55E' : '#64748B',
            boxShadow: child.status === 'active' ? '0 0 6px rgba(34,197,94,0.6)' : 'none',
          }}/>
          {child.status === 'active' ? 'Active today' : 'Inactive'}
        </div>
      </div>

      {/* row 3 — language footer */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        paddingTop: 12, borderTop: '1px solid rgba(255,255,255,0.05)',
        fontSize: 12, color: '#CBD5E1',
      }}>
        <span><span style={{ color: '#94A3B8' }}>Language:</span> {langLabel}</span>
        <span style={{ color: '#A5B4FC', fontWeight: 700 }}>View progress →</span>
      </div>
    </button>
  );
}

function ChildStat({ icon, value, color }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
      <span style={{ fontSize: 14 }}>{icon}</span>
      <span style={{ fontWeight: 800, fontSize: 13, color, fontVariantNumeric: 'tabular-nums' }}>{value}</span>
    </div>
  );
}

function SummaryStat({ label, value, icon }) {
  return (
    <div style={{ flex: 1, textAlign: 'center' }}>
      <div style={{ fontSize: 16, marginBottom: 2 }}>{icon}</div>
      <div style={{ fontWeight: 900, fontSize: 20, fontVariantNumeric: 'tabular-nums', color: '#fff', lineHeight: 1 }}>{value}</div>
      <div style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em', opacity: 0.85, marginTop: 3 }}>{label}</div>
    </div>
  );
}

// ───────────────────────────────────────────── Shared form bits
function AuthField({ label, icon, right, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{
          fontSize: 12, fontWeight: 700, color: '#CBD5E1',
          letterSpacing: '0.04em', display: 'flex', alignItems: 'center', gap: 6,
        }}>
          {icon && <span style={{ fontSize: 13 }}>{icon}</span>}{label}
        </div>
        {right}
      </div>
      {children}
    </div>
  );
}

function authInputStyle() {
  return {
    height: 50, background: '#15161D',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 14, color: '#F8FAFC',
    fontFamily: 'Poppins, system-ui, sans-serif', fontSize: 15, fontWeight: 500,
    padding: '0 14px', width: '100%', outline: 'none',
    transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
  };
}

function SocialButton({ label, icon, appleIcon }) {
  return (
    <button style={{
      flex: 1, height: 48, borderRadius: 14,
      background: '#15161D', border: '1px solid rgba(255,255,255,0.08)',
      color: '#F8FAFC', fontFamily: 'inherit', fontWeight: 700, fontSize: 14,
      cursor: 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
    }}>
      {appleIcon
        ? <svg width="18" height="18" viewBox="0 0 24 24" fill="#F8FAFC"><path d="M17.05 20.28c-.98.95-2.05.86-3.08.43-1.09-.46-2.09-.48-3.24 0-1.44.62-2.2.44-3.06-.43C2.79 15.25 3.51 7.59 9.05 7.31c1.35.07 2.29.74 3.08.8 1.18-.24 2.31-.93 3.57-.84 1.51.12 2.65.72 3.4 1.8-3.12 1.87-2.38 5.98.48 7.13-.57 1.5-1.31 2.99-2.54 4.08zM12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25z"/></svg>
        : <span style={{
            width: 20, height: 20, borderRadius: '50%',
            background: '#fff', color: '#0F172A',
            fontWeight: 900, fontSize: 12,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>{icon}</span>}
      {label}
    </button>
  );
}

function scorePw(pw) {
  let s = 0;
  if (pw.length >= 6)  s++;
  if (pw.length >= 10) s++;
  if (/[A-Z]/.test(pw) && /[a-z]/.test(pw)) s++;
  if (/[0-9]/.test(pw) || /[^A-Za-z0-9]/.test(pw)) s++;
  const labels = ['Weak', 'Fair', 'Good', 'Strong', 'Strong'];
  const colors = ['#EF4444', '#F59E0B', '#FACC15', '#22C55E', '#22C55E'];
  return { score: s, label: labels[s], color: colors[s] };
}

Object.assign(window, {
  LoginScreen, RegisterScreen, MyChildrenScreen, ChildCard, AddChildSheet,
});

// ───────────────────────────────────────────── ADD CHILD (mobile bottom sheet)
function AddChildSheet({ open, onClose }) {
  const [photo, setPhoto] = React.useState(null);
  const [color, setColor] = React.useState('#A855F7');
  const [name, setName] = React.useState('Layla');
  const [grade, setGrade] = React.useState(1);
  const [lang, setLang] = React.useState('ar');
  const fileRef = React.useRef(null);
  if (!open) return null;
  const onFile = (e) => { const f = e.target.files && e.target.files[0]; if (f) setPhoto(URL.createObjectURL(f)); };
  const fld = (label, child) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, flex: 1 }}>
      <label style={{ fontFamily: 'Poppins, sans-serif', fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{label}</label>
      {child}
    </div>
  );
  const inp = { height: 50, background: '#0B0C12', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, color: '#F8FAFC', fontFamily: 'Poppins, sans-serif', fontSize: 15, padding: '0 14px', width: '100%', outline: 'none', boxSizing: 'border-box' };
  return (
    <div onClick={onClose} style={{ position: 'absolute', inset: 0, zIndex: 200, background: 'rgba(5,8,22,0.7)', display: 'flex', alignItems: 'flex-end', justifyContent: 'center', fontFamily: 'Poppins, sans-serif' }}>
      <div onClick={e => e.stopPropagation()} style={{ width: '100%', background: '#15161D', borderRadius: '28px 28px 0 0', border: '1px solid rgba(255,255,255,0.08)', borderBottom: 'none', boxShadow: '0 -24px 64px rgba(0,0,0,0.55)', padding: '12px 20px 32px', animation: 'lxsheet 320ms cubic-bezier(0.16,1,0.3,1)', maxHeight: '88%', overflowY: 'auto' }}>
        <div style={{ width: 40, height: 5, borderRadius: 9999, background: 'rgba(255,255,255,0.18)', margin: '4px auto 18px' }}/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 20 }}>
          <div style={{ width: 48, height: 48, borderRadius: 14, background: 'linear-gradient(135deg,#A855F7,#6366F1)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24, boxShadow: '0 6px 16px rgba(99,102,241,0.4)' }}>👶</div>
          <div><div style={{ fontWeight: 800, fontSize: 20, color: '#F8FAFC' }}>Add a child</div><div style={{ fontSize: 13, color: '#94A3B8', marginTop: 2 }}>They'll log in with the email you set</div></div>
        </div>
        <input ref={fileRef} type="file" accept="image/*" onChange={onFile} style={{ display: 'none' }}/>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ width: 64, height: 64, borderRadius: '50%', background: photo ? `url(${photo}) center/cover` : color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, fontSize: 26, color: '#fff', flexShrink: 0, position: 'relative', boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.2)' }}>{!photo && (name.trim()[0] || 'L').toUpperCase()}<div style={{ position: 'absolute', bottom: -2, right: -2, width: 24, height: 24, borderRadius: '50%', background: '#4F46E5', border: '2px solid #15161D', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11 }}>📷</div></div>
            <div onClick={() => fileRef.current && fileRef.current.click()} style={{ flex: 1, border: '1.5px dashed rgba(99,102,241,0.45)', borderRadius: 14, padding: '12px 14px', display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', background: 'rgba(79,70,229,0.05)' }}>
              <span style={{ fontSize: 20 }}>⬆️</span><div><div style={{ fontWeight: 700, fontSize: 13, color: '#A5B4FC' }}>{photo ? 'Change photo' : 'Upload a photo'}</div><div style={{ fontSize: 11, color: '#94A3B8', marginTop: 1 }}>PNG or JPG · or pick a color below</div></div>
            </div>
          </div>
          {fld("Child's name", <input value={name} onChange={e => setName(e.target.value)} style={inp}/>)}
          {fld('Login email', <input defaultValue="layla@learnexia.com" style={{ ...inp, borderColor: '#4F46E5', boxShadow: '0 0 0 3px rgba(99,102,241,0.2)' }}/>)}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>Grade</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 6 }}>
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
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>…or pick an avatar color</label>
            <div style={{ display: 'flex', gap: 12 }}>
              {['#FB923C','#A855F7','#22C55E','#38BDF8','#FB7185'].map(c => (
                <div key={c} onClick={() => { setColor(c); setPhoto(null); }} style={{ width: 40, height: 40, borderRadius: '50%', background: c, cursor: 'pointer', boxShadow: color === c && !photo ? `0 0 0 3px #15161D, 0 0 0 5px ${c}` : 'none' }}/>
              ))}
            </div>
          </div>
          <button onClick={onClose} style={{ height: 56, borderRadius: 16, background: '#4F46E5', border: 'none', color: '#fff', fontWeight: 800, fontSize: 17, cursor: 'pointer', boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)', marginTop: 4 }}>Add {name.trim() || 'child'} →</button>
        </div>
      </div>
    </div>
  );
}
