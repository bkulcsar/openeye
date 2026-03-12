import { Suspense } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";

export const dynamic = "force-dynamic";
import { Skeleton } from "@/components/ui/skeleton";
import { Camera, MapPin, Scale, Activity } from "lucide-react";
import { getDashboardCounts } from "@/lib/data";

const stats = [
  { key: "cameras" as const, label: "Cameras", icon: Camera },
  { key: "zones" as const, label: "Zones", icon: MapPin },
  { key: "rules" as const, label: "Rules", icon: Scale },
  { key: "events" as const, label: "Total Events", icon: Activity },
];

function OverviewLoading() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <Card key={i}>
          <CardContent className="flex items-center gap-4 py-5">
            <Skeleton className="h-10 w-10 rounded-md" />
            <div className="space-y-1.5">
              <Skeleton className="h-6 w-12" />
              <Skeleton className="h-4 w-16" />
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

async function SystemOverview() {
  const [cameraCount, ruleCount, zoneCount, eventCount] = await getDashboardCounts();
  const counts = { cameras: cameraCount, zones: zoneCount, rules: ruleCount, events: eventCount };

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {stats.map((s) => {
        const Icon = s.icon;
        return (
          <Card key={s.key}>
            <CardContent className="flex items-center gap-4 py-5">
              <div className="flex h-10 w-10 items-center justify-center rounded-md bg-primary/10">
                <Icon className="h-5 w-5 text-primary" />
              </div>
              <div>
                <p className="text-2xl font-bold tabular-nums">{counts[s.key]}</p>
                <p className="text-sm text-muted-foreground">{s.label}</p>
              </div>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}

export default function SettingsPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <PageHeader title="Settings" description="System overview and configuration reference." />

      {/* System Overview */}
      <section>
        <h2 className="mb-3 text-lg font-semibold">System Overview</h2>
        <Suspense fallback={<OverviewLoading />}>
          <SystemOverview />
        </Suspense>
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
