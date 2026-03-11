"use client";

import { useEffect, useState } from "react";

interface SystemHealth {
  cameras: number;
  rules: number;
  zones: number;
  events: number;
}

export default function SettingsPage() {
  const [health, setHealth] = useState<SystemHealth | null>(null);

  useEffect(() => {
    Promise.all([
      fetch("/api/cameras").then((r) => r.json()),
      fetch("/api/rules").then((r) => r.json()),
      fetch("/api/zones").then((r) => r.json()),
      fetch("/api/events?limit=1").then((r) => r.json()),
    ]).then(([cameras, rules, zones, eventsData]) => {
      setHealth({
        cameras: cameras.length,
        rules: rules.length,
        zones: zones.length,
        events: eventsData.total ?? 0,
      });
    });
  }, []);

  return (
    <div className="p-8 max-w-4xl">
      <h1 className="text-2xl font-bold mb-6">Settings</h1>

      {/* System Overview */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold mb-3">System Overview</h2>
        {health ? (
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {[
              { label: "Cameras", value: health.cameras },
              { label: "Zones", value: health.zones },
              { label: "Rules", value: health.rules },
              { label: "Total Events", value: health.events },
            ].map((stat) => (
              <div key={stat.label} className="border rounded-lg p-4 bg-white shadow-sm text-center">
                <p className="text-2xl font-bold">{stat.value}</p>
                <p className="text-sm text-gray-500">{stat.label}</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-gray-400">Loading...</p>
        )}
      </div>

      {/* Detection Model Config (informational) */}
      <div className="mb-8">
        <h2 className="text-lg font-semibold mb-3">Detection Model Configuration</h2>
        <p className="text-sm text-gray-600 mb-3">
          These settings are configured via environment variables or appsettings.json on the backend services.
        </p>
        <div className="border rounded-lg bg-white shadow-sm divide-y">
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Inference URL</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__URL</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Model ID</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__MODELID</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Confidence Threshold</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__CONFIDENCETHRESHOLD</code>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">API Key</span>
            <code className="text-sm font-mono text-gray-800">ROBOFLOW__APIKEY</code>
          </div>
        </div>
      </div>

      {/* Infrastructure */}
      <div>
        <h2 className="text-lg font-semibold mb-3">Infrastructure</h2>
        <div className="border rounded-lg bg-white shadow-sm divide-y">
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Database</span>
            <span className="text-sm">PostgreSQL (via Prisma)</span>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Message Bus</span>
            <span className="text-sm">Redis Streams</span>
          </div>
          <div className="p-4 flex justify-between">
            <span className="text-sm text-gray-600">Orchestration</span>
            <span className="text-sm">.NET Aspire</span>
          </div>
        </div>
      </div>
    </div>
  );
}
