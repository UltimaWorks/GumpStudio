using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Security.Permissions;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.VisualBasic.CompilerServices;
using Ultima;

namespace GumpStudio
{
    public class GumpIDPropEditor : UITypeEditor
    {
        protected IWindowsFormsEditorService edSvc;
        protected int ReturnValue;

        protected static Color Convert555ToARGB(short Col)
        {
            return Color.FromArgb(
                ((short)(Col >> 10) & 31) * 8,
                ((short)(Col >> 5) & 31) * 8,
                (Col & 31) * 8);
        }

        // ── NEW: Auto-detect category from the element being edited ──
        private GumpCategory DetectCategory(ITypeDescriptorContext context)
        {
            if (context?.Instance == null)
                return GumpCategory.All;

            string typeName = context.Instance.GetType().Name;

            if (typeName.Contains("Background"))  return GumpCategory.Background;
            if (typeName.Contains("Button"))      return GumpCategory.Button;
            if (typeName.Contains("Checkbox"))    return GumpCategory.Checkbox;
            if (typeName.Contains("Radio"))       return GumpCategory.Checkbox;
            if (typeName.Contains("Image"))       return GumpCategory.Image;
            if (typeName.Contains("Tiled"))       return GumpCategory.Image;
            if (typeName.Contains("Alpha"))       return GumpCategory.Image;

            return GumpCategory.All;
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public override object EditValue(
            ITypeDescriptorContext context,
            IServiceProvider provider,
            object value)
        {
            edSvc = (IWindowsFormsEditorService)provider
                .GetService(typeof(IWindowsFormsEditorService));
            if (edSvc != null)
            {
                var browser = new GumpArtBrowser
                {
                    GumpID = Conversions.ToInteger(value),
                    FilterCategory = DetectCategory(context)  // ← NEW
                };
                if (edSvc.ShowDialog(browser) == DialogResult.OK)
                {
                    Image gump = Gumps.GetGump(browser.GumpID);
                    if (gump != null)
                    {
                        gump.Dispose();
                        ReturnValue = browser.GumpID;
                        browser.Dispose();
                        return ReturnValue;
                    }
                    MessageBox.Show("Invalid GumpID");
                    return value;
                }
                browser.Dispose();
            }
            return value;
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public override UITypeEditorEditStyle GetEditStyle(
            ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }
    }
}
