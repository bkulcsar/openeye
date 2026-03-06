# OpenEye Framework Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a C#/.NET in-process library that converts raw YOLO-style camera detections into higher-level semantic events using configurable YAML rules, with built-in object tracking and spatial zone evaluation.

**Architecture:** Pipeline of four stages — Object Tracker → Zone Evaluator → Rule Engine → Event Stream. Each stage is an independent, testable component. Detections flow through the pipeline per-frame, accumulating tracking and zone context before rule evaluation.

**Tech Stack:** .NET 8, C#, xUnit, YamlDotNet. No other runtime dependencies.

**Design doc:** `docs/plans/2026-03-06-openeye-framework-design.md`

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `OpenEye.sln`
- Create: `src/OpenEye/OpenEye.csproj`
- Create: `tests/OpenEye.Tests/OpenEye.Tests.csproj`
- Create: `tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj`
- Create: `.gitignore`

**Step 1: Create .gitignore**

```
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
```

**Step 2: Create solution and projects**

```bash
cd /c/Repos/openeye
dotnet new sln -n OpenEye
dotnet new classlib -n OpenEye -o src/OpenEye -f net8.0
dotnet new xunit -n OpenEye.Tests -o tests/OpenEye.Tests -f net8.0
dotnet new xunit -n OpenEye.IntegrationTests -o tests/OpenEye.IntegrationTests -f net8.0
```

**Step 3: Wire up solution references**

```bash
dotnet sln add src/OpenEye/OpenEye.csproj
dotnet sln add tests/OpenEye.Tests/OpenEye.Tests.csproj
dotnet sln add tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj
dotnet add tests/OpenEye.Tests reference src/OpenEye/OpenEye.csproj
dotnet add tests/OpenEye.IntegrationTests reference src/OpenEye/OpenEye.csproj
```

**Step 4: Add YamlDotNet dependency**

```bash
dotnet add src/OpenEye package YamlDotNet
```

**Step 5: Delete placeholder files, verify build**

Delete `src/OpenEye/Class1.cs`, `tests/OpenEye.Tests/UnitTest1.cs`, `tests/OpenEye.IntegrationTests/UnitTest1.cs`.

```bash
dotnet build
dotnet test
```

Expected: Build succeeds, 0 tests run.

**Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with core library and test projects"
```

---

## Task 2: Core Models

**Files:**
- Create: `src/OpenEye/Models/BoundingBox.cs`
- Create: `src/OpenEye/Models/PointF.cs`
- Create: `src/OpenEye/Models/Detection.cs`
- Create: `src/OpenEye/Models/TrackState.cs`
- Create: `src/OpenEye/Models/TrackedObject.cs`
- Create: `src/OpenEye/Models/TrajectoryPoint.cs`
- Create: `src/OpenEye/Models/Zone.cs`
- Create: `src/OpenEye/Models/Tripwire.cs`
- Create: `src/OpenEye/Models/TripwireDirection.cs`
- Create: `src/OpenEye/Models/OpenEyeEvent.cs`
- Create: `src/OpenEye/Models/Severity.cs`
- Test: `tests/OpenEye.Tests/Models/BoundingBoxTests.cs`

**Step 1: Write BoundingBox tests**

```csharp
// tests/OpenEye.Tests/Models/BoundingBoxTests.cs
using OpenEye.Models;

namespace OpenEye.Tests.Models;

public class BoundingBoxTests
{
    [Fact]
    public void Centroid_ReturnsCenter()
    {
        var box = new BoundingBox(0.1f, 0.2f, 0.4f, 0.6f);
        var centroid = box.Centroid;
        Assert.Equal(0.3f, centroid.X, precision: 5);
        Assert.Equal(0.5f, centroid.Y, precision: 5);
    }

    [Fact]
    public void Area_ReturnsWidthTimesHeight()
    {
        var box = new BoundingBox(0.0f, 0.0f, 0.5f, 0.4f);
        Assert.Equal(0.2f, box.Area, precision: 5);
    }

    [Fact]
    public void IoU_IdenticalBoxes_ReturnsOne()
    {
        var box = new BoundingBox(0.1f, 0.1f, 0.3f, 0.3f);
        Assert.Equal(1.0f, BoundingBox.IoU(box, box), precision: 5);
    }

    [Fact]
    public void IoU_NonOverlapping_ReturnsZero()
    {
        var a = new BoundingBox(0.0f, 0.0f, 0.1f, 0.1f);
        var b = new BoundingBox(0.5f, 0.5f, 0.1f, 0.1f);
        Assert.Equal(0.0f, BoundingBox.IoU(a, b), precision: 5);
    }

    [Fact]
    public void IoU_PartialOverlap_ReturnsCorrectValue()
    {
        // Box A: x=0, y=0, w=0.4, h=0.4 → covers [0,0.4] x [0,0.4]
        // Box B: x=0.2, y=0.2, w=0.4, h=0.4 → covers [0.2,0.6] x [0.2,0.6]
        // Intersection: [0.2,0.4] x [0.2,0.4] = 0.2*0.2 = 0.04
        // Union: 0.16 + 0.16 - 0.04 = 0.28
        // IoU = 0.04 / 0.28 ≈ 0.142857
        var a = new BoundingBox(0.0f, 0.0f, 0.4f, 0.4f);
        var b = new BoundingBox(0.2f, 0.2f, 0.4f, 0.4f);
        Assert.Equal(0.142857f, BoundingBox.IoU(a, b), precision: 4);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~BoundingBoxTests" -v n
```

Expected: FAIL — `BoundingBox` type does not exist.

**Step 3: Implement all core models**

```csharp
// src/OpenEye/Models/PointF.cs
namespace OpenEye.Models;

public readonly record struct PointF(float X, float Y);
```

```csharp
// src/OpenEye/Models/BoundingBox.cs
namespace OpenEye.Models;

public readonly record struct BoundingBox(float X, float Y, float Width, float Height)
{
    public PointF Centroid => new(X + Width / 2, Y + Height / 2);
    public float Area => Width * Height;

    public static float IoU(BoundingBox a, BoundingBox b)
    {
        float x1 = Math.Max(a.X, b.X);
        float y1 = Math.Max(a.Y, b.Y);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        float interWidth = Math.Max(0, x2 - x1);
        float interHeight = Math.Max(0, y2 - y1);
        float interArea = interWidth * interHeight;

        float unionArea = a.Area + b.Area - interArea;
        return unionArea == 0 ? 0 : interArea / unionArea;
    }
}
```

```csharp
// src/OpenEye/Models/Detection.cs
namespace OpenEye.Models;

public sealed record Detection(
    string ClassLabel,
    BoundingBox Box,
    float Confidence,
    DateTimeOffset Timestamp,
    string SourceId,
    long FrameIndex = 0);
```

```csharp
// src/OpenEye/Models/TrackState.cs
namespace OpenEye.Models;

public enum TrackState { Active, Lost, Expired }
```

```csharp
// src/OpenEye/Models/TrajectoryPoint.cs
namespace OpenEye.Models;

public readonly record struct TrajectoryPoint(PointF Position, DateTimeOffset Timestamp);
```

```csharp
// src/OpenEye/Models/TrackedObject.cs
namespace OpenEye.Models;

public sealed class TrackedObject
{
    public string TrackId { get; }
    public string ClassLabel { get; set; }
    public BoundingBox CurrentBox { get; set; }
    public List<TrajectoryPoint> Trajectory { get; } = new();
    public DateTimeOffset FirstSeen { get; }
    public DateTimeOffset LastSeen { get; set; }
    public TrackState State { get; set; } = TrackState.Active;
    public Dictionary<string, object> Metadata { get; } = new();
    public int MissedFrames { get; set; }

    public TrackedObject(string trackId, string classLabel, BoundingBox box, DateTimeOffset timestamp)
    {
        TrackId = trackId;
        ClassLabel = classLabel;
        CurrentBox = box;
        FirstSeen = timestamp;
        LastSeen = timestamp;
        Trajectory.Add(new TrajectoryPoint(box.Centroid, timestamp));
    }

    public void Update(BoundingBox box, DateTimeOffset timestamp, int trajectoryWindow)
    {
        CurrentBox = box;
        LastSeen = timestamp;
        MissedFrames = 0;
        State = TrackState.Active;
        Trajectory.Add(new TrajectoryPoint(box.Centroid, timestamp));
        if (Trajectory.Count > trajectoryWindow)
            Trajectory.RemoveAt(0);
    }
}
```

```csharp
// src/OpenEye/Models/Zone.cs
namespace OpenEye.Models;

public sealed record Zone(string ZoneId, List<PointF> Polygon, string SourceId);
```

```csharp
// src/OpenEye/Models/TripwireDirection.cs
namespace OpenEye.Models;

public enum TripwireDirection { Any, LeftToRight, RightToLeft, TopToBottom, BottomToTop }
```

```csharp
// src/OpenEye/Models/Tripwire.cs
namespace OpenEye.Models;

public sealed record Tripwire(
    string TripwireId,
    PointF PointA,
    PointF PointB,
    string SourceId,
    TripwireDirection Direction = TripwireDirection.Any);
```

```csharp
// src/OpenEye/Models/Severity.cs
namespace OpenEye.Models;

public enum Severity { Info, Warning, Critical }
```

```csharp
// src/OpenEye/Models/OpenEyeEvent.cs
namespace OpenEye.Models;

public sealed record OpenEyeEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string SourceId,
    string? ZoneId,
    IReadOnlyList<TrackedObject> TrackedObjects,
    string RuleId,
    Severity Severity = Severity.Info,
    Dictionary<string, object>? Metadata = null);
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~BoundingBoxTests" -v n
```

Expected: 5 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add core data models with BoundingBox IoU"
```

---

## Task 3: Hungarian Algorithm

The tracker needs the Hungarian algorithm (Kuhn-Munkres) for optimal detection-to-track assignment. This is a standalone utility class.

**Files:**
- Create: `src/OpenEye/Tracking/HungarianAlgorithm.cs`
- Test: `tests/OpenEye.Tests/Tracking/HungarianAlgorithmTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Tracking/HungarianAlgorithmTests.cs
using OpenEye.Tracking;

namespace OpenEye.Tests.Tracking;

public class HungarianAlgorithmTests
{
    [Fact]
    public void Solve_2x2_ReturnsOptimalAssignment()
    {
        // Cost matrix:
        // [1, 2]
        // [3, 0]
        // Optimal: row0→col0 (cost 1), row1→col1 (cost 0) = total 1
        var cost = new float[,] { { 1, 2 }, { 3, 0 } };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(0, result[0]); // row 0 → col 0
        Assert.Equal(1, result[1]); // row 1 → col 1
    }

    [Fact]
    public void Solve_3x3_ReturnsOptimalAssignment()
    {
        // Cost matrix:
        // [10, 5, 13]
        // [ 3, 7,  2]
        // [ 6, 9, 11]
        // Optimal: row0→col1 (5), row1→col2 (2), row2→col0 (6) = total 13
        var cost = new float[,] { { 10, 5, 13 }, { 3, 7, 2 }, { 6, 9, 11 } };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(0, result[2]);
    }

    [Fact]
    public void Solve_MoreRowsThanCols_UnassignedRowsGetMinusOne()
    {
        // 3 rows, 2 cols → one row unassigned
        var cost = new float[,] { { 1, 9 }, { 9, 1 }, { 5, 5 } };
        var result = HungarianAlgorithm.Solve(cost);

        // Two rows should be assigned, one should be -1
        int assigned = result.Count(r => r >= 0);
        Assert.Equal(2, assigned);
    }

    [Fact]
    public void Solve_1x1_ReturnsSingleAssignment()
    {
        var cost = new float[,] { { 42 } };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Single(result);
        Assert.Equal(0, result[0]);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~HungarianAlgorithmTests" -v n
```

Expected: FAIL — `HungarianAlgorithm` does not exist.

**Step 3: Implement Hungarian algorithm**

```csharp
// src/OpenEye/Tracking/HungarianAlgorithm.cs
namespace OpenEye.Tracking;

/// <summary>
/// Hungarian algorithm (Kuhn-Munkres) for minimum-cost assignment.
/// Given an NxM cost matrix, returns an array of length N where result[i]
/// is the column assigned to row i, or -1 if unassigned.
/// Handles non-square matrices by padding with a large value.
/// </summary>
public static class HungarianAlgorithm
{
    public static int[] Solve(float[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);
        int n = Math.Max(rows, cols);

        // Pad to square matrix
        var c = new float[n, n];
        float large = float.MaxValue / 2;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                c[i, j] = (i < rows && j < cols) ? costMatrix[i, j] : large;

        var u = new float[n + 1];
        var v = new float[n + 1];
        var p = new int[n + 1];   // p[j] = row assigned to col j
        var way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            var minv = new float[n + 1];
            var used = new bool[n + 1];
            Array.Fill(minv, float.MaxValue);

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                float delta = float.MaxValue;
                int j1 = -1;

                for (int j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    float cur = c[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        // Build result: result[row] = assigned col, or -1
        var result = new int[rows];
        Array.Fill(result, -1);
        for (int j = 1; j <= n; j++)
        {
            if (p[j] > 0 && p[j] <= rows && j <= cols)
                result[p[j] - 1] = j - 1;
        }

        return result;
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~HungarianAlgorithmTests" -v n
```

Expected: 4 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement Hungarian algorithm for optimal assignment"
```

---

## Task 4: Object Tracker (SORT-style)

**Files:**
- Create: `src/OpenEye/Tracking/TrackerOptions.cs`
- Create: `src/OpenEye/Tracking/ObjectTracker.cs`
- Test: `tests/OpenEye.Tests/Tracking/ObjectTrackerTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Tracking/ObjectTrackerTests.cs
using OpenEye.Models;
using OpenEye.Tracking;

namespace OpenEye.Tests.Tracking;

public class ObjectTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ObjectTracker _tracker = new(new TrackerOptions());

