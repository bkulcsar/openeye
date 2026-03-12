import { Suspense } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getNotificationConfigs, getRulesMinimal } from "@/lib/data";
import { NotificationList } from "./notification-list";

export const dynamic = "force-dynamic";

function NotificationsLoading() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-1">
        <Skeleton className="h-7 w-36" />
        <Skeleton className="h-4 w-64" />
      </div>
      <div className="space-y-3">
        {Array.from({ length: 2 }).map((_, i) => (
          <Card key={i}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex-1 space-y-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-56" />
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

async function NotificationsContent() {
  const [configs, rules] = await Promise.all([
    getNotificationConfigs(),
    getRulesMinimal(),
  ]);

  const serializedConfigs = configs.map((c) => ({
    ruleId: c.ruleId,
    channels: c.channels as Array<{ type: string; config: Record<string, string> }>,
  }));

  return (
    <NotificationList
      initialConfigs={serializedConfigs}
      initialRules={rules}
    />
  );
}

export default function NotificationsPage() {
  return (
    <Suspense fallback={<NotificationsLoading />}>
      <NotificationsContent />
    </Suspense>
  );
}
