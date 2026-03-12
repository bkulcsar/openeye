"use client";

import { useState } from "react";
import { RuleFormData, ConditionConfig } from "./types";
import { RuleCanvas } from "./RuleCanvas";
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
import { Loader2 } from "lucide-react";

interface RuleBuilderDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  cameraId: string;
  initialData?: Partial<RuleFormData>;
  onSave: (data: RuleFormData) => Promise<void>;
}

export function RuleBuilderDialog({ open, onOpenChange, cameraId, initialData, onSave }: RuleBuilderDialogProps) {
  const [name, setName] = useState(initialData?.name ?? "");
  const [objectClass, setObjectClass] = useState(initialData?.objectClass ?? "person");
  const [zoneId, setZoneId] = useState(initialData?.zoneId ?? "");
  const [conditions, setConditions] = useState<ConditionConfig[]>(initialData?.conditions ?? []);
  const [logic, setLogic] = useState<"all" | "any">(initialData?.logic ?? "all");
  const [cooldown, setCooldown] = useState(initialData?.cooldown ?? 30);
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave({
        name,
        cameraId,
        objectClass,
        zoneId: zoneId || undefined,
        conditions,
        logic,
        cooldown,
        enabled: true,
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{initialData ? "Edit Rule" : "Create Rule"}</DialogTitle>
          <DialogDescription>
            Configure conditions that trigger events when matched.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-2 gap-4 py-4">
          <div className="grid gap-2">
            <Label htmlFor="rule-name">Rule Name</Label>
            <Input
              id="rule-name"
              name="ruleName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Loitering Alert…"
              autoComplete="off"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="rule-class">Object Class</Label>
            <Input
              id="rule-class"
              name="objectClass"
              value={objectClass}
              onChange={(e) => setObjectClass(e.target.value)}
              placeholder="person…"
              autoComplete="off"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="rule-zone">Zone ID (optional)</Label>
            <Input
              id="rule-zone"
              name="zoneId"
              value={zoneId}
              onChange={(e) => setZoneId(e.target.value)}
              placeholder="Zone ID…"
              autoComplete="off"
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="rule-cooldown">Cooldown (seconds)</Label>
            <Input
              id="rule-cooldown"
              name="cooldown"
              type="number"
              value={cooldown}
              onChange={(e) => setCooldown(Number(e.target.value))}
              min={0}
              autoComplete="off"
            />
          </div>
        </div>

        <div>
          <h3 className="text-sm font-semibold mb-3">Conditions</h3>
          <RuleCanvas
            conditions={conditions}
            logic={logic}
            onConditionsChange={setConditions}
            onLogicChange={setLogic}
          />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button
            onClick={handleSave}
            disabled={!name || conditions.length === 0 || saving}
          >
            {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {initialData ? "Save Rule" : "Create Rule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
