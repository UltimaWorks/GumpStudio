using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using Ultima;

namespace GumpStudio
{
    public class GumpArtBrowser : Form
    {
        // ── Original fields ──
        private Button _cmdCache;
        private Button _cmdOK;
        private Label _lblSize;
        private Label _lblWait;
        private ListBox _lstGump;
        private Panel _Panel1;
        private PictureBox _picFullSize;
        private ToolTip _ToolTip1;
        protected static GumpCacheEntry[] Cache;
        private IContainer components;
        public int GumpID;

        // ── NEW: Filter controls ──
        private ComboBox _cmbCategory;
        private TextBox _txtSearch;
        private Label _lblCategory;
        private Label _lblSearch;

        // ── NEW: Filtered view + fast lookup ──
        private List<GumpCacheEntry> _filteredCache = new List<GumpCacheEntry>();
        private static HashSet<int> _validGumpIDs;

        // ── NEW: Pre-settable category ──
        private GumpCategory _filterCategory = GumpCategory.All;
        public GumpCategory FilterCategory
        {
            get => _filterCategory;
            set
            {
                _filterCategory = value;
                if (_cmbCategory != null)
                    _cmbCategory.SelectedItem = value;
            }
        }

        public GumpArtBrowser()
        {
            Load += GumpArtBrowser_Load;
            InitializeComponent();
        }

        // ── Cache building (original logic preserved) ──
        protected void BuildCache()
        {
            _lblWait.Text = @"Please Wait, Generating Art Cache...";
            Show();
            Cache = null;
            _lstGump.Items.Clear();
            _lblWait.Visible = true;
            Application.DoEvents();
            var index = 0;
            int maxValue = UInt16.MaxValue;
            try
            {
                do
                {
                    _lblWait.Text =
                      $@"Please Wait, Generating Art Cache...  {(int)(100 * index / (double)maxValue)}%";
                    Application.DoEvents();
                    Bitmap gump;
                    try { gump = Gumps.GetGump(index); }
                    catch (Exception) { ++index; return; }
                    if (gump != null)
                    {
                        if (Cache != null)
                            Array.Resize(ref Cache, Cache.Length + 1);
                        else
                            Cache = new GumpCacheEntry[1];

                        Cache[Cache.Length - 1] = new GumpCacheEntry
                            { ID = index, Size = gump.Size };
                        gump.Dispose();
                    }
                    ++index;
                } while (index <= maxValue);

                using (var fs = new FileStream(
                    Application.StartupPath + "/GumpArt.cache", FileMode.Create))
                {
                    new BinaryFormatter().Serialize(fs,
                        Cache ?? throw new InvalidOperationException());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Error creating cache file:" + ex.Message);
            }
            finally
            {
                _lblWait.Visible = false;
                Application.DoEvents();
            }
            BuildLookup();
        }

        // ── NEW: Build HashSet for O(1) ID lookups ──
        private static void BuildLookup()
        {
            _validGumpIDs = new HashSet<int>();
            if (Cache == null) return;
            foreach (var entry in Cache)
                _validGumpIDs.Add(entry.ID);
        }

        // ── NEW: Heuristics ──
        private bool IsBackgroundGump(int id)
        {
            for (int i = 0; i < 9; i++)
                if (!_validGumpIDs.Contains(id + i)) return false;
            return true;
        }

        private bool IsButtonGump(GumpCacheEntry entry)
        {
            bool hasPair = _validGumpIDs.Contains(entry.ID + 1)
                        || _validGumpIDs.Contains(entry.ID - 1);
            bool isSmall = entry.Size.Width <= 300 && entry.Size.Height <= 150;
            return hasPair && isSmall && !IsBackgroundGump(entry.ID);
        }

        private bool IsCheckboxGump(GumpCacheEntry entry)
        {
            bool hasPair = _validGumpIDs.Contains(entry.ID + 1)
                        || _validGumpIDs.Contains(entry.ID - 1);
            bool isSmall = entry.Size.Width <= 80 && entry.Size.Height <= 80;
            bool square  = Math.Abs(entry.Size.Width - entry.Size.Height) <= 20;
            return hasPair && isSmall && square;
        }

        // ── NEW: Apply both category + search text filters ──
        private void ApplyFilters()
        {
            _filteredCache.Clear();
            if (Cache == null) return;

            string search = _txtSearch?.Text?.Trim() ?? "";
            GumpCategory cat = _cmbCategory?.SelectedItem is GumpCategory c
                ? c : _filterCategory;

            foreach (var entry in Cache)
            {
                // Search filter (hex or decimal ID)
                if (!string.IsNullOrEmpty(search))
                {
                    string hex = "0x" + entry.ID.ToString("X");
                    string dec = entry.ID.ToString();
                    if (hex.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                     && dec.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                // Category filter
                switch (cat)
                {
                    case GumpCategory.Background:
                        if (!IsBackgroundGump(entry.ID)) continue; break;
                    case GumpCategory.Button:
                        if (!IsButtonGump(entry)) continue; break;
                    case GumpCategory.Checkbox:
                        if (!IsCheckboxGump(entry)) continue; break;
                    case GumpCategory.Image:
                        break; // show all
                    case GumpCategory.All:
                    default:
                        break;
                }
                _filteredCache.Add(entry);
            }
        }

        // ── PopulateListbox — now uses filtered results ──
        private void PopulateListbox()
        {
            _lstGump.Items.Clear();
            ApplyFilters();
            foreach (var entry in _filteredCache)
                _lstGump.Items.Add(entry.ID);
            _lstGump.SelectedItem = GumpID;
        }

        // ── NEW: Filter change handlers ──
        private void cmbCategory_SelectedIndexChanged(object sender, EventArgs e)
            => PopulateListbox();

        private void txtSearch_TextChanged(object sender, EventArgs e)
            => PopulateListbox();

        // ── FIXED DrawItem — indexes _filteredCache, not Cache ──
        private void lstGump_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                if (e.Index < 0 || e.Index >= _filteredCache.Count) return;
                var entry = _filteredCache[e.Index];
                var gump = Gumps.GetGump(entry.ID);
                if (gump == null) return;

                var size1 = new Size(
                    Math.Min(entry.Size.Width, 100),
                    Math.Min(entry.Size.Height, 100));
                var rect = new Rectangle(e.Bounds.Location, size1);
                rect.Offset(45, 3);

                e.Graphics.FillRectangle(
                    (e.State & DrawItemState.Selected) > DrawItemState.None
                        ? SystemBrushes.Highlight : SystemBrushes.Window,
                    e.Bounds);
                e.Graphics.DrawString("0x" + entry.ID.ToString("X"),
                    Font, SystemBrushes.WindowText, e.Bounds.X, e.Bounds.Y);
                e.Graphics.DrawImage(gump, rect);
                gump.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error rendering gump art, try rebuilding cache.\r\n\r\n"
                    + ex.Message);
            }
        }

