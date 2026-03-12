# React & Next.js Best Practices Refactor Plan

**Date:** 2026-03-12  
**Baseline:** Vercel Engineering React Best Practices v1.0.0 (58 rules, 8 categories)  
**Stack:** Next.js 15.3 / React 19.1 / TypeScript 5.8 / Tailwind CSS v4 / shadcn/ui v4 / Prisma 6

---

## Audit Summary

Every page in the dashboard is marked `"use client"` and fetches data inside `useEffect` with raw `fetch()`. The API routes are simple Prisma wrappers with no auth. The `next.config.ts` has no optimization settings. The full review below maps **every applicable Vercel rule** to concrete findings and proposed changes.

---

## Phase 1 — Eliminating Client-Side Waterfalls (CRITICAL)

### 1.1 — Convert pages to Server Components with parallel data fetching

**Rules:** `async-parallel`, `async-suspense-boundaries`, `server-parallel-fetching`

**Finding:** Every page (`page.tsx`, `cameras/page.tsx`, `zones/page.tsx`, `rules/page.tsx`, `notifications/page.tsx`, `events/page.tsx`, `settings/page.tsx`) is an entirely client-side component using `useEffect` + `useState` to fetch data. This creates client-side waterfalls: the browser must download JS → hydrate → then start fetching data. The data is never available on initial HTML render.

**Current pattern (every page):**
```tsx
"use client";
export default function CamerasPage() {
  const [cameras, setCameras] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    fetch("/api/cameras").then(r => r.json()).then(setCameras).finally(() => setLoading(false));
  }, []);
  // ...render
}
```

**Target pattern — server component with direct Prisma queries:**
```tsx
// cameras/page.tsx (Server Component — no "use client")
import { prisma } from "@/lib/prisma";
import { Suspense } from "react";
import { CameraList } from "./camera-list"; // client component for interactions
import { CameraListSkeleton } from "./camera-list-skeleton";

export default function CamerasPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader title="Cameras" description="Manage camera streams." />
      <Suspense fallback={<CameraListSkeleton />}>
        <CameraListLoader />
      </Suspense>
    </div>
  );
}

async function CameraListLoader() {
  const cameras = await prisma.camera.findMany({
    include: { zones: true },
    orderBy: { createdAt: "desc" },
  });
  return <CameraList initialCameras={cameras} />;
}
```

**Changes per file:**

| Page | Parallel fetches needed | Notes |
|------|------------------------|-------|
| `page.tsx` (Home) | cameras, rules, zones, events (4 parallel) | Use `Promise.all` in a single async server component |
| `cameras/page.tsx` | cameras (1) | Split into server wrapper + `CameraList` client component |
| `zones/page.tsx` | zones, tripwires, cameras (3 parallel) | Use `Promise.all`, pass to client |
| `rules/page.tsx` | rules (1) | Same pattern |
| `notifications/page.tsx` | notifications, rules (2 parallel) | Use `Promise.all` |
| `events/page.tsx` | events with pagination (1) | Keep client for filter/pagination, use `searchParams` server-side for initial load |
| `settings/page.tsx` | cameras, rules, zones, events count (4 parallel) | Use `Promise.all` |

**Impact:** Eliminates the JS download → hydrate → fetch waterfall. Data arrives with the HTML. Suspense streams sections independently.

### 1.2 — Parallelize multi-fetch API routes

**Rule:** `async-parallel`, `async-api-routes`

**Finding:** No API routes currently have multi-fetch waterfalls since each route does a single Prisma call. However, `GET /api/cameras/[id]` does `include: { zones: true, rules: true }` which is a single query — this is fine.

**Status:** ✅ No action needed on API routes.

---

## Phase 2 — Bundle Size Optimization (CRITICAL)

### 2.1 — Add `optimizePackageImports` for lucide-react

**Rule:** `bundle-barrel-imports`

**Finding:** Every page imports icons from `lucide-react` barrel:
```tsx
import { Camera, MapPin, Cog, Activity, ArrowRight } from "lucide-react";
import { MoreHorizontal, Pencil, Trash2, Power } from "lucide-react";
import { MoreHorizontal, Trash2, Loader2 } from "lucide-react";
```
lucide-react has 1,500+ icons. Barrel import costs ~200-800ms on cold start.

**Fix — add to `next.config.ts`:**
```ts
const nextConfig: NextConfig = {
  output: "standalone",
  experimental: {
    optimizePackageImports: ["lucide-react"],
  },
};
```

