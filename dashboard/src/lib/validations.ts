import { z } from "zod";

// --- Camera ---
export const createCameraSchema = z.object({
  name: z.string().min(1).max(255),
  url: z.string().min(1).max(2048),
  type: z.string().max(50).optional(),
  targetFps: z.number().int().min(1).max(60).optional(),
  enabled: z.boolean().optional(),
});

export const updateCameraSchema = createCameraSchema.partial();

// --- Zone ---
export const createZoneSchema = z.object({
  name: z.string().min(1).max(255),
  cameraId: z.string().min(1),
  polygon: z.unknown(),
  type: z.string().max(50).optional(),
});

export const updateZoneSchema = createZoneSchema.partial();

// --- Rule ---
export const createRuleSchema = z.object({
  name: z.string().min(1).max(255),
  cameraId: z.string().min(1),
  objectClass: z.string().min(1).max(255),
  zoneId: z.string().nullable().optional(),
  tripwireId: z.string().nullable().optional(),
  enabled: z.boolean().optional(),
  conditions: z.unknown(),
  logic: z.string().max(50).optional(),
  cooldown: z.number().int().min(0).max(86400).optional(),
  sustained: z.number().min(0).nullable().optional(),
  within: z.number().min(0).nullable().optional(),
  minOccurrences: z.number().int().min(0).nullable().optional(),
  evidenceType: z.string().max(100).nullable().optional(),
});

export const updateRuleSchema = createRuleSchema.partial();

// --- Tripwire ---
export const createTripwireSchema = z.object({
  sourceId: z.string().min(1),
  startX: z.number().min(0).max(1),
  startY: z.number().min(0).max(1),
  endX: z.number().min(0).max(1),
  endY: z.number().min(0).max(1),
});

export const updateTripwireSchema = createTripwireSchema.partial();

// --- Primitive Config ---
export const createPrimitiveSchema = z.object({
  name: z.string().min(1).max(255),
  type: z.string().min(1).max(50),
  classLabel: z.string().min(1).max(255),
  zoneId: z.string().nullable().optional(),
  tripwireId: z.string().nullable().optional(),
  sourceId: z.string().min(1),
});

export const updatePrimitiveSchema = createPrimitiveSchema.partial().omit({ name: true });
