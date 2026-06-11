// Learnexia — functional Add Child modal (web) with photo upload.
// Used by the parent web app. English + Arabic via the `ar` prop.

function AddChildModal({ open, onClose, ar = false }) {
  const [photo, setPhoto] = React.useState(null);
  const [color, setColor] = React.useState('#A855F7');
  const [name, setName] = React.useState(ar ? 'ليلى' : 'Layla');
  const [grade, setGrade] = React.useState(1);
  const [lang, setLang] = React.useState('ar');
  const fileRef = React.useRef(null);

  if (!open) return null;

  const t = ar ? {
    title: 'أضف طفلاً', sub: 'سيسجّل الدخول بالبريد الذي تحدّده',
    upload: 'ارفع صورة', uploadSub: 'PNG أو JPG · أو اختر لوناً بالأسفل',
    change: 'تغيير الصورة', name: 'اسم الطفل', email: 'بريد الدخول',
    grade: 'الصف', lang: 'اللغة', colorLbl: '…أو اختر لون الصورة الرمزية',
    cancel: 'إلغاء', add: 'أضف', grades: ['الصف الأول','الصف الثاني','الصف الثالث'],
    langs: ['🇸🇦 العربية','🇬🇧 الإنجليزية'],
  } : {
    title: 'Add a child', sub: "They'll log in with the email you set",
    upload: 'Upload a photo', uploadSub: 'PNG or JPG · or pick a color below',
    change: 'Change photo', name: "Child's name", email: 'Login email',
    grade: 'Grade', lang: 'Language', colorLbl: '…or pick an avatar color',
    cancel: 'Cancel', add: 'Add', grades: ['Grade 1','Grade 2','Grade 3'],
    langs: ['🇸🇦 العربية','🇬🇧 English'],
  };

  const onFile = (e) => {
    const f = e.target.files && e.target.files[0];
    if (f) setPhoto(URL.createObjectURL(f));
  };

  const fontD = ar ? "'Cairo', sans-serif" : "var(--lx-font-display)";
  const initial = (name.trim()[0] || (ar ? 'ط' : 'L')).toUpperCase();

  return (
    <div onClick={onClose} style={{
      position: 'fixed', inset: 0, zIndex: 100,
      background: 'rgba(5,8,22,0.7)', backdropFilter: 'blur(4px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      direction: ar ? 'rtl' : 'ltr',
      fontFamily: ar ? "'Tajawal', sans-serif" : 'var(--lx-font-body)',
    }}>
      <div onClick={e => e.stopPropagation()} style={{
        width: 480, maxWidth: '92vw', background: '#15161D', borderRadius: 24,
        border: '1px solid rgba(255,255,255,0.08)',
        boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.06)',
        overflow: 'hidden',
        animation: 'acm-pop 280ms cubic-bezier(0.34,1.56,0.64,1)',
      }}>
        {/* header */}
        <div style={{ padding: '22px 24px 16px', display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{ width: 44, height: 44, borderRadius: 14, background: 'linear-gradient(135deg,#A855F7,#6366F1)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22, boxShadow: '0 6px 16px rgba(99,102,241,0.4)' }}>👶</div>
            <div><div style={{ fontFamily: fontD, fontWeight: 800, fontSize: 18, color: '#F8FAFC' }}>{t.title}</div><div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>{t.sub}</div></div>
          </div>
          <button onClick={onClose} style={{ width: 32, height: 32, borderRadius: 10, background: 'rgba(255,255,255,0.05)', border: 'none', color: '#94A3B8', fontSize: 18, cursor: 'pointer' }}>✕</button>
        </div>
        {/* body */}
        <div style={{ padding: '20px 24px', display: 'flex', flexDirection: 'column', gap: 16, maxHeight: '60vh', overflowY: 'auto' }}>
          <input ref={fileRef} type="file" accept="image/*" onChange={onFile} style={{ display: 'none' }} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ width: 64, height: 64, borderRadius: '50%', background: photo ? `url(${photo}) center/cover` : color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: fontD, fontWeight: 900, fontSize: 26, color: '#fff', flexShrink: 0, position: 'relative', boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.2)' }}>
              {!photo && initial}
              <div style={{ position: 'absolute', bottom: -2, [ar ? 'left' : 'right']: -2, width: 24, height: 24, borderRadius: '50%', background: '#4F46E5', border: '2px solid #15161D', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11 }}>📷</div>
            </div>
            <div onClick={() => fileRef.current && fileRef.current.click()} style={{ flex: 1, border: '1.5px dashed rgba(99,102,241,0.45)', borderRadius: 14, padding: '12px 14px', display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', background: 'rgba(79,70,229,0.05)' }}>
              <span style={{ fontSize: 20 }}>⬆️</span>
              <div><div style={{ fontFamily: fontD, fontWeight: 700, fontSize: 13, color: '#A5B4FC' }}>{photo ? t.change : t.upload}</div><div style={{ fontSize: 11, color: '#94A3B8', marginTop: 1 }}>{t.uploadSub}</div></div>
            </div>
          </div>
          <Field label={t.name} fontD={fontD}><input value={name} onChange={e => setName(e.target.value)} style={acmInput()} /></Field>
          <Field label={t.email} fontD={fontD}><input defaultValue="layla@learnexia.com" dir="ltr" style={{ ...acmInput(), borderColor: '#4F46E5', boxShadow: '0 0 0 3px rgba(99,102,241,0.2)' }} /></Field>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontFamily: fontD, fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{t.grade}</label>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 6 }}>
              {[1,2,3,4,5,6].map(g => {
                const on = grade === g;
                return (
                  <div key={g} onClick={() => setGrade(g)} style={{
                    height: 46, borderRadius: 12, cursor: 'pointer',
                    display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 1,
                    background: on ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#0B0C12',
                    border: on ? 'none' : '1px solid rgba(255,255,255,0.1)',
                    boxShadow: on ? '0 4px 12px rgba(99,102,241,0.4)' : 'none',
                  }}>
                    <span style={{ fontSize: 15 }}>{['🌱','🌿','🌳','🌲','🍃','🌴'][g-1]}</span>
                    <span style={{ fontFamily: fontD, fontWeight: 800, fontSize: 11, color: on ? '#fff' : '#94A3B8' }}>{ar ? ['١','٢','٣','٤','٥','٦'][g-1] : g}</span>
                  </div>
                );
              })}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontFamily: fontD, fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{t.lang}</label>
            <div style={{ display: 'flex', gap: 8 }}>
              {[{ id: 'ar', flag: '🇪🇬', label: 'AR' }, { id: 'en', flag: '🇺🇸', label: 'EN' }].map(l => {
                const on = lang === l.id;
                return (
                  <div key={l.id} onClick={() => setLang(l.id)} style={{
                    flex: 1, height: 48, borderRadius: 12, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
                    background: on ? 'rgba(79,70,229,0.18)' : '#0B0C12',
                    border: on ? '1.5px solid #4F46E5' : '1px solid rgba(255,255,255,0.1)',
                  }}>
                    <span style={{ fontSize: 18 }}>{l.flag}</span>
                    <span style={{ fontFamily: fontD, fontWeight: 700, fontSize: 14, color: on ? '#F8FAFC' : '#94A3B8' }}>{l.label}</span>
                  </div>
                );
              })}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontFamily: fontD, fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{t.colorLbl}</label>
            <div style={{ display: 'flex', gap: 10 }}>
              {['#FB923C','#A855F7','#22C55E','#38BDF8','#FB7185'].map(c => (
                <div key={c} onClick={() => { setColor(c); setPhoto(null); }} style={{ width: 36, height: 36, borderRadius: '50%', background: c, cursor: 'pointer', boxShadow: color === c && !photo ? `0 0 0 3px #15161D, 0 0 0 5px ${c}` : 'none' }} />
              ))}
            </div>
          </div>
        </div>
        {/* footer */}
        <div style={{ padding: '16px 24px 22px', display: 'flex', gap: 10, borderTop: '1px solid rgba(255,255,255,0.05)' }}>
          <button onClick={onClose} style={{ flex: 1, height: 48, borderRadius: 14, background: 'transparent', border: '1px solid rgba(255,255,255,0.12)', color: '#CBD5E1', fontFamily: fontD, fontWeight: 700, fontSize: 15, cursor: 'pointer' }}>{t.cancel}</button>
          <button onClick={onClose} style={{ flex: 2, height: 48, borderRadius: 14, background: '#4F46E5', border: 'none', color: '#fff', fontFamily: fontD, fontWeight: 800, fontSize: 15, cursor: 'pointer', boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' }}>{t.add} {name.trim() || (ar ? 'الطفل' : 'child')} {ar ? '←' : '→'}</button>
        </div>
      </div>
    </div>
  );

  function Field({ label, children, fontD, flex }) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, flex: flex ? 1 : undefined }}>
        <label style={{ fontFamily: fontD, fontWeight: 700, fontSize: 12, color: '#CBD5E1' }}>{label}</label>
        {children}
      </div>
    );
  }
  function acmInput() {
    return { height: 46, background: '#0B0C12', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 12, color: '#F8FAFC', fontFamily: ar ? "'Tajawal', sans-serif" : 'var(--lx-font-body)', fontSize: 15, padding: '0 14px', width: '100%', outline: 'none', boxSizing: 'border-box' };
  }
}

(function () {
  if (document.getElementById('acm-kf')) return;
  const s = document.createElement('style');
  s.id = 'acm-kf';
  s.textContent = '@keyframes acm-pop{0%{transform:scale(0.92);opacity:0}100%{transform:scale(1);opacity:1}}';
  document.head.appendChild(s);
})();

window.AddChildModal = AddChildModal;
