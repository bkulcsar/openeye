# OpenEye Dashboard UI Refactor Plan

## Date: 2026-03-12

## Current State Assessment

The dashboard is functional but raw — plain Tailwind utility classes, no component library, emoji icons, no dark mode, no loading/error states, and several Web Interface Guidelines violations. The goal is a clean, professional monitoring dashboard using **shadcn/ui** components.

---

## Web Interface Guidelines Review

### Global Issues

- `layout.tsx:13` — no `<meta name="theme-color">` matching page background
- `layout.tsx` — no skip link for main content (`<a href="#main">Skip to content</a>`)
- `layout.tsx` — no `color-scheme` set on `<html>` for dark mode
- `globals.css` — only `@import "tailwindcss"` — no base styles, no CSS variables for theming
- No dark mode support anywhere
- No `prefers-reduced-motion` handling on any transitions
- No `touch-action: manipulation` on interactive elements
- No `font-variant-numeric: tabular-nums` for number columns
- No font loading optimization (`font-display: swap`, preload)
- No toast/notification system for async feedback (`aria-live`)
- No error boundaries or error states
- No loading skeletons — pages show nothing while fetching
- Emoji icons instead of proper SVG icon system (no `aria-hidden` for decorative)
- No proper icon-button `aria-label` anywhere

### Sidebar (`Sidebar.tsx`)

- `Sidebar.tsx:10-16` — emoji icons instead of SVG (inaccessible, unprofessional)
- `Sidebar.tsx:27` — `<nav>` lacks `aria-label="Main navigation"`
- `Sidebar.tsx:35` — `transition-colors` should list properties explicitly (not `transition: all`, but `transition-colors` is acceptable in Tailwind — minor)

### Cameras Page (`cameras/page.tsx`)

- `:108-122` — form inputs lack `name` and `autocomplete` attributes
- `:113` — placeholder "Front Door Camera" should end with `…`
- `:120` — placeholder "rtsp://..." should end with `…`
- `:141` — checkbox `<input>` not wrapped in `<label>` with proper `htmlFor`/`id` pairing
- `:37-42` — no loading state shown while fetching cameras
- `:53-66` — no error handling on API calls (save silently fails)
- `:97-103` — delete uses inline "Confirm? Yes/No" — acceptable but a modal is better for destructive actions
- `:93-95` — toggle/edit/delete buttons lack `aria-label` (text-only buttons are fine, but toggle is ambiguous)

### Zones Page (`zones/page.tsx`)

- `:84-86` — inputs use only `placeholder` as label (violates: form inputs without labels)
- `:84-86` — inputs lack `name` and `autocomplete`
- `:84` — placeholder "Zone Name" should end with `…`
- `:36-38` — no loading state
- `:96,109` — delete is immediate with no confirmation — destructive action
- `:87` — raw JSON textarea for polygon (poor UX)
- Placeholders don't end with `…`

### Rules Page (`rules/page.tsx`)

- `:50` — delete is immediate with no confirmation — destructive action needs confirmation modal
- `:17-20` — no loading state
- `:38-48` — no error handling on save/delete

### Notifications Page (`notifications/page.tsx`)

- `:30-33` — inputs use only `placeholder` as label (no `<label>` elements)
- `:34` — `<select>` element lacks `aria-label` or associated `<label>`
- `:36` — raw JSON textarea for channel config (poor UX)
- `:22-25` — no loading/error state
- `:44` — delete is immediate — destructive action

### Events Page (`events/page.tsx`)

- `:52` — `formatTimestamp` uses `toLocaleString()` — should use `Intl.DateTimeFormat`
- `:63` — filter input lacks `<label>` (has placeholder only)
- `:77-79` — `<tr onClick>` for expansion — table rows shouldn't be click targets without keyboard support
- `:76` — fragment `<>` around tr pairs needs a `key` on the wrapper
- `:118` — pagination numbers should use `font-variant-numeric: tabular-nums`

### Settings Page (`settings/page.tsx`)

- `:32` — Loading text "Loading..." should be "Loading…" (ellipsis character)
- `:26-29` — stat values should use `font-variant-numeric: tabular-nums`
- No loading skeleton

### Rule Builder Dialog (`RuleBuilderDialog.tsx`)

