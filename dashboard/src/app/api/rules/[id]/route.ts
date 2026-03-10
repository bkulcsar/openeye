import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateRuleSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const rule = await prisma.rule.findUnique({ where: { id } });
  if (!rule) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(rule);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const result = updateRuleSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const rule = await prisma.rule.update({ where: { id }, data: result.data });
  await publishConfigChanged("rules");
  return NextResponse.json(rule);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.rule.delete({ where: { id } });
  await publishConfigChanged("rules");
  return NextResponse.json({ deleted: true });
}
