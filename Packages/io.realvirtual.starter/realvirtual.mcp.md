# realvirtual MCP Usage Guide

## General Workflow

### Always Start with a Screenshot
Before making changes to a scene, **always capture a screenshot first** to understand the current state:
```
screenshot_scene()    # 3D view of the scene
screenshot_editor()   # Full editor window with hierarchy/inspector
```
This helps analyze the problem and verify the current setup before taking action.

## Scene Analysis Best Practices

### Use Specialized Tools First

When analyzing a scene, **always prefer specialized high-level tools** over generic `component_get_all` calls. Each specialized tool returns structured data for its domain in a single call, replacing many individual queries.

| To understand... | Use this tool | NOT this |
|---|---|---|
| LogicStep flows | `logic_step_get_flow()` | `component_get_all` on each step |
| All drives | `drive_list()` | `component_get_all` on each drive |
| All sensors | `sensor_list()` | `component_get_all` on each sensor |
| All signals | `signal_list()` | `component_get_all` on each signal |
| All grips | `grip_list()` | `component_get_all` on each grip |
| All MUs | `m_u_list()` | `component_get_all` on each MU |
| Grip targets | `grip_target_list()` | `component_get_all` on each target |
| Transport surfaces | `transport_surface_list()` | `component_get_all` on each surface |
| Distance between objects | `transform_measure_distance()` | Multiple `scene_get_transform` + manual math |
| Object visual extents | `transform_get_bounds()` | `component_get_all` + manual scale math |
| Surface-to-surface gap | `transform_measure_surface_distance()` | `transform_get_bounds` on both + manual math |
| Center/axis of a bore or shaft | `transform_circle_center()` | `transform_get_bounds` center (systematically wrong on CAD) |

### Recommended Analysis Order

When starting work on a scene or subsystem:

1. **Screenshot first**: `screenshot_scene()` — understand the visual layout
2. **Find objects**: `scene_find()` or `scene_hierarchy()` — discover structure
3. **Specialized lists**: `drive_list()`, `sensor_list()`, `grip_list()` etc. — get domain overview
4. **LogicStep flows**: `logic_step_get_flow()` — understand automation logic
5. **Individual details**: `component_get()` / `component_get_all()` — only for specific components not covered above

**Example**: To understand a pick-and-place cell, use 4-5 calls instead of 30+:
```python
screenshot_scene()                              # Visual overview
scene_hierarchy(root="MyCell", depth=4)         # Structure
drive_list()                                    # All drives with positions and speeds
grip_list()                                     # All grips with pick signals
logic_step_get_flow(name="MyCell/MainCycle")    # Complete automation flow
```

### Measuring Distances

Use `transform_measure_distance()` to check distances between GameObjects instead of fetching multiple transforms and calculating manually. Returns both 3D distance and XZ (horizontal plane) distance in meters and millimeters.

```python
# Check if robot arm can reach a table
transform_measure_distance(objectA="Robot/Base/Axis1", objectB="Table/PlaceTarget")
# Returns: distance_m, distance_mm, distanceXZ_m, distanceXZ_mm, delta, heightDifference_m

# Check arm segment lengths (for reachability analysis)
transform_measure_distance(objectA="Robot/Base/Axis1", objectB="Robot/Base/Axis1/Axis2")         # Link 1
transform_measure_distance(objectA="Robot/Base/Axis1/Axis2", objectB="Robot/Base/Axis1/Axis2/Axis3")  # Link 2
# Max reach = Link1 + Link2. Compare with distance to target.
```

**Common use cases:**
- Robot arm reachability: measure arm segment lengths and compare sum with distance to target
- Sensor placement: verify sensor is within detection range of conveyor/part
- Gripper reach: check if gripper is close enough for GripTarget (compare with `GripRange`)

### Bounding Box Measurements

Use `transform_get_bounds()` to get the world-space axis-aligned bounding box (AABB) of a GameObject based on its Renderers. Use `transform_measure_surface_distance()` to measure the closest distance between bounding box surfaces of two objects.

These tools work with **visual extents** (meshes), not just pivot points — useful for verifying placement, checking collisions, and analyzing spatial relationships.

```python
# Get bounding box of a table (includes all child meshes)
transform_get_bounds(name="Demo2/Table")
# Returns: center, size, min, max (in both m and mm)

# Get bounds of just the parent object (no children)
transform_get_bounds(name="Demo2/Table", includeChildren=false)

# Measure surface-to-surface distance between Part and Table
transform_measure_surface_distance(objectA="Demo2/Part", objectB="Demo2/Table")
# Returns: surfaceDistance_m/mm, gapX/Y/Z, overlapping, touching, penetration (if overlapping)
```

**When to use which tool:**

