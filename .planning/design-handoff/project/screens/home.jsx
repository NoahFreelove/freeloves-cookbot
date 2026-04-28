// Home dashboard — desktop. Earns its space: recently cooked + suggested next +
// "what can I make from my pantry tonight" — not just stat counters.

const HomeDashboard = ({ aiOff = false }) => {
  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="home" aiOff={aiOff} />
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar title="Home" sub="Tuesday · 7:14 PM" />
        <div style={{ padding: '32px 32px 64px', maxWidth: 1180 }}>

          {/* Greeting + quick actions */}
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 24, marginBottom: 28 }}>
            <div style={{ flex: 1 }}>
              <div className="eyebrow" style={{ marginBottom: 10 }}>Welcome back, Maya</div>
              <h1 style={{ fontSize: 40, lineHeight: 1.05, letterSpacing: '-0.03em', textWrap: 'balance' }}>
                What's the kitchen
                <br />
                up to tonight?
              </h1>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              {!aiOff && (
                <button className="cb-btn accent">
                  <Icons.spark s={15} /> Generate a recipe
                </button>
              )}
              <button className="cb-btn ghost">
                <Icons.plus s={15} /> New recipe
              </button>
              <button className="cb-btn ghost">
                <Icons.cart s={15} /> New list
              </button>
            </div>
          </div>

          {/* Hero card — Tonight from your pantry */}
          <section className="cb-card" style={{ padding: 28, marginBottom: 24, position: 'relative', overflow: 'hidden' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 32 }}>
              <div>
                <div className="eyebrow" style={{ marginBottom: 8, color: 'var(--accent)' }}>Tonight from your pantry</div>
                <h2 style={{ fontSize: 30, letterSpacing: '-0.025em', marginBottom: 8, lineHeight: 1.1 }}>
                  Three recipes match what's in stock.
                </h2>
                <p style={{ color: 'var(--ink-3)', fontSize: 14.5, lineHeight: 1.55, marginBottom: 18, maxWidth: 420 }}>
                  Based on the 47 items in your pantry. We avoided anything expiring after this week.
                </p>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 0, borderTop: '1px solid var(--line)' }}>
                  {[
                    { n: 'Cacio e Pepe', meta: '15 min · uses 4 of 6 ingredients', stock: 'in stock' },
                    { n: 'Sheet-pan Harissa Chickpeas', meta: '35 min · uses 7 of 9 ingredients', stock: 'missing parsley' },
                    { n: 'Brown Butter Soba', meta: '20 min · uses 5 of 5 ingredients', stock: 'in stock' },
                  ].map((r, i) => (
                    <div key={i} style={{
                      display: 'flex', alignItems: 'center', gap: 16,
                      padding: '14px 0', borderBottom: '1px solid var(--line)'
                    }}>
                      <span className="num" style={{ width: 24, color: 'var(--ink-4)', fontSize: 13, fontWeight: 500 }}>0{i+1}</span>
                      <div style={{ flex: 1 }}>
                        <div style={{ fontWeight: 500, fontSize: 15 }}>{r.n}</div>
                        <div style={{ fontSize: 12.5, color: 'var(--ink-3)', marginTop: 2 }}>{r.meta}</div>
                      </div>
                      <span className={'cb-chip ' + (r.stock === 'in stock' ? '' : 'tag')}
                        style={{ fontSize: 11.5, color: r.stock === 'in stock' ? 'var(--green)' : 'var(--warn)' }}>
                        {r.stock === 'in stock' ? <Icons.check s={12}/> : <Icons.cart s={12}/>}
                        {r.stock}
                      </span>
                      <Icons.arrowR s={16} />
                    </div>
                  ))}
                </div>
              </div>
              <div style={{ position: 'relative' }}>
                <Stripe w="100%" h={300} label="dish photo · primary" />
                <div style={{
                  position: 'absolute', left: 16, bottom: 16,
                  background: 'rgba(35,26,14,0.92)', color: 'var(--cream)',
                  padding: '10px 14px', borderRadius: 10, fontSize: 12.5,
                  display: 'flex', alignItems: 'center', gap: 8
                }}>
                  <Icons.flame s={14} />
                  <span style={{ fontWeight: 500 }}>Cacio e Pepe</span>
                  <span style={{ opacity: 0.6 }}>· 15 min</span>
                </div>
              </div>
            </div>
          </section>

          {/* 4 stat tiles — repurposed: not just counts, but glances */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16, marginBottom: 32 }}>
            <div className="cb-stat">
              <div>
                <div className="l">Recipes</div>
                <div className="num v">128</div>
              </div>
              <div style={{ fontSize: 12, color: 'var(--ink-3)' }}>+3 this week</div>
            </div>
            <div className="cb-stat">
              <div>
                <div className="l">Cookbooks</div>
                <div className="num v">7</div>
              </div>
              <div style={{ fontSize: 12, color: 'var(--ink-3)' }}>2 shared with the house</div>
            </div>
            <div className="cb-stat">
              <div>
                <div className="l">Pantry items</div>
                <div className="num v">47</div>
              </div>
              <div style={{ fontSize: 12, color: 'var(--warn)' }}>4 low · 1 expiring</div>
            </div>
            <div className="cb-stat">
              <div>
                <div className="l">Grocery</div>
                <div className="num v">12</div>
              </div>
              <div style={{ fontSize: 12, color: 'var(--ink-3)' }}>list updated 2h ago</div>
            </div>
          </div>

          {/* Two-up: recently cooked + queue */}
          <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 16 }}>
            <section className="cb-card" style={{ padding: 22 }}>
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 14 }}>
                <h3 style={{ fontSize: 16 }}>Recently cooked</h3>
                <span style={{ fontSize: 12.5, color: 'var(--ink-3)' }}>last 14 days</span>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                {['Miso roast carrots', 'Bolognese, sunday', 'Sourdough #38', 'Banh mi'].map((n, i) => (
                  <div key={i}>
                    <Stripe w="100%" h={92} label={i === 2 ? 'in-progress' : 'cooked'} />
                    <div style={{ fontSize: 13, fontWeight: 500, marginTop: 8 }}>{n}</div>
                    <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 2 }}>
                      {['mon', 'sun', 'sat', 'thu'][i]} · {[2, 1, 3, 1][i]}× this month
                    </div>
                  </div>
                ))}
              </div>
            </section>

            <section className="cb-card" style={{ padding: 22 }}>
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 14 }}>
                <h3 style={{ fontSize: 16 }}>Up next</h3>
                <span style={{ fontSize: 12.5, color: 'var(--ink-3)' }}>queued</span>
              </div>
              {[
                { n: 'Tartine country loaf', when: 'starts 9 PM · autolyse' },
                { n: 'Slow short rib', when: 'sat · 6h braise' },
                { n: 'Citrus tart', when: 'sun · for hannah' },
              ].map((q, i) => (
                <div key={i} style={{
                  display: 'flex', alignItems: 'center', gap: 12,
                  padding: '10px 0', borderBottom: i < 2 ? '1px solid var(--line)' : 'none'
                }}>
                  <div style={{
                    width: 32, height: 32, borderRadius: 8,
                    background: 'var(--cream-2)',
                    display: 'grid', placeItems: 'center',
                  }}>
                    <Icons.clock s={15} />
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 13.5, fontWeight: 500 }}>{q.n}</div>
                    <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 1 }}>{q.when}</div>
                  </div>
                  <Icons.more s={15} />
                </div>
              ))}
            </section>
          </div>

        </div>
      </main>
    </div>
  );
};

window.HomeDashboard = HomeDashboard;
