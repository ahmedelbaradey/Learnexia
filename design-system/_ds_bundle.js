/* @ds-bundle: {"format":3,"namespace":"LearnexiaDesignSystem_dab417","components":[],"sourceHashes":{"ui_kits/parent-dashboard/AddChildModal.jsx":"5848c5167c81","ui_kits/parent-dashboard/DashboardComponents.jsx":"43b342507870","ui_kits/parent-dashboard/PagesApp.jsx":"c2c46d504dcd","ui_kits/parent-dashboard/PagesPublic.jsx":"4cf5dbfd9dcd","ui_kits/parent-dashboard/browser-window.jsx":"2e3bb69bede4","ui_kits/student-mobile/MobileComponents.jsx":"ccc02caa564a","ui_kits/student-mobile/Screens.jsx":"8bcecd2d04b2","ui_kits/student-mobile/ScreensAuth.jsx":"29033cb1d86d","ui_kits/student-mobile/ScreensExtra.jsx":"3515d35f2f78","ui_kits/student-mobile/ios-frame.jsx":"d67eb3ffe562"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.LearnexiaDesignSystem_dab417 = window.LearnexiaDesignSystem_dab417 || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// ui_kits/parent-dashboard/AddChildModal.jsx
try { (() => {
// Learnexia — functional Add Child modal (web) with photo upload.
// Used by the parent web app. English + Arabic via the `ar` prop.

function AddChildModal({
  open,
  onClose,
  ar = false
}) {
  const [photo, setPhoto] = React.useState(null);
  const [color, setColor] = React.useState('#A855F7');
  const [name, setName] = React.useState(ar ? 'ليلى' : 'Layla');
  const [grade, setGrade] = React.useState(1);
  const [lang, setLang] = React.useState('ar');
  const fileRef = React.useRef(null);
  if (!open) return null;
  const t = ar ? {
    title: 'أضف طفلاً',
    sub: 'سيسجّل الدخول بالبريد الذي تحدّده',
    upload: 'ارفع صورة',
    uploadSub: 'PNG أو JPG · أو اختر لوناً بالأسفل',
    change: 'تغيير الصورة',
    name: 'اسم الطفل',
    email: 'بريد الدخول',
    grade: 'الصف',
    lang: 'اللغة',
    colorLbl: '…أو اختر لون الصورة الرمزية',
    cancel: 'إلغاء',
    add: 'أضف',
    grades: ['الصف الأول', 'الصف الثاني', 'الصف الثالث'],
    langs: ['🇸🇦 العربية', '🇬🇧 الإنجليزية']
  } : {
    title: 'Add a child',
    sub: "They'll log in with the email you set",
    upload: 'Upload a photo',
    uploadSub: 'PNG or JPG · or pick a color below',
    change: 'Change photo',
    name: "Child's name",
    email: 'Login email',
    grade: 'Grade',
    lang: 'Language',
    colorLbl: '…or pick an avatar color',
    cancel: 'Cancel',
    add: 'Add',
    grades: ['Grade 1', 'Grade 2', 'Grade 3'],
    langs: ['🇸🇦 العربية', '🇬🇧 English']
  };
  const onFile = e => {
    const f = e.target.files && e.target.files[0];
    if (f) setPhoto(URL.createObjectURL(f));
  };
  const fontD = ar ? "'Cairo', sans-serif" : "var(--lx-font-display)";
  const initial = (name.trim()[0] || (ar ? 'ط' : 'L')).toUpperCase();
  return /*#__PURE__*/React.createElement("div", {
    onClick: onClose,
    style: {
      position: 'fixed',
      inset: 0,
      zIndex: 100,
      background: 'rgba(5,8,22,0.7)',
      backdropFilter: 'blur(4px)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      direction: ar ? 'rtl' : 'ltr',
      fontFamily: ar ? "'Tajawal', sans-serif" : 'var(--lx-font-body)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    onClick: e => e.stopPropagation(),
    style: {
      width: 480,
      maxWidth: '92vw',
      background: '#15161D',
      borderRadius: 24,
      border: '1px solid rgba(255,255,255,0.08)',
      boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.06)',
      overflow: 'hidden',
      animation: 'acm-pop 280ms cubic-bezier(0.34,1.56,0.64,1)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '22px 24px 16px',
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      gap: 12,
      borderBottom: '1px solid rgba(255,255,255,0.05)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 44,
      height: 44,
      borderRadius: 14,
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 22,
      boxShadow: '0 6px 16px rgba(99,102,241,0.4)'
    }
  }, "\uD83D\uDC76"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: fontD,
      fontWeight: 800,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, t.title), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, t.sub))), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    style: {
      width: 32,
      height: 32,
      borderRadius: 10,
      background: 'rgba(255,255,255,0.05)',
      border: 'none',
      color: '#94A3B8',
      fontSize: 18,
      cursor: 'pointer'
    }
  }, "\u2715")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '20px 24px',
      display: 'flex',
      flexDirection: 'column',
      gap: 16,
      maxHeight: '60vh',
      overflowY: 'auto'
    }
  }, /*#__PURE__*/React.createElement("input", {
    ref: fileRef,
    type: "file",
    accept: "image/*",
    onChange: onFile,
    style: {
      display: 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 64,
      height: 64,
      borderRadius: '50%',
      background: photo ? `url(${photo}) center/cover` : color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontFamily: fontD,
      fontWeight: 900,
      fontSize: 26,
      color: '#fff',
      flexShrink: 0,
      position: 'relative',
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.2)'
    }
  }, !photo && initial, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: -2,
      [ar ? 'left' : 'right']: -2,
      width: 24,
      height: 24,
      borderRadius: '50%',
      background: '#4F46E5',
      border: '2px solid #15161D',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 11
    }
  }, "\uD83D\uDCF7")), /*#__PURE__*/React.createElement("div", {
    onClick: () => fileRef.current && fileRef.current.click(),
    style: {
      flex: 1,
      border: '1.5px dashed rgba(99,102,241,0.45)',
      borderRadius: 14,
      padding: '12px 14px',
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      cursor: 'pointer',
      background: 'rgba(79,70,229,0.05)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20
    }
  }, "\u2B06\uFE0F"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: fontD,
      fontWeight: 700,
      fontSize: 13,
      color: '#A5B4FC'
    }
  }, photo ? t.change : t.upload), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      marginTop: 1
    }
  }, t.uploadSub)))), /*#__PURE__*/React.createElement(Field, {
    label: t.name,
    fontD: fontD
  }, /*#__PURE__*/React.createElement("input", {
    value: name,
    onChange: e => setName(e.target.value),
    style: acmInput()
  })), /*#__PURE__*/React.createElement(Field, {
    label: t.email,
    fontD: fontD
  }, /*#__PURE__*/React.createElement("input", {
    defaultValue: "layla@learnexia.com",
    dir: "ltr",
    style: {
      ...acmInput(),
      borderColor: '#4F46E5',
      boxShadow: '0 0 0 3px rgba(99,102,241,0.2)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontFamily: fontD,
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, t.grade), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(6, 1fr)',
      gap: 6
    }
  }, [1, 2, 3, 4, 5, 6].map(g => {
    const on = grade === g;
    return /*#__PURE__*/React.createElement("div", {
      key: g,
      onClick: () => setGrade(g),
      style: {
        height: 46,
        borderRadius: 12,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1,
        background: on ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#0B0C12',
        border: on ? 'none' : '1px solid rgba(255,255,255,0.1)',
        boxShadow: on ? '0 4px 12px rgba(99,102,241,0.4)' : 'none'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 15
      }
    }, ['🌱', '🌿', '🌳', '🌲', '🍃', '🌴'][g - 1]), /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: fontD,
        fontWeight: 800,
        fontSize: 11,
        color: on ? '#fff' : '#94A3B8'
      }
    }, ar ? ['١', '٢', '٣', '٤', '٥', '٦'][g - 1] : g));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontFamily: fontD,
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, t.lang), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8
    }
  }, [{
    id: 'ar',
    flag: '🇪🇬',
    label: 'AR'
  }, {
    id: 'en',
    flag: '🇺🇸',
    label: 'EN'
  }].map(l => {
    const on = lang === l.id;
    return /*#__PURE__*/React.createElement("div", {
      key: l.id,
      onClick: () => setLang(l.id),
      style: {
        flex: 1,
        height: 48,
        borderRadius: 12,
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 8,
        background: on ? 'rgba(79,70,229,0.18)' : '#0B0C12',
        border: on ? '1.5px solid #4F46E5' : '1px solid rgba(255,255,255,0.1)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 18
      }
    }, l.flag), /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: fontD,
        fontWeight: 700,
        fontSize: 14,
        color: on ? '#F8FAFC' : '#94A3B8'
      }
    }, l.label));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontFamily: fontD,
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, t.colorLbl), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10
    }
  }, ['#FB923C', '#A855F7', '#22C55E', '#38BDF8', '#FB7185'].map(c => /*#__PURE__*/React.createElement("div", {
    key: c,
    onClick: () => {
      setColor(c);
      setPhoto(null);
    },
    style: {
      width: 36,
      height: 36,
      borderRadius: '50%',
      background: c,
      cursor: 'pointer',
      boxShadow: color === c && !photo ? `0 0 0 3px #15161D, 0 0 0 5px ${c}` : 'none'
    }
  }))))), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '16px 24px 22px',
      display: 'flex',
      gap: 10,
      borderTop: '1px solid rgba(255,255,255,0.05)'
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    style: {
      flex: 1,
      height: 48,
      borderRadius: 14,
      background: 'transparent',
      border: '1px solid rgba(255,255,255,0.12)',
      color: '#CBD5E1',
      fontFamily: fontD,
      fontWeight: 700,
      fontSize: 15,
      cursor: 'pointer'
    }
  }, t.cancel), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    style: {
      flex: 2,
      height: 48,
      borderRadius: 14,
      background: '#4F46E5',
      border: 'none',
      color: '#fff',
      fontFamily: fontD,
      fontWeight: 800,
      fontSize: 15,
      cursor: 'pointer',
      boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)'
    }
  }, t.add, " ", name.trim() || (ar ? 'الطفل' : 'child'), " ", ar ? '←' : '→'))));
  function Field({
    label,
    children,
    fontD,
    flex
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        flex: flex ? 1 : undefined
      }
    }, /*#__PURE__*/React.createElement("label", {
      style: {
        fontFamily: fontD,
        fontWeight: 700,
        fontSize: 12,
        color: '#CBD5E1'
      }
    }, label), children);
  }
  function acmInput() {
    return {
      height: 46,
      background: '#0B0C12',
      border: '1px solid rgba(255,255,255,0.1)',
      borderRadius: 12,
      color: '#F8FAFC',
      fontFamily: ar ? "'Tajawal', sans-serif" : 'var(--lx-font-body)',
      fontSize: 15,
      padding: '0 14px',
      width: '100%',
      outline: 'none',
      boxSizing: 'border-box'
    };
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
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/parent-dashboard/AddChildModal.jsx", error: String((e && e.message) || e) }); }

// ui_kits/parent-dashboard/DashboardComponents.jsx
try { (() => {
// Learnexia Parent Dashboard — reusable UI primitives.

const pdFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};

// Linked children for the family switcher.
const PD_CHILDREN = [{
  id: 'sami',
  name: 'Sami',
  grade: 'Grade 3 · Level 12',
  av: 'S',
  from: '#FB923C',
  to: '#EF4444',
  xp: '+340 XP',
  up: 'Up 28% from last week'
}, {
  id: 'layla',
  name: 'Layla',
  grade: 'Grade 5 · Level 20',
  av: 'L',
  from: '#A855F7',
  to: '#6366F1',
  xp: '+512 XP',
  up: 'Up 12% from last week'
}, {
  id: 'yusuf',
  name: 'Yusuf',
  grade: 'Grade 1 · Level 4',
  av: 'Y',
  from: '#22C55E',
  to: '#0EA5E9',
  xp: '+180 XP',
  up: 'New this week 🎉'
}];

// Shared app context: current child + theme. App provides it; sidebar consumes it.
const PDCtx = React.createContext(null);
function usePD() {
  return React.useContext(PDCtx) || {};
}
function PDChildSwitcher({
  collapsed = false
}) {
  const {
    child = PD_CHILDREN[0],
    setChild
  } = usePD();
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    const onDoc = e => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, []);
  const Avatar = ({
    c,
    size = 36,
    fs = 16
  }) => /*#__PURE__*/React.createElement("div", {
    style: {
      width: size,
      height: size,
      borderRadius: '50%',
      flexShrink: 0,
      background: `linear-gradient(135deg,${c.from},${c.to})`,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: fs,
      fontWeight: 800,
      color: '#fff'
    }
  }, c.av);
  return /*#__PURE__*/React.createElement("div", {
    ref: ref,
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: () => setOpen(o => !o),
    style: {
      width: '100%',
      background: '#1E293B',
      borderRadius: 16,
      padding: 12,
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      cursor: 'pointer',
      border: open ? '1px solid rgba(99,102,241,0.6)' : '1px solid transparent',
      fontFamily: 'inherit',
      textAlign: 'left',
      transition: 'border-color 140ms ease'
    }
  }, /*#__PURE__*/React.createElement(Avatar, {
    c: child
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 13,
      color: '#F8FAFC'
    }
  }, child.name), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8'
    }
  }, child.grade)), /*#__PURE__*/React.createElement("div", {
    style: {
      color: '#94A3B8',
      fontSize: 13,
      transform: open ? 'rotate(90deg)' : 'none',
      transition: 'transform 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, "\u203A")), open && /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 'calc(100% + 8px)',
      left: 0,
      right: 0,
      zIndex: 80,
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.1)',
      borderRadius: 16,
      boxShadow: '0 20px 50px rgba(0,0,0,0.55)',
      padding: 8,
      display: 'flex',
      flexDirection: 'column',
      gap: 2,
      animation: 'pdDrop 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      fontWeight: 800,
      color: '#64748B',
      letterSpacing: '0.1em',
      textTransform: 'uppercase',
      padding: '6px 10px 4px'
    }
  }, "Switch child"), PD_CHILDREN.map(c => {
    const on = c.id === child.id;
    return /*#__PURE__*/React.createElement("button", {
      key: c.id,
      onClick: () => {
        setChild && setChild(c);
        setOpen(false);
      },
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        padding: '8px 10px',
        borderRadius: 12,
        background: on ? 'rgba(79,70,229,0.18)' : 'transparent',
        border: 'none',
        cursor: 'pointer',
        fontFamily: 'inherit',
        textAlign: 'left'
      }
    }, /*#__PURE__*/React.createElement(Avatar, {
      c: c,
      size: 32,
      fs: 14
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        minWidth: 0
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 700,
        fontSize: 13,
        color: '#F8FAFC'
      }
    }, c.name), /*#__PURE__*/React.createElement("div", {
      style: {
        fontSize: 11,
        color: '#94A3B8'
      }
    }, c.grade)), on && /*#__PURE__*/React.createElement("span", {
      style: {
        color: '#A5B4FC',
        fontSize: 14
      }
    }, "\u2713"));
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 1,
      background: 'rgba(255,255,255,0.07)',
      margin: '6px 4px'
    }
  }), /*#__PURE__*/React.createElement("button", {
    onClick: () => setOpen(false),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      padding: '8px 10px',
      borderRadius: 12,
      background: 'transparent',
      border: 'none',
      cursor: 'pointer',
      fontFamily: 'inherit',
      textAlign: 'left',
      color: '#A5B4FC',
      fontWeight: 700,
      fontSize: 13
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 32,
      height: 32,
      borderRadius: '50%',
      border: '1.5px dashed rgba(165,180,252,0.5)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 16
    }
  }, "\uFF0B"), "Add a child")));
}
function PDPrefsRow() {
  const {
    theme = 'night',
    setTheme
  } = usePD();
  const seg = active => ({
    flex: 1,
    height: 34,
    borderRadius: 9,
    border: 'none',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    fontFamily: 'inherit',
    fontWeight: 700,
    fontSize: 12,
    background: active ? '#334155' : 'transparent',
    color: active ? '#F8FAFC' : '#94A3B8',
    boxShadow: active ? '0 2px 6px rgba(0,0,0,0.3)' : 'none',
    transition: 'all 140ms ease'
  });
  const wrap = {
    display: 'flex',
    gap: 4,
    background: '#0F172A',
    borderRadius: 12,
    padding: 4,
    border: '1px solid rgba(255,255,255,0.06)'
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: wrap
  }, /*#__PURE__*/React.createElement("button", {
    style: seg(true),
    title: "English"
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 14
    }
  }, "\uD83C\uDDFA\uD83C\uDDF8"), "EN"), /*#__PURE__*/React.createElement("button", {
    style: seg(false),
    onClick: () => {
      window.location.href = 'index-ar.html';
    },
    title: "\u0627\u0644\u0639\u0631\u0628\u064A\u0629"
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 14
    }
  }, "\uD83C\uDDEA\uD83C\uDDEC"), "AR")), /*#__PURE__*/React.createElement("div", {
    style: wrap
  }, /*#__PURE__*/React.createElement("button", {
    style: seg(theme === 'night'),
    onClick: () => setTheme && setTheme('night'),
    title: "Night (navy)"
  }, "\uD83C\uDF19 Night"), /*#__PURE__*/React.createElement("button", {
    style: seg(theme === 'black'),
    onClick: () => setTheme && setTheme('black'),
    title: "Black (OLED)"
  }, "\u2B1B Black")));
}
function PDSidebar({
  active,
  onChange
}) {
  const {
    child = PD_CHILDREN[0],
    onLogout
  } = usePD();
  const items = [{
    id: 'children',
    label: 'My Children',
    icon: '👨‍👩‍👦'
  }, {
    id: 'overview',
    label: 'Overview',
    icon: '📊'
  }, {
    id: 'reports',
    label: 'Reports',
    icon: '📈'
  }, {
    id: 'energy',
    label: 'Helper Energy',
    icon: '⚡'
  }, {
    id: 'activity',
    label: 'Activity',
    icon: '⏱️'
  }, {
    id: 'subjects',
    label: 'Subjects',
    icon: '📚'
  }, {
    id: 'settings',
    label: 'Settings',
    icon: '⚙️'
  }];
  return /*#__PURE__*/React.createElement("aside", {
    style: {
      width: 240,
      background: 'var(--pd-rail,#0F172A)',
      borderRight: '1px solid rgba(255,255,255,0.06)',
      padding: '24px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 20,
      flexShrink: 0,
      ...pdFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      padding: '0 8px'
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/logo-mark.svg",
    style: {
      width: 36,
      height: 36
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "Learnexia")), /*#__PURE__*/React.createElement(PDChildSwitcher, null), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 2
    }
  }, items.map(i => /*#__PURE__*/React.createElement("button", {
    key: i.id,
    onClick: () => onChange(i.id),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '10px 12px',
      borderRadius: 12,
      border: 'none',
      background: active === i.id ? 'rgba(79,70,229,0.18)' : 'transparent',
      color: active === i.id ? '#A5B4FC' : '#94A3B8',
      fontWeight: active === i.id ? 700 : 500,
      fontSize: 14,
      cursor: 'pointer',
      textAlign: 'left',
      fontFamily: 'inherit',
      transition: 'all 120ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 16
    }
  }, i.icon), i.label))), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 'auto',
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(PDPrefsRow, null), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 16,
      padding: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#FACC15',
      letterSpacing: '0.08em',
      textTransform: 'uppercase'
    }
  }, "This week"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 20,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, child.xp), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8'
    }
  }, child.up)), /*#__PURE__*/React.createElement("button", {
    onClick: () => onLogout && onLogout(),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      width: '100%',
      padding: '10px 12px',
      borderRadius: 12,
      cursor: 'pointer',
      fontFamily: 'inherit',
      background: 'transparent',
      border: '1px solid rgba(239,68,68,0.25)',
      color: '#F87171',
      fontWeight: 700,
      fontSize: 13,
      textAlign: 'left',
      transition: 'background-color 140ms ease'
    },
    onMouseEnter: e => e.currentTarget.style.background = 'rgba(239,68,68,0.12)',
    onMouseLeave: e => e.currentTarget.style.background = 'transparent'
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 15
    }
  }, "\u21AA"), "Log out")));
}
function PDHeader({
  title,
  sub
}) {
  const {
    child
  } = usePD();
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '20px 32px',
      borderBottom: '1px solid rgba(255,255,255,0.06)',
      ...pdFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 22,
      color: '#F8FAFC'
    }
  }, title), sub && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 2
    }
  }, sub)), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10,
      alignItems: 'center'
    }
  }, child && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 9999,
      padding: '5px 12px 5px 6px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 26,
      height: 26,
      borderRadius: '50%',
      background: `linear-gradient(135deg,${child.from},${child.to})`,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 12,
      fontWeight: 800,
      color: '#fff'
    }
  }, child.av), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 13,
      fontWeight: 700,
      color: '#F8FAFC'
    }
  }, child.name)), /*#__PURE__*/React.createElement("select", {
    style: {
      background: '#1E293B',
      color: '#F8FAFC',
      border: '1px solid rgba(255,255,255,0.1)',
      padding: '8px 12px',
      borderRadius: 10,
      fontFamily: 'inherit',
      fontSize: 13,
      fontWeight: 600
    }
  }, /*#__PURE__*/React.createElement("option", null, "This week"), /*#__PURE__*/React.createElement("option", null, "Last week"), /*#__PURE__*/React.createElement("option", null, "This month")), /*#__PURE__*/React.createElement("button", {
    style: {
      background: '#4F46E5',
      color: '#fff',
      border: 'none',
      padding: '9px 16px',
      borderRadius: 10,
      fontWeight: 700,
      fontSize: 13,
      cursor: 'pointer',
      fontFamily: 'inherit',
      boxShadow: '0 4px 12px rgba(99,102,241,0.4)'
    }
  }, "Send Report")));
}
function PDStatCard({
  label,
  value,
  delta,
  accent = '#4F46E5',
  icon
}) {
  const positive = delta && delta.startsWith('+');
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 20,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      gap: 8,
      ...pdFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.08em'
    }
  }, label), icon && /*#__PURE__*/React.createElement("div", {
    style: {
      width: 32,
      height: 32,
      borderRadius: 10,
      background: `${accent}22`,
      color: accent,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 15
    }
  }, icon)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 28,
      color: '#F8FAFC',
      fontVariantNumeric: 'tabular-nums',
      lineHeight: 1
    }
  }, value), delta && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 700,
      color: positive ? '#22C55E' : '#EF4444'
    }
  }, delta, " vs last week"));
}
function PDActivityChart({
  data
}) {
  const max = Math.max(...data.map(d => d.xp));
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      alignItems: 'flex-end',
      height: 200,
      ...pdFont
    }
  }, data.map((d, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      flex: 1,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: `${d.xp / max * 160}px`,
      background: d.today ? 'linear-gradient(180deg,#A855F7,#4F46E5)' : 'linear-gradient(180deg,#334155,#1E293B)',
      borderRadius: '10px 10px 4px 4px',
      position: 'relative',
      boxShadow: d.today ? '0 6px 18px rgba(99,102,241,0.4)' : 'none'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: -22,
      left: '50%',
      transform: 'translateX(-50%)',
      fontSize: 11,
      fontWeight: 800,
      color: d.today ? '#A5B4FC' : '#64748B',
      fontVariantNumeric: 'tabular-nums',
      whiteSpace: 'nowrap'
    }
  }, d.xp)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: d.today ? '#F8FAFC' : '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, d.day))));
}
function PDWeakAreas({
  items
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12,
      ...pdFont
    }
  }, items.map((it, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      padding: '12px 14px',
      background: '#0F172A',
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.04)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 38,
      height: 38,
      borderRadius: 12,
      background: `${it.color}22`,
      color: it.color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 17,
      flexShrink: 0
    }
  }, it.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 13,
      color: '#F8FAFC'
    }
  }, it.topic), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8'
    }
  }, it.subject)), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 120
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: 8,
      background: '#1E293B',
      borderRadius: 9999,
      overflow: 'hidden',
      border: '1px solid rgba(255,255,255,0.04)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${it.accuracy}%`,
      background: it.accuracy < 50 ? '#EF4444' : it.accuracy < 70 ? '#F59E0B' : '#22C55E'
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: it.color,
      width: 44,
      textAlign: 'right',
      fontVariantNumeric: 'tabular-nums'
    }
  }, it.accuracy, "%"))));
}
function PDPanel({
  title,
  sub,
  action,
  children,
  style = {}
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 24,
      padding: 22,
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...pdFont,
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16,
      color: '#F8FAFC'
    }
  }, title), sub && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, sub)), action && /*#__PURE__*/React.createElement("button", {
    style: {
      background: 'transparent',
      border: '1px solid rgba(255,255,255,0.12)',
      color: '#A5B4FC',
      padding: '6px 12px',
      borderRadius: 9999,
      fontWeight: 700,
      fontSize: 12,
      cursor: 'pointer',
      fontFamily: 'inherit'
    }
  }, action)), children);
}
function PDRecommendation({
  icon,
  title,
  body,
  cta,
  accent = '#4F46E5'
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 14,
      padding: 14,
      background: '#0F172A',
      border: '1px solid rgba(255,255,255,0.04)',
      borderRadius: 16,
      ...pdFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      flexShrink: 0,
      background: `${accent}22`,
      color: accent,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 18
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, title), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 3,
      lineHeight: 1.45
    }
  }, body), /*#__PURE__*/React.createElement("button", {
    style: {
      marginTop: 10,
      background: 'transparent',
      border: 'none',
      color: accent,
      fontWeight: 700,
      fontSize: 12,
      cursor: 'pointer',
      padding: 0,
      fontFamily: 'inherit'
    }
  }, cta, " \u2192")));
}
Object.assign(window, {
  PDSidebar,
  PDHeader,
  PDStatCard,
  PDActivityChart,
  PDWeakAreas,
  PDPanel,
  PDRecommendation,
  PDCtx,
  PD_CHILDREN,
  usePD,
  PDChildSwitcher,
  PDPrefsRow
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/parent-dashboard/DashboardComponents.jsx", error: String((e && e.message) || e) }); }

// ui_kits/parent-dashboard/PagesApp.jsx
try { (() => {
// Learnexia Web — in-app pages: My Children, Reports, Settings, Activity, Subjects

const appFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};

// ────────────────────────────────────────────────────────────── MY CHILDREN (web)
function MyChildrenWebPage({
  onPick,
  onAddChild,
  sidebarActive,
  onNav
}) {
  const children = [{
    id: 1,
    name: 'Sami',
    color: '#FB923C',
    grade: 3,
    language: '🇬🇧 English',
    level: 12,
    xp: 1240,
    streak: 7,
    mastery: 72,
    active: true,
    weakest: 'Fractions'
  }, {
    id: 2,
    name: 'Layla',
    color: '#A855F7',
    grade: 1,
    language: '🇸🇦 العربية',
    level: 4,
    xp: 380,
    streak: 2,
    mastery: 45,
    active: true,
    weakest: 'Letters'
  }, {
    id: 3,
    name: 'Yusuf',
    color: '#38BDF8',
    grade: 5,
    language: '🇬🇧 English',
    level: 18,
    xp: 2860,
    streak: 0,
    mastery: 81,
    active: false,
    weakest: 'Geometry'
  }];
  return /*#__PURE__*/React.createElement(AppShell, {
    active: sidebarActive,
    onNav: onNav
  }, /*#__PURE__*/React.createElement(PDHeader, {
    title: "My Children",
    sub: "3 children linked to your account"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 28,
      display: 'flex',
      flexDirection: 'column',
      gap: 20,
      ...appFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#A855F7 0%,#6366F1 100%)',
      borderRadius: 24,
      padding: 28,
      display: 'grid',
      gridTemplateColumns: '1.4fr repeat(4, 1fr)',
      alignItems: 'center',
      gap: 20,
      color: '#fff',
      boxShadow: '0 16px 36px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.18)',
      position: 'relative',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      right: -20,
      top: -20,
      fontSize: 180,
      opacity: 0.18,
      pointerEvents: 'none'
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC66"), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 12,
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
      opacity: 0.85
    }
  }, "This Week \xB7 Combined"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 28,
      marginTop: 6,
      letterSpacing: '-0.02em'
    }
  }, "Your family is on a roll"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      marginTop: 6,
      opacity: 0.85
    }
  }, "3 active learners \xB7 18 lessons completed")), /*#__PURE__*/React.createElement(HeroStat, {
    icon: "\u2B50",
    value: "4,480",
    label: "Total XP"
  }), /*#__PURE__*/React.createElement(HeroStat, {
    icon: "\uD83D\uDCDA",
    value: "18",
    label: "Lessons"
  }), /*#__PURE__*/React.createElement(HeroStat, {
    icon: "\uD83D\uDD25",
    value: "9d",
    label: "Best streak"
  }), /*#__PURE__*/React.createElement(HeroStat, {
    icon: "\uD83C\uDFC6",
    value: "5",
    label: "Badges earned"
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "Pick a child to view their progress"), /*#__PURE__*/React.createElement("button", {
    onClick: onAddChild,
    style: {
      ...btnPrimary(),
      height: 44,
      padding: '0 18px'
    }
  }, "+ Add Child")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3, 1fr)',
      gap: 16
    }
  }, children.map(c => /*#__PURE__*/React.createElement(ChildWebCard, {
    key: c.id,
    child: c,
    onClick: () => onPick(c)
  })), /*#__PURE__*/React.createElement("button", {
    onClick: onAddChild,
    style: {
      background: 'transparent',
      border: '2px dashed rgba(99,102,241,0.4)',
      borderRadius: 24,
      padding: 32,
      minHeight: 260,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 12,
      color: '#A5B4FC',
      cursor: 'pointer',
      fontFamily: 'inherit',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    },
    onPointerOver: e => {
      e.currentTarget.style.background = 'rgba(79,70,229,0.06)';
      e.currentTarget.style.borderColor = '#4F46E5';
    },
    onPointerOut: e => {
      e.currentTarget.style.background = 'transparent';
      e.currentTarget.style.borderColor = 'rgba(99,102,241,0.4)';
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 64,
      height: 64,
      borderRadius: 20,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 32,
      fontWeight: 800
    }
  }, "+"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16,
      color: '#F8FAFC'
    }
  }, "Add a child"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      textAlign: 'center',
      maxWidth: 200
    }
  }, "Set their grade, language, and login email"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      padding: '16px 20px',
      borderRadius: 16,
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      background: 'rgba(34,197,94,0.15)',
      color: '#22C55E',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20
    }
  }, "\uD83D\uDEE1\uFE0F"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, "You're the only parent linked to these accounts"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "Each child logs in with their assigned email. Children can't self-register. ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700,
      cursor: 'pointer'
    }
  }, "Manage permissions \u2192"))))));
}
function ChildWebCard({
  child,
  onClick
}) {
  return /*#__PURE__*/React.createElement("div", {
    onClick: onClick,
    style: {
      background: '#1E293B',
      borderRadius: 24,
      padding: 24,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      cursor: 'pointer',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    },
    onPointerOver: e => {
      e.currentTarget.style.transform = 'translateY(-2px)';
      e.currentTarget.style.boxShadow = '0 8px 24px rgba(0,0,0,0.25)';
    },
    onPointerOut: e => {
      e.currentTarget.style.transform = 'translateY(0)';
      e.currentTarget.style.boxShadow = '0 4px 12px rgba(0,0,0,0.15)';
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 64,
      height: 64,
      borderRadius: '50%',
      background: child.color,
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 26,
      boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2), 0 6px 16px rgba(0,0,0,0.25)'
    }
  }, child.name[0]), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22,
      color: '#F8FAFC',
      lineHeight: 1
    }
  }, child.name), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      marginTop: 6,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 8px',
      borderRadius: 9999,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      fontWeight: 800,
      fontSize: 11
    }
  }, "Grade ", child.grade), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, child.language))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      fontSize: 11,
      fontWeight: 700,
      color: child.active ? '#22C55E' : '#64748B'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: child.active ? '#22C55E' : '#64748B',
      boxShadow: child.active ? '0 0 6px rgba(34,197,94,0.6)' : 'none'
    }
  }), child.active ? 'Active today' : 'Inactive')), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(ChildKPI, {
    icon: "\uD83E\uDDE0",
    value: `Lv ${child.level}`,
    label: "Level",
    color: "#A855F7"
  }), /*#__PURE__*/React.createElement(ChildKPI, {
    icon: "\u2B50",
    value: child.xp.toLocaleString(),
    label: "XP",
    color: "#FACC15"
  }), /*#__PURE__*/React.createElement(ChildKPI, {
    icon: "\uD83D\uDD25",
    value: `${child.streak}d`,
    label: "Streak",
    color: "#FB923C"
  })), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, "Mastery"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12,
      fontWeight: 800,
      color: '#F8FAFC'
    }
  }, child.mastery, "%")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 8,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${child.mastery}%`,
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)'
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      paddingTop: 14,
      borderTop: '1px solid rgba(255,255,255,0.05)',
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8'
    }
  }, "Weakest:"), " ", /*#__PURE__*/React.createElement("b", null, child.weakest)), /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 800
    }
  }, "View dashboard \u2192")));
}
function ChildKPI({
  icon,
  value,
  label,
  color
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      padding: '10px 12px',
      background: '#0F172A',
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.04)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 14
    }
  }, icon), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 16,
      color,
      fontVariantNumeric: 'tabular-nums'
    }
  }, value)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      fontWeight: 700,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginTop: 2
    }
  }, label));
}
function HeroStat({
  icon,
  value,
  label
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 22,
      marginBottom: 4
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 28,
      color: '#fff',
      fontVariantNumeric: 'tabular-nums',
      lineHeight: 1
    }
  }, value), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.08em',
      opacity: 0.85,
      marginTop: 4
    }
  }, label));
}

// ────────────────────────────────────────────────────────────── REPORTS
function ReportsWebPage({
  sidebarActive,
  onNav
}) {
  const monthData = [{
    day: '1',
    xp: 45
  }, {
    day: '2',
    xp: 60
  }, {
    day: '3',
    xp: 90
  }, {
    day: '4',
    xp: 30
  }, {
    day: '5',
    xp: 80
  }, {
    day: '6',
    xp: 70
  }, {
    day: '7',
    xp: 100
  }, {
    day: '8',
    xp: 50
  }, {
    day: '9',
    xp: 85
  }, {
    day: '10',
    xp: 95
  }, {
    day: '11',
    xp: 40
  }, {
    day: '12',
    xp: 110
  }, {
    day: '13',
    xp: 70
  }, {
    day: '14',
    xp: 60
  }, {
    day: '15',
    xp: 0
  }, {
    day: '16',
    xp: 75
  }, {
    day: '17',
    xp: 90
  }, {
    day: '18',
    xp: 110
  }, {
    day: '19',
    xp: 50
  }, {
    day: '20',
    xp: 130,
    today: true
  }];
  return /*#__PURE__*/React.createElement(AppShell, {
    active: sidebarActive,
    onNav: onNav
  }, /*#__PURE__*/React.createElement(PDHeader, {
    title: "Sami's reports",
    sub: "Detailed monthly breakdown \xB7 Switch child in header"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 28,
      display: 'flex',
      flexDirection: 'column',
      gap: 20,
      ...appFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(4, 1fr)',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(PDStatCard, {
    label: "Time learning",
    value: "14h 12m",
    delta: "+38%",
    accent: "#4F46E5",
    icon: "\u23F1\uFE0F"
  }), /*#__PURE__*/React.createElement(PDStatCard, {
    label: "XP earned",
    value: "2,180",
    delta: "+22%",
    accent: "#FACC15",
    icon: "\u2B50"
  }), /*#__PURE__*/React.createElement(PDStatCard, {
    label: "Lessons mastered",
    value: "42",
    delta: "+9",
    accent: "#22C55E",
    icon: "\u2713"
  }), /*#__PURE__*/React.createElement(PDStatCard, {
    label: "Avg. accuracy",
    value: "84%",
    delta: "+6%",
    accent: "#A855F7",
    icon: "\uD83C\uDFAF"
  })), /*#__PURE__*/React.createElement(PDPanel, {
    title: "Last 20 days \xB7 XP earned",
    sub: "Today highlighted in indigo",
    action: "Export CSV"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6,
      alignItems: 'flex-end',
      height: 220
    }
  }, monthData.map((d, i) => {
    const max = 130;
    return /*#__PURE__*/React.createElement("div", {
      key: i,
      style: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 6
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: '100%',
        height: `${d.xp / max * 180}px`,
        minHeight: d.xp === 0 ? 4 : 8,
        background: d.today ? 'linear-gradient(180deg,#A855F7,#4F46E5)' : d.xp === 0 ? '#1E293B' : 'linear-gradient(180deg,#334155,#1E293B)',
        borderRadius: '6px 6px 3px 3px',
        boxShadow: d.today ? '0 6px 18px rgba(99,102,241,0.4)' : 'none'
      }
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        fontSize: 10,
        fontWeight: 700,
        color: d.today ? '#A5B4FC' : '#64748B',
        fontVariantNumeric: 'tabular-nums'
      }
    }, d.day));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 20
    }
  }, /*#__PURE__*/React.createElement(PDPanel, {
    title: "Skills mastery",
    sub: "Mastery levels across subjects"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, [{
    name: 'Math',
    pct: 72,
    lessons: 14,
    color: '#4F46E5'
  }, {
    name: 'Reading',
    pct: 65,
    lessons: 8,
    color: '#A855F7'
  }, {
    name: 'Science',
    pct: 58,
    lessons: 6,
    color: '#22C55E'
  }, {
    name: 'English',
    pct: 81,
    lessons: 12,
    color: '#FB923C'
  }, {
    name: 'Arabic',
    pct: 48,
    lessons: 4,
    color: '#38BDF8'
  }].map(s => /*#__PURE__*/React.createElement("div", {
    key: s.name,
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      fontSize: 13,
      fontWeight: 700
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#F8FAFC'
    }
  }, s.name), /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8'
    }
  }, s.lessons, " lessons \xB7 ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: s.color,
      fontWeight: 800
    }
  }, s.pct, "%"))), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 10,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${s.pct}%`,
      background: s.color
    }
  })))))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "Time of day",
    sub: "When Sami learns best"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-end',
      gap: 6,
      height: 200,
      paddingBottom: 20
    }
  }, [['6a', 5], ['8a', 15], ['10a', 45], ['12p', 30], ['2p', 60], ['4p', 95], ['6p', 70], ['8p', 25]].map(([label, v], i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      flex: 1,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      fontWeight: 700,
      color: '#94A3B8',
      fontVariantNumeric: 'tabular-nums'
    }
  }, v, "m"), /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: `${v / 100 * 160}px`,
      background: v >= 90 ? 'linear-gradient(180deg,#FB923C,#EF4444)' : v >= 60 ? '#A855F7' : '#334155',
      borderRadius: '6px 6px 3px 3px',
      boxShadow: v >= 90 ? '0 4px 14px rgba(251,146,60,0.35)' : 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: v >= 90 ? '#F8FAFC' : '#94A3B8'
    }
  }, label)))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      padding: '10px 12px',
      background: 'rgba(251,146,60,0.08)',
      borderRadius: 12,
      border: '1px solid rgba(251,146,60,0.2)',
      fontSize: 13,
      color: '#FB923C',
      fontWeight: 700
    }
  }, "\uD83D\uDCA1 Peak focus is 4\u20135pm \u2014 great time for new material"))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "Areas to focus on",
    sub: "Topics where Sami is still building confidence"
  }, /*#__PURE__*/React.createElement(PDWeakAreas, {
    items: [{
      topic: 'Subtraction with borrowing',
      subject: 'Math',
      icon: '➖',
      color: '#EF4444',
      accuracy: 42
    }, {
      topic: 'Long vowels',
      subject: 'Reading',
      icon: '🔤',
      color: '#F59E0B',
      accuracy: 58
    }, {
      topic: 'States of matter',
      subject: 'Science',
      icon: '🧪',
      color: '#F59E0B',
      accuracy: 64
    }, {
      topic: 'Multiplication tables',
      subject: 'Math',
      icon: '✕',
      color: '#22C55E',
      accuracy: 78
    }]
  }))));
}