| Question | Tool |
|---|---|
| How far apart are two pivots? | `transform_measure_distance()` |
| What are the visual extents of an object? | `transform_get_bounds()` |
| Does object A sit on top of object B? | `transform_measure_surface_distance()` |
| Is there a gap between two objects? | `transform_measure_surface_distance()` |
| Are two objects overlapping/penetrating? | `transform_measure_surface_distance()` |
| Where is the axis of this bore / shaft? | `transform_circle_center()` |

**Common use cases:**
- Placement verification: check if a Part is resting on a Table (Y gap should be ~0)
- Collision checking: detect if objects are overlapping (`overlapping: true`)
- Reachability analysis: measure surface distance from gripper to part
- Layout planning: verify clearances between machines

### Circle / Bore Center Detection (Pro)

`transform_circle_center()` determines the **exact** center, axis and radius of a round feature
(bore, shaft, cylinder) from a single world-space ray. Use it whenever you need a rotation axis on
imported CAD — a bounding-box center is systematically wrong there, because CAD tessellation is
unevenly dense.

Shoot a ray at a round surface; the whole connected face is used for the fit:

```python
# Ray from outside the part, aimed at the bore wall
transform_circle_center(
    name="Gearbox/Housing/Flange",
    ox=0.5, oy=0.2, oz=0.0,        # ray origin, world space
    dx=-1.0, dy=0.0, dz=0.0)       # ray direction, world space
```

Returns `center`, `circleCenter`, `axis`, `radius`, `rmsError`, `extentStart`/`extentEnd`,
`isCylindrical` and the triangle/point counts.

| Parameter | Meaning |
|---|---|
| `name` | GameObject name or full hierarchy path |
| `ox,oy,oz` / `dx,dy,dz` | World-space ray origin and direction (a bare point is not accepted — on thin or overlapping faces it does not identify a unique triangle) |
| `surfaceAngle` | Optional region-growing angle in degrees, default 35 |
| `placement` | Where on the axis `center` is placed: `clicked` (default), `middle`, `start`, `end` |

Notes:
- The axis orientation is **deterministic and camera-independent**, so repeated calls and UI clicks
  return the same sign.
- The tool **fails with a concrete reason** rather than returning a wrong center — e.g. too little of
  the circle covered, surface curved but not cylindrical (sphere/torus), conical instead of
  cylindrical, or non-uniform scaling in the circle plane.
- Works on `MeshFilter` geometry regardless of the Read/Write import flag. Skinned meshes are not
  supported and say so.
- The same detector powers the **Move to Circle / Bore Center** method of the Move Pivot tool, so
  headless and interactive results are identical.

## Visual Verification (Video, Markers, Camera Framing)

These tools let an agent visually verify scene state beyond a single static screenshot: record a
short clip of motion, mark a specific point/object for later reference, and frame a camera on a
target without guessing position/rotation by hand.

### Video Recording

```python
sim_play()                                  # video only starts from an already-running Play session
video_record_start(maxDurationSec=10)       # 1280x720@30 by default; returns the target .mp4 path
video_record_status()                       # recording: true/false, elapsedSec, path
video_record_stop()                         # stops early; returns path, sizeBytes, durationSec
sim_stop()
```

- `video_record_start` only works while Play Mode is already running - it never enters Play Mode
  itself, because the Unity Recorder cannot survive the domain reload triggered by Play entry. Call
  `sim_play` first.
- `maxDurationSec` (default 60) is a safety net: if `video_record_stop` is never called, the
  recording stops itself after this many seconds. `video_record_stop` works either way and reports
  `stoppedBySafetyNet`.
- Output: `.log/recordings/rec_<scene>_<HHmmss>.mp4` (fixed name, no wildcards - Windows rejects the
  Recorder's `<Take>` wildcard in a file path).

### Debug Markers

```python
debug_marker_add(path="Robot/Gripper", type="axes", label="Gripper TCP", size=0.1)
debug_marker_add(x=1000, y=200, z=500, type="sphere", color="#00FFFF")
debug_marker_clear()                        # id="" clears all; pass an id to clear just one
```

- `type`: `cross` (default), `axes` (RGB = XYZ), `sphere`, `arrow`.
- Anchor with `path` (+ optional `localOffsetX/Y/Z`) or with a raw `x`/`y`/`z` world position.
- The marker **shape** renders twice - as SceneView Handles AND as a real (HideAndDontSave,
  never-saved) GameObject - so it shows up in both capture paths: SceneView screen-pixel captures
  and any `camera.Render()` capture (`screenshot_game`, `screenshot_scene`, `video_record_*`).
- The optional text `label` is **SceneView/Handles-only** - see `includeGizmos` below to capture it.
- Markers are never written to the scene file and are not meant to survive a Play Mode transition -
  always call `debug_marker_clear()` when done rather than relying on Play Mode to clean up for you.

### Screenshots With Gizmos/Handles (markers, selection, IK gizmos, ...)

`screenshot_scene` and `screenshot_game` render via an offscreen RenderTexture, which does **not**
include Editor Gizmos/Handles (they're drawn by the SceneView's own IMGUI pass, not part of a
camera's normal render). To capture Handles output (e.g. a `debug_marker_add` label, or any custom
`OnDrawGizmos`), use one of:

```python
screenshot_scene(includeGizmos=true)        # same screen-pixel capture as screenshot_editor(panel="scene")
screenshot_editor(panel="scene")            # equivalent, explicit panel capture
```

Default behavior of `screenshot_scene`/`screenshot_game` (no `includeGizmos`) is unchanged.

### Camera Framing

```python
camera_frame(target="Robot/Gripper", padding=1.5)          # frames a GameObject's renderer bounds
camera_frame(target="1000,200,500", view="game")            # frames a raw world position, Main Camera only
```

Computes camera distance from the target's renderer bounds (or a small default radius for a raw
position) and an isometric direction by default (override with `directionX/Y/Z`). `view` selects
`scene`, `game`, or `both` (default). Use this instead of guessing `transform_set_position` values
for the Scene/Main Camera.

