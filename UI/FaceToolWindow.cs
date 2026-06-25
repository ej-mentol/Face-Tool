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

        private CheckBox btnAlign = null!;
        private CheckBox btnSnap = null!;
        private CheckBox btnAlignSnap = null!;
        private CheckBox btnTrim = null!;
        private CheckBox btnRectify = null!;
        private Button btnRestore = null!;
        private Button btnClearAnchor = null!;
        private CheckBox btnClone = null!;
        private CheckBox btnPlaceTrim = null!;

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
        private CheckBox chkOperationLock = null!;

        private ComboBox cmbScope = null!;
        private NumericUpDown numOffset = null!;

        private Button[] targetRoseButtons = null!;
        private Button[] sourceRoseButtons = null!;
        private int targetRoseGridIndex = 4;
        private int sourceRoseGridIndex = 4;
        private int targetAnchorIndex = Operations.FacePlacement.AnchorOff;
        private int sourceAnchorIndex = Operations.FacePlacement.AnchorOff;

        private NumericUpDown numArrayX = null!, numArrayY = null!;
        private NumericUpDown numSpaceX = null!, numSpaceY = null!;
        private CheckBox chkKeepHierarchy = null!;

        private bool _isDarkTheme;

        public FaceToolWindow(Tools.FaceTool tool)
        {
            _tool = tool;
            InitializeComponent();
            
            Oy.Subscribe<bool>("Theme:Changed", isDark => {
                _isDarkTheme = isDark; // Update the internal state
                this.InvokeLater(() => Sledge.Shell.Registers.DialogRegister.ColorControlsRecursively(this, isDark));
                HighlightRose(targetRoseButtons, targetRoseGridIndex); // Reapply rose styling
                HighlightRose(sourceRoseButtons, sourceRoseGridIndex); // Reapply rose styling
            });
        }

        private void InitializeComponent()
        {
            this.Text = "Face Tool";
            this.TopMost = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(520, 420);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- OPERATIONS ---
            var grpOps = new GroupBox { Text = "Operations", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var opsTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, AutoSize = true, Width = 240 };
            opsTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            opsTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int r = 0; r < 4; r++) opsTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            btnAlign = new CheckBox { Text = "Align", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnSnap = new CheckBox { Text = "Snap", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnAlignSnap = new CheckBox { Text = "Align + Snap", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnClone = new CheckBox { Text = "Clone To Face", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnTrim = new CheckBox { Text = "Trim", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnPlaceTrim = new CheckBox { Text = "Place & Trim", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            chkOperationLock = new CheckBox { Text = "Lock Mode", AutoSize = true };
            var toolTip = new ToolTip();
            toolTip.SetToolTip(chkOperationLock, "Keep the current mode active after an operation is performed");


            opsTbl.Controls.Add(btnAlign, 0, 0); opsTbl.Controls.Add(btnSnap, 1, 0);
            opsTbl.Controls.Add(btnAlignSnap, 0, 1); opsTbl.Controls.Add(btnClone, 1, 1);
            opsTbl.Controls.Add(btnTrim, 0, 2); opsTbl.Controls.Add(btnPlaceTrim, 1, 2);
            opsTbl.Controls.Add(chkOperationLock, 0, 3);
            opsTbl.SetColumnSpan(chkOperationLock, 2);
            grpOps.Controls.Add(opsTbl);

            // --- ANCHOR MANAGEMENT ---
            var grpAncMan = new GroupBox { Text = "Anchor / Rectification", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var ancTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true, Width = 240 };
            for (int r = 0; r < 3; r++) ancTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            btnRectify = new CheckBox { Text = "Rectify", Dock = DockStyle.Fill, Margin = new Padding(1), Appearance = Appearance.Button };
            btnRestore = new Button { Text = "Restore from Anchor", Dock = DockStyle.Fill, Margin = new Padding(1), Enabled = false };
            btnClearAnchor = new Button { Text = "Clear Anchor", Dock = DockStyle.Fill, Margin = new Padding(1) };
            ancTbl.Controls.Add(btnRectify, 0, 0);
            ancTbl.Controls.Add(btnRestore, 0, 1);
            ancTbl.Controls.Add(btnClearAnchor, 0, 2);
            grpAncMan.Controls.Add(ancTbl);

            // --- PLACEMENT (two roses) ---
            var grpPlacement = new GroupBox { Text = "Placement", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var placementTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Width = 240 };
            placementTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            placementTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            targetRoseButtons = CreateRoseGrid(isTarget: true);
            sourceRoseButtons = CreateRoseGrid(isTarget: false);

            var targetPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
            targetPanel.Controls.Add(new Label { Text = "Target (2nd click)", AutoSize = true, ForeColor = Color.DimGray });
            targetPanel.Controls.Add(WrapRoseGrid(targetRoseButtons));

            var sourcePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
            sourcePanel.Controls.Add(new Label { Text = "Source (1st click)", AutoSize = true, ForeColor = Color.DimGray });
            sourcePanel.Controls.Add(WrapRoseGrid(sourceRoseButtons));

            placementTbl.Controls.Add(targetPanel, 0, 0);
            placementTbl.Controls.Add(sourcePanel, 1, 0);
            grpPlacement.Controls.Add(placementTbl);

            // --- CLONE ARRAY ---
            var grpClone = new GroupBox { Text = "Clone Array", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var cloneTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, AutoSize = true, Width = 240 };
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            numArrayX = CreateNum(1, 1, 100);
            numArrayY = CreateNum(1, 1, 100);
            numSpaceX = CreateNum(0, -1000, 1000, dec: true);
            numSpaceY = CreateNum(0, -1000, 1000, dec: true);

            cloneTbl.Controls.Add(new Label { Text = "Count:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cloneTbl.Controls.Add(LabeledNum("X", numArrayX), 1, 0);
            cloneTbl.Controls.Add(LabeledNum("Y", numArrayY), 2, 0);

            cloneTbl.Controls.Add(new Label { Text = "Spacing:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cloneTbl.Controls.Add(LabeledNum("X", numSpaceX), 1, 1);
            cloneTbl.Controls.Add(LabeledNum("Y", numSpaceY), 2, 1);

            chkKeepHierarchy = new CheckBox { Text = "Keep Hierarchy", AutoSize = true, Checked = true };
            cloneTbl.Controls.Add(chkKeepHierarchy, 0, 2);
            cloneTbl.SetColumnSpan(chkKeepHierarchy, 3);
            grpClone.Controls.Add(cloneTbl);

            // --- OPTIONS ---
            var grpOptions = new GroupBox { Text = "Options", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var optTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
            optTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            var optLeft = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            optLeft.Controls.Add(new Label { Text = "Offset:", AutoSize = true });
            numOffset = new NumericUpDown { Minimum = -10000, Maximum = 10000, Width = 120, Value = 0 };
            optLeft.Controls.Add(numOffset);

            optLeft.Controls.Add(chkSnapGrid = new CheckBox { Text = "Snap To Grid", AutoSize = true });
            optLeft.Controls.Add(chkInvertTrimSide = new CheckBox { Text = "Invert Trim Side", AutoSize = true });
            optLeft.Controls.Add(chkShowHoverHelper = new CheckBox { Text = "Show Hover Helper", AutoSize = true, Checked = true });
            chkShowHoverHelper.CheckedChanged += (s, e) => _tool.ShowHoverHelper = chkShowHoverHelper.Checked;
            optLeft.Controls.Add(chkInvertNext = new CheckBox { Text = "Invert Next Operation", AutoSize = true });

            var optRight = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            optRight.Controls.Add(new Label { Text = "Scope:", AutoSize = true });
            cmbScope = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbScope.Items.AddRange(new object[] { "Auto", "Brush", "Group", "Entity" });
            cmbScope.SelectedIndex = 0;
            cmbScope.SelectedIndexChanged += (s, e) => _tool.CurrentScope = (Tools.FaceTool.SelectionScope)cmbScope.SelectedIndex;
            optRight.Controls.Add(cmbScope);

            optRight.Controls.Add(new Label { Text = "Position Locks:", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            var posLocks = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            posLocks.Controls.Add(chkLockX = new CheckBox { Text = "X", AutoSize = true });
            posLocks.Controls.Add(chkLockY = new CheckBox { Text = "Y", AutoSize = true });
            posLocks.Controls.Add(chkLockZ = new CheckBox { Text = "Z", AutoSize = true });
            optRight.Controls.Add(posLocks);

            optRight.Controls.Add(new Label { Text = "Rotation Locks:", AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
            var rotLocks = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            rotLocks.Controls.Add(chkRotX = new CheckBox { Text = "X", AutoSize = true, Checked = true });
            rotLocks.Controls.Add(chkRotY = new CheckBox { Text = "Y", AutoSize = true, Checked = true });
            rotLocks.Controls.Add(chkRotZ = new CheckBox { Text = "Z", AutoSize = true });
            optRight.Controls.Add(rotLocks);

            optTbl.Controls.Add(optLeft, 0, 0);
            optTbl.Controls.Add(optRight, 1, 0);
            grpOptions.Controls.Add(optTbl);

            mainLayout.Controls.Add(grpOps, 0, 0);
            mainLayout.Controls.Add(grpAncMan, 1, 0);
            mainLayout.Controls.Add(grpPlacement, 0, 1);
            mainLayout.Controls.Add(grpClone, 1, 1);
            mainLayout.Controls.Add(grpOptions, 0, 2);
            mainLayout.SetColumnSpan(grpOptions, 2);

            this.Controls.Add(mainLayout);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            btnAlign.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Align);
            btnAlignSnap.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.AlignSnap);
            btnClone.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.CloneToFace);
            btnTrim.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Trim);
            btnPlaceTrim.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.PlaceTrim);
            btnSnap.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Snap);
            btnRectify.Click += (s, e) => _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Rectify);
            
            // These are also unchanged for now
            btnRestore.Click += async (s, e) => await PerformRestore();
            btnClearAnchor.Click += async (s, e) => await PerformClearAnchor();

            SetTargetRose(4);
            SetSourceRose(4);
        }

        private static TableLayoutPanel WrapRoseGrid(Button[] buttons)
        {
            var grid = new TableLayoutPanel { ColumnCount = 3, RowCount = 3, Width = 84, Height = 84, Margin = new Padding(0, 2, 0, 0) };
            for (int c = 0; c < 3; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            }
            for (int i = 0; i < 9; i++)
                grid.Controls.Add(buttons[i], i % 3, i / 3);
            return grid;
        }

        private Button[] CreateRoseGrid(bool isTarget)
        {
            var buttons = new Button[9];
            string[] labels = { "↖", "↑", "↗", "←", "○", "→", "↙", "↓", "↘" };
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                buttons[i] = new Button
                {
                    Text = labels[i],
                    Dock = DockStyle.Fill,
                    Margin = new Padding(1),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI Symbol", 9f)
                };
                buttons[i].Click += (s, e) =>
                {
                    if (isTarget)
                    {
                        if (targetRoseGridIndex == index) SetTargetRose(Operations.FacePlacement.AnchorOff);
                        else SetTargetRose(index);
                    }
                    else
                    {
                        if (sourceRoseGridIndex == index) SetSourceRose(Operations.FacePlacement.AnchorOff);
                        else SetSourceRose(index);
                    }
                };
            }
            return buttons;
        }

        private void SetTargetRose(int gridIndex)
        {
            if (gridIndex == Operations.FacePlacement.AnchorOff)
            {
                targetRoseGridIndex = -1;
                targetAnchorIndex = Operations.FacePlacement.AnchorOff;
                HighlightRose(targetRoseButtons, -1);
            }
            else
            {
                targetRoseGridIndex = gridIndex;
                targetAnchorIndex = gridIndex;
                HighlightRose(targetRoseButtons, gridIndex);
            }
        }

        private void SetSourceRose(int gridIndex)
        {
            if (gridIndex == Operations.FacePlacement.AnchorOff)
            {
                sourceRoseGridIndex = -1;
                sourceAnchorIndex = Operations.FacePlacement.AnchorOff;
                HighlightRose(sourceRoseButtons, -1);
            }
            else
            {
                sourceRoseGridIndex = gridIndex;
                sourceAnchorIndex = gridIndex;
                HighlightRose(sourceRoseButtons, gridIndex);
            }
        }

        private void HighlightRose(Button[] buttons, int selectedGridIndex)
        {
            for (int i = 0; i < 9; i++)
            {
                var inactiveForeColor = _isDarkTheme ? Color.White : SystemColors.ControlText;
                buttons[i].BackColor = i == selectedGridIndex ? SystemColors.Highlight : Color.Transparent;
                buttons[i].ForeColor = i == selectedGridIndex ? SystemColors.HighlightText : inactiveForeColor;
            }
        }

        private static FlowLayoutPanel LabeledNum(string axis, NumericUpDown num)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0) };
            p.Controls.Add(new Label { Text = axis, AutoSize = true, Width = 14, TextAlign = ContentAlignment.MiddleLeft });
            num.Width = 56;
            p.Controls.Add(num);
            return p;
        }

        private NumericUpDown CreateNum(decimal val, decimal min, decimal max, bool dec = false)
        {
            var n = new NumericUpDown { Value = val, Minimum = min, Maximum = max };
            if (dec) n.DecimalPlaces = 1;
            return n;
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

        // NOTE: GetTargets was the old system for resolving which objects to transform,
        // using Hammer's active selection as a multi-object source. The new system
        // resolves the target object directly via ResolveScope() on click (scope: Auto/Brush/Group/Entity).
        // Kept here temporarily in case we need to revert or reference the old logic.
        /*
        private IEnumerable<IMapObject> GetTargets(MapDocument doc)
        {
            if (_tool.SourceObject == null || _tool.SourceSolid == null) return Enumerable.Empty<IMapObject>();

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
                    filteredCandidates.Add(cand);
            }

            return filteredCandidates;
        }
        */

        public async void PerformOperation(Tools.FaceTool.ToolMode mode, List<Tools.FaceTool.SelectedFace> faces)
        {
            switch (mode)
            {
                case Tools.FaceTool.ToolMode.Align:
                    await PerformAlign(faces);
                    break;
                case Tools.FaceTool.ToolMode.Snap:
                    await PerformSnap(faces);
                    break;
                case Tools.FaceTool.ToolMode.AlignSnap:
                    await PerformAlignSnap(faces);
                    break;
                case Tools.FaceTool.ToolMode.CloneToFace:
                    await PerformClone(faces);
                    break;
                case Tools.FaceTool.ToolMode.Trim:
                    await PerformTrim(faces);
                    break;
                case Tools.FaceTool.ToolMode.Rectify:
                    await PerformRectify(faces);
                    break;
                case Tools.FaceTool.ToolMode.PlaceTrim:
                    await PerformPlaceTrim(faces);
                    break;
            }
        }

        private async Task PerformAlign(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            var pivot = Operations.FacePlacement.GetAlignPivot(source.Face, sourceAnchorIndex);
            var op = Operations.AlignOperation.Create(
                new[] { source.TransformableObject },
                source.Face.Plane.Normal,
                target.Face.Plane.Normal,
                pivot,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            _tool.OperationComplete();
        }

        private async Task PerformSnap(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 1 || doc == null) return;

            var target = faces[0]; // Only a target face was clicked

            // Source = current Hammer selection
            var sourceObjects = doc.Selection?.ToList();
            if (sourceObjects == null || sourceObjects.Count == 0) return;

            var transaction = new Transaction();
            foreach (var sourceObj in sourceObjects)
            {
                var allVerts = sourceObj.FindAll()
                    .OfType<Sledge.BspEditor.Primitives.MapObjects.Solid>()
                    .SelectMany(s => s.Faces)
                    .SelectMany(f => f.Vertices)
                    .ToList();

                if (!allVerts.Any()) continue;

                var snapMatrix = Operations.SnapOperation.CreateMatrix(
                    allVerts,
                    target.Face.Plane,
                    (float)numOffset.Value,
                    chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

                transaction.Add(new Operations.TransformWithTextures(snapMatrix, sourceObj));
            }

            if (chkSnapGrid.Checked)
            {
                var gridData = doc.Map.Data.GetOne<GridData>();
                if (gridData?.Grid != null)
                    transaction.Add(Operations.GridSnapOperation.Create(sourceObjects, gridData.Grid.Spacing));
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, transaction);
            _tool.OperationComplete();
        }

        private async Task PerformAlignSnap(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            var pivot = Operations.FacePlacement.GetAlignPivot(source.Face, sourceAnchorIndex);
            var alignMatrix = Operations.AlignOperation.CreateMatrix(
                source.Face.Plane.Normal,
                target.Face.Plane.Normal,
                pivot,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            var snapMatrix = Operations.FacePlacement.ComputeSnapMatrix(
                source.Face,
                target.Face,
                alignMatrix,
                sourceAnchorIndex,
                targetAnchorIndex,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            var transaction = new Transaction();
            var targets = new[] { source.TransformableObject };
            transaction.Add(new Operations.TransformWithTextures(alignMatrix * snapMatrix, targets));

            if (chkSnapGrid.Checked)
            {
                var gridData = doc.Map.Data.GetOne<GridData>();
                if (gridData?.Grid != null)
                    transaction.Add(Operations.GridSnapOperation.Create(targets, gridData.Grid.Spacing));
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, transaction);
            _tool.OperationComplete();
        }

        private async Task PerformClone(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return; 
            
            var source = faces[0];
            
            var targets = new[] { source.TransformableObject };
            int countX = (int)numArrayX.Value;
            int countY = (int)numArrayY.Value;

            IOperation op = countX > 1 || countY > 1
                ? Operations.CloneAndArrayOperations.CreateArray(
                    targets, _tool,
                    sourceAnchorIndex, targetAnchorIndex,
                    countX, countY,
                    numSpaceX.Value, numSpaceY.Value,
                    chkKeepHierarchy.Checked,
                    chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked,
                    (float)numOffset.Value,
                    chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked)
                : Operations.CloneAndArrayOperations.CloneToFace(
                    targets, _tool,
                    sourceAnchorIndex, targetAnchorIndex,
                    chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked,
                    (float)numOffset.Value,
                    chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            _tool.OperationComplete();
        }

        private async Task PerformTrim(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            bool invert = chkInvertTrimSide.Checked ^ chkInvertNext.Checked;
            var op = Operations.TrimOperation.Create(doc, source.Solid, target.Face.Plane, invert);
            if (op is Transaction t && t.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect the solid.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _tool.OperationComplete();
                return;
            }
            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            if (chkInvertNext.Checked) chkInvertNext.Checked = false;
            
            _tool.OperationComplete();
        }
        
        private async Task PerformRectify(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 1 || doc == null) return;

            var source = faces[0];

            var op = Operations.RectifyOperation.Create(doc, new[] { source.TransformableObject }, source.Face.Plane.Normal, source.Face.Origin);
            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            _tool.OperationComplete();
        }

        private async Task PerformPlaceTrim(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            var pivot = Operations.FacePlacement.GetAlignPivot(source.Face, sourceAnchorIndex);
            var alignMatrix = Operations.AlignOperation.CreateMatrix(
                source.Face.Plane.Normal,
                target.Face.Plane.Normal,
                pivot,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            var snapMatrix = Operations.FacePlacement.ComputeSnapMatrix(
                source.Face,
                target.Face,
                alignMatrix,
                sourceAnchorIndex,
                targetAnchorIndex,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked);

            var targets = new[] { source.TransformableObject };
            bool invert = chkInvertTrimSide.Checked ^ chkInvertNext.Checked;
            var op = Operations.TrimOperation.CreateTransformAndTrim(doc, targets, alignMatrix * snapMatrix, target.Face.Plane, invert);
            if (op is Transaction t && t.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect the aligned/snapped geometry.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _tool.OperationComplete();
                return;
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            if (chkInvertNext.Checked) chkInvertNext.Checked = false;

            _tool.OperationComplete();
        }

        private async Task PerformRestore()
        {
            var doc = _tool.GetDocument();
            if (doc == null) return;

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, Operations.RestoreOperation.Create(doc));
            await PerformClearAnchor();
            UpdateAnchorUI();
        }

        private async Task PerformClearAnchor()
        {
            var doc = _tool.GetDocument();
            if (doc == null) return;

            var anchor = doc.Map.Data.GetOne<Operations.TransformAnchor>();
            if (anchor != null)
                await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc,
                    new Sledge.BspEditor.Modification.Operations.Data.RemoveMapData(anchor));
            UpdateAnchorUI();
        }

        public bool IsOperationLockChecked => chkOperationLock.Checked;

        public void UpdateInterfaceForMode(Tools.FaceTool.ToolMode mode)
        {
            UpdateOperationButtons(mode);
            UpdateAnchorUI();
        }

        public void UpdateOperationButtons(Tools.FaceTool.ToolMode mode)
        {
            var buttons = new Dictionary<Tools.FaceTool.ToolMode, CheckBox>
            {
                [Tools.FaceTool.ToolMode.Align] = btnAlign,
                [Tools.FaceTool.ToolMode.Snap] = btnSnap,
                [Tools.FaceTool.ToolMode.AlignSnap] = btnAlignSnap,
                [Tools.FaceTool.ToolMode.CloneToFace] = btnClone,
                [Tools.FaceTool.ToolMode.Trim] = btnTrim,
                [Tools.FaceTool.ToolMode.Rectify] = btnRectify,
                [Tools.FaceTool.ToolMode.PlaceTrim] = btnPlaceTrim,
            };

            foreach (var p in buttons)
            {
                if (p.Value != null) p.Value.Checked = p.Key == mode;
            }
        }

        public void ResetAnchors()
        {
            SetTargetRose(Operations.FacePlacement.AnchorOff);
            SetSourceRose(Operations.FacePlacement.AnchorOff);
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
            _tool.ClearAllSelections();
            ResetAnchors();
        }

        private void ClearTargetSelection()
        {
            _tool.ClearTargetInSelection();
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
            if (!this.Visible) SaveSettings();
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

                var settings = System.Text.Json.JsonSerializer.Deserialize<FaceToolSettings>(File.ReadAllText(path));
                if (settings == null) return;

                if (settings.WindowX != int.MinValue && settings.WindowY != int.MinValue)
                {
                    var rect = new Rectangle(settings.WindowX, settings.WindowY, this.Width, this.Height);
                    if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
                        this.Location = new Point(settings.WindowX, settings.WindowY);
                }

                cmbScope.SelectedIndex = Math.Clamp(settings.ScopeIndex, 0, cmbScope.Items.Count - 1);
                numOffset.Value = Math.Clamp(settings.Offset, numOffset.Minimum, numOffset.Maximum);
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

                SetTargetRose(GridIndexFromAnchor(settings.TargetAnchor));
                SetSourceRose(GridIndexFromAnchor(settings.SourceAnchor));
                numArrayX.Value = Math.Max(1, settings.ArrayCountX);
                numArrayY.Value = Math.Max(1, settings.ArrayCountY);
                numSpaceX.Value = settings.SpacingX;
                numSpaceY.Value = settings.SpacingY;
                chkKeepHierarchy.Checked = settings.KeepHierarchy;
            }
            catch { }
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
                    RotZ = chkRotZ.Checked,
                    SourceAnchor = sourceAnchorIndex,
                    TargetAnchor = targetAnchorIndex,
                    ArrayCountX = (int)numArrayX.Value,
                    ArrayCountY = (int)numArrayY.Value,
                    SpacingX = numSpaceX.Value,
                    SpacingY = numSpaceY.Value,
                    KeepHierarchy = chkKeepHierarchy.Checked
                };

                var path = GetSettingsPath();
                var folder = Path.GetDirectoryName(path);
                if (folder != null && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(settings,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static int GridIndexFromAnchor(int anchorIndex)
            => anchorIndex == Operations.FacePlacement.AnchorOff ? -1 : Math.Clamp(anchorIndex, 0, 8);
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
        public int SourceAnchor { get; set; } = Operations.FacePlacement.AnchorOff;
        public int TargetAnchor { get; set; } = Operations.FacePlacement.AnchorOff;
        public int ArrayCountX { get; set; } = 1;
        public int ArrayCountY { get; set; } = 1;
        public decimal SpacingX { get; set; } = 0;
        public decimal SpacingY { get; set; } = 0;
        public bool KeepHierarchy { get; set; } = true;
    }
}
