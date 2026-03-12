"use client";

import { useState, useMemo } from "react";
import useSWR from "swr";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Separator } from "@/components/ui/separator";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { MoreHorizontal, Trash2, Loader2 } from "lucide-react";
import { toast } from "sonner";

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

interface Camera {
  id: string;
  name: string;
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());

export function ZoneList({
  initialZones,
  initialTripwires,
  initialCameras,
}: {
  initialZones: Zone[];
  initialTripwires: Tripwire[];
  initialCameras: Camera[];
}) {
  const { data: zones, mutate: mutateZones } = useSWR<Zone[]>("/api/zones", fetcher, { fallbackData: initialZones });
  const { data: tripwires, mutate: mutateTripwires } = useSWR<Tripwire[]>("/api/tripwires", fetcher, { fallbackData: initialTripwires });
  const { data: cameras } = useSWR<Camera[]>("/api/cameras", fetcher, { fallbackData: initialCameras });

  // Zone dialog
  const [zoneDialogOpen, setZoneDialogOpen] = useState(false);
  const [zoneName, setZoneName] = useState("");
  const [zoneCameraId, setZoneCameraId] = useState("");
  const [zonePolygon, setZonePolygon] = useState("");
  const [zoneSaving, setZoneSaving] = useState(false);

  // Tripwire dialog
  const [tripwireDialogOpen, setTripwireDialogOpen] = useState(false);
  const [tripwireSourceId, setTripwireSourceId] = useState("");
  const [tripwireCoords, setTripwireCoords] = useState({ startX: 0, startY: 0, endX: 1, endY: 1 });
  const [tripwireSaving, setTripwireSaving] = useState(false);

  // Delete targets
  const [deleteZone, setDeleteZone] = useState<Zone | null>(null);
  const [deleteTripwire, setDeleteTripwire] = useState<Tripwire | null>(null);

  const cameraById = useMemo(
    () => new Map((cameras ?? []).map((c) => [c.id, c.name])),
    [cameras]
  );
  const cameraName = (id: string) => cameraById.get(id) ?? id;

  const handleAddZone = async () => {
    let polygon;
    try {
      polygon = JSON.parse(zonePolygon);
    } catch {
      toast.error("Invalid polygon JSON");
      return;
    }
    setZoneSaving(true);
    const res = await fetch("/api/zones", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: zoneName, cameraId: zoneCameraId, polygon }),
    });
    if (res.ok) {
      setZoneDialogOpen(false);
      setZoneName("");
      setZoneCameraId("");
      setZonePolygon("");
      toast.success("Zone created");
      mutateZones();
    } else {
      toast.error("Failed to create zone");
    }
    setZoneSaving(false);
  };

  const handleDeleteZone = async () => {
    if (!deleteZone) return;
    const res = await fetch(`/api/zones/${deleteZone.id}`, { method: "DELETE" });
    if (res.ok) {
      toast.success("Zone deleted");
      mutateZones();
    } else {
      toast.error("Failed to delete zone");
    }
    setDeleteZone(null);
  };

  const handleAddTripwire = async () => {
    setTripwireSaving(true);
    const res = await fetch("/api/tripwires", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sourceId: tripwireSourceId, ...tripwireCoords }),
    });
    if (res.ok) {
      setTripwireDialogOpen(false);
      setTripwireSourceId("");
      setTripwireCoords({ startX: 0, startY: 0, endX: 1, endY: 1 });
      toast.success("Tripwire created");
      mutateTripwires();
    } else {
      toast.error("Failed to create tripwire");
    }
    setTripwireSaving(false);
  };

  const handleDeleteTripwire = async () => {
    if (!deleteTripwire) return;
    const res = await fetch(`/api/tripwires/${deleteTripwire.id}`, { method: "DELETE" });
    if (res.ok) {
      toast.success("Tripwire deleted");
      mutateTripwires();
    } else {
      toast.error("Failed to delete tripwire");
    }
    setDeleteTripwire(null);
  };

  const zoneList = zones ?? [];
  const tripwireList = tripwires ?? [];
  const cameraList = cameras ?? [];

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      {/* Zones Section */}
      <div className="space-y-4">
        <PageHeader
          title="Zones"
          description="Define detection zones on camera feeds."
          action={{ label: "Add Zone", onClick: () => setZoneDialogOpen(true) }}
        />

        {zoneList.length === 0 ? (
          <Card>
            <CardContent className="py-8 text-center">
              <p className="text-muted-foreground">No zones configured.</p>
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-3">
            {zoneList.map((zone) => (
              <Card key={zone.id}>
                <CardContent className="flex items-center gap-4 py-4">
                  <div className="min-w-0 flex-1">
                    <h3 className="font-medium">{zone.name}</h3>
                    <p className="text-sm text-muted-foreground">
                      Camera: {cameraName(zone.cameraId)} · Type: <Badge variant="secondary">{zone.type}</Badge>
                    </p>
                  </div>
                  <DropdownMenu>
                    <DropdownMenuTrigger className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-sm hover:bg-muted" aria-label="Zone actions">
                      <MoreHorizontal className="h-4 w-4" />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem
                        className="text-destructive focus:text-destructive"
                        onClick={() => setDeleteZone(zone)}
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      <Separator />

      {/* Tripwires Section */}
      <div className="space-y-4">
        <PageHeader
          title="Tripwires"
          description="Define line-crossing detection boundaries."
          action={{ label: "Add Tripwire", onClick: () => setTripwireDialogOpen(true) }}
        />

        {tripwireList.length === 0 ? (
          <Card>
            <CardContent className="py-8 text-center">
              <p className="text-muted-foreground">No tripwires configured.</p>
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-3">
            {tripwireList.map((tw) => (
              <Card key={tw.id}>
                <CardContent className="flex items-center gap-4 py-4">
                  <div className="min-w-0 flex-1">
                    <h3 className="font-medium text-sm font-mono">{tw.id}</h3>
                    <p className="text-sm text-muted-foreground">
                      Camera: {cameraName(tw.sourceId)} · ({tw.startX.toFixed(2)}, {tw.startY.toFixed(2)}) → ({tw.endX.toFixed(2)}, {tw.endY.toFixed(2)})
                    </p>
                  </div>
                  <DropdownMenu>
                    <DropdownMenuTrigger className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-sm hover:bg-muted" aria-label="Tripwire actions">
                      <MoreHorizontal className="h-4 w-4" />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem
                        className="text-destructive focus:text-destructive"
                        onClick={() => setDeleteTripwire(tw)}
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Zone Dialog */}
      <Dialog open={zoneDialogOpen} onOpenChange={setZoneDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Add Zone</DialogTitle>
            <DialogDescription>Define a new detection zone on a camera feed.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="zone-name">Zone Name</Label>
              <Input
                id="zone-name"
                name="zoneName"
                value={zoneName}
                onChange={(e) => setZoneName(e.target.value)}
                placeholder="Checkout area…"
                autoComplete="off"
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="zone-camera">Camera</Label>
              <select
                id="zone-camera"
                value={zoneCameraId}
                onChange={(e) => setZoneCameraId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Select camera…</option>
                {cameraList.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="zone-polygon">Polygon (JSON)</Label>
              <Textarea
                id="zone-polygon"
                name="zonePolygon"
                value={zonePolygon}
                onChange={(e) => setZonePolygon(e.target.value)}
                placeholder='[{"x":0.2,"y":0.2},{"x":0.8,"y":0.2},{"x":0.8,"y":0.8}]…'
                className="font-mono text-sm"
                rows={3}
                autoComplete="off"
                spellCheck={false}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setZoneDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleAddZone} disabled={!zoneName || !zoneCameraId || !zonePolygon || zoneSaving}>
              {zoneSaving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add Zone
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Tripwire Dialog */}
      <Dialog open={tripwireDialogOpen} onOpenChange={setTripwireDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Add Tripwire</DialogTitle>
            <DialogDescription>Define a line-crossing boundary between two points.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="tw-camera">Camera</Label>
              <select
                id="tw-camera"
                value={tripwireSourceId}
                onChange={(e) => setTripwireSourceId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Select camera…</option>
                {cameraList.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="tw-sx">Start X</Label>
                <Input id="tw-sx" type="number" step="0.01" min="0" max="1" value={tripwireCoords.startX}
                  onChange={(e) => setTripwireCoords(prev => ({ ...prev, startX: parseFloat(e.target.value) }))} />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="tw-sy">Start Y</Label>
                <Input id="tw-sy" type="number" step="0.01" min="0" max="1" value={tripwireCoords.startY}
                  onChange={(e) => setTripwireCoords(prev => ({ ...prev, startY: parseFloat(e.target.value) }))} />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="tw-ex">End X</Label>
                <Input id="tw-ex" type="number" step="0.01" min="0" max="1" value={tripwireCoords.endX}
                  onChange={(e) => setTripwireCoords(prev => ({ ...prev, endX: parseFloat(e.target.value) }))} />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="tw-ey">End Y</Label>
                <Input id="tw-ey" type="number" step="0.01" min="0" max="1" value={tripwireCoords.endY}
                  onChange={(e) => setTripwireCoords(prev => ({ ...prev, endY: parseFloat(e.target.value) }))} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTripwireDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleAddTripwire} disabled={!tripwireSourceId || tripwireSaving}>
              {tripwireSaving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add Tripwire
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Zone Confirm */}
      <AlertDialog open={!!deleteZone} onOpenChange={(open) => !open && setDeleteZone(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Zone</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete &quot;{deleteZone?.name}&quot;. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDeleteZone}>Delete Zone</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete Tripwire Confirm */}
      <AlertDialog open={!!deleteTripwire} onOpenChange={(open) => !open && setDeleteTripwire(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Tripwire</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete this tripwire. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDeleteTripwire}>Delete Tripwire</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