## Scene Structure Rules

### realvirtual Root GameObject
The top-level `realvirtual` GameObject (the one with the `realvirtualController` component) must NEVER be modified directly. Do not add children, components, or change properties on it.

When creating new GameObjects, always create them as **siblings** (at the same hierarchy level) of the `realvirtual` GameObject, not inside it. The `realvirtual` GameObject is a managed prefab and its internal structure must not be altered.

**Correct:**
```
Scene Root
  ├── realvirtual          ← DO NOT TOUCH
  ├── MyNewObject          ← Create new objects here (parallel)
  └── AnotherObject        ← Same level as realvirtual
```

**Wrong:**
```
Scene Root
  └── realvirtual
        └── MyNewObject    ← NEVER add inside realvirtual
```

### GameObject Identification
Always use full hierarchy paths when referencing GameObjects (e.g., `Robot/Axis1/Motor` instead of just `Motor`). Use `scene_find` to discover the correct path before operating on objects.

## Simulation Workflow

- Always stop the simulation (`sim_stop`) before recompiling scripts
- Sequence: `sim_stop` -> `editor_recompile` -> `editor_wait_ready` -> `sim_play`
- `component_set` does not work reliably during play mode

## LogicStep Rules

LogicSteps must be organized using containers. Follow these rules strictly:

### One LogicStep Per GameObject
Never place multiple LogicStep components on the same GameObject. Each LogicStep must be on its own dedicated child GameObject.

### Always Use Containers
LogicSteps must always be placed inside a `LogicStep_SerialContainer` or `LogicStep_ParallelContainer`:

- **SerialContainer**: Executes child LogicSteps one after another (in hierarchy order). **Auto-loops**: When all steps complete, it automatically restarts from the first step.
- **ParallelContainer**: Executes all child LogicSteps simultaneously, waits for all to finish before proceeding.

### Nesting Is Supported
Containers can be nested inside other containers:

```
LogicStep_SerialContainer (GameObject)
  ├── LogicStep_Delay (child GameObject)
  ├── LogicStep_WaitForSignalBool (child GameObject)
  ├── LogicStep_ParallelContainer (child GameObject)
  │     ├── LogicStep_DriveToPosition (child GameObject)
  │     └── LogicStep_SetSignalBool (child GameObject)
  └── LogicStep_Enable (child GameObject)
```

### Execution Order
The execution order within a container is determined by the GameObject hierarchy order in the Inspector (top to bottom).

### Naming Convention
Name GameObjects with numbered prefixes for clear execution order:
```
01 Start Entry Conveyor
02 Wait for Part at Sensor
03 Stop Entry Conveyor
04 Wait Part Taken
```

### Available LogicStep Types

| Component | Blocking | Purpose |
|---|---|---|
| `LogicStep_SerialContainer` | Yes | Executes children sequentially, auto-loops |
| `LogicStep_ParallelContainer` | Yes | Executes all children simultaneously |
| `LogicStep_SetSignalBool` | No | Sets a boolean signal value |
| `LogicStep_WaitForSensor` | Yes | Waits for sensor occupied/not-occupied |
| `LogicStep_WaitForSignalBool` | Yes | Waits for a boolean signal to be true/false |
| `LogicStep_Delay` | Yes | Waits for a duration in seconds |
| `LogicStep_DriveToPosition` | Yes | Moves a drive to target position |
| `LogicStep_SetDriveSpeed` | No | Sets drive speed |
| `LogicStep_Enable` | No | Enables/disables a GameObject |
| `LogicStep_Pause` | Yes | Pauses editor for debugging (breakpoint) |