**Impact:** 15-70% faster dev boot, 28% faster builds, 40% faster cold starts.

### 2.2 — Evaluate dynamic imports for heavy dialog components

**Rule:** `bundle-dynamic-imports`

**Finding:** `RuleBuilderDialog` is a complex multi-component dialog (RuleCanvas, ConditionPalette, ConditionCard). It's only shown when the user clicks "Create Rule" or "Edit". Currently it's statically imported and included in the main rules page bundle.

**Fix — `next/dynamic` for RuleBuilderDialog:**
```tsx
import dynamic from "next/dynamic";
const RuleBuilderDialog = dynamic(
  () => import("@/components/rule-builder/RuleBuilderDialog").then(m => m.RuleBuilderDialog),
  { ssr: false }
);
```

Similarly, `CameraFormDialog` could be lazy-loaded since it's only shown on button click.

**Impact:** Reduces initial JS for cameras and rules pages. Dialog code loads on demand.

### 2.3 — Defer Toaster/Sonner to post-hydration

**Rule:** `bundle-defer-third-party`

**Finding:** `<Toaster />` from sonner is loaded in the root layout and always included in the initial bundle. It's not needed until the first toast fires.

**Fix:**
```tsx
import dynamic from "next/dynamic";
const Toaster = dynamic(
  () => import("@/components/ui/sonner").then(m => m.Toaster),
  { ssr: false }
);
```

**Impact:** Removes sonner from initial bundle. Loads after hydration.

---

## Phase 3 — Server-Side Performance (HIGH)

### 3.1 — Minimize serialization at RSC boundaries

**Rule:** `server-serialization`, `server-dedup-props`

**Finding:** After Phase 1 conversion, server components will pass data to client components. We need to ensure we only pass fields the client needs.

**Example — Home page stats:** Instead of passing full camera/rule/zone arrays to the client, compute counts server-side and pass only numbers:
```tsx
// Server component
const [cameraCount, ruleCount, zoneCount, eventCount] = await Promise.all([
  prisma.camera.count(),
  prisma.rule.count(),
  prisma.zone.count(),
  prisma.event.count(),
]);
return <DashboardStats cameras={cameraCount} rules={ruleCount} zones={zoneCount} events={eventCount} />;
```

Currently the home page fetches full arrays via `/api/cameras` etc. just to get `.length`. After RSC conversion, use `prisma.*.count()` directly.

**Example — Cameras list:** Only pass the fields the `CameraList` client component renders:
```tsx
const cameras = await prisma.camera.findMany({
  select: { id: true, name: true, url: true, targetFps: true, enabled: true, zones: { select: { id: true, name: true } } },
  orderBy: { createdAt: "desc" },
});
```

### 3.2 — Use `React.cache()` for shared data

**Rule:** `server-cache-react`

**Finding:** After RSC conversion, the `cameras` list is needed by both `zones/page.tsx` (for the camera dropdown) and `cameras/page.tsx`. Within a single request there's no duplication issue, but if a single page's server component tree calls `prisma.camera.findMany()` in multiple places, we should deduplicate.

**Fix:**
```tsx
// lib/data.ts
import { cache } from "react";
import { prisma } from "./prisma";

export const getCameras = cache(() =>
  prisma.camera.findMany({ orderBy: { createdAt: "desc" } })
);

export const getRules = cache(() =>
  prisma.rule.findMany({ orderBy: { createdAt: "desc" } })
);
```

### 3.3 — Use `after()` for Redis publish in API routes

**Rule:** `server-after-nonblocking`

**Finding:** Every mutation API route awaits `publishConfigChanged()` before returning:
```tsx
await publishConfigChanged("cameras");
return NextResponse.json(camera, { status: 201 });
```
The Redis publish is not critical to the response. It should run after the response is sent.

**Fix:**
```tsx
import { after } from "next/server";

// In POST/PUT/DELETE handlers:
after(() => publishConfigChanged("cameras"));
return NextResponse.json(camera, { status: 201 });
```

**Files:** All 10 mutation handlers across `cameras/[id]`, `cameras/`, `zones/[id]`, `zones/`, `rules/[id]`, `rules/`, `notifications/`, `notifications/[ruleId]`, `tripwires/`, `tripwires/[id]`.

**Impact:** Faster API response times. Redis network latency no longer blocks the response.

---

## Phase 4 — Client-Side Data Fetching (MEDIUM-HIGH)

### 4.1 — Replace raw `fetch` + `useEffect` with SWR for client mutations

