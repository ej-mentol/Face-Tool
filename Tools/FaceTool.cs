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
        public Face? SourceFace { get; set; }
        public IMapObject? SourceObject { get; set; }
        public Solid? SourceSolid { get; set; }

        public Face? TargetFace { get; set; }
        public IMapObject? TargetObject { get; set; }
        public Solid? TargetSolid { get; set; }

        public bool ShowHoverHelper { get; set; } = true;

        private UI.FaceToolWindow _window;

        public FaceTool()
        {
            Usage = ToolUsage.View3D;
            _window = new UI.FaceToolWindow(this);
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
            SourceFace = null;
            SourceObject = null;
            SourceSolid = null;
            TargetFace = null;
            TargetObject = null;
            TargetSolid = null;
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
            if (viewport == null || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)) return;

            var hit = RaycastFace(document, camera, e.X, e.Y);

            if (hit == null)
            {
                // Click in empty space — clear selection
                if (e.Button == MouseButtons.Left)
                {
                    SourceFace = null;
                    SourceObject = null;
                    SourceSolid = null;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    TargetFace = null;
                    TargetObject = null;
                    TargetSolid = null;
                }
                viewport.Control.Invalidate();
                return;
            }

            var targetObject = ResolveScope(hit.Value.Solid, CurrentScope);

            if (e.Button == MouseButtons.Left)
            {
                // Ctrl + Left Click = Pick Target, Left Click = Pick Source
                if (Control.ModifierKeys == Keys.Control)
                {
                    TargetFace = hit.Value.Face;
                    TargetObject = targetObject;
                    TargetSolid = hit.Value.Solid;
                }
                else
                {
                    SourceFace = hit.Value.Face;
                    SourceObject = targetObject;
                    SourceSolid = hit.Value.Solid;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                TargetFace = hit.Value.Face;
                TargetObject = targetObject;
                TargetSolid = hit.Value.Solid;
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

        private void RefreshReferences(MapDocument document)
        {
            if (SourceObject != null)
            {
                var currentSourceObj = document.Map.Root.FindByID(SourceObject.ID);
                if (currentSourceObj == null)
                {
                    SourceFace = null;
                    SourceObject = null;
                    SourceSolid = null;
                }
                else
                {
                    SourceObject = currentSourceObj;
                    if (SourceSolid != null)
                    {
                        var currentSolid = document.Map.Root.FindByID(SourceSolid.ID) as Solid;
                        if (currentSolid != null)
                        {
                            SourceSolid = currentSolid;
                            if (SourceFace != null)
                            {
                                SourceFace = currentSolid.Faces.FirstOrDefault(f => f.ID == SourceFace.ID);
                            }
                        }
                        else
                        {
                            SourceFace = null;
                            SourceSolid = null;
                        }
                    }
                }
            }

            if (TargetObject != null)
            {
                var currentTargetObj = document.Map.Root.FindByID(TargetObject.ID);
                if (currentTargetObj == null)
                {
                    TargetFace = null;
                    TargetObject = null;
                    TargetSolid = null;
                }
                else
                {
                    TargetObject = currentTargetObj;
                    if (TargetSolid != null)
                    {
                        var currentSolid = document.Map.Root.FindByID(TargetSolid.ID) as Solid;
                        if (currentSolid != null)
                        {
                            TargetSolid = currentSolid;
                            if (TargetFace != null)
                            {
                                TargetFace = currentSolid.Faces.FirstOrDefault(f => f.ID == TargetFace.ID);
                            }
                        }
                        else
                        {
                            TargetFace = null;
                            TargetSolid = null;
                        }
                    }
                }
            }
        }

        protected override void Render(MapDocument document, BufferBuilder builder, Sledge.BspEditor.Rendering.Resources.ResourceCollector resourceCollector)
        {
            base.Render(document, builder, resourceCollector);
            RefreshReferences(document);

            var verts = new List<VertexStandard>();
            var indices = new List<uint>();
            var groups = new List<BufferGroup>();

            // Source Face (Blue)
            if (SourceFace != null)
            {
                var selectionColour = Color.FromArgb(64, Color.DeepSkyBlue).ToVector4();
                RenderFace(SourceFace, selectionColour, verts, indices, groups);
            }

            // Target Face (Green)
            if (TargetFace != null)
            {
                var selectionColour = Color.FromArgb(64, Color.LimeGreen).ToVector4();
                RenderFace(TargetFace, selectionColour, verts, indices, groups);
            }

            // Hover Face (Yellow) — preview of what a click would select, skipped if already committed
            if (ShowHoverHelper && HoverFace != null && HoverFace != SourceFace && HoverFace != TargetFace)
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
