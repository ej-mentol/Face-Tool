using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Rendering.Viewport;
using Sledge.BspEditor.Tools;
using System.IO;
using Sledge.Common.Shell;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Hotkeys;
using Sledge.Common.Shell.Settings;
using Sledge.Common.Translations;
using Sledge.Shell;
using Sledge.DataStructures.Geometric;
using Sledge.Rendering.Cameras;
using Sledge.Rendering.Pipelines;
using Sledge.Rendering.Primitives;
using Sledge.Rendering.Resources;
using Face = Sledge.BspEditor.Primitives.MapObjectData.Face;

namespace HammerTime.FaceTool.Tools
{
    [Export(typeof(ITool))]
    [Export(typeof(ISettingsContainer))]
    [Export]
    [OrderHint("Z")]
    [DefaultHotkey("Shift+F")]
    public class FaceTool : BaseTool, ISettingsContainer, System.ComponentModel.Composition.IPartImportsSatisfiedNotification
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
            MiterJoin,
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

        [Import]
        private TranslationStringsCatalog _catalog = null!;

        [Import]
        private Lazy<ITranslationStringProvider> _translator = null!;

        [Import(AllowDefault = true)]
        private IApplicationInfo _appInfo = null!;

        private UI.FaceToolWindow _window = null!;
        private bool _active = false;
        private bool _processingOperation = false;

        public FaceTool()
        {
            Usage = ToolUsage.View3D;
            Oy.Subscribe<ITool>("Tool:Activated", ToolActivated);
        }

