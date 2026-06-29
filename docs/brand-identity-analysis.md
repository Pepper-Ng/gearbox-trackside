# Gearbox Trackside Brand Identity Analysis

This document translates the composite mockup into a reusable styling brief for future template, layout, and UI work.

## Reference Assets

- Composite mockup: `../brand-identity-mockup.png`
- Existing icon asset: `../web/kiosk/public/icons/icon-256.png`
- Extracted dark logo card: `brand-assets/gearbox-trackside-logo-dark-card.png`
- Extracted light logo card: `brand-assets/gearbox-trackside-logo-light-card.png`
- Extracted dark wordmark crop: `brand-assets/gearbox-trackside-logo-dark-wordmark.png`
- Extracted light wordmark crop: `brand-assets/gearbox-trackside-logo-light-wordmark.png`
- Extracted dark wordmark transparent PNG: `brand-assets/gearbox-trackside-logo-dark-wordmark-transparent.png`
- Extracted light wordmark transparent PNG: `brand-assets/gearbox-trackside-logo-light-wordmark-transparent.png`

The extracted wordmarks are best-effort raster crops from the presentation board. Treat them as usable reference assets, not vector masters.

## Brand Essence

The identity sits in the overlap between race control, telemetry engineering, and premium motorsport broadcast graphics. It should feel technical and disciplined rather than playful, flashy, or game-like. The visual language communicates speed through long horizontal strokes, forward slant, segmented accents, and wide-format compositions. The mood is night-race control room: dark, sharp, luminous, data-heavy, and precise.

Key traits:

- Performance-first, not lifestyle-first.
- Motorsport engineering, not generic esports neon.
- Minimal noise, strong hierarchy, large numerics, crisp information density.
- Premium technical finish with subtle metallic highlights rather than glossy chrome.

## Logo System

### 1. Primary Logo

The primary lockup is a stacked composition built from three layers:

1. A tri-color line drawing of car rooflines and body shoulders spans the full width above the wordmark.
2. `GEARBOX` appears beneath the silhouette in small uppercase letters with wide tracking.
3. `TRACKSIDE` dominates the lockup as a large italic extended wordmark.

Important visual details:

- The car silhouette is drawn with thin, clean monoline strokes using rounded ends and smooth directional changes.
- The silhouette is split into three distinct strokes: red on the left, orange across the middle, blue on the right.
- `GEARBOX` is light-weight, uppercase, highly tracked, and visually calm.
- A short orange speed-line sits to the right of `GEARBOX` as a balancing accent.
- `TRACKSIDE` is the hero element: extra wide, italic, angular, and low to the baseline. It reads like a custom motorsport display face with beveled corners and subtle internal cut angles.
- The `TRACKSIDE` fill is not flat white. It reads as a soft silver-white vertical highlight, closer to brushed metallic white than pure paper white.
- The tagline sits below in small uppercase text: `RACE CONTROL`, `TELEMETRY`, `PERFORMANCE`, separated by centered white dots.
- Each tagline word uses a different accent color: red, orange, blue from left to right.

Composition rules for reuse:

- Preserve the long, low silhouette of the mark. It should feel wider than it is tall.
- Keep generous empty space around the logo. It needs breathing room to read as a premium motorsport mark.
- Do not compress the tracking in `GEARBOX` or the tagline.
- Do not substitute the main `TRACKSIDE` wordmark with a generic italic font if the extracted asset is available.

### 2. Dark And Light Logo Variants

The mockup shows the same primary lockup on both a dark graphite card and a light grey card. These should be treated as the canonical inverse pair.

Dark variant:

- The `TRACKSIDE` wordmark uses silver-white highlights against a deep black field.
- Fine diagonal blue streaks and low-contrast panel lines sit in the background, giving a subtle track-lighting feel.
- The card has a faint rounded border and low-glow edges.

Light variant:

- The `TRACKSIDE` wordmark switches to black/dark graphite while the car silhouette keeps the full red, orange, and blue accents.
- The background is not flat white. It is a soft light grey card with gentle vignetting and a faint rounded edge.
- The tagline keeps the same red/orange/blue semantic order.

For headers, overlays, dashboard mastheads, and narrow spaces, crop the primary lockup tightly rather than inventing a new horizontal brand arrangement.

### 3. Icon-Only Mark

The icon uses a stylized metallic white `G` with three short motion bars extending from its right edge in red, orange, and blue. The mark sits inside an open rounded-square frame made from segmented neon strokes.

Icon details:

