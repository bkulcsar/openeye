"use client";

import { CONDITION_TYPES, ConditionConfig } from "./types";

interface ConditionPaletteProps {
  onAdd: (condition: ConditionConfig) => void;
}

export function ConditionPalette({ onAdd }: ConditionPaletteProps) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold text-gray-700">Conditions</h3>
      {CONDITION_TYPES.map((typeDef) => (
        <button
          key={typeDef.type}
          onClick={() => {
            const defaultParams: Record<string, unknown> = {};
            for (const [key, paramDef] of Object.entries(typeDef.params)) {
              defaultParams[key] = paramDef.default;
            }
            onAdd({ type: typeDef.type, params: defaultParams });
          }}
          className="w-full text-left border rounded p-2 hover:bg-blue-50 transition-colors"
        >
          <div className="text-sm font-medium">{typeDef.label}</div>
          <div className="text-xs text-gray-500">{typeDef.description}</div>
        </button>
      ))}
    </div>
  );
}
