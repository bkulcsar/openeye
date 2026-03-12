import { Suspense } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getCameras } from "@/lib/data";
import { CameraList } from "./camera-list";

export const dynamic = "force-dynamic";

function CamerasLoading() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-1">
        <Skeleton className="h-7 w-32" />
        <Skeleton className="h-4 w-64" />
      </div>
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Card key={i}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex-1 space-y-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-64" />
              </div>
              <Skeleton className="h-8 w-8" />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

async function CamerasContent() {
  const cameras = await getCameras();
  const serialized = cameras.map((c) => ({
    ...c,
    createdAt: c.createdAt.toISOString(),
    updatedAt: c.updatedAt.toISOString(),
    zones: c.zones.map((z) => ({ id: z.id, name: z.name })),
  }));
  return <CameraList initialCameras={serialized} />;
}

export default function CamerasPage() {
  return (
    <Suspense fallback={<CamerasLoading />}>
      <CamerasContent />
    </Suspense>
  );
}
