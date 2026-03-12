import { cache } from "react";
import { prisma } from "./prisma";

export const getCameras = cache(() =>
  prisma.camera.findMany({
    include: { zones: true },
    orderBy: { createdAt: "desc" },
  })
);

export const getCamerasMinimal = cache(() =>
  prisma.camera.findMany({
    select: { id: true, name: true },
    orderBy: { createdAt: "desc" },
  })
);

export const getRules = cache(() =>
  prisma.rule.findMany({ orderBy: { createdAt: "desc" } })
);

export const getRulesMinimal = cache(() =>
  prisma.rule.findMany({
    select: { id: true, name: true },
    orderBy: { createdAt: "desc" },
  })
);

export const getZones = cache(() =>
  prisma.zone.findMany({ orderBy: { createdAt: "desc" } })
);

export const getTripwires = cache(() =>
  prisma.tripwire.findMany({ orderBy: { createdAt: "desc" } })
);

export const getNotificationConfigs = cache(() =>
  prisma.notificationConfig.findMany({ orderBy: { createdAt: "desc" } })
);

export const getDashboardCounts = cache(() =>
  Promise.all([
    prisma.camera.count(),
    prisma.rule.count(),
    prisma.zone.count(),
    prisma.event.count(),
  ])
);

export const getRecentEvents = cache((limit = 5) =>
  prisma.event.findMany({
    orderBy: { timestamp: "desc" },
    take: limit,
  })
);