// ────────────────────────────────────────────────────────────── SETTINGS
function SettingsWebPage({
  sidebarActive,
  onNav
}) {
  const [tab, setTab] = React.useState('profile');
  return /*#__PURE__*/React.createElement(AppShell, {
    active: sidebarActive,
    onNav: onNav
  }, /*#__PURE__*/React.createElement(PDHeader, {
    title: "Settings",
    sub: "Manage your account and preferences"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 28,
      display: 'grid',
      gridTemplateColumns: '220px 1fr',
      gap: 24,
      ...appFont
    }
  }, /*#__PURE__*/React.createElement("nav", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 4
    }
  }, [['profile', '👤 Profile'], ['notifications', '🔔 Notifications'], ['linked', '👨‍👩‍👦 Linked children'], ['security', '🛡️ Security'], ['plan', '💎 Plan & billing'], ['language', '🌍 Language & region']].map(([id, label]) => /*#__PURE__*/React.createElement("button", {
    key: id,
    onClick: () => setTab(id),
    style: {
      textAlign: 'left',
      padding: '10px 14px',
      borderRadius: 12,
      border: 'none',
      background: tab === id ? 'rgba(79,70,229,0.18)' : 'transparent',
      color: tab === id ? '#A5B4FC' : '#94A3B8',
      fontWeight: tab === id ? 800 : 600,
      fontSize: 14,
      cursor: 'pointer',
      fontFamily: 'inherit'
    }
  }, label))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 20
    }
  }, tab === 'profile' && /*#__PURE__*/React.createElement(SettingsProfile, null), tab === 'notifications' && /*#__PURE__*/React.createElement(SettingsNotifications, null), tab === 'linked' && /*#__PURE__*/React.createElement(SettingsLinked, null), tab === 'security' && /*#__PURE__*/React.createElement(SettingsSecurity, null), tab === 'plan' && /*#__PURE__*/React.createElement(SettingsPlan, null), tab === 'language' && /*#__PURE__*/React.createElement(SettingsLanguage, null))));
}
function SettingsProfile() {
  return /*#__PURE__*/React.createElement(PDPanel, {
    title: "Profile",
    sub: "This is how Learnexia knows you"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 84,
      height: 84,
      borderRadius: '50%',
      background: 'linear-gradient(135deg,#FB923C,#EF4444)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 32,
      color: '#fff'
    }
  }, "A"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("button", {
    style: btnPrimary()
  }, "Upload photo"), /*#__PURE__*/React.createElement("button", {
    style: btnGhost()
  }, "Remove"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(WebField, {
    label: "Full name"
  }, /*#__PURE__*/React.createElement("input", {
    style: webInputStyle(),
    defaultValue: "Ahmed Hassan"
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Email"
  }, /*#__PURE__*/React.createElement("input", {
    type: "email",
    style: webInputStyle(),
    defaultValue: "ahmed@email.com"
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Phone"
  }, /*#__PURE__*/React.createElement("input", {
    style: webInputStyle(),
    defaultValue: "+966 50 123 4567"
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Country"
  }, /*#__PURE__*/React.createElement("select", {
    style: webInputStyle(),
    defaultValue: "SA"
  }, /*#__PURE__*/React.createElement("option", {
    value: "SA"
  }, "\uD83C\uDDF8\uD83C\uDDE6 Saudi Arabia"), /*#__PURE__*/React.createElement("option", {
    value: "AE"
  }, "\uD83C\uDDE6\uD83C\uDDEA UAE")))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end',
      gap: 10,
      paddingTop: 6
    }
  }, /*#__PURE__*/React.createElement("button", {
    style: btnGhost()
  }, "Cancel"), /*#__PURE__*/React.createElement("button", {
    style: btnPrimary()
  }, "Save changes")));
}
function SettingsNotifications() {
  const [prefs, setPrefs] = React.useState({
    weekly: true,
    streak: true,
    lessonReminders: true,
    marketing: false,
    childReports: true,
    lowHearts: false
  });
  const toggle = k => setPrefs({
    ...prefs,
    [k]: !prefs[k]
  });
  const rows = [['weekly', 'Weekly progress reports', 'Sent every Sunday morning'], ['streak', 'Streak risk alerts', 'Ping me if a child\'s streak is about to break'], ['lessonReminders', 'Daily lesson reminders', 'Gentle nudge for each child at their best time'], ['childReports', 'Child milestones', 'Level-ups, new badges, league promotions'], ['lowHearts', 'Low hearts warning', 'When a child runs out of hearts during practice'], ['marketing', 'Tips & product updates', 'Helpful articles and new features']];
  return /*#__PURE__*/React.createElement(PDPanel, {
    title: "Notifications",
    sub: "Choose what we email you about"
  }, rows.map(([key, title, sub]) => /*#__PURE__*/React.createElement("div", {
    key: key,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      justifyContent: 'space-between',
      padding: '14px 0',
      borderTop: '1px solid rgba(255,255,255,0.05)'
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, title), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, sub)), /*#__PURE__*/React.createElement(Toggle, {
    on: prefs[key],
    onChange: () => toggle(key)
  }))));
}
function SettingsLinked() {
  const children = [{
    name: 'Sami',
    email: 'sami@learnexia.com',
    grade: 3,
    language: 'EN',
    color: '#FB923C'
  }, {
    name: 'Layla',
    email: 'layla@learnexia.com',
    grade: 1,
    language: 'AR',
    color: '#A855F7'
  }, {
    name: 'Yusuf',
    email: 'yusuf@learnexia.com',
    grade: 5,
    language: 'EN',
    color: '#38BDF8'
  }];
  return /*#__PURE__*/React.createElement(PDPanel, {
    title: "Linked children",
    sub: "Manage who's on your account",
    action: "+ Add child"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 10
    }
  }, children.map((c, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      padding: '14px 16px',
      background: '#0F172A',
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.04)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 44,
      height: 44,
      borderRadius: '50%',
      background: c.color,
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 16
    }
  }, c.name[0]), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, c.name), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 7px',
      borderRadius: 9999,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      fontWeight: 800,
      fontSize: 10
    }
  }, "Grade ", c.grade), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 7px',
      borderRadius: 9999,
      background: 'rgba(255,255,255,0.06)',
      color: '#94A3B8',
      fontWeight: 700,
      fontSize: 10
    }
  }, c.language)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 3
    }
  }, c.email)), /*#__PURE__*/React.createElement("button", {
    style: {
      ...btnGhost(),
      height: 36,
      padding: '0 14px',
      fontSize: 13
    }
  }, "Edit"), /*#__PURE__*/React.createElement("button", {
    style: {
      ...btnGhost(),
      height: 36,
      padding: '0 14px',
      fontSize: 13,
      color: '#EF4444',
      borderColor: 'rgba(239,68,68,0.3)'
    }
  }, "Remove")))));
}
function SettingsSecurity() {
  return /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(PDPanel, {
    title: "Password",
    sub: "Last changed 3 months ago"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3, 1fr)',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(WebField, {
    label: "Current"
  }, /*#__PURE__*/React.createElement("input", {
    type: "password",
    style: webInputStyle(),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "New"
  }, /*#__PURE__*/React.createElement("input", {
    type: "password",
    style: webInputStyle(),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Confirm"
  }, /*#__PURE__*/React.createElement("input", {
    type: "password",
    style: webInputStyle(),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement("button", {
    style: btnPrimary()
  }, "Update password"))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "Two-factor authentication",
    sub: "Add an extra layer of security"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: 16,
      background: '#0F172A',
      borderRadius: 14
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, "SMS authentication"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "+966 50 \u2022\u2022\u2022 4567")), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '4px 10px',
      borderRadius: 9999,
      background: 'rgba(34,197,94,0.18)',
      color: '#22C55E',
      fontWeight: 800,
      fontSize: 11
    }
  }, "Enabled"))));
}
function SettingsPlan() {
  return /*#__PURE__*/React.createElement(PDPanel, {
    title: "Plan & billing",
    sub: "You're on the Family plan"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 18,
      padding: 20,
      borderRadius: 16,
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      color: '#fff'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 40
    }
  }, "\uD83D\uDC8E"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20
    }
  }, "Family \xB7 3 children"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      opacity: 0.85,
      marginTop: 2
    }
  }, "Renews Dec 15, 2026 \xB7 $14.99 / month")), /*#__PURE__*/React.createElement("button", {
    style: {
      ...btnPrimary(),
      background: '#fff',
      color: '#4F46E5',
      boxShadow: 'none'
    }
  }, "Manage")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3,1fr)',
      gap: 14,
      marginTop: 14
    }
  }, [['Apr 2026', '$14.99', 'Paid'], ['May 2026', '$14.99', 'Paid'], ['Jun 2026', '$14.99', 'Paid']].map(([m, a, s], i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      padding: 14,
      background: '#0F172A',
      borderRadius: 12,
      border: '1px solid rgba(255,255,255,0.04)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, m), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 18,
      color: '#F8FAFC',
      marginTop: 2
    }
  }, a), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 800,
      color: '#22C55E',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginTop: 4
    }
  }, s)))));
}
function SettingsLanguage() {
  return /*#__PURE__*/React.createElement(PDPanel, {
    title: "Language & region",
    sub: "Affects your dashboard, not your children's apps"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(WebField, {
    label: "Display language"
  }, /*#__PURE__*/React.createElement("select", {
    style: webInputStyle(),
    defaultValue: "en"
  }, /*#__PURE__*/React.createElement("option", {
    value: "en"
  }, "\uD83C\uDDEC\uD83C\uDDE7 English"), /*#__PURE__*/React.createElement("option", {
    value: "ar"
  }, "\uD83C\uDDF8\uD83C\uDDE6 \u0627\u0644\u0639\u0631\u0628\u064A\u0629"))), /*#__PURE__*/React.createElement(WebField, {
    label: "Time zone"
  }, /*#__PURE__*/React.createElement("select", {
    style: webInputStyle(),
    defaultValue: "ksa"
  }, /*#__PURE__*/React.createElement("option", {
    value: "ksa"
  }, "Saudi Arabia (GMT+3)"), /*#__PURE__*/React.createElement("option", {
    value: "uae"
  }, "UAE (GMT+4)"), /*#__PURE__*/React.createElement("option", {
    value: "utc"
  }, "UTC"))), /*#__PURE__*/React.createElement(WebField, {
    label: "Date format"
  }, /*#__PURE__*/React.createElement("select", {
    style: webInputStyle(),
    defaultValue: "dmy"
  }, /*#__PURE__*/React.createElement("option", {
    value: "dmy"
  }, "DD/MM/YYYY"), /*#__PURE__*/React.createElement("option", {
    value: "mdy"
  }, "MM/DD/YYYY"), /*#__PURE__*/React.createElement("option", {
    value: "ymd"
  }, "YYYY-MM-DD"))), /*#__PURE__*/React.createElement(WebField, {
    label: "Week starts on"
  }, /*#__PURE__*/React.createElement("select", {
    style: webInputStyle(),
    defaultValue: "sun"
  }, /*#__PURE__*/React.createElement("option", {
    value: "sun"
  }, "Sunday"), /*#__PURE__*/React.createElement("option", {
    value: "mon"
  }, "Monday")))));
}
function Toggle({
  on,
  onChange
}) {
  return /*#__PURE__*/React.createElement("button", {
    onClick: onChange,
    style: {
      width: 44,
      height: 26,
      borderRadius: 9999,
      border: 'none',
      background: on ? '#4F46E5' : '#334155',
      position: 'relative',
      cursor: 'pointer',
      flexShrink: 0,
      transition: 'background 180ms',
      boxShadow: on ? '0 0 12px rgba(99,102,241,0.4)' : 'none'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      top: 3,
      left: on ? 21 : 3,
      width: 20,
      height: 20,
      borderRadius: '50%',
      background: '#fff',
      boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
      transition: 'left 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }));
}

// ────────────────────────────────────────────────────────────── App shell with sidebar
function AppShell({
  active,
  onNav,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      minHeight: 820,
      background: 'var(--pd-canvas,#0F172A)',
      color: '#F8FAFC',
      ...appFont
    }
  }, /*#__PURE__*/React.createElement(PDSidebar, {
    active: active,
    onChange: onNav
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      display: 'flex',
      flexDirection: 'column',
      minWidth: 0
    }
  }, children));
}
Object.assign(window, {
  MyChildrenWebPage,
  ReportsWebPage,
  SettingsWebPage,
  EnergyWebPage,
  AppShell
});

