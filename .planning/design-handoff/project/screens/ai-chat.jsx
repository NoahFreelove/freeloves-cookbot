// AI Chat — left rail conversation, right canvas where streaming text builds
// a recipe card live. The "this is a recipe, save it" affordance is the
// canvas itself, not a button hidden in chat.

const AiChat = () => {
  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="ai" />
      <main style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <TopBar
          title="AI Assistant"
          breadcrumb="generate"
          right={<span style={{ fontSize: 12.5, color: 'var(--ink-3)' }}>claude haiku 4.5 · streaming</span>}
        />

        <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', flex: 1, minHeight: 0 }}>
          {/* Left rail — chat */}
          <section style={{
            borderRight: '1px solid var(--line)',
            display: 'flex', flexDirection: 'column',
            background: 'var(--paper-2)'
          }}>
            <div style={{ flex: 1, padding: '24px 22px', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 18 }}>

              <div className="eyebrow">Today, 7:14 PM</div>

              <div>
                <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginBottom: 5, fontWeight: 500 }}>You</div>
                <div style={{
                  fontSize: 14, lineHeight: 1.5, color: 'var(--ink)',
                  background: 'var(--paper)', padding: '12px 14px',
                  borderRadius: 12, border: '1px solid var(--line)'
                }}>
                  A weeknight pasta using anchovies, garlic, and the kale that's about to wilt. 4 servings.
                </div>
              </div>

              <div>
                <div style={{ fontSize: 11.5, color: 'var(--accent)', marginBottom: 5, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Icons.spark s={11}/> CookBot
                </div>
                <div style={{ fontSize: 13.5, lineHeight: 1.55, color: 'var(--ink-2)' }}>
                  Building a recipe on the right. I'm leaning into the anchovy-garlic-kale base with orecchiette to catch the sauce. Browning the kale stems first instead of discarding.
                </div>
              </div>

              <div>
                <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginBottom: 5, fontWeight: 500 }}>You</div>
                <div style={{
                  fontSize: 14, lineHeight: 1.5, color: 'var(--ink)',
                  background: 'var(--paper)', padding: '12px 14px',
                  borderRadius: 12, border: '1px solid var(--line)'
                }}>
                  Make it dairy-free, and add chili.
                </div>
              </div>

              <div>
                <div style={{ fontSize: 11.5, color: 'var(--accent)', marginBottom: 5, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Icons.spark s={11}/> CookBot · revising
                </div>
                <div style={{ fontSize: 13.5, lineHeight: 1.55, color: 'var(--ink-2)' }}>
                  Swapping out the parmesan finish for toasted breadcrumbs and adding calabrian chili. Updated on the right
                  <span style={{ display: 'inline-block', width: 7, height: 14, background: 'var(--accent)', verticalAlign: 'text-bottom', marginLeft: 2, animation: 'cb-blink 1s steps(2) infinite' }} />
                </div>
              </div>
            </div>

            {/* Chat input */}
            <div style={{ padding: '14px 18px 18px', borderTop: '1px solid var(--line)', background: 'var(--paper-2)' }}>
              <div style={{
                background: 'var(--paper)', border: '1px solid var(--line-strong)',
                borderRadius: 14, padding: '10px 12px',
                display: 'flex', flexDirection: 'column', gap: 8
              }}>
                <div style={{ color: 'var(--ink-3)', fontSize: 13.5 }}>Refine the recipe…</div>
                <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                  <button className="cb-chip">make spicier</button>
                  <button className="cb-chip">half it</button>
                  <button className="cb-chip">vegan</button>
                  <div style={{ flex: 1 }} />
                  <button style={{
                    width: 30, height: 30, borderRadius: 15, border: 0,
                    background: 'var(--accent)', color: '#fff',
                    display: 'grid', placeItems: 'center', cursor: 'pointer'
                  }}>
                    <Icons.send s={15}/>
                  </button>
                </div>
              </div>
              <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 8, textAlign: 'center' }}>
                Recipe stays a draft until you save it
              </div>
            </div>
          </section>

          {/* Right canvas — streaming recipe card */}
          <section style={{ overflow: 'auto', padding: '32px 48px', position: 'relative' }}>
            {/* Save bar */}
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              marginBottom: 20
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span className="cb-chip" style={{ background: 'var(--accent-soft)', color: 'var(--accent-ink)' }}>
                  <span style={{ width: 6, height: 6, borderRadius: 3, background: 'var(--accent)', display: 'inline-block', animation: 'cb-pulse 1.4s ease-in-out infinite' }}/>
                  drafting · 87%
                </span>
                <span style={{ fontSize: 12.5, color: 'var(--ink-3)' }}>2 revisions · uses 11 of 14 pantry items</span>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="cb-btn ghost"><Icons.copy s={14}/> Copy JSON</button>
                <button className="cb-btn"><Icons.save s={14}/> Save to cookbook</button>
              </div>
            </div>

            <div className="cb-card" style={{ padding: '40px 44px', background: 'var(--paper)' }}>
              <div className="eyebrow" style={{ marginBottom: 12 }}>Weeknight · 28 min · serves 4</div>
              <h1 style={{ fontSize: 44, lineHeight: 1.05, letterSpacing: '-0.03em', marginBottom: 14, textWrap: 'balance' }}>
                Anchovy &amp; Calabrian Chili Orecchiette with Charred Kale
              </h1>
              <p style={{ fontSize: 15, lineHeight: 1.55, color: 'var(--ink-2)', maxWidth: 580, marginBottom: 28 }}>
                A pantry-forward weeknight pasta. The anchovies dissolve into the oil; the kale stems brown first to keep nothing wasted. Toasted breadcrumbs replace the cheese for crunch.
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.4fr', gap: 36 }}>
                <div>
                  <div className="eyebrow" style={{ marginBottom: 10 }}>Ingredients</div>
                  {[
                    ['1 lb', 'orecchiette'],
                    ['¼ cup', 'olive oil'],
                    ['8 fillets', 'oil-packed anchovies'],
                    ['6 cloves', 'garlic, sliced'],
                    ['1 tbsp', 'calabrian chili paste'],
                    ['1 bunch', 'lacinato kale, stems diced'],
                    ['½ cup', 'panko, toasted'],
                    ['1', 'lemon, zested'],
                  ].map(([q, n], i) => (
                    <div key={i} style={{
                      display: 'flex', gap: 12, padding: '8px 0',
                      borderBottom: '1px solid var(--line)',
                      fontSize: 14
                    }}>
                      <span className="num" style={{ width: 70, color: 'var(--ink-2)', fontWeight: 500 }}>{q}</span>
                      <span style={{ color: 'var(--ink-2)' }}>{n}</span>
                    </div>
                  ))}
                  <div style={{ display: 'flex', gap: 6, marginTop: 16, flexWrap: 'wrap' }}>
                    <span className="cb-chip tag">dairy-free</span>
                    <span className="cb-chip tag">spicy</span>
                    <span className="cb-chip tag">pantry</span>
                  </div>
                </div>

                <div>
                  <div className="eyebrow" style={{ marginBottom: 10 }}>Method</div>
                  {[
                    "Bring a large pot of salted water to a boil. Cook the orecchiette to 1 minute shy of al dente; reserve ¾ cup pasta water.",
                    "Meanwhile, warm olive oil over medium-low. Add anchovies and stir until they melt into the oil, about 2 min. Add garlic and chili; cook 1 min.",
                    "Stir in the diced kale stems. Cook until tender and lightly browned, 4 min. Add the leaves and a splash of pasta water; cover briefly to wilt.",
                  ].map((s, i) => (
                    <div key={i} style={{ display: 'flex', gap: 16, padding: '12px 0', borderBottom: '1px solid var(--line)' }}>
                      <span className="num" style={{
                        width: 28, height: 28, borderRadius: 14,
                        background: 'var(--cream-2)', display: 'grid', placeItems: 'center',
                        fontSize: 13, fontWeight: 600, color: 'var(--ink-2)', flexShrink: 0
                      }}>{i+1}</span>
                      <span style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--ink-2)' }}>{s}</span>
                    </div>
                  ))}
                  <div style={{ display: 'flex', gap: 16, padding: '12px 0', borderBottom: '1px solid var(--line)' }}>
                    <span className="num" style={{ width: 28, height: 28, borderRadius: 14, background: 'var(--accent-soft)', display: 'grid', placeItems: 'center', fontSize: 13, fontWeight: 600, color: 'var(--accent)', flexShrink: 0 }}>4</span>
                    <span style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--ink-2)' }}>
                      Toss the pasta into the pan with the lemon zest and toasted panko<span style={{ display: 'inline-block', width: 7, height: 14, background: 'var(--accent)', verticalAlign: 'text-bottom', marginLeft: 2, animation: 'cb-blink 1s steps(2) infinite' }}/>
                    </span>
                  </div>
                  <div style={{ display: 'flex', gap: 16, padding: '12px 0', opacity: 0.35 }}>
                    <span className="num" style={{ width: 28, height: 28, borderRadius: 14, background: 'var(--cream-2)', display: 'grid', placeItems: 'center', fontSize: 13, fontWeight: 600, color: 'var(--ink-3)', flexShrink: 0 }}>5</span>
                    <span style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--ink-3)' }}>—</span>
                  </div>
                </div>
              </div>
            </div>
            <style>{`
              @keyframes cb-blink { 50% { opacity: 0; } }
              @keyframes cb-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.3; } }
            `}</style>
          </section>
        </div>
      </main>
    </div>
  );
};

window.AiChat = AiChat;
