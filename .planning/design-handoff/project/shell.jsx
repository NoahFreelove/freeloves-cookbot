// Shared shell — top bar + sidebar — plus tiny atoms reused across screens.

const Logo = ({ collapsed }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 6px 12px' }}>
    <div style={{
      width: 28, height: 28, borderRadius: 8,
      background: 'var(--accent)', color: '#fff',
      display: 'grid', placeItems: 'center',
      fontWeight: 700, fontSize: 14, letterSpacing: '-0.04em'
    }}>cb</div>
    {!collapsed && <div style={{ fontWeight: 600, fontSize: 15, letterSpacing: '-0.02em' }}>CookBot</div>}
  </div>
);

const NavRow = ({ icon: I, label, active, hidden, kbd }) => {
  if (hidden) return null;
  return (
    <div className={'cb-row' + (active ? ' active' : '')}>
      <I s={18} />
      <span style={{ flex: 1 }}>{label}</span>
      {kbd && <span className="cb-kbd">{kbd}</span>}
    </div>
  );
};

const Sidebar = ({ active = 'home', aiOff = false, sectionLabel = null }) => (
  <aside className="side" style={{ width: 232 }}>
    <Logo />
    {sectionLabel && <div className="eyebrow" style={{ padding: '8px 12px 6px' }}>{sectionLabel}</div>}
    <NavRow icon={Icons.home}   label="Home"          active={active === 'home'} />
    <NavRow icon={Icons.book}   label="Cookbooks"     active={active === 'cookbooks'} />
    <NavRow icon={Icons.pantry} label="Pantry"        active={active === 'pantry'} />
    <NavRow icon={Icons.cart}   label="Grocery Lists" active={active === 'grocery'} />
    <div style={{ height: 1, background: 'var(--line)', margin: '10px 8px' }} />
    <NavRow icon={Icons.spark}  label="AI Assistant"  active={active === 'ai'} hidden={aiOff} />
    <NavRow icon={Icons.prompt} label="Prompt Builder" active={active === 'prompt'} hidden={aiOff} />
    <div style={{ flex: 1 }} />
    <NavRow icon={Icons.user}   label="Profile"       active={active === 'profile'} />
  </aside>
);

const TopBar = ({ title, sub, right, breadcrumb }) => (
  <header className="topbar">
    <Icons.menu s={18} />
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, flex: 1, minWidth: 0 }}>
      {breadcrumb && (
        <span style={{ color: 'var(--ink-3)', fontSize: 14 }}>
          {breadcrumb} <span style={{ margin: '0 6px', color: 'var(--ink-4)' }}>/</span>
        </span>
      )}
      <span style={{ fontWeight: 600, fontSize: 15, letterSpacing: '-0.01em' }}>{title}</span>
      {sub && <span style={{ color: 'var(--ink-3)', fontSize: 13 }}>{sub}</span>}
    </div>
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      {right}
      <button className="cb-btn ghost" style={{ padding: '6px 12px', fontSize: 13 }}>
        <Icons.user s={14} /> Maya
        <Icons.chevD s={12} />
      </button>
      <Icons.sun s={18} />
    </div>
  </header>
);

const Stripe = ({ w = '100%', h = 200, label = 'photo', style = {} }) => (
  <div className="cb-ph" style={{ width: w, height: h, borderRadius: 10, ...style }}>
    {label}
  </div>
);

window.Sidebar = Sidebar;
window.TopBar = TopBar;
window.NavRow = NavRow;
window.Logo = Logo;
window.Stripe = Stripe;
