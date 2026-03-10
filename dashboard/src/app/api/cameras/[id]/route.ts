import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { NextResponse } from "next/server";

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
  const camera = await prisma.camera.update({
    where: { id },
    data: body,
  });
  await publishConfigChanged("cameras");
  return NextResponse.json(camera);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.camera.delete({ where: { id } });
  await publishConfigChanged("cameras");
  return NextResponse.json({ deleted: true });
}
