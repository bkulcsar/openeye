"use client";

import { useEffect, useState } from "react";

interface Camera {
  id: string;
  name: string;
  url: string;
  targetFps: number;
  enabled: boolean;
  type: string;
  createdAt: string;
  updatedAt: string;
  zones: { id: string; name: string }[];
}

interface CameraForm {
  name: string;
  url: string;
  targetFps: number;
  enabled: boolean;
}

const emptyForm: CameraForm = { name: "", url: "", targetFps: 5, enabled: true };

export default function CamerasPage() {
  const [cameras, setCameras] = useState<Camera[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<CameraForm>(emptyForm);
  const [deleting, setDeleting] = useState<string | null>(null);

  useEffect(() => {
    fetch("/api/cameras")
      .then((r) => r.json())
      .then(setCameras);
  }, []);

  const resetForm = () => {
    setForm(emptyForm);
    setShowForm(false);
    setEditingId(null);
  };

  const handleSave = async () => {
    const method = editingId ? "PUT" : "POST";
    const url = editingId ? `/api/cameras/${editingId}` : "/api/cameras";

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(form),
    });

    if (res.ok) {
      const saved = await res.json();
      if (editingId) {
        setCameras(cameras.map((c) => (c.id === saved.id ? { ...c, ...saved } : c)));
      } else {
        setCameras([{ ...saved, zones: [] }, ...cameras]);
      }
      resetForm();
    }
  };

  const handleEdit = (camera: Camera) => {
    setForm({
      name: camera.name,
      url: camera.url,
      targetFps: camera.targetFps,
      enabled: camera.enabled,
    });
    setEditingId(camera.id);
    setShowForm(true);
  };

  const handleDelete = async (id: string) => {
    await fetch(`/api/cameras/${id}`, { method: "DELETE" });
    setCameras(cameras.filter((c) => c.id !== id));
    setDeleting(null);
  };

  const handleToggle = async (camera: Camera) => {
    const res = await fetch(`/api/cameras/${camera.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ enabled: !camera.enabled }),
    });
    if (res.ok) {
      const updated = await res.json();
      setCameras(cameras.map((c) => (c.id === updated.id ? { ...c, ...updated } : c)));
    }
  };

  const truncateUrl = (url: string, max = 40) =>
    url.length > max ? url.slice(0, max) + "…" : url;

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Cameras</h1>
        <button
          onClick={() => { resetForm(); setShowForm(true); }}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
        >
          Add Camera
        </button>
      </div>

      {showForm && (
        <div className="border rounded-lg p-4 bg-white shadow-sm mb-6">
          <h2 className="font-medium mb-3">{editingId ? "Edit Camera" : "New Camera"}</h2>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="w-full border rounded-md px-3 py-2 text-sm"
                placeholder="Front Door Camera"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Stream URL</label>
              <input
                type="text"
                value={form.url}
                onChange={(e) => setForm({ ...form, url: e.target.value })}
                className="w-full border rounded-md px-3 py-2 text-sm"
                placeholder="rtsp://192.168.1.100:554/stream"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Target FPS</label>
              <input
                type="number"
                value={form.targetFps}
                onChange={(e) => setForm({ ...form, targetFps: Number(e.target.value) })}
                className="w-full border rounded-md px-3 py-2 text-sm"
                min={1}
                max={30}
              />
            </div>
            <div className="flex items-end">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={form.enabled}
                  onChange={(e) => setForm({ ...form, enabled: e.target.checked })}
                  className="rounded"
                />
                Enabled
              </label>
            </div>
          </div>
          <div className="flex gap-2 mt-4">
            <button
              onClick={handleSave}
              disabled={!form.name || !form.url}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {editingId ? "Update" : "Create"}
            </button>
            <button
              onClick={resetForm}
              className="px-4 py-2 border rounded-lg hover:bg-gray-50"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      <div className="space-y-3">
        {cameras.map((camera) => (
          <div key={camera.id} className="border rounded-lg p-4 bg-white shadow-sm">
            <div className="flex items-center justify-between">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <h3 className="font-medium">{camera.name}</h3>
                  <span
                    className={`text-xs px-2 py-0.5 rounded-full ${
                      camera.enabled
                        ? "bg-green-100 text-green-700"
                        : "bg-gray-100 text-gray-500"
                    }`}
                  >
                    {camera.enabled ? "Active" : "Disabled"}
                  </span>
                </div>
                <p className="text-sm text-gray-500 mt-1">
                  {truncateUrl(camera.url)} · {camera.targetFps} FPS · {camera.zones?.length ?? 0} zone(s)
                </p>
              </div>
              <div className="flex gap-2 ml-4">
                <button
                  onClick={() => handleToggle(camera)}
                  className="text-sm text-gray-600 hover:text-gray-800"
                >
                  {camera.enabled ? "Disable" : "Enable"}
                </button>
                <button
                  onClick={() => handleEdit(camera)}
                  className="text-sm text-blue-600 hover:text-blue-800"
                >
                  Edit
                </button>
                {deleting === camera.id ? (
                  <>
                    <span className="text-sm text-red-600">Confirm?</span>
                    <button
                      onClick={() => handleDelete(camera.id)}
                      className="text-sm text-red-700 font-medium hover:text-red-900"
                    >
                      Yes
                    </button>
                    <button
                      onClick={() => setDeleting(null)}
                      className="text-sm text-gray-600 hover:text-gray-800"
                    >
                      No
                    </button>
                  </>
                ) : (
                  <button
                    onClick={() => setDeleting(camera.id)}
                    className="text-sm text-red-600 hover:text-red-800"
                  >
                    Delete
                  </button>
                )}
              </div>
            </div>
          </div>
        ))}
        {cameras.length === 0 && (
          <p className="text-gray-500 text-center py-8">No cameras yet. Add one to get started.</p>
        )}
      </div>
    </div>
  );
}