        public void OnImportsSatisfied()
        {
            _window = new UI.FaceToolWindow(this, _translator);

            if (_appInfo != null)
            {
                var folder = _appInfo.GetApplicationSettingsFolder("Translations");
                if (folder != null)
                {
                    try
                    {
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                        var file = Path.Combine(folder, "HammerTime.FaceTool.en.json");
                        if (!File.Exists(file))
                        {
                            File.WriteAllText(file, GetDefaultTranslationContent(), System.Text.Encoding.UTF8);
                        }
                    }
                    catch
                    {
                        // Ignore file system write errors
                    }
                }
            }

            _catalog.Load(typeof(FaceTool));

            // Fallback translation registration in case localized json file isn't loaded
            foreach (var langCode in new[] { "en", "debug_en" })
            {
                if (_catalog.Languages.ContainsKey(langCode))
                {
                    var lang = _catalog.Languages[langCode];
                    
                    if (!lang.Collection.Settings.ContainsKey("@Group.Tools/Plugins/Face Tool"))
                        lang.Collection.Settings["@Group.Tools/Plugins/Face Tool"] = "Face Tool";

                    var settingsPrefix = "HammerTime.FaceTool.FaceToolSettings.";
                    var fallbackSettings = new Dictionary<string, string>
                    {
                        { settingsPrefix + "HotkeysActiveOnlyWhenWindowFocused", "Hotkeys active only when Face Tool window is focused" },
                        { settingsPrefix + "ShowHoverHelper", "Show face hover preview helper" },
                        { settingsPrefix + "HelperResetDelay", "Visual helper reset delay (ms)" },
                        { settingsPrefix + "KeyAlign", "Hotkey: Align mode" },
                        { settingsPrefix + "KeySnap", "Hotkey: Snap mode" },
                        { settingsPrefix + "KeyAlignSnap", "Hotkey: Align & Snap mode" },
                        { settingsPrefix + "KeyCloneToFace", "Hotkey: Clone to face mode" },
                        { settingsPrefix + "KeyTrim", "Hotkey: Trim mode" },
                        { settingsPrefix + "KeyRectify", "Hotkey: Rectify mode" },
                        { settingsPrefix + "KeyPlaceTrim", "Hotkey: Place & Trim mode" }
                    };

                    foreach (var kv in fallbackSettings)
                    {
                        if (!lang.Collection.Settings.ContainsKey(kv.Key))
                            lang.Collection.Settings[kv.Key] = kv.Value;
                    }

                    var windowPrefix = "HammerTime.FaceTool.FaceToolWindow.";
                    var fallbackStrings = new Dictionary<string, string>
                    {
                        { windowPrefix + "AlignToolTip", "Align: Rotates the selected object so its source face matches the normal of the target face.\nSHIFT: (No modifier effect)." },
                        { windowPrefix + "SnapToolTip", "Snap: Moves the selected object to touch the target plane using the closest vertex.\nSHIFT: Snaps using the furthest vertex (places object on the opposite side)." },
                        { windowPrefix + "AlignSnapToolTip", "Align + Snap: Rotates and moves the object to touch the target face, aligning their axes.\nSHIFT: (No modifier effect)." },
                        { windowPrefix + "CloneToFaceToolTip", "Clone To Face: Creates a clone of the object and snaps it onto the target face.\nCan create an array of clones if Array counts > 1.\nSHIFT: (No modifier effect)." },
                        { windowPrefix + "TrimToolTip", "Trim: Cuts the selected solids using the plane of the clicked target face.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)." },
                        { windowPrefix + "PlaceTrimToolTip", "Place & Trim: Aligns, snaps, and cuts the object against the target face in one action.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)." },
                        { windowPrefix + "MiterJoinToolTip", "Miter Join: Joins two intersecting objects at a clean diagonal angle (bisector split).\nSHIFT: Inverts the cut direction for both objects." },
                        { windowPrefix + "RectifyToolTip", "Rectify: Aligns the selected object to the world axes based on its current face normal.\nSHIFT: (No modifier effect)." },
                        { windowPrefix + "RestoreToolTip", "Restore from Anchor: Restores the object to its original position recorded before alignment." },
                        { windowPrefix + "ClearAnchorToolTip", "Clear Anchor: Clears the stored origin/rotation anchor data." },

                        { windowPrefix + "Title", "Face Tool" },
                        { windowPrefix + "GroupOperations", "Operations" },
                        { windowPrefix + "GroupAnchor", "Anchor / Rectification" },
                        { windowPrefix + "GroupPlacement", "Placement" },
                        { windowPrefix + "GroupClone", "Clone Array Options" },
                        { windowPrefix + "GroupOptions", "Options" },
                        { windowPrefix + "GroupPresets", "Presets" },
                        { windowPrefix + "Align", "Align" },
                        { windowPrefix + "Snap", "Snap" },
                        { windowPrefix + "AlignSnap", "Align + Snap" },
                        { windowPrefix + "CloneToFace", "Clone To Face" },
                        { windowPrefix + "Trim", "Trim" },
                        { windowPrefix + "PlaceTrim", "Place && Trim" },
                        { windowPrefix + "MiterJoin", "Miter Join" },
                        { windowPrefix + "LockMode", "Lock Mode" },
                        { windowPrefix + "CtrlMode", "Ctrl Mode" },
                        { windowPrefix + "Rectify", "Rectify" },
                        { windowPrefix + "Restore", "Restore from Anchor" },
                        { windowPrefix + "ClearAnchor", "Clear Anchor" },
                        { windowPrefix + "Preset", "Preset:" },
                        { windowPrefix + "KeepHierarchy", "Keep Hierarchy" },
                        { windowPrefix + "SnapToGrid", "Snap To Grid" },
                        { windowPrefix + "Offset", "Offset:" },
                        { windowPrefix + "Scope", "Scope:" },
                        { windowPrefix + "PositionLocks", "Position Locks:" },
                        { windowPrefix + "RotationLocks", "Rotation Locks:" },
                        { windowPrefix + "Count", "Count:" },
                        { windowPrefix + "Spacing", "Spacing:" }
                    };

                    foreach (var kv in fallbackStrings)
                    {
                        if (!lang.Collection.Strings.ContainsKey(kv.Key))
                            lang.Collection.Strings[kv.Key] = kv.Value;
                    }
                }
            }
        }