### Reading & Debugging LogicSteps

**ALWAYS use `logic_step_get_flow()` to inspect existing LogicSteps.** This single call returns the complete hierarchy with step types, signal/sensor references, current values, drive positions, active step state, and cycle times. Do NOT use `component_get_all` on individual steps — that wastes many round-trips.

```python
# Get ALL LogicStep flows in the scene (finds top-level containers automatically)
logic_step_get_flow()

# Get flows under a specific parent
logic_step_get_flow(name="MyCell/PickAndPlaceCycle")

# Get a specific container's flow
logic_step_get_flow(name="MyCell/PickAndPlaceCycle/MainCycle")
```

The response includes for each step:
- `type`: Step type (SerialContainer, DriveTo, SetSignalBool, Delay, etc.)
- `stepActive` / `isWaiting`: Whether the step is currently active or waiting
- `params`: Type-specific parameters (drive path + position, signal path + value, duration, etc.)
- `activeStep` / `completedCycles` / `cycleTime`: Container statistics

**During simulation debugging**, call `logic_step_get_flow()` to see which step is currently active (`stepActive: true`) and where the flow is stuck (`isWaiting: true`).

### Configuring LogicSteps via MCP

**Setting Signal references** on LogicStep components uses hierarchy paths as string values:
```python
# Set the Signal property on LogicStep_SetSignalBool
component_set(
    name="MainCycle/01 Start Conveyor",
    componentType="LogicStep_SetSignalBool",
    properties='{"Signal": "DemoCell/PLCInterface/--- Conveyor/EntryConveyorStart", "SetToTrue": true}'
)

# Set Sensor reference on LogicStep_WaitForSensor
component_set(
    name="MainCycle/02 Wait Part",
    componentType="LogicStep_WaitForSensor",
    properties='{"Sensor": "DemoCell/Sensors/EntrySensor", "WaitForOccupied": true}'
)

# Set Signal reference on LogicStep_WaitForSignalBool
component_set(
    name="MainCycle/03 Wait Signal",
    componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": "DemoCell/PLCInterface/RobotIsLoading", "WaitForTrue": true}'
)
```

**Important**: Signal and Sensor references are Unity Object references. Pass the **hierarchy path as a string** — the MCP server resolves it to the actual component.

### Architecture Pattern: Independent Parallel Processes

For complex automation cells, use **multiple independent SerialContainers** under a **plain GameObject** (no LogicStep component on the parent). Each SerialContainer auto-loops independently. Synchronization happens through signals and sensors.

**IMPORTANT**: Do NOT use a `LogicStep_ParallelContainer` as the parent. A ParallelContainer waits for ALL children to finish before restarting any of them. For truly independent auto-looping processes, the parent must be a plain GameObject without any LogicStep component.

```
MainCycle (plain GameObject — NO LogicStep component!)
  ├── LogicStep_SerialContainer (Entry Conveyor)    ← auto-loops independently
  │     ├── 01 Start Entry Conveyor         (SetSignalBool)
  │     ├── 02 Wait Part at Entry Sensor    (WaitForSensor: occupied)
  │     ├── 03 Stop Entry Conveyor          (SetSignalBool)
  │     └── 04 Wait Part Taken              (WaitForSensor: not occupied)
  │          ↑ auto-loops back to 01
  │
  ├── LogicStep_SerialContainer (Robot and Machine Cycle)    ← auto-loops independently
  │     ├── 01 Wait Part at Entry Sensor    (WaitForSensor: occupied)
  │     ├── 02 Open Door                    (SetSignalBool)
  │     ├── 03 Start LoadCell               (SetSignalBool)
  │     ├── 04 Wait Loading Started         (WaitForSignalBool: RobotIsLoading=true)
  │     ├── 05 Wait Loading Done            (WaitForSignalBool: RobotIsLoading=false)
  │     ├── 06 Clear LoadCell               (SetSignalBool)
  │     ├── 07 Close Door                   (SetSignalBool)
  │     ├── 08 Start Machining              (SetSignalBool)
  │     ├── 09 Machining Delay              (Delay)
  │     ├── 10 Stop Machining               (SetSignalBool)
  │     ├── 11 Open Door                    (SetSignalBool)
  │     ├── 12 Start UnloadCell             (SetSignalBool)
  │     ├── 13 Wait Unloading Started       (WaitForSignalBool: RobotIsUnloading=true)
  │     ├── 14 Wait Unloading Done          (WaitForSignalBool: RobotIsUnloading=false)
  │     ├── 15 Clear UnloadCell             (SetSignalBool)
  │     └── 16 Close Door                   (SetSignalBool)
  │          ↑ auto-loops back to 01
  │
  └── LogicStep_SerialContainer (Exit Conveyor)    ← auto-loops independently
        ├── 01 Wait Unloading Started       (WaitForSignalBool: RobotIsUnloading=true)
        ├── 02 Wait Unloading Done          (WaitForSignalBool: RobotIsUnloading=false)
        ├── 03 Start Exit Conveyor          (SetSignalBool)
        ├── 04 Wait Part at Exit Sensor     (WaitForSensor: occupied)
        ├── 05 Wait Part Left               (WaitForSensor: not occupied)
        └── 06 Stop Exit Conveyor           (SetSignalBool)
             ↑ auto-loops back to 01
```

