import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

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
  const rule = await prisma.rule.create({
    data: {
      name: body.name,
      cameraId: body.cameraId,
      objectClass: body.objectClass,
      zoneId: body.zoneId ?? null,
      enabled: body.enabled ?? true,
      conditions: body.conditions,
      logic: body.logic ?? "all",
      cooldown: body.cooldown ?? 30,
    },
  });
  return NextResponse.json(rule, { status: 201 });
}
