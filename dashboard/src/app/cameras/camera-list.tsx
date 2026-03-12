"use client";

import { useState, useMemo } from "react";
import dynamic from "next/dynamic";
import useSWR from "swr";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
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

const CameraFormDialog = dynamic(
  () => import("@/components/cameras/camera-form-dialog").then(m => m.CameraFormDialog),
  { ssr: false }
);

import { MoreHorizontal, Pencil, Trash2, Power } from "lucide-react";
import { toast } from "sonner";

interface Camera {
  id: string;
  name: string;
  url: string;
  targetFps: number;
  enabled: boolean;
  type: string;
  createdAt: string;
  updatedAt: string;
  zones: { id: string; name: string }[];
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());

const truncateUrl = (url: string, max = 45) =>
  url.length > max ? url.slice(0, max) + "…" : url;

export function CameraList({ initialCameras }: { initialCameras: Camera[] }) {
  const { data: cameras, mutate } = useSWR<Camera[]>("/api/cameras", fetcher, {
    fallbackData: initialCameras,
  });

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingCamera, setEditingCamera] = useState<Camera | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Camera | null>(null);

  const handleSave = async (form: { name: string; url: string; targetFps: number; enabled: boolean }) => {
    const method = editingCamera ? "PUT" : "POST";
    const url = editingCamera ? `/api/cameras/${editingCamera.id}` : "/api/cameras";

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });

    if (!res.ok) {
      toast.error(editingCamera ? "Failed to update camera" : "Failed to create camera");
      return;
    }

    toast.success(editingCamera ? "Camera updated" : "Camera created");
    setEditingCamera(null);
    mutate();
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await fetch(`/api/cameras/${deleteTarget.id}`, { method: "DELETE" });
    if (res.ok) {
      toast.success("Camera deleted");
      mutate();
    } else {
      toast.error("Failed to delete camera");
    }
    setDeleteTarget(null);
  };

  const handleToggle = async (camera: Camera) => {
    const res = await fetch(`/api/cameras/${camera.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ enabled: !camera.enabled }),
    });
    if (res.ok) {
      const updated = await res.json();
      toast.success(updated.enabled ? "Camera enabled" : "Camera disabled");
      mutate();
    }
  };

  const openCreate = () => {
    setEditingCamera(null);
    setDialogOpen(true);
  };

  const openEdit = (camera: Camera) => {
    setEditingCamera(camera);
    setDialogOpen(true);
  };

  const cameraList = cameras ?? [];

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title="Cameras"
        description="Manage camera streams for video analytics."
        action={{ label: "Add Camera", onClick: openCreate }}
      />

      {cameraList.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">No cameras yet. Add one to get started.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {cameraList.map((camera) => (
            <Card key={camera.id}>
              <CardContent className="flex items-center gap-4 py-4">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-medium">{camera.name}</h3>
                    <Badge variant={camera.enabled ? "default" : "secondary"}>
                      {camera.enabled ? "Active" : "Disabled"}
                    </Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground truncate">
                    {truncateUrl(camera.url)} · {camera.targetFps} FPS · {camera.zones?.length ?? 0} zone(s)
                  </p>
                </div>
                <DropdownMenu>
                  <DropdownMenuTrigger className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-sm hover:bg-muted" aria-label="Camera actions">
                    <MoreHorizontal className="h-4 w-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => openEdit(camera)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => handleToggle(camera)}>
                      <Power className="mr-2 h-4 w-4" />
                      {camera.enabled ? "Disable" : "Enable"}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      onClick={() => setDeleteTarget(camera)}
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

      <CameraFormDialog
        key={editingCamera?.id ?? "new"}
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) setEditingCamera(null);
        }}
        editingId={editingCamera?.id}
        initialData={
          editingCamera
            ? {
                name: editingCamera.name,
                url: editingCamera.url,
                targetFps: editingCamera.targetFps,
                enabled: editingCamera.enabled,
              }
            : undefined
        }
        onSave={handleSave}
      />

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Camera</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete &quot;{deleteTarget?.name}&quot; and all its associated zones. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>Delete Camera</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