Each process runs independently and continuously loops. Synchronization happens naturally through signals and sensors (e.g., Entry Conveyor waits for sensor to clear before delivering next part, Machine Cycle waits for robot signals).

### When to Use ParallelContainer

Use `LogicStep_ParallelContainer` only when you need **multiple actions to happen simultaneously within a single step** of a sequence — for example, closing a door AND starting a machine at the same time. Do NOT use it to group independent processes that should auto-loop separately.

### Building LogicSteps via MCP: Step-by-Step

1. **Create a plain parent GameObject** (NO LogicStep component for independent processes):
   ```python
   game_object_create(name="MainCycle", parent="DemoCell")
   # Do NOT add a LogicStep component to MainCycle — it's just an organizer

   game_object_create(name="Entry Conveyor", parent="DemoCell/MainCycle")
   component_add(name="DemoCell/MainCycle/Entry Conveyor", componentType="LogicStep_SerialContainer")
   ```

2. **Create step GameObjects as children** of their container:
   ```python
   game_object_create(name="01 Start Entry Conveyor", parent="DemoCell/MainCycle/Entry Conveyor")
   component_add(name="..../01 Start Entry Conveyor", componentType="LogicStep_SetSignalBool")
   ```

3. **Configure component properties** (signals, sensors, values):
   ```python
   component_set(name="..../01 Start Entry Conveyor", componentType="LogicStep_SetSignalBool",
       properties='{"Signal": "DemoCell/PLCInterface/.../EntryConveyorStart", "SetToTrue": true}')
   ```

4. **Test**: `sim_play()`, then use `logic_step_get_flow()` to monitor progress — it shows `activeStep`, `stepActive`, `isWaiting`, and `completedCycles` for all containers at once.

### Important: Execution Order Is Hierarchy Order

The execution order of steps within a container is determined by **child index** (hierarchy order in Unity), NOT by the GameObject name. Renaming a step does not change its execution position. To reorder steps, use `transform_set_sibling_index`:

```python
# Move "03 Start Exit Conveyor" to be the first child (index 0)
transform_set_sibling_index(name="MainCycle/Exit Conveyor/03 Start Exit Conveyor", index=0)

# Move to last position
transform_set_sibling_index(name="MainCycle/Exit Conveyor/01 Wait Part", index=-1)
```

### Signal Pre-Setting for Reliable Operation

Some signals should be pre-set in **edit mode** (before pressing Play) to ensure reliable operation:

- **Entry conveyor start signal**: Pre-set to `true` so the conveyor starts running immediately
- **Trigger signals** (e.g., LoadCell, UnloadCell): Pre-set to `false` to ensure clean rising edge detection

```python
# Pre-set signals in edit mode (before sim_play)
component_set(name=".../EntryConveyorStart", componentType="PLCOutputBool",
    properties='{"Status": {"Value": true}}')
component_set(name=".../LoadCell", componentType="PLCOutputBool",
    properties='{"Status": {"Value": false}}')
```

### Signal Forcing (VIB — Virtual Commissioning)

**Force** is different from **Set Once** (`signal_set_*`): a forced signal holds its override value permanently via `Settings.Override = true`, so the PLC or simulation cannot overwrite it — the forced value always wins. This mirrors the TIA Portal Force Table / TwinCAT Force concept for boundary conditions (safety signals, pressure, operating mode) that have no real source during virtual commissioning.

**Key distinction:**

| Tool | Behavior | Equivalent |
|---|---|---|
| `signal_set_bool/float/int` | Writes `Status.Value` once; overwritten by next PLC cycle | TIA "Modify" |
| `signal_force_bool/float/int` | Sets `Settings.Override = true`; survives PLC writes | TIA "Force" |
| `signal_unforce` | Clears `Settings.Override`; live value resumes | TIA "Release Force" |

#### Individual Signal Forcing

```python
# Force a safety signal TRUE — survives all PLC writes
signal_force_bool(name="Notaus_OK", value=True)

# Force system pressure to 6.0 bar
signal_force_float(name="Systemdruck", value=6.0)

# Force operating mode to 2
signal_force_int(name="Betriebsart", value=2)

# Release a single signal (live PLC value resumes)
signal_unforce(name="Systemdruck")

# List all currently forced signals in the scene
signal_force_list()
# Returns: [{name, path, type, value}, ...] for all signals with Settings.Override = true
```

