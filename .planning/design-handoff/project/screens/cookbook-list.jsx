// Cookbook list/detail — grid view, scannable.

const CookbookList = () => {
  const books = [
    { n: 'Italian Weeknight', count: 28, c: 'var(--accent-soft)', who: 'Maya · shared with house' },
    { n: 'Sourdough Lab', count: 14, c: 'var(--cream-2)', who: 'Maya · private' },
    { n: 'Hannah\'s Bakes', count: 41, c: '#E1ECDF', who: 'Hannah · shared' },
    { n: 'Quick Lunches', count: 19, c: 'var(--accent-soft)', who: 'Maya · shared with house' },
    { n: 'Sunday Slow', count: 12, c: 'var(--cream-2)', who: 'Maya · shared' },
    { n: 'Pickles &amp; Ferments', count: 7, c: '#F0E2C8', who: 'Maya · private' },
  ];

  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="cookbooks" />
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar title="Cookbooks" right={<>
          <button className="cb-btn ghost"><Icons.download s={14}/> Import</button>
          <button className="cb-btn"><Icons.plus s={14}/> New cookbook</button>
        </>}/>
        <div style={{ padding: '32px 32px 64px', maxWidth: 1180 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
            <div style={{ position: 'relative', flex: 1, maxWidth: 360 }}>
              <Icons.search s={15} />
              <input placeholder="Search 128 recipes across 7 cookbooks…" style={{
                width: '100%', height: 38, padding: '0 14px 0 36px',
                background: 'var(--paper)', border: '1px solid var(--line)',
                borderRadius: 19, fontSize: 13.5, fontFamily: 'inherit', outline: 'none'
              }}/>
              <div style={{ position: 'absolute', left: 12, top: 11, color: 'var(--ink-3)' }}><Icons.search s={15}/></div>
            </div>
            <button className="cb-btn ghost" style={{ padding: '8px 12px', fontSize: 13 }}><Icons.filter s={14}/> Filters</button>
            <div style={{ flex: 1 }}/>
            <div style={{ display: 'flex', background: 'var(--paper)', border: '1px solid var(--line)', borderRadius: 8, padding: 2 }}>
              <button style={{ width: 30, height: 30, border: 0, background: 'var(--cream-2)', borderRadius: 6, cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icons.grid s={14}/></button>
              <button style={{ width: 30, height: 30, border: 0, background: 'transparent', borderRadius: 6, cursor: 'pointer', display: 'grid', placeItems: 'center', color: 'var(--ink-3)' }}><Icons.list s={14}/></button>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 18 }}>
            {books.map((b, i) => (
              <div key={i} className="cb-card" style={{ overflow: 'hidden', cursor: 'pointer' }}>
                <div style={{
                  height: 180, background: b.c, position: 'relative',
                  display: 'flex', alignItems: 'flex-end', padding: 16
                }}>
                  {/* fake thumbnail grid */}
                  <div style={{ position: 'absolute', inset: 12, display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 6 }}>
                    {Array.from({ length: 6 }).map((_, j) => (
                      <div key={j} style={{
                        background: 'rgba(255,255,255,0.55)', borderRadius: 4,
                        backgroundImage: 'repeating-linear-gradient(135deg, rgba(60,42,18,0.06) 0 1px, transparent 1px 8px)'
                      }}/>
                    ))}
                  </div>
                </div>
                <div style={{ padding: 18 }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
                    <h3 style={{ fontSize: 17, letterSpacing: '-0.015em' }} dangerouslySetInnerHTML={{__html: b.n}} />
                    <span className="num" style={{ fontSize: 13, color: 'var(--ink-3)', fontWeight: 500 }}>{b.count} <span style={{fontWeight:400}}>recipes</span></span>
                  </div>
                  <div style={{ fontSize: 12, color: 'var(--ink-3)' }}>{b.who}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </main>
    </div>
  );
};

window.CookbookList = CookbookList;
