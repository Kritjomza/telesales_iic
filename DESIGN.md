---
name: Telesale IIC Operations Console
description: Premium, modern, technology-focused enterprise UI for IIC telesales operations.
colors:
  navy: "#071a34"
  blue: "#005bbb"
  blue-hover: "#004a99"
  blue-soft: "#eaf3ff"
  ink: "#0b1220"
  text: "#111827"
  muted: "#526173"
  muted-strong: "#334155"
  app-bg: "#f4f7fb"
  surface: "#ffffff"
  surface-alt: "#f8fafc"
  border: "#d8e2ee"
  success: "#15803d"
  success-bg: "#ecfdf3"
  success-border: "#bbf7d0"
  warning: "#a16207"
  warning-bg: "#fffbeb"
  warning-border: "#fde68a"
  danger: "#b91c1c"
  danger-bg: "#fef2f2"
  danger-border: "#fecaca"
  info: "#0369a1"
  info-bg: "#eff6ff"
  info-border: "#bfdbfe"
typography:
  family: "Inter, ui-sans-serif, system-ui, sans-serif"
  body-size: "13px"
  body-line-height: "1.5"
radius:
  sm: "4px"
  md: "6px"
  lg: "8px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
  xxl: "32px"
motion:
  micro: "150ms ease"
  overlay: "220ms ease-out"
---

# Design System: Telesale IIC Operations Console

## 1. North Star

**Creative North Star: "Secure Operations Console"**

The interface should feel like a trusted technology platform for internal telesales operators: sharp, premium, clean, and fast. It uses IIC blue as the decisive action color, navy/black for authority, white surfaces for clarity, and cool neutral backgrounds for long-session comfort.

The physical usage scene: telesales users work on laptops and desktop monitors under office lighting, scanning dense customer records for hours while switching between search, filters, imports, edits, and advance customer data. The UI must prioritize speed, contrast, and confidence over decoration.

## 2. Color System

### Brand And Neutral

- **IIC Navy `#071a34`**: Shell, login brand block, dark headers, strong text moments.
- **IIC Blue `#005bbb`**: Primary actions, active pagination, focus states, selected controls.
- **IIC Black `#0b1220`**: Primary headings and high-emphasis copy.
- **App Background `#f4f7fb`**: Cool operational workspace background.
- **Surface `#ffffff`**: Cards, panels, tables, drawers, modals.
- **Surface Alt `#f8fafc`**: Table headers, filter bands, subtle grouped form sections.
- **Border `#d8e2ee`**: Default divisions and input borders.

### Semantic Status

- Success: `#15803d`, `#ecfdf3`, `#bbf7d0`
- Warning: `#a16207`, `#fffbeb`, `#fde68a`
- Danger: `#b91c1c`, `#fef2f2`, `#fecaca`
- Info: `#0369a1`, `#eff6ff`, `#bfdbfe`

### Rules

- Use a restrained palette. Blue is for actions, focus, and active state, not decorative fill.
- Large surfaces stay white or cool neutral. Navy may appear as a shell/sidebar/login brand surface.
- Body and table text must remain high contrast. Muted text uses `#526173` or darker, never pale gray.
- Semantic colors belong to badges, small alerts, and state indicators. They should not dominate the screen.

## 3. Typography

- Font family: `Inter, ui-sans-serif, system-ui, sans-serif`.
- Body: 13px / 1.5 for dense enterprise UI.
- Page title: 20px / 1.25, 700.
- Section title: 15px / 1.3, 700.
- Table/body data: 13px / 1.45.
- Labels and column headers: 11-12px, 700, modest uppercase only where it improves scanning.
- Avoid fluid type and oversized display text in product surfaces.

## 4. Shape, Elevation, And Motion

