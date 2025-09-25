using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsLegacyTest
{
    public partial class Form1 : Form
    {
        // dynamic state
        private string[] hangars;             // generated based on user choice
        private int days;                     // number of days chosen by user
        private readonly Dictionary<string, Booking> bookings = new Dictionary<string, Booking>();
        private readonly Dictionary<string, Button> slotButtons = new Dictionary<string, Button>();
        private Button selectedSlot = null;

        // sizing constants for the scrollable grid
        private const int DayColumnWidth = 120;
        private const int HangarColumnWidth = 120;
        private const int HeaderHeight = 30;

        public Form1()
        {
            InitializeComponent();

            // Show configuration dialog before building UI
            using (var cfg = new ConfigureDialog())
            {
                var dr = cfg.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    int hangarCount = cfg.HangarCount;
                    int daysCount = cfg.DaysCount;

                    // validate
                    if (hangarCount < 1) hangarCount = 1;
                    if (daysCount < 1) daysCount = 1;

                    // create hangar names "Hangar 1", "Hangar 2", ...
                    hangars = new string[hangarCount];
                    for (int i = 0; i < hangarCount; i++)
                        hangars[i] = $"Hangar {i + 1}";

                    days = daysCount;
                }
                else
                {
                    // user cancelled configuration: set sensible defaults
                    hangars = new string[] { "Hangar 1", "Hangar 2", "Hangar 3", "Hangar 4" };
                    days = 7;
                }
            }

            // build the UI now that we know hangar/day counts
            SetupUI();
        }

        // Booking data container
        private class Booking
        {
            public string Hangar { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
        }

        // ---------- UI builder ----------
        private void SetupUI()
        {
            // clear existing UI/state
            this.Controls.Clear();
            bookings.Clear();
            slotButtons.Clear();
            selectedSlot = null;

            // sizing constants (tweak to taste)
            const int DayColumnWidth = 120;      // px per day column when using absolute sizing
            const int HangarColumnWidth = 120;   // px for the hangar-name column
            const int HeaderHeight = 30;         // px for the date header row
            const int HangarRowHeight = 120;     // px per hangar row when vertical scrolling is needed
            const int TopButtonsHeight = 40;     // top control bar height

            // ---------------- main layout ----------------
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, TopButtonsHeight)); // buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));              // content
            this.Controls.Add(mainLayout);

            // --------------- top buttons -----------------
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            Button addButton = new Button { Text = "Add", Width = 75 };
            Button editButton = new Button { Text = "Edit", Width = 75 };
            Button deleteButton = new Button { Text = "Delete", Width = 75 };

            addButton.Click += AddButton_Click;
            editButton.Click += EditButton_Click;
            deleteButton.Click += DeleteButton_Click;

            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(deleteButton);

            mainLayout.Controls.Add(buttonPanel, 0, 0);

            // --------------- scroll container -------------
            Panel scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            mainLayout.Controls.Add(scrollPanel, 0, 1);

            // --------------- schedule grid ----------------
            TableLayoutPanel scheduleGrid = new TableLayoutPanel
            {
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            int colCount = 1 + days;
            int rowCount = 1 + hangars.Length;
            scheduleGrid.ColumnCount = colCount;
            scheduleGrid.RowCount = rowCount;

            // NOTE: We'll set ColumnStyles/RowStyles in UpdateSizing() below,
            // but TableLayoutPanel needs the count set before adding controls.

            // ---- Add header (top-left and date headers) ----
            var headerHangar = new Label
            {
                Text = "Hangar/Date",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            scheduleGrid.Controls.Add(headerHangar, 0, 0);

            for (int d = 0; d < days; d++)
            {
                DateTime day = DateTime.Today.AddDays(d);
                var dateLabel = new Label
                {
                    Text = day.ToString("MM/dd"),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Margin = new Padding(0)
                };
                scheduleGrid.Controls.Add(dateLabel, d + 1, 0);
            }

            // ---- Add body: hangar labels + buttons ----
            for (int r = 0; r < hangars.Length; r++)
            {
                var hangarLabel = new Label
                {
                    Text = hangars[r],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 0, 0)
                };
                scheduleGrid.Controls.Add(hangarLabel, 0, r + 1);

                for (int c = 0; c < days; c++)
                {
                    DateTime date = DateTime.Today.AddDays(c);
                    string key = SlotKey(hangars[r], date);

                    var slot = new Button
                    {
                        Dock = DockStyle.Fill,
                        Text = "",
                        Tag = key,
                        Margin = new Padding(4),
                        UseVisualStyleBackColor = false
                    };
                    slot.Click += Slot_Click;

                    scheduleGrid.Controls.Add(slot, c + 1, r + 1);
                    slotButtons[key] = slot;
                }
            }

            // add scheduleGrid to the scroll panel
            scrollPanel.Controls.Add(scheduleGrid);
            scheduleGrid.Location = new Point(0, 0);
            scheduleGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // ---- Sizing / layout logic that switches between "fill" and "scroll" modes ----
            void UpdateSizing()
            {
                int panelWidth = Math.Max(1, scrollPanel.ClientSize.Width);
                int panelHeight = Math.Max(1, scrollPanel.ClientSize.Height);

                // natural sizes if we used absolute columns/rows
                int totalGridWidth = HangarColumnWidth + (days * DayColumnWidth);
                int totalGridHeight = HeaderHeight + (hangars.Length * HangarRowHeight);

                // --- choose column strategy ---
                if (totalGridWidth <= panelWidth)
                {
                    // enough horizontal space: make date columns percent-based and fill horizontally
                    scheduleGrid.ColumnStyles.Clear();
                    scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HangarColumnWidth));
                    for (int i = 0; i < days; i++)
                        scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / days));

                    scheduleGrid.Dock = DockStyle.Fill; // fill horizontally inside the panel
                    scheduleGrid.AutoSize = false;
                }
                else
                {
                    // not enough width: make date columns fixed and allow horizontal scrolling
                    scheduleGrid.ColumnStyles.Clear();
                    scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, HangarColumnWidth));
                    for (int i = 0; i < days; i++)
                        scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DayColumnWidth));

                    scheduleGrid.Dock = DockStyle.None; // allow the grid to be larger than panel
                    scheduleGrid.AutoSize = false;
                    scheduleGrid.Width = totalGridWidth;
                    scheduleGrid.Location = new Point(0, 0);
                    scheduleGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                }

                // --- choose row strategy (vertical behavior) ---
                if (totalGridHeight <= panelHeight)
                {
                    // enough vertical space: make hangar rows percent-based so they all expand evenly
                    scheduleGrid.RowStyles.Clear();
                    scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight)); // header
                    for (int r = 0; r < hangars.Length; r++)
                        scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / hangars.Length));

                    // fill vertically so rows expand to fill panel height
                    scheduleGrid.Height = panelHeight;
                }
                else
                {
                    // not enough height: make each hangar row a fixed height, allow vertical scrolling
                    scheduleGrid.RowStyles.Clear();
                    scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight)); // header
                    for (int r = 0; r < hangars.Length; r++)
                        scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, HangarRowHeight));

                    scheduleGrid.Height = totalGridHeight;
                    scheduleGrid.Location = new Point(0, 0);
                    scheduleGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                }

                // ensure scrollPanel knows the grid size (so scrollbars appear appropriately)
                scheduleGrid.Refresh();

                // repaint slot visuals according to bookings (keeps color/selection consistent)
                RefreshSlotVisuals();
            }

            // initial sizing pass
            UpdateSizing();

            // keep sizing in sync when the scrollPanel (or window) is resized
            scrollPanel.Resize += (s, e) => UpdateSizing();
            this.Resize += (s, e) => UpdateSizing();
        }


        // helper: build a key for hangar+date
        private string SlotKey(string hangar, DateTime date) => $"{hangar}-{date:yyyy-MM-dd}";

        // parse key back to hangar/date
        private (string hangar, DateTime date) ParseSlotKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 11)
                throw new ArgumentException("Invalid slot key", nameof(key));
            string datePart = key.Substring(key.Length - 10);
            string hangarPart = key.Substring(0, key.Length - 11);
            DateTime date = DateTime.Parse(datePart);
            return (hangarPart, date);
        }

        // refresh visuals: booked = red, free = green
        private void RefreshSlotVisuals()
        {
            foreach (var kv in slotButtons)
            {
                string key = kv.Key;
                Button b = kv.Value;

                if (bookings.ContainsKey(key))
                {
                    b.BackColor = Color.LightCoral;
                    string desc = bookings[key].Description ?? "";
                    b.Text = desc.Length <= 30 ? desc : desc.Substring(0, 27) + "…";
                }
                else
                {
                    b.BackColor = Color.LightGreen;
                    b.Text = "";
                }

                // selection highlight
                if (selectedSlot == b)
                    b.FlatStyle = FlatStyle.Popup;
                else
                    b.FlatStyle = FlatStyle.Standard;
            }
        }

        // clicking a slot selects it and opens editor (same behavior as before)
        private void Slot_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;

            // clear previous selection visual
            if (selectedSlot != null && !selectedSlot.IsDisposed)
                selectedSlot.FlatStyle = FlatStyle.Standard;

            // select this slot and show it visually
            selectedSlot = btn;
            selectedSlot.FlatStyle = FlatStyle.Popup;

            // (do NOT open the BookingDialog here)
        }


        private void AddButton_Click(object sender, EventArgs e)
        {
            if (selectedSlot == null)
            {
                MessageBox.Show("Please select a slot first.", "Add", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string oldKey = selectedSlot.Tag as string;
            if (string.IsNullOrEmpty(oldKey)) return;

            (string preHangar, DateTime preDate) = ParseSlotKey(oldKey);

            using (var dlg = new BookingDialog(GetHangarNames(), days, preselectedHangar: preHangar, preselectedDate: preDate))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var booking = new Booking
                    {
                        Hangar = dlg.SelectedHangar,
                        Date = dlg.SelectedDate,
                        Description = dlg.Description
                    };
                    string newKey = SlotKey(booking.Hangar, booking.Date);

                    bookings[newKey] = booking;
                    if (newKey != oldKey && bookings.ContainsKey(oldKey))
                        bookings.Remove(oldKey);

                    RefreshSlotVisuals();
                }
            }
        }


        private void EditButton_Click(object sender, EventArgs e)
        {
            if (selectedSlot == null)
            {
                MessageBox.Show("Please select a slot first.", "Edit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string key = selectedSlot.Tag as string;
            if (string.IsNullOrEmpty(key)) return;

            Booking pre = bookings.ContainsKey(key) ? bookings[key] : null;
            (string preHangar, DateTime preDate) = ParseSlotKey(key);

            using (var dlg = pre != null
                             ? new BookingDialog(GetHangarNames(), days, preselectedHangar: pre.Hangar, preselectedDate: pre.Date)
                             : new BookingDialog(GetHangarNames(), days, preselectedHangar: preHangar, preselectedDate: preDate))
            {
                if (pre != null) dlg.Description = pre.Description;

                var dr = dlg.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    var updated = new Booking { Hangar = dlg.SelectedHangar, Date = dlg.SelectedDate, Description = dlg.Description };
                    string newKey = SlotKey(updated.Hangar, updated.Date);

                    if (bookings.ContainsKey(newKey) && newKey != key)
                    {
                        if (MessageBox.Show("That slot is already booked. Overwrite?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;
                    }

                    bookings[newKey] = updated;
                    if (newKey != key && bookings.ContainsKey(key))
                        bookings.Remove(key);

                    RefreshSlotVisuals();
                }
                else if (dr == DialogResult.Abort)
                {
                    if (bookings.ContainsKey(key))
                    {
                        bookings.Remove(key);
                        RefreshSlotVisuals();
                    }
                }
            }
        }


        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (selectedSlot == null)
            {
                MessageBox.Show("Please select a slot to delete.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string key = selectedSlot.Tag as string;
            if (string.IsNullOrEmpty(key)) return;

            if (bookings.ContainsKey(key))
            {
                if (MessageBox.Show("Remove this booking?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    bookings.Remove(key);
                    RefreshSlotVisuals();
                }
            }
            else
            {
                MessageBox.Show("No booking exists in the selected slot.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }


        // helper: return current hangar name list
        private string[] GetHangarNames()
        {
            return (string[])hangars.Clone();
        }

        // ---------- Small dialogs used by the form ----------
        // BookingDialog is the same inline modal used earlier to pick hangar/date/description
        private class BookingDialog : Form
        {
            private ComboBox cbHangar;
            private ComboBox cbDate;
            private TextBox txtDesc;
            private Button btnOk;
            private Button btnCancel;

            public string SelectedHangar => cbHangar.SelectedItem?.ToString();
            public DateTime SelectedDate => DateTime.Parse(cbDate.SelectedItem.ToString().Split('|')[0].Trim());
            public string Description { get => txtDesc.Text; set => txtDesc.Text = value; }

            public BookingDialog(string[] hangars, int days, string preselectedHangar = null, DateTime? preselectedDate = null)
            {
                Text = "Booking";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                Width = 420;
                Height = 220;

                Label lblHangar = new Label { Text = "Hangar:", Left = 10, Top = 14, Width = 60 };
                cbHangar = new ComboBox { Left = 80, Top = 10, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
                cbHangar.Items.AddRange(hangars);

                Label lblDate = new Label { Text = "Date:", Left = 10, Top = 48, Width = 60 };
                cbDate = new ComboBox { Left = 80, Top = 44, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };

                for (int i = 0; i < days; i++)
                {
                    DateTime d = DateTime.Today.AddDays(i);
                    cbDate.Items.Add($"{d:yyyy-MM-dd} | {d:ddd MMM dd}");
                }

                Label lblDesc = new Label { Text = "Description:", Left = 10, Top = 82, Width = 80 };
                txtDesc = new TextBox { Left = 10, Top = 105, Width = 370, Height = 30 };

                btnOk = new Button { Text = "OK", Left = 220, Width = 70, Top = 140, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 300, Width = 70, Top = 140, DialogResult = DialogResult.Cancel };


                Controls.Add(lblHangar);
                Controls.Add(cbHangar);
                Controls.Add(lblDate);
                Controls.Add(cbDate);
                Controls.Add(lblDesc);
                Controls.Add(txtDesc);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                // preselect
                if (!string.IsNullOrEmpty(preselectedHangar) && cbHangar.Items.Contains(preselectedHangar))
                    cbHangar.SelectedItem = preselectedHangar;
                else if (cbHangar.Items.Count > 0)
                    cbHangar.SelectedIndex = 0;

                if (preselectedDate.HasValue)
                {
                    string dateKey = preselectedDate.Value.ToString("yyyy-MM-dd");
                    for (int i = 0; i < cbDate.Items.Count; i++)
                    {
                        string item = cbDate.Items[i].ToString();
                        if (item.StartsWith(dateKey))
                        {
                            cbDate.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else if (cbDate.Items.Count > 0)
                    cbDate.SelectedIndex = 0;
            }
        }

        // small dialog to configure hangar/day counts (shown on startup)
        private class ConfigureDialog : Form
        {
            private NumericUpDown nudHangars;
            private NumericUpDown nudDays;
            private Button btnOk;
            private Button btnCancel;

            public int HangarCount => (int)nudHangars.Value;
            public int DaysCount => (int)nudDays.Value;

            public ConfigureDialog()
            {
                Text = "Configure Schedule Size";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                Width = 320;
                Height = 180;

                Label lbl1 = new Label { Text = "Number of Hangars:", Left = 10, Top = 14, Width = 140 };
                nudHangars = new NumericUpDown { Left = 160, Top = 10, Width = 120, Minimum = 1, Maximum = 200, Value = 4 };

                Label lbl2 = new Label { Text = "Number of Days:", Left = 10, Top = 48, Width = 140 };
                nudDays = new NumericUpDown { Left = 160, Top = 44, Width = 120, Minimum = 1, Maximum = 365, Value = 7 };

                btnOk = new Button { Text = "OK", Left = 140, Top = 90, Width = 70, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 220, Top = 90, Width = 70, DialogResult = DialogResult.Cancel };

                Controls.Add(lbl1);
                Controls.Add(nudHangars);
                Controls.Add(lbl2);
                Controls.Add(nudDays);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }
    }
}