        // ── FIXED MeasureItem — indexes _filteredCache, not Cache ──
        private void lstGump_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _filteredCache.Count)
            { e.ItemHeight = 15; return; }
            var h = _filteredCache[e.Index].Size.Height;
            e.ItemHeight = (h <= 100 ? (h >= 15 ? h : 15) : 100) + 5;
        }

        // ── Remaining handlers (unchanged logic) ──
        private void lstGump_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lstGump.SelectedItem == null) return;
            _picFullSize.Image?.Dispose();
            _picFullSize.Image = Gumps.GetGump(Convert.ToInt32(_lstGump.SelectedItem));
            if (_picFullSize.Image != null)
                _lblSize.Text = "Width: " + _picFullSize.Image.Width
                              + "   Height: " + _picFullSize.Image.Height;
        }

        private void lstGump_DoubleClick(object sender, EventArgs e)
        {
            if (_lstGump.SelectedItem == null) return;
            GumpID = Convert.ToInt32(_lstGump.SelectedItem);
            DialogResult = DialogResult.OK;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            GumpID = Convert.ToInt32(_lstGump.SelectedItem);
            DialogResult = DialogResult.OK;
        }

        private void cmdCache_Click(object sender, EventArgs e)
        {
            _cmdOK.Enabled = false;
            if (MessageBox.Show("Rebuilding the cache may take several minutes.\r\n"
                + "Are you sure?", "Rebuild Cache",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information)
                    == DialogResult.OK)
            {
                BuildCache();
                PopulateListbox();
            }
            _cmdOK.Enabled = true;
        }

        private void GumpArtBrowser_Load(object sender, EventArgs e)
        {
            if (Cache == null)
            {
                if (!File.Exists(Application.StartupPath + "/GumpArt.cache"))
                {
                    BuildCache();
                }
                else
                {
                    FileStream fs = null;
                    try
                    {
                        fs = new FileStream(Application.StartupPath
                            + "/GumpArt.cache", FileMode.Open);
                        Cache = (GumpCacheEntry[])
                            new BinaryFormatter().Deserialize(fs);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error reading cache:\r\n" + ex.Message);
                    }
                    finally { fs?.Close(); }
                }
            }
            if (_validGumpIDs == null) BuildLookup();
            _cmbCategory.SelectedItem = _filterCategory;
            PopulateListbox();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        // ── InitializeComponent — adds filter row at top ──
        private void InitializeComponent()
        {
            components    = new Container();
            _lstGump      = new ListBox();
            _Panel1       = new Panel();
            _picFullSize  = new PictureBox();
            _lblSize      = new Label();
            _lblWait      = new Label();
            _cmdCache     = new Button();
            _ToolTip1     = new ToolTip(components);
            _cmdOK        = new Button();
            _cmbCategory  = new ComboBox();
            _txtSearch    = new TextBox();
            _lblCategory  = new Label();
            _lblSearch    = new Label();

            _Panel1.SuspendLayout();
            ((ISupportInitialize)_picFullSize).BeginInit();
            SuspendLayout();

            // ── Category label + dropdown ──
            _lblCategory.AutoSize = true;
            _lblCategory.Location = new Point(8, 11);
            _lblCategory.Text = "Category:";

            _cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCategory.Location = new Point(66, 8);
            _cmbCategory.Size = new Size(126, 21);
            _cmbCategory.DataSource = Enum.GetValues(typeof(GumpCategory));
            _cmbCategory.SelectedIndexChanged += cmbCategory_SelectedIndexChanged;

            // ── Search label + textbox ──
            _lblSearch.AutoSize = true;
            _lblSearch.Location = new Point(200, 11);
            _lblSearch.Text = "Search:";

            _txtSearch.Location = new Point(250, 8);
            _txtSearch.Size = new Size(150, 20);
            _txtSearch.TextChanged += txtSearch_TextChanged;

            // ── ListBox (shifted down 32px for filter row) ──
            _lstGump.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left;
            _lstGump.DrawMode = DrawMode.OwnerDrawVariable;
            _lstGump.IntegralHeight = false;
            _lstGump.Location = new Point(8, 38);
            _lstGump.Size = new Size(184, 290);
            _lstGump.DrawItem += lstGump_DrawItem;
            _lstGump.MeasureItem += lstGump_MeasureItem;
            _lstGump.SelectedIndexChanged += lstGump_SelectedIndexChanged;
            _lstGump.DoubleClick += lstGump_DoubleClick;

            // ── Preview panel (shifted down 32px) ──
            _Panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                           | AnchorStyles.Left | AnchorStyles.Right;
            _Panel1.AutoScroll = true;
            _Panel1.BackColor = Color.Black;
            _Panel1.BorderStyle = BorderStyle.Fixed3D;
            _Panel1.Controls.Add(_picFullSize);
            _Panel1.Location = new Point(200, 38);
            _Panel1.Size = new Size(312, 256);

            _picFullSize.Location = new Point(0, 0);
            _picFullSize.Size = new Size(100, 50);
            _picFullSize.SizeMode = PictureBoxSizeMode.AutoSize;

            _lblSize.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblSize.AutoSize = true;
            _lblSize.Location = new Point(200, 307);

            _lblWait.BackColor = Color.Transparent;
            _lblWait.BorderStyle = BorderStyle.Fixed3D;
            _lblWait.Font = new Font("Microsoft Sans Serif", 14.25F);
            _lblWait.Location = new Point(168, 131);
            _lblWait.Size = new Size(184, 72);
            _lblWait.Text = "Please Wait, Generating Art Cache...";
            _lblWait.TextAlign = ContentAlignment.MiddleCenter;
            _lblWait.Visible = false;

            _cmdCache.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _cmdCache.FlatStyle = FlatStyle.Flat;
            _cmdCache.Location = new Point(480, 304);
            _cmdCache.Size = new Size(32, 23);
            _ToolTip1.SetToolTip(_cmdCache, "Rebuild Cache");
            _cmdCache.Click += cmdCache_Click;

            _cmdOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _cmdOK.FlatStyle = FlatStyle.System;
            _cmdOK.Location = new Point(400, 304);
            _cmdOK.Size = new Size(75, 23);
            _cmdOK.Text = "OK";
            _cmdOK.Click += cmdOK_Click;

            AcceptButton = _cmdOK;
            AutoScaleBaseSize = new Size(5, 13);
            ClientSize = new Size(520, 334);
            Controls.Add(_lblCategory);
            Controls.Add(_cmbCategory);
            Controls.Add(_lblSearch);
            Controls.Add(_txtSearch);
            Controls.Add(_cmdOK);
            Controls.Add(_cmdCache);
            Controls.Add(_lblWait);
            Controls.Add(_lblSize);
            Controls.Add(_Panel1);
            Controls.Add(_lstGump);
            Text = "GumpID Browser";

            _Panel1.ResumeLayout(false);
            _Panel1.PerformLayout();
            ((ISupportInitialize)_picFullSize).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
