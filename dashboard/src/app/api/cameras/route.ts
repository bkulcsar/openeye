import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createCameraSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

export async function GET() {
  const cameras = await prisma.camera.findMany({
    include: { zones: true },
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(cameras);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createCameraSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const camera = await prisma.camera.create({
    data: {
      name: result.data.name,
      url: result.data.url,
      targetFps: result.data.targetFps ?? 5,
      enabled: result.data.enabled ?? true,
    },
  });
  after(() => publishConfigChanged("cameras"));
  return NextResponse.json(camera, { status: 201 });
}