- `:37` — modal overlay has no `overscroll-behavior: contain`
- `:37` — no Escape key handler to close modal
- `:37` — no focus trap inside modal
- `:38` — no `role="dialog"` or `aria-modal="true"`
- `:42-73` — inputs lack `name` and `autocomplete` attributes
- `ConditionCard.tsx:22` — "Remove" button is text-only, works but could use icon + `aria-label`

### Anti-patterns Detected

- `<div onClick>` navigation — none found (good)
- `transition: all` — none found (good)
- `outline-none` without replacement — none found (good)
- Form inputs without labels — **multiple pages** (zones, notifications, events filter)
- Icon buttons without `aria-label` — sidebar uses emoji, no proper icon buttons
- Images without dimensions — no images yet
- Hardcoded date format — `events/page.tsx:52` uses `toLocaleString()`

---

## Implementation Plan: shadcn/ui Refactor

### Phase 0: Foundation Setup

#### 0.1 — Install shadcn/ui and Dependencies

```bash
cd dashboard
npx shadcn@latest init
```

This sets up:
- CSS variables for theming in `globals.css`
- `tailwind.config.ts` with shadcn preset
- `lib/utils.ts` with `cn()` helper
- `components.json` config file

#### 0.2 — Install Required shadcn Components

```bash
npx shadcn@latest add button card input label select textarea badge \
  table dialog alert-dialog dropdown-menu separator switch tabs \
  tooltip skeleton sonner sidebar breadcrumb sheet \
  navigation-menu toggle-group checkbox scroll-area
```

#### 0.3 — Install Lucide Icons

```bash
npm install lucide-react
```

Replace all emoji icons with Lucide SVG icons. Provides proper `aria-hidden` handling and consistent sizing.

#### 0.4 — Setup Global Styles and Theme

Update `globals.css` with:
- CSS custom properties for light/dark theme (comes with shadcn init)
- `color-scheme: light dark` on `:root`
- `font-variant-numeric: tabular-nums` utility class
- `touch-action: manipulation` on interactive elements
- `prefers-reduced-motion` media query for transitions
- Skip navigation link styles

Update `layout.tsx` with:
- `<meta name="theme-color">` tag
- Skip link `<a href="#main-content" className="sr-only focus:not-sr-only ...">Skip to content</a>`
- `<main id="main-content">` landmark
- Theme provider for dark mode toggle (use `next-themes`)

Install:
```bash
npm install next-themes
```

---

### Phase 1: Layout Shell

#### 1.1 — Replace Sidebar with shadcn Sidebar

**Current:** Custom `<aside>` with emoji icons and hardcoded dark background.

**Target:** Use shadcn `Sidebar` component with:
- Lucide icons (`Camera`, `MapPin`, `Cog`, `Bell`, `Activity`, `LayoutDashboard`, `Settings`)
- Collapsible mode (icon-only on mobile / collapsed)
- `aria-label="Main navigation"` on `<nav>`
- Active state with shadcn accent colors
- Logo/brand area at top
- Optional collapse toggle at bottom
- Dark mode aware via CSS variables

**Files to change:**
- `src/components/Sidebar.tsx` → replace entirely with shadcn `AppSidebar` component
- Create `src/components/app-sidebar.tsx` using shadcn sidebar primitives
- Create `src/components/theme-provider.tsx` — next-themes provider
- Create `src/components/theme-toggle.tsx` — dark/light mode toggle button

#### 1.2 — Update Root Layout

**Current:** Raw `<body className="bg-gray-50 ...">` with inline flex.

**Target:**
- Wrap in `<ThemeProvider>` and `<SidebarProvider>`
- Add `<meta name="theme-color">` via Next.js metadata
- Add skip link
- Use `<SidebarInset>` for main content area
- Add `<Toaster />` from sonner for toast notifications

**Files to change:**
- `src/app/layout.tsx` — restructure with providers and semantic landmarks

#### 1.3 — Create Shared Page Header Component

Create a reusable `PageHeader` component for all pages:
- Title (h1)
- Optional description
- Optional action button slot (right-aligned)
- Breadcrumb integration

**Files to create:**
- `src/components/page-header.tsx`

---

### Phase 2: Cameras Page

#### 2.1 — Camera List Cards

**Current:** Plain `<div>` with borders and inline action buttons.

