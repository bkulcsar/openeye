import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createZoneSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const cameraId = searchParams.get("cameraId");
  const zones = await prisma.zone.findMany({
    where: cameraId ? { cameraId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(zones);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createZoneSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const zone = await prisma.zone.create({
    data: {
      name: result.data.name,
      cameraId: result.data.cameraId,
      polygon: result.data.polygon,
      type: result.data.type ?? "zone",
    },
  });
  await publishConfigChanged("zones");
  return NextResponse.json(zone, { status: 201 });
}
