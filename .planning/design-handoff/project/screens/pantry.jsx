// Pantry — desktop. Stock view with categories + AI populate (hidden if AI off).

const Pantry = ({ aiOff = false }) => {
  const cats = [
    { n: 'Dry goods',    items: [['Flour, AP', '2.4 kg', 'in'], ['Rice, jasmine', '1.1 kg', 'in'], ['Pasta, orecchiette', '300 g', 'low'], ['Pasta, spaghetti', '900 g', 'in']] },
    { n: 'Oils & vinegars', items: [['Olive oil, evoo', '600 ml', 'in'], ['Sesame oil', '120 ml', 'low'], ['Rice vinegar', '400 ml', 'in']] },
    { n: 'Produce',      items: [['Lacinato kale', '1 bunch', 'expiring'], ['Lemons', '4', 'in'], ['Garlic', '~12 cloves', 'in']] },
    { n: 'Dairy',        items: [['Pecorino', '180 g', 'in'], ['Butter, unsalted', '500 g', 'in'], ['Eggs', '8', 'low']] },
  ];

  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="pantry" aiOff={aiOff}/>
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar title="Pantry" sub="47 items · last sync 2h ago" right={<>
          {!aiOff && <button className="cb-btn ghost"><Icons.spark s={14}/> AI standardize</button>}
          {!aiOff && <button className="cb-btn ghost"><Icons.spark s={14}/> AI populate</button>}
          <button className="cb-btn"><Icons.plus s={14}/> Add item</button>
        </>}/>

        <div style={{ padding: '32px 32px 64px', maxWidth: 1180 }}>
          {/* Summary strip */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginBottom: 28 }}>
            {[
              { l: 'In stock', v: 38, c: 'var(--green)' },
              { l: 'Running low', v: 4, c: 'var(--warn)' },
              { l: 'Expiring this week', v: 1, c: 'var(--accent)' },
              { l: 'Out', v: 4, c: 'var(--ink-3)' },
            ].map((s, i) => (
              <div key={i} className="cb-card" style={{ padding: 16, display: 'flex', alignItems: 'center', gap: 12 }}>
                <div style={{ width: 8, height: 36, borderRadius: 4, background: s.c }}/>
                <div>
                  <div className="num" style={{ fontSize: 26, fontWeight: 600, letterSpacing: '-0.02em' }}>{s.v}</div>
                  <div style={{ fontSize: 11.5, color: 'var(--ink-3)' }}>{s.l}</div>
                </div>
              </div>
            ))}
          </div>

          {/* Search */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 18 }}>
            <input placeholder="Find an item…" style={{
              flex: 1, maxWidth: 360, height: 36, padding: '0 14px',
              background: 'var(--paper)', border: '1px solid var(--line)',
              borderRadius: 18, fontSize: 13.5, fontFamily: 'inherit', outline: 'none'
            }}/>
            <button className="cb-btn ghost" style={{ padding: '6px 12px', fontSize: 12.5 }}>All</button>
            <button className="cb-btn ghost" style={{ padding: '6px 12px', fontSize: 12.5, background: 'var(--cream-2)' }}>Low only</button>
            <button className="cb-btn ghost" style={{ padding: '6px 12px', fontSize: 12.5 }}>Expiring</button>
          </div>

          {/* Category sections */}
          {cats.map((cat, ci) => (
            <section key={ci} style={{ marginBottom: 28 }}>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 8 }}>
                <h3 style={{ fontSize: 14, letterSpacing: '-0.01em' }}>{cat.n}</h3>
                <span style={{ fontSize: 12, color: 'var(--ink-3)' }}>{cat.items.length} items</span>
              </div>
              <div className="cb-card" style={{ overflow: 'hidden' }}>
                {cat.items.map(([n, q, status], i) => (
                  <div key={i} style={{
                    display: 'grid', gridTemplateColumns: '1fr 120px 110px 80px',
                    alignItems: 'center', padding: '12px 18px',
                    borderBottom: i < cat.items.length - 1 ? '1px solid var(--line)' : 'none',
                    background: status === 'expiring' ? 'rgba(194,65,12,0.04)' : 'transparent'
                  }}>
                    <div style={{ fontSize: 14, fontWeight: 500 }}>{n}</div>
                    <div className="num" style={{ fontSize: 13.5, color: 'var(--ink-2)' }}>{q}</div>
                    <div>
                      <span className="cb-chip" style={{
                        background: status === 'in' ? 'var(--green-soft)' : status === 'low' ? 'var(--warn-soft)' : 'var(--accent-soft)',
                        color: status === 'in' ? 'var(--green)' : status === 'low' ? 'var(--warn)' : 'var(--accent-ink)'
                      }}>
                        {status === 'in' ? 'in stock' : status === 'low' ? 'running low' : 'expires sat'}
                      </span>
                    </div>
                    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                      <button style={{ background: 'transparent', border: 0, color: 'var(--ink-3)', cursor: 'pointer' }}><Icons.cart s={15}/></button>
                      <button style={{ background: 'transparent', border: 0, color: 'var(--ink-3)', cursor: 'pointer' }}><Icons.more s={15}/></button>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
      </main>
    </div>
  );
};

window.Pantry = Pantry;
