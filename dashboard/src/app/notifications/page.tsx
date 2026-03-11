"use client";

import { useEffect, useState } from "react";

interface NotificationChannel {
  type: string;
  config: Record<string, string>;
}

interface NotificationConfig {
  ruleId: string;
  channels: NotificationChannel[];
}

export default function NotificationsPage() {
  const [configs, setConfigs] = useState<NotificationConfig[]>([]);
  const [showAdd, setShowAdd] = useState(false);
  const [ruleId, setRuleId] = useState("");
  const [channelType, setChannelType] = useState("webhook");
  const [channelConfigStr, setChannelConfigStr] = useState('{"url": ""}');

  useEffect(() => {
    fetch("/api/notifications").then((r) => r.json()).then(setConfigs);
  }, []);

  const handleAdd = async () => {
    let config;
    try { config = JSON.parse(channelConfigStr); } catch { return; }
    const res = await fetch("/api/notifications", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ruleId, channels: [{ type: channelType, config }] }),
    });
    if (res.ok) {
      const nc = await res.json();
      setConfigs([nc, ...configs]);
      setShowAdd(false);
      setRuleId("");
      setChannelConfigStr('{"url": ""}');
    }
  };

  const handleDelete = async (id: string) => {
    await fetch(`/api/notifications/${id}`, { method: "DELETE" });
    setConfigs(configs.filter((c) => c.ruleId !== id));
  };

  return (
    <div className="p-8 max-w-4xl">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Notifications</h1>
        <button onClick={() => setShowAdd(!showAdd)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
          Add Notification
        </button>
      </div>

      {showAdd && (
        <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
          <input placeholder="Rule ID" value={ruleId} onChange={(e) => setRuleId(e.target.value)} className="w-full border rounded px-3 py-2" />
          <select value={channelType} onChange={(e) => setChannelType(e.target.value)} className="w-full border rounded px-3 py-2">
            <option value="webhook">Webhook</option>
            <option value="email">Email</option>
            <option value="whatsapp">WhatsApp</option>
            <option value="dashboard">Dashboard Push</option>
          </select>
          <textarea placeholder='Channel config JSON, e.g. {"url": "https://example.com/hook"}' value={channelConfigStr}
            onChange={(e) => setChannelConfigStr(e.target.value)} className="w-full border rounded px-3 py-2 h-16 font-mono text-sm" />
          <div className="flex gap-2">
            <button onClick={handleAdd} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
            <button onClick={() => setShowAdd(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
          </div>
        </div>
      )}

      <div className="space-y-2">
        {configs.map((nc) => (
          <div key={nc.ruleId} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
            <div>
              <h3 className="font-medium font-mono text-sm">Rule: {nc.ruleId}</h3>
              <p className="text-sm text-gray-500">
                {nc.channels.map((ch) => ch.type).join(", ")} · {nc.channels.length} channel(s)
              </p>
            </div>
            <button onClick={() => handleDelete(nc.ruleId)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
          </div>
        ))}
        {configs.length === 0 && <p className="text-gray-500 text-center py-4">No notification configs. Add one to get started.</p>}
      </div>
    </div>
  );
}