    private Detection Det(string label, float x, float y, float w, float h, int frameOffset = 0)
        => new(label, new BoundingBox(x, y, w, h), 0.9f, T0.AddSeconds(frameOffset), "cam-1", frameOffset);

    [Fact]
    public void FirstFrame_CreatesNewTracks()
    {
        var detections = new[] { Det("person", 0.1f, 0.1f, 0.2f, 0.3f) };
        var tracks = _tracker.Update(detections);

        Assert.Single(tracks);
        Assert.Equal("person", tracks[0].ClassLabel);
        Assert.Equal(TrackState.Active, tracks[0].State);
    }

    [Fact]
    public void SecondFrame_SamePosition_MaintainsTrackId()
    {
        var d1 = new[] { Det("person", 0.1f, 0.1f, 0.2f, 0.3f, 0) };
        var d2 = new[] { Det("person", 0.11f, 0.11f, 0.2f, 0.3f, 1) };

        var t1 = _tracker.Update(d1);
        var t2 = _tracker.Update(d2);

        Assert.Single(t2.Where(t => t.State == TrackState.Active));
        Assert.Equal(t1[0].TrackId, t2.First(t => t.State == TrackState.Active).TrackId);
    }

    [Fact]
    public void MissedFrames_TrackBecomesLost()
    {
        var d1 = new[] { Det("person", 0.1f, 0.1f, 0.2f, 0.3f, 0) };
        _tracker.Update(d1);

        // Feed empty frames
        for (int i = 1; i <= 5; i++)
            _tracker.Update(Array.Empty<Detection>());

        var tracks = _tracker.GetActiveTracks();
        var lost = tracks.Where(t => t.State == TrackState.Lost).ToList();
        Assert.Single(lost);
    }

    [Fact]
    public void ExceededMaxLost_TrackExpires()
    {
        var tracker = new ObjectTracker(new TrackerOptions { MaxLostFrames = 3 });
        var d1 = new[] { Det("person", 0.1f, 0.1f, 0.2f, 0.3f, 0) };
        tracker.Update(d1);

        // Feed 4 empty frames (exceeds max_lost_frames=3)
        for (int i = 1; i <= 4; i++)
            tracker.Update(Array.Empty<Detection>());

        var tracks = tracker.GetActiveTracks();
        Assert.Empty(tracks.Where(t => t.State != TrackState.Expired));
    }

    [Fact]
    public void TwoObjects_TrackedIndependently()
    {
        var d1 = new[]
        {
            Det("person", 0.1f, 0.1f, 0.1f, 0.1f, 0),
            Det("person", 0.8f, 0.8f, 0.1f, 0.1f, 0),
        };
        var d2 = new[]
        {
            Det("person", 0.11f, 0.11f, 0.1f, 0.1f, 1),
            Det("person", 0.81f, 0.81f, 0.1f, 0.1f, 1),
        };

        _tracker.Update(d1);
        var tracks = _tracker.Update(d2);
        var active = tracks.Where(t => t.State == TrackState.Active).ToList();

        Assert.Equal(2, active.Count);
        Assert.NotEqual(active[0].TrackId, active[1].TrackId);
    }