Both PLCInput and PLCOutput signals are supported by `signal_force_*` (searching both directions).

#### Force Sets (Named VIB Scenarios)

`SignalForceSet` components group multiple signals + their target override values into named scenarios. Apply or release an entire scenario with one call.

```python
# List all SignalForceSet components in the scene
force_set_list()
# Returns: {sets:[{name, gameObject, path, active, signalCount}], manager:{exclusiveMode}}

# Apply a named scenario (forces all enabled entries in the set)
force_set_apply(setName="VIB_Grundzustand")

# Release a scenario (unforces all entries)
force_set_release(setName="VIB_Grundzustand")
```

**setName** is the `SetName` field on the `SignalForceSet` component — not the GameObject name.

If a `SignalForceManager` with `ExclusiveMode = true` is present, `force_set_apply` automatically releases all other sets first (scenario switching).

#### VIB Workflow Pattern

```python
# 1. Check what is currently forced
signal_force_list()

# 2. Apply VIB baseline (safety signals, pressure, mode)
force_set_apply(setName="VIB_Grundzustand")

# 3. Inject a fault for testing
force_set_apply(setName="TEST_Druck_zu_niedrig")

# 4. Release fault, restore baseline
force_set_release(setName="TEST_Druck_zu_niedrig")

# 5. Emergency release of all forces in the scene
# (use signal_force_list() then signal_unforce() on each, or Release All in Force Monitor window)
```

**Safety note:** Forced signals (`Settings.Override = true`) are serialized — they survive play/stop if not explicitly released. `SignalForceSet.ReleaseOnStopSim` (default `true`) handles automatic release on simulation stop. The Signal Force Monitor window (`realvirtual DEV/Testing/Signal Force Monitor`) shows all active forces with a "Release ALL Forces" button.

### Working with DrivesRecorder / ReplayRecording

When a robot uses `DrivesRecorder` with `ReplayRecording` components (pre-recorded motion sequences), integrate with LogicSteps using the replay status signals:

- **`ReplayRecording.StartOnSignal`**: A `PLCOutputBool` — set to `true` to trigger the replay (rising edge only)
- **`ReplayRecording.IsReplayingSignal`**: A `PLCInputBool` — becomes `true` while replaying, `false` when done

**Key rules:**
- Set `DrivesRecorder.PlayOnStart = false` to prevent timing conflicts with LogicStep-triggered replays
- `ReplayRecording` triggers on **positive flank only** (`StartOnSignal` goes from false → true)
- After triggering, **clear the trigger signal** (set back to false) so the next cycle has a clean flank
- DrivesRecorder replays only reproduce **drive positions** — digital output signals (e.g., robot position signals) do NOT fire during replay
- Use `RobotIsLoading` / `RobotIsUnloading` status signals from `ReplayRecording.IsReplayingSignal` instead of robot position signals

**Pattern for triggering and waiting on a replay:**
```python
# Step 1: Set trigger signal (starts replay on rising edge)
component_set(name="03 Start LoadCell", componentType="LogicStep_SetSignalBool",
    properties='{"Signal": ".../LoadCell", "SetToTrue": true}')

# Step 2: Wait for replay to actually start
component_set(name="04 Wait Loading Started", componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": ".../RobotIsLoading", "WaitForTrue": true}')

# Step 3: Wait for replay to finish
component_set(name="05 Wait Loading Done", componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": ".../RobotIsLoading", "WaitForTrue": false}')

# Step 4: Clear trigger signal for next cycle
component_set(name="06 Clear LoadCell", componentType="LogicStep_SetSignalBool",
    properties='{"Signal": ".../LoadCell", "SetToTrue": false}')
```

## Building & Authoring Scenes

The analysis section above covers how to *read* a scene. This section covers how to *build*
one — and the realvirtual-specific traps that make a freshly-built material-flow demo silently
do nothing.

### Required baseline

- The `realvirtual` controller prefab (with `realvirtualController`) must be present in the scene.
- Create new objects as **siblings** of `realvirtual`, never inside it (see *Scene Structure Rules*).

### The three things that silently break material flow

These three account for almost every "I built it but nothing moves / nothing is detected":

1. **MUs must be on layer `rvMU`.** Sensors and transport surfaces only detect MUs on the
   `rvMU*` layers. An MU left on `Default` is invisible to the whole system. A `Source`
   re-layers spawned MUs via its `GenerateOnLayer` field — set it to the layer **name** `rvMU`
   (a layer **index** string like `"17"` is silently ignored).