**Target:** shadcn `Card` components with:
- `CardHeader` with camera name + `Badge` for status (Active/Disabled)
- `CardContent` with stream URL (truncated), FPS, zone count
- `CardFooter` with `DropdownMenu` for actions (Edit, Enable/Disable, Delete)
- Delete via `AlertDialog` (confirmation modal)
- `Skeleton` cards while loading
- Empty state with illustration and CTA

#### 2.2 — Camera Form (Add/Edit)

**Current:** Inline form that appears/hides.

**Target:** shadcn `Dialog` or `Sheet` with:
- `Label` + `Input` with proper `name`, `autocomplete`, `htmlFor`
- `Switch` component for enabled toggle
- Form validation feedback inline
- `Button` with loading spinner on save
- Proper focus trap and Escape-to-close

#### 2.3 — Error & Loading States

- Add `Skeleton` loader for camera list
- Add toast on save success/failure (sonner)
- Add `AlertDialog` for delete confirmation

**Files to change:**
- `src/app/cameras/page.tsx` — full rewrite
- Create `src/components/cameras/camera-card.tsx`
- Create `src/components/cameras/camera-form-dialog.tsx`

---

### Phase 3: Zones & Tripwires Page

#### 3.1 — Zone List

**Current:** Plain list with inline delete.

**Target:**
- `Card` components with zone name, camera association, type badge
- `DropdownMenu` for actions
- `AlertDialog` for delete confirmation
- `Skeleton` loading state
- Empty state

#### 3.2 — Zone Form (Add/Edit)

**Current:** Inline form with raw JSON textarea for polygon.

**Target:** shadcn `Dialog` with:
- `Label` + `Input` for zone name
- `Select` dropdown for camera selection (populated from API)
- `Textarea` for polygon (keep JSON for now, but with proper `Label` and `name`)
- Future: visual polygon editor on camera snapshot

#### 3.3 — Tripwire Section

**Current:** Separate section with numeric coordinate inputs.

**Target:**
- Separate `Card` section with `Separator`
- `Label` + `Input` for all coordinate fields
- `Select` for camera (instead of raw camera ID text input)
- `AlertDialog` for delete confirmation

**Files to change:**
- `src/app/zones/page.tsx` — full rewrite
- Create `src/components/zones/zone-card.tsx`
- Create `src/components/zones/zone-form-dialog.tsx`
- Create `src/components/zones/tripwire-card.tsx`
- Create `src/components/zones/tripwire-form-dialog.tsx`

---

### Phase 4: Rules Page & Rule Builder

#### 4.1 — Rules List

**Current:** Plain cards with inline edit/delete.

**Target:**
- `Card` components with rule name, object class badge, condition count, logic indicator
- `Badge` for enabled/disabled
- `DropdownMenu` for actions (Edit, Toggle, Delete)
- `AlertDialog` for delete
- `Skeleton` loading, empty state

#### 4.2 — Rule Builder Dialog

**Current:** Custom modal with raw `div` overlay, no focus trap, no escape key.

**Target:** shadcn `Dialog` with:
- Proper `DialogContent`, `DialogHeader`, `DialogTitle`, `DialogDescription`
- Focus trapped automatically
- Escape to close
- `overscroll-behavior: contain` via shadcn defaults
- `Label` + `Input` for all form fields with `name`, `autocomplete`
- `Select` for zone (dropdown from API)
- `Select` for logic operator (ALL/ANY)
- Condition palette and canvas kept (improved styling with shadcn Cards)
- `Button` with loading state

#### 4.3 — Condition Cards & Palette

**Current:** Functional but plain styling.

**Target:**
- `Card` for each condition with better visual hierarchy
- `Button variant="outline"` for palette items
- `Input` + `Label` for condition parameters
- `Badge` for AND/OR connectors
- `Button variant="ghost" size="icon"` for remove with `Trash2` icon + `aria-label="Remove condition"`

**Files to change:**
- `src/app/rules/page.tsx` — full rewrite
- `src/components/rule-builder/RuleBuilderDialog.tsx` — refactor to use shadcn Dialog
- `src/components/rule-builder/RuleCanvas.tsx` — update styling
- `src/components/rule-builder/ConditionCard.tsx` — update to shadcn Card + Input + Label
- `src/components/rule-builder/ConditionPalette.tsx` — update to shadcn Button cards