**Rule:** `client-swr-dedup`

**Finding:** After Phase 1, initial data loading will move to server components. But client components still need to:
- Refresh data after mutations (create/update/delete)
- Handle optimistic updates

Currently each page manually manages state arrays and splices data after mutations. SWR would provide:
- Automatic revalidation after mutations via `mutate()`
- Deduplication if the same data is needed in multiple places
- Stale-while-revalidate for faster perceived updates

**Fix:** Install `swr` and convert mutation flows:
```tsx
import useSWR from "swr";

function CameraList({ initialCameras }) {
  const { data: cameras, mutate } = useSWR("/api/cameras", fetcher, {
    fallbackData: initialCameras, // from server component
  });

  const handleDelete = async (id) => {
    await fetch(`/api/cameras/${id}`, { method: "DELETE" });
    mutate(); // revalidate
  };
}
```

**Scope:** Apply to cameras, zones, rules, notifications pages. Events page is read-only so it benefits less.

**Impact:** Eliminates manual state management for data arrays, automatic deduplication, graceful revalidation.

### 4.2 — `formatTimestamp` recreation

**Rule:** `rerender-derived-state-no-effect` (indirectly), `js-cache-function-results`

**Finding:** In `page.tsx` (Home), `formatTimestamp` creates a new `Intl.DateTimeFormat` on every call:
```tsx
const formatTimestamp = (ts: string) =>
  new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(ts));
```

In `events/page.tsx`, the formatter is correctly hoisted to module level:
```tsx
const fmt = new Intl.DateTimeFormat(undefined, { dateStyle: "short", timeStyle: "medium" });
```

**Fix:** Hoist the formatter in `page.tsx` to module level, matching the events page pattern.

---

## Phase 5 — Re-render Optimization (MEDIUM)

### 5.1 — Use functional `setState` to prevent stale closures

**Rule:** `rerender-functional-setstate`

**Finding:** Multiple pages directly reference state in setState callbacks, creating stale closure risks and preventing stable callback references:

```tsx
// cameras/page.tsx
setCameras(cameras.map((c) => (c.id === saved.id ? { ...c, ...saved } : c)));
setCameras(cameras.filter((c) => c.id !== deleteTarget.id));
// Same pattern in zones, rules, notifications pages
```

**Fix (all CRUD pages):**
```tsx
setCameras(prev => prev.map(c => c.id === saved.id ? { ...c, ...saved } : c));
setCameras(prev => prev.filter(c => c.id !== deleteTarget.id));
```

**Files:** `cameras/page.tsx`, `zones/page.tsx`, `rules/page.tsx`, `notifications/page.tsx`

### 5.2 — Hoist `Intl.DateTimeFormat` instance

**Rule:** `rerender-lazy-state-init` (related — avoid re-creating expensive objects)

**Finding:** As noted in 4.2, the `formatTimestamp` function in `page.tsx` recreates `Intl.DateTimeFormat` on every invocation. Hoist to module level.

### 5.3 — Derive `fields` during render, not in state

**Rule:** `rerender-derived-state-no-effect`

**Finding:** In `notifications/page.tsx`, `fields` is derived correctly:
```tsx
const fields = CHANNEL_FIELDS[channelType] ?? [];
```
This is already the correct pattern. ✅ No action needed.

### 5.4 — Hoist `cameraName` lookup function

**Rule:** `js-index-maps`

**Finding:** Both `zones/page.tsx` and `notifications/page.tsx` use:
```tsx
const cameraName = (id: string) => cameras.find((c) => c.id === id)?.name ?? id;
```
This is O(n) per call. For small lists this is fine, but for consistency, extract to a Map-based lookup when the list grows.

**Fix (zones/page.tsx, notifications/page.tsx):**
```tsx
const cameraById = useMemo(
  () => new Map(cameras.map(c => [c.id, c.name])),
  [cameras]
);
const cameraName = (id: string) => cameraById.get(id) ?? id;
```

**Impact:** O(1) lookups. Minor for current data sizes, but follows best practices and scales.

### 5.5 — CameraFormDialog does not reset form on reopen

**Rule:** `rerender-derived-state-no-effect`

**Finding:** `CameraFormDialog` initializes state from `initialData` only once:
```tsx
const [form, setForm] = useState<CameraForm>(initialData ?? emptyForm);
```
When the dialog reopens with different `initialData` (switching from "add" to "edit"), the state doesn't update because `useState` only uses the initial value on first mount.