// ────────────────────────────────────────────────────────────── HELPER ENERGY (web)
function EnergyWebPage({
  sidebarActive,
  onNav
}) {
  const usage = [{
    icon: '💡',
    n: 38,
    label: 'Hints',
    cost: 1,
    bg: 'rgba(45,212,191,0.18)',
    fg: '#2DD4BF'
  }, {
    icon: '🔍',
    n: 12,
    label: 'Explanations',
    cost: 3,
    bg: 'rgba(168,85,247,0.18)',
    fg: '#C4B5FD'
  }, {
    icon: '📖',
    n: 6,
    label: 'Deep',
    cost: 5,
    bg: 'rgba(79,70,229,0.20)',
    fg: '#A5B4FC'
  }, {
    icon: '🎯',
    n: 4,
    label: 'Practice',
    cost: 5,
    bg: 'rgba(251,146,60,0.18)',
    fg: '#FDBA74'
  }];
  const spent = usage.reduce((s, u) => s + u.n * u.cost, 0); // 38+36+30+20 = 124
  return /*#__PURE__*/React.createElement(AppShell, {
    active: sidebarActive,
    onNav: onNav
  }, /*#__PURE__*/React.createElement(PDHeader, {
    title: "Helper Energy",
    sub: "How Sami's AI-helper usage is metered \xB7 Switch child in header"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 28,
      display: 'flex',
      flexDirection: 'column',
      gap: 20,
      ...appFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1.4fr 1fr',
      gap: 20,
      alignItems: 'stretch'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,rgba(20,184,166,0.18),rgba(15,23,42,0.4))',
      border: '1px solid rgba(45,212,191,0.35)',
      borderRadius: 20,
      padding: 22,
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 9
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 22,
      filter: 'drop-shadow(0 0 8px rgba(45,212,191,0.6))'
    }
  }, "\u26A1"), /*#__PURE__*/React.createElement("b", {
    style: {
      fontSize: 16,
      color: '#F8FAFC'
    }
  }, "Energy left this month")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 30,
      color: '#2DD4BF',
      fontVariantNumeric: 'tabular-nums'
    }
  }, "180", /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 16,
      color: '#64748B'
    }
  }, " / 300"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 7
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 26,
      background: '#0F172A',
      border: '2px solid #14B8A6',
      borderRadius: 9,
      padding: 3,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '60%',
      background: 'linear-gradient(90deg,#2DD4BF,#14B8A6)',
      borderRadius: 5
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 6,
      height: 13,
      background: '#14B8A6',
      borderRadius: '0 4px 4px 0'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 13,
      color: '#94A3B8'
    }
  }, "\uD83D\uDCC5 Resets in ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#CBD5E1'
    }
  }, "12 days"), " \xB7 20/day cap"), /*#__PURE__*/React.createElement("button", {
    style: {
      height: 38,
      padding: '0 16px',
      borderRadius: 11,
      border: 'none',
      background: 'linear-gradient(135deg,#2DD4BF,#14B8A6)',
      color: '#06302B',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 13,
      cursor: 'pointer'
    }
  }, "\uD83D\uDD0B Buy top-up"))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 20,
      padding: 22,
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 800,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, "Two separate meters"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 26
    }
  }, "\u2764\uFE0F"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#FB7185',
      fontSize: 14
    }
  }, "Hearts"), " ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8',
      fontSize: 13
    }
  }, "= lives in practice (mistakes)"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 24
    }
  }, "\u26A1"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF',
      fontSize: 14
    }
  }, "Energy"), " ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8',
      fontSize: 13
    }
  }, "= AI-helper fuel (this page)"))), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#64748B',
      lineHeight: 1.5
    }
  }, "Spending energy never costs hearts, and losing hearts never costs energy."))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "AI helpers used this week",
    sub: `${spent} energy spent across ${usage.reduce((s, u) => s + u.n, 0)} helpers`
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(4,1fr)',
      gap: 14
    }
  }, usage.map(u => /*#__PURE__*/React.createElement("div", {
    key: u.label,
    style: {
      padding: 16,
      borderRadius: 14,
      background: '#0F172A',
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      background: u.bg,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 19,
      margin: '0 auto 8px'
    }
  }, u.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 24,
      color: '#F8FAFC',
      fontVariantNumeric: 'tabular-nums'
    }
  }, u.n), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      fontWeight: 700
    }
  }, u.label), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: u.fg,
      marginTop: 3
    }
  }, "\u26A1", u.cost, " each"))))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 20,
      alignItems: 'start'
    }
  }, /*#__PURE__*/React.createElement(PDPanel, {
    title: "What each helper costs",
    sub: "Children spend energy; they never see prices"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, [['💡', 'Hint', 1], ['🔍', 'Explain Mistake', 3], ['📖', 'Deep Explanation', 5], ['🎯', 'Practice Generation', 5]].map(([ic, name, c]) => /*#__PURE__*/React.createElement("div", {
    key: name,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '11px 13px',
      background: '#0F172A',
      borderRadius: 13
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18
    }
  }, ic), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1,
      fontWeight: 700,
      fontSize: 13,
      color: '#F8FAFC'
    }
  }, name), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#2DD4BF',
      background: 'rgba(45,212,191,0.14)',
      padding: '3px 10px',
      borderRadius: 9999
    }
  }, "\u26A1 ", c))))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "Plan & top-ups",
    sub: "You buy energy \u2014 your child just uses it"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: 14,
      background: 'linear-gradient(135deg,rgba(45,212,191,0.16),#0F172A)',
      border: '1px solid rgba(45,212,191,0.3)',
      borderRadius: 14,
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 28
    }
  }, "\uD83D\uDD0B"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#2DD4BF'
    }
  }, "+500 \u26A1"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8'
    }
  }, "Top-up pack \xB7 added instantly")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "$2.99")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 14,
      borderRadius: 13,
      background: '#0F172A'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC',
      marginBottom: 8
    }
  }, "Free"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      lineHeight: 1.8
    }
  }, "\u26A1 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF'
    }
  }, "300"), "/mo", /*#__PURE__*/React.createElement("br", null), "\uD83D\uDCC5 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF'
    }
  }, "20"), "/day cap")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 14,
      borderRadius: 13,
      background: 'linear-gradient(160deg,rgba(79,70,229,0.18),#0F172A)',
      border: '1px solid rgba(79,70,229,0.45)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC',
      marginBottom: 8,
      display: 'flex',
      alignItems: 'center',
      gap: 6
    }
  }, "Premium ", /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 9,
      fontWeight: 800,
      background: '#4F46E5',
      color: '#fff',
      padding: '2px 7px',
      borderRadius: 9999
    }
  }, "POPULAR")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      lineHeight: 1.8
    }
  }, "\u26A1 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF'
    }
  }, "3000"), "/mo", /*#__PURE__*/React.createElement("br", null), "\uD83D\uDCC5 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF'
    }
  }, "150"), "/day soft cap"))))), /*#__PURE__*/React.createElement(PDPanel, {
    title: "What your child sees",
    sub: "Kid-facing surfaces \u2014 costs are previewed and confirmed; out-of-energy is never a scold"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3,1fr)',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(160deg,rgba(45,212,191,0.12),#0F172A)',
      border: '1px solid rgba(45,212,191,0.3)',
      borderRadius: 16,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 10,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 30
    }
  }, "\uD83D\uDD0D"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 15,
      color: '#F8FAFC'
    }
  }, "Use \u26A13 for an explanation?"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, "Balance after: ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF'
    }
  }, "177 \u26A1"), " left"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      width: '100%',
      marginTop: 4
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 36,
      borderRadius: 11,
      border: '1px solid rgba(255,255,255,0.15)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 12,
      fontWeight: 800,
      color: '#CBD5E1'
    }
  }, "Not now"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1.3,
      height: 36,
      borderRadius: 11,
      background: 'linear-gradient(135deg,#2DD4BF,#14B8A6)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 12,
      fontWeight: 800,
      color: '#06302B'
    }
  }, "Use \u26A13 \u2192")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#5eead4',
      fontWeight: 700,
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginTop: 2
    }
  }, "Cost preview & confirm")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(56,189,248,0.10)',
      border: '1px solid rgba(56,189,248,0.3)',
      borderRadius: 16,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 9,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 32
    }
  }, "\uD83D\uDE34"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 15,
      color: '#F8FAFC'
    }
  }, "Lexi needs a rest!"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      lineHeight: 1.5
    }
  }, "Used all ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#38BDF8'
    }
  }, "20"), " helpers today. Energy is fine \u2014 back tomorrow."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 6,
      background: '#0F172A',
      borderRadius: 9999,
      padding: '6px 13px',
      fontWeight: 800,
      fontSize: 12,
      color: '#38BDF8'
    }
  }, "\uD83C\uDF19 Resets in 6h 12m"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#7DD3FC',
      fontWeight: 700,
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginTop: 2
    }
  }, "Daily cap reached")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(168,85,247,0.10)',
      border: '1px solid rgba(168,85,247,0.3)',
      borderRadius: 16,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 9,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 32
    }
  }, "\uD83D\uDD0C"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 15,
      color: '#F8FAFC'
    }
  }, "Out of energy"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      lineHeight: 1.5
    }
  }, "This month's energy is used up. A grown-up can add more."), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 36,
      padding: '0 16px',
      borderRadius: 11,
      background: 'linear-gradient(135deg,#A855F7,#7C3AED)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 12,
      fontWeight: 800,
      color: '#fff'
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67 Ask a parent"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#C4B5FD',
      fontWeight: 700,
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginTop: 2
    }
  }, "Monthly balance empty"))))));
}
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/parent-dashboard/PagesApp.jsx", error: String((e && e.message) || e) }); }

// ui_kits/parent-dashboard/PagesPublic.jsx
try { (() => {
// Learnexia Web — public (pre-auth) pages: Landing, Login, Register

const webFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};

// ────────────────────────────────────────────────────────────── LANDING
function LandingPage({
  onLogin,
  onSignup
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      minHeight: '100%',
      background: '#0F172A',
      color: '#F8FAFC',
      ...webFont
    }
  }, /*#__PURE__*/React.createElement("nav", {
    style: {
      position: 'sticky',
      top: 0,
      zIndex: 10,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '18px 48px',
      background: 'rgba(15,23,42,0.85)',
      backdropFilter: 'blur(20px)',
      borderBottom: '1px solid rgba(255,255,255,0.05)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/logo-mark.svg",
    style: {
      width: 36,
      height: 36
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20
    }
  }, "Learnexia")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 28,
      fontSize: 14,
      fontWeight: 600
    }
  }, /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#CBD5E1',
      cursor: 'pointer'
    }
  }, "How it works"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#CBD5E1',
      cursor: 'pointer'
    }
  }, "Subjects"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#CBD5E1',
      cursor: 'pointer'
    }
  }, "For schools"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#CBD5E1',
      cursor: 'pointer'
    }
  }, "Pricing")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: onLogin,
    style: btnGhost()
  }, "Log in"), /*#__PURE__*/React.createElement("button", {
    onClick: onSignup,
    style: btnPrimary()
  }, "Start free"))), /*#__PURE__*/React.createElement("section", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1.1fr 1fr',
      gap: 48,
      padding: '72px 48px 96px',
      alignItems: 'center',
      position: 'relative',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: -80,
      left: -80,
      width: 480,
      height: 480,
      borderRadius: '50%',
      background: 'radial-gradient(circle, rgba(168,85,247,0.25) 0%, transparent 65%)',
      pointerEvents: 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'flex',
      flexDirection: 'column',
      gap: 24
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      alignSelf: 'flex-start',
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '6px 14px',
      borderRadius: 9999,
      background: 'rgba(168,85,247,0.15)',
      color: '#A855F7',
      fontWeight: 800,
      fontSize: 12,
      letterSpacing: '0.06em',
      textTransform: 'uppercase',
      border: '1px solid rgba(168,85,247,0.3)'
    }
  }, "\u2728 Powered by AI"), /*#__PURE__*/React.createElement("h1", {
    style: {
      margin: 0,
      fontWeight: 900,
      fontSize: 64,
      lineHeight: 1.05,
      letterSpacing: '-0.03em'
    }
  }, "An ", /*#__PURE__*/React.createElement("span", {
    style: {
      background: 'linear-gradient(90deg,#FACC15,#FB923C)',
      WebkitBackgroundClip: 'text',
      WebkitTextFillColor: 'transparent',
      backgroundClip: 'text'
    }
  }, "adventure game"), " your kids will love \u2014 that teaches."), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      fontSize: 18,
      lineHeight: 1.55,
      color: '#CBD5E1',
      maxWidth: 520
    }
  }, "Learnexia mixes a personal AI tutor with hearts, streaks, XP and badges. Kids learn Math, Science, English and Arabic by playing \u2014 you watch them grow."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      marginTop: 8
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: onSignup,
    style: {
      ...btnPrimary(),
      height: 56,
      padding: '0 28px',
      fontSize: 16
    }
  }, "Create parent account \u2192"), /*#__PURE__*/React.createElement("button", {
    style: {
      ...btnGhost(),
      height: 56,
      padding: '0 24px',
      fontSize: 15
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      marginRight: 8
    }
  }, "\u25B6"), " Watch demo (2 min)")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 28,
      marginTop: 16,
      fontSize: 13,
      color: '#94A3B8',
      fontWeight: 600
    }
  }, /*#__PURE__*/React.createElement("span", null, "\u2B50 4.9 in App Store"), /*#__PURE__*/React.createElement("span", null, "\uD83D\uDEE1\uFE0F COPPA-compliant"), /*#__PURE__*/React.createElement("span", null, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC66 Free for first child"))), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'flex',
      justifyContent: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 320,
      height: 640,
      borderRadius: 44,
      background: 'linear-gradient(165deg,#A855F7 0%,#4F46E5 50%,#1E293B 100%)',
      border: '8px solid #1a1a1a',
      boxShadow: '0 40px 100px rgba(99,102,241,0.5), 0 0 0 1px rgba(255,255,255,0.05)',
      display: 'flex',
      flexDirection: 'column',
      padding: 24,
      gap: 16,
      transform: 'rotate(-4deg)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22
    }
  }, "Sami"), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '4px 10px',
      borderRadius: 9999,
      background: 'rgba(251,146,60,0.2)',
      color: '#FB923C',
      fontWeight: 800,
      fontSize: 12
    }
  }, "\uD83D\uDD25 7")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(0,0,0,0.3)',
      borderRadius: 18,
      padding: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#FACC15',
      letterSpacing: '0.1em',
      textTransform: 'uppercase'
    }
  }, "Continue learning"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      marginTop: 4
    }
  }, "Fractions"), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 6,
      background: 'rgba(0,0,0,0.4)',
      borderRadius: 9999,
      marginTop: 10,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '60%',
      background: 'linear-gradient(90deg,#22C55E,#FACC15)'
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 10
    }
  }, ['🧮', '🧪', '📖', '🇬🇧'].map((e, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      background: 'rgba(0,0,0,0.3)',
      borderRadius: 16,
      padding: 12,
      fontSize: 26
    }
  }, e))), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 'auto',
      textAlign: 'center',
      fontSize: 32,
      animation: 'lxpulse 2s ease-in-out infinite'
    }
  }, "\uD83C\uDF1F")), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 80,
      right: -30,
      background: '#22C55E',
      color: '#0F172A',
      fontWeight: 800,
      fontSize: 13,
      padding: '8px 14px',
      borderRadius: 9999,
      boxShadow: '0 8px 24px rgba(34,197,94,0.4)',
      transform: 'rotate(8deg)'
    }
  }, "+50 XP \u2B50"), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: 100,
      left: -40,
      background: 'rgba(15,23,42,0.85)',
      backdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.1)',
      color: '#fff',
      fontWeight: 700,
      fontSize: 12,
      padding: '10px 14px',
      borderRadius: 16,
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      boxShadow: '0 12px 28px rgba(0,0,0,0.4)',
      transform: 'rotate(-4deg)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18
    }
  }, "\uD83C\uDFC6"), " New badge!"))), /*#__PURE__*/React.createElement("section", {
    style: {
      padding: '32px 48px 96px',
      background: '#0B1020'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 56
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 12,
      color: '#A855F7',
      letterSpacing: '0.12em',
      textTransform: 'uppercase'
    }
  }, "Why Learnexia"), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: '8px 0 0',
      fontWeight: 900,
      fontSize: 44,
      letterSpacing: '-0.02em'
    }
  }, "Built for kids. Trusted by parents.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3, 1fr)',
      gap: 20
    }
  }, /*#__PURE__*/React.createElement(Feature, {
    icon: "\uD83E\uDD16",
    iconBg: "rgba(168,85,247,0.15)",
    color: "#A855F7",
    title: "AI tutor that explains",
    body: "Stuck on a problem? Lexi explains it with pictures, examples and patient follow-ups \u2014 adapted to your child's grade."
  }), /*#__PURE__*/React.createElement(Feature, {
    icon: "\uD83C\uDFAE",
    iconBg: "rgba(251,146,60,0.15)",
    color: "#FB923C",
    title: "Gamified, not gimmicky",
    body: "Streaks, XP, badges and weekly leagues turn practice into a game your child wants to come back to."
  }), /*#__PURE__*/React.createElement(Feature, {
    icon: "\uD83D\uDCCA",
    iconBg: "rgba(34,197,94,0.15)",
    color: "#22C55E",
    title: "Parents stay in the loop",
    body: "Weekly reports tell you exactly where they're flying and where they need help. No guesswork."
  }), /*#__PURE__*/React.createElement(Feature, {
    icon: "\uD83C\uDF0D",
    iconBg: "rgba(56,189,248,0.15)",
    color: "#38BDF8",
    title: "Arabic + English, native",
    body: "Full RTL support, native Arabic content, and bilingual lessons designed by curriculum experts."
  }), /*#__PURE__*/React.createElement(Feature, {
    icon: "\uD83D\uDEE1\uFE0F",
    iconBg: "rgba(250,204,21,0.15)",
    color: "#FACC15",
    title: "Safe and ad-free",
    body: "No ads, no DMs, no data resold. COPPA-compliant from day one. You add your kids, no one else can."
  }), /*#__PURE__*/React.createElement(Feature, {
    icon: "\u26A1",
    iconBg: "rgba(79,70,229,0.15)",
    color: "#A5B4FC",
    title: "5 minutes a day works",
    body: "Short, focused lessons are designed to fit the attention span of a 6\u201314 year old. Big effects, small sessions."
  }))), /*#__PURE__*/React.createElement("section", {
    style: {
      padding: '64px 48px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 32
    }
  }, /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: 0,
      fontWeight: 900,
      fontSize: 36,
      letterSpacing: '-0.02em'
    }
  }, "Four subjects. One adventure.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(4,1fr)',
      gap: 14
    }
  }, [{
    emoji: '🧮',
    name: 'Math',
    color: '#4F46E5',
    topics: 'Numbers · Fractions · Geometry'
  }, {
    emoji: '🧪',
    name: 'Science',
    color: '#22C55E',
    topics: 'Plants · States · Space'
  }, {
    emoji: '📖',
    name: 'Arabic',
    color: '#FB923C',
    topics: 'Reading · Grammar · Poetry'
  }, {
    emoji: '🇬🇧',
    name: 'English',
    color: '#A855F7',
    topics: 'Phonics · Verbs · Stories'
  }].map((s, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      background: '#1E293B',
      borderRadius: 24,
      padding: 24,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 56,
      height: 56,
      borderRadius: 18,
      background: `${s.color}22`,
      color: s.color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 30
    }
  }, s.emoji), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22
    }
  }, s.name), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8'
    }
  }, s.topics), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 8,
      color: s.color,
      fontWeight: 700,
      fontSize: 13
    }
  }, "Grade 1\u20136 \u2192"))))), /*#__PURE__*/React.createElement("section", {
    style: {
      padding: '32px 48px 64px',
      background: '#0B1020'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 48
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 12,
      color: '#22C55E',
      letterSpacing: '0.12em',
      textTransform: 'uppercase'
    }
  }, "For parents"), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: '8px 0 0',
      fontWeight: 900,
      fontSize: 44,
      letterSpacing: '-0.02em'
    }
  }, "See exactly what they're learning."), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: '12px auto 0',
      fontSize: 17,
      color: '#CBD5E1',
      maxWidth: 560,
      lineHeight: 1.55
    }
  }, "No guesswork. Every lesson, streak and weak spot rolls up into a weekly picture you can act on.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1.3fr',
      gap: 20,
      marginBottom: 20,
      alignItems: 'stretch'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      overflow: 'hidden',
      borderRadius: 28,
      background: 'linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)',
      padding: 36,
      display: 'flex',
      flexDirection: 'column',
      gap: 22
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 52,
      lineHeight: 1,
      filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.4))'
    }
  }, "\uD83C\uDFAE"), /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontWeight: 900,
      fontSize: 26,
      lineHeight: 1.2,
      color: '#fff'
    }
  }, "Set up once. Watch them learn forever."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, [['✨', 'AI explanations tailored to each child\'s grade'], ['📊', 'Weekly reports show exactly what they\'ve mastered'], ['🎯', 'Daily missions keep them coming back without nagging'], ['🛡️', 'COPPA-compliant — no ads, no DMs, no data resold']].map(([emoji, text], i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      fontSize: 15,
      color: 'rgba(255,255,255,0.92)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      background: 'rgba(255,255,255,0.1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20,
      flexShrink: 0
    }
  }, emoji), text)))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 28,
      padding: 28,
      display: 'flex',
      flexDirection: 'column'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      marginBottom: 20
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, "Sami's week at a glance"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "XP earned per day \xB7 today in indigo")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      padding: '6px 12px',
      borderRadius: 9999,
      background: 'rgba(34,197,94,0.15)',
      color: '#22C55E',
      fontWeight: 800,
      fontSize: 13
    }
  }, "\u2191 28% this week")), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      display: 'flex',
      gap: 12,
      alignItems: 'flex-end',
      minHeight: 220
    }
  }, [{
    day: 'Mon',
    xp: 45
  }, {
    day: 'Tue',
    xp: 80
  }, {
    day: 'Wed',
    xp: 30
  }, {
    day: 'Thu',
    xp: 95
  }, {
    day: 'Fri',
    xp: 50
  }, {
    day: 'Sat',
    xp: 70
  }, {
    day: 'Sun',
    xp: 110,
    today: true
  }].map((d, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      flex: 1,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: `${d.xp / 110 * 180}px`,
      background: d.today ? 'linear-gradient(180deg,#A855F7,#4F46E5)' : 'linear-gradient(180deg,#334155,#1E293B)',
      borderRadius: '10px 10px 4px 4px',
      position: 'relative',
      boxShadow: d.today ? '0 6px 18px rgba(99,102,241,0.4)' : 'none'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: -22,
      left: '50%',
      transform: 'translateX(-50%)',
      fontSize: 12,
      fontWeight: 800,
      color: d.today ? '#A5B4FC' : '#64748B',
      fontVariantNumeric: 'tabular-nums'
    }
  }, d.xp)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 700,
      color: d.today ? '#F8FAFC' : '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, d.day)))))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1.3fr 1fr',
      gap: 20,
      alignItems: 'stretch'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 28,
      padding: 32,
      display: 'flex',
      flexDirection: 'column',
      gap: 20
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, "A patient tutor, always on"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "Lexi explains with pictures and follow-ups \u2014 never just the answer.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      alignItems: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 56,
      height: 56,
      borderRadius: '50%',
      background: 'linear-gradient(135deg,#A78BFA,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      flexShrink: 0,
      boxShadow: '0 8px 20px rgba(99,102,241,0.4)'
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/mascot-owl.svg",
    style: {
      width: 46,
      height: 46
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(15,23,42,0.7)',
      border: '1px solid rgba(255,255,255,0.1)',
      borderRadius: 22,
      borderBottomLeftRadius: 4,
      padding: '16px 20px',
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 11,
      color: '#A5B4FC',
      textTransform: 'uppercase',
      letterSpacing: '0.08em',
      marginBottom: 6
    }
  }, "Lexi \xB7 AI Tutor"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 15,
      lineHeight: 1.55,
      color: '#F8FAFC'
    }
  }, "When we compare two numbers, the one with more ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#FACC15'
    }
  }, "tens"), " is bigger. Want me to show you with blocks?"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      marginTop: 12,
      flexWrap: 'wrap'
    }
  }, ['Yes, show me', 'Give a hint'].map((c, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      fontSize: 13,
      fontWeight: 600,
      color: '#A5B4FC',
      background: 'rgba(79,70,229,0.18)',
      border: '1px solid rgba(99,102,241,0.3)',
      padding: '6px 12px',
      borderRadius: 9999
    }
  }, c)))))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      borderRadius: 28,
      padding: 28,
      display: 'flex',
      flexDirection: 'column',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, "Every child, one tap away"), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#15161D',
      borderRadius: 20,
      padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: '50%',
      background: '#FB923C',
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 22,
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.18)'
    }
  }, "S"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "Sami"), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 8px',
      borderRadius: 9999,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      fontWeight: 800,
      fontSize: 11
    }
  }, "Grade 3")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "sami@learnexia.com")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      fontSize: 11,
      color: '#22C55E',
      fontWeight: 700
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: '#22C55E',
      boxShadow: '0 0 6px rgba(34,197,94,0.6)'
    }
  }), "Active")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 14,
      alignItems: 'center'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#A855F7'
    }
  }, "\uD83E\uDDE0 Lv 12"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#FACC15'
    }
  }, "\u2B50 1,240"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#FB923C'
    }
  }, "\uD83D\uDD25 7d")), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: 6
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, "Mastery"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12,
      fontWeight: 800,
      color: '#F8FAFC'
    }
  }, "72%")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 8,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '72%',
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)'
    }
  })))), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#CBD5E1'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8'
    }
  }, "Weakest:"), " ", /*#__PURE__*/React.createElement("b", null, "Fractions"), " \u2014 Lexi is already adapting.")))), /*#__PURE__*/React.createElement("section", {
    style: {
      padding: '32px 48px 96px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 56
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 12,
      color: '#22C55E',
      letterSpacing: '0.12em',
      textTransform: 'uppercase'
    }
  }, "For Parents"), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: '8px 0 0',
      fontWeight: 900,
      fontSize: 44,
      letterSpacing: '-0.02em'
    }
  }, "See exactly what your child gets out of it.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1.2fr',
      gap: 32,
      alignItems: 'start'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)',
      borderRadius: 28,
      padding: 40,
      display: 'flex',
      flexDirection: 'column',
      gap: 24,
      boxShadow: '0 24px 60px rgba(91,33,182,0.35), inset 0 1px 0 rgba(255,255,255,0.15)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 64,
      lineHeight: 1,
      filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.5))'
    }
  }, "\uD83C\uDFAE"), /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontWeight: 900,
      fontSize: 30,
      lineHeight: 1.15,
      letterSpacing: '-0.02em'
    }
  }, "Set up once. Watch them learn forever."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, [['✨', 'AI-powered explanations tailored to each child\'s grade'], ['📊', 'Weekly reports show exactly what they\'ve mastered'], ['🎯', 'Daily missions keep them coming back without nagging'], ['🛡️', 'COPPA-compliant — no ads, no DMs, no data resold']].map(([emoji, text], i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      fontSize: 15,
      color: 'rgba(255,255,255,0.92)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      flexShrink: 0,
      background: 'rgba(255,255,255,0.1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20
    }
  }, emoji), text)))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 20
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 24,
      padding: 24,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'baseline',
      marginBottom: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16
    }
  }, "Your weekly report"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#22C55E',
      fontWeight: 800
    }
  }, "+28% vs last week")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10,
      alignItems: 'flex-end',
      height: 120
    }
  }, [45, 80, 30, 95, 50, 70, 110].map((xp, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      flex: 1,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: `${xp / 110 * 95}px`,
      background: i === 6 ? 'linear-gradient(180deg,#A855F7,#4F46E5)' : 'linear-gradient(180deg,#334155,#1E293B)',
      borderRadius: '8px 8px 3px 3px',
      boxShadow: i === 6 ? '0 6px 18px rgba(99,102,241,0.4)' : 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      fontWeight: 700,
      color: i === 6 ? '#F8FAFC' : '#64748B',
      textTransform: 'uppercase'
    }
  }, ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][i]))))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      alignItems: 'flex-end'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 56,
      height: 56,
      borderRadius: '50%',
      flexShrink: 0,
      background: 'linear-gradient(135deg,#A78BFA,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      boxShadow: '0 8px 20px rgba(99,102,241,0.4)'
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/mascot-owl.svg",
    style: {
      width: 46,
      height: 46
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(15,23,42,0.75)',
      backdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.1)',
      borderRadius: 22,
      borderBottomLeftRadius: 4,
      padding: '16px 20px',
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 11,
      color: '#A5B4FC',
      textTransform: 'uppercase',
      letterSpacing: '0.08em',
      marginBottom: 4
    }
  }, "Lexi \xB7 AI Tutor"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 15,
      lineHeight: 1.55,
      color: '#F8FAFC'
    }
  }, "When we compare two numbers, the one with more ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#FACC15'
    }
  }, "tens"), " is bigger. Want me to show you with blocks?"))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#15161D',
      borderRadius: 20,
      padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: '50%',
      background: '#FB923C',
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 22,
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.18)',
      flexShrink: 0
    }
  }, "S"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 18
    }
  }, "Sami"), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 8px',
      borderRadius: 9999,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      fontWeight: 800,
      fontSize: 11
    }
  }, "Grade 3")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "sami@learnexia.com")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      fontSize: 11,
      color: '#22C55E',
      fontWeight: 700
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: '#22C55E',
      boxShadow: '0 0 6px rgba(34,197,94,0.6)'
    }
  }), "Active today")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#A855F7'
    }
  }, "\uD83E\uDDE0 Lv 12"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#FACC15'
    }
  }, "\u2B50 1,240"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#FB923C'
    }
  }, "\uD83D\uDD25 7d"), /*#__PURE__*/React.createElement("span", {
    style: {
      marginRight: 0,
      marginLeft: 'auto',
      color: '#A5B4FC',
      fontWeight: 700,
      fontSize: 12
    }
  }, "View progress \u2192")))))), /*#__PURE__*/React.createElement("section", {
    style: {
      padding: '32px 48px 96px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#4F46E5 0%,#A855F7 100%)',
      borderRadius: 32,
      padding: '56px 48px',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      gap: 32,
      boxShadow: '0 24px 60px rgba(99,102,241,0.45), inset 0 1px 0 rgba(255,255,255,0.2)',
      position: 'relative',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      right: 40,
      bottom: -40,
      fontSize: 280,
      opacity: 0.15
    }
  }, "\uD83C\uDF1F"), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 36,
      color: '#fff',
      letterSpacing: '-0.02em',
      lineHeight: 1.1
    }
  }, "Ready to start the adventure?"), /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 10,
      fontSize: 16,
      color: 'rgba(255,255,255,0.9)'
    }
  }, "Free for your first child \xB7 No credit card required")), /*#__PURE__*/React.createElement("button", {
    onClick: onSignup,
    style: {
      height: 60,
      padding: '0 32px',
      borderRadius: 16,
      border: 'none',
      background: '#fff',
      color: '#4F46E5',
      fontFamily: 'inherit',
      fontWeight: 900,
      fontSize: 17,
      cursor: 'pointer',
      boxShadow: '0 16px 32px rgba(0,0,0,0.25)',
      whiteSpace: 'nowrap'
    }
  }, "Create parent account \u2192"))), /*#__PURE__*/React.createElement("footer", {
    style: {
      padding: '40px 48px',
      borderTop: '1px solid rgba(255,255,255,0.05)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      color: '#64748B',
      fontSize: 13
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/logo-mark.svg",
    style: {
      width: 28,
      height: 28,
      opacity: 0.7
    }
  }), /*#__PURE__*/React.createElement("span", null, "\xA9 2026 Learnexia \xB7 Made for curious kids")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 24,
      fontWeight: 600
    }
  }, /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#94A3B8',
      cursor: 'pointer'
    }
  }, "Privacy"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#94A3B8',
      cursor: 'pointer'
    }
  }, "Terms"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#94A3B8',
      cursor: 'pointer'
    }
  }, "Support"), /*#__PURE__*/React.createElement("a", {
    style: {
      color: '#94A3B8',
      cursor: 'pointer'
    }
  }, "\u0627\u0644\u0639\u0631\u0628\u064A\u0629"))));
}
function Feature({
  icon,
  iconBg,
  color,
  title,
  body
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 24,
      padding: 28,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: 16,
      background: iconBg,
      color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 26
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, title), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      lineHeight: 1.55,
      color: '#CBD5E1'
    }
  }, body));
}

