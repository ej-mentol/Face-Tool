using System.ComponentModel.Composition;
using System.Drawing;
using System.Threading.Tasks;
using Sledge.Common.Shell.Context;
using Sledge.Common.Shell.Menu;
using LogicAndTrick.Oy;

namespace HammerTime.FaceTool.UI
{
    /// <summary>
    /// Adds "Face Tool" entry to the Tools menu. Activates the FaceTool
    /// by publishing the Sledge tool activation message via Oy.
    /// </summary>
    [Export(typeof(IMenuItem))]
    public class FaceToolMenuItem : IMenuItem
    {
        [Import]
        private Tools.FaceTool _faceTool = null!;

        // IMenuItem
        public string ID => "HammerTime_FaceTool_Activate";
        public string Name => "Face Tool";
        public string Description => "Activate the Face Tool for face-to-face alignment and snapping";
        public Image? Icon => null;
        public bool AllowedInToolbar => true;

        // Menu placement
        public string Section => "Tools";
        public string Path => "";
        public string Group => "Tools";
        public string OrderHint => "J";
        public string ShortcutText => "Shift+F";

        // IMenuItemExtendedProperties
        public bool IsToggle => false;
        public bool GetToggleState(IContext context) => false;

        // IContextAware
        public bool IsInContext(IContext context) => true;

        public async Task Invoke(IContext context)
        {
            await Oy.Publish("Tool:Activated", _faceTool);
        }
    }
}
