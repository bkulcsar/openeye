"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { toast } from "sonner";

interface TrackedObject {
  objectId?: string;
  label?: string;
  confidence?: number;
}

interface EventItem {
  id: string;
  eventType: string;
  timestamp: string;
  sourceId: string;
  zoneId: string | null;
  ruleId: string;
  trackedObjects: TrackedObject[];
  metadata: Record<string, unknown> | null;
}

const PAGE_SIZE = 20;

const fmt = new Intl.DateTimeFormat(undefined, {
  dateStyle: "short",
  timeStyle: "medium",
});

export default function EventsPage() {
  const [events, setEvents] = useState<EventItem[]>([]);
  const [total, setTotal] = useState(0);
  const [offset, setOffset] = useState(0);
  const [sourceFilter, setSourceFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    const params = new URLSearchParams({
      limit: String(PAGE_SIZE),
      offset: String(offset),
    });
    if (sourceFilter) params.set("sourceId", sourceFilter);
    fetch(`/api/events?${params}`)
      .then((r) => r.json())
      .then((d) => {
        setEvents(d.events);
        setTotal(d.total);
      })
      .catch(() => toast.error("Failed to load events"))
      .finally(() => setLoading(false));
  }, [offset, sourceFilter]);

  const totalPages = Math.ceil(total / PAGE_SIZE);
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1;

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader title="Events" description="Browse triggered events across all rules." />

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-4">
        <div className="grid w-56 gap-1.5">
          <Label htmlFor="filter-source">Source ID</Label>
          <Input
            id="filter-source"
            name="sourceId"
            placeholder="Filter by source…"
            value={sourceFilter}
            onChange={(e) => {
              setSourceFilter(e.target.value);
              setOffset(0);
            }}
            autoComplete="off"
          />
        </div>
      </div>

      {/* Table */}
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-10" />
              <TableHead>Type</TableHead>
              <TableHead>Source</TableHead>
              <TableHead>Rule</TableHead>
              <TableHead>Zone</TableHead>
              <TableHead className="text-right">Objects</TableHead>
              <TableHead className="text-right">Timestamp</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              Array.from({ length: 6 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell><Skeleton className="h-4 w-4" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-20" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  <TableCell><Skeleton className="h-4 w-16" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-6" /></TableCell>
                  <TableCell className="text-right"><Skeleton className="ml-auto h-4 w-28" /></TableCell>
                </TableRow>
              ))
            ) : events.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-12 text-center text-muted-foreground">
                  No events found.
                </TableCell>
              </TableRow>
            ) : (
              events.map((ev) => {
                const expanded = expandedId === ev.id;
                return (
                  <TableRow key={ev.id} data-state={expanded ? "expanded" : undefined}>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        aria-label={expanded ? "Collapse details" : "Expand details"}
                        aria-expanded={expanded}
                        onClick={() => setExpandedId(expanded ? null : ev.id)}
                      >
                        <ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />
                      </Button>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{ev.eventType}</Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{ev.sourceId}</TableCell>
                    <TableCell className="font-mono text-xs">{ev.ruleId}</TableCell>
                    <TableCell className="font-mono text-xs">{ev.zoneId ?? "—"}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {Array.isArray(ev.trackedObjects) ? ev.trackedObjects.length : 0}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {fmt.format(new Date(ev.timestamp))}
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </div>

      {/* Expanded detail — rendered below table for simplicity */}
      {expandedId && (() => {
        const ev = events.find((e) => e.id === expandedId);
        if (!ev) return null;
        return (
          <div className="rounded-md border bg-muted/50 p-4 space-y-3">
            <h3 className="font-medium">Event Details — <span className="font-mono text-sm">{ev.id}</span></h3>
            <div className="grid gap-2 text-sm sm:grid-cols-2">
              <div><span className="text-muted-foreground">Type:</span> {ev.eventType}</div>
              <div><span className="text-muted-foreground">Source:</span> {ev.sourceId}</div>
              <div><span className="text-muted-foreground">Rule:</span> {ev.ruleId}</div>
              <div><span className="text-muted-foreground">Zone:</span> {ev.zoneId ?? "—"}</div>
              <div className="sm:col-span-2">
                <span className="text-muted-foreground">Timestamp:</span>{" "}
                {fmt.format(new Date(ev.timestamp))}
              </div>
            </div>

            {Array.isArray(ev.trackedObjects) && ev.trackedObjects.length > 0 && (
              <div>
                <h4 className="mb-1 text-sm font-medium text-muted-foreground">Tracked Objects</h4>
                <div className="rounded border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Object ID</TableHead>
                        <TableHead>Label</TableHead>
                        <TableHead className="text-right">Confidence</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {ev.trackedObjects.map((obj, idx) => (
                        <TableRow key={idx}>
                          <TableCell className="font-mono text-xs">{obj.objectId ?? "—"}</TableCell>
                          <TableCell>{obj.label ?? "—"}</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {obj.confidence != null ? `${(obj.confidence * 100).toFixed(1)}%` : "—"}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </div>
            )}

            {ev.metadata && Object.keys(ev.metadata).length > 0 && (
              <div>
                <h4 className="mb-1 text-sm font-medium text-muted-foreground">Metadata</h4>
                <pre className="max-h-48 overflow-auto rounded bg-muted p-3 text-xs">
                  {JSON.stringify(ev.metadata, null, 2)}
                </pre>
              </div>
            )}
          </div>
        );
      })()}

      {/* Pagination */}
      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground tabular-nums">
            {offset + 1}–{Math.min(offset + PAGE_SIZE, total)} of {total}
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="icon"
              disabled={offset === 0}
              onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
              aria-label="Previous page"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="text-sm tabular-nums">
              Page {currentPage} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="icon"
              disabled={offset + PAGE_SIZE >= total}
              onClick={() => setOffset(offset + PAGE_SIZE)}
              aria-label="Next page"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
