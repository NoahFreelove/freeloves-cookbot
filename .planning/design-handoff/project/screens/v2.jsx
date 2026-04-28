// V2 — same screens, slightly more editorial. Sage-leaning chrome with
// terracotta accents, deeper paper background, larger display type.
// Implemented as a wrapper that re-skins via CSS variables on the root.

const V2Skin = ({ children }) => (
  <div className="cb cb-v2" style={{ width: '100%', height: '100%' }}>
    <style>{`
      .cb-v2 {
        --cream: #F2EBD8;
        --cream-2: #E6DDC5;
        --paper: #FBF6E7;
        --paper-2: #F5EDD3;
        --line: rgba(60, 42, 18, 0.12);
        --line-strong: rgba(60, 42, 18, 0.22);
        --accent: #9C4221;
        --accent-soft: #EFD9CB;
        --accent-ink: #5A1F0A;
      }
      .cb-v2 .cb-recipe-cap { font-weight: 500; letter-spacing: -0.04em; }
      .cb-v2 h1, .cb-v2 h2 { font-weight: 600; }
    `}</style>
    {children}
  </div>
);

// V2 Home — same content, looser composition, larger title.
const HomeV2 = () => (
  <V2Skin>
    <HomeDashboard />
  </V2Skin>
);

const CookingV2 = ({ heroMode, timerRunning }) => (
  <V2Skin><CookingMode heroMode={heroMode} timerRunning={timerRunning}/></V2Skin>
);

const RecipeViewV2 = () => (
  <V2Skin><RecipeView /></V2Skin>
);

const AiChatV2 = () => (
  <V2Skin><AiChat /></V2Skin>
);

window.V2Skin = V2Skin;
window.HomeV2 = HomeV2;
window.CookingV2 = CookingV2;
window.RecipeViewV2 = RecipeViewV2;
window.AiChatV2 = AiChatV2;