2. **Drives do not start by themselves.** A `Drive` needs a start impulse: set `JogForward = true`,
   drive it from a signal, or call `drive_jog_forward(name=...)` at runtime. Conveyor direction
   is the drive's **local** axis sign — a rotated prefab can make `+speed` run "backwards" in
   world space, so verify direction with a screenshot during Play.
3. **Beam sensors raycast in a LOCAL direction.** `Sensor.RayCastDirection` is in the sensor's
   local space. The `SensorBeam` prefab is rotated X = 90°, so to shoot the beam across the
   belt (world +Z) the local direction is `(0, 1, 0)`. Set the height so the beam actually
   crosses the MU body. The sensor's `AdditionalRayCastLayers` must contain `rvMU`
   (and/or `rvMUSensor`) or nothing is ever hit.

Also plan a **Sink** at the end of the line, or MUs pile up / fall off the belt, and keep the
`Source` spacing (`GenerateIfDistance`) **larger than the MU length** to avoid a jam.

### Recipe: conveyor + source + sensor + sink

```python
# 1. Conveyor with a running drive
prefab_instantiate(assetPath="Assets/.../Conveyor.prefab", name="Conveyor")
component_set(name="Conveyor", componentType="Drive", properties='{"TargetSpeed": 300, "JogForward": true}')

# 2. Source at the start — spawns MUs onto rvMU
component_set(name="Conveyor/Source", componentType="Source",
    properties='{"GenerateOnLayer": "rvMU", "GenerateIfDistance": 400}')

# 3. Beam sensor across the belt end (local up for the X=90° rotated SensorBeam prefab)
component_set(name="Conveyor/EndSensor", componentType="Sensor",
    properties='{"UseRaycast": true, "RayCastDirection": {"x":0,"y":1,"z":0}, "RayCastLength": 200, "AdditionalRayCastLayers": ["rvMU"]}')

# 4. Validate before playing — catches layer/mask/direction mistakes up front
rv_doctor()

# 5. Play and verify
sim_play()
sensor_get(name="Conveyor/EndSensor")   # poll repeatedly; see verification note below
```

### Validate with `rv_doctor`

`rv_doctor()` scans the whole scene for the common material-flow mis-configurations and returns
`{path, component, severity, problem, fix}` per finding:

- MUs not on an `rvMU*` layer (invisible to sensors/transport)
- Sensors with an **empty or invalid** raycast layer mask (incl. dead `g4a *` layer names)
- Sensors with `UseRaycast` but zero `RayCastLength`
- Sources whose `GenerateOnLayer` is not a real layer name
- Drives with `TargetSpeed = 0` (will never move)

Run it **after building** and **before** a long debug session — it replaces most of the manual
poking. Fix `error` findings first; they usually mean "nothing happens at runtime".

### Verifying event-based things (sensors)

A sensor's momentary `occupied` flips back to `false` between two MUs, which looks like
"not working" even when it is. For pass-through detection, **poll `sensor_get` repeatedly during
Play** and watch for occupancy transitions rather than reading a single instant. Screenshot-first
still applies: a screenshot **during** Play shows the beam colour (occupied vs. free) and whether
the ray actually crosses the belt.

Recommended actuator loop: `component_set → save_scene → sim_play → drive_set_speed →
poll sensor/state → sim_stop`.

## Footguns / Common Errors

Symptom → cause → fix. These are the realvirtual-specific traps that cost the most time:

| Symptom | Cause | Fix |
|---|---|---|
| Conveyor does not run in Play | Drive does not auto-start | Set `JogForward: true`, drive from a signal, or `drive_jog_forward()` |
| Sensor never triggers (`occupied` stays false / `Counter` 0) | MUs on `Default`, not `rvMU`; or sensor mask has no valid layer | Put MUs on `rvMU` (`Source.GenerateOnLayer = "rvMU"`); add `rvMU` to `AdditionalRayCastLayers` |
| Raycast beam shoots into the floor | `RayCastDirection` is **local**; SensorBeam prefab is rotated X = 90° | Use local `(0,1,0)` for a world +Z (across-belt) beam |
| `GenerateOnLayer: "17"` has no effect | Field expects a layer **name**, not an index string | Use the name, e.g. `"rvMU"` |
| MUs pile up / tip over at the belt end | No `Sink`, or `GenerateIfDistance` < MU length | Add a `Sink`; increase `GenerateIfDistance` beyond the MU length |
| Sensor mask contains dead names (`g4a MU`, `g4a SensorMU`) | Legacy pre-6 layer names | Rename to `rvMU` / `rvMUSensor` (fixed in shipped prefabs; run `rv_doctor` to find remaining ones) |
| `component_set` reported `ok` but a field looks unchanged | (fixed) previously a `List<string>`/unresolved reference was a silent no-op | Now returns an error and lists which fields **did** apply (`applied`, `fieldsSet`) — read them |
| Direction sign is inverted | Conveyor speed is the drive's **local** axis; prefab may be rotated | Verify with a screenshot during Play; flip the speed sign |