// ────────────────────────────────────────────────────────────── LOGIN (web)
function LoginWebPage({
  onLogin,
  onRegister,
  onLanding
}) {
  const [role, setRole] = React.useState('parent');
  const [showPw, setShowPw] = React.useState(false);
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const canSubmit = email.includes('@') && pw.length >= 4;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      minHeight: '100%',
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      background: '#0F172A',
      color: '#F8FAFC',
      ...webFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      overflow: 'hidden',
      background: 'linear-gradient(165deg,#4F3FB0 0%,#3B2C8F 50%,#1E1B4B 100%)',
      padding: 56,
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'space-between'
    }
  }, [...Array(12)].map((_, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      position: 'absolute',
      top: `${i * 73 % 100}%`,
      left: `${i * 41 % 100}%`,
      width: i % 3 + 3,
      height: i % 3 + 3,
      borderRadius: '50%',
      background: '#fff',
      opacity: 0.2 + i % 4 / 10,
      boxShadow: `0 0 ${i % 3 * 4 + 6}px rgba(255,255,255,0.5)`
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      cursor: 'pointer'
    },
    onClick: onLanding
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/logo-mark.svg",
    style: {
      width: 40,
      height: 40
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22
    }
  }, "Learnexia")), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 96,
      animation: 'lxpulse 2.4s ease-in-out infinite',
      filter: 'drop-shadow(0 0 24px rgba(250,204,21,0.5))'
    }
  }, "\uD83C\uDF1F"), /*#__PURE__*/React.createElement("h1", {
    style: {
      margin: '20px 0 0',
      fontWeight: 900,
      fontSize: 48,
      lineHeight: 1.1,
      letterSpacing: '-0.02em'
    }
  }, "Welcome back to the adventure."), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: '14px 0 0',
      fontSize: 16,
      color: 'rgba(255,255,255,0.8)',
      maxWidth: 380
    }
  }, "Pick up your streak, keep your hearts full, and watch your kids fly through new skills.")), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      color: 'rgba(255,255,255,0.6)',
      fontSize: 13
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18
    }
  }, "\uD83D\uDD25"), " 240,000+ kids learning today")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '56px',
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center',
      maxWidth: 520,
      margin: '0 auto',
      width: '100%'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 32
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.12em'
    }
  }, "Log in"), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: '8px 0 4px',
      fontWeight: 900,
      fontSize: 32,
      letterSpacing: '-0.02em'
    }
  }, "Welcome back"), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      fontSize: 14,
      color: '#94A3B8'
    }
  }, "Log in to keep your streak alive \uD83D\uDD25")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      padding: 4,
      background: '#1E293B',
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.06)',
      marginBottom: 18
    }
  }, [{
    id: 'parent',
    label: 'I\'m a Parent',
    emoji: '👨‍👩‍👦'
  }, {
    id: 'student',
    label: 'I\'m a Student',
    emoji: '🎓'
  }].map(r => /*#__PURE__*/React.createElement("button", {
    key: r.id,
    onClick: () => setRole(r.id),
    style: {
      flex: 1,
      padding: '12px 12px',
      borderRadius: 10,
      border: 'none',
      background: role === r.id ? '#4F46E5' : 'transparent',
      color: role === r.id ? '#fff' : '#94A3B8',
      fontFamily: 'inherit',
      fontWeight: 700,
      fontSize: 14,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 8,
      boxShadow: role === r.id ? '0 4px 12px rgba(99,102,241,0.35)' : 'none',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("span", null, r.emoji), r.label))), /*#__PURE__*/React.createElement(WebField, {
    label: "Email"
  }, /*#__PURE__*/React.createElement("input", {
    type: "email",
    value: email,
    onChange: e => setEmail(e.target.value),
    placeholder: role === 'parent' ? 'parent@email.com' : 'sami@learnexia.com',
    style: webInputStyle()
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Password",
    right: /*#__PURE__*/React.createElement("button", {
      onClick: () => setShowPw(!showPw),
      style: textBtn()
    }, showPw ? 'Hide' : 'Show')
  }, /*#__PURE__*/React.createElement("input", {
    type: showPw ? 'text' : 'password',
    value: pw,
    onChange: e => setPw(e.target.value),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022",
    style: webInputStyle()
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      margin: '4px 0 20px'
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      cursor: 'pointer',
      fontSize: 13,
      color: '#CBD5E1'
    }
  }, /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    style: {
      accentColor: '#4F46E5'
    }
  }), " Remember me"), /*#__PURE__*/React.createElement("button", {
    style: textBtn()
  }, "Forgot password?")), /*#__PURE__*/React.createElement("button", {
    onClick: canSubmit ? onLogin : undefined,
    disabled: !canSubmit,
    style: {
      ...btnPrimary(),
      height: 52,
      fontSize: 16,
      background: canSubmit ? '#4F46E5' : '#2A2D3E',
      color: canSubmit ? '#fff' : '#64748B',
      cursor: canSubmit ? 'pointer' : 'not-allowed',
      boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none'
    }
  }, "Log in \u2192"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      margin: '24px 0'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(255,255,255,0.08)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 600,
      color: '#64748B'
    }
  }, "OR CONTINUE WITH"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(255,255,255,0.08)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement(WebSocialButton, {
    provider: "google"
  }), /*#__PURE__*/React.createElement(WebSocialButton, {
    provider: "apple"
  }), /*#__PURE__*/React.createElement(WebSocialButton, {
    provider: "microsoft"
  })), role === 'parent' ? /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginTop: 28,
      fontSize: 14,
      color: '#94A3B8'
    }
  }, "New to Learnexia?", ' ', /*#__PURE__*/React.createElement("button", {
    onClick: onRegister,
    style: {
      ...textBtn(),
      fontSize: 14,
      fontWeight: 800
    }
  }, "Create parent account")) : /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 28,
      padding: '14px 16px',
      borderRadius: 14,
      background: 'rgba(245,158,11,0.08)',
      border: '1px solid rgba(245,158,11,0.25)',
      fontSize: 13,
      color: '#CBD5E1',
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#F59E0B',
      fontWeight: 800
    }
  }, "Need an account?"), " Ask a parent to add you \u2014 kids can't self-register.")));
}

// ────────────────────────────────────────────────────────────── REGISTER (web)
function RegisterWebPage({
  onRegister,
  onLogin,
  onLanding
}) {
  const [name, setName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const [country, setCountry] = React.useState('SA');
  const [agreed, setAgreed] = React.useState(false);
  const canSubmit = name.trim().length > 1 && email.includes('@') && pw.length >= 6 && agreed;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      minHeight: '100%',
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      background: '#0F172A',
      color: '#F8FAFC',
      ...webFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '56px',
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center',
      maxWidth: 560,
      margin: '0 auto',
      width: '100%'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      marginBottom: 32,
      cursor: 'pointer'
    },
    onClick: onLanding
  }, /*#__PURE__*/React.createElement("img", {
    src: "../../assets/logo-mark.svg",
    style: {
      width: 36,
      height: 36
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20
    }
  }, "Learnexia")), /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 28
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.12em'
    }
  }, "Step 1 of 2"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 4,
      background: '#1E293B',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '50%',
      background: 'linear-gradient(90deg,#A855F7,#4F46E5)'
    }
  }))), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: '16px 0 4px',
      fontWeight: 900,
      fontSize: 32,
      letterSpacing: '-0.02em'
    }
  }, "Create your parent account"), /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      fontSize: 14,
      color: '#94A3B8'
    }
  }, "You'll add your children's accounts next.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      marginBottom: 20,
      padding: '12px 16px',
      borderRadius: 14,
      background: 'rgba(168,85,247,0.1)',
      border: '1px solid rgba(168,85,247,0.3)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 26
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC66"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#A855F7'
    }
  }, "Parent / Guardian only"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "Children can't self-register. You'll create their accounts in the next step."))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: '1fr 1fr',
      gap: 14,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement(WebField, {
    label: "Full name"
  }, /*#__PURE__*/React.createElement("input", {
    value: name,
    onChange: e => setName(e.target.value),
    placeholder: "Ahmed Hassan",
    style: webInputStyle()
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Country"
  }, /*#__PURE__*/React.createElement("select", {
    value: country,
    onChange: e => setCountry(e.target.value),
    style: {
      ...webInputStyle(),
      appearance: 'none',
      cursor: 'pointer',
      backgroundImage: 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'12\' height=\'8\' viewBox=\'0 0 12 8\'><path fill=\'%2394A3B8\' d=\'M6 8L0 0h12z\'/></svg>")',
      backgroundRepeat: 'no-repeat',
      backgroundPosition: 'right 14px center',
      paddingRight: 36
    }
  }, /*#__PURE__*/React.createElement("option", {
    value: "SA"
  }, "\uD83C\uDDF8\uD83C\uDDE6 Saudi Arabia"), /*#__PURE__*/React.createElement("option", {
    value: "AE"
  }, "\uD83C\uDDE6\uD83C\uDDEA UAE"), /*#__PURE__*/React.createElement("option", {
    value: "EG"
  }, "\uD83C\uDDEA\uD83C\uDDEC Egypt"), /*#__PURE__*/React.createElement("option", {
    value: "JO"
  }, "\uD83C\uDDEF\uD83C\uDDF4 Jordan"), /*#__PURE__*/React.createElement("option", {
    value: "QA"
  }, "\uD83C\uDDF6\uD83C\uDDE6 Qatar"), /*#__PURE__*/React.createElement("option", {
    value: "KW"
  }, "\uD83C\uDDF0\uD83C\uDDFC Kuwait"), /*#__PURE__*/React.createElement("option", {
    value: "US"
  }, "\uD83C\uDDFA\uD83C\uDDF8 United States"), /*#__PURE__*/React.createElement("option", {
    value: "GB"
  }, "\uD83C\uDDEC\uD83C\uDDE7 United Kingdom")))), /*#__PURE__*/React.createElement(WebField, {
    label: "Email"
  }, /*#__PURE__*/React.createElement("input", {
    type: "email",
    value: email,
    onChange: e => setEmail(e.target.value),
    placeholder: "parent@email.com",
    style: webInputStyle()
  })), /*#__PURE__*/React.createElement(WebField, {
    label: "Password",
    hint: "At least 6 characters"
  }, /*#__PURE__*/React.createElement("input", {
    type: "password",
    value: pw,
    onChange: e => setPw(e.target.value),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022",
    style: webInputStyle()
  })), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 12,
      padding: '14px 16px',
      borderRadius: 14,
      marginTop: 8,
      background: agreed ? 'rgba(34,197,94,0.06)' : '#1E293B',
      border: agreed ? '1px solid rgba(34,197,94,0.3)' : '1px solid rgba(255,255,255,0.06)',
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 22,
      height: 22,
      borderRadius: 6,
      flexShrink: 0,
      marginTop: 2,
      background: agreed ? '#22C55E' : 'transparent',
      border: agreed ? 'none' : '2px solid rgba(255,255,255,0.2)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      color: '#0F172A',
      fontWeight: 900,
      fontSize: 13
    }
  }, agreed && '✓'), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#CBD5E1',
      lineHeight: 1.5
    }
  }, "I'm a parent or legal guardian and I agree to the", ' ', /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "Terms"), " and", ' ', /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "Privacy Policy"), ", including consent to create accounts for my children."), /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    checked: agreed,
    onChange: e => setAgreed(e.target.checked),
    style: {
      display: 'none'
    }
  })), /*#__PURE__*/React.createElement("button", {
    onClick: canSubmit ? onRegister : undefined,
    disabled: !canSubmit,
    style: {
      ...btnPrimary(),
      height: 52,
      fontSize: 16,
      marginTop: 18,
      background: canSubmit ? '#4F46E5' : '#2A2D3E',
      color: canSubmit ? '#fff' : '#64748B',
      cursor: canSubmit ? 'pointer' : 'not-allowed',
      boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none'
    }
  }, "Continue \u2192 Add Children"), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginTop: 20,
      fontSize: 14,
      color: '#94A3B8'
    }
  }, "Already have an account?", ' ', /*#__PURE__*/React.createElement("button", {
    onClick: onLogin,
    style: {
      ...textBtn(),
      fontSize: 14,
      fontWeight: 800
    }
  }, "Log in"))), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      overflow: 'hidden',
      background: 'linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)',
      padding: 56,
      display: 'flex',
      flexDirection: 'column',
      justifyContent: 'center',
      gap: 28
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 96,
      lineHeight: 1,
      filter: 'drop-shadow(0 0 24px rgba(250,204,21,0.5))'
    }
  }, "\uD83C\uDFAE"), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: 0,
      fontWeight: 900,
      fontSize: 40,
      lineHeight: 1.15,
      letterSpacing: '-0.02em',
      maxWidth: 460
    }
  }, "Set up once. Watch them learn forever."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 14,
      maxWidth: 460
    }
  }, [['✨', 'AI-powered explanations tailored to each child\'s grade'], ['📊', 'Weekly reports show exactly what they\'ve mastered'], ['🎯', 'Daily missions keep them coming back without nagging'], ['🛡️', 'COPPA-compliant — no ads, no DMs, no data resold']].map(([emoji, text], i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      fontSize: 15,
      color: 'rgba(255,255,255,0.92)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      background: 'rgba(255,255,255,0.1)',
      backdropFilter: 'blur(8px)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20,
      flexShrink: 0
    }
  }, emoji), text)))));
}

// ────────────────────────────────────────────────────────────── helpers
function WebField({
  label,
  right,
  hint,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      marginBottom: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 700,
      color: '#CBD5E1',
      letterSpacing: '0.04em'
    }
  }, label), right), children, hint && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8'
    }
  }, hint));
}
function webInputStyle() {
  return {
    height: 48,
    background: '#1E293B',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 14,
    color: '#F8FAFC',
    fontFamily: 'Poppins, system-ui, sans-serif',
    fontSize: 15,
    fontWeight: 500,
    padding: '0 14px',
    width: '100%',
    outline: 'none'
  };
}
function btnPrimary() {
  return {
    height: 40,
    padding: '0 18px',
    borderRadius: 12,
    border: 'none',
    background: '#4F46E5',
    color: '#fff',
    fontFamily: 'Poppins, system-ui, sans-serif',
    fontWeight: 700,
    fontSize: 14,
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)'
  };
}
function btnGhost() {
  return {
    height: 40,
    padding: '0 18px',
    borderRadius: 12,
    background: 'transparent',
    color: '#CBD5E1',
    border: '1px solid rgba(255,255,255,0.12)',
    fontFamily: 'Poppins, system-ui, sans-serif',
    fontWeight: 600,
    fontSize: 14,
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center'
  };
}
function textBtn() {
  return {
    background: 'transparent',
    border: 'none',
    color: '#A5B4FC',
    fontFamily: 'Poppins, system-ui, sans-serif',
    fontWeight: 600,
    fontSize: 12,
    cursor: 'pointer',
    padding: 0
  };
}
function WebSocialButton({
  provider
}) {
  const data = {
    google: {
      icon: 'G',
      label: 'Google',
      bg: '#fff',
      fg: '#0F172A'
    },
    apple: {
      icon: '🍎',
      label: 'Apple',
      bg: '#1E293B',
      fg: '#F8FAFC'
    },
    microsoft: {
      icon: '⊞',
      label: 'Microsoft',
      bg: '#1E293B',
      fg: '#F8FAFC'
    }
  }[provider];
  return /*#__PURE__*/React.createElement("button", {
    style: {
      flex: 1,
      height: 48,
      borderRadius: 14,
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.08)',
      color: '#F8FAFC',
      fontFamily: 'Poppins, system-ui, sans-serif',
      fontWeight: 700,
      fontSize: 13,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 22,
      height: 22,
      borderRadius: '50%',
      background: data.bg,
      color: data.fg,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 13
    }
  }, data.icon), data.label);
}
Object.assign(window, {
  LandingPage,
  LoginWebPage,
  RegisterWebPage,
  WebField,
  webInputStyle,
  btnPrimary,
  btnGhost,
  textBtn,
  WebSocialButton,
  Feature
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/parent-dashboard/PagesPublic.jsx", error: String((e && e.message) || e) }); }

// ui_kits/parent-dashboard/browser-window.jsx
try { (() => {
// Chrome.jsx — Simplified Chrome browser window (dark theme, macOS)
// No dependencies, no image assets. All inline styles + inline SVG.

const CHROME_C = {
  barBg: '#202124',
  tabBg: '#35363a',
  text: '#e8eaed',
  dim: '#9aa0a6',
  urlBg: '#282a2d'
};
function ChromeTrafficLights() {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      padding: '0 14px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 12,
      height: 12,
      borderRadius: '50%',
      background: '#ff5f57'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 12,
      height: 12,
      borderRadius: '50%',
      background: '#febc2e'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 12,
      height: 12,
      borderRadius: '50%',
      background: '#28c840'
    }
  }));
}

// Single tab (active has curved scoops)
function ChromeTab({
  title = 'New Tab',
  active = false
}) {
  const curve = flip => /*#__PURE__*/React.createElement("svg", {
    width: "8",
    height: "10",
    viewBox: "0 0 8 10",
    style: {
      position: 'absolute',
      bottom: 0,
      [flip ? 'right' : 'left']: -8,
      transform: flip ? 'scaleX(-1)' : 'none'
    }
  }, /*#__PURE__*/React.createElement("path", {
    d: "M0 10C2 9 6 8 8 0V10H0Z",
    fill: CHROME_C.tabBg
  }));
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      height: 34,
      alignSelf: 'flex-end',
      padding: '0 12px',
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      background: active ? CHROME_C.tabBg : 'transparent',
      borderRadius: '8px 8px 0 0',
      minWidth: 120,
      maxWidth: 220,
      fontFamily: 'system-ui, sans-serif',
      fontSize: 12,
      color: active ? CHROME_C.text : CHROME_C.dim
    }
  }, active && curve(false), active && curve(true), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 14,
      height: 14,
      borderRadius: '50%',
      background: '#5f6368',
      flexShrink: 0
    }
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1,
      whiteSpace: 'nowrap',
      overflow: 'hidden',
      textOverflow: 'ellipsis'
    }
  }, title));
}
function ChromeTabBar({
  tabs = [{
    title: 'New Tab'
  }],
  activeIndex = 0
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      height: 44,
      background: CHROME_C.barBg,
      paddingRight: 8
    }
  }, /*#__PURE__*/React.createElement(ChromeTrafficLights, null), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-end',
      height: '100%',
      paddingLeft: 4,
      flex: 1
    }
  }, tabs.map((t, i) => /*#__PURE__*/React.createElement(ChromeTab, {
    key: i,
    title: t.title,
    active: i === activeIndex
  }))));
}
function ChromeToolbar({
  url = 'example.com'
}) {
  const iconDot = /*#__PURE__*/React.createElement("div", {
    style: {
      width: 28,
      height: 28,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 16,
      height: 16,
      borderRadius: '50%',
      background: CHROME_C.dim,
      opacity: 0.4
    }
  }));
  return /*#__PURE__*/React.createElement("div", {
    style: {
      height: 40,
      background: CHROME_C.tabBg,
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      padding: '0 8px'
    }
  }, iconDot, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 30,
      borderRadius: 15,
      background: CHROME_C.urlBg,
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '0 14px',
      margin: '0 6px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 12,
      height: 12,
      borderRadius: '50%',
      background: CHROME_C.dim,
      opacity: 0.4
    }
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1,
      color: CHROME_C.text,
      fontSize: 13,
      fontFamily: 'system-ui, sans-serif'
    }
  }, url)), iconDot);
}
function ChromeWindow({
  tabs = [{
    title: 'New Tab'
  }],
  activeIndex = 0,
  url = 'example.com',
  width = 900,
  height = 600,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width,
      height,
      borderRadius: 10,
      overflow: 'hidden',
      boxShadow: '0 24px 80px rgba(0,0,0,0.35), 0 0 0 1px rgba(0,0,0,0.1)',
      display: 'flex',
      flexDirection: 'column',
      background: CHROME_C.tabBg
    }
  }, /*#__PURE__*/React.createElement(ChromeTabBar, {
    tabs: tabs,
    activeIndex: activeIndex
  }), /*#__PURE__*/React.createElement(ChromeToolbar, {
    url: url
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      background: '#fff',
      overflow: 'auto'
    }
  }, children));
}
Object.assign(window, {
  ChromeWindow,
  ChromeTabBar,
  ChromeToolbar,
  ChromeTab,
  ChromeTrafficLights
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/parent-dashboard/browser-window.jsx", error: String((e && e.message) || e) }); }

// ui_kits/student-mobile/MobileComponents.jsx
try { (() => {
// Learnexia mobile UI primitives — buttons, HUD, cards, etc.
// All styling references CSS variables from /colors_and_type.css.

const lxFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};
function HudBar({
  streak = 7,
  hearts = 4,
  xp = 1240,
  gems = 42,
  energy = 180,
  onEnergy
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      alignItems: 'center',
      padding: '0 16px',
      ...lxFont
    }
  }, /*#__PURE__*/React.createElement(Pill, {
    icon: "\uD83D\uDD25",
    value: streak,
    color: "#FB923C"
  }), /*#__PURE__*/React.createElement(Pill, {
    icon: "\u2764\uFE0F",
    value: hearts,
    color: "#FB7185"
  }), /*#__PURE__*/React.createElement(Pill, {
    icon: "\u2B50",
    value: xp.toLocaleString(),
    color: "#FACC15"
  }), /*#__PURE__*/React.createElement(Pill, {
    icon: "\u26A1",
    value: energy,
    color: "#2DD4BF",
    onClick: onEnergy
  }));
}
function Pill({
  icon,
  value,
  color,
  onClick
}) {
  const Tag = onClick ? 'button' : 'div';
  return /*#__PURE__*/React.createElement(Tag, {
    onClick: onClick,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      padding: '7px 12px',
      borderRadius: 9999,
      border: 'none',
      background: `${color}22`,
      color,
      fontWeight: 800,
      fontSize: 14,
      fontVariantNumeric: 'tabular-nums',
      fontFamily: 'inherit',
      cursor: onClick ? 'pointer' : 'default',
      boxShadow: onClick ? `0 0 0 1px ${color}55` : 'none'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 15
    }
  }, icon), value);
}
function XPBar({
  value = 0.65,
  label
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      ...lxFont
    }
  }, label && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 10,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden',
      border: '1px solid rgba(255,255,255,0.06)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${value * 100}%`,
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)',
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
      transition: 'width 600ms cubic-bezier(0.16,1,0.3,1)'
    }
  })));
}
function PrimaryButton({
  children,
  onClick,
  variant = 'primary',
  full = false,
  style = {}
}) {
  const variants = {
    primary: {
      bg: '#4F46E5',
      fg: '#fff',
      glow: 'rgba(99,102,241,0.4)'
    },
    success: {
      bg: '#22C55E',
      fg: '#0F172A',
      glow: 'rgba(34,197,94,0.3)'
    },
    danger: {
      bg: '#EF4444',
      fg: '#fff',
      glow: 'rgba(239,68,68,0.35)'
    },
    secondary: {
      bg: '#334155',
      fg: '#F8FAFC',
      glow: 'rgba(0,0,0,0.15)'
    },
    purple: {
      bg: '#A855F7',
      fg: '#fff',
      glow: 'rgba(168,85,247,0.4)'
    },
    ghost: {
      bg: 'transparent',
      fg: '#CBD5E1',
      glow: 'transparent'
    }
  };
  const v = variants[variant];
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    style: {
      height: 52,
      padding: '0 24px',
      width: full ? '100%' : undefined,
      borderRadius: 16,
      border: variant === 'ghost' ? '1px solid rgba(255,255,255,0.16)' : 'none',
      background: v.bg,
      color: v.fg,
      fontFamily: 'Poppins, system-ui, sans-serif',
      fontWeight: 700,
      fontSize: 16,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 8,
      boxShadow: variant === 'ghost' ? 'none' : `0 4px 12px ${v.glow}, inset 0 1px 0 rgba(255,255,255,0.18)`,
      transition: 'transform 120ms cubic-bezier(0.16,1,0.3,1)',
      ...style
    },
    onPointerDown: e => e.currentTarget.style.transform = 'scale(0.95)',
    onPointerUp: e => e.currentTarget.style.transform = 'scale(1)',
    onPointerLeave: e => e.currentTarget.style.transform = 'scale(1)'
  }, children);
}
function LessonCard({
  tag,
  title,
  meta,
  progress,
  state = 'active',
  onClick
}) {
  const stateStyles = {
    active: {
      border: '2px solid #4F46E5',
      shadow: '0 8px 24px rgba(99,102,241,0.25)',
      opacity: 1
    },
    completed: {
      border: '1px solid rgba(34,197,94,0.3)',
      shadow: '0 4px 12px rgba(0,0,0,0.15)',
      opacity: 1
    },
    locked: {
      border: '1px solid rgba(255,255,255,0.06)',
      shadow: 'none',
      opacity: 0.55
    }
  }[state];
  return /*#__PURE__*/React.createElement("div", {
    onClick: state === 'locked' ? undefined : onClick,
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      gap: 10,
      cursor: state === 'locked' ? 'not-allowed' : 'pointer',
      position: 'relative',
      ...stateStyles,
      ...lxFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      alignSelf: 'flex-start',
      padding: '4px 10px',
      borderRadius: 9999,
      fontWeight: 700,
      fontSize: 10,
      letterSpacing: '0.08em',
      textTransform: 'uppercase',
      color: state === 'completed' ? '#22C55E' : '#A5B4FC',
      background: state === 'completed' ? 'rgba(34,197,94,0.18)' : 'rgba(79,70,229,0.2)'
    }
  }, tag), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 18,
      color: '#F8FAFC',
      lineHeight: 1.2
    }
  }, title), meta && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, meta), progress !== undefined && /*#__PURE__*/React.createElement("div", {
    style: {
      height: 6,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden',
      marginTop: 2
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${progress * 100}%`,
      background: state === 'completed' ? '#22C55E' : 'linear-gradient(90deg,#4F46E5,#A855F7)'
    }
  })), state === 'locked' && /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 14,
      right: 16,
      fontSize: 18
    }
  }, "\uD83D\uDD12"));
}
function MissionRow({
  icon,
  iconBg,
  title,
  sub,
  value,
  total,
  reward,
  done
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      background: '#1E293B',
      borderRadius: 20,
      padding: '14px 16px',
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      ...lxFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 44,
      height: 44,
      borderRadius: 14,
      background: iconBg,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20,
      flexShrink: 0
    }
  }, done ? '✓' : icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, title), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      marginTop: 2
    }
  }, sub), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 5,
      background: '#0F172A',
      borderRadius: 9999,
      marginTop: 8,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${value / total * 100}%`,
      background: done ? '#22C55E' : 'linear-gradient(90deg,#22C55E,#4F46E5)'
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: done ? 'rgba(34,197,94,0.18)' : 'rgba(245,158,11,0.18)',
      color: done ? '#22C55E' : '#F59E0B',
      padding: '6px 10px',
      borderRadius: 9999,
      fontWeight: 800,
      fontSize: 13,
      whiteSpace: 'nowrap'
    }
  }, "\u2B50 +", reward));
}
function AnswerButton({
  children,
  state = 'default',
  keyLetter,
  onClick
}) {
  const styles = {
    default: {
      border: '2px solid rgba(255,255,255,0.08)',
      bg: '#1E293B',
      fg: '#F8FAFC'
    },
    selected: {
      border: '2px solid #4F46E5',
      bg: 'rgba(79,70,229,0.15)',
      fg: '#F8FAFC'
    },
    correct: {
      border: '2px solid #22C55E',
      bg: 'rgba(34,197,94,0.15)',
      fg: '#22C55E'
    },
    wrong: {
      border: '2px solid #EF4444',
      bg: 'rgba(239,68,68,0.15)',
      fg: '#EF4444'
    }
  }[state];
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    style: {
      ...styles,
      ...lxFont,
      borderRadius: 16,
      padding: '14px 16px',
      background: styles.bg,
      color: styles.fg,
      fontWeight: 600,
      fontSize: 16,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      cursor: 'pointer',
      textAlign: 'left',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("span", null, children, state === 'correct' && ' ✓', state === 'wrong' && ' ✗'), /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'ui-monospace, monospace',
      fontSize: 11,
      color: '#94A3B8',
      background: 'rgba(255,255,255,0.06)',
      padding: '2px 7px',
      borderRadius: 6
    }
  }, keyLetter));
}
function TabBar({
  active,
  onChange
}) {
  const tabs = [{
    id: 'home',
    icon: '🏠',
    label: 'Home'
  }, {
    id: 'skills',
    icon: '🌳',
    label: 'Skills'
  }, {
    id: 'mission',
    icon: '🎯',
    label: 'Quests'
  }, {
    id: 'league',
    icon: '🏆',
    label: 'League'
  }, {
    id: 'profile',
    icon: '👤',
    label: 'Me'
  }];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      left: 12,
      right: 12,
      bottom: 38,
      height: 64,
      borderRadius: 22,
      background: 'rgba(15,23,42,0.75)',
      backdropFilter: 'blur(20px)',
      WebkitBackdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.08)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-around',
      boxShadow: '0 8px 32px rgba(0,0,0,0.5)',
      ...lxFont
    }
  }, tabs.map(t => /*#__PURE__*/React.createElement("button", {
    key: t.id,
    onClick: () => onChange(t.id),
    style: {
      background: 'transparent',
      border: 'none',
      cursor: 'pointer',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 2,
      color: active === t.id ? '#A5B4FC' : '#64748B',
      fontWeight: 700,
      fontSize: 10,
      padding: '4px 8px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 22,
      filter: active === t.id ? 'none' : 'grayscale(0.6) opacity(0.7)'
    }
  }, t.icon), t.label)));
}
function MascotAvatar({
  size = 64
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: size,
      height: size,
      borderRadius: '50%',
      background: 'linear-gradient(135deg,#A78BFA,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      boxShadow: '0 8px 20px rgba(99,102,241,0.4)',
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement("img", {
    src: window.__resources && window.__resources.mascotOwl || "../../assets/mascot-owl.svg",
    style: {
      width: size * 0.85,
      height: size * 0.85
    }
  }));
}
function TutorBubble({
  children,
  chips = [],
  onChip
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10,
      alignItems: 'flex-end',
      ...lxFont
    }
  }, /*#__PURE__*/React.createElement(MascotAvatar, {
    size: 52
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(15,23,42,0.75)',
      backdropFilter: 'blur(20px)',
      WebkitBackdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.1)',
      borderRadius: 20,
      borderBottomLeftRadius: 4,
      padding: '14px 16px',
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 10,
      color: '#A5B4FC',
      textTransform: 'uppercase',
      letterSpacing: '0.08em',
      marginBottom: 4
    }
  }, "Lexi \xB7 AI Tutor"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      lineHeight: 1.5,
      color: '#F8FAFC'
    }
  }, children), chips.length > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6,
      marginTop: 10,
      flexWrap: 'wrap'
    }
  }, chips.map((c, i) => /*#__PURE__*/React.createElement("button", {
    key: i,
    onClick: () => onChip && onChip(c),
    style: {
      fontSize: 12,
      fontWeight: 600,
      color: '#A5B4FC',
      background: 'rgba(79,70,229,0.18)',
      border: '1px solid rgba(99,102,241,0.3)',
      padding: '5px 10px',
      borderRadius: 9999,
      cursor: 'pointer',
      fontFamily: 'inherit'
    }
  }, c)))));
}
Object.assign(window, {
  HudBar,
  Pill,
  XPBar,
  PrimaryButton,
  LessonCard,
  MissionRow,
  AnswerButton,
  TabBar,
  MascotAvatar,
  TutorBubble
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/student-mobile/MobileComponents.jsx", error: String((e && e.message) || e) }); }

// ui_kits/student-mobile/Screens.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
// Learnexia student mobile — individual screens.
// All assume a 402×874 iPhone canvas behind them.

const screenFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};
function ScreenShell({
  children,
  scroll = true,
  padTop = 60,
  padBottom = 120
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      paddingTop: padTop,
      paddingBottom: padBottom,
      overflow: scroll ? 'auto' : 'hidden',
      ...screenFont
    }
  }, children);
}

