using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using Sledge.BspEditor.Documents;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations;
using Sledge.BspEditor.Modification.Operations.Mutation;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.Shell;

namespace HammerTime.FaceTool.UI
{
    public class FaceToolWindow : Form
    {
        private readonly Tools.FaceTool _tool;

        private Button btnAlign = null!;
        private Button btnSnap = null!;
        private Button btnAlignSnap = null!;
        private Button btnTrim = null!;
        private Button btnRectify = null!;
        private Button btnRestore = null!;
        private Button btnClearAnchor = null!;
        private Button btnClone = null!;
        private Button btnPlaceTrim = null!;

        private CheckBox chkLockX = null!;
        private CheckBox chkLockY = null!;
        private CheckBox chkLockZ = null!;

        private CheckBox chkRotX = null!;
        private CheckBox chkRotY = null!;
        private CheckBox chkRotZ = null!;

        private CheckBox chkSnapGrid = null!;
        private CheckBox chkInvertTrimSide = null!;
        private CheckBox chkShowHoverHelper = null!;
        private CheckBox chkInvertNext = null!;

        private ComboBox cmbScope = null!;

        private NumericUpDown numOffset = null!;

        public FaceToolWindow(Tools.FaceTool tool)
        {
            _tool = tool;
            InitializeComponent();

            Oy.Subscribe<bool>("Theme:Changed", dark => {
                this.InvokeLater(() => Sledge.Shell.Registers.DialogRegister.ColorControlsRecursively(this, dark));
            });
        }

        private void InitializeComponent()
        {
            this.Text = "Face Tool";
            this.TopMost = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(240, 0);

            // --- Create controls ---
            btnAlign = new Button();
            btnSnap = new Button();
            btnAlignSnap = new Button();
            btnTrim = new Button();
            btnRectify = new Button();
            btnRestore = new Button();
            btnClearAnchor = new Button();
            btnClone = new Button();

            chkLockX = new CheckBox();
            chkLockY = new CheckBox();
            chkLockZ = new CheckBox();

            chkRotX = new CheckBox();
            chkRotY = new CheckBox();
            chkRotZ = new CheckBox();

            chkSnapGrid = new CheckBox();
            chkInvertTrimSide = new CheckBox();
            chkShowHoverHelper = new CheckBox();
            chkInvertNext = new CheckBox();

            cmbScope = new ComboBox();

            numOffset = new NumericUpDown();

            // --- Layout ---
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // Scope Group
            var grpScope = new GroupBox { Text = "Scope", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 8) };
            var scopeLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            cmbScope.Items.AddRange(new object[] { "Auto", "Brush", "Group", "Entity" });
            cmbScope.SelectedIndex = 0;
            cmbScope.Width = 200;
            cmbScope.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScope.SelectedIndexChanged += (s, e) => _tool.CurrentScope = (Tools.FaceTool.SelectionScope)cmbScope.SelectedIndex;
            scopeLayout.Controls.Add(cmbScope);
            grpScope.Controls.Add(scopeLayout);