### Reliable writes & parameter aliases

`component_set` / `component_set_all` no longer fail silently:

- **Unknown field → error** (with a "did you mean …?" hint), never a silent `ok`.
- **Unresolved reference / wrong JSON shape → error**, not a no-op.
- The response includes **`applied`** (the field names actually written) and **`fieldsSet`**, so a
  partial write is visible instead of ambiguous.
- **`List<string>` and other primitive lists are now settable** (e.g. `AdditionalRayCastLayers`).

Parameter names are also tolerant: common aliases are accepted so a wrong guess does not cost a
round-trip. The canonical name always wins if present.

| Canonical parameter | Also accepted |
|---|---|
| `name` (GameObject id) | `target`, `path`, `objectName`, `gameObject`, `object`, `go` |
| `componentType` | `component`, `type`, `componentName` |
| `searchTerm` | `query`, `search`, `term`, `q` |
| `assetPath` | `path`, `prefabPath`, `asset` |
| `methodName` | `method` |

## realvirtual Layer Reference

Material flow depends on these layers. Legacy `g4a *` names are **dead** — use the `rv*` names.
Both a layer **name** and its index are valid where a mask is built, but string layer **fields**
(`Source.GenerateOnLayer`, `Sensor.AdditionalRayCastLayers`) expect the **name**.

| Layer | Index | Purpose |
|---|---|---|
| `rvMUSensor` | 13 | MUs that sensors detect |
| `rvMUTransport` | 14 | MUs on transport surfaces |
| `rvSensor` | 15 | Sensor beams / bodies |
| `rvTransport` | 16 | Transport surfaces |
| `rvMU` | 17 | Movable Units (general) — MUs must be here to be seen |
| `rvSnapping` | 18 | Snap points |
| `rvSimDynamic` / `rvSimStatic` | 19 / 20 | Dynamic / static simulation geometry |
| `rvSelection` | 21 | Selection highlighting |

Legacy → current mapping: `g4a MU` → `rvMU`, `g4a SensorMU` → `rvMUSensor`,
`g4a Transport` → `rvTransport`.

## Publishing a Release (`package_publish`)

`package_publish` triggers the full release flow headlessly: pack the UPM package, upload it to the
download portal, publish it to the UPM scoped registry. It is the MCP equivalent of the Package
Share window's **Publish** tab.

**This is permanent and public.** A `stable` publish reaches every paying customer and cannot be
withdrawn. Treat it like a deploy, not like a query.

```python
# 1. Always dry-run first. Packs, hashes and validates; opens no network connection.
package_publish(target="starter", channel="beta", confirm="yes", dryRun=True)
package_publish_status()   # poll until status is dryrun_done

# 2. The real publish
package_publish(target="starter", channel="beta", confirm="yes")
package_publish_status()   # poll until done / partial / error
```

| Parameter | Required | Meaning |
|---|---|---|
| `target` | yes | `starter` or `professional` |
| `channel` | yes | `beta` or `stable`. There is **no default** |
| `confirm` | yes | Beta: `yes`, `publish` or `publish-beta`. Stable: exactly `PUBLISH-STABLE` |
| `dryRun` | no | Pack + hash + validate only, nothing is uploaded |
| `allowDirty` | no | Bypass the Plastic workspace gate (default `false`) |

Rules that matter when driving this tool:

- **Never invent the `confirm` value.** It exists so a human explicitly asks for a publish. Only pass
  it when the user actually asked to publish; do not supply `PUBLISH-STABLE` on your own initiative.
- **`channel` has no default.** Omitting it is an error, never a stable publish. The window's
  `EditorPrefs["rv_upm_channel"]` default does not apply here, and the tool never writes that pref.
- **The workspace gate blocks a dirty *target package*.** Uncommitted changes inside the package
  being published — including newly added, not yet checked-in files — abort the publish and are
  listed under `blockingChanges`. Check in first; use `allowDirty=true` only deliberately.
- **Changes elsewhere under `Packages/` only warn.** They appear in `warnings` and
  `otherPendingChanges` but do not block, so `packages-lock.json` alone never stops a publish.
  Publishing `professional` while `starter` is dirty adds an explicit `DEPENDENCY WARNING` —
  read it, it means the published package may lean on uncommitted Starter code.
- **Read both outcomes.** `package_publish_status` returns `uploaded` and `registryPublished`
  separately. `status: "partial"` means the tarball reached the portal but the registry publish
  failed — the version is **not** served to customers yet. Do not report that as success.

Status values: `idle`, `running`, `dryrun_done`, `done`, `partial`, `error`, `refused`.

Release notes (`POST /registry/news`) are **not** posted by this tool; use the window for that.
