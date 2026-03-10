import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
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
  const zone = await prisma.zone.create({
    data: {
      name: body.name,
      cameraId: body.cameraId,
      polygon: body.polygon,
      type: body.type ?? "zone",
    },
  });
  await publishConfigChanged("zones");
  return NextResponse.json(zone, { status: 201 });
}
