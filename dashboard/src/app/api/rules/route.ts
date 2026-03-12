import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createRuleSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

import { Prisma } from "@prisma/client";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const cameraId = searchParams.get("cameraId");
  const rules = await prisma.rule.findMany({
    where: cameraId ? { cameraId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(rules);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createRuleSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const rule = await prisma.rule.create({
    data: {
      name: result.data.name,
      cameraId: result.data.cameraId,
      objectClass: result.data.objectClass,
      zoneId: result.data.zoneId ?? null,
      enabled: result.data.enabled ?? true,
      conditions: result.data.conditions as Prisma.InputJsonValue,
      logic: result.data.logic ?? "all",
      cooldown: result.data.cooldown ?? 30,
    },
  });
  after(() => publishConfigChanged("rules"));
  return NextResponse.json(rule, { status: 201 });
}