---

### Phase 5: Notifications Page

#### 5.1 — Notification Config List

**Current:** Plain cards with raw rule ID display.

**Target:**
- `Card` with rule name (resolved from API, not just ID)
- Channel type `Badge` components (Webhook, Email, WhatsApp, Dashboard)
- `DropdownMenu` for actions
- `AlertDialog` for delete

#### 5.2 — Notification Form

**Current:** Raw inputs with JSON textarea for config.

**Target:** shadcn `Dialog` with:
- `Select` for rule (dropdown populated from rules API)
- `Select` for channel type
- Dynamic form fields based on channel type:
  - Webhook → `Input` for URL
  - Email → `Input` for email address
  - WhatsApp → `Input` for phone number  
  - Dashboard Push → no extra config
- `Button` for test notification
- Proper `Label` + `Input` with `name`, `autocomplete`

**Files to change:**
- `src/app/notifications/page.tsx` — full rewrite
- Create `src/components/notifications/notification-card.tsx`
- Create `src/components/notifications/notification-form-dialog.tsx`

---

### Phase 6: Events Page

#### 6.1 — Events Table

**Current:** Basic `<table>` with `<tr onClick>` for expansion.

**Target:** shadcn `Table` components (`Table`, `TableHeader`, `TableRow`, `TableHead`, `TableBody`, `TableCell`) with:
- Proper keyboard support for expansion (button inside row, not row itself)
- `Collapsible` for detail expansion (or accordion pattern)
- `Badge` for event type
- `Intl.DateTimeFormat` for timestamps
- `font-variant-numeric: tabular-nums` on number columns
- `Skeleton` rows while loading

#### 6.2 — Filters & Pagination

**Current:** Single text input for source filter, basic prev/next pagination.

**Target:**
- `Label` + `Input` for source filter with proper `name`
- Date range picker (consider shadcn date picker or keep simple for now)
- `Select` for rule filter
- `Select` for event type filter
- Pagination with `Button` group, page info with tabular-nums
- URL state sync for filters/pagination (use `nuqs` or `URLSearchParams`)

#### 6.3 — Event Detail Expansion

**Current:** JSON dump in `<pre>` tags.

**Target:**
- Expandable row or side sheet (`Sheet`) for event details
- Structured display of tracked objects (mini table or card list)
- Metadata displayed as key-value pairs
- Evidence thumbnail if available

**Files to change:**
- `src/app/events/page.tsx` — full rewrite
- Create `src/components/events/event-table.tsx`
- Create `src/components/events/event-detail.tsx`
- Create `src/components/events/event-filters.tsx`

---

### Phase 7: Settings Page

#### 7.1 — System Overview Dashboard

**Current:** Basic stat cards, informational config list.

**Target:**
- `Card` components for each stat with Lucide icon, value, and label
- `font-variant-numeric: tabular-nums` on values
- `Skeleton` cards while loading (not "Loading..." text)
- Consider adding trend/status indicators

#### 7.2 — Config Information Sections

**Current:** Simple divided list.

**Target:**
- Grouped in `Card` with `CardHeader` and `CardContent`
- `Separator` between sections
- `Badge` for values
- Use `<code>` for env var names (already done, just improve styling)

**Files to change:**
- `src/app/settings/page.tsx` — refactor styling

---

### Phase 8: Home / Dashboard Page

#### 8.1 — Dashboard Overview

**Current:** Just a heading and description paragraph.

**Target:** Actual dashboard landing page with:
- Summary stat cards (cameras active, rules active, events today, recent alerts)
- Recent events mini-list (last 5-10 events)
- Quick action buttons (Add Camera, Create Rule)
- System health indicators
- Reuse components from settings and events pages

**Files to change:**
- `src/app/page.tsx` — full rewrite
- Create `src/components/dashboard/stats-cards.tsx`
- Create `src/components/dashboard/recent-events.tsx`

---

### Phase 9: Polish & Accessibility

#### 9.1 — Dark Mode

- `ThemeProvider` wrapping the app (next-themes)
- Theme toggle in sidebar footer
- All colors via CSS variables (comes with shadcn)
- `<meta name="theme-color">` updates with theme
- `color-scheme: dark` on `<html>` in dark mode

#### 9.2 — Responsive Design

