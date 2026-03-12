import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createTripwireSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const tripwires = await prisma.tripwire.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(tripwires);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createTripwireSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const tripwire = await prisma.tripwire.create({ data: result.data });
  after(() => publishConfigChanged("tripwires"));
  return NextResponse.json(tripwire, { status: 201 });
}
