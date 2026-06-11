---
name: Telesale IIC Dashboard
description: Modernized enterprise telesales administration system.
colors:
  primary: "#0f172a"
  primary-light: "#1e293b"
  accent: "#475569"
  accent-light: "#f1f5f9"
  surface: "#ffffff"
  surface-alt: "#f8fafc"
  border: "#e2e8f0"
  text-muted: "#64748b"
  text-light: "#94a3b8"
  success: "#15803d"
  success-light: "#f0fdf4"
  warning: "#b45309"
  warning-light: "#fffbeb"
  danger: "#b91c1c"
  danger-light: "#fef2f2"
  info: "#0369a1"
  info-light: "#f0f9ff"
typography:
  body:
    fontFamily: "Inter, ui-sans-serif, system-ui, sans-serif"
    fontSize: "13px"
    lineHeight: "1.5"
rounded:
  sm: "4px"
  md: "6px"
  lg: "8px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "#ffffff"
    rounded: "{rounded.md}"
    padding: "0px 16px"
    height: "38px"
  button-primary-hover:
    backgroundColor: "{colors.primary-light}"
  button-secondary:
    backgroundColor: "#ffffff"
    textColor: "{colors.primary}"
    rounded: "{rounded.md}"
    padding: "0px 16px"
    height: "38px"
  button-secondary-hover:
    backgroundColor: "{colors.surface-alt}"
  input-field:
    backgroundColor: "#ffffff"
    rounded: "{rounded.md}"
    padding: "8px 12px"
---

# Design System: Telesale IIC Dashboard

## 1. Overview

**Creative North Star: "The Operations Console"**

A high-productivity, keyboard-friendly console for internal telesales agents, supervisors, and administrators. The visual environment is designed for long hours of reading and entering structured data. It rejects the visual clutter of modern consumer SaaS (excessive whitespace, glassmorphism, animated transitions, rounded cards) in favor of high contrast, dense grid layouts, crisp borders, and semantic color codes.

**Key Characteristics:**
- **High Data Density:** Small spacing, compact tables, and nested forms that maximize readable data on standard screen resolutions.
- **Strict Visual Restraint:** Backgrounds are flat neutrals (white or slate-tinted off-white); borders are solid, light, and sharp; elevation is used sparingly.
- **Immediate Status Comprehension:** Colored badges map directly to call/lead status values, ensuring immediate scannability of large lead lists.

## 2. Colors

The color palette is built on high-contrast slate neutrals with focused corporate primary tones and clear semantic accents.