    [Fact]
    public void Trajectory_AccumulatesPositions()
    {
        _tracker.Update(new[] { Det("person", 0.1f, 0.1f, 0.2f, 0.3f, 0) });
        _tracker.Update(new[] { Det("person", 0.15f, 0.15f, 0.2f, 0.3f, 1) });
        _tracker.Update(new[] { Det("person", 0.2f, 0.2f, 0.2f, 0.3f, 2) });

        var track = _tracker.GetActiveTracks().First(t => t.State == TrackState.Active);
        Assert.Equal(3, track.Trajectory.Count);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ObjectTrackerTests" -v n
```

Expected: FAIL — types don't exist.

**Step 3: Implement TrackerOptions and ObjectTracker**

```csharp
// src/OpenEye/Tracking/TrackerOptions.cs
namespace OpenEye.Tracking;

public sealed class TrackerOptions
{
    public int MaxLostFrames { get; set; } = 30;
    public float IouThreshold { get; set; } = 0.3f;
    public int TrajectoryWindow { get; set; } = 50;
}
```

```csharp
// src/OpenEye/Tracking/ObjectTracker.cs
using OpenEye.Models;

namespace OpenEye.Tracking;

public sealed class ObjectTracker
{
    private readonly TrackerOptions _options;
    private readonly List<TrackedObject> _tracks = new();
    private int _nextId;

    public ObjectTracker(TrackerOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections)
    {
        var activeTracks = _tracks.Where(t => t.State != TrackState.Expired).ToList();

        if (activeTracks.Count == 0 && detections.Count == 0)
            return _tracks.AsReadOnly();

        if (activeTracks.Count == 0)
        {
            // All detections become new tracks
            foreach (var det in detections)
                CreateTrack(det);
            return _tracks.AsReadOnly();
        }

        if (detections.Count == 0)
        {
            // All active tracks become lost/expired
            foreach (var track in activeTracks)
                IncrementMissed(track);
            return _tracks.AsReadOnly();
        }

        // Build IoU cost matrix (rows=tracks, cols=detections)
        var costMatrix = new float[activeTracks.Count, detections.Count];
        for (int i = 0; i < activeTracks.Count; i++)
            for (int j = 0; j < detections.Count; j++)
            {
                float iou = BoundingBox.IoU(activeTracks[i].CurrentBox, detections[j].Box);
                // Only match same class
                if (activeTracks[i].ClassLabel != detections[j].ClassLabel)
                    iou = 0;
                costMatrix[i, j] = 1.0f - iou; // Convert similarity to cost
            }

        var assignment = HungarianAlgorithm.Solve(costMatrix);
        var matchedDetections = new HashSet<int>();

        for (int i = 0; i < activeTracks.Count; i++)
        {
            int j = assignment[i];
            if (j >= 0 && j < detections.Count)
            {
                float iou = BoundingBox.IoU(activeTracks[i].CurrentBox, detections[j].Box);
                bool sameClass = activeTracks[i].ClassLabel == detections[j].ClassLabel;
                if (sameClass && iou >= _options.IouThreshold)
                {
                    activeTracks[i].Update(detections[j].Box, detections[j].Timestamp, _options.TrajectoryWindow);
                    matchedDetections.Add(j);
                    continue;
                }
            }
            IncrementMissed(activeTracks[i]);
        }

        // Unmatched detections become new tracks
        for (int j = 0; j < detections.Count; j++)
        {
            if (!matchedDetections.Contains(j))
                CreateTrack(detections[j]);
        }

        return _tracks.AsReadOnly();
    }

    public IReadOnlyList<TrackedObject> GetActiveTracks()
        => _tracks.Where(t => t.State != TrackState.Expired).ToList().AsReadOnly();

    private void CreateTrack(Detection det)
    {
        var track = new TrackedObject($"track-{_nextId++}", det.ClassLabel, det.Box, det.Timestamp);
        _tracks.Add(track);
    }

    private void IncrementMissed(TrackedObject track)
    {
        track.MissedFrames++;
        if (track.MissedFrames > _options.MaxLostFrames)
            track.State = TrackState.Expired;
        else
            track.State = TrackState.Lost;
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ObjectTrackerTests" -v n
```

Expected: 6 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement SORT-style object tracker with IoU matching"
```

---

## Task 5: Geometry Utilities (Point-in-Polygon, Line Crossing)

**Files:**
- Create: `src/OpenEye/Zones/Geometry.cs`
- Test: `tests/OpenEye.Tests/Zones/GeometryTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Zones/GeometryTests.cs
using OpenEye.Models;
using OpenEye.Zones;

namespace OpenEye.Tests.Zones;

public class GeometryTests
{
    // Simple square zone: [0.2,0.2] to [0.8,0.8]
    private static readonly List<PointF> Square = new()
    {
        new(0.2f, 0.2f), new(0.8f, 0.2f), new(0.8f, 0.8f), new(0.2f, 0.8f)
    };

    [Fact]
    public void PointInPolygon_InsideSquare_ReturnsTrue()
    {
        Assert.True(Geometry.PointInPolygon(new PointF(0.5f, 0.5f), Square));
    }

    [Fact]
    public void PointInPolygon_OutsideSquare_ReturnsFalse()
    {
        Assert.False(Geometry.PointInPolygon(new PointF(0.1f, 0.1f), Square));
    }

    [Fact]
    public void PointInPolygon_OnEdge_ReturnsTrueOrFalseConsistently()
    {
        // Edge behavior: just verify it doesn't throw
        var result = Geometry.PointInPolygon(new PointF(0.2f, 0.5f), Square);
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void PointInPolygon_Triangle()
    {
        var triangle = new List<PointF>
        {
            new(0.0f, 0.0f), new(1.0f, 0.0f), new(0.5f, 1.0f)
        };
        Assert.True(Geometry.PointInPolygon(new PointF(0.5f, 0.3f), triangle));
        Assert.False(Geometry.PointInPolygon(new PointF(0.9f, 0.9f), triangle));
    }

    [Fact]
    public void SegmentsIntersect_CrossingLines_ReturnsTrue()
    {
        // Horizontal line from (0,0.5) to (1,0.5)
        // Vertical line from (0.5,0) to (0.5,1)
        Assert.True(Geometry.SegmentsIntersect(
            new PointF(0, 0.5f), new PointF(1, 0.5f),
            new PointF(0.5f, 0), new PointF(0.5f, 1)));
    }

    [Fact]
    public void SegmentsIntersect_ParallelLines_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new PointF(0, 0), new PointF(1, 0),
            new PointF(0, 1), new PointF(1, 1)));
    }

    [Fact]
    public void SegmentsIntersect_NonCrossing_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new PointF(0, 0), new PointF(0.4f, 0),
            new PointF(0.5f, 0), new PointF(1, 0.5f)));
    }

    [Fact]
    public void CrossProduct_ReturnsCorrectSign()
    {
        // Positive: counter-clockwise
        Assert.True(Geometry.Cross(
            new PointF(0, 0), new PointF(1, 0), new PointF(0, 1)) > 0);
        // Negative: clockwise
        Assert.True(Geometry.Cross(
            new PointF(0, 0), new PointF(0, 1), new PointF(1, 0)) < 0);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GeometryTests" -v n
```

Expected: FAIL.

**Step 3: Implement Geometry utility**

```csharp
// src/OpenEye/Zones/Geometry.cs
using OpenEye.Models;

namespace OpenEye.Zones;

public static class Geometry
{
    /// <summary>
    /// Ray-casting algorithm for point-in-polygon test.
    /// </summary>
    public static bool PointInPolygon(PointF point, IReadOnlyList<PointF> polygon)
    {
        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y)
                    / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// 2D cross product of vectors (b-a) and (c-a).
    /// </summary>
    public static float Cross(PointF a, PointF b, PointF c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    /// <summary>
    /// Returns true if line segment (p1,p2) intersects segment (p3,p4).
    /// </summary>
    public static bool SegmentsIntersect(PointF p1, PointF p2, PointF p3, PointF p4)
    {
        float d1 = Cross(p3, p4, p1);
        float d2 = Cross(p3, p4, p2);
        float d3 = Cross(p1, p2, p3);
        float d4 = Cross(p1, p2, p4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        // Collinear cases (on-segment check)
        if (d1 == 0 && OnSegment(p3, p4, p1)) return true;
        if (d2 == 0 && OnSegment(p3, p4, p2)) return true;
        if (d3 == 0 && OnSegment(p1, p2, p3)) return true;
        if (d4 == 0 && OnSegment(p1, p2, p4)) return true;

        return false;
    }

    private static bool OnSegment(PointF a, PointF b, PointF p)
    {
        return Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) &&
               Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GeometryTests" -v n
```

Expected: 8 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add geometry utilities for point-in-polygon and line intersection"
```

---

## Task 6: Zone Evaluator

**Files:**
- Create: `src/OpenEye/Zones/ZoneOccupancy.cs`
- Create: `src/OpenEye/Zones/ZoneEvaluator.cs`
- Test: `tests/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs
using OpenEye.Models;
using OpenEye.Zones;

namespace OpenEye.Tests.Zones;

public class ZoneEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Zone TestZone = new("zone-a",
        new List<PointF>
        {
            new(0.2f, 0.2f), new(0.8f, 0.2f), new(0.8f, 0.8f), new(0.2f, 0.8f)
        }, "cam-1");

    private static readonly Tripwire TestTripwire = new("trip-a",
        new PointF(0.5f, 0.0f), new PointF(0.5f, 1.0f), "cam-1");

    private TrackedObject MakeTrack(string id, float cx, float cy, DateTimeOffset t)
    {
        float bx = cx - 0.05f;
        float by = cy - 0.05f;
        return new TrackedObject(id, "person", new BoundingBox(bx, by, 0.1f, 0.1f), t);
    }

    [Fact]
    public void Evaluate_ObjectInZone_ReturnsOccupancy()
    {
        var evaluator = new ZoneEvaluator(new[] { TestZone }, Array.Empty<Tripwire>());
        var track = MakeTrack("t1", 0.5f, 0.5f, T0); // centroid inside zone

        var result = evaluator.Evaluate(new[] { track }, T0);

        Assert.Single(result.ZoneOccupancies);
        Assert.Equal("t1", result.ZoneOccupancies[0].TrackId);
        Assert.Equal("zone-a", result.ZoneOccupancies[0].ZoneId);
    }

    [Fact]
    public void Evaluate_ObjectOutsideZone_NoOccupancy()
    {
        var evaluator = new ZoneEvaluator(new[] { TestZone }, Array.Empty<Tripwire>());
        var track = MakeTrack("t1", 0.05f, 0.05f, T0); // centroid outside zone

        var result = evaluator.Evaluate(new[] { track }, T0);

        Assert.Empty(result.ZoneOccupancies);
    }

    [Fact]
    public void Evaluate_ObjectEntersZone_ReportsTransition()
    {
        var evaluator = new ZoneEvaluator(new[] { TestZone }, Array.Empty<Tripwire>());

        // Frame 1: outside
        var trackOutside = MakeTrack("t1", 0.05f, 0.05f, T0);
        evaluator.Evaluate(new[] { trackOutside }, T0);

        // Frame 2: inside
        trackOutside.Update(new BoundingBox(0.45f, 0.45f, 0.1f, 0.1f), T0.AddSeconds(1), 50);
        var result = evaluator.Evaluate(new[] { trackOutside }, T0.AddSeconds(1));

        Assert.Contains(result.Transitions, t => t.Type == ZoneTransitionType.Enter && t.ZoneId == "zone-a");
    }

    [Fact]
    public void Evaluate_ObjectExitsZone_ReportsTransition()
    {
        var evaluator = new ZoneEvaluator(new[] { TestZone }, Array.Empty<Tripwire>());

        // Frame 1: inside
        var track = MakeTrack("t1", 0.5f, 0.5f, T0);
        evaluator.Evaluate(new[] { track }, T0);

        // Frame 2: outside
        track.Update(new BoundingBox(0.0f, 0.0f, 0.1f, 0.1f), T0.AddSeconds(1), 50);
        var result = evaluator.Evaluate(new[] { track }, T0.AddSeconds(1));

        Assert.Contains(result.Transitions, t => t.Type == ZoneTransitionType.Exit && t.ZoneId == "zone-a");
    }

    [Fact]
    public void Evaluate_ObjectCrossesTripwire_ReportsCrossing()
    {
        var evaluator = new ZoneEvaluator(Array.Empty<Zone>(), new[] { TestTripwire });

        // Track with trajectory that crosses the tripwire (x=0.5 line)
        var track = MakeTrack("t1", 0.3f, 0.5f, T0);
        evaluator.Evaluate(new[] { track }, T0);

        track.Update(new BoundingBox(0.55f, 0.45f, 0.1f, 0.1f), T0.AddSeconds(1), 50);
        var result = evaluator.Evaluate(new[] { track }, T0.AddSeconds(1));

        Assert.Single(result.TripwireCrossings);
        Assert.Equal("trip-a", result.TripwireCrossings[0].TripwireId);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v n
```

Expected: FAIL.

**Step 3: Implement ZoneOccupancy, ZoneEvaluator and result types**

```csharp
// src/OpenEye/Zones/ZoneOccupancy.cs
namespace OpenEye.Zones;

public sealed record ZoneOccupancy(string TrackId, string ZoneId, DateTimeOffset EnteredAt);

public enum ZoneTransitionType { Enter, Exit }

public sealed record ZoneTransition(string TrackId, string ZoneId, ZoneTransitionType Type, DateTimeOffset Timestamp);

public sealed record TripwireCrossing(string TrackId, string TripwireId, DateTimeOffset Timestamp);

public sealed record ZoneEvaluationResult(
    IReadOnlyList<ZoneOccupancy> ZoneOccupancies,
    IReadOnlyList<ZoneTransition> Transitions,
    IReadOnlyList<TripwireCrossing> TripwireCrossings);
```

```csharp
// src/OpenEye/Zones/ZoneEvaluator.cs
using OpenEye.Models;

namespace OpenEye.Zones;

public sealed class ZoneEvaluator
{
    private readonly IReadOnlyList<Zone> _zones;
    private readonly IReadOnlyList<Tripwire> _tripwires;

    // TrackId → set of ZoneIds currently occupied
    private readonly Dictionary<string, Dictionary<string, DateTimeOffset>> _previousZoneMembership = new();
    // TrackId → last trajectory point index checked for tripwire
    private readonly Dictionary<string, int> _lastTrajectoryIndex = new();

    public ZoneEvaluator(IReadOnlyList<Zone> zones, IReadOnlyList<Tripwire> tripwires)
    {
        _zones = zones;
        _tripwires = tripwires;
    }

    public ZoneEvaluationResult Evaluate(IReadOnlyList<TrackedObject> tracks, DateTimeOffset timestamp)
    {
        var occupancies = new List<ZoneOccupancy>();
        var transitions = new List<ZoneTransition>();
        var crossings = new List<TripwireCrossing>();

        foreach (var track in tracks)
        {
            if (track.State == TrackState.Expired) continue;

            var centroid = track.CurrentBox.Centroid;
            var currentZones = new HashSet<string>();

            // Check zone membership
            foreach (var zone in _zones)
            {
                if (Geometry.PointInPolygon(centroid, zone.Polygon))
                {
                    currentZones.Add(zone.ZoneId);

                    if (!_previousZoneMembership.TryGetValue(track.TrackId, out var prevZones)
                        || !prevZones.ContainsKey(zone.ZoneId))
                    {
                        // Entered zone
                        transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId, ZoneTransitionType.Enter, timestamp));
                    }
                }
            }

            // Check for exits
            if (_previousZoneMembership.TryGetValue(track.TrackId, out var previousZones))
            {
                foreach (var (zoneId, enteredAt) in previousZones)
                {
                    if (!currentZones.Contains(zoneId))
                    {
                        transitions.Add(new ZoneTransition(track.TrackId, zoneId, ZoneTransitionType.Exit, timestamp));
                    }
                }
            }

            // Update zone membership state
            var newMembership = new Dictionary<string, DateTimeOffset>();
            foreach (var zoneId in currentZones)
            {
                if (_previousZoneMembership.TryGetValue(track.TrackId, out var prev)
                    && prev.TryGetValue(zoneId, out var existingEntry))
                    newMembership[zoneId] = existingEntry; // Preserve original entry time
                else
                    newMembership[zoneId] = timestamp;

                occupancies.Add(new ZoneOccupancy(track.TrackId, zoneId, newMembership[zoneId]));
            }
            _previousZoneMembership[track.TrackId] = newMembership;

            // Check tripwire crossings
            if (track.Trajectory.Count >= 2)
            {
                int startIdx = _lastTrajectoryIndex.GetValueOrDefault(track.TrackId, 0);
                startIdx = Math.Max(startIdx, 0);
                // Ensure we check at least the last segment
                if (startIdx >= track.Trajectory.Count - 1)
                    startIdx = track.Trajectory.Count - 2;

                for (int i = startIdx; i < track.Trajectory.Count - 1; i++)
                {
                    var p1 = track.Trajectory[i].Position;
                    var p2 = track.Trajectory[i + 1].Position;

                    foreach (var tripwire in _tripwires)
                    {
                        if (Geometry.SegmentsIntersect(p1, p2, tripwire.PointA, tripwire.PointB))
                        {
                            crossings.Add(new TripwireCrossing(track.TrackId, tripwire.TripwireId, timestamp));
                        }
                    }
                }

                _lastTrajectoryIndex[track.TrackId] = track.Trajectory.Count - 1;
            }
        }

        return new ZoneEvaluationResult(occupancies, transitions, crossings);
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v n
```

Expected: 5 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement zone evaluator with enter/exit transitions and tripwire crossing"
```

---

## Task 7: YAML Configuration Loader

**Files:**
- Create: `src/OpenEye/Configuration/OpenEyeConfig.cs`
- Create: `src/OpenEye/Configuration/ConfigModels.cs`
- Test: `tests/OpenEye.Tests/Configuration/ConfigLoaderTests.cs`
- Create: `tests/OpenEye.Tests/Configuration/test-config.yaml`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Configuration/ConfigLoaderTests.cs
using OpenEye.Configuration;
using OpenEye.Models;

namespace OpenEye.Tests.Configuration;

public class ConfigLoaderTests
{
    private const string ValidYaml = @"
cameras:
  - id: camera-01
    zones:
      - id: entrance
        polygon: [[0.1, 0.2], [0.4, 0.2], [0.4, 0.8], [0.1, 0.8]]
    tripwires:
      - id: door-line
        points: [[0.25, 0.1], [0.25, 0.9]]
        direction: left-to-right

tracker:
  max_lost_frames: 30
  iou_threshold: 0.3
  trajectory_window: 50

rules:
  - id: loitering
    trigger:
      object: person
      zone: entrance
      condition: ""duration > 60s""
    event:
      type: loitering
      severity: warning
      cooldown: 120s

event_defaults:
  cooldown: 30s
  max_per_minute: 10
";

    [Fact]
    public void FromYamlString_ParsesCameras()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Single(config.Cameras);
        Assert.Equal("camera-01", config.Cameras[0].Id);
    }

    [Fact]
    public void FromYamlString_ParsesZones()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Single(config.Cameras[0].Zones);
        Assert.Equal("entrance", config.Cameras[0].Zones[0].ZoneId);
        Assert.Equal(4, config.Cameras[0].Zones[0].Polygon.Count);
    }

    [Fact]
    public void FromYamlString_ParsesTripwires()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Single(config.Cameras[0].Tripwires);
        Assert.Equal("door-line", config.Cameras[0].Tripwires[0].TripwireId);
        Assert.Equal(TripwireDirection.LeftToRight, config.Cameras[0].Tripwires[0].Direction);
    }

    [Fact]
    public void FromYamlString_ParsesTrackerOptions()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Equal(30, config.TrackerOptions.MaxLostFrames);
        Assert.Equal(0.3f, config.TrackerOptions.IouThreshold);
        Assert.Equal(50, config.TrackerOptions.TrajectoryWindow);
    }

    [Fact]
    public void FromYamlString_ParsesRules()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Single(config.Rules);
        Assert.Equal("loitering", config.Rules[0].Id);
        Assert.Equal("person", config.Rules[0].Trigger.Object);
        Assert.Equal("entrance", config.Rules[0].Trigger.Zone);
        Assert.Equal("duration > 60s", config.Rules[0].Trigger.Condition);
        Assert.Equal("loitering", config.Rules[0].Event.Type);
        Assert.Equal(Severity.Warning, config.Rules[0].Event.Severity);
        Assert.Equal(TimeSpan.FromSeconds(120), config.Rules[0].Event.Cooldown);
    }