        private static string GetDefaultTranslationContent()
        {
            return @"{
    ""@Meta"": {
        ""Base"": ""HammerTime.FaceTool"",
        ""Language"": ""en"",
        ""LanguageDescription"": ""English"",
        ""Inherit"": """"
    },
    ""@Settings"": {
        ""@Group.Tools/Plugins/Face Tool"": ""Face Tool"",
        ""FaceToolSettings"": {
            ""HotkeysActiveOnlyWhenWindowFocused"": ""Hotkeys active only when Face Tool window is focused"",
            ""ShowHoverHelper"": ""Show face hover preview helper"",
            ""HelperResetDelay"": ""Visual helper reset delay (ms)"",
            ""KeyAlign"": ""Hotkey: Align mode"",
            ""KeySnap"": ""Hotkey: Snap mode"",
            ""KeyAlignSnap"": ""Hotkey: Align & Snap mode"",
            ""KeyCloneToFace"": ""Hotkey: Clone to face mode"",
            ""KeyTrim"": ""Hotkey: Trim mode"",
            ""KeyRectify"": ""Hotkey: Rectify mode"",
            ""KeyPlaceTrim"": ""Hotkey: Place & Trim mode""
        }
    },
    ""FaceToolWindow"": {
        ""AlignToolTip"": ""Align: Rotates the selected object so its source face matches the normal of the target face.\nSHIFT: (No modifier effect)."",
        ""SnapToolTip"": ""Snap: Moves the selected object to touch the target plane using the closest vertex.\nSHIFT: Snaps using the furthest vertex (places object on the opposite side)."",
        ""AlignSnapToolTip"": ""Align + Snap: Rotates and moves the object to touch the target face, aligning their axes.\nSHIFT: (No modifier effect)."",
        ""CloneToFaceToolTip"": ""Clone To Face: Creates a clone of the object and snaps it onto the target face.\nCan create an array of clones if Array counts > 1.\nSHIFT: (No modifier effect)."",
        ""TrimToolTip"": ""Trim: Cuts the selected solids using the plane of the clicked target face.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)."",
        ""PlaceTrimToolTip"": ""Place & Trim: Aligns, snaps, and cuts the object against the target face in one action.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)."",
        ""MiterJoinToolTip"": ""Miter Join: Joins two intersecting objects at a clean diagonal angle (bisector split).\nSHIFT: Inverts the cut direction for both objects."",
        ""RectifyToolTip"": ""Rectify: Aligns the selected object to the world axes based on its current face normal.\nSHIFT: (No modifier effect)."",
        ""RestoreToolTip"": ""Restore from Anchor: Restores the object to its original position recorded before alignment."",
        ""ClearAnchorToolTip"": ""Clear Anchor: Clears the stored origin/rotation anchor data."",

        ""Title"": ""Face Tool"",
        ""GroupOperations"": ""Operations"",
        ""GroupAnchor"": ""Anchor / Rectification"",
        ""GroupPlacement"": ""Placement"",
        ""GroupClone"": ""Clone Array Options"",
        ""GroupOptions"": ""Options"",
        ""GroupPresets"": ""Presets"",
        ""Align"": ""Align"",
        ""Snap"": ""Snap"",
        ""AlignSnap"": ""Align + Snap"",
        ""CloneToFace"": ""Clone To Face"",
        ""Trim"": ""Trim"",
        ""PlaceTrim"": ""Place && Trim"",
        ""MiterJoin"": ""Miter Join"",
        ""LockMode"": ""Lock Mode"",
        ""CtrlMode"": ""Ctrl Mode"",
        ""Rectify"": ""Rectify"",
        ""Restore"": ""Restore from Anchor"",
        ""ClearAnchor"": ""Clear Anchor"",
        ""Preset"": ""Preset:"",
        ""KeepHierarchy"": ""Keep Hierarchy"",
        ""SnapToGrid"": ""Snap To Grid"",
        ""Offset"": ""Offset:"",
        ""Scope"": ""Scope:"",
        ""PositionLocks"": ""Position Locks:"",
        ""RotationLocks"": ""Rotation Locks:"",
        ""Count"": ""Count:"",
        ""Spacing"": ""Spacing:""
    }
}";
        }

        private void ToolActivated(ITool tool)
        {
            if (tool == null) return;
            string toolName = tool.Name;
            if (toolName == "Face Tool" || toolName == "SelectTool")
            {
                if (_active)
                {
                    _window.InvokeLater(() => {
                        if (!_window.Visible)
                        {
                            _window.Show();
                        }
                    });
                }
            }
            else
            {
                _window.InvokeLater(() => _window.Hide());
                CurrentMode = ToolMode.None;
                _selectedFaces.Clear();
                _window.UpdateInterfaceForMode(ToolMode.None);
            }
        }

        public async Task SetCurrentMode(ToolMode mode)
        {
            await SetCurrentModeInternal(mode, publishToolChange: true);
        }

        private async Task SetCurrentModeInternal(ToolMode mode, bool publishToolChange)
        {
            if (mode == CurrentMode)
            {
                mode = ToolMode.None;
            }

            var doc = GetDocument();

            if (mode == ToolMode.Snap || mode == ToolMode.Trim)
            {
                if (doc == null || doc.Selection == null || doc.Selection.IsEmpty)
                {
                    MessageBox.Show("Please select one or more objects first.", "Face Tool Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    mode = ToolMode.None;
                }
            }

            CurrentMode = mode;
            _selectedFaces.Clear();

            if (doc != null && CurrentMode != ToolMode.None && CurrentMode != ToolMode.Snap && CurrentMode != ToolMode.Trim)
            {
                if (doc.Selection != null && !doc.Selection.IsEmpty)
                {
                    await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc,
                        new Sledge.BspEditor.Modification.Operations.Selection.Deselect(doc.Selection.ToList()));
                }
            }

            if (publishToolChange)
            {
                if (CurrentMode == ToolMode.None)
                {
                    await Oy.Publish("ActivateTool", "SelectTool");
                }
                else
                {
                    await Oy.Publish("ActivateTool", "Face Tool");
                }
            }

            _window.UpdateInterfaceForMode(CurrentMode);
        }

        public async Task OperationComplete()
        {
            try
            {
                bool keepModeActive = _window.IsOperationLockChecked;

                if (HelperResetDelay > 0)
                {
                    await Task.Delay(HelperResetDelay);
                }

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
                    await SetCurrentMode(ToolMode.None);
                }
            }
            finally
            {
                _processingOperation = false;
            }
        }

        private async void RunOperation(ToolMode mode, List<SelectedFace> faces, bool altMode = false)
        {
            try
            {
                await _window.PerformOperation(mode, faces, altMode);
            }
            catch (Exception ex)
            {
                _processingOperation = false;
                MessageBox.Show(ex.Message, "Face Tool Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _active = true;
            var parent = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "Shell") ?? Form.ActiveForm;
            if (parent != null && parent != _window)
            {
                _window.Owner = parent;
            }
            _window.Show();
            await base.ToolSelected();

            if (CurrentMode == ToolMode.None)
            {
                await Task.Delay(50);
                await Oy.Publish("ActivateTool", "SelectTool");
            }
        }

        public void DeactivatePlugin()
        {
            _active = false;
            _ = SetCurrentMode(ToolMode.None);
        }

        public override async Task ToolDeselected()
        {
            await SetCurrentModeInternal(ToolMode.None, publishToolChange: false);
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

            if (_processingOperation) return;

            // Exit mode on right click
            if (e.Button == MouseButtons.Right)
            {
                if (CurrentMode != ToolMode.None)
                {
                    _ = SetCurrentMode(ToolMode.None);
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
            bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            bool requireCtrl = _window.IsCtrlModeChecked;
            int step = _selectedFaces.Count;

            // Rectify: 1-click
            if (CurrentMode == ToolMode.Rectify)
            {
                _processingOperation = true;
                _selectedFaces.Add(selectedFaceInfo);
                RunOperation(CurrentMode, new List<SelectedFace>(_selectedFaces));
                return;
            }

            // Snap & Trim: 1-click on target face
            if (CurrentMode == ToolMode.Snap || CurrentMode == ToolMode.Trim)
            {
                if (!requireCtrl || ctrl)
                {
                    _processingOperation = true;
                    _selectedFaces.Clear();
                    _selectedFaces.Add(selectedFaceInfo); // Target
                    
                    bool useAltMode = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                    RunOperation(CurrentMode, new List<SelectedFace>(_selectedFaces), useAltMode);
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
                if (!requireCtrl || ctrl)
                {
                    _processingOperation = true;
                    _selectedFaces.Add(selectedFaceInfo);
                    bool useAltMode = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                    RunOperation(CurrentMode, new List<SelectedFace>(_selectedFaces), useAltMode);
                }
                else
                {
                    // If Ctrl mode is enabled but Ctrl is not pressed, a click updates the source face
                    _selectedFaces[0] = selectedFaceInfo;
                }
            }
            else
            {
                // Stale state — reset and start fresh
                _selectedFaces.Clear();
                _selectedFaces.Add(selectedFaceInfo);
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

        // ISettingsContainer implementation
        public string Name => "HammerTime.FaceTool.FaceToolSettings";
        public bool ValuesLoaded { get; private set; } = false;

        public bool HotkeysActiveOnlyWhenWindowFocused { get; set; } = false;
        public int HelperResetDelay { get; set; } = 500;
        public Keys KeyAlign { get; set; } = Keys.D1;
        public Keys KeySnap { get; set; } = Keys.D2;
        public Keys KeyAlignSnap { get; set; } = Keys.D3;
        public Keys KeyCloneToFace { get; set; } = Keys.D4;
        public Keys KeyTrim { get; set; } = Keys.D5;
        public Keys KeyRectify { get; set; } = Keys.D6;
        public Keys KeyPlaceTrim { get; set; } = Keys.D7;

        public IEnumerable<SettingKey> GetKeys()
        {
            yield return new SettingKey("Tools/Plugins/Face Tool", "HotkeysActiveOnlyWhenWindowFocused", typeof(bool));
            yield return new SettingKey("Tools/Plugins/Face Tool", "ShowHoverHelper", typeof(bool));
            yield return new SettingKey("Tools/Plugins/Face Tool", "HelperResetDelay", typeof(int));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyAlign", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeySnap", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyAlignSnap", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyCloneToFace", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyTrim", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyRectify", typeof(Keys));
            yield return new SettingKey("Tools/Plugins/Face Tool", "KeyPlaceTrim", typeof(Keys));
        }

        public void LoadValues(ISettingsStore store)
        {
            HotkeysActiveOnlyWhenWindowFocused = store.Get("HotkeysActiveOnlyWhenWindowFocused", false);
            ShowHoverHelper = store.Get("ShowHoverHelper", true);
            HelperResetDelay = store.Get("HelperResetDelay", 500);
            KeyAlign = store.Get("KeyAlign", Keys.D1);
            KeySnap = store.Get("KeySnap", Keys.D2);
            KeyAlignSnap = store.Get("KeyAlignSnap", Keys.D3);
            KeyCloneToFace = store.Get("KeyCloneToFace", Keys.D4);
            KeyTrim = store.Get("KeyTrim", Keys.D5);
            KeyRectify = store.Get("KeyRectify", Keys.D6);
            KeyPlaceTrim = store.Get("KeyPlaceTrim", Keys.D7);
            
            ValuesLoaded = true;
        }

        public void StoreValues(ISettingsStore store)
        {
            store.Set("HotkeysActiveOnlyWhenWindowFocused", HotkeysActiveOnlyWhenWindowFocused);
            store.Set("ShowHoverHelper", ShowHoverHelper);
            store.Set("HelperResetDelay", HelperResetDelay);
            store.Set("KeyAlign", KeyAlign);
            store.Set("KeySnap", KeySnap);
            store.Set("KeyAlignSnap", KeyAlignSnap);
            store.Set("KeyCloneToFace", KeyCloneToFace);
            store.Set("KeyTrim", KeyTrim);
            store.Set("KeyRectify", KeyRectify);
            store.Set("KeyPlaceTrim", KeyPlaceTrim);
        }

        // Viewport keyboard event overrides
        protected override void KeyDown(MapDocument document, MapViewport viewport, PerspectiveCamera camera, ViewportEvent e)
        {
            if (!HotkeysActiveOnlyWhenWindowFocused && ProcessModeChangeKey(e.KeyCode))
            {
                e.Handled = true;
                return;
            }
            base.KeyDown(document, viewport, camera, e);
        }

        protected override void KeyDown(MapDocument document, MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            if (!HotkeysActiveOnlyWhenWindowFocused && ProcessModeChangeKey(e.KeyCode))
            {
                e.Handled = true;
                return;
            }
            base.KeyDown(document, viewport, camera, e);
        }

        public bool ProcessModeChangeKey(Keys keyData)
        {
            if (_processingOperation) return false;
            if (keyData == KeyAlign) { _ = SetCurrentMode(ToolMode.Align); return true; }
            if (keyData == KeySnap) { _ = SetCurrentMode(ToolMode.Snap); return true; }
            if (keyData == KeyAlignSnap) { _ = SetCurrentMode(ToolMode.AlignSnap); return true; }
            if (keyData == KeyCloneToFace) { _ = SetCurrentMode(ToolMode.CloneToFace); return true; }
            if (keyData == KeyTrim) { _ = SetCurrentMode(ToolMode.Trim); return true; }
            if (keyData == KeyRectify) { _ = SetCurrentMode(ToolMode.Rectify); return true; }
            if (keyData == KeyPlaceTrim) { _ = SetCurrentMode(ToolMode.PlaceTrim); return true; }
            return false;
        }
    }
}
