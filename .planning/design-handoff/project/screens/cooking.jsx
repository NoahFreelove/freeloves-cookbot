// Cooking Mode — tablet, hands-busy. Adaptive: timer-as-hero when running,
// step-as-hero otherwise. Ingredients always visible without tapping.

const CookingMode = ({ heroMode = 'adaptive', timerRunning = true }) => {
  const showTimer = heroMode === 'timer' || (heroMode === 'adaptive' && timerRunning);
  const showStep  = heroMode === 'step'  || (heroMode === 'adaptive' && !timerRunning);
  const showIng   = heroMode === 'ing';

  const stepNum = 4;
  const totalSteps = 9;
  const stepText = "Reduce heat to medium-low. Stir in the cold butter, one cube at a time, swirling to emulsify. Don't let it boil.";

  const ingredients = [
    { n: 'pasta water', q: '½ cup', this: true },
    { n: 'unsalted butter, cold', q: '4 tbsp', this: true },
    { n: 'pecorino, finely grated', q: '1¼ cup', this: false },
    { n: 'black pepper, cracked', q: '2 tsp', this: false },
  ];

  return (
    <div className="cb" style={{ width: '100%', height: '100%', background: 'var(--ink)', color: 'var(--cream)', display: 'flex', flexDirection: 'column' }}>
      {/* Tablet top bar — minimal, always-visible exit */}
      <header style={{
        height: 56, display: 'flex', alignItems: 'center',
        padding: '0 24px', gap: 16, borderBottom: '1px solid rgba(255,255,255,0.06)'
      }}>
        <button style={{ background: 'transparent', border: 0, color: 'var(--cream)', display: 'flex', alignItems: 'center', gap: 8, fontSize: 14, cursor: 'pointer', padding: 0 }}>
          <Icons.arrowL s={18} /> Exit
        </button>
        <div style={{ flex: 1, textAlign: 'center', fontSize: 14, fontWeight: 500 }}>
          Cacio e Pepe
          <span style={{ opacity: 0.5, marginLeft: 10 }}>· step {stepNum} of {totalSteps}</span>
        </div>
        <div className="cb-chip" style={{ background: 'rgba(255,255,255,0.08)', color: 'var(--cream)' }}>
          <Icons.bell s={12} /> notifications on
        </div>
      </header>

      {/* Step rail — top of screen, always visible */}
      <div style={{ display: 'flex', gap: 6, padding: '12px 24px 0' }}>
        {Array.from({ length: totalSteps }).map((_, i) => (
          <div key={i} style={{
            flex: 1, height: 4, borderRadius: 2,
            background: i < stepNum - 1 ? 'rgba(255,255,255,0.5)' :
                        i === stepNum - 1 ? 'var(--accent)' :
                        'rgba(255,255,255,0.12)'
          }} />
        ))}
      </div>

      {/* Main split: hero (left) + ingredients-this-step (right) */}
      <div style={{ flex: 1, display: 'grid', gridTemplateColumns: '1.6fr 1fr', minHeight: 0 }}>
        <section style={{ padding: '40px 48px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', minWidth: 0 }}>

          {/* Hero region — timer or step */}
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center', minHeight: 0 }}>
            {showTimer && (
              <div>
                <div style={{ fontSize: 12, letterSpacing: '0.16em', textTransform: 'uppercase', color: 'rgba(255,255,255,0.5)', marginBottom: 12 }}>
                  Pasta — boil until al dente
                </div>
                <div className="num" style={{
                  fontSize: 224, lineHeight: 0.92, fontWeight: 600,
                  letterSpacing: '-0.05em', color: 'var(--cream)',
                  fontFeatureSettings: '"tnum"',
                }}>
                  6:48
                </div>
                <div style={{ display: 'flex', gap: 12, marginTop: 28, alignItems: 'center' }}>
                  <button style={{
                    width: 64, height: 64, borderRadius: 32, border: 0,
                    background: 'var(--accent)', color: '#fff',
                    display: 'grid', placeItems: 'center', cursor: 'pointer'
                  }}>
                    <Icons.pause s={22} />
                  </button>
                  <button style={{
                    height: 44, padding: '0 18px', borderRadius: 22,
                    background: 'rgba(255,255,255,0.08)', border: 0, color: 'var(--cream)',
                    fontSize: 14, fontWeight: 500, cursor: 'pointer'
                  }}>+ 30s</button>
                  <button style={{
                    height: 44, padding: '0 18px', borderRadius: 22,
                    background: 'transparent', border: '1px solid rgba(255,255,255,0.18)',
                    color: 'var(--cream)', fontSize: 14, fontWeight: 500, cursor: 'pointer'
                  }}>Reset</button>
                </div>
                <div style={{ marginTop: 24, fontSize: 17, lineHeight: 1.45, color: 'rgba(255,255,255,0.78)', maxWidth: 540, textWrap: 'pretty' }}>
                  {stepText}
                </div>
              </div>
            )}

            {showStep && (
              <div>
                <div style={{ fontSize: 12, letterSpacing: '0.16em', textTransform: 'uppercase', color: 'rgba(255,255,255,0.5)', marginBottom: 16 }}>
                  Step {stepNum}
                </div>
                <div style={{
                  fontSize: 52, lineHeight: 1.12, fontWeight: 500,
                  letterSpacing: '-0.025em', textWrap: 'balance',
                  color: 'var(--cream)', maxWidth: 720
                }}>
                  {stepText}
                </div>
                <div style={{ display: 'flex', gap: 10, marginTop: 36 }}>
                  <button style={{
                    height: 56, padding: '0 22px', borderRadius: 28,
                    background: 'var(--accent)', border: 0, color: '#fff',
                    fontSize: 16, fontWeight: 500, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 10
                  }}>
                    <Icons.clock s={18}/> Start 7-min timer
                  </button>
                  <button style={{
                    height: 56, padding: '0 22px', borderRadius: 28,
                    background: 'rgba(255,255,255,0.08)', border: 0, color: 'var(--cream)',
                    fontSize: 16, fontWeight: 500, cursor: 'pointer',
                  }}>
                    <Icons.spark s={16}/> Ask about this step
                  </button>
                </div>
              </div>
            )}

            {showIng && (
              <div>
                <div style={{ fontSize: 12, letterSpacing: '0.16em', textTransform: 'uppercase', color: 'rgba(255,255,255,0.5)', marginBottom: 16 }}>
                  For this step
                </div>
                {ingredients.filter(i => i.this).map((ing, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'baseline', gap: 24, padding: '20px 0', borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
                    <div className="num" style={{ fontSize: 56, fontWeight: 600, letterSpacing: '-0.03em', minWidth: 180 }}>{ing.q}</div>
                    <div style={{ fontSize: 26, color: 'rgba(255,255,255,0.85)' }}>{ing.n}</div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Bottom step nav */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, paddingTop: 32, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
            <button style={{
              flex: 1, height: 64, borderRadius: 14,
              background: 'rgba(255,255,255,0.06)', border: 0, color: 'var(--cream)',
              fontSize: 15, fontWeight: 500, cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'flex-start', padding: '0 20px', gap: 14
            }}>
              <Icons.arrowL s={18}/>
              <span style={{ textAlign: 'left' }}>
                <div style={{ fontSize: 11.5, opacity: 0.6, letterSpacing: '0.05em', textTransform: 'uppercase' }}>Previous</div>
                <div>Drain pasta</div>
              </span>
            </button>
            <button style={{
              flex: 2, height: 64, borderRadius: 14,
              background: 'var(--accent)', border: 0, color: '#fff',
              fontSize: 17, fontWeight: 500, cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 24px'
            }}>
              <span style={{ textAlign: 'left' }}>
                <div style={{ fontSize: 11.5, opacity: 0.7, letterSpacing: '0.05em', textTransform: 'uppercase' }}>Next</div>
                <div>Add cheese off the heat</div>
              </span>
              <Icons.arrowR s={20}/>
            </button>
          </div>
        </section>

        {/* Right rail — ingredients always visible */}
        <aside style={{
          background: 'rgba(255,255,255,0.04)',
          borderLeft: '1px solid rgba(255,255,255,0.06)',
          padding: '32px 28px', overflowY: 'auto'
        }}>
          <div style={{ fontSize: 12, letterSpacing: '0.16em', textTransform: 'uppercase', color: 'rgba(255,255,255,0.5)', marginBottom: 14 }}>
            Ingredients
            <span style={{ marginLeft: 8, color: 'var(--accent)', textTransform: 'none', letterSpacing: 0 }}>· scaled 1.5×</span>
          </div>
          {ingredients.map((ing, i) => (
            <div key={i} style={{
              padding: '14px 12px',
              margin: '0 -12px',
              borderRadius: 10,
              background: ing.this ? 'rgba(194,65,12,0.18)' : 'transparent',
              border: ing.this ? '1px solid rgba(194,65,12,0.35)' : '1px solid transparent',
              marginBottom: 6
            }}>
              <div className="num" style={{
                fontSize: 22, fontWeight: 600, letterSpacing: '-0.02em',
                color: ing.this ? 'var(--cream)' : 'rgba(255,255,255,0.6)'
              }}>{ing.q}</div>
              <div style={{ fontSize: 14, color: ing.this ? 'rgba(255,255,255,0.8)' : 'rgba(255,255,255,0.5)', marginTop: 2 }}>{ing.n}</div>
            </div>
          ))}

          <div style={{
            marginTop: 24, padding: 14, borderRadius: 12,
            background: 'rgba(255,255,255,0.06)',
            display: 'flex', alignItems: 'center', gap: 10
          }}>
            <Icons.scale s={16}/>
            <div style={{ fontSize: 13, flex: 1 }}>
              <div style={{ fontWeight: 500 }}>Serves 6</div>
              <div style={{ opacity: 0.6, fontSize: 11.5, marginTop: 1 }}>scaled from 4</div>
            </div>
            <div style={{ display: 'flex', gap: 4 }}>
              <button style={{ width: 28, height: 28, borderRadius: 14, background: 'rgba(255,255,255,0.08)', border: 0, color: 'var(--cream)', cursor: 'pointer' }}>−</button>
              <button style={{ width: 28, height: 28, borderRadius: 14, background: 'rgba(255,255,255,0.08)', border: 0, color: 'var(--cream)', cursor: 'pointer' }}>+</button>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};

window.CookingMode = CookingMode;
