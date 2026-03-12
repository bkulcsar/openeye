import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateTripwireSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const tripwire = await prisma.tripwire.findUnique({ where: { id } });
  if (!tripwire) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(tripwire);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const result = updateTripwireSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const tripwire = await prisma.tripwire.update({ where: { id }, data: result.data });
  after(() => publishConfigChanged("tripwires"));
  return NextResponse.json(tripwire);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.tripwire.delete({ where: { id } });
  after(() => publishConfigChanged("tripwires"));
  return NextResponse.json({ deleted: true });
}
