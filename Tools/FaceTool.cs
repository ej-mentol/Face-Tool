using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Rendering.Viewport;
using Sledge.BspEditor.Tools;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Hotkeys;
using Sledge.DataStructures.Geometric;
using Sledge.Rendering.Cameras;
using Sledge.Rendering.Pipelines;
using Sledge.Rendering.Primitives;
using Sledge.Rendering.Resources;
using Face = Sledge.BspEditor.Primitives.MapObjectData.Face;

namespace HammerTime.FaceTool.Tools
{
    [Export(typeof(ITool))]
    [Export]
    [OrderHint("Z")]
    [DefaultHotkey("Shift+F")]
    public class FaceTool : BaseTool
    {
        // New state machine logic
        public enum ToolMode
        {
            None,
            Align,
            Snap,
            AlignSnap,
            CloneToFace,
            Trim,
            Rectify,
            PlaceTrim,
        }
        
        public struct SelectedFace
        {
            public Face Face { get; }
            public Solid Solid { get; }
            public IMapObject TransformableObject { get; }

            public SelectedFace(Face face, Solid solid, IMapObject transformableObject)
            {
                Face = face;
                Solid = solid;
                TransformableObject = transformableObject;
            }
        }

        public ToolMode CurrentMode { get; private set; } = ToolMode.None;
        private readonly List<SelectedFace> _selectedFaces = new List<SelectedFace>();

        // Compatibility properties for old code (Snap, Rectify, Clone ops)
        public Face? SourceFace => _selectedFaces.Count > 0 ? _selectedFaces[0].Face : null;
        public Solid? SourceSolid => _selectedFaces.Count > 0 ? _selectedFaces[0].Solid : null;
        public IMapObject? SourceObject => _selectedFaces.Count > 0 ? _selectedFaces[0].TransformableObject : null;
        public Face? TargetFace => _selectedFaces.Count > 1 ? _selectedFaces[1].Face : null;
        public Solid? TargetSolid => _selectedFaces.Count > 1 ? _selectedFaces[1].Solid : null;
        public IMapObject? TargetObject => _selectedFaces.Count > 1 ? _selectedFaces[1].TransformableObject : null;

        public bool ShowHoverHelper { get; set; } = true;

        private readonly UI.FaceToolWindow _window;

        public FaceTool()
        {
            Usage = ToolUsage.View3D;
            _window = new UI.FaceToolWindow(this);
        }

        public void SetCurrentMode(ToolMode mode)
        {
            if (mode == CurrentMode)
            {
                mode = ToolMode.None;
            }

            CurrentMode = mode;
            _selectedFaces.Clear();
            _window.UpdateInterfaceForMode(CurrentMode);
        }

        public void OperationComplete()
        {
            bool keepModeActive = _window.IsOperationLockChecked;

            // Clear selections from the last operation
            _selectedFaces.Clear();

            if (keepModeActive)
            {
                // Partial reset: just update the UI, stay in the current mode for the next operation
                _window.UpdateInterfaceForMode(CurrentMode);
            }
            else
            {
                // Full reset: turn off the mode
                SetCurrentMode(ToolMode.None);
            }
        }

        // Methods for legacy compatibility
        public void ClearAllSelections()
        {
            _selectedFaces.Clear();
        }

        public void ClearTargetInSelection()
        {
            if (_selectedFaces.Count > 1)
            {
                _selectedFaces.RemoveAt(1);
            }
        }

        public override async Task ToolSelected()
        {
            var parent = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "Shell") ?? Form.ActiveForm;
            if (parent != null)
            {
                _window.Owner = parent;
            }
            _window.Show();
            await base.ToolSelected();
        }

        public override async Task ToolDeselected()
        {
            _window.Hide();
            SetCurrentMode(ToolMode.None); // Ensure state is fully reset
            HoverFace = null;
            await base.ToolDeselected();
        }

