"use client";

import { useState } from "react";
import { RuleFormData, ConditionConfig } from "./types";
import { RuleCanvas } from "./RuleCanvas";

interface RuleBuilderDialogProps {
  cameraId: string;
  initialData?: Partial<RuleFormData>;
  onSave: (data: RuleFormData) => void;
  onCancel: () => void;
}

export function RuleBuilderDialog({ cameraId, initialData, onSave, onCancel }: RuleBuilderDialogProps) {
  const [name, setName] = useState(initialData?.name ?? "");
  const [objectClass, setObjectClass] = useState(initialData?.objectClass ?? "person");
  const [zoneId, setZoneId] = useState(initialData?.zoneId ?? "");
  const [conditions, setConditions] = useState<ConditionConfig[]>(initialData?.conditions ?? []);
  const [logic, setLogic] = useState<"all" | "any">(initialData?.logic ?? "all");
  const [cooldown, setCooldown] = useState(initialData?.cooldown ?? 30);

  const handleSave = () => {
    onSave({
      name,
      cameraId,
      objectClass,
      zoneId: zoneId || undefined,
      conditions,
      logic,
      cooldown,
      enabled: true,
    });
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[90vh] overflow-y-auto p-6">
        <h2 className="text-xl font-bold mb-4">
          {initialData ? "Edit Rule" : "Create Rule"}
        </h2>

        {/* Basic fields */}
        <div className="grid grid-cols-2 gap-4 mb-6">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Rule Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="e.g., Loitering Alert"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Object Class</label>
            <input
              type="text"
              value={objectClass}
              onChange={(e) => setObjectClass(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="e.g., person"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Zone ID (optional)</label>
            <input
              type="text"
              value={zoneId}
              onChange={(e) => setZoneId(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="Zone ID"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Cooldown (seconds)</label>
            <input
              type="number"
              value={cooldown}
              onChange={(e) => setCooldown(Number(e.target.value))}
              className="w-full border rounded px-3 py-2"
              min={0}
            />
          </div>
        </div>

        {/* Rule Builder Canvas */}
        <div className="mb-6">
          <h3 className="text-sm font-semibold text-gray-700 mb-2">Conditions</h3>
          <RuleCanvas
            conditions={conditions}
            logic={logic}
            onConditionsChange={setConditions}
            onLogicChange={setLogic}
          />
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-3">
          <button
            onClick={onCancel}
            className="px-4 py-2 border rounded-lg text-gray-600 hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={!name || conditions.length === 0}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            Save Rule
          </button>
        </div>
      </div>
    </div>
  );
}