            // Operations Group
            var grpOps = new GroupBox { Text = "Operations", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 8) };
            var opsLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };

            btnAlign.Text = "Align"; btnAlign.Width = 200;
            btnSnap.Text = "Snap"; btnSnap.Width = 200;
            btnAlignSnap.Text = "Align + Snap"; btnAlignSnap.Width = 200;
            btnClone.Text = "Clone To Face"; btnClone.Width = 200;
            btnTrim.Text = "Trim"; btnTrim.Width = 200;
            btnPlaceTrim = new Button();
            btnPlaceTrim.Text = "Place & Trim"; btnPlaceTrim.Width = 200;

            opsLayout.Controls.Add(btnAlign);
            opsLayout.Controls.Add(btnSnap);
            opsLayout.Controls.Add(btnAlignSnap);
            opsLayout.Controls.Add(btnClone);
            opsLayout.Controls.Add(btnTrim);
            opsLayout.Controls.Add(btnPlaceTrim);
            grpOps.Controls.Add(opsLayout);

            // Anchor / Rectification Group
            var grpAnchor = new GroupBox { Text = "Anchor / Rectification", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 8) };
            var ancLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };

            btnRectify.Text = "Rectify"; btnRectify.Width = 200;
            btnRestore.Text = "Restore from Anchor"; btnRestore.Width = 200;
            btnClearAnchor.Text = "Clear Anchor"; btnClearAnchor.Width = 200;

            ancLayout.Controls.Add(btnRectify);
            ancLayout.Controls.Add(btnRestore);
            ancLayout.Controls.Add(btnClearAnchor);
            grpAnchor.Controls.Add(ancLayout);

            // Options Group
            var grpOptions = new GroupBox { Text = "Options", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0) };
            var optLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };

            var lblOffset = new Label { Text = "Offset:", AutoSize = true };
            numOffset.Minimum = -10000;
            numOffset.Maximum = 10000;
            numOffset.Value = 0;
            numOffset.Width = 200;

            chkSnapGrid.Text = "Snap To Grid";
            chkSnapGrid.Checked = false;

            chkInvertTrimSide.Text = "Invert Trim Side";
            chkInvertTrimSide.Checked = false;

            chkShowHoverHelper.Text = "Show Hover Helper";
            chkShowHoverHelper.Checked = true;
            chkShowHoverHelper.CheckedChanged += (s, e) => _tool.ShowHoverHelper = chkShowHoverHelper.Checked;

            chkInvertNext.Text = "Invert Next Operation";
            chkInvertNext.Checked = false;

            var lblPosLocks = new Label { Text = "Position Locks:", AutoSize = true };
            chkLockX.Text = "X"; chkLockX.AutoSize = true;
            chkLockY.Text = "Y"; chkLockY.AutoSize = true;
            chkLockZ.Text = "Z"; chkLockZ.AutoSize = true;

            var posLocksPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Width = 200, Margin = new Padding(0) };
            posLocksPanel.Controls.Add(chkLockX);
            posLocksPanel.Controls.Add(chkLockY);
            posLocksPanel.Controls.Add(chkLockZ);

            var lblRotLocks = new Label { Text = "Rotation Locks:", AutoSize = true };
            chkRotX.Text = "X"; chkRotX.AutoSize = true; chkRotX.Checked = true;
            chkRotY.Text = "Y"; chkRotY.AutoSize = true; chkRotY.Checked = true;
            chkRotZ.Text = "Z"; chkRotZ.AutoSize = true; chkRotZ.Checked = false;

            var rotLocksPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Width = 200, Margin = new Padding(0) };
            rotLocksPanel.Controls.Add(chkRotX);
            rotLocksPanel.Controls.Add(chkRotY);
            rotLocksPanel.Controls.Add(chkRotZ);

            optLayout.Controls.Add(lblOffset);
            optLayout.Controls.Add(numOffset);
            optLayout.Controls.Add(chkSnapGrid);
            optLayout.Controls.Add(chkInvertTrimSide);
            optLayout.Controls.Add(chkShowHoverHelper);
            optLayout.Controls.Add(chkInvertNext);
            optLayout.Controls.Add(lblPosLocks);
            optLayout.Controls.Add(posLocksPanel);
            optLayout.Controls.Add(lblRotLocks);
            optLayout.Controls.Add(rotLocksPanel);
            grpOptions.Controls.Add(optLayout);

            layout.Controls.Add(grpScope, 0, 0);
            layout.Controls.Add(grpOps, 0, 1);
            layout.Controls.Add(grpAnchor, 0, 2);
            layout.Controls.Add(grpOptions, 0, 3);

            this.Controls.Add(layout);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // --- Bind Events ---
            btnAlign.Click += async (s, e) => await PerformAlign();
            btnSnap.Click += async (s, e) => await PerformSnap();
            btnAlignSnap.Click += async (s, e) => await PerformAlignSnap();
            btnClone.Click += async (s, e) => await PerformClone();
            btnTrim.Click += async (s, e) => await PerformTrim();
            btnRectify.Click += async (s, e) => await PerformRectify();
            btnRestore.Click += async (s, e) => await PerformRestore();
            btnClearAnchor.Click += async (s, e) => await PerformClearAnchor();
            btnPlaceTrim.Click += async (s, e) => await PerformPlaceTrim();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Gets the targets for the operation.
        /// If the document's active selection contains the clicked source object (or any of its descendants),
        /// we resolve each selected object to the CurrentScope, de-duplicate them, and exclude the target object.
        /// If not (e.g. selection is empty or doesn't contain the clicked object), we fall back to the resolved SourceObject.
        /// We also exclude the target object (the destination wall) and its hierarchy, and de-duplicate parent-child relations
        /// to prevent double transformation of nested objects.
        /// </summary>
        private IEnumerable<IMapObject> GetTargets(MapDocument doc)
        {
            if (_tool.SourceObject == null || _tool.SourceSolid == null) return Enumerable.Empty<IMapObject>();

            // Check if the clicked solid (or any of its ancestors) is in doc.Selection
            bool clickedObjectIsSelected = false;
            if (doc.Selection != null && !doc.Selection.IsEmpty)
            {
                IMapObject? curr = _tool.SourceSolid;
                while (curr != null)
                {
                    if (doc.Selection.Contains(curr))
                    {
                        clickedObjectIsSelected = true;
                        break;
                    }
                    curr = curr.Hierarchy.Parent;
                }
            }

            List<IMapObject> candidates;
            if (clickedObjectIsSelected && doc.Selection != null)
            {
                candidates = doc.Selection
                    .Select(x => _tool.ResolveScope(x, _tool.CurrentScope))
                    .Where(x => x != null)
                    .Distinct()
                    .ToList();
            }
            else
            {
                candidates = new List<IMapObject> { _tool.SourceObject };
            }

            // Exclude the target object and all its ancestors/descendants to prevent it from moving/rotating
            if (_tool.TargetObject != null)
            {
                var targetAncestorsAndDescendants = new HashSet<IMapObject>();
                var curr = _tool.TargetObject;
                while (curr != null)
                {
                    targetAncestorsAndDescendants.Add(curr);
                    curr = curr.Hierarchy.Parent;
                }

                void AddDescendants(IMapObject obj)
                {
                    foreach (var child in obj.Hierarchy)
                    {
                        targetAncestorsAndDescendants.Add(child);
                        AddDescendants(child);
                    }
                }
                AddDescendants(_tool.TargetObject);

                candidates = candidates.Where(c => !targetAncestorsAndDescendants.Contains(c)).ToList();
            }

            // De-duplicate hierarchy: if a parent/ancestor is in candidates, remove the descendant to avoid double transformation
            var candidateSet = new HashSet<IMapObject>(candidates);
            var filteredCandidates = new List<IMapObject>();
            foreach (var cand in candidates)
            {
                bool hasAncestorInSelection = false;
                var parent = cand.Hierarchy.Parent;
                while (parent != null)
                {
                    if (candidateSet.Contains(parent))
                    {
                        hasAncestorInSelection = true;
                        break;
                    }
                    parent = parent.Hierarchy.Parent;
                }
                if (!hasAncestorInSelection)
                {
                    filteredCandidates.Add(cand);
                }
            }

            return filteredCandidates;
        }

        // --- Operation handlers ---

        private async Task PerformAlign()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.TargetFace == null || _tool.SourceObject == null || doc == null) return;

            var targets = GetTargets(doc);
            var op = Operations.AlignOperation.Create(
                targets,
                _tool.SourceFace.Plane.Normal,
                _tool.TargetFace.Plane.Normal,
                _tool.SourceFace.Origin,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            // Selection kept on purpose: Face.Transform mutates vertices in place,
            // so SourceFace still points at the (now rotated) face — chain Snap/Trim without re-picking.
            UpdateAnchorUI();
        }

        private async Task PerformSnap()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.TargetFace == null || _tool.SourceObject == null || doc == null) return;

            var targets = GetTargets(doc);
            var op = Operations.SnapOperation.Create(
                targets,
                _tool.SourceFace.Vertices,
                _tool.TargetFace.Plane,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);

            if (chkSnapGrid.Checked)
            {
                var gridData = doc.Map.Data.GetOne<GridData>();
                if (gridData?.Grid != null)
                {
                    var gridOp = Operations.GridSnapOperation.Create(targets, gridData.Grid.Spacing);
                    await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, gridOp);
                }
            }
            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformAlignSnap()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.TargetFace == null || _tool.SourceObject == null || doc == null) return;

            // Compute align matrix
            var alignMatrix = Operations.AlignOperation.CreateMatrix(
                _tool.SourceFace.Plane.Normal,
                _tool.TargetFace.Plane.Normal,
                _tool.SourceFace.Origin,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            // Compute snap matrix using rotated vertices
            var rotatedVertices = _tool.SourceFace.Vertices
                .Select(v => Vector3.Transform(v, alignMatrix))
                .ToList();

            var snapMatrix = Operations.SnapOperation.CreateMatrix(
                rotatedVertices,
                _tool.TargetFace.Plane,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            // Combine into single transform and execute atomically
            var combined = alignMatrix * snapMatrix;
            var transaction = new Transaction();
            var targets = GetTargets(doc);
            transaction.Add(new Operations.TransformWithTextures(combined, targets));

            if (chkSnapGrid.Checked)
            {
                var gridData = doc.Map.Data.GetOne<GridData>();
                if (gridData?.Grid != null)
                {
                    transaction.Add(Operations.GridSnapOperation.Create(targets, gridData.Grid.Spacing));
                }
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, transaction);
            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformClone()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.TargetFace == null || _tool.SourceObject == null || doc == null) return;

            var targets = GetTargets(doc);
            var op = Operations.CloneAndArrayOperations.CloneToFace(
                targets,
                _tool,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked,
                (float)numOffset.Value);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformTrim()
        {
            var doc = _tool.GetDocument();
            if (doc == null) return;

            if (_tool.SourceObject is Sledge.BspEditor.Primitives.MapObjects.Solid solid && _tool.TargetFace != null)
            {
                bool invert = chkInvertTrimSide.Checked ^ chkInvertNext.Checked;
                var op = Operations.TrimOperation.Create(doc, solid, _tool.TargetFace.Plane, invert);
                if (op is Transaction t && t.IsEmpty)
                {
                    MessageBox.Show("The clipping plane does not intersect the solid.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
                if (chkInvertNext.Checked)
                {
                    chkInvertNext.Checked = false;
                }
            }
            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformPlaceTrim()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.TargetFace == null || _tool.SourceObject == null || doc == null) return;

            // 1. Compute align and snap matrices
            var alignMatrix = Operations.AlignOperation.CreateMatrix(
                _tool.SourceFace.Plane.Normal,
                _tool.TargetFace.Plane.Normal,
                _tool.SourceFace.Origin,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            var rotatedVertices = _tool.SourceFace.Vertices
                .Select(v => Vector3.Transform(v, alignMatrix))
                .ToList();

            var snapMatrix = Operations.SnapOperation.CreateMatrix(
                rotatedVertices,
                _tool.TargetFace.Plane,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            var combined = alignMatrix * snapMatrix;
            var targets = GetTargets(doc);

            bool invert = chkInvertTrimSide.Checked ^ chkInvertNext.Checked;
            var op = Operations.TrimOperation.CreateTransformAndTrim(doc, targets, combined, _tool.TargetFace.Plane, invert);
            if (op is Transaction t && t.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect the aligned/snapped geometry.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);

            if (chkInvertNext.Checked)
            {
                chkInvertNext.Checked = false;
            }

            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformRectify()
        {
            var doc = _tool.GetDocument();
            if (_tool.SourceFace == null || _tool.SourceObject == null || doc == null) return;

            var targets = GetTargets(doc);
            var op = Operations.RectifyOperation.Create(doc, targets, _tool.SourceFace.Plane.Normal, _tool.SourceFace.Origin);
            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            ClearSelection();
            UpdateAnchorUI();
        }

        private async Task PerformRestore()
        {
            var doc = _tool.GetDocument();
            if (doc == null) return;

            var op = Operations.RestoreOperation.Create(doc);
            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);

            await PerformClearAnchor();
            UpdateAnchorUI();
        }

        private async Task PerformClearAnchor()
        {
            var doc = _tool.GetDocument();
            if (doc == null) return;

            var anchor = doc.Map.Data.GetOne<Operations.TransformAnchor>();
            if (anchor != null)
            {
                var op = new Sledge.BspEditor.Modification.Operations.Data.RemoveMapData(anchor);
                await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            }
            UpdateAnchorUI();
        }

        private void UpdateAnchorUI()
        {
            var doc = _tool.GetDocument();
            if (doc == null)
            {
                btnRestore.Enabled = false;
                btnRestore.Text = "Restore from Anchor";
                btnRectify.Enabled = true;
                return;
            }

            var anchor = doc.Map.Data.GetOne<Operations.TransformAnchor>();
            if (anchor != null)
            {
                btnRestore.Enabled = true;
                btnRestore.Text = "Restore from Anchor (Set)";
                btnRectify.Enabled = false;
            }
            else
            {
                btnRestore.Enabled = false;
                btnRestore.Text = "Restore from Anchor";
                btnRectify.Enabled = true;
            }
        }

        private void ClearSelection()
        {
            _tool.SourceFace = null;
            _tool.SourceObject = null;
            _tool.SourceSolid = null;
            _tool.TargetFace = null;
            _tool.TargetObject = null;
            _tool.TargetSolid = null;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var parent = this.Owner;
            if (parent != null)
            {
                bool isDark = parent.BackColor.R < 100;
                Sledge.Shell.Registers.DialogRegister.ColorControlsRecursively(this, isDark);
            }
            LoadSettings();
            UpdateAnchorUI();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!this.Visible)
            {
                SaveSettings();
            }
        }

        private static string GetSettingsPath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hammertime");
            return Path.Combine(folder, "FaceToolSettings.json");
        }

        private void LoadSettings()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var settings = System.Text.Json.JsonSerializer.Deserialize<FaceToolSettings>(json);
                if (settings == null) return;

                if (settings.WindowX != int.MinValue && settings.WindowY != int.MinValue)
                {
                    var rect = new Rectangle(settings.WindowX, settings.WindowY, this.Width, this.Height);
                    if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
                    {
                        this.Location = new Point(settings.WindowX, settings.WindowY);
                    }
                }

                cmbScope.SelectedIndex = Math.Clamp(settings.ScopeIndex, 0, cmbScope.Items.Count - 1);
                numOffset.Value = Math.Max(numOffset.Minimum, Math.Min(numOffset.Maximum, settings.Offset));
                chkSnapGrid.Checked = settings.SnapToGrid;
                chkInvertTrimSide.Checked = settings.InvertTrimSide;
                chkShowHoverHelper.Checked = settings.ShowHoverHelper;
                chkInvertNext.Checked = settings.InvertNext;

                chkLockX.Checked = settings.LockX;
                chkLockY.Checked = settings.LockY;
                chkLockZ.Checked = settings.LockZ;

                chkRotX.Checked = settings.RotX;
                chkRotY.Checked = settings.RotY;
                chkRotZ.Checked = settings.RotZ;
            }
            catch
            {
                // Ignore settings load errors to keep app running stable
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new FaceToolSettings
                {
                    WindowX = this.Location.X,
                    WindowY = this.Location.Y,
                    ScopeIndex = cmbScope.SelectedIndex,
                    Offset = numOffset.Value,
                    SnapToGrid = chkSnapGrid.Checked,
                    InvertTrimSide = chkInvertTrimSide.Checked,
                    ShowHoverHelper = chkShowHoverHelper.Checked,
                    InvertNext = chkInvertNext.Checked,

                    LockX = chkLockX.Checked,
                    LockY = chkLockY.Checked,
                    LockZ = chkLockZ.Checked,

                    RotX = chkRotX.Checked,
                    RotY = chkRotY.Checked,
                    RotZ = chkRotZ.Checked
                };

                var path = GetSettingsPath();
                var folder = Path.GetDirectoryName(path);
                if (folder != null && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignore settings save errors to keep app running stable
            }
        }
    }

    public class FaceToolSettings
    {
        public int WindowX { get; set; } = int.MinValue;
        public int WindowY { get; set; } = int.MinValue;
        public int ScopeIndex { get; set; } = 0;
        public decimal Offset { get; set; } = 0;
        public bool SnapToGrid { get; set; } = false;
        public bool InvertTrimSide { get; set; } = false;
        public bool ShowHoverHelper { get; set; } = true;
        public bool InvertNext { get; set; } = false;

        public bool LockX { get; set; } = false;
        public bool LockY { get; set; } = false;
        public bool LockZ { get; set; } = false;

        public bool RotX { get; set; } = true;
        public bool RotY { get; set; } = true;
        public bool RotZ { get; set; } = false;
    }
}