    [Fact]
    public void FromYamlString_ParsesEventDefaults()
    {
        var config = OpenEyeConfig.FromYamlString(ValidYaml);
        Assert.Equal(TimeSpan.FromSeconds(30), config.EventDefaults.Cooldown);
        Assert.Equal(10, config.EventDefaults.MaxPerMinute);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -v n
```

Expected: FAIL.

**Step 3: Implement config models and YAML parser**

```csharp
// src/OpenEye/Configuration/ConfigModels.cs
using OpenEye.Models;

namespace OpenEye.Configuration;

public sealed class CameraConfig
{
    public string Id { get; set; } = "";
    public List<Zone> Zones { get; set; } = new();
    public List<Tripwire> Tripwires { get; set; } = new();
}

public sealed class RuleTriggerConfig
{
    public string Object { get; set; } = "";
    public string? Zone { get; set; }
    public string? Tripwire { get; set; }
    public string Condition { get; set; } = "";
}

public sealed class RuleEventConfig
{
    public string Type { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Info;
    public TimeSpan? Cooldown { get; set; }
}

public sealed class RuleConfig
{
    public string Id { get; set; } = "";
    public RuleTriggerConfig Trigger { get; set; } = new();
    public RuleEventConfig Event { get; set; } = new();
}

public sealed class EventDefaultsConfig
{
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxPerMinute { get; set; } = 10;
}
```

```csharp
// src/OpenEye/Configuration/OpenEyeConfig.cs
using OpenEye.Models;
using OpenEye.Tracking;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenEye.Configuration;

public sealed class OpenEyeConfig
{
    public List<CameraConfig> Cameras { get; set; } = new();
    public TrackerOptions TrackerOptions { get; set; } = new();
    public List<RuleConfig> Rules { get; set; } = new();
    public EventDefaultsConfig EventDefaults { get; set; } = new();

    public static OpenEyeConfig FromYamlString(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var raw = deserializer.Deserialize<RawConfig>(yaml);
        return MapFromRaw(raw);
    }

    public static OpenEyeConfig FromYaml(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return FromYamlString(yaml);
    }

    private static OpenEyeConfig MapFromRaw(RawConfig raw)
    {
        var config = new OpenEyeConfig();

        if (raw.Tracker != null)
        {
            config.TrackerOptions = new TrackerOptions
            {
                MaxLostFrames = raw.Tracker.MaxLostFrames ?? 30,
                IouThreshold = raw.Tracker.IouThreshold ?? 0.3f,
                TrajectoryWindow = raw.Tracker.TrajectoryWindow ?? 50,
            };
        }

        if (raw.EventDefaults != null)
        {
            config.EventDefaults = new EventDefaultsConfig
            {
                Cooldown = ParseTimeSpan(raw.EventDefaults.Cooldown) ?? TimeSpan.FromSeconds(30),
                MaxPerMinute = raw.EventDefaults.MaxPerMinute ?? 10,
            };
        }

        foreach (var cam in raw.Cameras ?? new())
        {
            var camConfig = new CameraConfig { Id = cam.Id ?? "" };

            foreach (var z in cam.Zones ?? new())
            {
                var polygon = z.Polygon?.Select(p => new PointF((float)p[0], (float)p[1])).ToList()
                    ?? new List<PointF>();
                camConfig.Zones.Add(new Zone(z.Id ?? "", polygon, cam.Id ?? ""));
            }

            foreach (var tw in cam.Tripwires ?? new())
            {
                var pts = tw.Points ?? new();
                var ptA = pts.Count > 0 ? new PointF((float)pts[0][0], (float)pts[0][1]) : default;
                var ptB = pts.Count > 1 ? new PointF((float)pts[1][0], (float)pts[1][1]) : default;
                var dir = ParseDirection(tw.Direction);
                camConfig.Tripwires.Add(new Tripwire(tw.Id ?? "", ptA, ptB, cam.Id ?? "", dir));
            }

            config.Cameras.Add(camConfig);
        }

        foreach (var r in raw.Rules ?? new())
        {
            config.Rules.Add(new RuleConfig
            {
                Id = r.Id ?? "",
                Trigger = new RuleTriggerConfig
                {
                    Object = r.Trigger?.Object ?? "",
                    Zone = r.Trigger?.Zone,
                    Tripwire = r.Trigger?.Tripwire,
                    Condition = r.Trigger?.Condition ?? "",
                },
                Event = new RuleEventConfig
                {
                    Type = r.Event?.Type ?? "",
                    Severity = ParseSeverity(r.Event?.Severity),
                    Cooldown = ParseTimeSpan(r.Event?.Cooldown),
                },
            });
        }

        return config;
    }

    private static TripwireDirection ParseDirection(string? dir) => dir?.ToLowerInvariant() switch
    {
        "left-to-right" => TripwireDirection.LeftToRight,
        "right-to-left" => TripwireDirection.RightToLeft,
        "top-to-bottom" => TripwireDirection.TopToBottom,
        "bottom-to-top" => TripwireDirection.BottomToTop,
        _ => TripwireDirection.Any,
    };

    private static Severity ParseSeverity(string? severity) => severity?.ToLowerInvariant() switch
    {
        "warning" => Severity.Warning,
        "critical" => Severity.Critical,
        _ => Severity.Info,
    };

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.EndsWith("s") && double.TryParse(value.AsSpan(0, value.Length - 1), out var seconds))
            return TimeSpan.FromSeconds(seconds);
        if (value.EndsWith("m") && double.TryParse(value.AsSpan(0, value.Length - 1), out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (value.EndsWith("h") && double.TryParse(value.AsSpan(0, value.Length - 1), out var hours))
            return TimeSpan.FromHours(hours);
        return null;
    }

    // Raw YAML deserialization models
    private class RawConfig
    {
        public List<RawCamera>? Cameras { get; set; }
        public RawTracker? Tracker { get; set; }
        public List<RawRule>? Rules { get; set; }
        public RawEventDefaults? EventDefaults { get; set; }
    }

    private class RawCamera
    {
        public string? Id { get; set; }
        public List<RawZone>? Zones { get; set; }
        public List<RawTripwire>? Tripwires { get; set; }
    }

    private class RawZone
    {
        public string? Id { get; set; }
        public List<List<double>>? Polygon { get; set; }
    }

    private class RawTripwire
    {
        public string? Id { get; set; }
        public List<List<double>>? Points { get; set; }
        public string? Direction { get; set; }
    }

    private class RawTracker
    {
        public int? MaxLostFrames { get; set; }
        public float? IouThreshold { get; set; }
        public int? TrajectoryWindow { get; set; }
    }

    private class RawRule
    {
        public string? Id { get; set; }
        public RawTrigger? Trigger { get; set; }
        public RawEvent? Event { get; set; }
    }

    private class RawTrigger
    {
        public string? Object { get; set; }
        public string? Zone { get; set; }
        public string? Tripwire { get; set; }
        public string? Condition { get; set; }
    }

    private class RawEvent
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Cooldown { get; set; }
    }

    private class RawEventDefaults
    {
        public string? Cooldown { get; set; }
        public int? MaxPerMinute { get; set; }
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ConfigLoaderTests" -v n
```

Expected: 6 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement YAML configuration loader"
```

---

## Task 8: Rule Condition Parser

**Files:**
- Create: `src/OpenEye/Rules/ConditionType.cs`
- Create: `src/OpenEye/Rules/ParsedCondition.cs`
- Create: `src/OpenEye/Rules/ConditionParser.cs`
- Test: `tests/OpenEye.Tests/Rules/ConditionParserTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Rules/ConditionParserTests.cs
using OpenEye.Rules;

namespace OpenEye.Tests.Rules;

public class ConditionParserTests
{
    [Fact]
    public void Parse_ZoneEnter()
    {
        var c = ConditionParser.Parse("zone_enter");
        Assert.Equal(ConditionType.ZoneEnter, c.Type);
    }

    [Fact]
    public void Parse_ZoneExit()
    {
        var c = ConditionParser.Parse("zone_exit");
        Assert.Equal(ConditionType.ZoneExit, c.Type);
    }

    [Fact]
    public void Parse_DurationGreaterThan()
    {
        var c = ConditionParser.Parse("duration > 60s");
        Assert.Equal(ConditionType.Duration, c.Type);
        Assert.Equal(TimeSpan.FromSeconds(60), c.TimeThreshold);
    }

    [Fact]
    public void Parse_DurationMinutes()
    {
        var c = ConditionParser.Parse("duration > 5m");
        Assert.Equal(ConditionType.Duration, c.Type);
        Assert.Equal(TimeSpan.FromMinutes(5), c.TimeThreshold);
    }

    [Fact]
    public void Parse_CountGreaterThan()
    {
        var c = ConditionParser.Parse("count > 5");
        Assert.Equal(ConditionType.CountGreaterThan, c.Type);
        Assert.Equal(5, c.IntThreshold);
    }

    [Fact]
    public void Parse_CountLessThan()
    {
        var c = ConditionParser.Parse("count < 2");
        Assert.Equal(ConditionType.CountLessThan, c.Type);
        Assert.Equal(2, c.IntThreshold);
    }

    [Fact]
    public void Parse_LineCrossed()
    {
        var c = ConditionParser.Parse("line_crossed");
        Assert.Equal(ConditionType.LineCrossed, c.Type);
    }

    [Fact]
    public void Parse_Absent()
    {
        var c = ConditionParser.Parse("absent > 300s");
        Assert.Equal(ConditionType.Absent, c.Type);
        Assert.Equal(TimeSpan.FromSeconds(300), c.TimeThreshold);
    }

    [Fact]
    public void Parse_Speed()
    {
        var c = ConditionParser.Parse("speed > 0.1");
        Assert.Equal(ConditionType.Speed, c.Type);
        Assert.Equal(0.1f, c.FloatThreshold, precision: 5);
    }

    [Fact]
    public void Parse_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConditionParser.Parse("invalid_condition"));
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ConditionParserTests" -v n
```

Expected: FAIL.

**Step 3: Implement condition types and parser**

```csharp
// src/OpenEye/Rules/ConditionType.cs
namespace OpenEye.Rules;

public enum ConditionType
{
    ZoneEnter,
    ZoneExit,
    Duration,
    CountGreaterThan,
    CountLessThan,
    LineCrossed,
    Absent,
    Speed,
}
```

```csharp
// src/OpenEye/Rules/ParsedCondition.cs
namespace OpenEye.Rules;

public sealed class ParsedCondition
{
    public ConditionType Type { get; init; }
    public TimeSpan? TimeThreshold { get; init; }
    public int? IntThreshold { get; init; }
    public float? FloatThreshold { get; init; }
}
```

```csharp
// src/OpenEye/Rules/ConditionParser.cs
using System.Globalization;

namespace OpenEye.Rules;

public static class ConditionParser
{
    public static ParsedCondition Parse(string condition)
    {
        var trimmed = condition.Trim().ToLowerInvariant();

        if (trimmed == "zone_enter")
            return new ParsedCondition { Type = ConditionType.ZoneEnter };
        if (trimmed == "zone_exit")
            return new ParsedCondition { Type = ConditionType.ZoneExit };
        if (trimmed == "line_crossed")
            return new ParsedCondition { Type = ConditionType.LineCrossed };

        if (trimmed.StartsWith("duration >"))
            return new ParsedCondition { Type = ConditionType.Duration, TimeThreshold = ParseTime(trimmed.AsSpan("duration >".Length)) };

        if (trimmed.StartsWith("count >"))
            return new ParsedCondition { Type = ConditionType.CountGreaterThan, IntThreshold = ParseInt(trimmed.AsSpan("count >".Length)) };

        if (trimmed.StartsWith("count <"))
            return new ParsedCondition { Type = ConditionType.CountLessThan, IntThreshold = ParseInt(trimmed.AsSpan("count <".Length)) };

        if (trimmed.StartsWith("absent >"))
            return new ParsedCondition { Type = ConditionType.Absent, TimeThreshold = ParseTime(trimmed.AsSpan("absent >".Length)) };

        if (trimmed.StartsWith("speed >"))
            return new ParsedCondition { Type = ConditionType.Speed, FloatThreshold = ParseFloat(trimmed.AsSpan("speed >".Length)) };

        throw new ArgumentException($"Unknown condition: '{condition}'");
    }

    private static TimeSpan ParseTime(ReadOnlySpan<char> value)
    {
        var s = value.Trim();
        if (s.EndsWith("s") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
            return TimeSpan.FromSeconds(sec);
        if (s.EndsWith("m") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var min))
            return TimeSpan.FromMinutes(min);
        if (s.EndsWith("h") && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hr))
            return TimeSpan.FromHours(hr);
        throw new ArgumentException($"Invalid time value: '{s.ToString()}'");
    }

    private static int ParseInt(ReadOnlySpan<char> value)
    {
        return int.Parse(value.Trim(), CultureInfo.InvariantCulture);
    }

    private static float ParseFloat(ReadOnlySpan<char> value)
    {
        return float.Parse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ConditionParserTests" -v n
```

Expected: 10 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement rule condition parser"
```

---

## Task 9: Rule Engine

**Files:**
- Create: `src/OpenEye/Rules/RuleEngine.cs`
- Test: `tests/OpenEye.Tests/Rules/RuleEngineTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Rules/RuleEngineTests.cs
using OpenEye.Configuration;
using OpenEye.Models;
using OpenEye.Rules;
using OpenEye.Zones;

namespace OpenEye.Tests.Rules;

public class RuleEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RuleConfig MakeRule(string id, string obj, string condition,
        string eventType, string? zone = null, string? tripwire = null, string? cooldown = null)
        => new()
        {
            Id = id,
            Trigger = new RuleTriggerConfig { Object = obj, Zone = zone, Tripwire = tripwire, Condition = condition },
            Event = new RuleEventConfig { Type = eventType, Severity = Severity.Warning, Cooldown = cooldown != null ? TimeSpan.Parse(cooldown) : null },
        };

    private static TrackedObject MakeTrack(string id, string cls, float cx, float cy, DateTimeOffset t)
        => new(id, cls, new BoundingBox(cx - 0.05f, cy - 0.05f, 0.1f, 0.1f), t);

    [Fact]
    public void ZoneEnter_Fires()
    {
        var rules = new[] { MakeRule("r1", "person", "zone_enter", "entered", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0) },
            new List<ZoneTransition> { new("t1", "z1", ZoneTransitionType.Enter, T0) },
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("entered", events[0].EventType);
        Assert.Equal("r1", events[0].RuleId);
    }

    [Fact]
    public void ZoneExit_Fires()
    {
        var rules = new[] { MakeRule("r1", "person", "zone_exit", "exited", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy>(),
            new List<ZoneTransition> { new("t1", "z1", ZoneTransitionType.Exit, T0) },
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.1f, 0.1f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("exited", events[0].EventType);
    }

    [Fact]
    public void Duration_FiresWhenExceeded()
    {
        var rules = new[] { MakeRule("r1", "person", "duration > 10s", "loitering", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        // Object has been in zone for 15 seconds
        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0.AddSeconds(-15)) },
            new List<ZoneTransition>(),
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("loitering", events[0].EventType);
    }

    [Fact]
    public void Duration_DoesNotFireBelowThreshold()
    {
        var rules = new[] { MakeRule("r1", "person", "duration > 60s", "loitering", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        // Object has been in zone for 5 seconds
        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0.AddSeconds(-5)) },
            new List<ZoneTransition>(),
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Empty(events);
    }

    [Fact]
    public void CountGreaterThan_Fires()
    {
        var rules = new[] { MakeRule("r1", "person", "count > 2", "crowded", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy>
            {
                new("t1", "z1", T0), new("t2", "z1", T0), new("t3", "z1", T0)
            },
            new List<ZoneTransition>(),
            new List<TripwireCrossing>());

        var tracks = new[]
        {
            MakeTrack("t1", "person", 0.3f, 0.3f, T0),
            MakeTrack("t2", "person", 0.5f, 0.5f, T0),
            MakeTrack("t3", "person", 0.7f, 0.7f, T0),
        };
        var events = engine.Evaluate(tracks, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("crowded", events[0].EventType);
    }

    [Fact]
    public void CountLessThan_Fires()
    {
        var rules = new[] { MakeRule("r1", "person", "count < 2", "understaffed", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0) },
            new List<ZoneTransition>(),
            new List<TripwireCrossing>());

        var tracks = new[] { MakeTrack("t1", "person", 0.5f, 0.5f, T0) };
        var events = engine.Evaluate(tracks, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("understaffed", events[0].EventType);
    }

    [Fact]
    public void LineCrossed_Fires()
    {
        var rules = new[] { MakeRule("r1", "person", "line_crossed", "crossed", tripwire: "tw1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy>(),
            new List<ZoneTransition>(),
            new List<TripwireCrossing> { new("t1", "tw1", T0) });

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Single(events);
        Assert.Equal("crossed", events[0].EventType);
    }

    [Fact]
    public void WrongClass_DoesNotFire()
    {
        var rules = new[] { MakeRule("r1", "forklift", "zone_enter", "entered", zone: "z1") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0) },
            new List<ZoneTransition> { new("t1", "z1", ZoneTransitionType.Enter, T0) },
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);
        var events = engine.Evaluate(new[] { track }, zoneResult, T0);

        Assert.Empty(events);
    }

    [Fact]
    public void Cooldown_PreventsRepeatFiring()
    {
        var rules = new[] { MakeRule("r1", "person", "zone_enter", "entered", zone: "z1", cooldown: "00:02:00") };
        var engine = new RuleEngine(rules, new EventDefaultsConfig());

        var zoneResult = new ZoneEvaluationResult(
            new List<ZoneOccupancy> { new("t1", "z1", T0) },
            new List<ZoneTransition> { new("t1", "z1", ZoneTransitionType.Enter, T0) },
            new List<TripwireCrossing>());

        var track = MakeTrack("t1", "person", 0.5f, 0.5f, T0);

        // First fire
        var events1 = engine.Evaluate(new[] { track }, zoneResult, T0);
        Assert.Single(events1);

        // Second fire within cooldown
        var events2 = engine.Evaluate(new[] { track }, zoneResult, T0.AddSeconds(30));
        Assert.Empty(events2);

        // Third fire after cooldown
        var events3 = engine.Evaluate(new[] { track }, zoneResult, T0.AddMinutes(3));
        Assert.Single(events3);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v n
```

Expected: FAIL.

**Step 3: Implement RuleEngine**

```csharp
// src/OpenEye/Rules/RuleEngine.cs
using OpenEye.Configuration;
using OpenEye.Models;
using OpenEye.Zones;

namespace OpenEye.Rules;

public sealed class RuleEngine
{
    private readonly IReadOnlyList<RuleConfig> _rules;
    private readonly List<ParsedRule> _parsedRules;
    private readonly EventDefaultsConfig _defaults;

    // (RuleId, TrackId) → last fire time, for cooldown
    private readonly Dictionary<string, DateTimeOffset> _lastFired = new();
    // RuleId → last fire time for absent tracking
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new();

    public RuleEngine(IReadOnlyList<RuleConfig> rules, EventDefaultsConfig defaults)
    {
        _rules = rules;
        _defaults = defaults;
        _parsedRules = rules.Select(r => new ParsedRule(r, ConditionParser.Parse(r.Trigger.Condition))).ToList();
    }

    public IReadOnlyList<OpenEyeEvent> Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult zoneResult,
        DateTimeOffset timestamp)
    {
        var events = new List<OpenEyeEvent>();

        foreach (var pr in _parsedRules)
        {
            var rule = pr.Config;
            var condition = pr.Condition;
            var matchingTracks = tracks.Where(t => t.ClassLabel == rule.Trigger.Object && t.State == TrackState.Active).ToList();

            switch (condition.Type)
            {
                case ConditionType.ZoneEnter:
                    foreach (var tr in zoneResult.Transitions)
                    {
                        if (tr.Type == ZoneTransitionType.Enter && tr.ZoneId == rule.Trigger.Zone)
                        {
                            var track = matchingTracks.FirstOrDefault(t => t.TrackId == tr.TrackId);
                            if (track != null && !InCooldown(rule, track.TrackId, timestamp))
                                events.Add(CreateEvent(rule, timestamp, track));
                        }
                    }
                    break;

                case ConditionType.ZoneExit:
                    foreach (var tr in zoneResult.Transitions)
                    {
                        if (tr.Type == ZoneTransitionType.Exit && tr.ZoneId == rule.Trigger.Zone)
                        {
                            var track = matchingTracks.FirstOrDefault(t => t.TrackId == tr.TrackId);
                            if (track != null && !InCooldown(rule, track.TrackId, timestamp))
                                events.Add(CreateEvent(rule, timestamp, track));
                        }
                    }
                    break;

                case ConditionType.Duration:
                    foreach (var occ in zoneResult.ZoneOccupancies)
                    {
                        if (occ.ZoneId != rule.Trigger.Zone) continue;
                        var track = matchingTracks.FirstOrDefault(t => t.TrackId == occ.TrackId);
                        if (track == null) continue;

                        var duration = timestamp - occ.EnteredAt;
                        if (duration >= condition.TimeThreshold && !InCooldown(rule, track.TrackId, timestamp))
                            events.Add(CreateEvent(rule, timestamp, track));
                    }
                    break;

                case ConditionType.CountGreaterThan:
                {
                    var count = zoneResult.ZoneOccupancies
                        .Where(o => o.ZoneId == rule.Trigger.Zone)
                        .Select(o => o.TrackId)
                        .Distinct()
                        .Count(tid => matchingTracks.Any(t => t.TrackId == tid));

                    if (count > condition.IntThreshold && !InCooldown(rule, "__count__", timestamp))
                        events.Add(CreateEvent(rule, timestamp, matchingTracks.ToArray()));
                    break;
                }

                case ConditionType.CountLessThan:
                {
                    var count = zoneResult.ZoneOccupancies
                        .Where(o => o.ZoneId == rule.Trigger.Zone)
                        .Select(o => o.TrackId)
                        .Distinct()
                        .Count(tid => matchingTracks.Any(t => t.TrackId == tid));

                    if (count < condition.IntThreshold && !InCooldown(rule, "__count__", timestamp))
                        events.Add(CreateEvent(rule, timestamp, matchingTracks.ToArray()));
                    break;
                }

                case ConditionType.LineCrossed:
                    foreach (var crossing in zoneResult.TripwireCrossings)
                    {
                        if (crossing.TripwireId != rule.Trigger.Tripwire) continue;
                        var track = matchingTracks.FirstOrDefault(t => t.TrackId == crossing.TrackId);
                        if (track != null && !InCooldown(rule, track.TrackId, timestamp))
                            events.Add(CreateEvent(rule, timestamp, track));
                    }
                    break;

                case ConditionType.Absent:
                {
                    var zoneOccupants = zoneResult.ZoneOccupancies
                        .Where(o => o.ZoneId == rule.Trigger.Zone)
                        .Select(o => o.TrackId)
                        .Distinct()
                        .Where(tid => matchingTracks.Any(t => t.TrackId == tid))
                        .ToList();

                    var seenKey = $"{rule.Id}::{rule.Trigger.Zone}";
                    if (zoneOccupants.Count > 0)
                    {
                        _lastSeen[seenKey] = timestamp;
                    }
                    else if (_lastSeen.TryGetValue(seenKey, out var lastSeenAt))
                    {
                        if (timestamp - lastSeenAt >= condition.TimeThreshold && !InCooldown(rule, "__absent__", timestamp))
                            events.Add(CreateEvent(rule, timestamp));
                    }
                    break;
                }

                case ConditionType.Speed:
                    foreach (var track in matchingTracks)
                    {
                        if (track.Trajectory.Count < 2) continue;
                        var last = track.Trajectory[^1];
                        var prev = track.Trajectory[^2];
                        var dt = (last.Timestamp - prev.Timestamp).TotalSeconds;
                        if (dt <= 0) continue;
                        var dx = last.Position.X - prev.Position.X;
                        var dy = last.Position.Y - prev.Position.Y;
                        var speed = (float)(Math.Sqrt(dx * dx + dy * dy) / dt);
                        if (speed > condition.FloatThreshold && !InCooldown(rule, track.TrackId, timestamp))
                            events.Add(CreateEvent(rule, timestamp, track));
                    }
                    break;
            }
        }

        return events;
    }

    private bool InCooldown(RuleConfig rule, string trackId, DateTimeOffset timestamp)
    {
        var cooldown = rule.Event.Cooldown ?? _defaults.Cooldown;
        if (cooldown == TimeSpan.Zero) return false;

        var key = $"{rule.Id}::{trackId}";
        if (_lastFired.TryGetValue(key, out var lastFired) && timestamp - lastFired < cooldown)
            return true;

        _lastFired[key] = timestamp;
        return false;
    }

    private OpenEyeEvent CreateEvent(RuleConfig rule, DateTimeOffset timestamp, params TrackedObject[] tracks)
    {
        return new OpenEyeEvent(
            EventType: rule.Event.Type,
            Timestamp: timestamp,
            SourceId: tracks.Length > 0 ? tracks[0].CurrentBox.ToString() : "",
            ZoneId: rule.Trigger.Zone,
            TrackedObjects: tracks.ToList(),
            RuleId: rule.Id,
            Severity: rule.Event.Severity);
    }

    private sealed record ParsedRule(RuleConfig Config, ParsedCondition Condition);
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v n
```

Expected: 9 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement rule engine with all condition types and cooldown"
```

---

## Task 10: Event Stream (Deduplication & Throttling)

**Files:**
- Create: `src/OpenEye/Pipeline/EventStream.cs`
- Test: `tests/OpenEye.Tests/Pipeline/EventStreamTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Pipeline/EventStreamTests.cs
using OpenEye.Models;
using OpenEye.Pipeline;

namespace OpenEye.Tests.Pipeline;

public class EventStreamTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static OpenEyeEvent MakeEvent(string ruleId, string type, DateTimeOffset t, string trackId = "t1")
    {
        var track = new TrackedObject(trackId, "person", new BoundingBox(0.1f, 0.1f, 0.1f, 0.1f), t);
        return new OpenEyeEvent(type, t, "cam-1", null, new[] { track }, ruleId);
    }

    [Fact]
    public async Task Events_AreDelivered()
    {
        var stream = new EventStream(maxPerMinute: 100);
        var evt = MakeEvent("r1", "test", T0);
        stream.Publish(evt);
        stream.Complete();

        var received = new List<OpenEyeEvent>();
        await foreach (var e in stream.Events)
            received.Add(e);

        Assert.Single(received);
        Assert.Equal("test", received[0].EventType);
    }

    [Fact]
    public async Task Throttle_DropsExcessEvents()
    {
        var stream = new EventStream(maxPerMinute: 2);

        // Publish 5 events for the same rule within one minute
        for (int i = 0; i < 5; i++)
            stream.Publish(MakeEvent("r1", "test", T0.AddSeconds(i)));
        stream.Complete();

        var received = new List<OpenEyeEvent>();
        await foreach (var e in stream.Events)
            received.Add(e);

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public async Task Throttle_DifferentRules_IndependentLimits()
    {
        var stream = new EventStream(maxPerMinute: 1);

        stream.Publish(MakeEvent("r1", "type-a", T0));
        stream.Publish(MakeEvent("r2", "type-b", T0));
        stream.Complete();

        var received = new List<OpenEyeEvent>();
        await foreach (var e in stream.Events)
            received.Add(e);

        Assert.Equal(2, received.Count);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~EventStreamTests" -v n
```

Expected: FAIL.

**Step 3: Implement EventStream**

```csharp
// src/OpenEye/Pipeline/EventStream.cs
using System.Threading.Channels;
using OpenEye.Models;

namespace OpenEye.Pipeline;

public sealed class EventStream
{
    private readonly int _maxPerMinute;
    private readonly Channel<OpenEyeEvent> _channel;

    // RuleId → list of timestamps within the current window
    private readonly Dictionary<string, List<DateTimeOffset>> _throttleWindow = new();

    public EventStream(int maxPerMinute = 10)
    {
        _maxPerMinute = maxPerMinute;
        _channel = Channel.CreateUnbounded<OpenEyeEvent>();
    }

    public void Publish(OpenEyeEvent evt)
    {
        if (!_throttleWindow.TryGetValue(evt.RuleId, out var timestamps))
        {
            timestamps = new List<DateTimeOffset>();
            _throttleWindow[evt.RuleId] = timestamps;
        }

        // Purge old timestamps (older than 1 minute)
        var cutoff = evt.Timestamp.AddMinutes(-1);
        timestamps.RemoveAll(t => t < cutoff);

        if (timestamps.Count >= _maxPerMinute)
            return; // Throttled

        timestamps.Add(evt.Timestamp);
        _channel.Writer.TryWrite(evt);
    }

    public void Complete() => _channel.Writer.Complete();

    public IAsyncEnumerable<OpenEyeEvent> Events => _channel.Reader.ReadAllAsync();
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~EventStreamTests" -v n
```

Expected: 3 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement event stream with throttling"
```

---

## Task 11: Pipeline Orchestrator (OpenEyeEngine)

**Files:**
- Create: `src/OpenEye/OpenEyeEngine.cs`
- Test: `tests/OpenEye.Tests/OpenEyeEngineTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/OpenEyeEngineTests.cs
using OpenEye.Configuration;
using OpenEye.Models;

namespace OpenEye.Tests;

public class OpenEyeEngineTests
{
    private static readonly string TestConfig = @"
cameras:
  - id: cam-1
    zones:
      - id: zone-a
        polygon: [[0.0, 0.0], [1.0, 0.0], [1.0, 1.0], [0.0, 1.0]]
    tripwires: []

tracker:
  max_lost_frames: 5
  iou_threshold: 0.2
  trajectory_window: 20

rules:
  - id: enter-zone
    trigger:
      object: person
      zone: zone-a
      condition: zone_enter
    event:
      type: person-entered
      severity: info
      cooldown: 0s

event_defaults:
  cooldown: 0s
  max_per_minute: 100
";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessFrame_DetectionInZone_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(TestConfig);
        var engine = new OpenEyeEngine(config);

        var detections = new[]
        {
            new Detection("person", new BoundingBox(0.4f, 0.4f, 0.1f, 0.1f), 0.9f, T0, "cam-1"),
        };

        engine.ProcessFrame("cam-1", detections, T0);
        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Single(events);
        Assert.Equal("person-entered", events[0].EventType);
    }

    [Fact]
    public async Task ProcessFrame_NoDetections_NoEvents()
    {
        var config = OpenEyeConfig.FromYamlString(TestConfig);
        var engine = new OpenEyeEngine(config);

        engine.ProcessFrame("cam-1", Array.Empty<Detection>(), T0);
        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessFrame_MultipleFrames_TracksObject()
    {
        var config = OpenEyeConfig.FromYamlString(TestConfig);
        var engine = new OpenEyeEngine(config);

        // Frame 1: person enters zone
        engine.ProcessFrame("cam-1", new[]
        {
            new Detection("person", new BoundingBox(0.4f, 0.4f, 0.1f, 0.1f), 0.9f, T0, "cam-1"),
        }, T0);

        // Frame 2: same person moves slightly (tracked, no new enter event)
        engine.ProcessFrame("cam-1", new[]
        {
            new Detection("person", new BoundingBox(0.42f, 0.42f, 0.1f, 0.1f), 0.9f, T0.AddSeconds(1), "cam-1"),
        }, T0.AddSeconds(1));

        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        // Only 1 zone_enter event (first frame), not 2
        Assert.Single(events);
    }
}
```

**Step 2: Run to verify failure**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~OpenEyeEngineTests" -v n
```

Expected: FAIL.

**Step 3: Implement OpenEyeEngine**

```csharp
// src/OpenEye/OpenEyeEngine.cs
using OpenEye.Configuration;
using OpenEye.Models;
using OpenEye.Pipeline;
using OpenEye.Rules;
using OpenEye.Tracking;
using OpenEye.Zones;

namespace OpenEye;

public sealed class OpenEyeEngine
{
    private readonly OpenEyeConfig _config;
    private readonly Dictionary<string, ObjectTracker> _trackers = new();
    private readonly Dictionary<string, ZoneEvaluator> _zoneEvaluators = new();
    private readonly RuleEngine _ruleEngine;
    private readonly EventStream _eventStream;

    public OpenEyeEngine(OpenEyeConfig config)
    {
        _config = config;
        _ruleEngine = new RuleEngine(config.Rules, config.EventDefaults);
        _eventStream = new EventStream(config.EventDefaults.MaxPerMinute);

        foreach (var cam in config.Cameras)
        {
            _trackers[cam.Id] = new ObjectTracker(config.TrackerOptions);
            _zoneEvaluators[cam.Id] = new ZoneEvaluator(cam.Zones, cam.Tripwires);
        }
    }

    public void ProcessFrame(string cameraId, IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        if (!_trackers.TryGetValue(cameraId, out var tracker))
            return;
        if (!_zoneEvaluators.TryGetValue(cameraId, out var zoneEvaluator))
            return;

        // Stage 1: Track objects
        tracker.Update(detections);
        var activeTracks = tracker.GetActiveTracks();

        // Stage 2: Evaluate zones
        var zoneResult = zoneEvaluator.Evaluate(activeTracks, timestamp);

        // Stage 3: Evaluate rules
        var events = _ruleEngine.Evaluate(activeTracks, zoneResult, timestamp);

        // Stage 4: Publish to event stream
        foreach (var evt in events)
            _eventStream.Publish(evt);
    }

    public void Complete() => _eventStream.Complete();

    public IAsyncEnumerable<OpenEyeEvent> Events => _eventStream.Events;
}
```

**Step 4: Run tests**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~OpenEyeEngineTests" -v n
```

Expected: 3 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement OpenEyeEngine pipeline orchestrator"
```

---

## Task 12: Integration Tests

Full end-to-end scenarios with realistic detection sequences.

**Files:**
- Test: `tests/OpenEye.IntegrationTests/RetailScenarioTests.cs`
- Test: `tests/OpenEye.IntegrationTests/WarehouseScenarioTests.cs`

**Step 1: Write retail scenario tests**

```csharp
// tests/OpenEye.IntegrationTests/RetailScenarioTests.cs
using OpenEye;
using OpenEye.Configuration;
using OpenEye.Models;

namespace OpenEye.IntegrationTests;

public class RetailScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string RetailConfig = @"
cameras:
  - id: cam-entrance
    zones:
      - id: entrance
        polygon: [[0.0, 0.0], [0.5, 0.0], [0.5, 1.0], [0.0, 1.0]]
      - id: checkout
        polygon: [[0.5, 0.0], [1.0, 0.0], [1.0, 1.0], [0.5, 1.0]]
    tripwires:
      - id: door-line
        points: [[0.25, 0.0], [0.25, 1.0]]

tracker:
  max_lost_frames: 10
  iou_threshold: 0.2
  trajectory_window: 100

rules:
  - id: loitering
    trigger:
      object: person
      zone: entrance
      condition: ""duration > 5s""
    event:
      type: loitering
      severity: warning
      cooldown: 0s

  - id: queue-too-long
    trigger:
      object: person
      zone: checkout
      condition: ""count > 3""
    event:
      type: queue-alert
      severity: info
      cooldown: 0s

  - id: door-crossing
    trigger:
      object: person
      tripwire: door-line
      condition: line_crossed
    event:
      type: person-entered-store
      severity: info
      cooldown: 0s

event_defaults:
  cooldown: 0s
  max_per_minute: 100
";

    [Fact]
    public async Task Loitering_PersonStaysInEntrance_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(RetailConfig);
        var engine = new OpenEyeEngine(config);

        // Person appears at entrance and stays for 10 seconds
        for (int i = 0; i <= 10; i++)
        {
            var t = T0.AddSeconds(i);
            engine.ProcessFrame("cam-entrance", new[]
            {
                new Detection("person", new BoundingBox(0.2f, 0.4f, 0.1f, 0.2f), 0.95f, t, "cam-entrance"),
            }, t);
        }
        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Contains(events, e => e.EventType == "loitering");
    }

    [Fact]
    public async Task QueueAlert_FourPeopleAtCheckout_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(RetailConfig);
        var engine = new OpenEyeEngine(config);

        // 4 people appear at checkout (zone is right half: x > 0.5)
        var detections = new[]
        {
            new Detection("person", new BoundingBox(0.55f, 0.1f, 0.1f, 0.1f), 0.9f, T0, "cam-entrance"),
            new Detection("person", new BoundingBox(0.65f, 0.2f, 0.1f, 0.1f), 0.9f, T0, "cam-entrance"),
            new Detection("person", new BoundingBox(0.75f, 0.3f, 0.1f, 0.1f), 0.9f, T0, "cam-entrance"),
            new Detection("person", new BoundingBox(0.85f, 0.4f, 0.1f, 0.1f), 0.9f, T0, "cam-entrance"),
        };

        engine.ProcessFrame("cam-entrance", detections, T0);
        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Contains(events, e => e.EventType == "queue-alert");
    }

    [Fact]
    public async Task DoorCrossing_PersonWalksThrough_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(RetailConfig);
        var engine = new OpenEyeEngine(config);

        // Person walks from left (x=0.1) to right (x=0.4), crossing the tripwire at x=0.25
        engine.ProcessFrame("cam-entrance", new[]
        {
            new Detection("person", new BoundingBox(0.05f, 0.4f, 0.1f, 0.2f), 0.9f, T0, "cam-entrance"),
        }, T0);

        engine.ProcessFrame("cam-entrance", new[]
        {
            new Detection("person", new BoundingBox(0.35f, 0.4f, 0.1f, 0.2f), 0.9f, T0.AddSeconds(1), "cam-entrance"),
        }, T0.AddSeconds(1));

        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Contains(events, e => e.EventType == "person-entered-store");
    }
}
```

**Step 2: Write warehouse scenario tests**

```csharp
// tests/OpenEye.IntegrationTests/WarehouseScenarioTests.cs
using OpenEye;
using OpenEye.Configuration;
using OpenEye.Models;

namespace OpenEye.IntegrationTests;

public class WarehouseScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly string WarehouseConfig = @"
cameras:
  - id: cam-floor
    zones:
      - id: restricted
        polygon: [[0.6, 0.6], [1.0, 0.6], [1.0, 1.0], [0.6, 1.0]]
      - id: loading-dock
        polygon: [[0.0, 0.0], [0.4, 0.0], [0.4, 0.4], [0.0, 0.4]]
    tripwires: []

tracker:
  max_lost_frames: 10
  iou_threshold: 0.2
  trajectory_window: 100

rules:
  - id: forklift-in-restricted
    trigger:
      object: forklift
      zone: restricted
      condition: zone_enter
    event:
      type: safety-violation
      severity: critical
      cooldown: 0s

  - id: loading-dock-idle
    trigger:
      object: forklift
      zone: loading-dock
      condition: ""duration > 5s""
    event:
      type: idle-forklift
      severity: warning
      cooldown: 0s

event_defaults:
  cooldown: 0s
  max_per_minute: 100
";

    [Fact]
    public async Task SafetyViolation_ForkliftEntersRestricted_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(WarehouseConfig);
        var engine = new OpenEyeEngine(config);

        // Forklift enters the restricted zone (x > 0.6, y > 0.6)
        engine.ProcessFrame("cam-floor", new[]
        {
            new Detection("forklift", new BoundingBox(0.7f, 0.7f, 0.15f, 0.15f), 0.9f, T0, "cam-floor"),
        }, T0);

        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Contains(events, e => e.EventType == "safety-violation" && e.Severity == Severity.Critical);
    }

    [Fact]
    public async Task IdleForklift_StaysAtLoadingDock_EmitsEvent()
    {
        var config = OpenEyeConfig.FromYamlString(WarehouseConfig);
        var engine = new OpenEyeEngine(config);

        // Forklift sits at loading dock for 10 seconds
        for (int i = 0; i <= 10; i++)
        {
            var t = T0.AddSeconds(i);
            engine.ProcessFrame("cam-floor", new[]
            {
                new Detection("forklift", new BoundingBox(0.1f, 0.1f, 0.15f, 0.15f), 0.9f, t, "cam-floor"),
            }, t);
        }

        engine.Complete();

        var events = new List<OpenEyeEvent>();
        await foreach (var e in engine.Events)
            events.Add(e);

        Assert.Contains(events, e => e.EventType == "idle-forklift");
    }
}
```

**Step 3: Run all tests**

```bash
dotnet test -v n
```

Expected: All tests PASS (unit + integration).

**Step 4: Commit**

```bash
git add -A
git commit -m "test: add retail and warehouse integration tests"
```

---

## Task 13: Sample Console App

**Files:**
- Create: `samples/RetailDemo/RetailDemo.csproj`
- Create: `samples/RetailDemo/Program.cs`
- Create: `samples/RetailDemo/config.yaml`

**Step 1: Create the sample project**

```bash
cd /c/Repos/openeye
dotnet new console -n RetailDemo -o samples/RetailDemo -f net8.0
dotnet sln add samples/RetailDemo/RetailDemo.csproj
dotnet add samples/RetailDemo reference src/OpenEye/OpenEye.csproj
```

**Step 2: Create sample config.yaml**

```yaml
# samples/RetailDemo/config.yaml
cameras:
  - id: front-door-cam
    zones:
      - id: entrance
        polygon: [[0.0, 0.0], [0.3, 0.0], [0.3, 1.0], [0.0, 1.0]]
      - id: checkout-area
        polygon: [[0.6, 0.0], [1.0, 0.0], [1.0, 1.0], [0.6, 1.0]]
    tripwires:
      - id: door-line
        points: [[0.15, 0.0], [0.15, 1.0]]

tracker:
  max_lost_frames: 30
  iou_threshold: 0.3
  trajectory_window: 50

rules:
  - id: person-entered
    trigger:
      object: person
      tripwire: door-line
      condition: line_crossed
    event:
      type: person-entered-store
      severity: info
      cooldown: 0s

  - id: loitering-at-entrance
    trigger:
      object: person
      zone: entrance
      condition: "duration > 30s"
    event:
      type: loitering
      severity: warning
      cooldown: 60s

  - id: checkout-queue-long
    trigger:
      object: person
      zone: checkout-area
      condition: "count > 5"
    event:
      type: queue-alert
      severity: warning
      cooldown: 30s

event_defaults:
  cooldown: 30s
  max_per_minute: 20
```

**Step 3: Write Program.cs**

```csharp
// samples/RetailDemo/Program.cs
using OpenEye;
using OpenEye.Configuration;
using OpenEye.Models;

Console.WriteLine("=== OpenEye Retail Demo ===");
Console.WriteLine();

var config = OpenEyeConfig.FromYaml("config.yaml");
var engine = new OpenEyeEngine(config);

// Simulate detections: person walks from outside (x=0.05) through door to entrance
var t = DateTimeOffset.UtcNow;
var random = new Random(42);

Console.WriteLine("Simulating: Person walks into store...");
for (int frame = 0; frame < 60; frame++)
{
    var time = t.AddMilliseconds(frame * 500); // 2 FPS
    float x = 0.05f + frame * 0.005f; // Slowly moving right
    float y = 0.5f + (float)(random.NextDouble() - 0.5) * 0.02f; // Slight vertical jitter

    engine.ProcessFrame("front-door-cam", new[]
    {
        new Detection("person", new BoundingBox(x, y, 0.08f, 0.15f), 0.92f, time, "front-door-cam"),
    }, time);
}

Console.WriteLine("Simulating: Crowd at checkout...");
for (int frame = 0; frame < 10; frame++)
{
    var time = t.AddSeconds(60 + frame);
    var detections = new List<Detection>();
    for (int p = 0; p < 6; p++)
    {
        float px = 0.65f + p * 0.05f;
        float py = 0.3f + p * 0.1f;
        detections.Add(new Detection("person", new BoundingBox(px, py, 0.08f, 0.15f), 0.9f, time, "front-door-cam"));
    }
    engine.ProcessFrame("front-door-cam", detections, time);
}

engine.Complete();

Console.WriteLine();
Console.WriteLine("--- Events ---");
await foreach (var evt in engine.Events)
{
    Console.WriteLine($"[{evt.Severity}] {evt.EventType} (rule: {evt.RuleId}) at {evt.Timestamp:HH:mm:ss.fff}");
}

Console.WriteLine();
Console.WriteLine("Demo complete.");
```

**Step 4: Build and run**

```bash
cd /c/Repos/openeye/samples/RetailDemo
dotnet run
```

Expected: Console output showing events detected.

**Step 5: Commit**

```bash
cd /c/Repos/openeye
git add -A
git commit -m "feat: add RetailDemo sample console app"
```

---

## Task 14: Run All Tests & Final Verification

**Step 1: Run full test suite**

```bash
cd /c/Repos/openeye
dotnet test -v n
```

Expected: All tests PASS.

**Step 2: Run the sample to verify end-to-end**

```bash
cd /c/Repos/openeye/samples/RetailDemo
dotnet run
```

Expected: Events printed to console.

**Step 3: Final commit if any cleanup needed**

```bash
cd /c/Repos/openeye
dotnet build --configuration Release
```

Expected: Build succeeds with no warnings.
