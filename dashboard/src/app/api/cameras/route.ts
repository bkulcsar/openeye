import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { NextResponse } from "next/server";

export async function GET() {
  const cameras = await prisma.camera.findMany({
    include: { zones: true },
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(cameras);
}

export async function POST(request: Request) {
  const body = await request.json();
  const camera = await prisma.camera.create({
    data: {
      name: body.name,
      url: body.url,
      targetFps: body.targetFps ?? 5,
      enabled: body.enabled ?? true,
    },
  });
  await publishConfigChanged("cameras");
  return NextResponse.json(camera, { status: 201 });
}
