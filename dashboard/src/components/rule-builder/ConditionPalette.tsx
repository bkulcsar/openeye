"use client";

import { CONDITION_TYPES, ConditionConfig } from "./types";
import { Button } from "@/components/ui/button";

interface ConditionPaletteProps {
  onAdd: (condition: ConditionConfig) => void;
}

export function ConditionPalette({ onAdd }: ConditionPaletteProps) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold">Conditions</h3>
      {CONDITION_TYPES.map((typeDef) => (
        <Button
          key={typeDef.type}
          variant="outline"
          className="w-full h-auto flex-col items-start gap-0.5 p-3"
          onClick={() => {
            const defaultParams: Record<string, unknown> = {};
            for (const [key, paramDef] of Object.entries(typeDef.params)) {
              defaultParams[key] = paramDef.default;
            }
            onAdd({ type: typeDef.type, params: defaultParams });
          }}
        >
          <span className="text-sm font-medium">{typeDef.label}</span>
          <span className="text-xs text-muted-foreground font-normal">{typeDef.description}</span>
        </Button>
      ))}
    </div>
  );
}