### Primary
- **Deep Navy Slate** (#0f172a): Main brand/ink color. Used for headings, primary text, and primary button backgrounds.
- **Slate Gray** (#1e293b): Muted primary color. Used for active navigation item text and primary hover states.

### Accent
- **Steel Blue Accent** (#475569): Secondary highlight color. Used for inactive controls, secondary button text, and panel headers.
- **Steel Ice Light** (#f1f5f9): Light accent tint. Used for sidebar item backgrounds, table header background hover, and secondary card fills.

### Neutral
- **Solid White** (#ffffff): Clean surface color. Used for table backgrounds, active panel content, and primary buttons.
- **Cool Off-White** (#f8fafc): Screen background and alt-row color. Used to ease eye strain.
- **Border Slate** (#e2e8f0): Thin division color. Used for gridlines, form outlines, and dividers.
- **Text Muted** (#64748b): High-contrast subtext. Used for column headers and auxiliary descriptions.
- **Text Light** (#94a3b8): Placeholder color. Used for inputs and empty states.

### Semantic
- **Success Green** (#15803d) / **Success Tint** (#f0fdf4): Approved, won, or completed statuses.
- **Warning Amber** (#b45309) / **Warning Tint** (#fffbeb): Waiting or pending actions.
- **Danger Red** (#b91c1c) / **Danger Tint** (#fef2f2): Lost, rejected, or critical issues.
- **Info Blue** (#0369a1) / **Info Tint** (#f0f9ff): Assigned or details status.

### Named Rules
**The 10% Accent Rule.** Accent and semantic colors are never used as decorative fills on large surfaces. They must be confined to small indicators (badges, pills, borders) and occupy less than 10% of any screen surface to keep status signals meaningful.

## 3. Typography

**Display Font:** Inter, ui-sans-serif, system-ui, sans-serif
**Body Font:** Inter, ui-sans-serif, system-ui, sans-serif
**Label/Mono Font:** ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace

**Character:** A single, clean sans-serif typeface family (Inter) is used for all text to optimize readability at small font sizes on low-dpi screens.

### Hierarchy
- **Display / H1** (Bold, 20px, 1.2): Main page headers and metric numbers.
- **Headline / H2** (Bold, 15px, 1.3): Panel titles and form section headers.
- **Title** (Semi-bold, 13px, 1.4): Navigation parent items and strong table data.
- **Body** (Regular, 13px, 1.5): Standard copy, inputs, table rows. Line length is capped at (75ch) for paragraphs.
- **Label / Subtext** (Regular or Semi-bold, 11px, 1.2): Column headers, table metadata, badge text, and form descriptions. Usually uppercase with (0.5px) letter spacing.

### Named Rules
**The No-Underline Rule.** Text links and actionable items are never underlined at rest. They must indicate interactiveness via hover background fills and color changes.

## 4. Elevation

The layout is flat by default to maximize vertical pixel efficiency. Depth is indicated by border nesting and tonal backgrounds rather than physical drop shadows.

### Shadow Vocabulary
- **Flat Border** (none): Standard containment for widgets, tables, and buttons.
- **Ambient Low** (`0 1px 3px 0 rgba(0,0,0,0.05)`): Standard panel elevation.
- **Active Overlay** (`0 10px 15px -3px rgba(0,0,0,0.04)`): Applied only to overlays like dropdowns, drawers, and modal popups.

### Named Rules
**The Flat-Rest Rule.** Buttons, cards, and inputs are flat at rest. Physical shadows are reserved for floating overlays (drawers, modals) that escape the current grid context.

## 5. Components

### Buttons
- **Shape:** Rounded-md (6px border radius)
- **Primary:** Deep Navy (#0f172a) background with white text. Height (38px).
- **Secondary:** White (#ffffff) background with Border Slate (#e2e8f0) border. Height (38px).
- **Hover:** Primary shifts to Slate Gray (#1e293b); Secondary shifts to Cool Off-White (#f8fafc).
- **Danger Action:** Light Red (#fef2f2) background with Red (#b91c1c) text. Height (26px), padding (0px 8px).

### Chips / Status Badges
- **Style:** Light semantic backgrounds with thin border in the same family. Height (20px), padding (0px 8px).
- **Status Mapping:** `new`/`pending` = gray; `assigned`/`booking` = light slate; `wait` = amber; `approved`/`win`/`sent` = green; `lost`/`rejected` = red.

### Cards / Containers
- **Corner Style:** Rounded-lg (8px border radius)
- **Background:** Solid White (#ffffff) with Slate Border (#e2e8f0).
- **Shadow Strategy:** Ambient Low (`var(--shadow)`) at rest. No shadows on nested cards.
- **Padding:** Compact (18px) for metrics; standard (16px to 24px) for dashboard sections.

### Inputs / Fields
- **Style:** White (#ffffff) background with solid border (#e2e8f0) and 6px border radius. Height (38px).
- **Focus:** Primary Slate (#0f172a) border with a subtle slate glow shadow (`0 0 0 2px rgba(15, 23, 42, 0.08)`).

### Navigation
- **Style:** Sidebar shell width (260px) with thin slate right border (#e2e8f0).
- **Items:** Hover state triggers Steel Ice (#f1f5f9) background. Active children are highlighted in border gray (#e2e8f0) with bold text.

## 6. Do's and Don'ts

### Do:
- **Do** align all text fields and inputs to the vertical grid.
- **Do** use uppercase and spaced labels for table column headers to emphasize data structures.
- **Do** ensure status badges have at least a 1px border colored slightly darker than their background to prevent them from washing out.
- **Do** wrap tables in overflow containers (`.table-wrap`) to handle horizontal scrolling gracefully.

### Don't:
- **Don't** use generic SaaS gradients, text gradients, or neon visual accents.
- **Don't** use glassmorphism, background blurs, or soft drop shadows on inline cards.
- **Don't** use rounded corners larger than 8px on cards or panels.
- **Don't** use nested cards inside main page panels; use flat divider lines or background bands to section content.
- **Don't** use side-stripe borders (e.g. `border-left: 4px solid var(--primary)`) on tables or callout boxes.
- **Don't** exceed 12px of padding on table cells (`td`, `th`) to preserve compact data density.
