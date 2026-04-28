// Prompt Builder — copyable system prompt for external LLMs.

const PromptBuilder = ({ aiOff = false }) => {
  return (
    <div className="cb cb-shell" style={{ width: '100%', height: '100%' }}>
      <Sidebar active="prompt" aiOff={aiOff}/>
      <main style={{ overflow: 'auto', height: '100%' }}>
        <TopBar title="Prompt Builder" sub="for ChatGPT, Gemini, Claude.ai" right={
          <button className="cb-btn"><Icons.copy s={14}/> Copy prompt</button>
        }/>

        <div style={{ padding: '32px 32px 64px', maxWidth: 1180, display: 'grid', gridTemplateColumns: '320px 1fr', gap: 32 }}>
          {/* Options */}
          <aside>
            <div className="eyebrow" style={{ marginBottom: 14 }}>Configure</div>

            <div className="cb-card" style={{ padding: 18, marginBottom: 14 }}>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Output format</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {[['Canonical JSON (CookBot can re-import)', true], ['Markdown', false], ['Plain text', false]].map(([l, on], i) => (
                  <label key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13.5, cursor: 'pointer' }}>
                    <span style={{
                      width: 18, height: 18, borderRadius: 9,
                      border: on ? '5px solid var(--accent)' : '2px solid var(--line-strong)',
                      background: on ? 'var(--paper)' : 'transparent'
                    }}/>
                    {l}
                  </label>
                ))}
              </div>
            </div>

            <div className="cb-card" style={{ padding: 18, marginBottom: 14 }}>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Include</div>
              {['Pantry context (47 items)', 'Dietary preferences', 'Equipment list', 'Past favorites'].map((l, i) => (
                <label key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13.5, padding: '6px 0', cursor: 'pointer' }}>
                  <span style={{
                    width: 16, height: 16, borderRadius: 4,
                    background: i < 2 ? 'var(--accent)' : 'transparent',
                    border: i < 2 ? 0 : '1.5px solid var(--line-strong)',
                    display: 'grid', placeItems: 'center', color: '#fff'
                  }}>
                    {i < 2 && <Icons.check s={11}/>}
                  </span>
                  {l}
                </label>
              ))}
            </div>

            <div className="cb-card" style={{ padding: 18 }}>
              <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 10 }}>Voice</div>
              <select style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: '1px solid var(--line)', fontFamily: 'inherit', background: 'var(--paper)' }}>
                <option>Neutral · concise</option>
                <option>Warm · home cook</option>
                <option>Technical · pro kitchen</option>
              </select>
            </div>
          </aside>

          {/* Prompt preview */}
          <div>
            <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 12 }}>
              <div className="eyebrow">Generated prompt</div>
              <span style={{ fontSize: 12, color: 'var(--ink-3)' }} className="num">2,184 chars · ~520 tokens</span>
            </div>
            <pre className="mono" style={{
              background: 'var(--ink)', color: 'var(--cream)',
              padding: 28, borderRadius: 14, fontSize: 12.5, lineHeight: 1.65,
              whiteSpace: 'pre-wrap', margin: 0,
              fontFamily: 'var(--f-mono)'
            }}>
{`You are a recipe-generation assistant for CookBot, a home
cookbook app. Generate exactly ONE recipe per request.

OUTPUT FORMAT
Return a single JSON object matching this schema, no prose:

{
  "title": string,
  "summary": string,        // 1–2 sentences
  "servings": number,
  "active_minutes": number,
  "total_minutes": number,
  "ingredients": [{ "qty": string, "unit": string, "name": string }],
  "steps": [{ "text": string, "timer_minutes"?: number,
              "uses_ingredients"?: string[] }],
  "tags": string[]
}

CONSTRAINTS
- Use only common home-kitchen equipment.
- Prefer ingredients already present in the user's PANTRY.
- Respect DIETARY restrictions strictly.

`}<span style={{ color: 'var(--accent-soft)' }}>{`PANTRY (excerpt)
- Flour, AP — 2.4 kg
- Olive oil, evoo — 600 ml
- Pecorino — 180 g  …41 more

DIETARY
- household: no shellfish
- maya: dairy-light preferred`}</span>{`

USER REQUEST → describe a dish to generate.`}
            </pre>
          </div>
        </div>
      </main>
    </div>
  );
};

window.PromptBuilder = PromptBuilder;