**Fix:** Use a `key` prop on the dialog to force remount, or use `useEffect` to sync:
```tsx
// In cameras/page.tsx — force remount by key:
<CameraFormDialog key={editingCamera?.id ?? "new"} ... />
```
This is the idiomatic React pattern for resetting component state when data changes.

Similarly, `RuleBuilderDialog` has the same issue — it initializes from `initialData` only on mount.

---

## Phase 6 — Rendering Performance (MEDIUM)

### 6.1 — Use conditional rendering with ternary, not `&&`

**Rule:** `rendering-conditional-render`

**Finding:** Several places use `&&` for conditional rendering. Most are safe (boolean conditions), but should be reviewed:

```tsx
// page-header.tsx
{action && (<Button onClick={action.onClick}>{action.label}</Button>)}
```

This is safe because `action` is an object (renders nothing when falsy). ✅ No urgent fix needed.

```tsx
// events/page.tsx
{expandedId && (() => { ... })()}
```

`expandedId` is `string | null`, truthy check is fine. ✅ No fix needed.

**Status:** No `&&` rendering bugs found. ✅

### 6.2 — Use `content-visibility` for events table

**Rule:** `rendering-content-visibility`

**Finding:** The events page renders up to 20 rows. If expanded to larger page sizes, `content-visibility: auto` on `<TableRow>` would skip layout for off-screen rows.

**Fix — add CSS class:**
```css
.event-row {
  content-visibility: auto;
  contain-intrinsic-size: 0 48px;
}
```

**Impact:** Low for current 20-row limit. Useful if page size increases.

### 6.3 — Use `useTransition` for events filter/pagination

**Rule:** `rendering-usetransition-loading`

**Finding:** `events/page.tsx` uses manual `loading` state:
```tsx
const [loading, setLoading] = useState(true);
useEffect(() => {
  setLoading(true);
  // fetch...
  .finally(() => setLoading(false));
}, [offset, sourceFilter]);
```

**Fix:** Use `useTransition` for the filter/pagination actions:
```tsx
const [isPending, startTransition] = useTransition();

const handleFilterChange = (value: string) => {
  startTransition(() => {
    setSourceFilter(value);
    setOffset(0);
  });
};
```

This keeps the previous results visible while new data loads, instead of showing skeleton rows.

---

## Phase 7 — JavaScript Performance (LOW-MEDIUM)

### 7.1 — Hoist `Intl.DateTimeFormat` to module level

**Rule:** `js-cache-function-results`

**Finding:** Already covered in 4.2/5.2. `page.tsx` creates a new formatter on every call. `events/page.tsx` does it correctly.

**Fix:** Hoist in `page.tsx`:
```tsx
const fmt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

const formatTimestamp = (ts: string) => fmt.format(new Date(ts));
```

### 7.2 — Hoist `truncateUrl` to module level

**Rule:** `js-early-exit`

**Finding:** `cameras/page.tsx` defines `truncateUrl` inside the component:
```tsx
const truncateUrl = (url: string, max = 45) =>
  url.length > max ? url.slice(0, max) + "…" : url;
```
This recreates the function on every render. Move to module level.

---

## Phase 8 — Next.js Configuration (HIGH)

### 8.1 — Add `optimizePackageImports` to `next.config.ts`

Already described in Phase 2.1. One-line config change.

### 8.2 — Consider `serverExternalPackages` for Prisma

**Finding:** Prisma can benefit from being treated as an external package on the server:
```ts
const nextConfig: NextConfig = {
  output: "standalone",
  serverExternalPackages: ["@prisma/client"],
  experimental: {
    optimizePackageImports: ["lucide-react"],
  },
};
```

---

## Implementation Priority & Ordering

