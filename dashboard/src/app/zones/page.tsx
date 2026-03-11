"use client";

import { useEffect, useState } from "react";

interface Zone {
  id: string;
  name: string;
  cameraId: string;
  polygon: unknown;
  type: string;
}

interface Tripwire {
  id: string;
  sourceId: string;
  startX: number;
  startY: number;
  endX: number;
  endY: number;
}

export default function ZonesPage() {
  const [zones, setZones] = useState<Zone[]>([]);
  const [tripwires, setTripwires] = useState<Tripwire[]>([]);
  const [showAddZone, setShowAddZone] = useState(false);
  const [showAddTripwire, setShowAddTripwire] = useState(false);
  const [zoneName, setZoneName] = useState("");
  const [zoneCameraId, setZoneCameraId] = useState("");
  const [zonePolygon, setZonePolygon] = useState("");
  const [tripwireSourceId, setTripwireSourceId] = useState("");
  const [tripwireCoords, setTripwireCoords] = useState({ startX: 0, startY: 0, endX: 1, endY: 1 });

  useEffect(() => {
    fetch("/api/zones").then((r) => r.json()).then(setZones);
    fetch("/api/tripwires").then((r) => r.json()).then(setTripwires);
  }, []);

  const handleAddZone = async () => {
    let polygon;
    try { polygon = JSON.parse(zonePolygon); } catch { return; }
    const res = await fetch("/api/zones", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: zoneName, cameraId: zoneCameraId, polygon }),
    });
    if (res.ok) {
      const zone = await res.json();
      setZones([zone, ...zones]);
      setShowAddZone(false);
      setZoneName("");
      setZoneCameraId("");
      setZonePolygon("");
    }
  };

  const handleDeleteZone = async (id: string) => {
    await fetch(`/api/zones/${id}`, { method: "DELETE" });
    setZones(zones.filter((z) => z.id !== id));
  };

  const handleAddTripwire = async () => {
    const res = await fetch("/api/tripwires", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sourceId: tripwireSourceId, ...tripwireCoords }),
    });
    if (res.ok) {
      const tw = await res.json();
      setTripwires([tw, ...tripwires]);
      setShowAddTripwire(false);
      setTripwireSourceId("");
      setTripwireCoords({ startX: 0, startY: 0, endX: 1, endY: 1 });
    }
  };

  const handleDeleteTripwire = async (id: string) => {
    await fetch(`/api/tripwires/${id}`, { method: "DELETE" });
    setTripwires(tripwires.filter((t) => t.id !== id));
  };

  return (
    <div className="p-8 max-w-4xl">
      {/* Zones Section */}
      <div className="mb-10">
        <div className="flex items-center justify-between mb-4">
          <h1 className="text-2xl font-bold">Zones</h1>
          <button onClick={() => setShowAddZone(!showAddZone)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
            Add Zone
          </button>
        </div>

        {showAddZone && (
          <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
            <input placeholder="Zone Name" value={zoneName} onChange={(e) => setZoneName(e.target.value)} className="w-full border rounded px-3 py-2" />
            <input placeholder="Camera ID" value={zoneCameraId} onChange={(e) => setZoneCameraId(e.target.value)} className="w-full border rounded px-3 py-2" />
            <textarea placeholder='Polygon JSON, e.g. [{"x":0.2,"y":0.2},{"x":0.8,"y":0.2},{"x":0.8,"y":0.8},{"x":0.2,"y":0.8}]' value={zonePolygon} onChange={(e) => setZonePolygon(e.target.value)} className="w-full border rounded px-3 py-2 h-20 font-mono text-sm" />
            <div className="flex gap-2">
              <button onClick={handleAddZone} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
              <button onClick={() => setShowAddZone(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
            </div>
          </div>
        )}

        <div className="space-y-2">
          {zones.map((zone) => (
            <div key={zone.id} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
              <div>
                <h3 className="font-medium">{zone.name}</h3>
                <p className="text-sm text-gray-500">Camera: {zone.cameraId} · Type: {zone.type}</p>
              </div>
              <button onClick={() => handleDeleteZone(zone.id)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
            </div>
          ))}
          {zones.length === 0 && <p className="text-gray-500 text-center py-4">No zones configured.</p>}
        </div>
      </div>

      {/* Tripwires Section */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-2xl font-bold">Tripwires</h2>
          <button onClick={() => setShowAddTripwire(!showAddTripwire)} className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
            Add Tripwire
          </button>
        </div>

        {showAddTripwire && (
          <div className="border rounded-lg p-4 bg-white shadow-sm mb-4 space-y-3">
            <input placeholder="Camera ID" value={tripwireSourceId} onChange={(e) => setTripwireSourceId(e.target.value)} className="w-full border rounded px-3 py-2" />
            <div className="grid grid-cols-2 gap-2">
              <input type="number" step="0.01" min="0" max="1" placeholder="Start X" value={tripwireCoords.startX}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, startX: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="Start Y" value={tripwireCoords.startY}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, startY: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="End X" value={tripwireCoords.endX}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, endX: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
              <input type="number" step="0.01" min="0" max="1" placeholder="End Y" value={tripwireCoords.endY}
                onChange={(e) => setTripwireCoords({ ...tripwireCoords, endY: parseFloat(e.target.value) })} className="border rounded px-3 py-2" />
            </div>
            <div className="flex gap-2">
              <button onClick={handleAddTripwire} className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
              <button onClick={() => setShowAddTripwire(false)} className="px-4 py-2 border rounded text-gray-600 hover:bg-gray-50">Cancel</button>
            </div>
          </div>
        )}

        <div className="space-y-2">
          {tripwires.map((tw) => (
            <div key={tw.id} className="border rounded-lg p-4 bg-white shadow-sm flex items-center justify-between">
              <div>
                <h3 className="font-medium font-mono text-sm">{tw.id}</h3>
                <p className="text-sm text-gray-500">
                  Camera: {tw.sourceId} · ({tw.startX.toFixed(2)}, {tw.startY.toFixed(2)}) → ({tw.endX.toFixed(2)}, {tw.endY.toFixed(2)})
                </p>
              </div>
              <button onClick={() => handleDeleteTripwire(tw.id)} className="text-sm text-red-600 hover:text-red-800">Delete</button>
            </div>
          ))}
          {tripwires.length === 0 && <p className="text-gray-500 text-center py-4">No tripwires configured.</p>}
        </div>
      </div>
    </div>
  );
}