- Cards, panels, drawers, and modals use 8px radius maximum.
- Inputs and buttons use 6px radius.
- Icon buttons and compact actions use 4px radius.
- Default containment is a crisp 1px border.
- Shadows are subtle and reserved for overlay depth or light panel separation:
  - Panel: `0 1px 2px rgba(15, 23, 42, 0.06)`
  - Overlay: `0 18px 36px rgba(7, 26, 52, 0.18)`
- Motion is limited to 150-250ms hover/focus/overlay feedback.
- Respect `prefers-reduced-motion`; no page-load choreography.

## 5. Login Page

The login page should read as secure and technical without being flashy.

- Layout: centered responsive auth shell with a two-column composition on desktop and single-column on mobile.
- Brand panel: navy/black surface with IIC mark, platform name, and restrained trust cues.
- Form panel: white card with crisp border, compact spacing, strong field labels, clear password visibility affordance.
- Background: cool neutral with subtle grid/technical texture generated in CSS, not heavy imagery or animation.
- Button: full-width IIC blue primary action, clear loading state, disabled state.
- Validation: compact danger alert with icon and high-contrast copy.
- Mobile: panels stack; brand details collapse into a concise header; no horizontal scroll.

## 6. Customer Manage Page

The page is a high-density scanning workspace for telesales and sales users. It must feel like a premium technology console, but the hierarchy stays operational: page purpose, primary actions, search/filter controls, customer table, pagination.

### Header

- Topbar uses white surface, bottom border, compact spacing, and a clear breadcrumb/title pair.
- Header copy should explain the job of the page in one concise sentence: managing customer records, completeness, renewals, and advance data.
- Actions align right on desktop and wrap to full-width buttons on mobile.
- Primary action is Add Customer; Import is secondary.
- Header buttons use the shared 38px button vocabulary; no oversized hero actions.

### Metrics

- Four compact metric cards with small icon tile, label, and value.
- Cards are equal height with stable dimensions.
- Metric cards may include one short context line when it improves scanning, but must not become promotional cards.
- Metric values use tabular numbers and strong contrast.
- Hover lift is 1px maximum; no wide shadows or decorative glow.

### Search And Filters

- Filter panel remains a single bordered surface.
- Search input spans available width where possible; selects and actions align to a clean grid.
- Completeness controls use segmented buttons; missing-field filter uses a standard select.
- Active states are clear with white selected segment and navy text.
- Active applied filters should be visible as compact chips or a concise status row inside the filter panel.
- Search, Clear, completeness, and missing-field controls must preserve existing filter behavior and pagination reset behavior.

### Tables

- Fixed-layout tables preserve scanning and prevent layout shift.
- Sticky headers may be used when table containers scroll vertically.
- Header background is surface-alt with strong column labels.
- Customer name is the primary cell; address and match metadata are secondary.
- Row hover uses a subtle blue tint.
- ID, date, status, completeness, and actions align consistently. Actions remain visually quieter than customer identity.
- Empty and loading states live inside the table body with icon/structured copy or skeleton-like rows, not standalone spinners.
- Horizontal scroll is acceptable on tablet/mobile; do not compress data into unreadable columns.
- Long Thai company names and addresses wrap inside the primary cell without escaping the table scroller.

### Actions

- Row actions stay compact and icon-led.
- "Advance" remains a small filled/tinted action pill.
- Edit and delete are icon buttons with accessible labels.
- Dangerous actions use red only on hover or explicit danger buttons.
- Action groups should have stable dimensions so rows do not jump when Advance is or is not available.

### Badges

- Status badges use semantic tint, 1px border, 20px height, 4px radius.
- Completeness badge uses success/warning vocabulary and can reveal missing fields through the existing popover.
- Badges must keep text readable at 11px.
- Missing-field popovers use the overlay shadow and sit above table content without clipping.

### Drawers, Modals, And Forms

