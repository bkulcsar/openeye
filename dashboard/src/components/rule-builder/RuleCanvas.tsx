"use client";

import { ConditionConfig } from "./types";
import { ConditionCard } from "./ConditionCard";
import { ConditionPalette } from "./ConditionPalette";
import { Badge } from "@/components/ui/badge";

interface RuleCanvasProps {
  conditions: ConditionConfig[];
  logic: "all" | "any";
  onConditionsChange: (conditions: ConditionConfig[]) => void;
  onLogicChange: (logic: "all" | "any") => void;
}

export function RuleCanvas({ conditions, logic, onConditionsChange, onLogicChange }: RuleCanvasProps) {
  const handleAdd = (condition: ConditionConfig) => {
    onConditionsChange([...conditions, condition]);
  };

  const handleUpdate = (index: number, condition: ConditionConfig) => {
    const updated = [...conditions];
    updated[index] = condition;
    onConditionsChange(updated);
  };

  const handleRemove = (index: number) => {
    onConditionsChange(conditions.filter((_, i) => i !== index));
  };

  return (
    <div className="flex gap-6">
      {/* Palette */}
      <div className="w-48 flex-shrink-0">
        <ConditionPalette onAdd={handleAdd} />
      </div>

      {/* Canvas */}
      <div className="flex-1 min-h-[300px] rounded-lg border-2 border-dashed border-muted-foreground/25 p-4">
        <div className="flex items-center gap-2 mb-4">
          <span className="text-sm text-muted-foreground">Match</span>
          <select
            value={logic}
            onChange={(e) => onLogicChange(e.target.value as "all" | "any")}
            aria-label="Condition logic"
            className="flex h-8 rounded-md border border-input bg-transparent px-2 py-1 text-sm shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            <option value="all">ALL conditions</option>
            <option value="any">ANY condition</option>
          </select>
        </div>

        {conditions.length === 0 ? (
          <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
            Click a condition from the palette to add it
          </div>
        ) : (
          <div className="space-y-3">
            {conditions.map((condition, index) => (
              <div key={index}>
                {index > 0 && (
                  <div className="flex justify-center py-1">
                    <Badge variant="outline" className="text-xs">
                      {logic === "all" ? "AND" : "OR"}
                    </Badge>
                  </div>
                )}
                <ConditionCard
                  condition={condition}
                  index={index}
                  onUpdate={handleUpdate}
                  onRemove={handleRemove}
                />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