- The `G` is thick, beveled, and slightly forward-leaning, matching the aggressive geometry of `TRACKSIDE`.
- The three horizontal bars reinforce the idea of telemetry data, speed strips, or sector progression.
- The frame is not a closed box. It is an interrupted rounded square with separate red, orange, and blue corner strokes.
- The icon looks best on black or deep graphite surfaces.

Use the existing raster icon asset when possible rather than recreating it from scratch.

## Color System

The palette is compact, high-contrast, and semantically ordered from left to right.

| Role | Hex | Meaning | Typical Use |
| --- | --- | --- | --- |
| Neon Red | `#FF202D` | Race control, urgency, left-most energy | Alerts, lead accent, first sector, first tagline word |
| Neon Orange | `#FF8A00` | Telemetry, heat, live data | Secondary accent, middle sector, RPM midrange, second tagline word |
| Neon Blue | `#00B0FF` | Performance, analysis, speed | Cool accent, right-most energy, charts, performance label |
| Dark Graphite | `#0D0F12` | Primary background | App background, deep panels, dark brand field |
| Deep Charcoal | `#14161A` | Secondary surface | Cards, inner panels, tile backgrounds |
| Light Grey | `#E6E6E6` | Light-theme surface and neutral contrast | Light theme cards, inverse branding support |

Additional functional colors visible in the mockup:

- White and silver-white are used for key marks, numerics, and top-priority information.
- Green appears in telemetry/tyre status as a health or normal-state color, not as a core brand color.
- Magenta/purple appears in the spectator overlay for fastest-lap emphasis; treat it as a special-state highlight, not a primary brand accent.

Color behavior matters as much as the values:

- Red, orange, and blue should usually appear in that order from left to right.
- The three accent colors work best as segmented strokes, bars, and labels rather than soft blended gradients.
- The dark neutrals should dominate most screens. Accent colors are there to direct attention, not to flood the interface.

Recommended CSS-style tokens:

```css
:root {
  --gt-red: #FF202D;
  --gt-orange: #FF8A00;
  --gt-blue: #00B0FF;
  --gt-graphite: #0D0F12;
  --gt-charcoal: #14161A;
  --gt-light: #E6E6E6;
  --gt-white: #FFFFFF;
}
```

## Typography

The mockup explicitly recommends two type families:

- `Rajdhani` for headlines and numbers.
- `Exo 2` for body text and UI text.

### Rajdhani Usage

Use Rajdhani for:

- Major KPI numerals.
- Overlay section titles.
- Tile headings when you want a condensed motorsport tone.
- Tabular data that benefits from a technical, squared-off silhouette.

Preferred treatment:

- Uppercase for labels and titles.
- SemiBold to Bold for most hero data.
- Tight vertical rhythm, but generous horizontal space.
- Keep numerics large and luminous.

### Exo 2 Usage

Use Exo 2 for:

- Supporting labels.
- Body copy.
- Secondary metadata.
- Button text, form labels, small UI captions.

Preferred treatment:

- Medium or SemiBold for labels.
- Uppercase with tracking for compact UI microcopy.
- Sentence case for longer explanatory text.

### Wordmark Guidance

The `TRACKSIDE` wordmark appears custom or heavily modified. It should not be recreated with Rajdhani or Exo 2 if the extracted asset is available. If a later agent must approximate the wordmark in code or vector form, the substitute should be:

- Very wide.
- Italic or forward-leaning.
- Squared rather than rounded.
- Slightly beveled or cut at key corners.
- Visually aerodynamic rather than industrial blocky.

Working UI scale guidance:

- Tiny labels: 11 to 13 px, uppercase, 0.10em to 0.18em tracking.
- Standard UI labels: 13 to 16 px.
- Panel headings: 18 to 24 px Rajdhani.
- Large numerics: 44 to 72 px Rajdhani, depending on density.
- Hero data callouts: 80 px and above where the layout allows it.

## Layout And Composition Language

The mockup consistently uses a wide, broadcast-oriented grid. Everything leans toward horizontal motion.

Core layout rules:

- Favor panoramic compositions over centered poster layouts.
- Build screens from modular rectangular panels with subtle rounding.
- Use thin separators, low-contrast borders, and dark surface-on-surface contrast.
- Give data modules strong alignment and consistent spacing.
- Let major information blocks feel docked and engineered, not floating and casual.

Spatial character:

- Corner radii should be modest, usually around 12 to 18 px on major cards.
- Borders should be faint, around a 1 px low-opacity neutral stroke.
- Shadows should be soft but restrained.
- Glows should be small and intentional, mostly tied to accent color edges or important states.

Shape language:

