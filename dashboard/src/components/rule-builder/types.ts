export interface ConditionConfig {
  type: string;
  params: Record<string, unknown>;
}

export interface RuleFormData {
  name: string;
  cameraId: string;
  objectClass: string;
  zoneId?: string;
  conditions: ConditionConfig[];
  logic: "all" | "any";
  cooldown: number;
  enabled: boolean;
}

export const CONDITION_TYPES = [
  {
    type: "duration",
    label: "Duration",
    description: "Object stays in zone for N seconds",
    params: { minSeconds: { type: "number", default: 5, label: "Min Seconds" } },
  },
  {
    type: "count_above",
    label: "Count Above",
    description: "More than N objects in zone",
    params: { threshold: { type: "number", default: 3, label: "Threshold" } },
  },
  {
    type: "line_cross",
    label: "Line Cross",
    description: "Object crosses a tripwire",
    params: { tripwireId: { type: "string", default: "", label: "Tripwire ID" } },
  },
  {
    type: "speed",
    label: "Speed",
    description: "Object speed exceeds threshold",
    params: {
      minSpeed: { type: "number", default: 0, label: "Min Speed" },
      maxSpeed: { type: "number", default: 100, label: "Max Speed" },
    },
  },
  {
    type: "presence",
    label: "Presence",
    description: "Object is present in zone",
    params: {},
  },
  {
    type: "absence",
    label: "Absence",
    description: "No objects in zone for N seconds",
    params: { timeoutSeconds: { type: "number", default: 30, label: "Timeout (s)" } },
  },
] as const;
