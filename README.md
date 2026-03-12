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

## Local Development

For local development, .NET Aspire orchestrates **everything** — backend services, Redis, PostgreSQL, Roboflow Inference, and the Next.js dashboard. No Docker Compose needed.

### 1. Install dashboard dependencies

```bash
cd dashboard
npm install
```

### 2. Run everything via Aspire

```bash
cd src
dotnet run --project OpenEye.AppHost
```

Aspire automatically starts Redis, PostgreSQL (with schema from `docker/init.sql`), Roboflow Inference, and all backend services. The Aspire dashboard opens automatically with health monitoring.

Dashboard is available at **http://localhost:3000**.

### 4. Configure cameras

Use the dashboard at http://localhost:3000/cameras to add camera sources, or edit `src/OpenEye.FrameCapture/appsettings.json`:

```json
{
  "Cameras": [
    { "Id": "cam-1", "Url": "rtsp://192.168.1.100:554/stream", "TargetFps": 5 }
  ]
}
```

## Detection Models

OpenEye uses [Roboflow Inference](https://github.com/roboflow/inference) as its model server. Inference is **fully open-source** and runs locally via Docker — no cloud dependency, no account required for built-in models.

### Built-in models (no API key needed)

Roboflow Inference ships with support for popular pretrained YOLO models that are downloaded automatically on first request:

| Model ID | Architecture | Classes |
|----------|-------------|--------|
| `yolov8n-640` | YOLOv8 Nano | COCO 80 classes (person, car, etc.) |
| `yolov8s-640` | YOLOv8 Small | COCO 80 classes |
| `yolov8m-640` | YOLOv8 Medium | COCO 80 classes |
| `yolov11n-640` | YOLOv11 Nano | COCO 80 classes |

The default is `yolov8n-640`. Change the model by setting `Roboflow:ModelId` in the detection-bridge config.

### Using a custom model

If you've trained your own YOLO model (e.g., for forklifts, PPE, specific products), you have two options:

**Option 1: Upload to Roboflow (requires API key)**
1. Train your model anywhere (Ultralytics, custom pipeline, etc.)
2. Upload weights to a [Roboflow](https://roboflow.com/) project
3. Set `Roboflow:ModelId` to `your-project/version` (e.g., `forklift-detection/3`)
4. Set `ROBOFLOW_API_KEY` so the inference server can pull the model

**Option 2: Mount weights locally (no API key)**
1. Export your model to ONNX or supported format
2. Mount the weights directory into the inference container:
   ```yaml
   roboflow-inference:
     volumes:
       - ./my-models:/models
   ```
3. Reference the model via the inference API

### What is the API key for?

The `ROBOFLOW_API_KEY` is **only** needed to download private models from Roboflow's cloud model registry. If you use built-in models (like `yolov8n-640`) or mount custom weights locally, **no API key is required**.

## Configuration

### Via Aspire (local development)

Connection strings are injected automatically — no manual configuration needed.

### Via `appsettings.json` (local standalone)

Each service reads its settings from `appsettings.json`:

| Setting | Service | Example |
|---------|---------|---------|
| `ConnectionStrings:redis` | All | `localhost:6379` |
| `ConnectionStrings:openeye` | pipeline-core, event-router | `Host=localhost;Database=openeye;Username=postgres;Password=postgres` |
| `Roboflow:Url` | detection-bridge | `http://localhost:9001` |
| `Roboflow:ModelId` | detection-bridge | `yolov8n-640` (see [Detection Models](#detection-models)) |
| `Roboflow:ApiKey` | detection-bridge | Optional — only for Roboflow-hosted models |
| `Cameras` | frame-capture | Array of camera configs |
| `CameraIds` | detection-bridge, pipeline-core | `["cam-1"]` |

### Via environment variables (Docker Compose)

When running with Docker Compose, configure services through environment variables in `docker-compose.yml` — no rebuild required. .NET uses `__` (double underscore) as the nesting separator:

| Environment Variable | `appsettings.json` Equivalent |
|---------------------|-------------------------------|
| `ConnectionStrings__redis` | `ConnectionStrings:redis` |
| `ConnectionStrings__openeye` | `ConnectionStrings:openeye` |
| `Roboflow__Url` | `Roboflow:Url` |
| `Roboflow__ModelId` | `Roboflow:ModelId` |
| `Roboflow__ApiKey` | `Roboflow:ApiKey` |

Example override in `docker-compose.yml`:
```yaml
detection-bridge:
  environment:
    Roboflow__ModelId: my-custom-model/2
    Roboflow__ApiKey: your-key-here
```

After changing environment variables, restart the affected service: `docker compose restart detection-bridge`

## Redis Streams Topology

```
frames:{cameraId}       frame-capture → detection-bridge
detections:{cameraId}   detection-bridge → pipeline-core
events                  pipeline-core → event-router
config:changed          dashboard → pipeline-core, detection-bridge (pub/sub)
```

## Database Schema

The project uses a **database-first** approach. `docker/init.sql` is the single source of truth for the schema — it runs automatically when PostgreSQL starts (via Aspire or Docker Compose).

The Next.js dashboard uses Prisma only as a **typed query client**, not as a schema manager. When you change the database schema:

1. Edit `docker/init.sql`
2. Recreate the database (or run `ALTER TABLE` manually)
3. Run `npx prisma db pull` in `dashboard/` to sync `schema.prisma` from the live DB
4. Run `npx prisma generate` to regenerate the TypeScript client

> **Do not** use `prisma db push` or `prisma migrate` — the .NET backend services query these tables directly, and `init.sql` must remain the authority.

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
├── docker-compose.yml        Production deployment (infra + apps)
├── Dockerfile.dotnet         Multi-stage .NET build (all C# services)
├── Dockerfile.dashboard      Multi-stage Next.js build
└── init.sql                  PostgreSQL bootstrap schema
```

## Production Deployment

To run the full stack in Docker (no local SDKs required):

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