| Priority | Phase | Impact | Effort | Description |
|----------|-------|--------|--------|-------------|
| 🔴 1 | Phase 2.1 | CRITICAL | ~5 min | Add `optimizePackageImports` to next.config.ts |
| 🔴 2 | Phase 3.3 | HIGH | ~30 min | Convert `publishConfigChanged` to use `after()` in API routes |
| 🔴 3 | Phase 1.1 | CRITICAL | ~3 hr | Convert pages to Server Components with Suspense |
| 🟠 4 | Phase 2.2 | HIGH | ~15 min | Dynamic imports for RuleBuilderDialog, CameraFormDialog |
| 🟠 5 | Phase 3.1 | HIGH | ~30 min | Minimize serialization (select only needed fields, use count()) |
| 🟠 6 | Phase 3.2 | MEDIUM | ~15 min | Create `lib/data.ts` with `React.cache()` wrapped queries |
| 🟡 7 | Phase 4.1 | MEDIUM-HIGH | ~1 hr | Install SWR, convert client mutation flows |
| 🟡 8 | Phase 5.1 | MEDIUM | ~15 min | Convert all setState to functional updates |
| 🟡 9 | Phase 5.5 | MEDIUM | ~10 min | Add `key` prop to force dialog remount on data change |
| 🟢 10 | Phase 7 | LOW | ~10 min | Hoist formatTimestamp, truncateUrl to module level |
| 🟢 11 | Phase 6.3 | LOW | ~15 min | useTransition for events filter/pagination |
| 🟢 12 | Phase 2.3 | LOW | ~5 min | Dynamic import for Toaster |
| 🟢 13 | Phase 5.4 | LOW | ~10 min | Map-based camera lookup in zones/notifications |

---

## Detailed File Change Matrix

| File | Phase 1 (RSC) | Phase 2 (Bundle) | Phase 3 (Server) | Phase 4 (SWR) | Phase 5 (Rerenders) | Phase 7 (JS) |
|------|:---:|:---:|:---:|:---:|:---:|:---:|
| `next.config.ts` | | ✏️ | | | | |
| `layout.tsx` | | ✏️ (Toaster) | | | | |
| `page.tsx` | ✏️ RSC split | | ✏️ count() | | | ✏️ hoist fmt |
| `cameras/page.tsx` | ✏️ RSC split | ✏️ dynamic dialog | | ✏️ SWR | ✏️ func setState, key | ✏️ hoist truncate |
| `zones/page.tsx` | ✏️ RSC split | | | ✏️ SWR | ✏️ func setState | ✏️ Map lookup |
| `rules/page.tsx` | ✏️ RSC split | ✏️ dynamic dialog | | ✏️ SWR | ✏️ func setState, key | |
| `notifications/page.tsx` | ✏️ RSC split | | | ✏️ SWR | ✏️ func setState | ✏️ Map lookup |
| `events/page.tsx` | ✏️ partial RSC | | | | | ✏️ useTransition |
| `settings/page.tsx` | ✏️ RSC | | ✏️ count() | | | |
| `api/cameras/route.ts` | | | ✏️ after() | | | |
| `api/cameras/[id]/route.ts` | | | ✏️ after() | | | |
| `api/zones/route.ts` | | | ✏️ after() | | | |
| `api/zones/[id]/route.ts` | | | ✏️ after() | | | |
| `api/rules/route.ts` | | | ✏️ after() | | | |
| `api/rules/[id]/route.ts` | | | ✏️ after() | | | |
| `api/notifications/route.ts` | | | ✏️ after() | | | |
| `api/notifications/[ruleId]/route.ts` | | | ✏️ after() | | | |
| `api/tripwires/route.ts` | | | ✏️ after() | | | |
| `api/tripwires/[id]/route.ts` | | | ✏️ after() | | | |
| `lib/data.ts` (NEW) | | | ✏️ cache() | | | |
| `camera-form-dialog.tsx` | | | | | ✏️ key reset | |
| `RuleBuilderDialog.tsx` | | | | | ✏️ key reset | |

---

## Rules Reviewed but Not Applicable

| Rule | Why N/A |
|------|---------|
| `server-auth-actions` | No Server Actions used currently. If added, must authenticate. |
| `server-hoist-static-io` | No static I/O (fonts loaded via `next/font`, no OG images). |
| `client-event-listeners` | No global event listeners used. |
| `client-passive-event-listeners` | No scroll/touch listeners. |
| `client-localstorage-schema` | No localStorage usage (theme handled by next-themes). |
| `rerender-memo` | No expensive computations that warrant memo extraction. |
| `rerender-dependencies` | Effects have minimal deps, already narrow. |
| `rerender-transitions` | No frequent continuous state updates except events pagination (covered). |
| `rerender-use-ref-transient-values` | No transient values (no mousemove, no intervals). |
| `rendering-animate-svg-wrapper` | No animated SVGs. |
| `rendering-svg-precision` | No custom SVGs (using lucide-react). |
| `rendering-hydration-*` | Theme handled by next-themes with `suppressHydrationWarning`. |
| `rendering-activity` | No frequently toggling expensive components. |
| `js-batch-dom-css` | No direct DOM style manipulation. |
| `advanced-*` | No complex callback ref patterns needed. |
