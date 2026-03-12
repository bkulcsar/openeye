import { Suspense } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getRules } from "@/lib/data";
import { RuleList } from "./rule-list";

export const dynamic = "force-dynamic";

function RulesLoading() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-1">
        <Skeleton className="h-7 w-24" />
        <Skeleton className="h-4 w-64" />
      </div>
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Card key={i}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex-1 space-y-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-4 w-56" />
              </div>
              <Skeleton className="h-8 w-8" />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

async function RulesContent() {
  const rules = await getRules();
  const serialized = rules.map((r) => ({
    ...r,
    conditions: r.conditions as Array<{ type: string; params: Record<string, unknown> }>,
    createdAt: r.createdAt.toISOString(),
    updatedAt: r.updatedAt.toISOString(),
  }));
  return <RuleList initialRules={serialized} />;
}

export default function RulesPage() {
  return (
    <Suspense fallback={<RulesLoading />}>
      <RulesContent />
    </Suspense>
  );
}
