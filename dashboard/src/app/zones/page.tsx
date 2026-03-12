import { Suspense } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { getZones, getTripwires, getCamerasMinimal } from "@/lib/data";

export const dynamic = "force-dynamic";
import { ZoneList } from "./zone-list";

function ZonesLoading() {
  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <div className="space-y-4">
        <div className="space-y-1">
          <Skeleton className="h-7 w-24" />
          <Skeleton className="h-4 w-56" />
        </div>
        <div className="space-y-3">
          {Array.from({ length: 2 }).map((_, i) => (
            <Card key={i}>
              <CardContent className="flex items-center gap-4 py-4">
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-5 w-32" />
                  <Skeleton className="h-4 w-48" />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
      <Separator />
      <div className="space-y-4">
        <div className="space-y-1">
          <Skeleton className="h-7 w-28" />
          <Skeleton className="h-4 w-56" />
        </div>
        <div className="space-y-3">
          {Array.from({ length: 2 }).map((_, i) => (
            <Card key={i}>
              <CardContent className="flex items-center gap-4 py-4">
                <div className="flex-1 space-y-2">
                  <Skeleton className="h-5 w-40" />
                  <Skeleton className="h-4 w-56" />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}

async function ZonesContent() {
  const [zones, tripwires, cameras] = await Promise.all([
    getZones(),
    getTripwires(),
    getCamerasMinimal(),
  ]);

  const serializedZones = zones.map((z) => ({
    id: z.id,
    name: z.name,
    cameraId: z.cameraId,
    polygon: z.polygon,
    type: z.type,
  }));

  const serializedTripwires = tripwires.map((t) => ({
    id: t.id,
    sourceId: t.sourceId,
    startX: Number(t.startX),
    startY: Number(t.startY),
    endX: Number(t.endX),
    endY: Number(t.endY),
  }));

  return (
    <ZoneList
      initialZones={serializedZones}
      initialTripwires={serializedTripwires}
      initialCameras={cameras}
    />
  );
}

export default function ZonesPage() {
  return (
    <Suspense fallback={<ZonesLoading />}>
      <ZonesContent />
    </Suspense>
  );
}
