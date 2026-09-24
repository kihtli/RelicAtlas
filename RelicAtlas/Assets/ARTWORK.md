# Relic Atlas visual assets — 0.1.0.13

`AetherArchive.png` is original background artwork generated with the built-in
imagegen tool. The unmodified PNG is embedded in the plugin assembly. ImGui crops
it through texture coordinates when drawing; it requires no runtime download.

`RelicAtlasLogo.png` is a separate original transparent logo generated with the
built-in imagegen tool. Its exact prompt is in `LOGO-PROMPT.md`. Both images are
embedded in the assembly and loaded through the shared texture provider. The
logo's transparent outer margins are omitted using UV coordinates at draw time.

Game job and weapon icons come from Dalamud's game texture provider using
installed game data; those icons are not bundled with this package.

## Exact generation prompt

Use case: stylized-concept. Asset type: original atmospheric background illustration for a real FFXIV relic-tracker plugin header, not a UI mockup. Create a luxurious futuristic cyberpunk city/data-vault scene inspired by the purple and cyan luminous architecture of FFXIV Solution Nine. A suspended faceted violet aether crystal inside an elegant architectural halo on the RIGHT THIRD, intricate sleek dark metallic cathedral-like data structures and softly glowing cyan conduits receding into purple mist behind it. Sophisticated AAA game key-art finish, strong dimensional lighting, rich deep aubergine and midnight indigo, luminous lavender edges and precise cyan highlights, a few tiny drifting light particles. Wide panoramic composition, approximately 3:1 landscape. The LEFT HALF must remain very dark, soft and largely empty for readable overlaid headings. The right subject occupies the middle vertical band, with comfortable margins, suitable for cropping to a shallow hero header. Restrained and beautiful, no rainbow colors, no yellow. No text, no letters, no logos, no UI controls, no frames, no watermark. This is a project asset that will be integrated into the plugin.

## Exact logo generation prompt

Use case: logo-brand. Asset type: finished transparent PNG logo lockup to embed in a premium desktop game plugin, replacing a crude vector logo. Brand name, exact text: "RELIC ATLAS". Create a genuinely sophisticated, professionally art-directed logo for a Final Fantasy XIV relic weapon tracker with a futuristic Solution Nine / elegant cyberpunk identity. Wide horizontal composition, approximately 3:1. On the left, an original compact emblem combining a relic blade and a prismatic compass or astrolabe into one memorable, strong silhouette. Abstract and refined, two or three broad crystalline facets, no literal letter R or A monogram, no shield badge, no bookmark, no corporate ribbon, no generic hexagon. Rich amethyst/violet with a precise luminous cyan edge and a small pale highlight; dimensional material polish without excessive detail or bloom. On the right, the words RELIC ATLAS in two balanced stacked lines: bespoke geometric science-fiction lettering, carefully kerned, pale silver-white, calm and authoritative, highly readable. The emblem and wordmark must feel like one designed identity. Optimize the result to remain excellent when displayed only 185 pixels wide by 60 pixels high: bold forms, no tiny fine detail, no decorative microtext. The complete logo must be on a genuinely transparent background with alpha, no background plate, no scenery, no mockup, no surrounding border, no extra text, no watermark. Keep comfortable but modest transparent margins. Render a polished final production asset, not a sketch or concept sheet.
