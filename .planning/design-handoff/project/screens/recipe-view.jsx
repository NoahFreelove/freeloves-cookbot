// Recipe View — editorial layout. Two-column: ingredients (sidebar) +
// method (body). Display-weight title, hanging numerals, scale control prominent.

const RecipeView = () => {
  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="cookbooks" />
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar
          title="Cacio e Pepe"
          breadcrumb={<><span>Cookbooks</span> <span style={{ margin: '0 6px', color: 'var(--ink-4)' }}>/</span> <span>Italian Weeknight</span></>}
          right={<>
            <button className="cb-btn ghost"><Icons.share s={14}/> Share</button>
            <button className="cb-btn accent"><Icons.flame s={14}/> Cook this</button>
          </>}
        />
        <article style={{ maxWidth: 1080, margin: '0 auto', padding: '40px 32px 80px' }}>

          {/* Hero */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 40, marginBottom: 48, alignItems: 'end' }}>
            <div>
              <div className="eyebrow" style={{ marginBottom: 14 }}>Pasta · Italian · Weeknight</div>
              <h1 className="cb-recipe-cap">
                Cacio<br/>e Pepe.
              </h1>
              <p style={{ fontSize: 17, lineHeight: 1.5, color: 'var(--ink-2)', marginTop: 20, maxWidth: 480, textWrap: 'pretty' }}>
                Three ingredients, no margin for error. The pasta water is the binder — pull it cold, swirl
                it furiously off the heat, and you get a glossy emulsion that clings to every strand.
              </p>
              <div style={{ display: 'flex', gap: 24, marginTop: 28, paddingTop: 20, borderTop: '1px solid var(--line)' }}>
                <div>
                  <div className="eyebrow" style={{ fontSize: 10 }}>Active</div>
                  <div className="num" style={{ fontSize: 22, fontWeight: 600, marginTop: 4 }}>15<span style={{ fontSize: 13, color: 'var(--ink-3)', marginLeft: 4 }}>min</span></div>
                </div>
                <div>
                  <div className="eyebrow" style={{ fontSize: 10 }}>Total</div>
                  <div className="num" style={{ fontSize: 22, fontWeight: 600, marginTop: 4 }}>20<span style={{ fontSize: 13, color: 'var(--ink-3)', marginLeft: 4 }}>min</span></div>
                </div>
                <div>
                  <div className="eyebrow" style={{ fontSize: 10 }}>Serves</div>
                  <div className="num" style={{ fontSize: 22, fontWeight: 600, marginTop: 4 }}>4</div>
                </div>
                <div>
                  <div className="eyebrow" style={{ fontSize: 10 }}>Made</div>
                  <div className="num" style={{ fontSize: 22, fontWeight: 600, marginTop: 4 }}>9<span style={{ fontSize: 13, color: 'var(--ink-3)', marginLeft: 4 }}>×</span></div>
                </div>
              </div>
            </div>
            <Stripe w="100%" h={420} label="hero photo · 4:3" />
          </div>

          {/* Two col */}
          <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: 56 }}>
            <aside style={{ position: 'sticky', top: 80, alignSelf: 'start' }}>
              <div className="eyebrow" style={{ marginBottom: 10 }}>Ingredients</div>

              <div style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '10px 12px', background: 'var(--paper)',
                border: '1px solid var(--line)', borderRadius: 10,
                marginBottom: 16
              }}>
                <Icons.scale s={16}/>
                <span style={{ fontSize: 13, color: 'var(--ink-3)', flex: 1 }}>Scale</span>
                <button style={{ width: 26, height: 26, borderRadius: 13, border: '1px solid var(--line)', background: 'var(--paper)', cursor: 'pointer' }}>−</button>
                <span className="num" style={{ minWidth: 32, textAlign: 'center', fontWeight: 600 }}>4</span>
                <button style={{ width: 26, height: 26, borderRadius: 13, border: '1px solid var(--line)', background: 'var(--paper)', cursor: 'pointer' }}>+</button>
              </div>

              {[
                ['400 g', 'tonnarelli or spaghetti'],
                ['200 g', 'pecorino romano, finely grated'],
                ['2 tsp', 'black pepper, freshly cracked'],
                ['4 tbsp', 'unsalted butter, cold, cubed'],
                ['—', 'kosher salt for the water'],
              ].map(([q, n], i) => (
                <div key={i} style={{
                  display: 'flex', gap: 12, padding: '10px 0',
                  borderBottom: '1px solid var(--line)',
                }}>
                  <span className="num" style={{ width: 64, color: 'var(--ink-2)', fontWeight: 500, fontSize: 14 }}>{q}</span>
                  <span style={{ color: 'var(--ink-2)', fontSize: 14, lineHeight: 1.45 }}>{n}</span>
                </div>
              ))}

              <div style={{ display: 'flex', gap: 6, marginTop: 14, flexWrap: 'wrap' }}>
                <span className="cb-chip tag">3 ingredients</span>
                <span className="cb-chip tag">vegetarian</span>
              </div>
            </aside>

            <div>
              <div className="eyebrow" style={{ marginBottom: 18 }}>Method</div>
              {[
                { t: 'Boil', b: 'Bring a generous pot of water to a hard boil and salt it well — about 1 tbsp per liter. The starchy water is half the recipe.' },
                { t: 'Toast the pepper', b: 'In a wide pan over medium-low, toast the cracked pepper dry until fragrant, about 1 minute. Add a splash of pasta water to bloom into a glaze.' },
                { t: 'Cook the pasta', b: 'Drop the pasta into the boiling water. Cook to 1 minute shy of al dente. Reserve a full cup of pasta water before draining.', timer: '8 min' },
                { t: 'Emulsify', b: 'Off the heat, add the cold butter cube by cube to the peppered pan, swirling. Add the drained pasta and a splash of pasta water; toss until glossy.' },
                { t: 'Add the cheese', b: 'Off the heat (this matters), shower in the pecorino while tossing. Add water in small splashes if it tightens. Serve immediately on warm plates.' },
              ].map((s, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '40px 1fr', gap: 16, padding: '20px 0', borderBottom: '1px solid var(--line)' }}>
                  <div className="num" style={{
                    fontSize: 28, fontWeight: 600, color: 'var(--accent)',
                    letterSpacing: '-0.03em', lineHeight: 1
                  }}>0{i+1}</div>
                  <div>
                    <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 6, letterSpacing: '-0.015em' }}>{s.t}</div>
                    <div style={{ fontSize: 15, lineHeight: 1.6, color: 'var(--ink-2)', textWrap: 'pretty' }}>{s.b}</div>
                    {s.timer && (
                      <div style={{ marginTop: 12 }}>
                        <span className="cb-chip timer"><Icons.clock s={12}/> {s.timer}</span>
                      </div>
                    )}
                  </div>
                </div>
              ))}

              <div style={{ marginTop: 28, padding: 20, background: 'var(--cream-2)', borderRadius: 12 }}>
                <div className="eyebrow" style={{ marginBottom: 8 }}>Notes from your last cook</div>
                <p style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--ink-2)', textWrap: 'pretty' }}>
                  "Used aged pecorino — too sharp. Try a 70/30 with parmigiano next time." <span style={{ color: 'var(--ink-3)' }}>— Mar 14</span>
                </p>
              </div>
            </div>
          </div>
        </article>
      </main>
    </div>
  );
};

window.RecipeView = RecipeView;
