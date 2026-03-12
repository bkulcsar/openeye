"use client";

import { useEffect, useState } from "react";
import { Camera, MapPin, Cog, Activity, ArrowRight } from "lucide-react";
import Link from "next/link";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";

interface Stats {
  cameras: number;
  zones: number;
  rules: number;
  totalEvents: number;
}

interface RecentEvent {
  id: string;
  eventType: string;
  timestamp: string;
  sourceId: string;
  ruleId: string;
}

const statDefs = [
  { key: "cameras" as const, label: "Cameras", icon: Camera, href: "/cameras" },
  { key: "zones" as const, label: "Zones", icon: MapPin, href: "/zones" },
  { key: "rules" as const, label: "Rules", icon: Cog, href: "/rules" },
  { key: "totalEvents" as const, label: "Total Events", icon: Activity, href: "/events" },
];

const formatTimestamp = (ts: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(ts));

export default function Home() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [recentEvents, setRecentEvents] = useState<RecentEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetch("/api/cameras").then((r) => r.json()),
      fetch("/api/rules").then((r) => r.json()),
      fetch("/api/zones").then((r) => r.json()),
      fetch("/api/events?limit=5").then((r) => r.json()),
    ])
      .then(([cameras, rules, zones, eventsData]) => {
        setStats({
          cameras: cameras.length,
          rules: rules.length,
          zones: zones.length,
          totalEvents: eventsData.total ?? 0,
        });
        setRecentEvents(eventsData.events ?? []);
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-wrap-balance">Dashboard</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Video analytics monitoring and configuration overview.
        </p>
      </div>

      {/* Stats Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {loading
          ? Array.from({ length: 4 }).map((_, i) => (
              <Card key={i}>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <Skeleton className="h-4 w-20" />
                  <Skeleton className="h-4 w-4" />
                </CardHeader>
                <CardContent>
                  <Skeleton className="h-8 w-12" />
                </CardContent>
              </Card>
            ))
          : statDefs.map((s) => (
              <Link key={s.key} href={s.href}>
                <Card className="transition-colors hover:bg-accent/50">
                  <CardHeader className="flex flex-row items-center justify-between pb-2">
                    <CardDescription>{s.label}</CardDescription>
                    <s.icon className="h-4 w-4 text-muted-foreground" />
                  </CardHeader>
                  <CardContent>
                    <p className="text-3xl font-bold tabular-nums">
                      {stats?.[s.key] ?? 0}
                    </p>
                  </CardContent>
                </Card>
              </Link>
            ))}
      </div>

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
          {loading ? (
            <div className="space-y-3">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="flex items-center gap-4">
                  <Skeleton className="h-5 w-24" />
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-4 w-20 ml-auto" />
                </div>
              ))}
            </div>
          ) : recentEvents.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">
              No events yet. Configure cameras and rules to start monitoring.
            </p>
          ) : (
            <div className="space-y-3">
              {recentEvents.map((event) => (
                <div
                  key={event.id}
                  className="flex items-center gap-4 text-sm"
                >
                  <Badge variant="secondary">{event.eventType}</Badge>
                  <span className="text-muted-foreground">{event.sourceId}</span>
                  <span className="ml-auto text-xs text-muted-foreground tabular-nums">
                    {formatTimestamp(event.timestamp)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
