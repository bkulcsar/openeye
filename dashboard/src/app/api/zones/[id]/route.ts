import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateZoneSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const zone = await prisma.zone.findUnique({ where: { id } });
  if (!zone) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(zone);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const result = updateZoneSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const zone = await prisma.zone.update({ where: { id }, data: result.data });
  await publishConfigChanged("zones");
  return NextResponse.json(zone);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.zone.delete({ where: { id } });
  await publishConfigChanged("zones");
  return NextResponse.json({ deleted: true });
}
