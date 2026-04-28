// Grocery List — phone (390 wide), big checkable rows, organized by aisle.

const GroceryListPhone = () => {
  const aisles = [
    { n: 'Produce', items: [['Lacinato kale', '1 bunch', false], ['Lemons', '4', false], ['Parsley', '1 bunch', true]] },
    { n: 'Pantry', items: [['Orecchiette', '1 lb', false], ['Calabrian chili paste', '1 jar', false], ['Panko', '1 box', true]] },
    { n: 'Refrigerated', items: [['Eggs', '1 dozen', false]] },
  ];

  return (
    <div className="cb" style={{ width: '100%', height: '100%', background: 'var(--cream)', display: 'flex', flexDirection: 'column' }}>
      {/* phone status bar */}
      <div className="num" style={{ height: 38, padding: '0 22px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 14, fontWeight: 600 }}>
        <span>9:41</span>
        <span style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 12 }}>● ● ● ●</span>
      </div>
      <header style={{ padding: '12px 20px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <Icons.arrowL s={20}/>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 17, fontWeight: 600, letterSpacing: '-0.015em' }}>This week</div>
          <div style={{ fontSize: 12, color: 'var(--ink-3)' }}>7 items · from 3 recipes</div>
        </div>
        <Icons.share s={18}/>
        <Icons.more s={18}/>
      </header>

      <div style={{ padding: '0 14px 14px' }}>
        {/* progress */}
        <div style={{ background: 'var(--paper)', border: '1px solid var(--line)', borderRadius: 14, padding: 14, marginBottom: 14 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 8 }}>
            <span style={{ fontSize: 13, fontWeight: 500 }}>Progress</span>
            <span className="num" style={{ fontSize: 13, color: 'var(--ink-3)' }}>2 / 7</span>
          </div>
          <div style={{ height: 6, background: 'var(--cream-2)', borderRadius: 3, overflow: 'hidden' }}>
            <div style={{ width: '28%', height: '100%', background: 'var(--accent)' }}/>
          </div>
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '0 14px 24px' }}>
        {aisles.map((aisle, ai) => (
          <section key={ai} style={{ marginBottom: 18 }}>
            <div className="eyebrow" style={{ padding: '0 6px', marginBottom: 8 }}>{aisle.n}</div>
            <div style={{ background: 'var(--paper)', border: '1px solid var(--line)', borderRadius: 14, overflow: 'hidden' }}>
              {aisle.items.map(([n, q, done], i) => (
                <div key={i} style={{
                  display: 'flex', alignItems: 'center', gap: 14,
                  padding: '14px 16px',
                  borderBottom: i < aisle.items.length - 1 ? '1px solid var(--line)' : 'none',
                  opacity: done ? 0.45 : 1
                }}>
                  <div style={{
                    width: 24, height: 24, borderRadius: 12,
                    border: done ? 0 : '2px solid var(--line-strong)',
                    background: done ? 'var(--accent)' : 'transparent',
                    display: 'grid', placeItems: 'center', flexShrink: 0
                  }}>
                    {done && <Icons.check s={14}/>}
                  </div>
                  <div style={{ flex: 1, fontSize: 15, fontWeight: 500, textDecoration: done ? 'line-through' : 'none' }}>
                    {n}
                  </div>
                  <div className="num" style={{ fontSize: 13, color: 'var(--ink-3)' }}>{q}</div>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>

      {/* Bottom action */}
      <div style={{ padding: '10px 14px 20px', borderTop: '1px solid var(--line)', background: 'var(--cream)' }}>
        <button className="cb-btn accent" style={{ width: '100%', justifyContent: 'center', height: 50, fontSize: 15, borderRadius: 25 }}>
          <Icons.plus s={16}/> Add item
        </button>
      </div>
      {/* home indicator */}
      <div style={{ height: 8, display: 'flex', justifyContent: 'center', alignItems: 'center', paddingBottom: 6 }}>
        <div style={{ width: 120, height: 4, borderRadius: 2, background: 'var(--ink)' }}/>
      </div>
    </div>
  );
};

window.GroceryListPhone = GroceryListPhone;
