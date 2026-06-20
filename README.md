# HammerTime Face Tool

> [!IMPORTANT]
> **Experimental Release Candidate (RC) & Format Notice**
> *Developed using AI with the active participation of the repository owner.*
> 
> This plugin is currently in an experimental Release Candidate (RC) state. As HammerTime and other editors (like J.A.C.K.) evolve, there is a minor theoretical risk of format deviations. To ensure maximum safety for your primary work, keep in mind that subtle incompatibilities *could* occur.
> 
> For absolute reliability, you can:
> * Save your map files in the standard `.map` format.
> * Alternatively, perform complex alignment operations in a separate temporary map using a simple orthogonal reference brush (such as a $90^\circ$ block) as an anchor, then copy the aligned geometry back to your primary editor.

A plugin for the **HammerTime** level editor designed for quick and precise alignment, snapping, cloning, and trimming of geometric objects (brushes/solids, groups, and entities) relative to the planes of selected faces.

## Key Features

* **Align** — Rotates the source object so that the normal of the selected source face matches the opposite normal of the target face.
* **Snap** — Moves the object in space so that it touches the target face without penetration (uses closest-vertex snapping logic).
* **Align + Snap** — Combined atomic operation of alignment and snapping.
* **Clone to Face** — Clones the source object and aligns/snaps the clone to the target face.
* **Trim** — Cuts/trims the source solid using the plane of the target face.
* **Place & Trim** — Aligns, snaps, and trims the object against the target face in a single atomic action.
* **Restore** — Restores the object to its original position using the system of control points (Anchors).

---

## Mathematics and Logic

### 1. Rotation Locks
To provide precise control over object orientation, rotation locks are available:
* **Free Rotation (all locks unchecked)**: The object is rotated along the shortest 3D arc. Normals align perfectly, but the object can tilt sideways (roll) relative to the horizon.
* **Partial Locks (1 lock checked)**: Rotation is decomposed into **ZXY (Yaw-Pitch-Roll)** Tait-Bryan Euler angles. The locked axis angle is zeroed out, and the quaternion is reconstructed.
* **Single Axis Restrictions (2 locks checked)**: When rotation is allowed around only one axis (e.g. X and Y are locked, allowing only Z/Yaw), vectors are projected directly onto the 2D plane perpendicular to the free axis. This is mathematically robust and avoids gimbal lock, keeping the object strictly upright.

### 2. Closest-Vertex Snapping
Instead of centroid-based snapping, which causes brushes to sink into walls or hover in mid-air on slanted surfaces:
* The algorithm calculates the signed distance of all vertices of the source face to the target plane.
* It selects the vertex with the minimum signed distance (the one that would penetrate furthest into the plane).
* The object is shifted by this distance (plus user-defined offset). The geometry touches the target plane at its closest point without clipping through.

---

## Selection Scope

The tool supports different levels of object hierarchy via the **Scope** selector:
* **Auto** — Automatically resolves the clicked solid to its top-level parent (Group or Entity).
* **Brush** — Only transforms the specific clicked brush (Solid).
* **Group** — Transforms the entire group that contains the clicked brush.
* **Entity** — Transforms the entire entity object containing the clicked brush.

---

## Settings Persistence

UI configurations and window coordinates are automatically saved when the tool window is hidden or when the editor is closed.
* Settings are stored at: `%APPDATA%\Hammertime\FaceToolSettings.json`.
* Serialization uses the native **`System.Text.Json`** library without external dependencies.
* Multi-monitor configurations (including negative screen coordinates for left-side monitors) are supported.

---

## Requirements

* Target Framework: **.NET 6.0-windows**
* Host Application: **HammerTime** level editor
