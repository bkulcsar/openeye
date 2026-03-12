"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Camera, MapPin, Scale, Activity } from "lucide-react";
import { toast } from "sonner";

interface SystemHealth {
  cameras: number;
  rules: number;
  zones: number;
  events: number;
}

const stats = [
  { key: "cameras" as const, label: "Cameras", icon: Camera },
  { key: "zones" as const, label: "Zones", icon: MapPin },
  { key: "rules" as const, label: "Rules", icon: Scale },
  { key: "events" as const, label: "Total Events", icon: Activity },
];

export default function SettingsPage() {
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetch("/api/cameras").then((r) => r.json()),
      fetch("/api/rules").then((r) => r.json()),
      fetch("/api/zones").then((r) => r.json()),
      fetch("/api/events?limit=1").then((r) => r.json()),
    ])
      .then(([cameras, rules, zones, eventsData]) => {
        setHealth({
          cameras: cameras.length,
          rules: rules.length,
          zones: zones.length,
          events: eventsData.total ?? 0,
        });
      })
      .catch(() => toast.error("Failed to load system stats"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <PageHeader title="Settings" description="System overview and configuration reference." />

      {/* System Overview */}
      <section>
        <h2 className="mb-3 text-lg font-semibold">System Overview</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {loading
            ? Array.from({ length: 4 }).map((_, i) => (
                <Card key={i}>
                  <CardContent className="flex items-center gap-4 py-5">
                    <Skeleton className="h-10 w-10 rounded-md" />
                    <div className="space-y-1.5">
                      <Skeleton className="h-6 w-12" />
                      <Skeleton className="h-4 w-16" />
                    </div>
                  </CardContent>
                </Card>
              ))
            : stats.map((s) => {
                const Icon = s.icon;
                return (
                  <Card key={s.key}>
                    <CardContent className="flex items-center gap-4 py-5">
                      <div className="flex h-10 w-10 items-center justify-center rounded-md bg-primary/10">
                        <Icon className="h-5 w-5 text-primary" />
                      </div>
                      <div>
                        <p className="text-2xl font-bold tabular-nums">{health?.[s.key] ?? 0}</p>
                        <p className="text-sm text-muted-foreground">{s.label}</p>
                      </div>
                    </CardContent>
                  </Card>
                );
              })}
        </div>
      </section>

      <Separator />

      {/* Detection Model Config */}
      <section>
        <Card>
          <CardHeader>
            <CardTitle>Detection Model Configuration</CardTitle>
            <p className="text-sm text-muted-foreground">
              These settings are configured via environment variables or appsettings.json on the backend services.
            </p>
          </CardHeader>
          <CardContent className="divide-y">
            {[
              { label: "Inference URL", value: "ROBOFLOW__URL" },
              { label: "Model ID", value: "ROBOFLOW__MODELID" },
              { label: "Confidence Threshold", value: "ROBOFLOW__CONFIDENCETHRESHOLD" },
              { label: "API Key", value: "ROBOFLOW__APIKEY" },
            ].map((item) => (
              <div key={item.label} className="flex items-center justify-between py-3 first:pt-0 last:pb-0">
                <span className="text-sm text-muted-foreground">{item.label}</span>
                <Badge variant="secondary">
                  <code className="text-xs">{item.value}</code>
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </section>

      {/* Infrastructure */}
      <section>
        <Card>
          <CardHeader>
            <CardTitle>Infrastructure</CardTitle>
          </CardHeader>
          <CardContent className="divide-y">
            {[
              { label: "Database", value: "PostgreSQL (Prisma)" },
              { label: "Message Bus", value: "Redis Streams" },
              { label: "Orchestration", value: ".NET Aspire" },
            ].map((item) => (
              <div key={item.label} className="flex items-center justify-between py-3 first:pt-0 last:pb-0">
                <span className="text-sm text-muted-foreground">{item.label}</span>
                <span className="text-sm">{item.value}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
