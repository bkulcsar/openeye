import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createPrimitiveSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const primitives = await prisma.primitiveConfig.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(primitives);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createPrimitiveSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const primitive = await prisma.primitiveConfig.create({ data: result.data });
  await publishConfigChanged("primitives");
  return NextResponse.json(primitive, { status: 201 });
}
