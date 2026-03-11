# OpenEye

Multi-service video analytics platform that converts camera streams into actionable events using configurable rules. Designed for retail stores, restaurants, factories, and warehouses.

```
Camera Streams ─▶ Frame Capture ─▶ Detection Bridge ─▶ Pipeline Core ─▶ Event Router ─▶ Dashboard
                                          │
                                   Roboflow Inference
```

## Architecture

| Service | Language | Role |
|---------|----------|------|
| **frame-capture** | C# | Connects to RTSP/MJPEG/USB streams, decimates to target FPS, publishes frames via Redis Streams |
| **detection-bridge** | C# | Sends frames to Roboflow Inference, publishes detections |
| **pipeline-core** | C# | Object tracking (SORT/IoU), zone evaluation (point-in-polygon, tripwire), primitive extraction, rule engine with temporal aggregation |
| **event-router** | C# | Persists events to PostgreSQL, dispatches notifications (webhook, email, WhatsApp, dashboard) |
| **dashboard** | TypeScript | Next.js config UI — cameras, zones, rules, notifications, event history |
| **roboflow-inference** | Prebuilt | Object detection model server |

**Infrastructure:** Redis 7 (Streams + pub/sub), PostgreSQL 16, .NET Aspire orchestration.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for Redis, PostgreSQL, Roboflow Inference)
- A [Roboflow](https://roboflow.com/) API key (optional — only for inference)

## Quick Start

### 1. Start infrastructure

```bash
cd docker
docker compose up -d redis postgres roboflow-inference
```

This starts the infrastructure dependencies: Redis, PostgreSQL (with schema from `init.sql`), and Roboflow Inference.

### 2. Set up the dashboard

```bash
cd dashboard
npm install
npx prisma generate
npx prisma db push
npm run dev
```

Dashboard is available at **http://localhost:3000**.

### 3. Run backend services via Aspire

```bash
cd src
dotnet run --project OpenEye.AppHost
```

The Aspire dashboard opens automatically with health monitoring for all services. Connection strings are injected automatically.

### 4. Configure cameras

Use the dashboard at http://localhost:3000/cameras to add camera sources, or edit `src/OpenEye.FrameCapture/appsettings.json`:

```json
{
  "Cameras": [
    { "Id": "cam-1", "Url": "rtsp://192.168.1.100:554/stream", "TargetFps": 5 }
  ]
}
```

## Configuration

Services auto-configure connection strings when run via Aspire. For standalone mode, configure in each service's `appsettings.json`:

| Setting | Service | Example |
|---------|---------|---------|
| `ConnectionStrings:redis` | All | `localhost:6379` |
| `ConnectionStrings:openeye` | pipeline-core, event-router | `Host=localhost;Database=openeye;Username=postgres;Password=postgres` |
| `Roboflow:Url` | detection-bridge | `http://localhost:9001` |
| `Roboflow:ModelId` | detection-bridge | `yolov8n-640` |
| `Roboflow:ApiKey` | detection-bridge | Your Roboflow API key |
| `Cameras` | frame-capture | Array of camera configs |
| `CameraIds` | detection-bridge, pipeline-core | `["cam-1"]` |

Environment variables: set `ROBOFLOW_API_KEY` for Docker Compose inference.

## Redis Streams Topology

```
frames:{cameraId}       frame-capture → detection-bridge
detections:{cameraId}   detection-bridge → pipeline-core
events                  pipeline-core → event-router
config:changed          dashboard → pipeline-core, detection-bridge (pub/sub)
```

## Data Model

| Table | Purpose |
|-------|---------|
| `cameras` | Stream sources with URL, type, FPS, enabled flag |
| `zones` | Named polygons (normalized coords) bound to a camera |
| `tripwires` | Line segments for crossing detection |
| `primitive_configs` | Feature extractors: presence, absence, count, zone_duration, speed, line_crossed |
| `rules` | Condition sets with temporal logic (sustained, within, cooldown), tied to zones/tripwires |
| `notification_configs` | Per-rule notification channels (webhook, email, WhatsApp, dashboard push) |
| `events` | Fired rule events with tracked objects and metadata |

## Pipeline Stages

1. **Object Tracking** — SORT-style IoU matching with Hungarian algorithm for consistent object IDs across frames
2. **Zone Evaluation** — Point-in-polygon tests and tripwire crossing detection using normalized coordinates
3. **Primitive Extraction** — Computes features (presence, count, duration, speed, line crossings) per zone per camera
4. **Rule Engine** — Evaluates condition sets (all/any logic) with temporal aggregation (sustained duration, sliding windows, min occurrences), cooldown-based deduplication
5. **Event Publishing** — Fires events to Redis `events` stream with optional evidence requests

## Project Structure

```
src/
├── OpenEye.AppHost/          .NET Aspire orchestrator
├── OpenEye.FrameCapture/     Camera stream capture worker
├── OpenEye.DetectionBridge/  Roboflow inference bridge worker
├── OpenEye.PipelineCore/     Tracking, zones, primitives, rules
├── OpenEye.EventRouter/      Event persistence and notifications
├── OpenEye.Shared/           Shared models, Redis/Postgres helpers
├── OpenEye.Abstractions/     Interfaces (IRuleEngine, IObjectTracker, etc.)
├── OpenEye.ServiceDefaults/  Aspire service defaults
├── OpenEye.Tests/            Unit tests
└── OpenEye.IntegrationTests/ Integration tests

dashboard/
├── src/app/                  Next.js App Router pages + API routes
├── src/components/           UI components (sidebar, rule builder)
├── src/lib/                  Prisma client, Redis client, Zod validations
└── prisma/schema.prisma      Database schema

docker/
├── docker-compose.yml        Full-stack services (infra + apps)
├── Dockerfile.dotnet         Multi-stage .NET build (all C# services)
├── Dockerfile.dashboard      Multi-stage Next.js build
└── init.sql                  PostgreSQL bootstrap schema
```

## Docker Deployment

To run the full stack without Aspire:

```bash
cd docker
docker compose up -d --build
```

This builds and starts all services (frame-capture, detection-bridge, pipeline-core, event-router, dashboard) alongside the infrastructure. Dashboard is available at **http://localhost:3000**.

## Testing

```bash
# .NET unit tests
dotnet test src/OpenEye.Tests --nologo

# Dashboard type checking
cd dashboard && npx tsc --noEmit
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, C#, Worker Services |
| Orchestration | .NET Aspire 9.5 |
| Computer Vision | OpenCvSharp4 |
| Object Detection | Roboflow Inference (bring-your-own model) |
| Message Bus | Redis 7 Streams + pub/sub (StackExchange.Redis) |
| Database | PostgreSQL 16 (Npgsql, Dapper, Prisma) |
| Dashboard | Next.js 15, React 19, TypeScript, Tailwind CSS 4 |
| Validation | Zod (dashboard API routes) |

## License

Private — all rights reserved.
