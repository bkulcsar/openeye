"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { RuleBuilderDialog } from "@/components/rule-builder/RuleBuilderDialog";
import { RuleFormData } from "@/components/rule-builder/types";
import { MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { toast } from "sonner";

interface Rule {
  id: string;
  name: string;
  cameraId: string;
  objectClass: string;
  zoneId: string | null;
  enabled: boolean;
  conditions: Array<{ type: string; params: Record<string, unknown> }>;
  logic: string;
  cooldown: number;
}

export default function RulesPage() {
  const [rules, setRules] = useState<Rule[]>([]);
  const [loading, setLoading] = useState(true);
  const [builderOpen, setBuilderOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<Rule | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Rule | null>(null);

  useEffect(() => {
    fetch("/api/rules")
      .then((r) => r.json())
      .then(setRules)
      .catch(() => toast.error("Failed to load rules"))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async (data: RuleFormData) => {
    const method = editingRule ? "PUT" : "POST";
    const url = editingRule ? `/api/rules/${editingRule.id}` : "/api/rules";

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });

    if (!res.ok) {
      toast.error(editingRule ? "Failed to update rule" : "Failed to create rule");
      return;
    }

    const savedRule = await res.json();
    if (editingRule) {
      setRules(rules.map((r) => (r.id === savedRule.id ? savedRule : r)));
      toast.success("Rule updated");
    } else {
      setRules([savedRule, ...rules]);
      toast.success("Rule created");
    }

    setBuilderOpen(false);
    setEditingRule(null);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await fetch(`/api/rules/${deleteTarget.id}`, { method: "DELETE" });
    if (res.ok) {
      setRules(rules.filter((r) => r.id !== deleteTarget.id));
      toast.success("Rule deleted");
    } else {
      toast.error("Failed to delete rule");
    }
    setDeleteTarget(null);
  };

  const openCreate = () => {
    setEditingRule(null);
    setBuilderOpen(true);
  };

  const openEdit = (rule: Rule) => {
    setEditingRule(rule);
    setBuilderOpen(true);
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title="Rules"
        description="Define event detection rules for your cameras."
        action={{ label: "Create Rule", onClick: openCreate }}
      />

      {loading ? (
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
      ) : rules.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">No rules yet. Create one to get started.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {rules.map((rule) => (
            <Card key={rule.id}>
              <CardContent className="flex items-center gap-4 py-4">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-medium">{rule.name}</h3>
                    <Badge variant={rule.enabled ? "default" : "secondary"}>
                      {rule.enabled ? "Active" : "Disabled"}
                    </Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    <Badge variant="outline" className="mr-1">{rule.objectClass}</Badge>
                    {rule.conditions.length} condition(s) · {rule.logic} · {rule.cooldown}s cooldown
                  </p>
                </div>
                <DropdownMenu>
                  <DropdownMenuTrigger className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-sm hover:bg-muted" aria-label="Rule actions">
                    <MoreHorizontal className="h-4 w-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => openEdit(rule)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      onClick={() => setDeleteTarget(rule)}
                    >
                      <Trash2 className="mr-2 h-4 w-4" />
                      Delete
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <RuleBuilderDialog
        open={builderOpen}
        onOpenChange={(open) => {
          setBuilderOpen(open);
          if (!open) setEditingRule(null);
        }}
        cameraId={editingRule?.cameraId ?? ""}
        initialData={
          editingRule
            ? {
                name: editingRule.name,
                objectClass: editingRule.objectClass,
                zoneId: editingRule.zoneId ?? undefined,
                conditions: editingRule.conditions,
                logic: editingRule.logic as "all" | "any",
                cooldown: editingRule.cooldown,
              }
            : undefined
        }
        onSave={handleSave}
      />

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Rule</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete &quot;{deleteTarget?.name}&quot;. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>Delete Rule</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