// ───────────────────────────────────────────── HOME
function HomeScreen({
  onContinue,
  onMission,
  onEnergy
}) {
  return /*#__PURE__*/React.createElement(ScreenShell, null, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement(HudBar, {
    streak: 7,
    hearts: 4,
    xp: 1240,
    energy: 180,
    onEnergy: onEnergy
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px',
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement(MascotAvatar, {
    size: 56
  }), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      fontWeight: 600
    }
  }, "Welcome back,"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 22,
      fontWeight: 800,
      color: '#F8FAFC',
      lineHeight: 1.1
    }
  }, "Sami!"))), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 12,
      padding: '0 20px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 22,
      color: '#F8FAFC'
    }
  }, "Continue Learning"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20
    }
  }, "\uD83D\uDCDA")), /*#__PURE__*/React.createElement("button", {
    style: {
      background: 'transparent',
      border: 'none',
      color: '#94A3B8',
      fontWeight: 600,
      fontSize: 13,
      cursor: 'pointer',
      fontFamily: 'inherit'
    }
  }, "See all")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12,
      overflowX: 'auto',
      paddingBottom: 4,
      scrollbarWidth: 'none',
      WebkitOverflowScrolling: 'touch',
      padding: '0 16px 4px'
    }
  }, /*#__PURE__*/React.createElement(SubjectCard, {
    emoji: "\uD83E\uDDEE",
    subject: "Math",
    topic: "Fractions",
    pct: 60
  }), /*#__PURE__*/React.createElement(SubjectCard, {
    emoji: "\uD83E\uDDEA",
    subject: "Science",
    topic: "Plants",
    pct: 35
  }), /*#__PURE__*/React.createElement(SubjectCard, {
    emoji: "\uD83C\uDDEC\uD83C\uDDE7",
    subject: "English",
    topic: "Verbs",
    pct: 48
  }), /*#__PURE__*/React.createElement(SubjectCard, {
    emoji: "\uD83D\uDCD6",
    subject: "Arabic",
    topic: "Reading",
    pct: 72
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16,
      color: '#F8FAFC'
    }
  }, "Daily Quests"), /*#__PURE__*/React.createElement("button", {
    onClick: onMission,
    style: {
      background: 'transparent',
      border: 'none',
      color: '#A5B4FC',
      fontWeight: 700,
      fontSize: 12,
      cursor: 'pointer',
      fontFamily: 'inherit'
    }
  }, "See all \u2192")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement(MissionRow, {
    icon: "\uD83C\uDFAF",
    iconBg: "rgba(79,70,229,0.2)",
    title: "Get 10 right in a row",
    sub: "6 of 10",
    value: 6,
    total: 10,
    reward: 50
  }), /*#__PURE__*/React.createElement(MissionRow, {
    icon: "\uD83D\uDD25",
    iconBg: "rgba(251,146,60,0.2)",
    title: "Practice 3 days in a row",
    sub: "2 of 3",
    value: 2,
    total: 3,
    reward: 30
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 16,
      border: '1px solid rgba(255,255,255,0.06)',
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: '50%',
      background: 'radial-gradient(circle at 30% 30%,#FBBF24,#B45309)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 26,
      boxShadow: '0 6px 16px rgba(180,83,9,0.5)'
    }
  }, "\uD83C\uDFC6"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, "Bronze League"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, "Rank #4 \xB7 240 XP to promotion")), /*#__PURE__*/React.createElement("div", {
    style: {
      color: '#94A3B8',
      fontSize: 20
    }
  }, "\u203A")))));
}

// ───────────────────────────────────────────── SKILL TREE
function SkillTreeScreen({
  onStart
}) {
  const nodes = [{
    state: 'complete',
    label: 'Numbers',
    stars: 3
  }, {
    state: 'complete',
    label: 'Counting',
    stars: 3
  }, {
    state: 'complete',
    label: 'Compare',
    stars: 2
  }, {
    state: 'active',
    label: 'Addition',
    stars: 0
  }, {
    state: 'locked',
    label: 'Subtract',
    stars: 0
  }, {
    state: 'locked',
    label: 'Fractions',
    stars: 0
  }];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 12px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 26,
      color: '#F8FAFC'
    }
  }, "Math \xB7 Numbers"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "Unit 2 of 8 \xB7 Mastery 45%")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '24px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 24,
      alignItems: 'center',
      position: 'relative'
    }
  }, nodes.map((n, i) => /*#__PURE__*/React.createElement(SkillNode, _extends({
    key: i
  }, n, {
    index: i,
    onClick: n.state === 'active' ? onStart : undefined
  })))));
}
function SkillNode({
  state,
  label,
  stars,
  index,
  onClick
}) {
  const offset = [-60, -20, 30, 60, 20, -40][index % 6];
  const styles = {
    complete: {
      bg: 'radial-gradient(circle at 30% 30%,#86EFAC,#22C55E)',
      shadow: '0 8px 20px rgba(34,197,94,0.45)',
      icon: '✓'
    },
    active: {
      bg: 'radial-gradient(circle at 30% 30%,#A5B4FC,#4F46E5)',
      shadow: '0 0 32px rgba(99,102,241,0.7)',
      icon: '✏️'
    },
    locked: {
      bg: '#334155',
      shadow: 'inset 0 1px 0 rgba(255,255,255,0.06)',
      icon: '🔒'
    }
  }[state];
  return /*#__PURE__*/React.createElement("div", {
    onClick: onClick,
    style: {
      transform: `translateX(${offset}px)`,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 6,
      cursor: state === 'active' ? 'pointer' : 'default'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 80,
      height: 80,
      borderRadius: '50%',
      background: styles.bg,
      boxShadow: styles.shadow,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 32,
      color: state === 'locked' ? '#64748B' : '#fff',
      animation: state === 'active' ? 'lxpulse 2s ease-in-out infinite' : 'none'
    }
  }, styles.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 13,
      color: state === 'locked' ? '#64748B' : '#F8FAFC'
    }
  }, label), state === 'complete' && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#FACC15'
    }
  }, '⭐'.repeat(stars)));
}

// ───────────────────────────────────────────── LESSON
function LessonScreen({
  onAsk,
  onStart
}) {
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 10,
      color: '#A5B4FC',
      textTransform: 'uppercase',
      letterSpacing: '0.1em'
    }
  }, "Math \xB7 Numbers \xB7 Lesson 3"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 26,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, "Compare Bigger", /*#__PURE__*/React.createElement("br", null), "& Smaller")), /*#__PURE__*/React.createElement(TutorBubble, {
    chips: ['Yes, show me', 'Give a hint', 'Skip'],
    onChip: onAsk
  }, "When we compare two numbers, the one with more ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#FACC15'
    }
  }, "tens"), " is bigger. Want me to show you with blocks?"), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 13,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, "Example"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 20,
      justifyContent: 'center'
    }
  }, /*#__PURE__*/React.createElement(NumberBlocks, {
    tens: 2,
    ones: 7,
    color: "#4F46E5"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      alignSelf: 'center',
      fontSize: 28,
      fontWeight: 800,
      color: '#FACC15'
    }
  }, "<"), /*#__PURE__*/React.createElement(NumberBlocks, {
    tens: 5,
    ones: 4,
    color: "#22C55E"
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-around',
      fontWeight: 800,
      fontSize: 22,
      color: '#F8FAFC'
    }
  }, /*#__PURE__*/React.createElement("div", null, "27"), /*#__PURE__*/React.createElement("div", null, "54"))), /*#__PURE__*/React.createElement(PrimaryButton, {
    full: true,
    onClick: onStart
  }, "Start Quiz \xB7 5 questions")));
}
function NumberBlocks({
  tens,
  ones,
  color
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6
    }
  }, Array.from({
    length: tens
  }).map((_, i) => /*#__PURE__*/React.createElement("div", {
    key: 't' + i,
    style: {
      width: 10,
      height: 50,
      background: color,
      borderRadius: 3,
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column-reverse',
      gap: 2,
      flexWrap: 'wrap',
      height: 50
    }
  }, Array.from({
    length: ones
  }).map((_, i) => /*#__PURE__*/React.createElement("div", {
    key: 'o' + i,
    style: {
      width: 8,
      height: 8,
      background: color,
      borderRadius: 2
    }
  }))));
}

// ───────────────────────────────────────────── QUIZ
function QuizScreen({
  onComplete
}) {
  const [selected, setSelected] = React.useState(null);
  const [revealed, setRevealed] = React.useState(false);
  const answers = [{
    id: 'A',
    label: '2',
    correct: false
  }, {
    id: 'B',
    label: '4',
    correct: true
  }, {
    id: 'C',
    label: '6',
    correct: false
  }];
  const check = () => setRevealed(true);
  const next = () => onComplete();
  const answeredCorrect = revealed && answers.find(a => a.id === selected)?.correct;
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 56
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 22,
      color: '#F8FAFC'
    }
  }, "Quiz Time"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20
    }
  }, "\u26A1")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 4
    }
  }, [1, 2, 3, 4, 5].map(i => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      fontSize: 18,
      filter: i <= 5 ? 'drop-shadow(0 0 4px rgba(239,68,68,0.5))' : 'grayscale(1) opacity(0.3)'
    }
  }, "\u2764\uFE0F")))), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 15,
      color: '#CBD5E1'
    }
  }, "Question 1 / 5"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 15,
      color: '#CBD5E1',
      fontVariantNumeric: 'tabular-nums'
    }
  }, "20%")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 6,
      background: '#1E2030',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '20%',
      background: 'linear-gradient(90deg,#F59E0B,#EF4444)'
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1A1C26',
      borderRadius: 20,
      padding: '20px 18px 28px',
      border: '1px solid rgba(255,255,255,0.04)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      fontSize: 12,
      fontWeight: 700,
      color: '#94A3B8',
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
      marginBottom: 18
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: "18",
    height: "18",
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "#94A3B8",
    strokeWidth: "1.8",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M9.5 2A2.5 2.5 0 0 1 12 4.5v15a2.5 2.5 0 0 1-4.96.44 2.5 2.5 0 0 1-2.96-3.08 3 3 0 0 1-.34-5.58 2.5 2.5 0 0 1 1.32-4.24 2.5 2.5 0 0 1 1.98-3A2.5 2.5 0 0 1 9.5 2Z"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M14.5 2A2.5 2.5 0 0 0 12 4.5v15a2.5 2.5 0 0 0 4.96.44 2.5 2.5 0 0 0 2.96-3.08 3 3 0 0 0 .34-5.58 2.5 2.5 0 0 0-1.32-4.24 2.5 2.5 0 0 0-1.98-3A2.5 2.5 0 0 0 14.5 2Z"
  })), "Math \xB7 Fractions"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC',
      textAlign: 'center',
      lineHeight: 1.2
    }
  }, "What is 1/2 of 8?")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, answers.map(a => {
    let state = 'default';
    if (revealed) {
      if (a.correct) state = 'correct';else if (selected === a.id) state = 'wrong';
    } else if (selected === a.id) state = 'selected';
    return /*#__PURE__*/React.createElement(QuizAnswer, {
      key: a.id,
      answer: a,
      state: state,
      onClick: () => !revealed && setSelected(a.id)
    });
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 8,
      fontWeight: 700,
      fontSize: 16,
      color: '#F59E0B'
    }
  }, "XP Earned: ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#FACC15',
      fontWeight: 800
    }
  }, "+20"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18,
      filter: 'drop-shadow(0 0 6px rgba(250,204,21,0.5))'
    }
  }, "\u2B50")), !revealed ? /*#__PURE__*/React.createElement("button", {
    onClick: selected ? check : undefined,
    disabled: !selected,
    style: {
      height: 56,
      borderRadius: 16,
      border: 'none',
      background: selected ? '#4F46E5' : '#2A2D3E',
      color: selected ? '#fff' : '#64748B',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: selected ? 'pointer' : 'not-allowed',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10,
      boxShadow: selected ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none'
    }
  }, "Submit Answer \u2192") : /*#__PURE__*/React.createElement("button", {
    onClick: next,
    style: {
      height: 56,
      borderRadius: 16,
      border: 'none',
      background: answeredCorrect ? '#22C55E' : '#4F46E5',
      color: '#fff',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10,
      boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)'
    }
  }, "Continue \u2192")));
}
function QuizAnswer({
  answer,
  state,
  onClick
}) {
  const styles = {
    default: {
      border: '#4F46E5',
      bg: '#1A1C26',
      dotBg: '#0F1018',
      dotFg: '#A5B4FC'
    },
    selected: {
      border: '#A855F7',
      bg: 'rgba(168,85,247,0.08)',
      dotBg: '#A855F7',
      dotFg: '#fff'
    },
    correct: {
      border: '#22C55E',
      bg: 'rgba(34,197,94,0.10)',
      dotBg: '#22C55E',
      dotFg: '#0F172A'
    },
    wrong: {
      border: '#EF4444',
      bg: 'rgba(239,68,68,0.10)',
      dotBg: '#EF4444',
      dotFg: '#fff'
    }
  }[state];
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      padding: '16px 18px 16px 14px',
      background: styles.bg,
      borderRadius: 16,
      border: 'none',
      borderLeft: `4px solid ${styles.border}`,
      fontFamily: 'inherit',
      cursor: 'pointer',
      textAlign: 'left',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)',
      animation: state === 'wrong' ? 'lxshake 0.3s' : 'none'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 36,
      height: 36,
      borderRadius: '50%',
      background: styles.dotBg,
      color: styles.dotFg,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 800,
      fontSize: 15,
      flexShrink: 0
    }
  }, answer.id), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontWeight: 800,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, answer.label), state === 'correct' && /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#22C55E',
      fontSize: 22
    }
  }, "\u2713"), state === 'wrong' && /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#EF4444',
      fontSize: 22
    }
  }, "\u2717"));
}

