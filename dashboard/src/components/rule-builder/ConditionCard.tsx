"use client";

import { ConditionConfig, CONDITION_TYPES } from "./types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Trash2 } from "lucide-react";

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
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <div>
          <CardTitle className="text-sm">{typeDef.label}</CardTitle>
          <p className="text-xs text-muted-foreground">{typeDef.description}</p>
        </div>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Remove condition"
          onClick={() => onRemove(index)}
          className="text-destructive hover:text-destructive"
        >
          <Trash2 className="h-4 w-4" />
        </Button>
      </CardHeader>
      {Object.keys(typeDef.params).length > 0 && (
        <CardContent className="space-y-3">
          {Object.entries(typeDef.params).map(([key, paramDef]) => (
            <div key={key} className="grid gap-1.5">
              <Label htmlFor={`cond-${index}-${key}`} className="text-xs">{paramDef.label}</Label>
              <Input
                id={`cond-${index}-${key}`}
                name={key}
                type={paramDef.type}
                value={(condition.params[key] as string | number) ?? paramDef.default}
                onChange={(e) => {
                  const value = paramDef.type === "number" ? Number(e.target.value) : e.target.value;
                  onUpdate(index, {
                    ...condition,
                    params: { ...condition.params, [key]: value },
                  });
                }}
                autoComplete="off"
              />
            </div>
          ))}
        </CardContent>
      )}
    </Card>
  );
}