- Drawers slide from the right, max 520px desktop, full width on mobile.
- Drawer and modal headers use crisp typography and border separation.
- Forms keep 13px inputs, 38px height where possible, and two-column rows only when there is enough width.
- Fieldsets use surface-alt and 6px radius; avoid nested card styling.
- Focus rings use IIC blue with a soft 3px ring.
- Form footers use right-aligned Cancel/Save actions on desktop and full-width stacked actions on mobile.
- Preserve every existing field and validation behavior.

### Pagination

- Pagination belongs to the table panel footer.
- Desktop: record count left, page controls right.
- Mobile: stack controls, allow page buttons to wrap, keep tap targets at least 32px.

### Floating AI Assistant

- The AI assistant is available only as a compact Manage-page overlay.
- Closed state: a fixed right-edge AI tab, sized like a premium attached pill, with a deep blue/navy surface, white text, and a restrained blue glow on hover/focus.
- Open state: a floating panel slides in from the right, 360-440px wide on desktop, with translucent white surface, subtle border, controlled backdrop blur, and overlay shadow.
- The closed tab must not consume table, filter, pagination, import, edit, or advance layout space.
- The open panel sits above the page content but below modals and toasts; modals remain the highest operational overlay.
- Clicking the tab again, the close button, Escape, or the soft backdrop closes the panel. Focus is not trapped because the panel is a non-modal assistant.
- Motion uses transform and opacity only, 150-250ms, and collapses to near-instant behavior for `prefers-reduced-motion`.
- Mobile: tab becomes smaller and sits above the bottom edge; panel becomes near full-width and uses safe viewport insets.
- The assistant must not cover table actions, filter actions, pagination, drawers, modals, or import dialogs.

## 7. Reports Workspace

Reports are executive-operational surfaces: they should feel more analytical and premium than CRUD pages while remaining dense, calm, and trustworthy.

### Information Architecture

- Start with a report header that states the reporting purpose in plain language.
- Do not show global summary KPI cards above the report tabs unless explicitly requested. Prioritize tab selection and the active report content first.
- Keep tabs compact and descriptive. Each tab must map to one analytical job: operation audit, performance, renewal risk, or project/license ledger.
- Within a report tab, use this order: section summary, filter/action row when available, chart or distribution, then detailed table.
- Do not invent data, business calculations, export behavior, pagination, sorting, or API contracts for visual completeness.

### KPI Cards

- KPI cards use white surfaces, 1px cool borders, 8px radius, compact padding, and tabular numeric values.
- Each KPI shows a short label, a strong value, and concise context. Use trend/delta only when it already exists in data.
- Icons are optional and small. Avoid decorative oversized icons; prefer text hierarchy and subtle metric rails.
- Hover feedback is a 1px lift and slightly stronger border only.

### Filter And Action Bars

- Report filters live in a single compact toolbar with segmented controls for small mutually exclusive ranges such as 30/60/90 days.
- Active filters use white or blue-soft surfaces with navy/blue text and a clear border, not full-saturation fills unless the action is primary.
- Export/download actions, where existing logic is present, should align to the right and use secondary button treatment.

### Charts

- Charts use a restrained report palette: navy `#071a34`, IIC blue `#005bbb`, info `#0369a1`, success `#15803d`, warning `#a16207`, danger `#b91c1c`, and neutral grid `#d8e2ee`.
- Prefer simple bar, distribution, or progress visuals that can be read without decoration.
- Labels, legends, and tooltips must be explicit. Empty chart areas show a structured empty state.
- Chart styling must never change chart data source, grouping rules, or calculations.

### Tables

- Report tables are optimized for scanning: sticky headers when useful, fixed table layout, strong header labels, tabular numeric columns, and subtle row hover.
- Customer/entity cells are primary; metadata is secondary and muted but still contrast-safe.
- Numeric values and dates align consistently. Status badges use the shared semantic badge system.
- Mobile and tablet layouts keep all columns available through horizontal scroll rather than hiding report data.

### States

- Loading states use skeleton rows or compact structured panels, not isolated spinners.
- Empty states explain the exact report condition, for example no renewals inside the selected range.
- Error states provide a retry action when the existing load action can be safely re-run.
- Forbidden access continues to use the existing permission fallback.

