"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { MoreHorizontal, Trash2, Loader2 } from "lucide-react";
import { toast } from "sonner";

interface NotificationChannel {
  type: string;
  config: Record<string, string>;
}

interface NotificationConfig {
  ruleId: string;
  channels: NotificationChannel[];
}

interface Rule {
  id: string;
  name: string;
}

const CHANNEL_TYPES = [
  { value: "webhook", label: "Webhook" },
  { value: "email", label: "Email" },
  { value: "whatsapp", label: "WhatsApp" },
  { value: "dashboard", label: "Dashboard Push" },
];

const CHANNEL_FIELDS: Record<string, { key: string; label: string; placeholder: string; type: string }[]> = {
  webhook: [{ key: "url", label: "Webhook URL", placeholder: "https://example.com/hook…", type: "url" }],
  email: [{ key: "email", label: "Email Address", placeholder: "alerts@example.com…", type: "email" }],
  whatsapp: [{ key: "phone", label: "Phone Number", placeholder: "+1234567890…", type: "tel" }],
  dashboard: [],
};

export default function NotificationsPage() {
  const [configs, setConfigs] = useState<NotificationConfig[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [loading, setLoading] = useState(true);

  // Add dialog
  const [dialogOpen, setDialogOpen] = useState(false);
  const [ruleId, setRuleId] = useState("");
  const [channelType, setChannelType] = useState("webhook");
  const [channelConfig, setChannelConfig] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  // Delete
  const [deleteTarget, setDeleteTarget] = useState<NotificationConfig | null>(null);

  useEffect(() => {
    Promise.all([
      fetch("/api/notifications").then((r) => r.json()),
      fetch("/api/rules").then((r) => r.json()),
    ])
      .then(([n, r]) => {
        setConfigs(n);
        setRules(r);
      })
      .catch(() => toast.error("Failed to load data"))
      .finally(() => setLoading(false));
  }, []);

  const ruleName = (id: string) => rules.find((r) => r.id === id)?.name ?? id;

  const handleAdd = async () => {
    setSaving(true);
    const res = await fetch("/api/notifications", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ruleId, channels: [{ type: channelType, config: channelConfig }] }),
    });
    if (res.ok) {
      const nc = await res.json();
      setConfigs([nc, ...configs]);
      setDialogOpen(false);
      setRuleId("");
      setChannelType("webhook");
      setChannelConfig({});
      toast.success("Notification config created");
    } else {
      toast.error("Failed to create notification config");
    }
    setSaving(false);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await fetch(`/api/notifications/${deleteTarget.ruleId}`, { method: "DELETE" });
    if (res.ok) {
      setConfigs(configs.filter((c) => c.ruleId !== deleteTarget.ruleId));
      toast.success("Notification config deleted");
    } else {
      toast.error("Failed to delete notification config");
    }
    setDeleteTarget(null);
  };

  const fields = CHANNEL_FIELDS[channelType] ?? [];

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title="Notifications"
        description="Configure how you get notified when rules trigger."
        action={{ label: "Add Notification", onClick: () => setDialogOpen(true) }}
      />

      {loading ? (
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
      ) : configs.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">No notification configs. Add one to get started.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {configs.map((nc) => (
            <Card key={nc.ruleId}>
              <CardContent className="flex items-center gap-4 py-4">
                <div className="min-w-0 flex-1">
                  <h3 className="font-medium">{ruleName(nc.ruleId)}</h3>
                  <div className="mt-1 flex flex-wrap items-center gap-1.5">
                    {nc.channels.map((ch, i) => (
                      <Badge key={i} variant="secondary">{ch.type}</Badge>
                    ))}
                    <span className="text-sm text-muted-foreground">
                      · {nc.channels.length} channel(s)
                    </span>
                  </div>
                </div>
                <DropdownMenu>
                  <DropdownMenuTrigger className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-sm hover:bg-muted" aria-label="Notification actions">
                    <MoreHorizontal className="h-4 w-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      onClick={() => setDeleteTarget(nc)}
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

      {/* Add Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Add Notification</DialogTitle>
            <DialogDescription>Choose a rule and notification channel.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="notif-rule">Rule</Label>
              <select
                id="notif-rule"
                value={ruleId}
                onChange={(e) => setRuleId(e.target.value)}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Select rule…</option>
                {rules.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="notif-channel">Channel Type</Label>
              <select
                id="notif-channel"
                value={channelType}
                onChange={(e) => {
                  setChannelType(e.target.value);
                  setChannelConfig({});
                }}
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                {CHANNEL_TYPES.map((ct) => (
                  <option key={ct.value} value={ct.value}>{ct.label}</option>
                ))}
              </select>
            </div>
            {fields.map((field) => (
              <div key={field.key} className="grid gap-2">
                <Label htmlFor={`notif-${field.key}`}>{field.label}</Label>
                <Input
                  id={`notif-${field.key}`}
                  name={field.key}
                  type={field.type}
                  value={channelConfig[field.key] ?? ""}
                  onChange={(e) => setChannelConfig({ ...channelConfig, [field.key]: e.target.value })}
                  placeholder={field.placeholder}
                  autoComplete="off"
                />
              </div>
            ))}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleAdd} disabled={!ruleId || saving}>
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add Notification
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Notification Config</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove all notification channels for this rule. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
