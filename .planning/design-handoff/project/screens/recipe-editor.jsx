// Recipe Editor — desktop. Author/edit recipe. Chip-based step composer with
// inline timer chips and ingredient-reference chips, but cleaner — no special
// syntax visible to the user.

const RecipeEditor = () => {
  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="cookbooks" />
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar
          title="Editing: Brown Butter Soba"
          breadcrumb="Cookbooks / Weeknight"
          right={<>
            <button className="cb-btn ghost"><Icons.copy s={14}/> Paste raw text</button>
            <button className="cb-btn ghost">Cancel</button>
            <button className="cb-btn"><Icons.save s={14}/> Save</button>
          </>}
        />

        <div style={{ maxWidth: 1180, margin: '0 auto', padding: '32px 32px 80px', display: 'grid', gridTemplateColumns: '1fr 320px', gap: 32 }}>
          <div>
            {/* Title */}
            <input defaultValue="Brown Butter Soba"
              style={{
                fontSize: 38, fontWeight: 600, letterSpacing: '-0.025em',
                width: '100%', border: 0, outline: 'none', background: 'transparent',
                fontFamily: 'inherit', color: 'var(--ink)', marginBottom: 6
              }} />
            <input defaultValue="A 20-minute weeknight noodle bowl with nutty browned butter, scallion, and lemon."
              style={{
                fontSize: 15, color: 'var(--ink-2)', width: '100%',
                border: 0, outline: 'none', background: 'transparent', fontFamily: 'inherit', marginBottom: 24
              }} />

            {/* Photo */}
            <Stripe w="100%" h={180} label="drag a photo here, or paste url" style={{ marginBottom: 32 }} />

            {/* Ingredients */}
            <div className="eyebrow" style={{ marginBottom: 12 }}>Ingredients</div>
            <div style={{ background: 'var(--paper)', border: '1px solid var(--line)', borderRadius: 12, overflow: 'hidden', marginBottom: 32 }}>
              {[
                ['200', 'g', 'soba noodles'],
                ['4', 'tbsp', 'unsalted butter'],
                ['3', '', 'scallions, sliced thin'],
                ['1', '', 'lemon, juiced'],
                ['', 'pinch', 'flaky salt'],
              ].map(([q, u, n], i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '60px 70px 1fr 28px', gap: 0, alignItems: 'center', borderBottom: i < 4 ? '1px solid var(--line)' : 'none' }}>
                  <input defaultValue={q} className="num" style={{ padding: '12px 14px', border: 0, background: 'transparent', fontFamily: 'inherit', fontSize: 14, fontWeight: 500, outline: 'none' }}/>
                  <input defaultValue={u} style={{ padding: '12px 8px', border: 0, background: 'transparent', fontFamily: 'inherit', fontSize: 13.5, color: 'var(--ink-3)', outline: 'none' }}/>
                  <input defaultValue={n} style={{ padding: '12px 8px', border: 0, background: 'transparent', fontFamily: 'inherit', fontSize: 14, outline: 'none' }}/>
                  <button style={{ background: 'transparent', border: 0, color: 'var(--ink-4)', cursor: 'pointer' }}><Icons.more s={16}/></button>
                </div>
              ))}
              <div style={{ padding: 10, borderTop: '1px solid var(--line)', background: 'var(--paper-2)' }}>
                <button className="cb-btn ghost" style={{ width: '100%', justifyContent: 'center', fontWeight: 500 }}>
                  <Icons.plus s={14}/> Add ingredient
                </button>
              </div>
            </div>

            {/* Steps with chip composer */}
            <div className="eyebrow" style={{ marginBottom: 12 }}>Steps</div>
            {[
              {
                n: 1,
                content: ["Bring water to boil. Cook ", { ing: 'soba' }, " for ", { timer: '4 min' }, ", then drain and rinse with cold water."]
              },
              {
                n: 2,
                content: ["In a small skillet, melt the ", { ing: 'butter' }, " over medium. Swirl until it foams and turns nutty brown — about ", { timer: '3 min' }, ". Don't walk away."]
              },
              {
                n: 3,
                content: ["Toss the noodles with the brown butter, ", { ing: 'scallions' }, ", and ", { ing: 'lemon' }, ". Season with ", { ing: 'flaky salt' }, "."]
              }
            ].map((step, si) => (
              <div key={si} style={{
                background: 'var(--paper)', border: '1px solid var(--line)',
                borderRadius: 12, padding: 18, marginBottom: 12,
                display: 'grid', gridTemplateColumns: '34px 1fr 28px', gap: 14, alignItems: 'flex-start'
              }}>
                <div className="num" style={{
                  width: 30, height: 30, borderRadius: 15,
                  background: 'var(--cream-2)', display: 'grid', placeItems: 'center',
                  fontSize: 13, fontWeight: 600, color: 'var(--ink-2)'
                }}>{step.n}</div>
                <div style={{ fontSize: 15, lineHeight: 1.6, color: 'var(--ink)', minHeight: 24 }}>
                  {step.content.map((c, i) =>
                    typeof c === 'string' ? <span key={i}>{c}</span> :
                    c.timer ? (
                      <span key={i} className="cb-chip timer" style={{ height: 22, padding: '0 8px', fontSize: 12.5, margin: '0 2px', verticalAlign: 'baseline' }}>
                        <Icons.clock s={11}/> {c.timer}
                      </span>
                    ) : (
                      <span key={i} className="cb-chip ing" style={{ height: 22, padding: '0 8px', fontSize: 12.5, margin: '0 2px', verticalAlign: 'baseline', background: 'var(--accent-soft)', color: 'var(--accent-ink)' }}>
                        {c.ing}
                      </span>
                    )
                  )}
                  {si === 2 && (
                    <span style={{ display: 'inline-block', width: 1, height: 16, background: 'var(--accent)', verticalAlign: 'text-bottom', marginLeft: 1, animation: 'cb-blink 1s steps(2) infinite' }}/>
                  )}
                </div>
                <button style={{ background: 'transparent', border: 0, color: 'var(--ink-4)', cursor: 'pointer' }}><Icons.more s={16}/></button>
              </div>
            ))}
            <button className="cb-btn ghost" style={{ marginTop: 8, width: '100%', justifyContent: 'center' }}>
              <Icons.plus s={14}/> Add step
            </button>

            <style>{`@keyframes cb-blink { 50% { opacity: 0; } }`}</style>
          </div>

          {/* Right rail — meta */}
          <aside style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div className="cb-card" style={{ padding: 18 }}>
              <div className="eyebrow" style={{ marginBottom: 10 }}>Cookbook</div>
              <div className="cb-row active" style={{ background: 'var(--cream-2)', color: 'var(--ink-2)' }}>
                <Icons.book s={14}/> Weeknight
                <Icons.chevD s={12}/>
              </div>
            </div>

            <div className="cb-card" style={{ padding: 18 }}>
              <div className="eyebrow" style={{ marginBottom: 10 }}>Times &amp; servings</div>
              {[
                ['Active', '5 min'],
                ['Total', '20 min'],
                ['Serves', '2'],
              ].map(([l, v], i) => (
                <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: i < 2 ? '1px solid var(--line)' : 'none', fontSize: 13.5 }}>
                  <span style={{ color: 'var(--ink-3)' }}>{l}</span>
                  <span className="num" style={{ fontWeight: 500 }}>{v}</span>
                </div>
              ))}
            </div>

            <div className="cb-card" style={{ padding: 18 }}>
              <div className="eyebrow" style={{ marginBottom: 10 }}>Tags</div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
                <span className="cb-chip">noodles ×</span>
                <span className="cb-chip">vegetarian ×</span>
                <span className="cb-chip">weeknight ×</span>
              </div>
              <input placeholder="add tag…" style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: '1px solid var(--line)', background: 'var(--cream)', fontFamily: 'inherit', fontSize: 13, outline: 'none' }}/>
            </div>

            <div className="cb-card" style={{ padding: 18, background: 'var(--accent-soft)', border: '1px solid var(--accent-soft)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
                <Icons.spark s={14}/>
                <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--accent-ink)' }}>AI suggestions</span>
              </div>
              <p style={{ fontSize: 12.5, color: 'var(--accent-ink)', lineHeight: 1.5, marginBottom: 10 }}>
                Try toasting sesame seeds for finish — only +1 min and matches the brown butter.
              </p>
              <button className="cb-btn ghost" style={{ fontSize: 12.5, padding: '6px 10px', borderColor: 'rgba(107,31,0,.25)' }}>Apply</button>
            </div>
          </aside>
        </div>
      </main>
    </div>
  );
};

window.RecipeEditor = RecipeEditor;
