import { Suspense } from "react";
import { Camera, MapPin, Cog, Activity, ArrowRight } from "lucide-react";
import Link from "next/link";

export const dynamic = "force-dynamic";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { buttonVariants } from "@/components/ui/button-variants";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { getDashboardCounts, getRecentEvents } from "@/lib/data";

const statDefs = [
  { key: "cameras" as const, label: "Cameras", icon: Camera, href: "/cameras" },
  { key: "zones" as const, label: "Zones", icon: MapPin, href: "/zones" },
  { key: "rules" as const, label: "Rules", icon: Cog, href: "/rules" },
  { key: "totalEvents" as const, label: "Total Events", icon: Activity, href: "/events" },
];

const fmt = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

function StatsLoading() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <Card key={i}>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <Skeleton className="h-4 w-20" />
            <Skeleton className="h-4 w-4" />
          </CardHeader>
          <CardContent>
            <Skeleton className="h-8 w-12" />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

async function StatsCards() {
  const [cameraCount, ruleCount, zoneCount, eventCount] = await getDashboardCounts();
  const counts = { cameras: cameraCount, zones: zoneCount, rules: ruleCount, totalEvents: eventCount };

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {statDefs.map((s) => (
        <Link key={s.key} href={s.href}>
          <Card className="transition-colors hover:bg-accent/50">
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardDescription>{s.label}</CardDescription>
              <s.icon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <p className="text-3xl font-bold tabular-nums">
                {counts[s.key]}
              </p>
            </CardContent>
          </Card>
        </Link>
      ))}
    </div>
  );
}

function EventsLoading() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <div key={i} className="flex items-center gap-4">
          <Skeleton className="h-5 w-24" />
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-20 ml-auto" />
        </div>
      ))}
    </div>
  );
}

async function RecentEventsList() {
  const events = await getRecentEvents(5);

  if (events.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        No events yet. Configure cameras and rules to start monitoring.
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {events.map((event) => (
        <div key={event.id} className="flex items-center gap-4 text-sm">
          <Badge variant="secondary">{event.eventType}</Badge>
          <span className="text-muted-foreground">{event.sourceId}</span>
          <span className="ml-auto text-xs text-muted-foreground tabular-nums">
            {fmt.format(new Date(event.timestamp))}
          </span>
        </div>
      ))}
    </div>
  );
}

export default function Home() {
  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-wrap-balance">Dashboard</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Video analytics monitoring and configuration overview.
        </p>
      </div>

      <Suspense fallback={<StatsLoading />}>
        <StatsCards />
      </Suspense>

      {/* Quick Actions */}
      <div className="flex flex-wrap gap-3">
        <Link href="/cameras" className={buttonVariants()}>
          <Camera className="mr-2 h-4 w-4" />
          Add Camera
        </Link>
        <Link href="/rules" className={buttonVariants({ variant: "outline" })}>
          <Cog className="mr-2 h-4 w-4" />
          Create Rule
        </Link>
      </div>

      {/* Recent Events */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Recent Events</CardTitle>
            <CardDescription>Last 5 events across all cameras</CardDescription>
          </div>
          <Link href="/events" className={buttonVariants({ variant: "ghost", size: "sm" })}>
            View All
            <ArrowRight className="ml-1 h-4 w-4" />
          </Link>
        </CardHeader>
        <CardContent>
          <Suspense fallback={<EventsLoading />}>
            <RecentEventsList />
          </Suspense>
        </CardContent>
      </Card>
    </div>
  );
}
