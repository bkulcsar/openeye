import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updatePrimitiveSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  const primitive = await prisma.primitiveConfig.findUnique({ where: { name } });
  if (!primitive) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(primitive);
}

export async function PUT(request: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  const body = await request.json();
  const result = updatePrimitiveSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const primitive = await prisma.primitiveConfig.update({ where: { name }, data: result.data });
  await publishConfigChanged("primitives");
  return NextResponse.json(primitive);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  await prisma.primitiveConfig.delete({ where: { name } });
  await publishConfigChanged("primitives");
  return NextResponse.json({ deleted: true });
}
