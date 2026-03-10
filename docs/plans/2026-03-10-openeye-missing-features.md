# OpenEye Missing Features Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement all features from the design doc that are currently Not Met or Partially Met — dashboard navigation, zones/notifications/settings pages, missing API routes, docker-compose application services, and evidence request handling.

**Architecture:** Add a shared sidebar layout to the existing Next.js dashboard, create CRUD pages + API routes for zones/tripwires/notifications/settings, wire docker-compose with all 5 application services, and add evidence request forwarding in EventRouter.

**Tech Stack:** Next.js (App Router), TypeScript, Tailwind CSS, Prisma, Zod, Docker Compose, .NET 10 / C#

---

## Gap Summary

| Gap | Status | Plan Task |
|-----|--------|-----------|
| Shared sidebar navigation | Missing | Task 1 |
| Zones & Tripwires management page + API routes | Missing | Task 2 |
| Primitives API routes | Missing | Task 3 |
| Notifications page + API route | Missing | Task 4 |
| Settings page | Missing | Task 5 |
| Docker Compose application services | Partial | Task 6 |
| Evidence request handling in EventRouter | Missing | Task 7 |
| Aspire dashboard ↔ Redis wiring | Partial | Task 8 |

## File Structure

```
dashboard/src/
├── app/
│   ├── layout.tsx                          ← MODIFY (add sidebar layout)
│   ├── page.tsx                            ← MODIFY (redirect or simplify)
│   ├── cameras/page.tsx                    ← EXISTS (add back-link)
│   ├── rules/page.tsx                      ← EXISTS
│   ├── events/page.tsx                     ← EXISTS
│   ├── zones/page.tsx                      ← CREATE
│   ├── notifications/page.tsx              ← CREATE
│   ├── settings/page.tsx                   ← CREATE
│   └── api/
│       ├── cameras/                        ← EXISTS
│       ├── zones/                          ← EXISTS
│       ├── rules/                          ← EXISTS
│       ├── events/                         ← EXISTS
│       ├── tripwires/
│       │   ├── route.ts                    ← CREATE (GET list, POST create)
│       │   └── [id]/route.ts              ← CREATE (GET, PUT, DELETE)
│       ├── primitives/
│       │   ├── route.ts                    ← CREATE (GET list, POST create)
│       │   └── [id]/route.ts              ← CREATE (GET, PUT, DELETE)
│       └── notifications/
│           ├── route.ts                    ← CREATE (GET list, POST create)
│           └── [ruleId]/route.ts          ← CREATE (GET, PUT, DELETE)
├── components/
│   ├── Sidebar.tsx                         ← CREATE
│   └── rule-builder/                       ← EXISTS
└── lib/
    ├── prisma.ts                           ← EXISTS
    ├── redis.ts                            ← EXISTS
    └── validations.ts                      ← MODIFY (add tripwire, primitive, notification schemas)

docker/
└── docker-compose.yml                      ← MODIFY (add app service definitions)

src/OpenEye.EventRouter/
└── Worker.cs                               ← MODIFY (add evidence request handling)

src/OpenEye.AppHost/
└── AppHost.cs                              ← MODIFY (pass Redis reference to dashboard)
```

---

## Chunk 1: Dashboard Navigation & Pages

### Task 1: Sidebar Navigation Component & Layout Update

**Files:**
- Create: `dashboard/src/components/Sidebar.tsx`
- Modify: `dashboard/src/app/layout.tsx`
- Modify: `dashboard/src/app/page.tsx`

- [ ] **Step 1: Create Sidebar component**

