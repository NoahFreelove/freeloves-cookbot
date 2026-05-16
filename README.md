# Freelove's Cook bot

*This app is completely vibecoded with Claude Opus 4.6, but it has been useful to me, so I*
 *publish it in hopes its useful to someone else too.*

I love cooking and baking, I use LLMs to generate or clean up a lot of my recipes because online recipe websites
have so much slop and just so many ads. To get to the recipe you have to scroll down two pages
of about the author and dismiss 8 video ads. And if you have a question you're likely going to an LLM anyway!

I have too many recipes I enjoy generated, I'd love to host them somewhere so I can reference
them. But I also would appreciate it if ones I generate could just be saved in some standardized
format too, so I made this app.

This is a cooking and baking tracking app.

This app can be self hosted completely. It uses Blazor and SQLite

## Features

- **AI recipe generation** — uses Claude Sonnet 4.6 to generate recipes in a structured step format; integrates directly via Anthropic API key or via a prompt generator you can paste into any LLM
- **Multi-format recipe input** — paste recipes as YAML, plain text, or free-form and the app will parse them
- **Structured recipe steps with inline timers** — each step can carry a timer duration so you always know how long to wait
- **Step-by-step cooking mode** — walk through a recipe one step at a time with countdown timers and browser notifications when a timer expires
- **Recipe scaling with fraction display** — scale any recipe up or down; ingredient amounts display as clean fractions
- **600+ ingredient seed database with autocomplete** — start typing an ingredient and get suggestions instantly
- **Flexible units** — any unit string is accepted ("cups", "handful", "splash", etc.) so you're never fighting the form
- **Pantry tracking** — track your ingredients and see at a glance whether you have enough to make a recipe
- **Shopping lists** — generate a shopping list from any recipe
- **Shareable cookbooks** — group recipes into cookbooks and share them with others
- **Multi-user support** — password-optional accounts, designed for self-hosting on a trusted network
- **User authorization hardening** — recipes and cookbooks are protected so users can only edit their own content
- **AI toggle** — if you hate AI being integrated into everything, flip the toggle in your profile and you won't see "AI" anywhere else in the app

If you absolutely hate AI being integrated into everything, there is a toggle in the profile page to disable it.
You won't even see "AI" anywhere else in the app after that.

It has AI chatbot integration if you wish to use an Anthropic API key, but it also has a
prompt generator which you can just put into whichever AI agent you like best so you don't
have to pay extra.

I'd like to implement some more features like smarter food expiration tracking, recipe
substitutions, and export features.

## Recipe Format

FreelovesCookBot stores every recipe in a single canonical format: the `RecipeDocument` C# record (in `CookBot.Domain.Recipes`). YAML, JSON export, and the database column all serialize to the same shape via `JsonRecipeSerializer`; the AI prompt's structured-output schema is generated automatically from the same C# type.

### YAML wire format (v3)

The app reads and writes recipes in a YAML envelope with all v3 fields:

```yaml
---
version: 3
name: Brown Butter Cookies
servings: 24
prepTimeMinutes: 20
cookTimeMinutes: 12
photoUrl: https://example.com/cookies.jpg
description: Crisp-edged, chewy-centered butter cookies.
tags: [baking, dessert]
ingredients:
  - id: 1
    name: butter
    amount: 226
    unit: g
  - id: 2
    name: flour
    amount: 250
    unit: g
steps:
  - kind: section
    heading: Brown the butter
  - kind: content
    text: Melt the [butter](#1) over medium heat until amber and nutty.
  - kind: content
    text: Bake until edges are set.
    temperature:
      value: 375
      unit: F
    timers:
      - duration: 12
        unit: min
        label: bake
---
```

Steps come in two kinds: `section` (a heading with no other fields) and `content` (instruction text with optional timers and an optional per-step temperature). Ingredient references use `[ingredient name](#id)` markdown link syntax to bind step text to the ingredient list by its per-recipe `id`.

### JSON export format

The `.cookbook.json` export and database column use indented JSON via `JsonRecipeSerializer.SerializeIndented`. The shape is identical to the YAML example above. Gas mark temperatures get a special human-readable rendering in the indented form:

```json
{
  "kind": "content",
  "text": "Roast vegetables.",
  "temperature": "4½"
}
```

In the canonical wire format (database column, AI prompt schema, `.cookbook.json` export from `Serialize`), gas half-stops are stored as `{ "value": 4.5, "unit": "gas" }`. Only the human-readable indented JSON (`SerializeIndented`) renders them as `"4½"` for visual ergonomics.

### V1 → V2 → V3 upcaster lineage

Recipes stored by older app versions are upcast on import through a forward-only chain:

- **V1 → V2** (`Migration_V1_To_V2`): renames `prepTime` → `prepTimeMinutes` and `cookTime` → `cookTimeMinutes` (Pitfall C2 — units in field name); renames per-ingredient `localId` → `id` (D-06); replaces the `IsSection: true` boolean discriminator and the `{ section: "X" }` legacy YAML step shape with the `kind: "section"` / `kind: "content"` polymorphic discriminator.
- **V2 → V3** (`Migration_V2_To_V3`): introduces `photoUrl` (string?, max 2048), `description` (string?, max 4096), and per-step `temperature` (`{ value, unit }` where unit is `"F"`, `"C"`, or `"gas"`). All three fields default to null on existing v2 documents — per-field null-coalescing per PITFALLS C7.

### Internally managed format

The recipe format is managed internally; users do not need to author YAML or JSON directly. The recipe editor produces canonical-format documents through the chip composer. The upcaster chain is forward-only — `.cookbook.json` files exported from older app versions upcast on import; downgrade is unsupported.
