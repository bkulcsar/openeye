import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateCameraSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const camera = await prisma.camera.findUnique({
    where: { id },
    include: { zones: true, rules: true },
  });
  if (!camera) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(camera);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const result = updateCameraSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const camera = await prisma.camera.update({
    where: { id },
    data: result.data,
  });
  after(() => publishConfigChanged("cameras"));
  return NextResponse.json(camera);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.camera.delete({ where: { id } });
  after(() => publishConfigChanged("cameras"));
  return NextResponse.json({ deleted: true });
}