        public override Image GetIcon()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "HammerTime.FaceTool.Resources.Tool_FaceTool.png";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var original = Image.FromStream(stream))
                        {
                            var resized = new Bitmap(32, 32);
                            using (var g = Graphics.FromImage(resized))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(original, 0, 0, 32, 32);
                            }
                            return resized;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default system application icon
            }
            return SystemIcons.Application.ToBitmap();
        }

        public override string GetName()
        {
            return "Face Tool";
        }

        public enum SelectionScope
        {
            Auto,
            Brush,
            Group,
            Entity
        }

        public SelectionScope CurrentScope { get; set; } = SelectionScope.Auto;

        // Face under cursor, updated on MouseMove, shown as a neutral preview before commit.
        public Face? HoverFace { get; set; }

        private static (Face Face, Solid Solid)? RaycastFace(MapDocument document, PerspectiveCamera camera, int x, int y)
        {
            var (start, end) = camera.CastRayFromScreen(new Vector3(x, y, 0));
            var ray = new Line(start, end);

            var hit = document.Map.Root.GetBoudingBoxIntersectionsForVisibleObjects(ray)
                .OfType<Solid>()
                .Where(s => !s.IsHidden())
                .SelectMany(a => a.Faces.Select(f => new { Face = f, Solid = a }))
                .Select(x => new { x.Face, x.Solid, Intersection = new Polygon(x.Face.Vertices).GetIntersectionPoint(ray) })
                .Where(x => x.Intersection != null)
                .OrderBy(x => (x.Intersection.GetValueOrDefault() - ray.Start).Length())
                .FirstOrDefault();

            return hit == null ? null : (hit.Face, hit.Solid);
        }

        protected override void MouseMove(MapDocument document, MapViewport viewport, PerspectiveCamera camera, ViewportEvent e)
        {
            if (viewport == null) return;

            // Dragging (camera orbit via middle button, or any drag) shouldn't fight for hover state.
            if (e.Dragging)
            {
                return;
            }

            if (!ShowHoverHelper)
            {
                if (HoverFace != null)
                {
                    HoverFace = null;
                    viewport.Control.Invalidate();
                }
                return;
            }

            var hit = RaycastFace(document, camera, e.X, e.Y);
            var newHover = hit?.Face;
            if (newHover != HoverFace)
            {
                HoverFace = newHover;
                viewport.Control.Invalidate();
            }
        }

        protected override void MouseLeave(MapDocument document, MapViewport viewport, PerspectiveCamera camera, ViewportEvent e)
        {
            if (HoverFace != null)
            {
                HoverFace = null;
                viewport.Control.Invalidate();
            }
        }

        protected override void MouseDown(MapDocument document, MapViewport viewport, PerspectiveCamera camera, ViewportEvent e)
        {
            if (viewport == null) return;

            // Exit mode on right click
            if (e.Button == MouseButtons.Right)
            {
                if (CurrentMode != ToolMode.None)
                {
                    SetCurrentMode(ToolMode.None);
                    viewport.Control.Invalidate();
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            var hit = RaycastFace(document, camera, e.X, e.Y);

            if (hit == null)
            {
                if (CurrentMode != ToolMode.None)
                {
                    _selectedFaces.Clear();
                    _window.UpdateInterfaceForMode(CurrentMode);
                }
                viewport.Control.Invalidate();
                return;
            }

            // Fully transparent in None mode — pass through to Hammer
            if (CurrentMode == ToolMode.None)
            {
                return;
            }

            // We are in an active mode.
            var selectedFaceInfo = new SelectedFace(hit.Value.Face, hit.Value.Solid, ResolveScope(hit.Value.Solid, CurrentScope));
            bool ctrl = Control.ModifierKeys == Keys.Control;
            int step = _selectedFaces.Count;

            // Rectify: 1-click
            if (CurrentMode == ToolMode.Rectify)
            {
                _selectedFaces.Add(selectedFaceInfo);
                _window.PerformOperation(CurrentMode, new List<SelectedFace>(_selectedFaces));
                return;
            }

            // Snap: no first click needed — uses Hammer selection as Source.
            // Only needs a target face (Ctrl+click).
            if (CurrentMode == ToolMode.Snap)
            {
                if (ctrl)
                {
                    _selectedFaces.Clear();
                    _selectedFaces.Add(selectedFaceInfo); // Target
                    _window.PerformOperation(CurrentMode, new List<SelectedFace>(_selectedFaces));
                }
                viewport.Control.Invalidate();
                return;
            }
            
            // Handle all 2-click operations
            if (step == 0) // Waiting for the first face (Source)
            {
                _selectedFaces.Add(selectedFaceInfo);
            }
            else if (step == 1) // Waiting for the second face (Target)
            {
                if (ctrl)
                {
                    // This is the trigger-click. Select face 2 and execute.
                    _selectedFaces.Add(selectedFaceInfo);
                    _window.PerformOperation(CurrentMode, new List<SelectedFace>(_selectedFaces));
                }
                else
                {
                    // This is a re-selection of the first face (Source)
                    _selectedFaces[0] = selectedFaceInfo;
                }
            }

            viewport.Control.Invalidate();
        }

        public IMapObject ResolveScope(IMapObject solid, SelectionScope scope)
        {
            if (scope == SelectionScope.Brush) return solid;

            if (scope == SelectionScope.Auto)
            {
                IMapObject current = solid;
                while (current.Hierarchy.Parent != null && !(current.Hierarchy.Parent is Root))
                {
                    current = current.Hierarchy.Parent;
                }
                return current;
            }

            IMapObject currentObj = solid;
            IMapObject lastMatch = solid;

            while (currentObj != null)
            {
                if (scope == SelectionScope.Group && currentObj is Sledge.BspEditor.Primitives.MapObjects.Group)
                    lastMatch = currentObj;
                else if (scope == SelectionScope.Entity && currentObj is Sledge.BspEditor.Primitives.MapObjects.Entity)
                    lastMatch = currentObj;

                currentObj = currentObj.Hierarchy.Parent;
            }

            return lastMatch;
        }

        protected override void Render(MapDocument document, BufferBuilder builder, Sledge.BspEditor.Rendering.Resources.ResourceCollector resourceCollector)
        {
            base.Render(document, builder, resourceCollector);

            var verts = new List<VertexStandard>();
            var indices = new List<uint>();
            var groups = new List<BufferGroup>();

            // Draw selected faces
            if (_selectedFaces.Count > 0)
            {
                var source = _selectedFaces[0];
                RenderFace(source.Face, Color.FromArgb(64, Color.DeepSkyBlue).ToVector4(), verts, indices, groups);
            }
            if (_selectedFaces.Count > 1)
            {
                var target = _selectedFaces[1];
                RenderFace(target.Face, Color.FromArgb(64, Color.LimeGreen).ToVector4(), verts, indices, groups);
            }

            // Draw hover face
            var selectedFaceObjects = _selectedFaces.Select(f => f.Face).ToList();
            if (ShowHoverHelper && HoverFace != null && !selectedFaceObjects.Contains(HoverFace))
            {
                var hoverColour = Color.FromArgb(40, Color.Gold).ToVector4();
                RenderFace(HoverFace, hoverColour, verts, indices, groups);
            }

            if (verts.Count > 0)
            {
                builder.Append(verts, indices, groups);
            }
        }

        private void RenderFace(Face face, Vector4 color, List<VertexStandard> verts, List<uint> indices, List<BufferGroup> groups)
        {
            if (face.Vertices.Count < 3) return;

            // DEBUG: flag non-convex faces so we can confirm whether that's the source
            // of the bowtie/torn-edge artifact before committing to angle-sort triangulation.
            if (!face.ToPolygon().IsConvex())
            {
                color = Color.FromArgb(120, Color.Magenta).ToVector4();
            }

            var normalOffset = face.Plane.Normal * 0.2f;

            // --- Draw Solid Flat Face ---
            var indOffs = (uint)indices.Count;
            var offs = (uint)verts.Count;
            var centroid = face.Origin + normalOffset;

            verts.Add(new VertexStandard
            {
                Position = centroid,
                Colour = Vector4.One,
                Tint = color,
                Flags = VertexFlags.FlatColour
            });

            verts.AddRange(face.Vertices.Select(x => new VertexStandard
            {
                Position = x + normalOffset,
                Colour = Vector4.One,
                Tint = color,
                Flags = VertexFlags.FlatColour
            }));

            var vertCount = face.Vertices.Count;
            for (uint i = 0; i < vertCount; i++)
            {
                indices.Add(offs); // centroid
                indices.Add(offs + 1 + i);
                indices.Add(offs + 1 + (i + 1) % (uint)vertCount);
            }

            groups.Add(new BufferGroup(PipelineType.TexturedAlpha, CameraType.Perspective, face.Origin, indOffs, (uint)(indices.Count - indOffs)));

            // --- Draw Wireframe Outline (Border) ---
            var wfIndOffs = (uint)indices.Count;
            var wfOffs = (uint)verts.Count;

            var outlineColour = new Vector4(color.X, color.Y, color.Z, 1f);

            verts.AddRange(face.Vertices.Select(x => new VertexStandard
            {
                Position = x + normalOffset,
                Colour = outlineColour,
                Tint = Vector4.One
            }));

            for (var i = 0; i < vertCount; i++)
            {
                indices.Add(wfOffs + (uint)i);
                indices.Add(wfOffs + (uint)((i + 1) % vertCount));
            }

            groups.Add(new BufferGroup(PipelineType.Wireframe, CameraType.Perspective, face.Origin, wfIndOffs, (uint)(indices.Count - wfIndOffs)));
        }
    }
}
