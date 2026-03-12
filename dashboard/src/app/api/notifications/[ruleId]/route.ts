import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { updateNotificationSchema } from "@/lib/validations";
import { after, NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  const config = await prisma.notificationConfig.findUnique({ where: { ruleId } });
  if (!config) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(config);
}

export async function PUT(request: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  const body = await request.json();
  const result = updateNotificationSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const config = await prisma.notificationConfig.update({ where: { ruleId }, data: result.data });
  after(() => publishConfigChanged("notifications"));
  return NextResponse.json(config);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ ruleId: string }> }) {
  const { ruleId } = await params;
  await prisma.notificationConfig.delete({ where: { ruleId } });
  after(() => publishConfigChanged("notifications"));
  return NextResponse.json({ deleted: true });
}
