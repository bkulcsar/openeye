import { prisma } from "@/lib/prisma";
import { publishConfigChanged } from "@/lib/redis";
import { createNotificationSchema } from "@/lib/validations";
import { NextResponse } from "next/server";

export async function GET() {
  const configs = await prisma.notificationConfig.findMany({
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(configs);
}

export async function POST(request: Request) {
  const body = await request.json();
  const result = createNotificationSchema.safeParse(body);
  if (!result.success) {
    return NextResponse.json({ error: result.error.flatten() }, { status: 400 });
  }
  const config = await prisma.notificationConfig.create({ data: result.data });
  await publishConfigChanged("notifications");
  return NextResponse.json(config, { status: 201 });
}
