# cookbot-site

The marketing/landing site for [Freelove's Cookbot](https://github.com/NoahFreelove/freeloves-cookbot),
served at **cookbot.noahfreelove.com** via Cloudflare Pages.

It is deliberately tiny: one hand-written `index.html`, one stylesheet, an SVG
favicon. No framework, no build step, no JavaScript. The aesthetic is a
mid-century recipe card — cocoa ink on cream.

## Layout

```
site/
├── public/            # everything here is served verbatim (pages_build_output_dir)
│   ├── index.html
│   ├── styles.css
│   ├── favicon.svg
│   ├── robots.txt
│   ├── sitemap.xml
│   └── _headers       # CSP + security headers (must live IN the served dir)
├── wrangler.toml
└── package.json
```

## Develop

```bash
npm install            # just pulls in wrangler
npm run dev            # serves public/ locally with the _headers applied
```

(For a quick look without wrangler, open `public/index.html` directly — the only
thing that won't apply is the `_headers` CSP.)

## Deploy

```bash
npm run deploy         # = wrangler pages deploy  (reads public/ from wrangler.toml)
```

First deploy creates the `cookbot` Pages project. Then, once, in the Cloudflare
dashboard: **Pages → cookbot → Custom domains → add `cookbot.noahfreelove.com`**.
The GoDaddy DNS already points the subdomain at Cloudflare, so the custom domain
attaches with no further DNS changes.

## Editing copy

All text lives in `public/index.html`; all styling/colors in `public/styles.css`
(see the `:root` token block at the top). If you add an image or any inline
script later, loosen the matching directive in `public/_headers` — the CSP is
intentionally strict (`script-src 'none'`).