```tsx
// dashboard/src/components/Sidebar.tsx
"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navItems = [
  { href: "/", label: "Home", icon: "🏠" },
  { href: "/cameras", label: "Cameras", icon: "📷" },
  { href: "/zones", label: "Zones", icon: "📐" },
  { href: "/rules", label: "Rules", icon: "⚙️" },
  { href: "/notifications", label: "Notifications", icon: "🔔" },
  { href: "/events", label: "Events", icon: "📋" },
  { href: "/settings", label: "Settings", icon: "🛠️" },
];

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="w-56 bg-gray-900 text-gray-100 min-h-screen flex flex-col">
      <div className="p-4 border-b border-gray-700">
        <h1 className="text-lg font-bold">OpenEye</h1>
      </div>
      <nav className="flex-1 p-2">
        {navItems.map((item) => {
          const isActive = pathname === item.href;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={`flex items-center gap-3 px-3 py-2 rounded-md text-sm mb-1 transition-colors ${
                isActive
                  ? "bg-gray-700 text-white"
                  : "text-gray-400 hover:bg-gray-800 hover:text-white"
              }`}
            >
              <span>{item.icon}</span>
              <span>{item.label}</span>
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
```

- [ ] **Step 2: Update layout.tsx to include Sidebar**

```tsx
// dashboard/src/app/layout.tsx
import type { Metadata } from "next";
import "./globals.css";
import { Sidebar } from "@/components/Sidebar";

export const metadata: Metadata = {
  title: "OpenEye Dashboard",
  description: "Video analytics monitoring and rule configuration",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-gray-50 text-gray-900 min-h-screen flex">
        <Sidebar />
        <main className="flex-1 overflow-auto">{children}</main>
      </body>
    </html>
  );
}
```

- [ ] **Step 3: Simplify home page (sidebar provides navigation now)**

```tsx
// dashboard/src/app/page.tsx
export default function Home() {
  return (
    <div className="p-8">
      <h1 className="text-3xl font-bold">OpenEye Dashboard</h1>
      <p className="mt-2 text-gray-600">
        Video analytics monitoring and configuration. Use the sidebar to navigate.
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Verify dashboard compiles**

Run: `cd dashboard && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add dashboard/src/components/Sidebar.tsx dashboard/src/app/layout.tsx dashboard/src/app/page.tsx
git commit -m "feat: add sidebar navigation and update dashboard layout"
```

---

### Task 2: Zones & Tripwires Page + API Routes

**Files:**
- Create: `dashboard/src/app/api/tripwires/route.ts`
- Create: `dashboard/src/app/api/tripwires/[id]/route.ts`
- Create: `dashboard/src/app/zones/page.tsx`
- Modify: `dashboard/src/lib/validations.ts` (add tripwire schemas)

- [ ] **Step 1: Add tripwire Zod schemas to validations.ts**

Append to `dashboard/src/lib/validations.ts`:

```typescript
// --- Tripwire ---
export const createTripwireSchema = z.object({
  sourceId: z.string().min(1),
  startX: z.number().min(0).max(1),
  startY: z.number().min(0).max(1),
  endX: z.number().min(0).max(1),
  endY: z.number().min(0).max(1),
});

export const updateTripwireSchema = createTripwireSchema.partial();
```

- [ ] **Step 2: Create tripwires API route (list + create)**

```typescript
// dashboard/src/app/api/tripwires/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createTripwireSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const tripwires = await prisma.tripwire.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(tripwires);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createTripwireSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const tripwire = await prisma.tripwire.create({ data: result.data });
  await publishConfigChanged("tripwires");
  return NextResponse.json(tripwire, { status: 201 });
}
```

- [ ] **Step 3: Create tripwires [id] API route (get, update, delete)**

```typescript
// dashboard/src/app/api/tripwires/[id]/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateTripwireSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const tripwire = await prisma.tripwire.findUnique({ where: { id } });
  if (!tripwire) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(tripwire);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const result = updateTripwireSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const tripwire = await prisma.tripwire.update({ where: { id }, data: result.data });
  await publishConfigChanged("tripwires");
  return NextResponse.json(tripwire);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.tripwire.delete({ where: { id } });
  await publishConfigChanged("tripwires");
  return NextResponse.json({ deleted: true });
}
```

- [ ] **Step 4: Create zones page with zone list + tripwire list**

```tsx
// dashboard/src/app/zones/page.tsx
"use client";

