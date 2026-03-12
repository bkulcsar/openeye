"use client";

import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Loader2 } from "lucide-react";

interface CameraForm {
  name: string;
  url: string;
  targetFps: number;
  enabled: boolean;
}

interface CameraFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialData?: CameraForm;
  editingId?: string | null;
  onSave: (data: CameraForm) => Promise<void>;
}

const emptyForm: CameraForm = { name: "", url: "", targetFps: 5, enabled: true };

export function CameraFormDialog({
  open,
  onOpenChange,
  initialData,
  editingId,
  onSave,
}: CameraFormDialogProps) {
  const [form, setForm] = useState<CameraForm>(initialData ?? emptyForm);
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave(form);
      onOpenChange(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{editingId ? "Edit Camera" : "Add Camera"}</DialogTitle>
          <DialogDescription>
            {editingId
              ? "Update the camera configuration."
              : "Configure a new camera stream."}
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-4">
          <div className="grid gap-2">
            <Label htmlFor="camera-name">Name</Label>
            <Input
              id="camera-name"
              name="name"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              placeholder="Front Door Camera…"
              autoComplete="off"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="camera-url">Stream URL</Label>
            <Input
              id="camera-url"
              name="url"
              value={form.url}
              onChange={(e) => setForm({ ...form, url: e.target.value })}
              placeholder="rtsp://192.168.1.100:554/stream…"
              autoComplete="off"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="camera-fps">Target FPS</Label>
            <Input
              id="camera-fps"
              name="targetFps"
              type="number"
              value={form.targetFps}
              onChange={(e) => setForm({ ...form, targetFps: Number(e.target.value) })}
              min={1}
              max={30}
              autoComplete="off"
            />
          </div>
          <div className="flex items-center gap-3">
            <Switch
              id="camera-enabled"
              checked={form.enabled}
              onCheckedChange={(checked) => setForm({ ...form, enabled: checked })}
            />
            <Label htmlFor="camera-enabled">Enabled</Label>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={!form.name || !form.url || saving}
          >
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {editingId ? "Save Camera" : "Add Camera"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
