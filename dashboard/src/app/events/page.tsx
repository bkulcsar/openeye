"use client";

import { useEffect, useState } from "react";

interface EventRecord {
  id: string;
  eventType: string;
  timestamp: string;
  sourceId: string;
  zoneId: string | null;
  ruleId: string;
  trackedObjects: unknown[];
  metadata: unknown | null;
  createdAt: string;
}

const PAGE_SIZE = 20;

export default function EventsPage() {
  const [events, setEvents] = useState<EventRecord[]>([]);
  const [total, setTotal] = useState(0);
  const [offset, setOffset] = useState(0);
  const [sourceFilter, setSourceFilter] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const params = new URLSearchParams({
      limit: String(PAGE_SIZE),
      offset: String(offset),
    });
    if (sourceFilter.trim()) {
      params.set("sourceId", sourceFilter.trim());
    }
    fetch(`/api/events?${params}`)
      .then((r) => r.json())
      .then((data) => {
        setEvents(data.events);
        setTotal(data.total);
      })
      .finally(() => setLoading(false));
  }, [offset, sourceFilter]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.floor(offset / PAGE_SIZE) + 1;

  const handleFilterChange = (value: string) => {
    setSourceFilter(value);
    setOffset(0);
  };

  const formatTimestamp = (ts: string) => {
    const d = new Date(ts);
    return d.toLocaleString();
  };

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Events</h1>
        <span className="text-sm text-gray-500">{total} total</span>
      </div>

      {/* Filter */}
      <div className="mb-4">
        <input
          type="text"
          placeholder="Filter by source ID…"
          value={sourceFilter}
          onChange={(e) => handleFilterChange(e.target.value)}
          className="border rounded-md px-3 py-2 text-sm w-64"
        />
      </div>

      {/* Events table */}
      <div className="border rounded-lg bg-white shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b bg-gray-50 text-left text-gray-600">
              <th className="px-4 py-3 font-medium">Event Type</th>
              <th className="px-4 py-3 font-medium">Timestamp</th>
              <th className="px-4 py-3 font-medium">Source</th>
              <th className="px-4 py-3 font-medium">Zone</th>
              <th className="px-4 py-3 font-medium">Rule</th>
            </tr>
          </thead>
          <tbody>
            {events.map((event) => (
              <>
                <tr
                  key={event.id}
                  onClick={() =>
                    setExpandedId(expandedId === event.id ? null : event.id)
                  }
                  className="border-b hover:bg-gray-50 cursor-pointer"
                >
                  <td className="px-4 py-3">
                    <span className="inline-block bg-blue-100 text-blue-800 text-xs font-medium px-2 py-0.5 rounded-full">
                      {event.eventType}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-600">
                    {formatTimestamp(event.timestamp)}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-700">
                    {event.sourceId}
                  </td>
                  <td className="px-4 py-3 text-gray-600">
                    {event.zoneId ?? "—"}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-700">
                    {event.ruleId}
                  </td>
                </tr>
                {expandedId === event.id && (
                  <tr key={`${event.id}-detail`} className="border-b bg-gray-50">
                    <td colSpan={5} className="px-4 py-4">
                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <h4 className="text-xs font-medium text-gray-500 mb-1">
                            Tracked Objects
                          </h4>
                          <pre className="text-xs bg-gray-100 rounded p-3 overflow-auto max-h-48">
                            {JSON.stringify(event.trackedObjects, null, 2)}
                          </pre>
                        </div>
                        <div>
                          <h4 className="text-xs font-medium text-gray-500 mb-1">
                            Metadata
                          </h4>
                          <pre className="text-xs bg-gray-100 rounded p-3 overflow-auto max-h-48">
                            {event.metadata
                              ? JSON.stringify(event.metadata, null, 2)
                              : "—"}
                          </pre>
                        </div>
                      </div>
                    </td>
                  </tr>
                )}
              </>
            ))}
            {!loading && events.length === 0 && (
              <tr>
                <td
                  colSpan={5}
                  className="px-4 py-8 text-center text-gray-500"
                >
                  No events found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-4">
          <button
            onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
            disabled={offset === 0}
            className="px-4 py-2 border rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Previous
          </button>
          <span className="text-sm text-gray-600">
            Page {currentPage} of {totalPages}
          </span>
          <button
            onClick={() => setOffset(offset + PAGE_SIZE)}
            disabled={offset + PAGE_SIZE >= total}
            className="px-4 py-2 border rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
