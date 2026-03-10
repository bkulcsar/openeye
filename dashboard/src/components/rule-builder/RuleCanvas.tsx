"use client";

import { ConditionConfig } from "./types";
import { ConditionCard } from "./ConditionCard";
import { ConditionPalette } from "./ConditionPalette";

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
      {/* Palette (left sidebar) */}
      <div className="w-48 flex-shrink-0">
        <ConditionPalette onAdd={handleAdd} />
      </div>

      {/* Canvas (center) */}
      <div className="flex-1 min-h-[300px] border-2 border-dashed border-gray-200 rounded-lg p-4">
        <div className="flex items-center gap-2 mb-4">
          <span className="text-sm text-gray-600">Match</span>
          <select
            value={logic}
            onChange={(e) => onLogicChange(e.target.value as "all" | "any")}
            className="border rounded px-2 py-1 text-sm"
          >
            <option value="all">ALL conditions</option>
            <option value="any">ANY condition</option>
          </select>
        </div>

        {conditions.length === 0 ? (
          <div className="flex items-center justify-center h-48 text-gray-400">
            Click a condition from the palette to add it
          </div>
        ) : (
          <div className="space-y-3">
            {conditions.map((condition, index) => (
              <div key={index}>
                {index > 0 && (
                  <div className="text-center text-xs text-gray-400 py-1">
                    {logic === "all" ? "AND" : "OR"}
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