// ───────────────────────────────────────────── REWARD
function RewardScreen({
  onDone
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      position: 'relative',
      background: 'radial-gradient(circle at 50% 35%,rgba(79,70,229,0.55),#0F172A 70%)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 18,
      padding: '60px 24px 80px',
      ...screenFont
    }
  }, Array.from({
    length: 20
  }).map((_, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      position: 'absolute',
      top: `${10 + Math.random() * 60}%`,
      left: `${Math.random() * 100}%`,
      width: 8,
      height: 12,
      background: ['#FACC15', '#FB7185', '#22C55E', '#38BDF8', '#A855F7'][i % 5],
      borderRadius: 2,
      transform: `rotate(${Math.random() * 360}deg)`,
      opacity: 0.85
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 120,
      height: 120,
      borderRadius: '50%',
      background: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 60,
      boxShadow: '0 0 60px rgba(250,204,21,0.7), inset 0 -8px 16px rgba(0,0,0,0.2)',
      animation: 'lxpop 700ms cubic-bezier(0.34,1.56,0.64,1)'
    }
  }, "\uD83C\uDFC6"), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 32,
      color: '#F8FAFC'
    }
  }, "Lesson Complete!"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#CBD5E1',
      marginTop: 4
    }
  }, "Streak protected \xB7 5 of 5 correct")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(30,41,59,0.85)',
      backdropFilter: 'blur(20px)',
      border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 24,
      padding: '18px 24px',
      display: 'flex',
      gap: 22,
      alignItems: 'center',
      boxShadow: '0 16px 36px rgba(0,0,0,0.5)'
    }
  }, /*#__PURE__*/React.createElement(Stat, {
    icon: "\u2B50",
    value: "+50",
    label: "XP",
    color: "#FACC15"
  }), /*#__PURE__*/React.createElement(Stat, {
    icon: "\uD83D\uDD25",
    value: "8 days",
    label: "Streak",
    color: "#FB923C"
  }), /*#__PURE__*/React.createElement(Stat, {
    icon: "\uD83C\uDFC6",
    value: "+1",
    label: "Badge",
    color: "#FBBF24"
  })), /*#__PURE__*/React.createElement(PrimaryButton, {
    onClick: onDone,
    variant: "primary",
    style: {
      marginTop: 6,
      minWidth: 200
    }
  }, "Keep Going"));
}
function Stat({
  icon,
  value,
  label,
  color
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      minWidth: 70
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 24,
      marginBottom: 2
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color,
      fontVariantNumeric: 'tabular-nums'
    }
  }, value), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      fontWeight: 700,
      marginTop: 2
    }
  }, label));
}
function SubjectCard({
  emoji,
  subject,
  topic,
  pct
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      flex: '0 0 160px',
      minWidth: 160,
      background: '#1E293B',
      borderRadius: 20,
      padding: 14,
      border: '1px solid rgba(99,102,241,0.45)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      flexDirection: 'column',
      gap: 8,
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 32
    }
  }, emoji), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 19,
      color: '#F8FAFC',
      lineHeight: 1
    }
  }, subject), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8'
    }
  }, topic, " \xB7 ", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#CBD5E1',
      fontWeight: 700,
      fontVariantNumeric: 'tabular-nums'
    }
  }, pct, "%")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 4,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden',
      marginTop: 2
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${pct}%`,
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)'
    }
  })));
}
Object.assign(window, {
  HomeScreen,
  SkillTreeScreen,
  LessonScreen,
  QuizScreen,
  RewardScreen,
  ScreenShell,
  SubjectCard
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/student-mobile/Screens.jsx", error: String((e && e.message) || e) }); }

// ui_kits/student-mobile/ScreensAuth.jsx
try { (() => {
// Learnexia — Auth + Parent My Children screens.
// Per spec (P1-03, P1-04): parent registers → adds children with assigned login email,
// grade (1-6), language (AR/EN), country. Children only log in, never self-register.

const authFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};

// ───────────────────────────────────────────── LOGIN
function LoginScreen({
  onLogin,
  onRegister
}) {
  const [role, setRole] = React.useState('parent'); // 'parent' | 'student'
  const [showPw, setShowPw] = React.useState(false);
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const canSubmit = email.includes('@') && pw.length >= 4;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      position: 'relative',
      background: '#0F172A',
      ...authFont,
      overflow: 'auto'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: -180,
      left: '50%',
      transform: 'translateX(-50%)',
      width: 460,
      height: 360,
      borderRadius: '50%',
      background: 'radial-gradient(circle, rgba(168,85,247,0.35) 0%, rgba(168,85,247,0) 65%)',
      pointerEvents: 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      padding: '70px 24px 32px',
      display: 'flex',
      flexDirection: 'column',
      gap: 20
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 72,
      height: 72,
      borderRadius: 22,
      margin: '0 auto 12px',
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      boxShadow: '0 12px 32px rgba(168,85,247,0.4), inset 0 2px 0 rgba(255,255,255,0.18)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 36,
      filter: 'drop-shadow(0 0 8px rgba(250,204,21,0.6))'
    }
  }, "\uD83C\uDF1F")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 28,
      color: '#F8FAFC',
      letterSpacing: '-0.02em'
    }
  }, "Welcome back"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 6
    }
  }, "Log in to keep your streak alive \uD83D\uDD25")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      padding: 4,
      background: '#15161D',
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.06)'
    }
  }, [{
    id: 'parent',
    label: 'Parent',
    emoji: '👨‍👩‍👦'
  }, {
    id: 'student',
    label: 'Student',
    emoji: '🎓'
  }].map(r => /*#__PURE__*/React.createElement("button", {
    key: r.id,
    onClick: () => setRole(r.id),
    style: {
      flex: 1,
      padding: '10px 12px',
      borderRadius: 10,
      border: 'none',
      background: role === r.id ? '#4F46E5' : 'transparent',
      color: role === r.id ? '#fff' : '#94A3B8',
      fontFamily: 'inherit',
      fontWeight: 700,
      fontSize: 14,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 6,
      boxShadow: role === r.id ? '0 4px 12px rgba(99,102,241,0.35)' : 'none',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("span", null, r.emoji), r.label))), /*#__PURE__*/React.createElement(AuthField, {
    label: "Email",
    icon: "\u2709\uFE0F"
  }, /*#__PURE__*/React.createElement("input", {
    type: "email",
    placeholder: role === 'parent' ? 'parent@email.com' : 'sami@learnexia.com',
    value: email,
    onChange: e => setEmail(e.target.value),
    style: authInputStyle()
  })), /*#__PURE__*/React.createElement(AuthField, {
    label: "Password",
    icon: "\uD83D\uDD12",
    right: /*#__PURE__*/React.createElement("button", {
      onClick: () => setShowPw(!showPw),
      style: {
        background: 'transparent',
        border: 'none',
        color: '#A5B4FC',
        fontFamily: 'inherit',
        fontWeight: 600,
        fontSize: 12,
        cursor: 'pointer',
        padding: 0
      }
    }, showPw ? 'Hide' : 'Show')
  }, /*#__PURE__*/React.createElement("input", {
    type: showPw ? 'text' : 'password',
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022",
    value: pw,
    onChange: e => setPw(e.target.value),
    style: authInputStyle()
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end',
      marginTop: -8
    }
  }, /*#__PURE__*/React.createElement("button", {
    style: {
      background: 'transparent',
      border: 'none',
      color: '#A5B4FC',
      fontFamily: 'inherit',
      fontWeight: 600,
      fontSize: 13,
      cursor: 'pointer',
      padding: 4
    }
  }, "Forgot password?")), /*#__PURE__*/React.createElement("button", {
    onClick: canSubmit ? onLogin : undefined,
    disabled: !canSubmit,
    style: {
      height: 56,
      borderRadius: 16,
      border: 'none',
      background: canSubmit ? '#4F46E5' : '#2A2D3E',
      color: canSubmit ? '#fff' : '#64748B',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: canSubmit ? 'pointer' : 'not-allowed',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10,
      boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, "Log In \u2192"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      margin: '4px 0'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(255,255,255,0.08)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 600,
      color: '#64748B'
    }
  }, "OR"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(255,255,255,0.08)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement(SocialButton, {
    label: "Google",
    icon: "G"
  }), /*#__PURE__*/React.createElement(SocialButton, {
    label: "Apple",
    icon: "",
    appleIcon: true
  })), role === 'parent' && /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 8
    }
  }, "New here?", ' ', /*#__PURE__*/React.createElement("button", {
    onClick: onRegister,
    style: {
      background: 'transparent',
      border: 'none',
      color: '#A5B4FC',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 14,
      cursor: 'pointer',
      padding: 0
    }
  }, "Create parent account")), role === 'student' && /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      fontSize: 13,
      color: '#64748B',
      marginTop: 8,
      padding: '12px 14px',
      borderRadius: 12,
      background: 'rgba(245,158,11,0.06)',
      border: '1px solid rgba(245,158,11,0.18)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#F59E0B',
      fontWeight: 700
    }
  }, "Need an account?"), " Ask a parent to add you.")));
}

// ───────────────────────────────────────────── REGISTER (parent-only)
function RegisterScreen({
  onRegister,
  onLogin
}) {
  const [name, setName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [pw, setPw] = React.useState('');
  const [country, setCountry] = React.useState('SA');
  const [agreed, setAgreed] = React.useState(false);
  const pwStrength = scorePw(pw);
  const canSubmit = name.trim().length > 1 && email.includes('@') && pw.length >= 6 && agreed;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      background: '#0F172A',
      ...authFont,
      overflow: 'auto'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '70px 24px 32px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.12em'
    }
  }, "Step 1 of 2"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 28,
      color: '#F8FAFC',
      marginTop: 6,
      letterSpacing: '-0.01em'
    }
  }, "Create parent account"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 6,
      lineHeight: 1.5
    }
  }, "You'll add your children's accounts in the next step.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '12px 14px',
      borderRadius: 14,
      background: 'rgba(168,85,247,0.1)',
      border: '1px solid rgba(168,85,247,0.3)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 26
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC66"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#A855F7'
    }
  }, "Parent / Guardian"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "Only parents can register. Kids get accounts added by you."))), /*#__PURE__*/React.createElement(AuthField, {
    label: "Full name",
    icon: "\uD83D\uDC64"
  }, /*#__PURE__*/React.createElement("input", {
    value: name,
    onChange: e => setName(e.target.value),
    placeholder: "e.g. Ahmed Hassan",
    style: authInputStyle()
  })), /*#__PURE__*/React.createElement(AuthField, {
    label: "Email",
    icon: "\u2709\uFE0F"
  }, /*#__PURE__*/React.createElement("input", {
    type: "email",
    value: email,
    onChange: e => setEmail(e.target.value),
    placeholder: "parent@email.com",
    style: authInputStyle()
  })), /*#__PURE__*/React.createElement(AuthField, {
    label: "Password",
    icon: "\uD83D\uDD12"
  }, /*#__PURE__*/React.createElement("input", {
    type: "password",
    value: pw,
    onChange: e => setPw(e.target.value),
    placeholder: "At least 6 characters",
    style: authInputStyle()
  }), pw.length > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 4,
      marginTop: 8
    }
  }, [0, 1, 2, 3].map(i => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      flex: 1,
      height: 4,
      borderRadius: 9999,
      background: i < pwStrength.score ? pwStrength.color : 'rgba(255,255,255,0.08)',
      transition: 'background 200ms'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: pwStrength.color,
      minWidth: 50,
      textAlign: 'right'
    }
  }, pwStrength.label))), /*#__PURE__*/React.createElement(AuthField, {
    label: "Country",
    icon: "\uD83C\uDF0D"
  }, /*#__PURE__*/React.createElement("select", {
    value: country,
    onChange: e => setCountry(e.target.value),
    style: {
      ...authInputStyle(),
      appearance: 'none',
      backgroundImage: 'url("data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' width=\'12\' height=\'8\' viewBox=\'0 0 12 8\'><path fill=\'%2394A3B8\' d=\'M6 8L0 0h12z\'/></svg>")',
      backgroundRepeat: 'no-repeat',
      backgroundPosition: 'right 14px center',
      paddingRight: 36,
      cursor: 'pointer'
    }
  }, /*#__PURE__*/React.createElement("option", {
    value: "SA"
  }, "\uD83C\uDDF8\uD83C\uDDE6 Saudi Arabia"), /*#__PURE__*/React.createElement("option", {
    value: "AE"
  }, "\uD83C\uDDE6\uD83C\uDDEA United Arab Emirates"), /*#__PURE__*/React.createElement("option", {
    value: "EG"
  }, "\uD83C\uDDEA\uD83C\uDDEC Egypt"), /*#__PURE__*/React.createElement("option", {
    value: "JO"
  }, "\uD83C\uDDEF\uD83C\uDDF4 Jordan"), /*#__PURE__*/React.createElement("option", {
    value: "QA"
  }, "\uD83C\uDDF6\uD83C\uDDE6 Qatar"), /*#__PURE__*/React.createElement("option", {
    value: "KW"
  }, "\uD83C\uDDF0\uD83C\uDDFC Kuwait"), /*#__PURE__*/React.createElement("option", {
    value: "OM"
  }, "\uD83C\uDDF4\uD83C\uDDF2 Oman"), /*#__PURE__*/React.createElement("option", {
    value: "BH"
  }, "\uD83C\uDDE7\uD83C\uDDED Bahrain"), /*#__PURE__*/React.createElement("option", {
    value: "US"
  }, "\uD83C\uDDFA\uD83C\uDDF8 United States"), /*#__PURE__*/React.createElement("option", {
    value: "GB"
  }, "\uD83C\uDDEC\uD83C\uDDE7 United Kingdom"))), /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 12,
      padding: '12px 14px',
      borderRadius: 14,
      background: agreed ? 'rgba(34,197,94,0.06)' : '#15161D',
      border: agreed ? '1px solid rgba(34,197,94,0.25)' : '1px solid rgba(255,255,255,0.06)',
      cursor: 'pointer',
      transition: 'all 180ms'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 24,
      height: 24,
      borderRadius: 7,
      flexShrink: 0,
      marginTop: 2,
      background: agreed ? '#22C55E' : 'transparent',
      border: agreed ? 'none' : '2px solid rgba(255,255,255,0.2)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      color: '#0F172A',
      fontWeight: 900,
      fontSize: 14
    }
  }, agreed && '✓'), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontSize: 13,
      color: '#CBD5E1',
      lineHeight: 1.5
    }
  }, "I'm a parent or legal guardian and I agree to the", ' ', /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "Terms"), " and", ' ', /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "Privacy Policy"), ", including consent to create accounts for my children."), /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    checked: agreed,
    onChange: e => setAgreed(e.target.checked),
    style: {
      display: 'none'
    }
  })), /*#__PURE__*/React.createElement("button", {
    onClick: canSubmit ? onRegister : undefined,
    disabled: !canSubmit,
    style: {
      height: 56,
      borderRadius: 16,
      border: 'none',
      background: canSubmit ? '#4F46E5' : '#2A2D3E',
      color: canSubmit ? '#fff' : '#64748B',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: canSubmit ? 'pointer' : 'not-allowed',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10,
      boxShadow: canSubmit ? '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)' : 'none',
      marginTop: 4
    }
  }, "Continue \u2192 Add Children"), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      fontSize: 14,
      color: '#94A3B8'
    }
  }, "Already have an account?", ' ', /*#__PURE__*/React.createElement("button", {
    onClick: onLogin,
    style: {
      background: 'transparent',
      border: 'none',
      color: '#A5B4FC',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 14,
      cursor: 'pointer',
      padding: 0
    }
  }, "Log in"))));
}

// ───────────────────────────────────────────── MY CHILDREN (parent)
function MyChildrenScreen({
  onAddChild,
  onPick
}) {
  const children = [{
    id: 1,
    name: 'Sami',
    initial: 'S',
    color: '#FB923C',
    grade: 3,
    language: 'en',
    country: 'SA',
    email: 'sami@learnexia.com',
    level: 12,
    xp: 1240,
    streak: 7,
    status: 'active'
  }, {
    id: 2,
    name: 'Layla',
    initial: 'L',
    color: '#A855F7',
    grade: 1,
    language: 'ar',
    country: 'SA',
    email: 'layla@learnexia.com',
    level: 4,
    xp: 380,
    streak: 2,
    status: 'active'
  }];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 56
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...authFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.12em'
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC66 Parent \xB7 Ahmed"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, "My Children"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 4
    }
  }, children.length, " ", children.length === 1 ? 'child' : 'children', " linked to your account")), /*#__PURE__*/React.createElement("button", {
    onClick: onAddChild,
    style: {
      width: 44,
      height: 44,
      borderRadius: 14,
      border: 'none',
      background: '#4F46E5',
      color: '#fff',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 22,
      cursor: 'pointer',
      flexShrink: 0,
      boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)'
    }
  }, "+")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      borderRadius: 20,
      padding: 18,
      boxShadow: '0 8px 24px rgba(99,102,241,0.35)',
      color: '#fff'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.12em',
      opacity: 0.85,
      marginBottom: 8
    }
  }, "This Week \xB7 All Children"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement(SummaryStat, {
    label: "Total XP",
    value: "820",
    icon: "\u2B50"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 1,
      alignSelf: 'stretch',
      background: 'rgba(255,255,255,0.2)'
    }
  }), /*#__PURE__*/React.createElement(SummaryStat, {
    label: "Lessons",
    value: "18",
    icon: "\uD83D\uDCDA"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 1,
      alignSelf: 'stretch',
      background: 'rgba(255,255,255,0.2)'
    }
  }), /*#__PURE__*/React.createElement(SummaryStat, {
    label: "Active",
    value: "6/7",
    icon: "\uD83D\uDD25"
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, children.map(c => /*#__PURE__*/React.createElement(ChildCard, {
    key: c.id,
    child: c,
    onClick: () => onPick(c)
  }))), /*#__PURE__*/React.createElement("button", {
    onClick: onAddChild,
    style: {
      padding: 20,
      borderRadius: 20,
      background: 'transparent',
      border: '2px dashed rgba(99,102,241,0.4)',
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      cursor: 'pointer',
      fontFamily: 'inherit',
      color: '#A5B4FC',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    },
    onPointerOver: e => {
      e.currentTarget.style.background = 'rgba(79,70,229,0.08)';
      e.currentTarget.style.borderColor = '#4F46E5';
    },
    onPointerOut: e => {
      e.currentTarget.style.background = 'transparent';
      e.currentTarget.style.borderColor = 'rgba(99,102,241,0.4)';
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 48,
      height: 48,
      borderRadius: 14,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 26,
      fontWeight: 800,
      flexShrink: 0
    }
  }, "+"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16,
      color: '#F8FAFC'
    }
  }, "Add a child"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "Set their grade, language, and login email"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 10,
      padding: '12px 14px',
      borderRadius: 14,
      background: '#15161D',
      border: '1px solid rgba(255,255,255,0.05)',
      fontSize: 12,
      color: '#94A3B8',
      lineHeight: 1.5
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 16
    }
  }, "\uD83D\uDD12"), /*#__PURE__*/React.createElement("span", null, "You can see only your own children's progress. Each child logs in with the email you assigned."))));
}
function ChildCard({
  child,
  onClick
}) {
  const langLabel = {
    en: '🇬🇧 English',
    ar: '🇸🇦 العربية'
  }[child.language] || child.language;
  return /*#__PURE__*/React.createElement("button", {
    onClick: onClick,
    style: {
      background: '#15161D',
      borderRadius: 20,
      padding: 16,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      flexDirection: 'column',
      gap: 14,
      cursor: 'pointer',
      fontFamily: 'inherit',
      color: '#F8FAFC',
      width: '100%',
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: '50%',
      background: child.color,
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 22,
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.18), 0 4px 12px rgba(0,0,0,0.2)',
      flexShrink: 0
    }
  }, child.initial), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      flexWrap: 'wrap'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, child.name), /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '2px 8px',
      borderRadius: 9999,
      background: 'rgba(79,70,229,0.18)',
      color: '#A5B4FC',
      fontWeight: 800,
      fontSize: 11,
      letterSpacing: '0.04em'
    }
  }, "Grade ", child.grade)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 4,
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap'
    }
  }, child.email)), /*#__PURE__*/React.createElement("div", {
    style: {
      color: '#94A3B8',
      fontSize: 22
    }
  }, "\u203A")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement(ChildStat, {
    icon: "\uD83E\uDDE0",
    value: `Lv ${child.level}`,
    color: "#A855F7"
  }), /*#__PURE__*/React.createElement(ChildStat, {
    icon: "\u2B50",
    value: child.xp.toLocaleString(),
    color: "#FACC15"
  }), /*#__PURE__*/React.createElement(ChildStat, {
    icon: "\uD83D\uDD25",
    value: `${child.streak}d`,
    color: "#FB923C"
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      marginLeft: 'auto',
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      fontSize: 11,
      color: '#94A3B8',
      fontWeight: 600
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: child.status === 'active' ? '#22C55E' : '#64748B',
      boxShadow: child.status === 'active' ? '0 0 6px rgba(34,197,94,0.6)' : 'none'
    }
  }), child.status === 'active' ? 'Active today' : 'Inactive')), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      paddingTop: 12,
      borderTop: '1px solid rgba(255,255,255,0.05)',
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#94A3B8'
    }
  }, "Language:"), " ", langLabel), /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "View progress \u2192")));
}
function ChildStat({
  icon,
  value,
  color
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 4
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 14
    }
  }, icon), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color,
      fontVariantNumeric: 'tabular-nums'
    }
  }, value));
}
function SummaryStat({
  label,
  value,
  icon
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 16,
      marginBottom: 2
    }
  }, icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      fontVariantNumeric: 'tabular-nums',
      color: '#fff',
      lineHeight: 1
    }
  }, value), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      fontWeight: 700,
      textTransform: 'uppercase',
      letterSpacing: '0.08em',
      opacity: 0.85,
      marginTop: 3
    }
  }, label));
}

// ───────────────────────────────────────────── Shared form bits
function AuthField({
  label,
  icon,
  right,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      fontWeight: 700,
      color: '#CBD5E1',
      letterSpacing: '0.04em',
      display: 'flex',
      alignItems: 'center',
      gap: 6
    }
  }, icon && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 13
    }
  }, icon), label), right), children);
}
function authInputStyle() {
  return {
    height: 50,
    background: '#15161D',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 14,
    color: '#F8FAFC',
    fontFamily: 'Poppins, system-ui, sans-serif',
    fontSize: 15,
    fontWeight: 500,
    padding: '0 14px',
    width: '100%',
    outline: 'none',
    transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
  };
}
function SocialButton({
  label,
  icon,
  appleIcon
}) {
  return /*#__PURE__*/React.createElement("button", {
    style: {
      flex: 1,
      height: 48,
      borderRadius: 14,
      background: '#15161D',
      border: '1px solid rgba(255,255,255,0.08)',
      color: '#F8FAFC',
      fontFamily: 'inherit',
      fontWeight: 700,
      fontSize: 14,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10
    }
  }, appleIcon ? /*#__PURE__*/React.createElement("svg", {
    width: "18",
    height: "18",
    viewBox: "0 0 24 24",
    fill: "#F8FAFC"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M17.05 20.28c-.98.95-2.05.86-3.08.43-1.09-.46-2.09-.48-3.24 0-1.44.62-2.2.44-3.06-.43C2.79 15.25 3.51 7.59 9.05 7.31c1.35.07 2.29.74 3.08.8 1.18-.24 2.31-.93 3.57-.84 1.51.12 2.65.72 3.4 1.8-3.12 1.87-2.38 5.98.48 7.13-.57 1.5-1.31 2.99-2.54 4.08zM12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25z"
  })) : /*#__PURE__*/React.createElement("span", {
    style: {
      width: 20,
      height: 20,
      borderRadius: '50%',
      background: '#fff',
      color: '#0F172A',
      fontWeight: 900,
      fontSize: 12,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center'
    }
  }, icon), label);
}
function scorePw(pw) {
  let s = 0;
  if (pw.length >= 6) s++;
  if (pw.length >= 10) s++;
  if (/[A-Z]/.test(pw) && /[a-z]/.test(pw)) s++;
  if (/[0-9]/.test(pw) || /[^A-Za-z0-9]/.test(pw)) s++;
  const labels = ['Weak', 'Fair', 'Good', 'Strong', 'Strong'];
  const colors = ['#EF4444', '#F59E0B', '#FACC15', '#22C55E', '#22C55E'];
  return {
    score: s,
    label: labels[s],
    color: colors[s]
  };
}
Object.assign(window, {
  LoginScreen,
  RegisterScreen,
  MyChildrenScreen,
  ChildCard,
  AddChildSheet
});

// ───────────────────────────────────────────── ADD CHILD (mobile bottom sheet)
function AddChildSheet({
  open,
  onClose
}) {
  const [photo, setPhoto] = React.useState(null);
  const [color, setColor] = React.useState('#A855F7');
  const [name, setName] = React.useState('Layla');
  const [grade, setGrade] = React.useState(1);
  const [lang, setLang] = React.useState('ar');
  const fileRef = React.useRef(null);
  if (!open) return null;
  const onFile = e => {
    const f = e.target.files && e.target.files[0];
    if (f) setPhoto(URL.createObjectURL(f));
  };
  const fld = (label, child) => /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontFamily: 'Poppins, sans-serif',
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, label), child);
  const inp = {
    height: 50,
    background: '#0B0C12',
    border: '1px solid rgba(255,255,255,0.1)',
    borderRadius: 14,
    color: '#F8FAFC',
    fontFamily: 'Poppins, sans-serif',
    fontSize: 15,
    padding: '0 14px',
    width: '100%',
    outline: 'none',
    boxSizing: 'border-box'
  };
  return /*#__PURE__*/React.createElement("div", {
    onClick: onClose,
    style: {
      position: 'absolute',
      inset: 0,
      zIndex: 200,
      background: 'rgba(5,8,22,0.7)',
      display: 'flex',
      alignItems: 'flex-end',
      justifyContent: 'center',
      fontFamily: 'Poppins, sans-serif'
    }
  }, /*#__PURE__*/React.createElement("div", {
    onClick: e => e.stopPropagation(),
    style: {
      width: '100%',
      background: '#15161D',
      borderRadius: '28px 28px 0 0',
      border: '1px solid rgba(255,255,255,0.08)',
      borderBottom: 'none',
      boxShadow: '0 -24px 64px rgba(0,0,0,0.55)',
      padding: '12px 20px 32px',
      animation: 'lxsheet 320ms cubic-bezier(0.16,1,0.3,1)',
      maxHeight: '88%',
      overflowY: 'auto'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 5,
      borderRadius: 9999,
      background: 'rgba(255,255,255,0.18)',
      margin: '4px auto 18px'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      marginBottom: 20
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 48,
      height: 48,
      borderRadius: 14,
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 24,
      boxShadow: '0 6px 16px rgba(99,102,241,0.4)'
    }
  }, "\uD83D\uDC76"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 20,
      color: '#F8FAFC'
    }
  }, "Add a child"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 2
    }
  }, "They'll log in with the email you set"))), /*#__PURE__*/React.createElement("input", {
    ref: fileRef,
    type: "file",
    accept: "image/*",
    onChange: onFile,
    style: {
      display: 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 16
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 64,
      height: 64,
      borderRadius: '50%',
      background: photo ? `url(${photo}) center/cover` : color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 26,
      color: '#fff',
      flexShrink: 0,
      position: 'relative',
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.2)'
    }
  }, !photo && (name.trim()[0] || 'L').toUpperCase(), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: -2,
      right: -2,
      width: 24,
      height: 24,
      borderRadius: '50%',
      background: '#4F46E5',
      border: '2px solid #15161D',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 11
    }
  }, "\uD83D\uDCF7")), /*#__PURE__*/React.createElement("div", {
    onClick: () => fileRef.current && fileRef.current.click(),
    style: {
      flex: 1,
      border: '1.5px dashed rgba(99,102,241,0.45)',
      borderRadius: 14,
      padding: '12px 14px',
      display: 'flex',
      alignItems: 'center',
      gap: 10,
      cursor: 'pointer',
      background: 'rgba(79,70,229,0.05)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20
    }
  }, "\u2B06\uFE0F"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 13,
      color: '#A5B4FC'
    }
  }, photo ? 'Change photo' : 'Upload a photo'), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      marginTop: 1
    }
  }, "PNG or JPG \xB7 or pick a color below")))), fld("Child's name", /*#__PURE__*/React.createElement("input", {
    value: name,
    onChange: e => setName(e.target.value),
    style: inp
  })), fld('Login email', /*#__PURE__*/React.createElement("input", {
    defaultValue: "layla@learnexia.com",
    style: {
      ...inp,
      borderColor: '#4F46E5',
      boxShadow: '0 0 0 3px rgba(99,102,241,0.2)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, "Grade"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(6, 1fr)',
      gap: 6
    }
  }, [1, 2, 3, 4, 5, 6].map(g => {
    const on = grade === g;
    return /*#__PURE__*/React.createElement("div", {
      key: g,
      onClick: () => setGrade(g),
      style: {
        height: 46,
        borderRadius: 12,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1,
        background: on ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#0B0C12',
        border: on ? 'none' : '1px solid rgba(255,255,255,0.1)',
        boxShadow: on ? '0 4px 12px rgba(99,102,241,0.4)' : 'none'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 15
      }
    }, ['🌱', '🌿', '🌳', '🌲', '🍃', '🌴'][g - 1]), /*#__PURE__*/React.createElement("span", {
      style: {
        fontWeight: 800,
        fontSize: 11,
        color: on ? '#fff' : '#94A3B8'
      }
    }, g));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, "Language"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8
    }
  }, [{
    id: 'ar',
    flag: '🇪🇬',
    label: 'AR'
  }, {
    id: 'en',
    flag: '🇺🇸',
    label: 'EN'
  }].map(l => {
    const on = lang === l.id;
    return /*#__PURE__*/React.createElement("div", {
      key: l.id,
      onClick: () => setLang(l.id),
      style: {
        flex: 1,
        height: 48,
        borderRadius: 12,
        cursor: 'pointer',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 8,
        background: on ? 'rgba(79,70,229,0.18)' : '#0B0C12',
        border: on ? '1.5px solid #4F46E5' : '1px solid rgba(255,255,255,0.1)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 18
      }
    }, l.flag), /*#__PURE__*/React.createElement("span", {
      style: {
        fontWeight: 700,
        fontSize: 14,
        color: on ? '#F8FAFC' : '#94A3B8'
      }
    }, l.label));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("label", {
    style: {
      fontWeight: 700,
      fontSize: 12,
      color: '#CBD5E1'
    }
  }, "\u2026or pick an avatar color"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 12
    }
  }, ['#FB923C', '#A855F7', '#22C55E', '#38BDF8', '#FB7185'].map(c => /*#__PURE__*/React.createElement("div", {
    key: c,
    onClick: () => {
      setColor(c);
      setPhoto(null);
    },
    style: {
      width: 40,
      height: 40,
      borderRadius: '50%',
      background: c,
      cursor: 'pointer',
      boxShadow: color === c && !photo ? `0 0 0 3px #15161D, 0 0 0 5px ${c}` : 'none'
    }
  })))), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    style: {
      height: 56,
      borderRadius: 16,
      background: '#4F46E5',
      border: 'none',
      color: '#fff',
      fontWeight: 800,
      fontSize: 17,
      cursor: 'pointer',
      boxShadow: '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)',
      marginTop: 4
    }
  }, "Add ", name.trim() || 'child', " \u2192"))));
}
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/student-mobile/ScreensAuth.jsx", error: String((e && e.message) || e) }); }

// ui_kits/student-mobile/ScreensExtra.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
// Learnexia student mobile — additional screens from the wireframes spec.
// Splash, RoleSelect, GradeSelect, SubjectSelect, League, BadgeCollection,
// Hearts, DailyMission, Profile.

const extraFont = {
  fontFamily: 'Poppins, system-ui, sans-serif'
};

// ───────────────────────────────────────────── SPLASH
function SplashScreen({
  onContinue
}) {
  // deterministic sparkle positions so they don't jump on re-render
  const sparkles = React.useMemo(() => Array.from({
    length: 14
  }, (_, i) => ({
    top: i * 73 % 100,
    left: (i * 41 + 17) % 100,
    size: i % 3 + 3,
    opacity: 0.25 + i * 7 % 50 / 100
  })), []);
  return /*#__PURE__*/React.createElement("div", {
    onClick: onContinue,
    style: {
      width: '100%',
      height: '100%',
      position: 'relative',
      background: 'radial-gradient(circle at 50% 45%,#4F3FB0 0%,#3B2C8F 40%,#241B6A 100%)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      cursor: 'pointer',
      ...extraFont,
      overflow: 'hidden'
    }
  }, sparkles.map((s, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      position: 'absolute',
      top: `${s.top}%`,
      left: `${s.left}%`,
      width: s.size,
      height: s.size,
      borderRadius: '50%',
      background: '#fff',
      opacity: s.opacity,
      boxShadow: `0 0 ${s.size * 2}px rgba(255,255,255,${s.opacity})`
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: '40%',
      left: '50%',
      transform: 'translate(-50%, -50%)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 18
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 132,
      height: 132,
      borderRadius: '50%',
      background: 'radial-gradient(circle, rgba(250,204,21,0.35) 0%, rgba(168,85,247,0) 65%)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      animation: 'lxpop 800ms cubic-bezier(0.34,1.56,0.64,1)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 88,
      filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.6))',
      animation: 'lxpulse 2.4s ease-in-out infinite'
    }
  }, "\uD83C\uDF1F")), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 36,
      color: '#F8FAFC',
      letterSpacing: '-0.02em'
    }
  }, "Learnexia"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: 'rgba(255,255,255,0.7)',
      marginTop: 8,
      fontWeight: 500
    }
  }, "AI Learning Adventure Begins"))), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: '70%',
      left: 0,
      right: 0,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6
    }
  }, [0, 1, 2].map(i => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: i === 0 ? '#A855F7' : i === 1 ? '#6366F1' : 'rgba(255,255,255,0.3)',
      animation: `lxdot 1.4s ease-in-out ${i * 0.2}s infinite`
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 220,
      height: 6,
      borderRadius: 9999,
      background: 'rgba(0,0,0,0.35)',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '55%',
      background: 'linear-gradient(90deg,#C4B5FD,#818CF8)',
      borderRadius: 9999,
      animation: 'lxload 2.5s ease-in-out infinite'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      color: 'rgba(255,255,255,0.7)',
      fontSize: 15,
      fontWeight: 500
    }
  }, "Loading\u2026 ", /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 16
    }
  }, "\u26A1"))), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: 50,
      left: 0,
      right: 0,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: 'rgba(255,255,255,0.45)',
      letterSpacing: '0.18em',
      textTransform: 'uppercase'
    }
  }, "POWERED BY AI"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      fontSize: 13,
      color: 'rgba(255,255,255,0.65)',
      fontWeight: 500
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A78BFA'
    }
  }, "\u2726"), "Gamified Learning", /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A78BFA'
    }
  }, "\u2726"))));
}