import { useEffect, useState } from "react";

interface Zone {
  id: string;
  name: string;
  cameraId: string;
  polygon: unknown;
  type: string;
}

interface Tripwire {
  id: string;
  sourceId: string;
  startX: number;
  startY: number;
  endX: number;
  endY: number;
}

export default function ZonesPage() {
  const [zones, setZones] = useState<Zone[]>([]);
  const [tripwires, setTripwires] = useState<Tripwire[]>([]);
  const [showAddZone, setShowAddZone] = useState(false);
  const [showAddTripwire, setShowAddTripwire] = useState(false);
  const [zoneName, setZoneName] = useState("");
  const [zoneCameraId, setZoneCameraId] = useState("");
  const [zonePolygon, setZonePolygon] = useState("");
  const [tripwireSourceId, setTripwireSourceId] = useState("");
  const [tripwireCoords, setTripwireCoords] = useState({ startX: 0, startY: 0, endX: 1, endY: 1 });

  useEffect(() => {
    fetch("/api/zones").then((r) => r.json()).then(setZones);
    fetch("/api/tripwires").then((r) => r.json()).then(setTripwires);
  }, []);

  const handleAddZone = async () => {
    let polygon;
    try { polygon = JSON.parse(zonePolygon); } catch { return; }
    const res = await fetch("/api/zones", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: zoneName, cameraId: zoneCameraId, polygon }),
    });
    if (res.ok) {
      const zone = await res.json();
      setZones([zone, ...zones]);
      setShowAddZone(false);
      setZoneName("");
      setZoneCameraId("");
      setZonePolygon("");
    }
  };

  const handleDeleteZone = async (id: string) => {
    await fetch(`/api/zones/${id}`, { method: "DELETE" });
    setZones(zones.filter((z) => z.id !== id));
  };

  const handleAddTripwire = async () => {
    const res = await fetch("/api/tripwires", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sourceId: tripwireSourceId, ...tripwireCoords }),
    });
    if (res.ok) {
      const tw = await res.json();
      setTripwires([tw, ...tripwires]);
      setShowAddTripwire(false);
      setTripwireSourceId("");
      setTripwireCoords({ startX: 0, startY: 0, endX: 1, endY: 1 });
    }
  };

  const handleDeleteTripwire = async (id: string) => {
    await fetch(`/api/tripwires/${id}`, { method: "DELETE" });
    setTripwires(tripwires.filter((t) => t.id !== id));
  };

  return (
    <div className="p-8 max-w-4xl">
      {/* Zones Section */}
      <div className="mb-10">
        <div className="flex items-center justify-between mb-4">
          <h1 className="text-2xl font-bold">Zones</h1>
          <button onClick={() => setShowAddZone(!showAddZone)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
            Add Zone
          </button>
        </div>

        {showAddZone && (
          <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
            <input placeholder="Zone Name" value={zoneName} onChange={(e) => setZoneName(e.target.value)} className="w-full border rounded px-3 py-2" />
            <input placeholder="Camera ID" value={zoneCameraId} onChange={(e) => setZoneCameraId(e.target.value)} className="w-full border rounded px-3 py-2" />
            <textarea placeholder='Polygon JSON, e.g. [{"x":0.2,"y":0.2},{"x":0.8,"y":0.2},{"x":0.8,"y":0.8},{"x":0.2,"y":0.8}]' value={zonePolygon} onChange={(e) => setZonePolygon(e.target.value)} className="w-full border rounded px-3 py-2 h-20 font-mono text-sm" />
            <div className="flex gap-2">
              <button onClick={handleAddZone} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
              <button onClick={() => setShowAddZone(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
            </div>
          </div>
        )}

        <div className="space-y-2">
          {zones.map((zone) => (
            <div key={zone.id} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
              <div>
                <h3 className="font-medium">{zone.name}</h3>
                <p className="text-sm text-gray-500">Camera: {zone.cameraId} · Type: {zone.type}</p>
              </div>
              <button onClick={() => handleDeleteZone(zone.id)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
            </div>
          ))}
          {zones.length === 0 && <p className="text-gray-500 text-center py-4">No zones configured.</p>}
        </div>
      </div>

      {/* Tripwires Section */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-2xl font-bold">Tripwires</h2>
          <button onClick={() => setShowAddTripwire(!showAddTripwire)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
            Add Tripwire
          </button>
        </div>

        {showAddTripwire && (
          <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
            <input placeholder="Camera ID" value={tripwireSourceId} onChange={(e) => setTripwireSourceId(e.target.value)} className="w-full border rounded px-3 py-2" />
            <div className="grid grid-cols-2 gap-2">
              <input type="number" step="0.01" min="0" max="1" placeholder="Start X" value={tripwireCoords.startX}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, startX: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="Start Y" value={tripwireCoords.startY}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, startY: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="End X" value={tripwireCoords.endX}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, endX: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="End Y" value={tripwireCoords.endY}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, endY: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
            </div>
            <div className="flex gap-2">
              <button onClick={handleAddTripwire} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
              <button onClick={() => setShowAddTripwire(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
            </div>
          </div>
        )}

        <div className="space-y-2">
          {tripwires.map((tw) => (
            <div key={tw.id} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
              <div>
                <h3 className="font-medium font-mono text-sm">{tw.id}</h3>
                <p className="text-sm text-gray-500">
                  Camera: {tw.sourceId} · ({tw.startX.toFixed(2)}, {tw.startY.toFixed(2)}) → ({tw.endX.toFixed(2)}, {tw.endY.toFixed(2)})
                </p>
              </div>
              <button onClick={() => handleDeleteTripwire(tw.id)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
            </div>
          ))}
          {tripwires.length === 0 && <p className="text-gray-500 text-center py-4">No tripwires configured.</p>}
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Verify dashboard compiles**

Run: `cd dashboard && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add dashboard/src/app/zones/ dashboard/src/app/api/tripwires/ dashboard/src/lib/validations.ts
git commit -m "feat: add zones & tripwires page with CRUD API routes"
```

---

### Task 3: Primitives API Routes

**Files:**
- Create: `dashboard/src/app/api/primitives/route.ts`
- Create: `dashboard/src/app/api/primitives/[name]/route.ts`
- Modify: `dashboard/src/lib/validations.ts` (add primitive schemas)

- [ ] **Step 1: Add primitive Zod schemas to validations.ts**

Append to `dashboard/src/lib/validations.ts`:

```typescript
// --- Primitive Config ---
export const createPrimitiveSchema = z.object({
  name: z.string().min(1).max(255),
  type: z.string().min(1).max(50),
  classLabel: z.string().min(1).max(255),
  zoneId: z.string().nullable().optional(),
  tripwireId: z.string().nullable().optional(),
  sourceId: z.string().min(1),
});

export const updatePrimitiveSchema = createPrimitiveSchema.partial().omit({ name: true });
```

- [ ] **Step 2: Create primitives API route (list + create)**

```typescript
// dashboard/src/app/api/primitives/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createPrimitiveSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const primitives = await prisma.primitiveConfig.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(primitives);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createPrimitiveSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const primitive = await prisma.primitiveConfig.create({ data: result.data });
  await publishConfigChanged("primitives");
  return NextResponse.json(primitive, { status: 201 });
}
```

- [ ] **Step 3: Create primitives [name] API route (get, update, delete)**

```typescript
// dashboard/src/app/api/primitives/[name]/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updatePrimitiveSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  const primitive = await prisma.primitiveConfig.findUnique({ where: { name } });
  if (!primitive) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(primitive);
}

export async function PUT(request: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  const body = await request.json();
  const result = updatePrimitiveSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const primitive = await prisma.primitiveConfig.update({ where: { name }, data: result.data });
  await publishConfigChanged("primitives");
  return NextResponse.json(primitive);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  await prisma.primitiveConfig.delete({ where: { name } });
  await publishConfigChanged("primitives");
  return NextResponse.json({ deleted: true });
}
```

- [ ] **Step 4: Verify dashboard compiles**

Run: `cd dashboard && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add dashboard/src/app/api/primitives/ dashboard/src/lib/validations.ts
git commit -m "feat: add primitives CRUD API routes with Zod validation"
```

---

### Task 4: Notifications Page + API Route

**Files:**
- Create: `dashboard/src/app/api/notifications/route.ts`
- Create: `dashboard/src/app/api/notifications/[ruleId]/route.ts`
- Create: `dashboard/src/app/notifications/page.tsx`
- Modify: `dashboard/src/lib/validations.ts` (add notification schemas)

- [ ] **Step 1: Add notification Zod schemas to validations.ts**

Append to `dashboard/src/lib/validations.ts`:

```typescript
// --- Notification Config ---
export const createNotificationSchema = z.object({
  ruleId: z.string().min(1),
  channels: z.array(z.object({
    type: z.string().min(1).max(50),
    config: z.record(z.string()),
  })).min(1),
});

export const updateNotificationSchema = z.object({
  channels: z.array(z.object({
    type: z.string().min(1).max(50),
    config: z.record(z.string()),
  })).min(1),
});
```

- [ ] **Step 2: Create notifications API route (list + create)**

```typescript
// dashboard/src/app/api/notifications/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createNotificationSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET() {
  const configs = await prisma.notificationConfig.findMany({
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(configs);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createNotificationSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const config = await prisma.notificationConfig.create({ data: result.data });
  await publishConfigChanged("notifications");
  return NextResponse.json(config, { status: 201 });
}
```

- [ ] **Step 3: Create notifications [ruleId] API route (get, update, delete)**

```typescript
// dashboard/src/app/api/notifications/[ruleId]/route.ts
import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateNotificationSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  const config = await prisma.notificationConfig.findUnique({ where: { ruleId } });
  if (!config) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(config);
}

export async function PUT(request: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  const body = await request.json();
  const result = updateNotificationSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const config = await prisma.notificationConfig.update({ where: { ruleId }, data: result.data });
  await publishConfigChanged("notifications");
  return NextResponse.json(config);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  await prisma.notificationConfig.delete({ where: { ruleId } });
  await publishConfigChanged("notifications");
  return NextResponse.json({ deleted: true });
}
```

- [ ] **Step 4: Create notifications page**

```tsx
// dashboard/src/app/notifications/page.tsx
"use client";

import { useEffect, useState } from "react";

interface NotificationChannel {
  type: string;
  config: Record<string, string>;
}

interface NotificationConfig {
  ruleId: string;
  channels: NotificationChannel[];
}

export default function NotificationsPage() {
  const [configs, setConfigs] = useState<NotificationConfig[]>([]);
  const [showAdd, setShowAdd] = useState(false);
  const [ruleId, setRuleId] = useState("");
  const [channelType, setChannelType] = useState("webhook");
  const [channelConfigStr, setChannelConfigStr] = useState('{"url": ""}');

  useEffect(() => {
    fetch("/api/notifications").then((r) => r.json()).then(setConfigs);
  }, []);

  const handleAdd = async () => {
    let config;
    try { config = JSON.parse(channelConfigStr); } catch { return; }
    const res = await fetch("/api/notifications", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ruleId, channels: [{ type: channelType, config }] }),
    });
    if (res.ok) {
      const nc = await res.json();
      setConfigs([nc, ...configs]);
      setShowAdd(false);
      setRuleId("");
      setChannelConfigStr('{"url": ""}');
    }
  };

  const handleDelete = async (id: string) => {
    await fetch(`/api/notifications/${id}`, { method: "DELETE" });
    setConfigs(configs.filter((c) => c.ruleId !== id));
  };

  return (
    <div className="p-8 max-w-4xl">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Notifications</h1>
        <button onClick={() => setShowAdd(!showAdd)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
          Add Notification
        </button>
      </div>

      {showAdd && (
        <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
          <input placeholder="Rule ID" value={ruleId} onChange={(e) => setRuleId(e.target.value)} className="w-full border rounded px-3 py-2" />
          <select value={channelType} onChange={(e) => setChannelType(e.target.value)} className="w-full border rounded px-3 py-2">
            <option value="webhook">Webhook</option>
            <option value="email">Email</option>
            <option value="whatsapp">WhatsApp</option>
            <option value="dashboard">Dashboard Push</option>
          </select>
          <textarea placeholder='Channel config JSON, e.g. {"url": "https://example.com/hook"}' value={channelConfigStr}
            onChange={(e) => setChannelConfigStr(e.target.value)} className="w-full border rounded px-3 py-2 h-16 font-mono text-sm" />
          <div className="flex gap-2">
            <button onClick={handleAdd} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
            <button onClick={() => setShowAdd(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
          </div>
        </div>
      )}

      <div className="space-y-2">
        {configs.map((nc) => (
          <div key={nc.ruleId} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
            <div>
              <h3 className="font-medium font-mono text-sm">Rule: {nc.ruleId}</h3>
              <p className="text-sm text-gray-500">
                {nc.channels.map((ch) => ch.type).join(", ")} · {nc.channels.length} channel(s)
              </p>
            </div>
            <button onClick={() => handleDelete(nc.ruleId)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
          </div>
        ))}
        {configs.length === 0 && <p className="text-gray-500 text-center py-4">No notification configs. Add one to get started.</p>}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Verify dashboard compiles**

Run: `cd dashboard && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add dashboard/src/app/notifications/ dashboard/src/app/api/notifications/ dashboard/src/lib/validations.ts
git commit -m "feat: add notifications page and CRUD API routes"
```

---

### Task 5: Settings Page

**Files:**
- Create: `dashboard/src/app/settings/page.tsx`

The design doc specifies a Settings page for: detection model config (inference URL, model ID, confidence) and system health overview. This is read-only configuration display since these settings come from appsettings.json / environment variables, not from the database.

- [ ] **Step 1: Create settings page**

```tsx
// dashboard/src/app/settings/page.tsx
"use client";

import { useEffect, useState } from "react";

interface SystemHealth {
  cameras: number;
  rules: number;
  zones: number;
  events: number;
}

export default function SettingsPage() {
  const [health, setHealth] = useState<SystemHealth | null>(null);

  useEffect(() => {
    Promise.all([
      fetch("/api/cameras").then((r) => r.json()),
      fetch("/api/rules").then((r) => r.json()),
      fetch("/api/zones").then((r) => r.json()),
      fetch("/api/events?limit=1").then((r) => r.json()),
    ]).then(([cameras, rules, zones, eventsData]) => {
      setHealth({
        cameras: cameras.length,
        rules: rules.length,
        zones: zones.length,
        events: eventsData.total ?? 0,
      });
    });
  }, []);

  return (
    <div className="p-8 max-w-4xl">
      <h1 className="text-2xl font-bold mb-6">Settings</h1>

      {/* System Overview */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold mb-3">System Overview</h2>
        {health ? (
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {[
              { label: "Cameras", value: health.cameras },
              { label: "Zones", value: health.zones },
              { label: "Rules", value: health.rules },
              { label: "Total Events", value: health.events },
            ].map((stat) => (
              <div key={stat.label} className="border rounded-lg p-4 bg-white shadow-sm text-center">
                <p className="text-2xl font-bold">{stat.value}</p>
                <p className="text-sm text-gray-500">{stat.label}</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-gray-400">Loading...</p>
        )}
      </div>

      {/* Detection Model Config (informational) */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold mb-3">Detection Model Configuration</h2>
        <p className="text-sm text-gray-600 mb-3">
          These settings are configured via environment variables or appsettings.json on the backend services.
        </p>
        <div className="border rounded-lg bg-white shadow-sm divide-y">
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Inference URL</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__URL</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Model ID</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__MODELID</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Confidence Threshold</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__CONFIDENCETHRESHOLD</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">API Key</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__APIKEY</code>
          </div>
        </div>
      </div>

      {/* Infrastructure */}
      <div>
        <h2 className="text-lg font-semibold mb-3">Infrastructure</h2>
        <div className="border rounded-lg bg-white shadow-sm divide-y">
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Database</span>
            <span className="text-sm">PostgreSQL (via Prisma)</span>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Message Bus</span>
            <span className="text-sm">Redis Streams</span>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Orchestration</span>
            <span className="text-sm">.NET Aspire</span>
          </div>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Verify dashboard compiles**

Run: `cd dashboard && npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add dashboard/src/app/settings/
git commit -m "feat: add settings page with system overview and config display"
```

---

## Chunk 2: Infrastructure & Backend

### Task 6: Docker Compose Application Services

**Files:**
- Modify: `docker/docker-compose.yml`

The current docker-compose.yml only has `redis`, `postgres`, and `roboflow-inference`. We need to add the 5 application services. These use the .NET Aspire host in production, but docker-compose provides a standalone fallback.

- [ ] **Step 1: Update docker-compose.yml with application service definitions**

Replace the full `docker/docker-compose.yml` with:

```yaml
# docker/docker-compose.yml
# Standalone Docker Compose for OpenEye platform.
# For development, use .NET Aspire AppHost instead.

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  postgres:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: openeye
      POSTGRES_USER: openeye
      POSTGRES_PASSWORD: openeye
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql

  roboflow-inference:
    image: roboflow/roboflow-inference-server-cpu:latest
    ports:
      - "9001:9001"
    environment:
      ROBOFLOW_API_KEY: ${ROBOFLOW_API_KEY:-}

  frame-capture:
    build:
      context: ../src
      dockerfile: ../docker/Dockerfile.dotnet
      args:
        PROJECT: OpenEye.FrameCapture
    depends_on:
      - redis
    environment:
      ConnectionStrings__redis: redis:6379

  detection-bridge:
    build:
      context: ../src
      dockerfile: ../docker/Dockerfile.dotnet
      args:
        PROJECT: OpenEye.DetectionBridge
    depends_on:
      - redis
      - roboflow-inference
    environment:
      ConnectionStrings__redis: redis:6379
      Roboflow__Url: http://roboflow-inference:9001

  pipeline-core:
    build:
      context: ../src
      dockerfile: ../docker/Dockerfile.dotnet
      args:
        PROJECT: OpenEye.PipelineCore
    depends_on:
      - redis
      - postgres
    environment:
      ConnectionStrings__redis: redis:6379
      ConnectionStrings__openeye: Host=postgres;Database=openeye;Username=openeye;Password=openeye

  event-router:
    build:
      context: ../src
      dockerfile: ../docker/Dockerfile.dotnet
      args:
        PROJECT: OpenEye.EventRouter
    depends_on:
      - redis
      - postgres
    environment:
      ConnectionStrings__redis: redis:6379
      ConnectionStrings__openeye: Host=postgres;Database=openeye;Username=openeye;Password=openeye

  dashboard:
    build:
      context: ../dashboard
      dockerfile: ../docker/Dockerfile.dashboard
    ports:
      - "3000:3000"
    depends_on:
      - postgres
      - redis
    environment:
      DATABASE_URL: postgresql://openeye:openeye@postgres:5432/openeye
      REDIS_URL: redis://redis:6379

volumes:
  redis-data:
  pgdata:
```

- [ ] **Step 2: Create Dockerfile.dotnet (multi-stage build)**

```dockerfile
# docker/Dockerfile.dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet"]
CMD ["*.dll"]
```

Note: The `CMD` needs to resolve the actual DLL name. A better approach:

```dockerfile
# docker/Dockerfile.dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
ARG PROJECT
ENV DOTNET_DLL="${PROJECT}.dll"
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT dotnet $DOTNET_DLL
```

- [ ] **Step 3: Create Dockerfile.dashboard**

```dockerfile
# docker/Dockerfile.dashboard
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npx prisma generate && npm run build

FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/prisma ./prisma
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

- [ ] **Step 4: Commit**

```bash
git add docker/
git commit -m "feat: add Docker Compose definitions for all application services with Dockerfiles"
```

---

### Task 7: Evidence Request Handling in EventRouter

**Files:**
- Modify: `src/OpenEye.EventRouter/Worker.cs`

The design doc specifies: "When an event includes an `EvidenceRequest`, event-router calls frame-capture to retrieve the frame/clip from its rolling buffer. Evidence files stored on shared volume, reference saved in PostgreSQL. Evidence URL attached to notifications."

For now, we implement the evidence request detection and logging, since the frame-capture evidence provider is not yet implemented (depends on shared volume architecture). This lays the groundwork.

- [ ] **Step 1: Read current EventRouter Worker.cs**

Read `src/OpenEye.EventRouter/Worker.cs` to understand the current message loop.

- [ ] **Step 2: Add evidence request handling to EventRouter**

After persisting the event and dispatching notifications, check if the event metadata contains an evidence request and log it. The `IEvidenceProvider` interface already exists in Abstractions.

In `src/OpenEye.EventRouter/Worker.cs`, add evidence detection logic:

```csharp
// After dispatching notifications in the message loop, add:

// Check for evidence request in event metadata
if (evt.Metadata?.TryGetValue("evidenceRequest", out var evidenceRequestObj) == true)
{
    try
    {
        var evidenceJson = JsonSerializer.Serialize(evidenceRequestObj);
        var evidenceRequest = JsonSerializer.Deserialize<EvidenceRequest>(evidenceJson);
        if (evidenceRequest is not null)
        {
            logger.LogInformation(
                "Evidence requested for event {EventId}: {Type} from {From} to {To}",
                evt.EventId, evidenceRequest.Type, evidenceRequest.From, evidenceRequest.To);
            // TODO: Call IEvidenceProvider.CaptureEvidenceAsync when frame-capture supports shared volume
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to process evidence request for event {EventId}", evt.EventId);
    }
}
```

- [ ] **Step 3: Add `using OpenEye.Shared.Models;`** if not already present (for `EvidenceRequest` type).

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/OpenEye.EventRouter`
Expected: Build succeeded

- [ ] **Step 5: Run all .NET tests**

Run: `dotnet test src/OpenEye.Tests --nologo`
Expected: All 82+ tests pass

- [ ] **Step 6: Commit**

```bash
git add src/OpenEye.EventRouter/Worker.cs
git commit -m "feat: add evidence request detection and logging in EventRouter"
```

---

### Task 8: Wire Dashboard Redis in Aspire AppHost

**Files:**
- Modify: `src/OpenEye.AppHost/AppHost.cs`

The dashboard uses Redis (via `dashboard/src/lib/redis.ts`) to publish `config:changed` notifications, but Aspire doesn't pass the Redis connection to the dashboard container.

- [ ] **Step 1: Read current AppHost.cs**

Read `src/OpenEye.AppHost/AppHost.cs` to see current wiring.

- [ ] **Step 2: Add Redis reference to dashboard**

In `src/OpenEye.AppHost/AppHost.cs`, add `.WithReference(redis)` to the dashboard builder:

```csharp
builder.AddNpmApp("dashboard", "../../dashboard", "dev")
    .WithReference(postgres)
    .WithReference(redis)           // ← ADD THIS LINE
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(postgres);
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/OpenEye.AppHost`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/OpenEye.AppHost/AppHost.cs
git commit -m "feat: wire Redis reference to dashboard in Aspire AppHost"
```

---

## Verification

After all tasks are complete:

- [ ] **Run full .NET build:** `dotnet build src/OpenEye.slnx` — expect 0 errors
- [ ] **Run all .NET tests:** `dotnet test src/OpenEye.Tests --nologo` — expect 82+ pass
- [ ] **Run dashboard type check:** `cd dashboard && npx tsc --noEmit` — expect 0 errors