### Responsive Rules

- Desktop: two-column report analytics can be used above tables; tables remain full-width and horizontally scrollable.
- Tablet: KPI cards wrap to two columns; report chart/summary panels stack.
- Mobile: KPI cards stack, tabs scroll horizontally, filter bars wrap, and report tables keep a minimum width inside an overflow container.
- Long Thai customer names and addresses must wrap safely inside primary cells without forcing viewport overflow outside the table scroller.

## 8. Master Data Workspace

Master Data screens are reference-data operations surfaces. They should feel precise, compact, and systematic, with clear navigation between datasets and calm table/form treatment.

### Menu And Navigation

- The Master Data sidebar group uses compact child rows with clear active and hover states.
- Related items stay in the existing route structure and order unless a future product decision changes navigation.
- Active Master Data items must be obvious through a stronger blue/navy state, not decorative icons or large menu cards.
- The sidebar group should remain usable with many items: tight spacing, no oversized blocks, and no hidden actions.

### Page Layout

- Each Master Data page starts with a clear header: breadcrumb, title, concise description, and right-aligned actions.
- Header actions use Add Record as the primary action. Import is secondary and only appears where import already exists.
- Below the header, use a compact summary strip only for operational context already available in state, such as total loaded rows and visible rows.
- Do not change CRUD, search, import, template, validation, permissions, API calls, or data mapping for visual completeness.

### Search And Controls

- Search lives in a bordered panel directly above the table.
- Search inputs use the shared 38px control height, IIC blue focus ring, and high-contrast placeholder text.
- Active search state is communicated with concise helper text or count text, not a large card.

### Tables And Lists

- Master Data tables use fixed layout, sticky headers when scrolling, tabular numeric columns, and subtle row hover.
- Primary entity names are strong; codes, IDs, amounts, and token-like values use compact technical styling.
- Row actions are grouped right, icon-led, and quiet until hover/focus.
- Empty and loading states use structured table states with icon/copy or skeleton rows, never lone centered text.
- Long Thai and English labels wrap within cells without breaking the page outside the table scroller.

### Forms, Drawers, And Import Modals

- Create/edit drawers use crisp headers, 13px labels, 38px inputs, two-column rows only when there is enough room, and right-aligned Cancel/Save actions.
- Import/template controls keep the existing workflow but should visually read as system utilities: compact, bordered, and readable.
- Delete remains a native confirmation unless the existing flow is intentionally replaced later; destructive row actions stay visually separated through danger hover states.

### Responsive And Motion

- Desktop: table-first layout with compact actions and wide horizontal scroll only when data requires it.
- Tablet: header actions, search, and table controls wrap cleanly.
- Mobile: tables remain horizontally scrollable with stable minimum widths; drawers and modals use full available width.
- Motion is limited to 150-250ms color, opacity, and transform transitions and respects `prefers-reduced-motion`.

## 9. Responsive Rules

- Desktop >= 1025px: sidebar plus content, four metrics, filter grid, full table.
- Tablet 641-1024px: content becomes single shell column, two metrics per row, filters stack logically, table scrolls horizontally.
- Mobile <= 640px: header actions full width, one metric per row, filter/action controls full width, drawer/modal full viewport width, pagination stacked.
- Avoid viewport-scaled fonts. Use structural wrapping and scroll containers instead.

## 10. Implementation Rules

- Do not change authentication logic, API calls, business filters, pagination, imports, edit flows, or database contracts.
- Prefer shared CSS classes and tokens over inline styling.
- Preserve existing components where possible.
- Keep backend, schema, migrations, and unrelated pages untouched.
- Every interactive element needs hover, focus-visible, disabled, and loading/error treatment where relevant.
- No glassmorphism, gradient text, heavy animation, GIF-driven UI, side-stripe cards, oversized radii, or decorative shadow stacks.