// ───────────────────────────────────────────── ROLE SELECT
function RoleSelectScreen({
  onPick
}) {
  const roles = [{
    id: 'student',
    emoji: '🎓',
    label: 'Student',
    sub: 'Learn, play, level up'
  }, {
    id: 'teacher',
    emoji: '👨‍🏫',
    label: 'Teacher',
    sub: 'Manage classes'
  }, {
    id: 'parent',
    emoji: '👨‍👩‍👦',
    label: 'Parent',
    sub: "Track your child's progress"
  }];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 20px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 24,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 40,
      marginBottom: 4
    }
  }, "\uD83C\uDFAE"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC'
    }
  }, "Welcome to Learnexia"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 6
    }
  }, "Pick the one that's you")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 10
    }
  }, roles.map(r => /*#__PURE__*/React.createElement("button", {
    key: r.id,
    onClick: () => onPick(r.id),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 16,
      padding: 18,
      borderRadius: 20,
      background: '#1E293B',
      border: '2px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      fontFamily: 'inherit',
      cursor: 'pointer',
      textAlign: 'left',
      color: '#F8FAFC',
      transition: 'all 180ms cubic-bezier(0.16,1,0.3,1)'
    },
    onPointerOver: e => {
      e.currentTarget.style.borderColor = '#4F46E5';
      e.currentTarget.style.background = '#243349';
    },
    onPointerOut: e => {
      e.currentTarget.style.borderColor = 'rgba(255,255,255,0.06)';
      e.currentTarget.style.background = '#1E293B';
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 56,
      height: 56,
      borderRadius: 16,
      background: 'rgba(79,70,229,0.18)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 28
    }
  }, r.emoji), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 17
    }
  }, r.label), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 2
    }
  }, r.sub)), /*#__PURE__*/React.createElement("div", {
    style: {
      color: '#A5B4FC',
      fontSize: 22
    }
  }, "\u203A"))))));
}

// ───────────────────────────────────────────── GRADE SELECT
function GradeSelectScreen({
  onPick
}) {
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 20px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 22,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.1em'
    }
  }, "Step 1 of 2"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, "What grade are you in?"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "We'll adjust the lessons for you")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(2,1fr)',
      gap: 10
    }
  }, [1, 2, 3, 4, 5, 6].map(g => /*#__PURE__*/React.createElement("button", {
    key: g,
    onClick: () => onPick(g),
    style: {
      padding: '20px 16px',
      borderRadius: 20,
      background: g === 3 ? 'linear-gradient(135deg,#A855F7,#6366F1)' : '#1E293B',
      border: g === 3 ? 'none' : '2px solid rgba(255,255,255,0.06)',
      boxShadow: g === 3 ? '0 8px 24px rgba(99,102,241,0.4)' : '0 4px 12px rgba(0,0,0,0.15)',
      color: '#F8FAFC',
      cursor: 'pointer',
      fontFamily: 'inherit',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'flex-start',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 28
    }
  }, ['🌱', '🌿', '🌳', '🌲', '🍃', '🌴'][g - 1]), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 18
    }
  }, "Grade ", g), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: g === 3 ? 'rgba(255,255,255,0.85)' : '#94A3B8'
    }
  }, "Ages ", 5 + g, "\u2013", 6 + g)))), /*#__PURE__*/React.createElement(PrimaryButton, {
    full: true,
    onClick: () => onPick(3)
  }, "Start Learning \u2192")));
}

// ───────────────────────────────────────────── SUBJECT SELECT
function SubjectSelectScreen({
  onPick
}) {
  const subjects = [{
    id: 'math',
    emoji: '🧮',
    label: 'Math',
    color: '#4F46E5',
    progress: 0.45
  }, {
    id: 'science',
    emoji: '🧪',
    label: 'Science',
    color: '#22C55E',
    progress: 0.28
  }, {
    id: 'arabic',
    emoji: '📖',
    label: 'Arabic',
    color: '#FB923C',
    progress: 0.62
  }, {
    id: 'english',
    emoji: '🇬🇧',
    label: 'English',
    color: '#A855F7',
    progress: 0.51
  }, {
    id: 'social',
    emoji: '🌍',
    label: 'Social Studies',
    color: '#38BDF8',
    progress: 0.14
  }];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 20px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC'
    }
  }, "Choose a Subject"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "Tap any subject to keep learning")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 10
    }
  }, subjects.map(s => /*#__PURE__*/React.createElement("button", {
    key: s.id,
    onClick: () => onPick(s.id),
    style: {
      padding: 16,
      borderRadius: 20,
      background: '#1E293B',
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      cursor: 'pointer',
      fontFamily: 'inherit',
      color: '#F8FAFC'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 52,
      height: 52,
      borderRadius: 16,
      background: `${s.color}22`,
      color: s.color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 26
    }
  }, s.emoji), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      textAlign: 'left'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 16
    }
  }, s.label), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      marginTop: 4,
      marginBottom: 6
    }
  }, Math.round(s.progress * 100), "% mastered"), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 6,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: `${s.progress * 100}%`,
      background: s.color
    }
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      color: '#94A3B8',
      fontSize: 18
    }
  }, "\u203A"))))));
}

// ───────────────────────────────────────────── LEAGUE
function LeagueScreen() {
  const players = [{
    rank: 1,
    name: 'Ahmed',
    xp: 2450,
    you: false,
    avatar: '#FB923C'
  }, {
    rank: 2,
    name: 'Sara',
    xp: 2380,
    you: false,
    avatar: '#A855F7'
  }, {
    rank: 3,
    name: 'Layla',
    xp: 2210,
    you: false,
    avatar: '#22C55E'
  }, {
    rank: 4,
    name: 'Yusuf',
    xp: 2050,
    you: false,
    avatar: '#38BDF8'
  }, {
    rank: 5,
    name: 'Omar',
    xp: 1980,
    you: false,
    avatar: '#FB7185'
  }, {
    rank: 6,
    name: 'Maya',
    xp: 1920,
    you: false,
    avatar: '#FACC15'
  }, {
    rank: 7,
    name: 'Sami',
    xp: 1240,
    you: true,
    avatar: '#EF4444'
  }, {
    rank: 8,
    name: 'Hassan',
    xp: 1180,
    you: false,
    avatar: '#A855F7'
  }];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#FBBF24,#B45309)',
      borderRadius: 24,
      padding: '20px 18px',
      boxShadow: '0 8px 24px rgba(180,83,9,0.4)',
      display: 'flex',
      alignItems: 'center',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 56,
      height: 56,
      borderRadius: '50%',
      background: 'rgba(255,255,255,0.2)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 32,
      boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2)'
    }
  }, "\uD83C\uDFC6"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      color: '#fff'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20
    }
  }, "Bronze League"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      opacity: 0.9
    }
  }, "3 days left \xB7 Top 10 promote"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6
    }
  }, players.map(p => /*#__PURE__*/React.createElement("div", {
    key: p.rank,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '12px 14px',
      borderRadius: 16,
      background: p.you ? 'rgba(79,70,229,0.18)' : '#1E293B',
      border: p.you ? '2px solid #4F46E5' : '1px solid rgba(255,255,255,0.04)',
      boxShadow: p.you ? '0 8px 24px rgba(99,102,241,0.25)' : 'none'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 28,
      textAlign: 'center',
      fontWeight: 800,
      fontSize: 14,
      color: p.rank <= 3 ? '#FACC15' : '#94A3B8',
      fontVariantNumeric: 'tabular-nums'
    }
  }, p.rank), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 38,
      height: 38,
      borderRadius: '50%',
      background: p.avatar,
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 800,
      fontSize: 14,
      boxShadow: 'inset 0 -2px 4px rgba(0,0,0,0.15)'
    }
  }, p.name[0]), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 14,
      color: p.you ? '#F8FAFC' : '#CBD5E1'
    }
  }, p.name, " ", p.you && /*#__PURE__*/React.createElement("span", {
    style: {
      color: '#A5B4FC',
      fontSize: 11,
      fontWeight: 600
    }
  }, "YOU"))), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#FACC15',
      fontVariantNumeric: 'tabular-nums'
    }
  }, p.xp.toLocaleString(), " XP")))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '0 4px'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(34,197,94,0.3)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      fontWeight: 700,
      color: '#22C55E',
      textTransform: 'uppercase',
      letterSpacing: '0.08em'
    }
  }, "\u2191 Promotion Zone"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 1,
      background: 'rgba(34,197,94,0.3)'
    }
  }))));
}

// ───────────────────────────────────────────── BADGE COLLECTION
function BadgeCollectionScreen() {
  const badges = [{
    tier: 'bronze',
    earned: true,
    name: 'First Steps',
    sub: 'Earned'
  }, {
    tier: 'bronze',
    earned: true,
    name: 'Quick Reader',
    sub: 'Earned'
  }, {
    tier: 'silver',
    earned: true,
    name: 'Quiz Master',
    sub: 'Earned'
  }, {
    tier: 'silver',
    earned: true,
    name: 'Streak Hero',
    sub: '7 days'
  }, {
    tier: 'gold',
    earned: true,
    name: 'Math Wizard',
    sub: 'Earned'
  }, {
    tier: 'gold',
    earned: false,
    name: 'Word Master',
    sub: 'Locked'
  }, {
    tier: 'gold',
    earned: false,
    name: 'Science Pro',
    sub: 'Locked'
  }, {
    tier: 'legendary',
    earned: false,
    name: 'All-Star',
    sub: 'Top 1%'
  }];
  const discFor = (tier, earned) => {
    if (!earned) return {
      bg: '#334155',
      color: '#64748B',
      shadow: 'inset 0 1px 0 rgba(255,255,255,0.06)'
    };
    return {
      bronze: {
        bg: 'radial-gradient(circle at 30% 30%,#FCD9B5,#B45309)',
        color: '#fff',
        shadow: '0 8px 20px rgba(180,83,9,0.45)'
      },
      silver: {
        bg: 'radial-gradient(circle at 30% 30%,#F1F5F9,#94A3B8)',
        color: '#0F172A',
        shadow: '0 8px 20px rgba(148,163,184,0.35)'
      },
      gold: {
        bg: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)',
        color: '#0F172A',
        shadow: '0 8px 20px rgba(245,158,11,0.45)'
      },
      legendary: {
        bg: 'radial-gradient(circle at 30% 30%,#FBCFE8,#A855F7 55%,#4F46E5)',
        color: '#fff',
        shadow: '0 0 24px rgba(168,85,247,0.6)'
      }
    }[tier];
  };
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC'
    }
  }, "\uD83C\uDFC6 Badges"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "5 of 8 earned \xB7 Collect them all")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 8,
      background: '#1E293B',
      borderRadius: 16,
      padding: 12,
      border: '1px solid rgba(255,255,255,0.06)'
    }
  }, [{
    label: 'Bronze',
    val: 2,
    color: '#B45309'
  }, {
    label: 'Silver',
    val: 2,
    color: '#94A3B8'
  }, {
    label: 'Gold',
    val: 1,
    color: '#F59E0B'
  }, {
    label: 'Legend',
    val: 0,
    color: '#A855F7'
  }].map(s => /*#__PURE__*/React.createElement("div", {
    key: s.label,
    style: {
      flex: 1,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: s.color,
      fontVariantNumeric: 'tabular-nums'
    }
  }, s.val), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      fontWeight: 700,
      marginTop: 2
    }
  }, s.label)))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3,1fr)',
      gap: 12
    }
  }, badges.map((b, i) => {
    const d = discFor(b.tier, b.earned);
    const isLegendary = b.tier === 'legendary';
    return /*#__PURE__*/React.createElement("div", {
      key: i,
      style: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 6,
        padding: 14,
        borderRadius: 20,
        background: isLegendary && b.earned ? 'rgba(168,85,247,0.12)' : '#1E293B',
        border: isLegendary ? '1px solid rgba(168,85,247,0.3)' : '1px solid rgba(255,255,255,0.04)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: 64,
        height: 64,
        borderRadius: '50%',
        background: d.bg,
        color: d.color,
        boxShadow: d.shadow,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 26
      }
    }, b.earned ? isLegendary ? '👑' : {
      bronze: '🥉',
      silver: '🥈',
      gold: '🥇'
    }[b.tier] : '🔒'), /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 700,
        fontSize: 12,
        color: b.earned ? '#F8FAFC' : '#64748B',
        textAlign: 'center',
        lineHeight: 1.2
      }
    }, b.name), /*#__PURE__*/React.createElement("div", {
      style: {
        fontSize: 9,
        color: '#94A3B8',
        textTransform: 'uppercase',
        letterSpacing: '0.06em',
        fontWeight: 700
      }
    }, b.sub));
  }))));
}

// ───────────────────────────────────────────── HEARTS
function HeartsScreen({
  onPractice
}) {
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 20px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 22,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 28,
      color: '#F8FAFC'
    }
  }, "Your Hearts"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 14,
      color: '#94A3B8',
      marginTop: 6
    }
  }, "You lose one for each wrong answer")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'radial-gradient(circle at 50% 50%,rgba(251,113,133,0.18),transparent 70%)',
      padding: '32px 16px',
      borderRadius: 24,
      display: 'flex',
      justifyContent: 'center',
      gap: 14
    }
  }, [1, 2, 3, 4, 5].map(i => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      fontSize: 44,
      filter: i <= 3 ? 'drop-shadow(0 0 12px rgba(251,113,133,0.6))' : 'grayscale(1) opacity(0.3)',
      animation: i === 3 ? 'lxpulse 1.5s ease-in-out infinite' : 'none'
    }
  }, "\u2764\uFE0F"))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(245,158,11,0.15)',
      border: '1px solid rgba(245,158,11,0.3)',
      borderRadius: 16,
      padding: 14,
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 22
    }
  }, "\u23F0"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F59E0B'
    }
  }, "Next heart in 23 min"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#FBBF24'
    }
  }, "Or earn some by practicing"))), /*#__PURE__*/React.createElement(PrimaryButton, {
    full: true,
    variant: "purple",
    onClick: onPractice
  }, "Practice Mode (no hearts)"), /*#__PURE__*/React.createElement("button", {
    style: {
      background: 'transparent',
      border: 'none',
      color: '#A5B4FC',
      fontFamily: 'inherit',
      fontWeight: 700,
      fontSize: 13,
      padding: 8,
      cursor: 'pointer'
    }
  }, "\uD83D\uDC8E Refill with 10 gems")));
}

// ───────────────────────────────────────────── DAILY MISSION (full)
function DailyMissionScreen() {
  const missions = [{
    icon: '✓',
    iconBg: 'rgba(34,197,94,0.2)',
    title: 'Complete 1 lesson',
    sub: 'Done',
    value: 1,
    total: 1,
    reward: 20,
    done: true
  }, {
    icon: '🎯',
    iconBg: 'rgba(79,70,229,0.2)',
    title: 'Get 10 questions right',
    sub: '6 of 10',
    value: 6,
    total: 10,
    reward: 50,
    done: false
  }, {
    icon: '🔥',
    iconBg: 'rgba(251,146,60,0.2)',
    title: 'Practice 3 days in a row',
    sub: '2 of 3',
    value: 2,
    total: 3,
    reward: 30,
    done: false
  }, {
    icon: '🧠',
    iconBg: 'rgba(168,85,247,0.2)',
    title: 'Ask Lexi a question',
    sub: 'Not yet',
    value: 0,
    total: 1,
    reward: 25,
    done: false
  }];
  const done = missions.filter(m => m.done).length;
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.1em'
    }
  }, "\uD83C\uDFAF Today's Mission"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, done, " of ", missions.length, " done"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#94A3B8',
      marginTop: 4
    }
  }, "Resets at midnight")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#F59E0B,#EF4444)',
      borderRadius: 24,
      padding: '18px 20px',
      display: 'flex',
      alignItems: 'center',
      gap: 14,
      boxShadow: '0 8px 24px rgba(245,158,11,0.35)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 60,
      height: 60,
      borderRadius: '50%',
      background: 'rgba(255,255,255,0.25)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 30
    }
  }, "\uD83C\uDF81"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      color: '#fff'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22,
      fontVariantNumeric: 'tabular-nums'
    }
  }, "+125 XP"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      opacity: 0.9
    }
  }, "Plus a Silver badge if you finish all"))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, missions.map((m, i) => /*#__PURE__*/React.createElement(MissionRow, _extends({
    key: i
  }, m))))));
}

// ───────────────────────────────────────────── PROFILE
function ProfileScreen() {
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 18,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'linear-gradient(135deg,#A855F7,#6366F1)',
      borderRadius: 24,
      padding: 22,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 12,
      boxShadow: '0 8px 24px rgba(99,102,241,0.35)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 88,
      height: 88,
      borderRadius: '50%',
      background: 'linear-gradient(135deg,#FB923C,#EF4444)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontWeight: 900,
      fontSize: 36,
      color: '#fff',
      boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2), 0 0 0 4px rgba(255,255,255,0.2)'
    }
  }, "S"), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 22,
      color: '#fff'
    }
  }, "Sami"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: 'rgba(255,255,255,0.85)',
      marginTop: 2
    }
  }, "Grade 3 \xB7 Joined Sep 2025")), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(255,255,255,0.2)',
      padding: '6px 14px',
      borderRadius: 9999,
      fontWeight: 800,
      fontSize: 13,
      color: '#fff'
    }
  }, "Level 12")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(2,1fr)',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement(StatTile, {
    icon: "\u2B50",
    value: "1,240",
    label: "Total XP",
    color: "#FACC15"
  }), /*#__PURE__*/React.createElement(StatTile, {
    icon: "\uD83D\uDD25",
    value: "7",
    label: "Day streak",
    color: "#FB923C"
  }), /*#__PURE__*/React.createElement(StatTile, {
    icon: "\uD83C\uDFC6",
    value: "5",
    label: "Badges",
    color: "#F59E0B"
  }), /*#__PURE__*/React.createElement(StatTile, {
    icon: "\uD83D\uDCDA",
    value: "34",
    label: "Lessons",
    color: "#22C55E"
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 15,
      color: '#F8FAFC'
    }
  }, "Recent Badges"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#A5B4FC',
      fontWeight: 700
    }
  }, "See all \u2192")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 16,
      justifyContent: 'space-around'
    }
  }, [{
    tier: 'gold',
    icon: '🥇',
    label: 'Math Wizard'
  }, {
    tier: 'silver',
    icon: '🥈',
    label: 'Quiz Master'
  }, {
    tier: 'silver',
    icon: '🥈',
    label: 'Streak Hero'
  }, {
    tier: 'bronze',
    icon: '🥉',
    label: 'First Quiz'
  }].map((b, i) => {
    const bg = {
      gold: 'radial-gradient(circle at 30% 30%,#FDE68A,#F59E0B)',
      silver: 'radial-gradient(circle at 30% 30%,#F1F5F9,#94A3B8)',
      bronze: 'radial-gradient(circle at 30% 30%,#FCD9B5,#B45309)'
    }[b.tier];
    return /*#__PURE__*/React.createElement("div", {
      key: i,
      style: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 6
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: 48,
        height: 48,
        borderRadius: '50%',
        background: bg,
        fontSize: 20,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        boxShadow: 'inset 0 -3px 6px rgba(0,0,0,0.2)'
      }
    }, b.icon), /*#__PURE__*/React.createElement("div", {
      style: {
        fontSize: 10,
        color: '#94A3B8',
        textAlign: 'center',
        fontWeight: 600,
        lineHeight: 1.2,
        maxWidth: 60
      }
    }, b.label));
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 18,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'baseline',
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 15,
      color: '#F8FAFC'
    }
  }, "Level 12 \u2192 13"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 13,
      color: '#FACC15',
      fontVariantNumeric: 'tabular-nums'
    }
  }, "820 / 1000 XP")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 10,
      background: '#0F172A',
      borderRadius: 9999,
      overflow: 'hidden',
      border: '1px solid rgba(255,255,255,0.06)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '82%',
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 8
    }
  }, "180 XP to next level"))));
}
function StatTile({
  icon,
  value,
  label,
  color
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#1E293B',
      borderRadius: 20,
      padding: 16,
      border: '1px solid rgba(255,255,255,0.06)',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 40,
      borderRadius: 12,
      background: `${color}22`,
      color,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20
    }
  }, icon), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 20,
      color: '#F8FAFC',
      fontVariantNumeric: 'tabular-nums',
      lineHeight: 1
    }
  }, value), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 10,
      color: '#94A3B8',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      fontWeight: 700,
      marginTop: 3
    }
  }, label)));
}

// ───────────────────────────────────────────── HELPER ENERGY
function EnergyScreen() {
  const MAX = 300;
  // view: 'live' (180, interactive) · 'low' (12) · 'cap' (daily cap reached) · 'empty' (0)
  const [view, setView] = React.useState('live');
  const [bal, setBal] = React.useState(180);
  const [revealed, setRevealed] = React.useState(null);
  const [pending, setPending] = React.useState(null); // confirm dialog

  const shownBal = view === 'low' ? 12 : view === 'empty' ? 0 : view === 'cap' ? bal : bal;
  const pct = Math.max(0, shownBal / MAX * 100);
  const low = view === 'low';
  const empty = view === 'empty';
  const cap = view === 'cap';
  const barColor = empty ? '#64748B' : cap ? 'linear-gradient(90deg,#7DD3FC,#38BDF8)' : low ? 'linear-gradient(90deg,#FBBF24,#F59E0B)' : 'linear-gradient(90deg,#2DD4BF,#14B8A6)';
  const edge = empty ? '#64748B' : cap ? '#38BDF8' : low ? '#F59E0B' : '#14B8A6';
  const txt = empty ? '#94A3B8' : cap ? '#38BDF8' : low ? '#F59E0B' : '#2DD4BF';
  const actions = [{
    id: 'hint',
    icon: '💡',
    label: 'Hint',
    cost: 1,
    bg: 'rgba(45,212,191,0.14)',
    fg: '#2DD4BF',
    say: 'Half means split into 2 equal groups. Split 8 into 2 groups 🍕'
  }, {
    id: 'explain',
    icon: '🔍',
    label: 'Explain Mistake',
    cost: 3,
    bg: 'rgba(168,85,247,0.14)',
    fg: '#C4B5FD',
    say: 'You added instead of subtracting. 8 − 3 takes 3 away, leaving 5.'
  }, {
    id: 'deep',
    icon: '📖',
    label: 'Deep Explanation',
    cost: 5,
    bg: 'rgba(79,70,229,0.16)',
    fg: '#A5B4FC',
    say: "Let's walk through fractions step by step with a pizza 🍕…"
  }, {
    id: 'prac',
    icon: '🎯',
    label: 'Practice Generation',
    cost: 5,
    bg: 'rgba(251,146,60,0.16)',
    fg: '#FDBA74',
    say: 'Made you 5 fresh practice questions on this skill!'
  }];
  const confirmSpend = () => {
    const a = pending;
    setPending(null);
    if (bal - a.cost < 0) return;
    setBal(b => b - a.cost);
    setRevealed(a);
  };
  const tabs = [['live', 'Full'], ['low', 'Low'], ['cap', 'Daily cap'], ['empty', 'Empty']];
  return /*#__PURE__*/React.createElement(ScreenShell, {
    padTop: 70
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px 16px',
      display: 'flex',
      flexDirection: 'column',
      gap: 16,
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#5eead4',
      fontWeight: 800,
      textTransform: 'uppercase',
      letterSpacing: '0.1em'
    }
  }, "\u26A1 Helper Energy"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: '#F8FAFC',
      marginTop: 4
    }
  }, "Fuel for Lexi's help"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      marginTop: 3
    }
  }, "Separate from your \u2764\uFE0F hearts \u2014 this only powers the AI helper.")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 5,
      background: '#0F172A',
      borderRadius: 12,
      padding: 4
    }
  }, tabs.map(([id, label]) => /*#__PURE__*/React.createElement("button", {
    key: id,
    onClick: () => {
      setView(id);
      setRevealed(null);
    },
    style: {
      flex: 1,
      padding: '7px 4px',
      borderRadius: 9,
      border: 'none',
      cursor: 'pointer',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 11,
      background: view === id ? 'rgba(45,212,191,0.16)' : 'transparent',
      color: view === id ? '#2DD4BF' : '#64748B'
    }
  }, label))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'radial-gradient(circle at 50% 0%,rgba(45,212,191,0.18),transparent 70%)',
      border: '1px solid rgba(45,212,191,0.25)',
      borderRadius: 22,
      padding: '20px 18px',
      display: 'flex',
      flexDirection: 'column',
      gap: 14
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 7
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 20,
      filter: 'drop-shadow(0 0 8px rgba(45,212,191,0.7))'
    }
  }, "\u26A1"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F8FAFC'
    }
  }, "This month")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 26,
      color: txt,
      fontVariantNumeric: 'tabular-nums'
    }
  }, cap ? /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18
    }
  }, "\u23F3 0/20 today") : /*#__PURE__*/React.createElement(React.Fragment, null, shownBal, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 14,
      color: '#64748B'
    }
  }, " / ", MAX)))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 26,
      background: '#0F172A',
      border: `2px solid ${edge}`,
      borderRadius: 9,
      padding: 3,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: pct + '%',
      background: barColor,
      borderRadius: 5,
      transition: 'width 400ms cubic-bezier(0.16,1,0.3,1)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      width: 6,
      height: 13,
      background: edge,
      borderRadius: '0 4px 4px 0'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      fontSize: 12,
      color: '#94A3B8'
    }
  }, /*#__PURE__*/React.createElement("span", null, "\uD83D\uDCC5"), /*#__PURE__*/React.createElement("span", null, cap ? /*#__PURE__*/React.createElement(React.Fragment, null, "Daily limit hit \u2014 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#7DD3FC'
    }
  }, "resets at midnight")) : /*#__PURE__*/React.createElement(React.Fragment, null, "Resets in ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#CBD5E1'
    }
  }, "12 days"), " \xB7 ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#CBD5E1'
    }
  }, "20"), "/day cap")))), empty ? /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(168,85,247,0.13)',
      border: '1px solid rgba(168,85,247,0.35)',
      borderRadius: 18,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 10,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 40
    }
  }, "\uD83D\uDD0C"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "Out of energy"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      maxWidth: 240,
      lineHeight: 1.5
    }
  }, "You've used this month's helper energy. Ask a grown-up to add more so Lexi can keep helping."), /*#__PURE__*/React.createElement("button", {
    style: {
      height: 44,
      padding: '0 22px',
      borderRadius: 13,
      border: 'none',
      background: 'linear-gradient(135deg,#A855F7,#7C3AED)',
      color: '#fff',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 14,
      cursor: 'pointer',
      marginTop: 4
    }
  }, "\uD83D\uDC68\u200D\uD83D\uDC69\u200D\uD83D\uDC67 Ask a parent")) : cap ? /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(56,189,248,0.12)',
      border: '1px solid rgba(56,189,248,0.35)',
      borderRadius: 18,
      padding: 18,
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      gap: 10,
      textAlign: 'center'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 40
    }
  }, "\uD83D\uDE34"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC'
    }
  }, "Lexi needs a rest!"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: '#94A3B8',
      maxWidth: 250,
      lineHeight: 1.5
    }
  }, "You used all ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#38BDF8'
    }
  }, "20"), " helpers for today. Your energy is fine \u2014 come back tomorrow for more."), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 6,
      background: '#0F172A',
      borderRadius: 9999,
      padding: '7px 14px',
      fontWeight: 800,
      fontSize: 13,
      color: '#38BDF8',
      marginTop: 2
    }
  }, "\uD83C\uDF19 Resets in 6h 12m")) : low ? /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(245,158,11,0.13)',
      border: '1px solid rgba(245,158,11,0.3)',
      borderRadius: 16,
      padding: 14,
      display: 'flex',
      alignItems: 'center',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 22
    }
  }, "\u26A1"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 800,
      fontSize: 14,
      color: '#F59E0B'
    }
  }, "Energy running low"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#FBBF24'
    }
  }, "Save it for when you're really stuck."))) : null, view === 'live' && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#94A3B8',
      fontWeight: 700,
      textTransform: 'uppercase',
      letterSpacing: '0.06em'
    }
  }, "Tap a helper \u2014 you'll confirm before spending"), actions.map(a => {
    const afford = bal - a.cost >= 0;
    return /*#__PURE__*/React.createElement("button", {
      key: a.id,
      onClick: () => setPending(a),
      disabled: !afford,
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 11,
        background: '#1E293B',
        border: '1px solid rgba(255,255,255,0.06)',
        borderRadius: 14,
        padding: '11px 13px',
        cursor: afford ? 'pointer' : 'not-allowed',
        opacity: afford ? 1 : 0.4,
        fontFamily: 'inherit',
        textAlign: 'left'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: 34,
        height: 34,
        borderRadius: 10,
        background: a.bg,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 17,
        flexShrink: 0
      }
    }, a.icon), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        fontWeight: 700,
        fontSize: 13,
        color: '#F8FAFC'
      }
    }, a.label), /*#__PURE__*/React.createElement("div", {
      style: {
        fontWeight: 800,
        fontSize: 13,
        color: '#2DD4BF',
        background: 'rgba(45,212,191,0.14)',
        padding: '3px 10px',
        borderRadius: 9999
      }
    }, "\u26A1 ", a.cost));
  })), revealed && view === 'live' && /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'rgba(45,212,191,0.06)',
      border: '1px solid rgba(45,212,191,0.35)',
      borderRadius: 16,
      padding: 13,
      display: 'flex',
      gap: 10,
      alignItems: 'flex-start',
      animation: 'lxpop 360ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 36,
      height: 36,
      borderRadius: '50%',
      background: 'linear-gradient(135deg,#A78BFA,#6366F1)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      flexShrink: 0,
      fontSize: 18
    }
  }, "\uD83E\uDD89"), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 11,
      color: '#2DD4BF',
      fontWeight: 800,
      marginBottom: 3
    }
  }, "Lexi says \xB7 \u2212", revealed.cost, " \u26A1"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      color: '#F8FAFC',
      lineHeight: 1.5
    }
  }, revealed.say)))), pending && /*#__PURE__*/React.createElement("div", {
    onClick: () => setPending(null),
    style: {
      position: 'absolute',
      inset: 0,
      zIndex: 60,
      background: 'rgba(5,8,22,0.66)',
      backdropFilter: 'blur(3px)',
      display: 'flex',
      alignItems: 'flex-end',
      ...extraFont
    }
  }, /*#__PURE__*/React.createElement("div", {
    onClick: e => e.stopPropagation(),
    style: {
      width: '100%',
      background: '#15161D',
      borderRadius: '24px 24px 0 0',
      borderTop: '1px solid rgba(45,212,191,0.3)',
      padding: '10px 20px 28px',
      display: 'flex',
      flexDirection: 'column',
      gap: 14,
      alignItems: 'center',
      animation: 'lxsheet 280ms cubic-bezier(0.16,1,0.3,1)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 40,
      height: 5,
      borderRadius: 100,
      background: 'rgba(255,255,255,0.2)',
      marginBottom: 4
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 40
    }
  }, pending.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 18,
      color: '#F8FAFC',
      textAlign: 'center'
    }
  }, "Use \u26A1", pending.cost, " for ", pending.label.toLowerCase(), "?"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      fontSize: 13,
      color: '#94A3B8'
    }
  }, "Balance after: ", /*#__PURE__*/React.createElement("b", {
    style: {
      color: '#2DD4BF',
      fontVariantNumeric: 'tabular-nums'
    }
  }, bal - pending.cost, " \u26A1"), " left"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 10,
      width: '100%',
      marginTop: 4
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: () => setPending(null),
    style: {
      flex: 1,
      height: 48,
      borderRadius: 14,
      border: '1px solid rgba(255,255,255,0.15)',
      background: 'transparent',
      color: '#CBD5E1',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 15,
      cursor: 'pointer'
    }
  }, "Not now"), /*#__PURE__*/React.createElement("button", {
    onClick: confirmSpend,
    style: {
      flex: 1.4,
      height: 48,
      borderRadius: 14,
      border: 'none',
      background: 'linear-gradient(135deg,#2DD4BF,#14B8A6)',
      color: '#06302B',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 15,
      cursor: 'pointer'
    }
  }, "Use \u26A1", pending.cost, " \u2192")))));
}
Object.assign(window, {
  SplashScreen,
  RoleSelectScreen,
  GradeSelectScreen,
  SubjectSelectScreen,
  LeagueScreen,
  BadgeCollectionScreen,
  HeartsScreen,
  DailyMissionScreen,
  ProfileScreen,
  MissionCompletedScreen,
  EnergyScreen
});

