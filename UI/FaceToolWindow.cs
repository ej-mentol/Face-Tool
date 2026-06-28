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
        private readonly Lazy<Sledge.Common.Translations.ITranslationStringProvider> _translator;

        private Button btnAlign = null!;
        private Button btnSnap = null!;
        private Button btnAlignSnap = null!;
        private Button btnTrim = null!;
        private Button btnRectify = null!;
        private Button btnRestore = null!;
        private Button btnClearAnchor = null!;
        private Button btnClone = null!;
        private Button btnPlaceTrim = null!;
        private Button btnMiterJoin = null!;

        private CheckBox chkLockX = null!;
        private CheckBox chkLockY = null!;
        private CheckBox chkLockZ = null!;

        private CheckBox chkRotX = null!;
        private CheckBox chkRotY = null!;
        private CheckBox chkRotZ = null!;

        private CheckBox chkSnapGrid = null!;
        private CheckBox chkOperationLock = null!;
        private CheckBox chkCtrlMode = null!;

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

        private ComboBox cmbProfiles = null!;
        private Button btnAddProfile = null!;
        private Button btnDeleteProfile = null!;
        private Button btnRenameProfile = null!;
        private readonly List<FaceToolProfile> _profiles = new List<FaceToolProfile>();

        private static readonly string ProfilesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hammertime",
            "FaceToolProfiles.json"
        );

        private bool _isDarkTheme;

        public FaceToolWindow(Tools.FaceTool tool, Lazy<Sledge.Common.Translations.ITranslationStringProvider> translator)
        {
            _tool = tool;
            _translator = translator;
            InitializeComponent();
            LoadProfilesList();
            
            Oy.Subscribe<bool>("Theme:Changed", isDark => {
                _isDarkTheme = isDark;
                this.InvokeLater(() => {
                    Sledge.Shell.Registers.DialogRegister.ColorControlsRecursively(this, isDark);
                    HighlightRose(targetRoseButtons, targetRoseGridIndex);
                    HighlightRose(sourceRoseButtons, sourceRoseGridIndex);
                    UpdateOperationButtons(_tool.CurrentMode);
                });
            });
        }

        private void InitializeComponent()
        {
            this.Text = GetString("Title", "Face Tool");
            this.TopMost = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(520, 420);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // --- OPERATIONS ---
            var grpOps = new GroupBox { Text = GetString("GroupOperations", "Operations"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var opsTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, AutoSize = true, Width = 240 };
            opsTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            opsTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int r = 0; r < 4; r++) opsTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            btnAlign = new Button { Text = GetString("Align", "Align"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnSnap = new Button { Text = GetString("Snap", "Snap"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnAlignSnap = new Button { Text = GetString("AlignSnap", "Align + Snap"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnClone = new Button { Text = GetString("CloneToFace", "Clone To Face"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnTrim = new Button { Text = GetString("Trim", "Trim"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnPlaceTrim = new Button { Text = GetString("PlaceTrim", "Place && Trim"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            chkOperationLock = new CheckBox { Text = GetString("LockMode", "Lock Mode"), AutoSize = true };
            chkCtrlMode = new CheckBox { Text = GetString("CtrlMode", "Ctrl Mode"), AutoSize = true };



            opsTbl.Controls.Add(btnAlign, 0, 0); opsTbl.Controls.Add(btnSnap, 1, 0);
            opsTbl.Controls.Add(btnAlignSnap, 0, 1); opsTbl.Controls.Add(btnClone, 1, 1);
            opsTbl.Controls.Add(btnTrim, 0, 2); opsTbl.Controls.Add(btnPlaceTrim, 1, 2);
            opsTbl.Controls.Add(chkOperationLock, 0, 3);
            opsTbl.Controls.Add(chkCtrlMode, 1, 3);
            grpOps.Controls.Add(opsTbl);

            // --- ANCHOR MANAGEMENT ---
            var grpAncMan = new GroupBox { Text = GetString("GroupAnchor", "Anchor / Rectification"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var ancTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, AutoSize = true, Width = 240 };
            for (int r = 0; r < 4; r++) ancTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            btnRectify = new Button { Text = GetString("Rectify", "Rectify"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnRestore = new Button { Text = GetString("Restore", "Restore from Anchor"), Dock = DockStyle.Fill, Margin = new Padding(1), Enabled = false };
            btnClearAnchor = new Button { Text = GetString("ClearAnchor", "Clear Anchor"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            btnMiterJoin = new Button { Text = GetString("MiterJoin", "Miter Join"), Dock = DockStyle.Fill, Margin = new Padding(1) };
            ancTbl.Controls.Add(btnRectify, 0, 0);
            ancTbl.Controls.Add(btnRestore, 0, 1);
            ancTbl.Controls.Add(btnClearAnchor, 0, 2);
            ancTbl.Controls.Add(btnMiterJoin, 0, 3);
            grpAncMan.Controls.Add(ancTbl);

            // --- PLACEMENT (two roses) ---
            var grpPlacement = new GroupBox { Text = GetString("GroupPlacement", "Placement"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var placementTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Width = 240 };
            placementTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            placementTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            targetRoseButtons = CreateRoseGrid(isTarget: true);
            sourceRoseButtons = CreateRoseGrid(isTarget: false);

            var targetPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
            targetPanel.Controls.Add(new Label { Text = "Target (2nd click)", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 3, 3, 0) });
            targetPanel.Controls.Add(WrapRoseGrid(targetRoseButtons));

            var sourcePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
            sourcePanel.Controls.Add(new Label { Text = "Source (1st click)", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 3, 3, 0) });
            sourcePanel.Controls.Add(WrapRoseGrid(sourceRoseButtons));

            placementTbl.Controls.Add(targetPanel, 0, 0);
            placementTbl.Controls.Add(sourcePanel, 1, 0);
            grpPlacement.Controls.Add(placementTbl);

            // --- CLONE ARRAY ---
            var grpClone = new GroupBox { Text = GetString("GroupClone", "Clone Array Options"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
            var cloneTbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, AutoSize = true, Width = 240 };
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            cloneTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            numArrayX = CreateNum(1, 1, 100);
            numArrayY = CreateNum(1, 1, 100);
            numSpaceX = CreateNum(0, -1000, 1000, dec: true);
            numSpaceY = CreateNum(0, -1000, 1000, dec: true);

            cloneTbl.Controls.Add(new Label { Text = GetString("Count", "Count:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cloneTbl.Controls.Add(LabeledNum("X", numArrayX), 1, 0);
            cloneTbl.Controls.Add(LabeledNum("Y", numArrayY), 2, 0);

            cloneTbl.Controls.Add(new Label { Text = GetString("Spacing", "Spacing:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cloneTbl.Controls.Add(LabeledNum("X", numSpaceX), 1, 1);
            cloneTbl.Controls.Add(LabeledNum("Y", numSpaceY), 2, 1);

            chkKeepHierarchy = new CheckBox { Text = GetString("KeepHierarchy", "Keep Hierarchy"), AutoSize = true, Checked = true };
            cloneTbl.Controls.Add(chkKeepHierarchy, 0, 2);
            cloneTbl.SetColumnSpan(chkKeepHierarchy, 3);
            grpClone.Controls.Add(cloneTbl);

            // --- OPTIONS ---
            var grpOptions = new GroupBox { Text = GetString("GroupOptions", "Options"), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
            var optTbl = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, AutoSize = true };
            optTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            optTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var optLeft = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            optLeft.Controls.Add(new Label { Text = GetString("Scope", "Scope:"), AutoSize = true });
            cmbScope = new ComboBox { Width = 120, Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbScope.Items.AddRange(new object[] { "Auto", "Brush", "Group", "Entity" });
            cmbScope.SelectedIndex = 0;
            cmbScope.SelectedIndexChanged += (s, e) => _tool.CurrentScope = (Tools.FaceTool.SelectionScope)cmbScope.SelectedIndex;
            optLeft.Controls.Add(cmbScope);

            optLeft.Controls.Add(new Label { Text = GetString("Offset", "Offset:"), AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
            numOffset = new NumericUpDown { Minimum = -10000, Maximum = 10000, Width = 120, Value = 0 };
            optLeft.Controls.Add(numOffset);

            

            var optRight = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            optRight.Controls.Add(new Label { Text = GetString("PositionLocks", "Position Locks:"), AutoSize = true });
            var posLocks = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            posLocks.Controls.Add(chkLockX = new CheckBox { Text = "X", AutoSize = true });
            posLocks.Controls.Add(chkLockY = new CheckBox { Text = "Y", AutoSize = true });
            posLocks.Controls.Add(chkLockZ = new CheckBox { Text = "Z", AutoSize = true });
            optRight.Controls.Add(posLocks);

            optRight.Controls.Add(new Label { Text = GetString("RotationLocks", "Rotation Locks:"), AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
            var rotLocks = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            rotLocks.Controls.Add(chkRotX = new CheckBox { Text = "X", AutoSize = true, Checked = true });
            rotLocks.Controls.Add(chkRotY = new CheckBox { Text = "Y", AutoSize = true, Checked = true });
            rotLocks.Controls.Add(chkRotZ = new CheckBox { Text = "Z", AutoSize = true });
            optRight.Controls.Add(rotLocks);

            optRight.Controls.Add(chkSnapGrid = new CheckBox { Text = GetString("SnapToGrid", "Snap To Grid"), AutoSize = true, Margin = new Padding(6, 6, 0, 0) });

            optTbl.Controls.Add(optLeft, 0, 0);
            optTbl.Controls.Add(optRight, 1, 0);

            grpOptions.Controls.Add(optTbl);

            mainLayout.Controls.Add(grpOps, 0, 0);
            mainLayout.Controls.Add(grpAncMan, 1, 0);
            mainLayout.Controls.Add(grpPlacement, 0, 1);
            mainLayout.Controls.Add(grpClone, 1, 1);
            mainLayout.Controls.Add(grpOptions, 0, 2);
            mainLayout.SetColumnSpan(grpOptions, 2);

            // --- PRESETS ---
            var grpPresets = new GroupBox { Text = GetString("GroupPresets", "Presets"), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
            var presetsTbl = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            cmbProfiles = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            btnAddProfile = new Button { Text = "+", Width = 28, Height = 24 };
            btnRenameProfile = new Button { Text = "R", Width = 28, Height = 24 };
            btnDeleteProfile = new Button { Text = "-", Width = 28, Height = 24 };

            presetsTbl.Controls.Add(new Label { Text = GetString("Preset", "Preset:"), AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
            presetsTbl.Controls.Add(cmbProfiles);
            presetsTbl.Controls.Add(btnAddProfile);
            presetsTbl.Controls.Add(btnRenameProfile);
            presetsTbl.Controls.Add(btnDeleteProfile);
            presetsTbl.Controls.Add(new Label
            {
                Text = "v0.1.1-alpha",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font(this.Font.FontFamily, 7f, FontStyle.Regular),
                Margin = new Padding(8, 6, 0, 0),
                Anchor = AnchorStyles.Right
            });
            grpPresets.Controls.Add(presetsTbl);

            mainLayout.Controls.Add(grpPresets, 0, 3);
            mainLayout.SetColumnSpan(grpPresets, 2);

            var spacer = new Panel { Height = 0, Dock = DockStyle.Fill };
            mainLayout.Controls.Add(spacer, 0, 4);
            mainLayout.SetColumnSpan(spacer, 2);

            this.Controls.Add(mainLayout);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var toolTip = new ToolTip();
            toolTip.SetToolTip(chkOperationLock, "Keep the current mode active after an operation is performed");
            toolTip.SetToolTip(chkCtrlMode, "Require holding CTRL to execute operations on click");

            toolTip.SetToolTip(btnAlign, GetString("AlignToolTip", "Align: Rotates the selected object so its source face matches the normal of the target face.\nSHIFT: (No modifier effect)."));
            toolTip.SetToolTip(btnSnap, GetString("SnapToolTip", "Snap: Moves the selected object to touch the target plane using the closest vertex.\nSHIFT: Snaps using the furthest vertex (places object on the opposite side)."));
            toolTip.SetToolTip(btnAlignSnap, GetString("AlignSnapToolTip", "Align + Snap: Rotates and moves the object to touch the target face, aligning their axes.\nSHIFT: (No modifier effect)."));
            toolTip.SetToolTip(btnClone, GetString("CloneToFaceToolTip", "Clone To Face: Creates a clone of the object and snaps it onto the target face.\nCan create an array of clones if Array counts > 1.\nSHIFT: (No modifier effect)."));
            toolTip.SetToolTip(btnTrim, GetString("TrimToolTip", "Trim: Cuts the selected solids using the plane of the clicked target face.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)."));
            toolTip.SetToolTip(btnPlaceTrim, GetString("PlaceTrimToolTip", "Place & Trim: Aligns, snaps, and cuts the object against the target face in one action.\nSHIFT: Inverts the cut direction (keeps the opposite side of the geometry)."));
            toolTip.SetToolTip(btnMiterJoin, GetString("MiterJoinToolTip", "Miter Join: Joins two intersecting objects at a clean diagonal angle (bisector split).\nSHIFT: Inverts the cut direction for both objects."));
            toolTip.SetToolTip(btnRectify, GetString("RectifyToolTip", "Rectify: Aligns the selected object to the world axes based on its current face normal.\nSHIFT: (No modifier effect)."));
            toolTip.SetToolTip(btnRestore, GetString("RestoreToolTip", "Restore from Anchor: Restores the object to its original position recorded before alignment."));
            toolTip.SetToolTip(btnClearAnchor, GetString("ClearAnchorToolTip", "Clear Anchor: Clears the stored origin/rotation anchor data."));

            btnAddProfile.Click += (s, e) => AddCurrentProfile();
            btnRenameProfile.Click += (s, e) => RenameCurrentProfile();
            btnDeleteProfile.Click += (s, e) => DeleteCurrentProfile();
            cmbProfiles.SelectedIndexChanged += CmbProfiles_SelectedIndexChanged;

            btnAlign.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Align);
            btnAlignSnap.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.AlignSnap);
            btnClone.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.CloneToFace);
            btnTrim.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Trim);
            btnPlaceTrim.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.PlaceTrim);
            btnSnap.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Snap);
            btnRectify.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.Rectify);
            btnMiterJoin.Click += async (s, e) => await _tool.SetCurrentMode(Tools.FaceTool.ToolMode.MiterJoin);
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
                _tool.DeactivatePlugin();
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

        public async void PerformOperation(Tools.FaceTool.ToolMode mode, List<Tools.FaceTool.SelectedFace> faces, bool altMode = false)
        {
            switch (mode)
            {
                case Tools.FaceTool.ToolMode.Align:
                    await PerformAlign(faces);
                    break;
                case Tools.FaceTool.ToolMode.Snap:
                    await PerformSnap(faces, altMode);
                    break;
                case Tools.FaceTool.ToolMode.AlignSnap:
                    await PerformAlignSnap(faces);
                    break;
                case Tools.FaceTool.ToolMode.CloneToFace:
                    await PerformClone(faces, altMode);
                    break;
                case Tools.FaceTool.ToolMode.Trim:
                    await PerformTrim(faces, altMode);
                    break;
                case Tools.FaceTool.ToolMode.Rectify:
                    await PerformRectify(faces);
                    break;
                case Tools.FaceTool.ToolMode.PlaceTrim:
                    await PerformPlaceTrim(faces, altMode);
                    break;
                case Tools.FaceTool.ToolMode.MiterJoin:
                    await PerformMiterJoin(faces, altMode);
                    break;
            }
        }

        private async Task PerformAlign(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            if (source.TransformableObject == target.TransformableObject)
            {
                await _tool.OperationComplete();
                return;
            }

            var pivot = Operations.FacePlacement.GetAlignPivot(source.Face, sourceAnchorIndex);
            var op = Operations.AlignOperation.Create(
                new[] { source.TransformableObject },
                source.Face.Plane.Normal,
                target.Face.Plane.Normal,
                pivot,
                chkRotX.Checked, chkRotY.Checked, chkRotZ.Checked);

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            await _tool.OperationComplete();
        }

        private async Task PerformSnap(List<Tools.FaceTool.SelectedFace> faces, bool altMode)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 1 || doc == null) return;

            var target = faces[0];

            var rawSelection = doc.Selection?.ToList();
            if (rawSelection == null || rawSelection.Count == 0) return;

            // Filter out children whose parent is already in the selection (prevent double transform)
            var candidateSet = new HashSet<IMapObject>(rawSelection);
            var sourceObjects = rawSelection.Where(obj => {
                var parent = obj.Hierarchy.Parent;
                while (parent != null)
                {
                    if (candidateSet.Contains(parent)) return false;
                    parent = parent.Hierarchy.Parent;
                }
                return true;
            }).ToList();

            if (sourceObjects.Count == 0) return;

            // One shared matrix from ALL vertices across ALL selected objects
            var allVerts = sourceObjects
                .SelectMany(o => o.FindAll())
                .OfType<Sledge.BspEditor.Primitives.MapObjects.Solid>()
                .SelectMany(s => s.Faces)
                .SelectMany(f => f.Vertices)
                .ToList();

            if (!allVerts.Any()) return;

            var snapMatrix = Operations.SnapOperation.CreateMatrix(
                allVerts,
                target.Face.Plane,
                (float)numOffset.Value,
                chkLockX.Checked, chkLockY.Checked, chkLockZ.Checked,
                altMode);

            var transaction = new Transaction();
            foreach (var sourceObj in sourceObjects)
                transaction.Add(new Operations.TransformWithTextures(snapMatrix, sourceObj));

            if (chkSnapGrid.Checked)
            {
                var gridData = doc.Map.Data.GetOne<GridData>();
                if (gridData?.Grid != null)
                    transaction.Add(Operations.GridSnapOperation.Create(sourceObjects, gridData.Grid.Spacing));
            }

            // Deselect selected brush objects after snap
            transaction.Add(new Sledge.BspEditor.Modification.Operations.Selection.Deselect(doc.Selection?.ToList() ?? new List<IMapObject>()));

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, transaction);
            await _tool.OperationComplete();
        }

        private async Task PerformAlignSnap(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            if (source.TransformableObject == target.TransformableObject)
            {
                await _tool.OperationComplete();
                return;
            }

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
            await _tool.OperationComplete();
        }

        private async Task PerformClone(List<Tools.FaceTool.SelectedFace> faces, bool altMode)
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
            await _tool.OperationComplete();
        }

        private async Task PerformTrim(List<Tools.FaceTool.SelectedFace> faces, bool altMode)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 1 || doc == null) return;

            var target = faces[0];

            var rawSelection = doc.Selection?.ToList();
            if (rawSelection == null || rawSelection.Count == 0) return;

            // Filter out children whose parent is already in the selection (prevent double split)
            var candidateSet = new HashSet<IMapObject>(rawSelection);
            var sourceObjects = rawSelection.Where(obj => {
                var parent = obj.Hierarchy.Parent;
                while (parent != null)
                {
                    if (candidateSet.Contains(parent)) return false;
                    parent = parent.Hierarchy.Parent;
                }
                return true;
            }).ToList();

            if (sourceObjects.Count == 0) return;

            bool invert = altMode;
            var transaction = new Transaction();

            // Find all solids inside selection and split them
            foreach (var obj in sourceObjects.SelectMany(o => o.FindAll()).OfType<Sledge.BspEditor.Primitives.MapObjects.Solid>())
            {
                var splitOp = Operations.TrimOperation.Create(doc, obj, target.Face.Plane, invert);
                if (splitOp != null)
                {
                    transaction.Add(splitOp);
                }
            }

            if (transaction.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect any selected solids.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _tool.OperationComplete();
                return;
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, transaction);
            
            await _tool.OperationComplete();
        }
        
        private async Task PerformRectify(List<Tools.FaceTool.SelectedFace> faces)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 1 || doc == null) return;

            var source = faces[0];

            var op = Operations.RectifyOperation.Create(doc, new[] { source.TransformableObject }, source.Face.Plane.Normal, source.Face.Origin);
            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            await _tool.OperationComplete();
        }

        private async Task PerformPlaceTrim(List<Tools.FaceTool.SelectedFace> faces, bool altMode)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            if (source.TransformableObject == target.TransformableObject)
            {
                await _tool.OperationComplete();
                return;
            }

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
            bool invert = altMode;
            var op = Operations.TrimOperation.CreateTransformAndTrim(doc, targets, alignMatrix * snapMatrix, target.Face.Plane, invert);
            if (op is Transaction t && t.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect the aligned/snapped geometry.", "Trim Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _tool.OperationComplete();
                return;
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);

            await _tool.OperationComplete();
        }

        private async Task PerformMiterJoin(List<Tools.FaceTool.SelectedFace> faces, bool altMode)
        {
            var doc = _tool.GetDocument();
            if (faces.Count < 2 || doc == null) return;

            var source = faces[0];
            var target = faces[1];

            if (source.Solid == target.Solid)
            {
                await _tool.OperationComplete();
                return;
            }

            var op = Operations.MiterJoinOperation.Create(doc, source.Solid, source.Face, target.Solid, target.Face, altMode);
            if (op is Transaction t && t.IsEmpty)
            {
                MessageBox.Show("The clipping plane does not intersect the selected solids.", "Miter Join Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _tool.OperationComplete();
                return;
            }

            await Sledge.BspEditor.Modification.MapDocumentOperation.Perform(doc, op);
            await _tool.OperationComplete();
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
        public bool IsCtrlModeChecked => chkCtrlMode.Checked;

        public void UpdateInterfaceForMode(Tools.FaceTool.ToolMode mode)
        {
            UpdateOperationButtons(mode);
            UpdateAnchorUI();
        }

        public void UpdateOperationButtons(Tools.FaceTool.ToolMode mode)
        {
            var buttons = new Dictionary<Tools.FaceTool.ToolMode, Button>
            {
                [Tools.FaceTool.ToolMode.Align] = btnAlign,
                [Tools.FaceTool.ToolMode.Snap] = btnSnap,
                [Tools.FaceTool.ToolMode.AlignSnap] = btnAlignSnap,
                [Tools.FaceTool.ToolMode.CloneToFace] = btnClone,
                [Tools.FaceTool.ToolMode.Trim] = btnTrim,
                [Tools.FaceTool.ToolMode.Rectify] = btnRectify,
                [Tools.FaceTool.ToolMode.PlaceTrim] = btnPlaceTrim,
                [Tools.FaceTool.ToolMode.MiterJoin] = btnMiterJoin,
            };

            Color normalBack = btnRestore != null ? btnRestore.BackColor : SystemColors.Control;
            Color normalFore = btnRestore != null ? btnRestore.ForeColor : SystemColors.ControlText;

            foreach (var p in buttons)
            {
                if (p.Value == null) continue;
                bool isActive = p.Key == mode;
                if (isActive)
                {
                    p.Value.BackColor = Color.DodgerBlue;
                    p.Value.ForeColor = Color.White;
                    p.Value.Font = new Font(p.Value.Font, FontStyle.Bold);
                }
                else
                {
                    p.Value.BackColor = normalBack;
                    p.Value.ForeColor = normalFore;
                    p.Value.Font = new Font(p.Value.Font, FontStyle.Regular);
                }
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
            UpdateOperationButtons(_tool.CurrentMode);
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
                _tool.ShowHoverHelper = settings.ShowHoverHelper;
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
                chkCtrlMode.Checked = settings.CtrlMode;
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
                    ShowHoverHelper = _tool.ShowHoverHelper,
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
                    KeepHierarchy = chkKeepHierarchy.Checked,
                    CtrlMode = chkCtrlMode.Checked
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

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            if (_tool.ProcessModeChangeKey(keyData))
            {
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private string GetString(string key, string defaultValue)
        {
            if (_translator?.Value == null) return defaultValue;
            var val = _translator.Value.GetString("HammerTime.FaceTool.FaceToolWindow", key);
            return string.IsNullOrEmpty(val) ? defaultValue : val;
        }

        private static int GridIndexFromAnchor(int anchorIndex)
            => anchorIndex == Operations.FacePlacement.AnchorOff ? -1 : Math.Clamp(anchorIndex, 0, 8);

        private void LoadProfilesList()
        {
            _profiles.Clear();
            try
            {
                if (File.Exists(ProfilesFilePath))
                {
                    string json = File.ReadAllText(ProfilesFilePath);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<List<FaceToolProfile>>(json);
                    if (loaded != null) _profiles.AddRange(loaded);
                }
            }
            catch { }

            if (_profiles.Count == 0)
            {
                _profiles.Add(new FaceToolProfile { Name = "Default", ScopeIndex = 0, Offset = 0, CtrlMode = true });
                _profiles.Add(new FaceToolProfile { Name = "Wall Alignment (No Rot Z)", ScopeIndex = 0, RotX = true, RotY = true, RotZ = false });
                _profiles.Add(new FaceToolProfile { Name = "Pipe Miter Join", ScopeIndex = 1, RotX = true, RotY = true, RotZ = true });
                SaveProfilesList();
            }

            UpdateProfilesCombo();
        }

        private void SaveProfilesList()
        {
            try
            {
                string? dir = Path.GetDirectoryName(ProfilesFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = System.Text.Json.JsonSerializer.Serialize(_profiles, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilesFilePath, json);
            }
            catch { }
        }

        private void UpdateProfilesCombo()
        {
            if (cmbProfiles == null) return;
            
            cmbProfiles.SelectedIndexChanged -= CmbProfiles_SelectedIndexChanged;
            
            string? currentName = cmbProfiles.SelectedItem?.ToString();
            cmbProfiles.Items.Clear();
            foreach (var p in _profiles)
            {
                cmbProfiles.Items.Add(p.Name);
            }
            
            int idx = -1;
            if (currentName != null)
            {
                idx = cmbProfiles.FindStringExact(currentName);
            }
            if (idx == -1 && cmbProfiles.Items.Count > 0)
            {
                idx = 0;
            }
            if (idx >= 0)
            {
                cmbProfiles.SelectedIndex = idx;
                LoadProfile(_profiles[idx]);
            }
            
            cmbProfiles.SelectedIndexChanged += CmbProfiles_SelectedIndexChanged;
        }

        private void CmbProfiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = cmbProfiles.SelectedIndex;
            if (idx >= 0 && idx < _profiles.Count)
            {
                LoadProfile(_profiles[idx]);
            }
        }

        private void LoadProfile(FaceToolProfile profile)
        {
            if (profile == null) return;

            cmbScope.SelectedIndex = Math.Clamp(profile.ScopeIndex, 0, cmbScope.Items.Count - 1);
            numOffset.Value = Math.Clamp(profile.Offset, numOffset.Minimum, numOffset.Maximum);
            chkSnapGrid.Checked = profile.SnapToGrid;
            chkLockX.Checked = profile.LockX;
            chkLockY.Checked = profile.LockY;
            chkLockZ.Checked = profile.LockZ;
            chkRotX.Checked = profile.RotX;
            chkRotY.Checked = profile.RotY;
            chkRotZ.Checked = profile.RotZ;
            chkKeepHierarchy.Checked = profile.KeepHierarchy;
            chkOperationLock.Checked = profile.OperationLock;
            chkCtrlMode.Checked = profile.CtrlMode;

            numArrayX.Value = Math.Clamp(profile.ArrayX, numArrayX.Minimum, numArrayX.Maximum);
            numArrayY.Value = Math.Clamp(profile.ArrayY, numArrayY.Minimum, numArrayY.Maximum);
            numSpaceX.Value = Math.Clamp(profile.SpaceX, numSpaceX.Minimum, numSpaceX.Maximum);
            numSpaceY.Value = Math.Clamp(profile.SpaceY, numSpaceY.Minimum, numSpaceY.Maximum);

            SetTargetRose(GridIndexFromAnchor(profile.TargetAnchor));
            SetSourceRose(GridIndexFromAnchor(profile.SourceAnchor));
        }

        private FaceToolProfile GetCurrentSettings(string name)
        {
            return new FaceToolProfile
            {
                Name = name,
                ScopeIndex = cmbScope.SelectedIndex,
                Offset = numOffset.Value,
                SnapToGrid = chkSnapGrid.Checked,
                LockX = chkLockX.Checked,
                LockY = chkLockY.Checked,
                LockZ = chkLockZ.Checked,
                RotX = chkRotX.Checked,
                RotY = chkRotY.Checked,
                RotZ = chkRotZ.Checked,
                TargetAnchor = targetAnchorIndex,
                SourceAnchor = sourceAnchorIndex,
                ArrayX = (int)numArrayX.Value,
                ArrayY = (int)numArrayY.Value,
                SpaceX = numSpaceX.Value,
                SpaceY = numSpaceY.Value,
                KeepHierarchy = chkKeepHierarchy.Checked,
                OperationLock = chkOperationLock.Checked,
                CtrlMode = chkCtrlMode.Checked
            };
        }

        private void AddCurrentProfile()
        {
            string name = PromptForInput("Enter profile name:", "Add Profile");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (_profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A profile named '{name}' already exists.", "Add Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newProfile = GetCurrentSettings(name);
            _profiles.Add(newProfile);
            SaveProfilesList();
            UpdateProfilesCombo();

            int idx = cmbProfiles.FindStringExact(name);
            if (idx >= 0) cmbProfiles.SelectedIndex = idx;
        }

        private void DeleteCurrentProfile()
        {
            int idx = cmbProfiles.SelectedIndex;
            if (idx < 0 || idx >= _profiles.Count) return;

            var profile = _profiles[idx];
            if (profile.Name == "Default")
            {
                MessageBox.Show("Cannot delete the Default profile.", "Delete Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to delete profile '{profile.Name}'?", "Delete Profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _profiles.RemoveAt(idx);
                SaveProfilesList();
                UpdateProfilesCombo();
            }
        }

        private void RenameCurrentProfile()
        {
            int idx = cmbProfiles.SelectedIndex;
            if (idx < 0 || idx >= _profiles.Count) return;

            var profile = _profiles[idx];
            if (profile.Name == "Default")
            {
                MessageBox.Show("Cannot rename the Default profile.", "Rename Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newName = PromptForInput("Enter new profile name:", "Rename Profile", profile.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName.Equals(profile.Name, StringComparison.Ordinal)) return;

            if (_profiles.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A profile named '{newName}' already exists.", "Rename Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            profile.Name = newName;
            SaveProfilesList();
            UpdateProfilesCombo();

            int newIdx = cmbProfiles.FindStringExact(newName);
            if (newIdx >= 0) cmbProfiles.SelectedIndex = newIdx;
        }

        private string PromptForInput(string promptText, string title, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 320,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label textLabel = new Label() { Left = 16, Top = 16, Text = promptText, AutoSize = true };
            TextBox textBox = new TextBox() { Left = 16, Top = 36, Width = 270, Text = defaultValue };
            Button confirmation = new Button() { Text = "OK", Left = 110, Width = 80, Top = 75, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 200, Width = 80, Top = 75, DialogResult = DialogResult.Cancel };
            
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            Sledge.Shell.Registers.DialogRegister.ColorControlsRecursively(prompt, _isDarkTheme);

            return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text : "";
        }
    }

    public class FaceToolProfile
    {
        public string Name { get; set; } = "";
        public int ScopeIndex { get; set; } = 0;
        public decimal Offset { get; set; } = 0;
        public bool SnapToGrid { get; set; } = false;
        public bool LockX { get; set; } = false;
        public bool LockY { get; set; } = false;
        public bool LockZ { get; set; } = false;
        public bool RotX { get; set; } = true;
        public bool RotY { get; set; } = true;
        public bool RotZ { get; set; } = false;
        public int TargetAnchor { get; set; } = -1;
        public int SourceAnchor { get; set; } = -1;
        public int ArrayX { get; set; } = 1;
        public int ArrayY { get; set; } = 1;
        public decimal SpaceX { get; set; } = 0;
        public decimal SpaceY { get; set; } = 0;
        public bool KeepHierarchy { get; set; } = true;
        public bool OperationLock { get; set; } = false;
        public bool CtrlMode { get; set; } = true;
    }

    public class FaceToolSettings
    {
        public int WindowX { get; set; } = int.MinValue;
        public int WindowY { get; set; } = int.MinValue;
        public int ScopeIndex { get; set; } = 0;
        public decimal Offset { get; set; } = 0;
        public bool SnapToGrid { get; set; } = false;
        public bool ShowHoverHelper { get; set; } = true;
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
        public bool CtrlMode { get; set; } = true;
    }
}
