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
