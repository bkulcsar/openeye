import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const limit = parseInt(searchParams.get("limit") ?? "50", 10);
  const offset = parseInt(searchParams.get("offset") ?? "0", 10);

  const events = await prisma.event.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { timestamp: "desc" },
    take: limit,
    skip: offset,
  });

  const total = await prisma.event.count({
    where: sourceId ? { sourceId } : undefined,
  });

  return NextResponse.json({ events, total, limit, offset });
}
