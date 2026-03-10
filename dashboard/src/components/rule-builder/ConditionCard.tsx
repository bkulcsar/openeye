"use client";

import { ConditionConfig, CONDITION_TYPES } from "./types";

interface ConditionCardProps {
  condition: ConditionConfig;
  index: number;
  onUpdate: (index: number, condition: ConditionConfig) => void;
  onRemove: (index: number) => void;
}

export function ConditionCard({ condition, index, onUpdate, onRemove }: ConditionCardProps) {
  const typeDef = CONDITION_TYPES.find((t) => t.type === condition.type);
  if (!typeDef) return null;

  return (
    <div className="border rounded-lg p-4 bg-white shadow-sm" draggable>
      <div className="flex items-center justify-between mb-2">
        <h4 className="font-medium text-sm">{typeDef.label}</h4>
        <button
          onClick={() => onRemove(index)}
          className="text-red-500 hover:text-red-700 text-sm"
        >
          Remove
        </button>
      </div>
      <p className="text-xs text-gray-500 mb-3">{typeDef.description}</p>
      <div className="space-y-2">
        {Object.entries(typeDef.params).map(([key, paramDef]) => (
          <div key={key} className="flex items-center gap-2">
            <label className="text-xs text-gray-600 w-24">{paramDef.label}</label>
            <input
              type={paramDef.type}
              value={(condition.params[key] as string | number) ?? paramDef.default}
              onChange={(e) => {
                const value = paramDef.type === "number" ? Number(e.target.value) : e.target.value;
                onUpdate(index, {
                  ...condition,
                  params: { ...condition.params, [key]: value },
                });
              }}
              className="border rounded px-2 py-1 text-sm flex-1"
            />
          </div>
        ))}
      </div>
    </div>
  );
}
