# App Icon Brief — THC Meal Planner

Use this document to generate app icons via ChatGPT (DALL-E), Midjourney, Adobe Firefly,
Ideogram, or any other AI image tool. Copy the relevant prompt sections directly into the
generator of your choice.

---

## App Identity

| Field | Value |
|-------|-------|
| App name | THC Meal Planner |
| App type | Family meal planning PWA |
| Audience | Small household — adults and children |
| Tone | Warm, friendly, approachable, organized |
| Platform targets | iOS, Android, Web (PWA), macOS (dock) |

---

## Brand Color Reference

Pull these directly from the app's design system when tinting or accenting the icon:

| Role | Hex |
|------|-----|
| Primary (Soft Blue) | `#60A5FA` |
| Secondary (Warm Yellow) | `#FBBF24` |
| Accent (Gentle Green) | `#A7F3D0` |
| Background (Off-White) | `#FAFAFA` |
| Text / Dark stroke | `#1F2937` |

The icon background can use a gentle gradient from the Soft Blue to the Warm Yellow, or a
solid Soft Blue. Avoid dark/heavy backgrounds — the app is light-themed and family-friendly.

---

## Concept Direction

The icon should communicate **meal planning + family + organization** at a glance. Suggested
visual elements (pick one concept or blend elements):

### Concept A — Calendar + Fork (Minimal / Modern)
A clean calendar grid with a fork or spoon icon overlaid or integrated into one of the date
squares. Works at small sizes; reads clearly on any platform.

### Concept B — Bowl with a Checkmark (Warm / Friendly)
A rounded, friendly bowl of food (think colorful vegetables or a simple meal icon) with a
small checkmark badge in the lower-right corner, implying "planned and done." The bowl
should be cartoon-like, not photorealistic.

### Concept C — House + Fork + Leaf Combination
A simple house silhouette where a fork and a leaf form the "chimney" or interior motif.
Communicates home cooking and health. Works well with the Soft Blue background.

### Concept D — Plate with Calendar Ring
A dinner plate where the outer ring is styled like a calendar (date ticks or soft grid lines).
Center of the plate shows a simple meal illustration (bowl, fork, or food symbols). Clean
and metaphorically direct.

---

## Style Guidelines

- **Style**: Flat or soft-shadow icon (iOS / Material You-compatible). Avoid pure skeuomorphic
  or photorealistic treatments.
- **Shape**: The artwork itself should be designed as a square with ~10–15% rounded-corner
  inset (safe area), so it adapts to both circular (Android) and rounded-rect (iOS/macOS) masks.
- **Complexity**: Simple enough to read at 16×16 px favicon size. No small text in the icon.
- **Color count**: 2–4 colors maximum. Use the brand palette above.
- **No gradients on small details** — gradients are fine for the background only.

---

## Prompt Templates

### ChatGPT / DALL-E
```
App icon for a family meal planning app called "THC Meal Planner". Flat design style,
rounded square format. Concept: [INSERT CONCEPT FROM ABOVE, e.g. "a friendly bowl of
colorful food with a small calendar icon in the corner"]. Color palette: soft blue
(#60A5FA), warm yellow (#FBBF24), pale green (#A7F3D0), with a soft blue or white
background. Clean, minimal, friendly, no text in the icon. Suitable for iOS, Android,
and web PWA use at all icon sizes.
```

### Midjourney
```
flat app icon, family meal planning, [INSERT CONCEPT], soft blue and warm yellow color
palette, pale green accent, rounded square, minimal, friendly, clean vector style,
no text, white or soft blue background, suitable for iOS and Android --ar 1:1 --v 6
--style raw
```

### Ideogram (good if you want "THC" lettering baked in)
```
App icon design. Friendly family meal planning icon. [INSERT CONCEPT]. Soft blue #60A5FA
background, warm yellow and pale green accents. Flat design. Minimal. No clutter. The letters
"THC" may optionally appear in small, clean sans-serif at the bottom of the icon in white.
Square format, rounded corners safe area.
```

---

## Required Export Sizes

Once you have an approved master image (minimum 1024×1024 px), export these sizes:

| File | Size | Used for |
|------|------|----------|
| `icon-512.png` | 512×512 | Android, PWA manifest large |
| `icon-192.png` | 192×192 | Android, PWA manifest small |
| `apple-touch-icon.png` | 180×180 | iOS home screen (no transparency) |
| `favicon-32.png` | 32×32 | Browser tab |
| `favicon-16.png` | 16×16 | Browser tab (small) |
| `favicon.ico` | 16+32+48 multi-size | Legacy browser support |
| `icon-macos-512.png` | 512×512 | macOS dock (can have rounded corners pre-applied) |

> iOS `apple-touch-icon` must have a solid (non-transparent) background — iOS adds its own
> rounded-rect mask.

### Recommended free tool for batch resizing
**Squoosh** (squoosh.app) or **RealFaviconGenerator** (realfavicongenerator.net) can take
your 1024×1024 master and export all the sizes above in one pass. RealFaviconGenerator
also produces the HTML `<link>` tags and the `site.webmanifest` snippet you'll paste into
the Vite project.

---

## Where to Place Files in the Project

```
frontend/
  public/
    apple-touch-icon.png      ← 180×180
    favicon.ico               ← multi-size
    favicon-16x16.png
    favicon-32x32.png
    icon-192.png
    icon-512.png
    site.webmanifest          ← PWA manifest (update name/theme_color to match brand)
  index.html                  ← add <link> tags here
```

### `index.html` link tags to add
```html
<link rel="icon" type="image/x-icon" href="/favicon.ico" />
<link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png" />
<link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png" />
<link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png" />
<link rel="manifest" href="/site.webmanifest" />
<meta name="theme-color" content="#60A5FA" />
```

### `site.webmanifest` snippet
```json
{
  "name": "THC Meal Planner",
  "short_name": "Meal Planner",
  "icons": [
    { "src": "/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icon-512.png", "sizes": "512x512", "type": "image/png" }
  ],
  "theme_color": "#60A5FA",
  "background_color": "#FAFAFA",
  "display": "standalone",
  "start_url": "/"
}
```

---

## Next Steps

1. Pick a concept (A, B, C, or D) or describe a custom direction.
2. Use the matching prompt template above in your generator of choice.
3. Iterate until you have a 1024×1024 master PNG you're happy with.
4. Run it through RealFaviconGenerator to produce all export sizes + the manifest.
5. Drop the files into `frontend/public/` and update `index.html` per the snippets above.
6. Test on iOS (Add to Home Screen), Android Chrome, and desktop browser tab.