- Horizontal bars, segmented strips, angled cuts, and tapered lines all fit the system.
- Rounded-pill consumer UI styling does not fit.
- Soft organic blobs do not fit.

## Surface Styling

### Dark Theme

The dark system is the default expression of the brand.

- Backgrounds sit between `#0D0F12` and `#14161A`.
- Panels are darker than the content they contain, but not pitch black unless they need logo contrast.
- Borders are subtle and cool-neutral.
- Decorative light streaks or faint diagonal traces can be used sparingly to imply speed and track lighting.
- Most content should stay crisp and readable rather than heavily stylized.

### Light Theme

The light theme version preserves the same geometry and spacing but flips the field to a soft off-white/light-grey card.

- Use `#E6E6E6` or a slightly warmer near-white as the base.
- Keep shadows soft and premium, like a polished hardware product card.
- Preserve the neon accent colors at full saturation so they still cut through the lighter field.
- Avoid pure clinical white with zero depth. The mockup uses subtle surface softness and halo lighting.

## UI Pattern Guidance From The Mockup

### Spectator Overlay

The spectator overlay preview establishes a strong template direction:

- Left rail leaderboard panel with stacked rows and tight spacing.
- Main race footage occupies the central wide frame.
- Small horizontal sector indicators sit above the action.
- A compact logo lockup lives at the top of the frame.
- Bottom ribbon contains fastest lap, lap count, ambient conditions, and branding.
- A small track map sits in the lower-right corner of the footage frame.

Styling rules for this pattern:

- Use translucent near-black overlays over imagery.
- Let white text dominate, with selective accent color for special states.
- Use magenta only for exceptional highlight moments like fastest lap.
- Keep all chrome tight, straight, and low-profile.

### Telemetry Dashboard

The telemetry dashboard preview points to a modular data cockpit aesthetic:

- Big numeric tiles for speed, gear, lap, and position.
- Mid-sized utility cards for throttle, brake, fuel, and temperatures.
- A dense line chart spanning the bottom for ongoing telemetry traces.
- An RPM strip rendered as a segmented horizontal band.
- A tyre status module with a simplified car outline and per-corner readouts.

Styling rules for this pattern:

- Large numerics should be white and dominant.
- Labels should be smaller, lighter, and highly legible.
- Use accent colors semantically, not decoratively.
- Keep charts thin-line and information-rich rather than soft or smoothed into marketing graphics.
- Green can be used for healthy statuses inside telemetry widgets, even though it is not a core brand color.

## Motion And Interaction Direction

If another agent turns this identity into an interactive UI, the motion should behave like a live motorsport system:

- Prefer horizontal wipes, sector-bar fills, and fast opacity transitions.
- Use short, controlled durations.
- Avoid springy, elastic, or playful motion.
- Accent reveals should feel like data coming online, not like consumer-app flourish.
- Hover states should sharpen contrast or add a restrained color edge, not inflate components.

## What To Preserve

- Black or deep graphite as the dominant canvas.
- The red -> orange -> blue accent sequence.
- Wide horizontal compositions.
- Condensed technical typography for numerics and headings.
- Bright white/silver core data against very dark surfaces.
- Sharp, controlled surfaces with subtle radii and low-noise chrome.
- The sense that every panel belongs in a pit wall, control room, or telemetry console.

## What To Avoid

- Generic purple-heavy neon cyberpunk styling.
- Rounded, friendly SaaS cards with soft giant shadows.
- Pastel gradients or desaturated muted accents.
- Centered lifestyle-marketing layouts.
- Cartoonish iconography.
- Overly glassy UI with blur as the primary visual effect.
- Replacing the accent order with random color placement.

## Implementation Shortcut For Future Agents

If a later agent needs to build templates in this style quickly, it should default to the following recipe:

1. Use `Rajdhani` for big numerics and section headings.
2. Use `Exo 2` for labels and supporting interface text.
3. Set the page or app background to `#0D0F12`.
4. Build modules on `#14161A` panels with a 1 px faint border and 12 to 18 px radius.
5. Use white for primary values and light grey for secondary text.
6. Reserve red, orange, and blue for semantic accents in that order.
7. Keep layouts wide, grid-based, and left-to-right.
8. Add only restrained glow, streak, or telemetry-line decoration.

## Asset Notes

- The icon already exists in the repo as a clean PNG and should be reused directly.
- The dark and light logo cards, plus tighter wordmark crops, were extracted from the mockup into `docs/brand-assets/` for reuse in documentation, prototypes, or manual cleanup.
- If production-quality branding is needed later, the next step should be to redraw the wordmarks as vector assets from these references.