// ───────────────────────────────────────────── MISSION COMPLETED
function MissionCompletedScreen({
  onContinue,
  onChallenge
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      position: 'relative',
      background: '#0A0B11',
      display: 'flex',
      flexDirection: 'column',
      padding: '60px 20px 32px',
      ...extraFont,
      overflow: 'auto'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center',
      marginBottom: 24
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 84,
      marginBottom: 12,
      animation: 'lxpop 800ms cubic-bezier(0.34,1.56,0.64,1)'
    }
  }, "\uD83C\uDF89"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 36,
      color: '#F8FAFC',
      letterSpacing: '-0.02em',
      lineHeight: 1.1
    }
  }, "Mission Completed!"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 15,
      color: '#94A3B8',
      marginTop: 10,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 6
    }
  }, "You crushed today's challenge ", /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 17
    }
  }, "\uD83D\uDCAA"))), /*#__PURE__*/React.createElement("div", {
    style: {
      background: '#15161D',
      borderRadius: 24,
      padding: '24px 20px 20px',
      border: '1px solid rgba(245,158,11,0.5)',
      boxShadow: '0 0 0 1px rgba(245,158,11,0.3), 0 0 48px rgba(245,158,11,0.25)',
      marginBottom: 28,
      animation: 'lxglow 2.4s ease-in-out infinite'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 13,
      fontWeight: 800,
      color: '#94A3B8',
      textAlign: 'center',
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
      marginBottom: 6
    }
  }, "Total Reward"), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 900,
      fontSize: 56,
      textAlign: 'center',
      lineHeight: 1,
      marginBottom: 22,
      background: 'linear-gradient(90deg,#F59E0B,#EF4444)',
      WebkitBackgroundClip: 'text',
      WebkitTextFillColor: 'transparent',
      backgroundClip: 'text',
      fontVariantNumeric: 'tabular-nums'
    }
  }, "+120 XP"), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '14px 16px',
      borderRadius: 16,
      background: 'rgba(168,85,247,0.08)',
      border: '1px solid rgba(168,85,247,0.3)',
      marginBottom: 10
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 22
    }
  }, "\uD83C\uDFC6"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontWeight: 800,
      fontSize: 16,
      color: '#A855F7'
    }
  }, "New Badge: Math Explorer"), /*#__PURE__*/React.createElement("svg", {
    width: "20",
    height: "20",
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "#A855F7",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M12 3-1.9 5.8a2 2 0 0 0 0 1.4L12 13l1.9-5.8a2 2 0 0 0 0-1.4L12 3Z"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M5 3v4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M19 17v4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M3 5h4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M17 19h4"
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 12,
      padding: '14px 16px',
      borderRadius: 16,
      background: 'rgba(245,158,11,0.08)',
      border: '1px solid rgba(245,158,11,0.3)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 22
    }
  }, "\uD83D\uDD25"), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontWeight: 800,
      fontSize: 16,
      color: '#F59E0B'
    }
  }, "Streak: 8 Days"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 18
    }
  }, "\uD83D\uDC4C"))), /*#__PURE__*/React.createElement("div", {
    style: {
      marginBottom: 32
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: "20",
    height: "20",
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "#94A3B8",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("polyline", {
    points: "22 7 13.5 15.5 8.5 10.5 2 17"
  }), /*#__PURE__*/React.createElement("polyline", {
    points: "16 7 22 7 22 13"
  })), /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 700,
      fontSize: 17,
      color: '#CBD5E1'
    }
  }, "Level 5 \u2192 6")), /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      fontSize: 17,
      color: '#CBD5E1',
      fontVariantNumeric: 'tabular-nums'
    }
  }, "850 / 1000 XP")), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 12,
      background: '#1E2030',
      borderRadius: 9999,
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: '85%',
      background: 'linear-gradient(90deg,#22C55E,#4F46E5)',
      boxShadow: 'inset 0 1px 0 rgba(255,255,255,0.3)',
      borderRadius: 9999,
      transition: 'width 1.2s cubic-bezier(0.16,1,0.3,1)'
    }
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginTop: 8,
      fontSize: 13,
      color: '#94A3B8'
    }
  }, /*#__PURE__*/React.createElement("span", null, "85% to next level"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontVariantNumeric: 'tabular-nums'
    }
  }, "150 XP left"))), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minHeight: 20
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: onContinue,
    style: {
      height: 60,
      borderRadius: 18,
      border: 'none',
      background: '#4F46E5',
      color: '#fff',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10,
      boxShadow: '0 6px 20px rgba(99,102,241,0.45), inset 0 1px 0 rgba(255,255,255,0.2)'
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: "20",
    height: "20",
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "#fff",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2Z"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7Z"
  })), "Continue Learning \u2192"), /*#__PURE__*/React.createElement("button", {
    onClick: onChallenge,
    style: {
      height: 60,
      borderRadius: 18,
      background: 'transparent',
      color: '#A5B4FC',
      border: '1.5px solid #4F46E5',
      fontFamily: 'inherit',
      fontWeight: 800,
      fontSize: 17,
      cursor: 'pointer',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 10
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: "20",
    height: "20",
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "#A5B4FC",
    strokeWidth: "2",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M14.5 17.5 3 6V3h3l11.5 11.5"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M13 19l6-6"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M16 16l4 4"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M19 21l2-2"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M14.5 6.5 21 13"
  }), /*#__PURE__*/React.createElement("path", {
    d: "M21 3v3l-3.5 3.5"
  })), "Play Challenge")));
}
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/student-mobile/ScreensExtra.jsx", error: String((e && e.message) || e) }); }

// ui_kits/student-mobile/ios-frame.jsx
try { (() => {
// iOS.jsx — Simplified iOS 26 (Liquid Glass) device frame
// Based on the iOS 26 UI Kit + Figma status bar spec. No assets, no deps.
// Exports: IOSDevice, IOSStatusBar, IOSNavBar, IOSGlassPill, IOSList, IOSListRow, IOSKeyboard

// ─────────────────────────────────────────────────────────────
// Status bar
// ─────────────────────────────────────────────────────────────
function IOSStatusBar({
  dark = false,
  time = '9:41'
}) {
  const c = dark ? '#fff' : '#000';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 154,
      alignItems: 'center',
      justifyContent: 'center',
      padding: '21px 24px 19px',
      boxSizing: 'border-box',
      position: 'relative',
      zIndex: 20,
      width: '100%'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 22,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      paddingTop: 1.5
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: '-apple-system, "SF Pro", system-ui',
      fontWeight: 590,
      fontSize: 17,
      lineHeight: '22px',
      color: c
    }
  }, time)), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      height: 22,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 7,
      paddingTop: 1,
      paddingRight: 1
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: "19",
    height: "12",
    viewBox: "0 0 19 12"
  }, /*#__PURE__*/React.createElement("rect", {
    x: "0",
    y: "7.5",
    width: "3.2",
    height: "4.5",
    rx: "0.7",
    fill: c
  }), /*#__PURE__*/React.createElement("rect", {
    x: "4.8",
    y: "5",
    width: "3.2",
    height: "7",
    rx: "0.7",
    fill: c
  }), /*#__PURE__*/React.createElement("rect", {
    x: "9.6",
    y: "2.5",
    width: "3.2",
    height: "9.5",
    rx: "0.7",
    fill: c
  }), /*#__PURE__*/React.createElement("rect", {
    x: "14.4",
    y: "0",
    width: "3.2",
    height: "12",
    rx: "0.7",
    fill: c
  })), /*#__PURE__*/React.createElement("svg", {
    width: "17",
    height: "12",
    viewBox: "0 0 17 12"
  }, /*#__PURE__*/React.createElement("path", {
    d: "M8.5 3.2C10.8 3.2 12.9 4.1 14.4 5.6L15.5 4.5C13.7 2.7 11.2 1.5 8.5 1.5C5.8 1.5 3.3 2.7 1.5 4.5L2.6 5.6C4.1 4.1 6.2 3.2 8.5 3.2Z",
    fill: c
  }), /*#__PURE__*/React.createElement("path", {
    d: "M8.5 6.8C9.9 6.8 11.1 7.3 12 8.2L13.1 7.1C11.8 5.9 10.2 5.1 8.5 5.1C6.8 5.1 5.2 5.9 3.9 7.1L5 8.2C5.9 7.3 7.1 6.8 8.5 6.8Z",
    fill: c
  }), /*#__PURE__*/React.createElement("circle", {
    cx: "8.5",
    cy: "10.5",
    r: "1.5",
    fill: c
  })), /*#__PURE__*/React.createElement("svg", {
    width: "27",
    height: "13",
    viewBox: "0 0 27 13"
  }, /*#__PURE__*/React.createElement("rect", {
    x: "0.5",
    y: "0.5",
    width: "23",
    height: "12",
    rx: "3.5",
    stroke: c,
    strokeOpacity: "0.35",
    fill: "none"
  }), /*#__PURE__*/React.createElement("rect", {
    x: "2",
    y: "2",
    width: "20",
    height: "9",
    rx: "2",
    fill: c
  }), /*#__PURE__*/React.createElement("path", {
    d: "M25 4.5V8.5C25.8 8.2 26.5 7.2 26.5 6.5C26.5 5.8 25.8 4.8 25 4.5Z",
    fill: c,
    fillOpacity: "0.4"
  }))));
}

// ─────────────────────────────────────────────────────────────
// Liquid glass pill — blur + tint + shine
// ─────────────────────────────────────────────────────────────
function IOSGlassPill({
  children,
  dark = false,
  style = {}
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      height: 44,
      minWidth: 44,
      borderRadius: 9999,
      position: 'relative',
      overflow: 'hidden',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      boxShadow: dark ? '0 2px 6px rgba(0,0,0,0.35), 0 6px 16px rgba(0,0,0,0.2)' : '0 1px 3px rgba(0,0,0,0.07), 0 3px 10px rgba(0,0,0,0.06)',
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      inset: 0,
      borderRadius: 9999,
      backdropFilter: 'blur(12px) saturate(180%)',
      WebkitBackdropFilter: 'blur(12px) saturate(180%)',
      background: dark ? 'rgba(120,120,128,0.28)' : 'rgba(255,255,255,0.5)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      inset: 0,
      borderRadius: 9999,
      boxShadow: dark ? 'inset 1.5px 1.5px 1px rgba(255,255,255,0.15), inset -1px -1px 1px rgba(255,255,255,0.08)' : 'inset 1.5px 1.5px 1px rgba(255,255,255,0.7), inset -1px -1px 1px rgba(255,255,255,0.4)',
      border: dark ? '0.5px solid rgba(255,255,255,0.15)' : '0.5px solid rgba(0,0,0,0.06)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      zIndex: 1,
      display: 'flex',
      alignItems: 'center',
      padding: '0 4px'
    }
  }, children));
}

// ─────────────────────────────────────────────────────────────
// Navigation bar — glass pills + large title
// ─────────────────────────────────────────────────────────────
function IOSNavBar({
  title = 'Title',
  dark = false,
  trailingIcon = true
}) {
  const muted = dark ? 'rgba(255,255,255,0.6)' : '#404040';
  const text = dark ? '#fff' : '#000';
  const pillIcon = content => /*#__PURE__*/React.createElement(IOSGlassPill, {
    dark: dark
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 36,
      height: 36,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center'
    }
  }, content));
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 10,
      paddingTop: 62,
      paddingBottom: 10,
      position: 'relative',
      zIndex: 5
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '0 16px'
    }
  }, pillIcon(/*#__PURE__*/React.createElement("svg", {
    width: "12",
    height: "20",
    viewBox: "0 0 12 20",
    fill: "none",
    style: {
      marginLeft: -1
    }
  }, /*#__PURE__*/React.createElement("path", {
    d: "M10 2L2 10l8 8",
    stroke: muted,
    strokeWidth: "2.5",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  }))), trailingIcon && pillIcon(/*#__PURE__*/React.createElement("svg", {
    width: "22",
    height: "6",
    viewBox: "0 0 22 6"
  }, /*#__PURE__*/React.createElement("circle", {
    cx: "3",
    cy: "3",
    r: "2.5",
    fill: muted
  }), /*#__PURE__*/React.createElement("circle", {
    cx: "11",
    cy: "3",
    r: "2.5",
    fill: muted
  }), /*#__PURE__*/React.createElement("circle", {
    cx: "19",
    cy: "3",
    r: "2.5",
    fill: muted
  })))), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: '0 16px',
      fontFamily: '-apple-system, system-ui',
      fontSize: 34,
      fontWeight: 700,
      lineHeight: '41px',
      color: text,
      letterSpacing: 0.4
    }
  }, title));
}

// ─────────────────────────────────────────────────────────────
// Grouped list (inset card, r:26) + row (52px)
// ─────────────────────────────────────────────────────────────
function IOSListRow({
  title,
  detail,
  icon,
  chevron = true,
  isLast = false,
  dark = false
}) {
  const text = dark ? '#fff' : '#000';
  const sec = dark ? 'rgba(235,235,245,0.6)' : 'rgba(60,60,67,0.6)';
  const ter = dark ? 'rgba(235,235,245,0.3)' : 'rgba(60,60,67,0.3)';
  const sep = dark ? 'rgba(84,84,88,0.65)' : 'rgba(60,60,67,0.12)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      minHeight: 52,
      padding: '0 16px',
      position: 'relative',
      fontFamily: '-apple-system, system-ui',
      fontSize: 17,
      letterSpacing: -0.43
    }
  }, icon && /*#__PURE__*/React.createElement("div", {
    style: {
      width: 30,
      height: 30,
      borderRadius: 7,
      background: icon,
      marginRight: 12,
      flexShrink: 0
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      color: text
    }
  }, title), detail && /*#__PURE__*/React.createElement("span", {
    style: {
      color: sec,
      marginRight: 6
    }
  }, detail), chevron && /*#__PURE__*/React.createElement("svg", {
    width: "8",
    height: "14",
    viewBox: "0 0 8 14",
    style: {
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement("path", {
    d: "M1 1l6 6-6 6",
    stroke: ter,
    strokeWidth: "2",
    fill: "none",
    strokeLinecap: "round",
    strokeLinejoin: "round"
  })), !isLast && /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: 0,
      right: 0,
      left: icon ? 58 : 16,
      height: 0.5,
      background: sep
    }
  }));
}
function IOSList({
  header,
  children,
  dark = false
}) {
  const hc = dark ? 'rgba(235,235,245,0.6)' : 'rgba(60,60,67,0.6)';
  const bg = dark ? '#1C1C1E' : '#fff';
  return /*#__PURE__*/React.createElement("div", null, header && /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: '-apple-system, system-ui',
      fontSize: 13,
      color: hc,
      textTransform: 'uppercase',
      padding: '8px 36px 6px',
      letterSpacing: -0.08
    }
  }, header), /*#__PURE__*/React.createElement("div", {
    style: {
      background: bg,
      borderRadius: 26,
      margin: '0 16px',
      overflow: 'hidden'
    }
  }, children));
}

// ─────────────────────────────────────────────────────────────
// Device frame
// ─────────────────────────────────────────────────────────────
function IOSDevice({
  children,
  width = 402,
  height = 874,
  dark = false,
  title,
  keyboard = false
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width,
      height,
      borderRadius: 48,
      overflow: 'hidden',
      position: 'relative',
      background: dark ? '#000' : '#F2F2F7',
      boxShadow: '0 40px 80px rgba(0,0,0,0.18), 0 0 0 1px rgba(0,0,0,0.12)',
      fontFamily: '-apple-system, system-ui, sans-serif',
      WebkitFontSmoothing: 'antialiased'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 11,
      left: '50%',
      transform: 'translateX(-50%)',
      width: 126,
      height: 37,
      borderRadius: 24,
      background: '#000',
      zIndex: 50
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 0,
      left: 0,
      right: 0,
      zIndex: 10
    }
  }, /*#__PURE__*/React.createElement(IOSStatusBar, {
    dark: dark
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      display: 'flex',
      flexDirection: 'column'
    }
  }, title !== undefined && /*#__PURE__*/React.createElement(IOSNavBar, {
    title: title,
    dark: dark
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto'
    }
  }, children), keyboard && /*#__PURE__*/React.createElement(IOSKeyboard, {
    dark: dark
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      bottom: 0,
      left: 0,
      right: 0,
      zIndex: 60,
      height: 34,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'flex-end',
      paddingBottom: 8,
      pointerEvents: 'none'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: 139,
      height: 5,
      borderRadius: 100,
      background: dark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.25)'
    }
  })));
}

// ─────────────────────────────────────────────────────────────
// Keyboard — iOS 26 liquid glass
// ─────────────────────────────────────────────────────────────
function IOSKeyboard({
  dark = false
}) {
  const glyph = dark ? 'rgba(255,255,255,0.7)' : '#595959';
  const sugg = dark ? 'rgba(255,255,255,0.6)' : '#333';
  const keyBg = dark ? 'rgba(255,255,255,0.22)' : 'rgba(255,255,255,0.85)';

  // special-key icons
  const icons = {
    shift: /*#__PURE__*/React.createElement("svg", {
      width: "19",
      height: "17",
      viewBox: "0 0 19 17"
    }, /*#__PURE__*/React.createElement("path", {
      d: "M9.5 1L1 9.5h4.5V16h8V9.5H18L9.5 1z",
      fill: glyph
    })),
    del: /*#__PURE__*/React.createElement("svg", {
      width: "23",
      height: "17",
      viewBox: "0 0 23 17"
    }, /*#__PURE__*/React.createElement("path", {
      d: "M7 1h13a2 2 0 012 2v11a2 2 0 01-2 2H7l-6-7.5L7 1z",
      fill: "none",
      stroke: glyph,
      strokeWidth: "1.6",
      strokeLinejoin: "round"
    }), /*#__PURE__*/React.createElement("path", {
      d: "M10 5l7 7M17 5l-7 7",
      stroke: glyph,
      strokeWidth: "1.6",
      strokeLinecap: "round"
    })),
    ret: /*#__PURE__*/React.createElement("svg", {
      width: "20",
      height: "14",
      viewBox: "0 0 20 14"
    }, /*#__PURE__*/React.createElement("path", {
      d: "M18 1v6H4m0 0l4-4M4 7l4 4",
      fill: "none",
      stroke: "#fff",
      strokeWidth: "1.8",
      strokeLinecap: "round",
      strokeLinejoin: "round"
    }))
  };
  const key = (content, {
    w,
    flex,
    ret,
    fs = 25,
    k
  } = {}) => /*#__PURE__*/React.createElement("div", {
    key: k,
    style: {
      height: 42,
      borderRadius: 8.5,
      flex: flex ? 1 : undefined,
      width: w,
      minWidth: 0,
      background: ret ? '#08f' : keyBg,
      boxShadow: '0 1px 0 rgba(0,0,0,0.075)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontFamily: '-apple-system, "SF Compact", system-ui',
      fontSize: fs,
      fontWeight: 458,
      color: ret ? '#fff' : glyph
    }
  }, content);
  const row = (keys, pad = 0) => /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6.5,
      justifyContent: 'center',
      padding: `0 ${pad}px`
    }
  }, keys.map(l => key(l, {
    flex: true,
    k: l
  })));
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      zIndex: 15,
      borderRadius: 27,
      overflow: 'hidden',
      padding: '11px 0 2px',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      boxShadow: dark ? '0 -2px 20px rgba(0,0,0,0.09)' : '0 -1px 6px rgba(0,0,0,0.018), 0 -3px 20px rgba(0,0,0,0.012)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      inset: 0,
      borderRadius: 27,
      backdropFilter: 'blur(12px) saturate(180%)',
      WebkitBackdropFilter: 'blur(12px) saturate(180%)',
      background: dark ? 'rgba(120,120,128,0.14)' : 'rgba(255,255,255,0.25)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      inset: 0,
      borderRadius: 27,
      boxShadow: dark ? 'inset 1.5px 1.5px 1px rgba(255,255,255,0.15)' : 'inset 1.5px 1.5px 1px rgba(255,255,255,0.7), inset -1px -1px 1px rgba(255,255,255,0.4)',
      border: dark ? '0.5px solid rgba(255,255,255,0.15)' : '0.5px solid rgba(0,0,0,0.06)',
      pointerEvents: 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 20,
      alignItems: 'center',
      padding: '8px 22px 13px',
      width: '100%',
      boxSizing: 'border-box',
      position: 'relative'
    }
  }, ['"The"', 'the', 'to'].map((w, i) => /*#__PURE__*/React.createElement(React.Fragment, {
    key: i
  }, i > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      width: 1,
      height: 25,
      background: '#ccc',
      opacity: 0.3
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      textAlign: 'center',
      fontFamily: '-apple-system, system-ui',
      fontSize: 17,
      color: sugg,
      letterSpacing: -0.43,
      lineHeight: '22px'
    }
  }, w)))), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 13,
      padding: '0 6.5px',
      width: '100%',
      boxSizing: 'border-box',
      position: 'relative'
    }
  }, row(['q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p']), row(['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l'], 20), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 14.25,
      alignItems: 'center'
    }
  }, key(icons.shift, {
    w: 45,
    k: 'shift'
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6.5,
      flex: 1
    }
  }, ['z', 'x', 'c', 'v', 'b', 'n', 'm'].map(l => key(l, {
    flex: true,
    k: l
  }))), key(icons.del, {
    w: 45,
    k: 'del'
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 6,
      alignItems: 'center'
    }
  }, key('ABC', {
    w: 92.25,
    fs: 18,
    k: 'abc'
  }), key('', {
    flex: true,
    k: 'space'
  }), key(icons.ret, {
    w: 92.25,
    ret: true,
    k: 'ret'
  }))), /*#__PURE__*/React.createElement("div", {
    style: {
      height: 56,
      width: '100%',
      position: 'relative'
    }
  }));
}
Object.assign(window, {
  IOSDevice,
  IOSStatusBar,
  IOSNavBar,
  IOSGlassPill,
  IOSList,
  IOSListRow,
  IOSKeyboard
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/student-mobile/ios-frame.jsx", error: String((e && e.message) || e) }); }

})();