- Sidebar collapses to icon-only on mobile or uses `Sheet` overlay
- All pages responsive: cards stack vertically on small screens
- Tables scroll horizontally on small screens (`ScrollArea`)
- Forms adjust to single column on mobile

#### 9.3 — Loading & Error States

- Every page: `Skeleton` components while data loads
- Toast notifications (sonner) for all mutations (create, update, delete)
- Error toast on API failures
- Error boundary component for unexpected errors
- Loading text uses `…` (ellipsis character, not three dots)

#### 9.4 — Micro-interactions

- `prefers-reduced-motion` respected (shadcn handles this mostly)
- Button hover/active states with increased contrast
- `focus-visible:ring` on all interactive elements (shadcn default)
- Smooth transitions on sidebar collapse, dialog open/close

#### 9.5 — Typography & Content

- Proper ellipsis `…` not `...` everywhere
- Placeholders end with `…`
- Specific button labels: "Save Camera" not "Save", "Create Rule" not "Create"
- Active voice throughout
- `text-wrap: balance` on headings

---

## File Impact Summary

### New Files
```
src/components/ui/          ← shadcn component directory (auto-generated)
src/components/app-sidebar.tsx
src/components/page-header.tsx
src/components/theme-provider.tsx
src/components/theme-toggle.tsx
src/components/cameras/camera-card.tsx
src/components/cameras/camera-form-dialog.tsx
src/components/zones/zone-card.tsx
src/components/zones/zone-form-dialog.tsx
src/components/zones/tripwire-card.tsx
src/components/zones/tripwire-form-dialog.tsx
src/components/notifications/notification-card.tsx
src/components/notifications/notification-form-dialog.tsx
src/components/events/event-table.tsx
src/components/events/event-detail.tsx
src/components/events/event-filters.tsx
src/components/dashboard/stats-cards.tsx
src/components/dashboard/recent-events.tsx
src/lib/utils.ts            ← cn() helper (shadcn init)
```

### Modified Files
```
src/app/globals.css         ← CSS variables, base styles, theme
src/app/layout.tsx          ← providers, skip link, meta, toaster
src/app/page.tsx            ← dashboard overview
src/app/cameras/page.tsx    ← shadcn components
src/app/zones/page.tsx      ← shadcn components
src/app/rules/page.tsx      ← shadcn components
src/app/notifications/page.tsx ← shadcn components
src/app/events/page.tsx     ← shadcn components
src/app/settings/page.tsx   ← shadcn components
src/components/Sidebar.tsx  ← replaced by app-sidebar.tsx
src/components/rule-builder/RuleBuilderDialog.tsx ← shadcn Dialog
src/components/rule-builder/RuleCanvas.tsx        ← styling updates
src/components/rule-builder/ConditionCard.tsx     ← shadcn Card/Input/Label
src/components/rule-builder/ConditionPalette.tsx  ← shadcn Button
tailwind.config.ts          ← shadcn preset
package.json                ← new deps
components.json             ← shadcn config (new)
```

### Deleted Files
```
src/components/Sidebar.tsx  ← replaced by app-sidebar.tsx
```

---

## Dependency Additions

| Package | Purpose |
|---|---|
| `lucide-react` | SVG icon library |
| `next-themes` | Dark mode provider |
| `class-variance-authority` | Component variant styling (shadcn dep) |
| `clsx` | Conditional classnames (shadcn dep) |
| `tailwind-merge` | Merge Tailwind classes (shadcn dep) |
| `sonner` | Toast notifications |
| `@radix-ui/*` | Accessible UI primitives (via shadcn) |
| `nuqs` | URL state management for filters/pagination (optional) |

---

## Recommended Execution Order

1. **Phase 0** — Foundation (shadcn init, icons, theme, globals)
2. **Phase 1** — Layout shell (sidebar, layout, page header)
3. **Phase 8** — Home dashboard (quick win, visible improvement)
4. **Phase 2** — Cameras (most straightforward CRUD page)
5. **Phase 3** — Zones & Tripwires
6. **Phase 4** — Rules & Rule Builder
7. **Phase 5** — Notifications
8. **Phase 6** — Events (most complex)
9. **Phase 7** — Settings
10. **Phase 9** — Polish pass (dark mode, responsive, a11y audit)

Each phase should be individually testable and deployable — no phase depends on a later phase.
