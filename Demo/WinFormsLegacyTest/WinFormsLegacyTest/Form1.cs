using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace WinFormsLegacyTest
{
    public partial class Form1 : Form
    {
        // Dynamic state
        private string[] hangars;             // Generated based on user choice
        private int days;                     // Number of days chosen by user
        private readonly Dictionary<string, Booking> bookings = new Dictionary<string, Booking>();
        private readonly Dictionary<string, Button> slotButtons = new Dictionary<string, Button>();
        private Button selectedSlot = null;

        // Used to delay resize handling
        private System.Windows.Forms.Timer resizeTimer;

        // Sizing constants for the scrollable grid
        private const int DayColumnWidth = 120;
        private const int HangarColumnWidth = 120;
        private const int HeaderHeight = 30;

        // Simple undo stack
        private const int MaxUndoSteps = 10;
        private Stack<UndoAction> undoStack = new Stack<UndoAction>();

        private class UndoAction
        {
            public string Description { get; set; }
            // previous state for the affected keys: value == null means key did not exist
            public Dictionary<string, Booking> Previous { get; set; }
            public DateTime Time { get; set; }
        }

        // clone a booking so undo stores independent copies
        private Booking CloneBooking(Booking b)
        {
            if (b == null) return null;
            return new Booking { Hangar = b.Hangar, Date = b.Date, Description = b.Description };
        }

        // push an undo snapshot for the given keys (call BEFORE you change bookings)
        private void PushUndo(IEnumerable<string> keys, string description)
        {
            var prev = new Dictionary<string, Booking>();
            foreach (var k in keys.Distinct())
            {
                prev[k] = bookings.ContainsKey(k) ? CloneBooking(bookings[k]) : null;
            }

            // push and cap stack size
            undoStack.Push(new UndoAction { Description = description, Previous = prev, Time = DateTime.Now });
            while (undoStack.Count > MaxUndoSteps)
                undoStack = new Stack<UndoAction>(undoStack.Reverse().Skip(1).Reverse()); // drop oldest
        }

        // apply the most recent undo
        private void UndoLastAction()
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("Nothing to undo.", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var action = undoStack.Pop();

            // Restore each key to its previous value (null => remove)
            foreach (var kv in action.Previous)
            {
                if (kv.Value == null)
                {
                    // previously there was no booking for this key — remove if present now
                    if (bookings.ContainsKey(kv.Key))
                        bookings.Remove(kv.Key);
                }
                else
                {
                    // restore cloned booking
                    bookings[kv.Key] = CloneBooking(kv.Value);
                }
            }

            // refresh UI
            RefreshSlotVisuals();

            MessageBox.Show($"Undid: {action.Description}", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }




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
            // larger default window
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(1200, 800);

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
            Button blockButton = new Button { Text = "Block", Width = 75 };
            Button multiButton = new Button { Text = "Multi", Width = 75 };
            Button clearButton = new Button { Text = "Clear", Width = 75 };
            Button clearHangarButton = new Button { Text = "Clear Hangar", Width = 100 };
            Button undoButton = new Button { Text = "Undo", Width = 75 };





            addButton.Click += AddButton_Click;
            editButton.Click += EditButton_Click;
            deleteButton.Click += DeleteButton_Click;
            blockButton.Click += BlockButton_Click;
            multiButton.Click += MultiButton_Click;
            clearButton.Click += ClearButton_Click;
            clearHangarButton.Click += ClearHangarButton_Click;
            undoButton.Click += (s, e) => UndoLastAction();



            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(deleteButton);
            buttonPanel.Controls.Add(blockButton);
            buttonPanel.Controls.Add(multiButton);
            buttonPanel.Controls.Add(clearButton);
            buttonPanel.Controls.Add(clearHangarButton);
            buttonPanel.Controls.Add(undoButton);

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

            // initialize debounce timer (now inside SetupUI so it can call the local UpdateSizing)
            resizeTimer = new System.Windows.Forms.Timer { Interval = 120 };
            resizeTimer.Tick += (s, e) =>
            {
                resizeTimer.Stop();
                UpdateSizing(); // calls the local UpdateSizing function further down in this method
            };

            // Enable double buffering on scheduleGrid
            var pi = scheduleGrid.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi?.SetValue(scheduleGrid, true, null);

            // When attaching resize handlers, debounce:
            scrollPanel.Resize += (s, e) => { resizeTimer.Stop(); resizeTimer.Start(); };
            this.Resize += (s, e) => { resizeTimer.Stop(); resizeTimer.Start(); };

            int colCount = 1 + days;
            int rowCount = 1 + hangars.Length;
            scheduleGrid.ColumnCount = colCount;
            scheduleGrid.RowCount = rowCount;

            // NOTE: We'll set ColumnStyles/RowStyles in UpdateSizing() below,
            // but TableLayoutPanel needs the count set before adding controls.

            this.SuspendLayout();
            scheduleGrid.SuspendLayout();

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

            scheduleGrid.ResumeLayout(false);
            this.ResumeLayout(false);

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

                // also turn on double buffering for the form itself (optional)
                this.SetStyle(System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer |
                              System.Windows.Forms.ControlStyles.AllPaintingInWmPaint, true);
                this.UpdateStyles();

            }

            // initial sizing pass
            UpdateSizing();

            // Instead of: scrollPanel.Resize += (s,e) => UpdateSizing();
            scrollPanel.Resize += (s, e) => {
                resizeTimer.Stop();
                resizeTimer.Start();
            };

            // Form resize also restarts it
            this.Resize += (s, e) => {
                resizeTimer.Stop();
                resizeTimer.Start();
            };

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

                    // before saving changes: include both oldKey and newKey in the snapshot
                    var affected = new List<string> { oldKey };
                    if (newKey != oldKey) affected.Add(newKey);
                    PushUndo(affected, "Add/Edit booking");

                    // save the booking (use the 'booking' variable, not 'updated')
                    bookings[newKey] = booking;

                    // if the booking moved from oldKey to newKey, remove the old entry
                    if (newKey != oldKey && bookings.ContainsKey(oldKey))
                        bookings.Remove(oldKey);

                    // refresh UI once
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

                    // before saving changes: include both old (key) and newKey in the snapshot
                    var affected = new List<string> { key };
                    if (newKey != key) affected.Add(newKey);
                    PushUndo(affected, "Add/Edit booking");

                    bookings[newKey] = updated;
                    if (newKey != key && bookings.ContainsKey(key))
                        bookings.Remove(key);

                    RefreshSlotVisuals();
                }
                else if (dr == DialogResult.Abort)
                {
                    if (bookings.ContainsKey(key))
                    {
                        PushUndo(new[] { key }, "Remove booking"); // allow undo for removal too
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

            if (!bookings.ContainsKey(key))
            {
                MessageBox.Show("No booking exists in the selected slot.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Remove this booking?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // before removing single booking, push undo snapshot
            PushUndo(new[] { key }, $"Delete booking {key}");

            bookings.Remove(key);
            // clear selection if it pointed to the removed slot
            if (selectedSlot != null && (selectedSlot.Tag as string) == key)
                selectedSlot = null;

            RefreshSlotVisuals();
        }

        private void BlockButton_Click(object sender, EventArgs e)
        {
            // Require selection of a hangar and dates using a dialog
            using (var dlg = new BlockDialog(GetHangarNames(), days))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string hangar = dlg.SelectedHangar;
                DateTime start = dlg.SelectedStartDate;
                DateTime end = dlg.SelectedEndDate;

                if (end < start)
                {
                    MessageBox.Show("End date must be the same or after the start date.", "Invalid range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Compute keys for the date range
                var keysToBlock = new List<string>();
                for (DateTime d = start.Date; d <= end.Date; d = d.AddDays(1))
                    keysToBlock.Add(SlotKey(hangar, d));

                // Check for conflicts: booked and not already "Unavailable"
                var conflicts = keysToBlock.Where(k => bookings.ContainsKey(k) && (bookings[k].Description ?? "") != "Unavailable").ToList();

                if (conflicts.Count > 0)
                {
                    var answer = MessageBox.Show(
                        $"{conflicts.Count} slot(s) in that range are already booked. Do you want to overwrite them and mark as Unavailable?",
                        "Confirm overwrite",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes)
                        return;
                }

                // after conflict check...
                // before applying blocks
                PushUndo(keysToBlock, $"Block {hangar} {start:yyyy-MM-dd}..{end:yyyy-MM-dd}");

                // Apply the block: set booking for each slot -> Description = "Unavailable"
                foreach (var key in keysToBlock)
                {
                    var tuple = ParseSlotKey(key); // returns (hangar, date)
                    bookings[key] = new Booking
                    {
                        Hangar = tuple.hangar,
                        Date = tuple.date,
                        Description = "Unavailable"
                    };
                }

                // Refresh UI
                RefreshSlotVisuals();


                // Apply the block: set booking for each slot -> Description = "Unavailable"
                foreach (var key in keysToBlock)
                {
                    // parse hangar & date from key to create Booking
                    var tuple = ParseSlotKey(key); // returns (hangar, date)
                    var booking = new Booking
                    {
                        Hangar = tuple.hangar,
                        Date = tuple.date,
                        Description = "Unavailable"
                    };
                    bookings[key] = booking;
                }

                // Refresh UI
                RefreshSlotVisuals();
            }
        }

        private void MultiButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new MultiBookingDialog(GetHangarNames(), days))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string hangar = dlg.SelectedHangar;
                DateTime start = dlg.SelectedStartDate;
                DateTime end = dlg.SelectedEndDate;
                string desc = dlg.Description?.Trim() ?? "";

                if (string.IsNullOrEmpty(desc))
                {
                    MessageBox.Show("Please enter a description for the event.", "Missing description", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (end < start)
                {
                    MessageBox.Show("End date must be the same or after the start date.", "Invalid range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Build keys for the range and ensure they're in the visible range
                var keysToCreate = new List<string>();
                for (DateTime d = start.Date; d <= end.Date; d = d.AddDays(1))
                {
                    int offset = (d.Date - DateTime.Today).Days;
                    if (offset < 0 || offset >= days)
                    {
                        MessageBox.Show("Selected date range must be within the currently displayed days.", "Out of range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    keysToCreate.Add(SlotKey(hangar, d));
                }

                // detect conflicts (existing bookings that aren't "Unavailable" — adjust to your policy)
                var conflicts = new List<string>();
                foreach (var k in keysToCreate)
                    if (bookings.ContainsKey(k) && (bookings[k].Description ?? "") != "Unavailable")
                        conflicts.Add(k);

                if (conflicts.Count > 0)
                {
                    var answer = MessageBox.Show(
                        $"{conflicts.Count} slot(s) in that range are already booked. Overwrite them with this event?",
                        "Confirm overwrite",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes)
                        return;
                }

                // before creating/updating - capture undo snapshot for all affected keys
                PushUndo(keysToCreate, $"Multi booking {hangar} {start:yyyy-MM-dd}..{end:yyyy-MM-dd}");

                // create/update bookings for every day in the range
                foreach (var k in keysToCreate)
                {
                    var t = ParseSlotKey(k); // returns (hangar, date)
                    bookings[k] = new Booking
                    {
                        Hangar = t.hangar,
                        Date = t.date,
                        Description = desc
                    };
                }

                RefreshSlotVisuals();
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            // count current bookings for helpful confirmation
            int count = bookings.Count;

            if (count == 0)
            {
                MessageBox.Show("There are no bookings to clear.", "Clear Schedule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Ask for confirmation (shows how many bookings will be removed)
            var answer = MessageBox.Show(
                $"This will permanently remove {count} booking{(count == 1 ? "" : "s")}. Are you sure you want to clear the entire schedule?",
                "Confirm clear schedule",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes) return;

            // before clearing
            PushUndo(bookings.Keys.ToList(), "Clear entire schedule");
            bookings.Clear();
            selectedSlot = null;
            RefreshSlotVisuals();


            // Clear all bookings and refresh UI
            bookings.Clear();

            // clear selection so there's no stale selectedSlot
            selectedSlot = null;

            RefreshSlotVisuals();

            MessageBox.Show("Schedule cleared.", "Clear Schedule", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearHangarButton_Click(object sender, EventArgs e)
        {
            // Ask the user to pick which hangar to clear
            using (var dlg = new HangarSelectDialog(GetHangarNames()))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string hangar = dlg.SelectedHangar;
                if (string.IsNullOrEmpty(hangar)) return;

                // Build list of keys for the visible days for that hangar
                var keysToRemove = new List<string>();
                for (int i = 0; i < days; i++)
                {
                    DateTime d = DateTime.Today.AddDays(i);
                    string key = SlotKey(hangar, d);
                    if (bookings.ContainsKey(key))
                        keysToRemove.Add(key);
                }

                // before removing
                PushUndo(keysToRemove, $"Clear hangar {hangar}");
                foreach (var k in keysToRemove) bookings.Remove(k);
                if (selectedSlot != null && keysToRemove.Contains(selectedSlot.Tag as string)) selectedSlot = null;
                RefreshSlotVisuals();


                if (keysToRemove.Count == 0)
                {
                    MessageBox.Show($"No bookings found for {hangar} in the visible range.", "Clear Hangar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Confirm
                var resp = MessageBox.Show(
                    $"This will remove {keysToRemove.Count} booking{(keysToRemove.Count == 1 ? "" : "s")} for {hangar} in the visible date range. Continue?",
                    "Confirm clear hangar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (resp != DialogResult.Yes) return;

                // Remove them
                foreach (var k in keysToRemove)
                    bookings.Remove(k);

                // Clear selection if it was a removed slot
                if (selectedSlot != null)
                {
                    string selKey = selectedSlot.Tag as string;
                    if (selKey != null && keysToRemove.Contains(selKey))
                        selectedSlot = null;
                }

                RefreshSlotVisuals();
                MessageBox.Show($"Cleared {keysToRemove.Count} booking{(keysToRemove.Count == 1 ? "" : "s")} for {hangar}.", "Clear Hangar", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        // Simple dialog to pick hangar + start/end date among the shown days
        private class BlockDialog : Form
        {
            private ComboBox cbHangar;
            private ComboBox cbStart;
            private ComboBox cbEnd;
            private Button btnOk;
            private Button btnCancel;

            public string SelectedHangar => cbHangar.SelectedItem?.ToString();

            // cb items are "yyyy-MM-dd | Tue MMM dd" same format as BookingDialog
            public DateTime SelectedStartDate => DateTime.Parse(cbStart.SelectedItem.ToString().Split('|')[0].Trim());
            public DateTime SelectedEndDate => DateTime.Parse(cbEnd.SelectedItem.ToString().Split('|')[0].Trim());

            public BlockDialog(string[] hangars, int days)
            {
                Text = "Block Unavailable Range";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                Width = 420;
                Height = 210;

                Label lblHangar = new Label { Text = "Hangar:", Left = 10, Top = 12, Width = 60 };
                cbHangar = new ComboBox { Left = 80, Top = 8, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
                cbHangar.Items.AddRange(hangars);

                Label lblStart = new Label { Text = "Start:", Left = 10, Top = 46, Width = 60 };
                cbStart = new ComboBox { Left = 80, Top = 42, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };

                Label lblEnd = new Label { Text = "End:", Left = 10, Top = 80, Width = 60 };
                cbEnd = new ComboBox { Left = 80, Top = 76, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };

                // fill date choices
                for (int i = 0; i < days; i++)
                {
                    DateTime d = DateTime.Today.AddDays(i);
                    string item = $"{d:yyyy-MM-dd} | {d:ddd MMM dd}";
                    cbStart.Items.Add(item);
                    cbEnd.Items.Add(item);
                }

                // default selections
                if (cbHangar.Items.Count > 0) cbHangar.SelectedIndex = 0;
                if (cbStart.Items.Count > 0) cbStart.SelectedIndex = 0;
                if (cbEnd.Items.Count > 0) cbEnd.SelectedIndex = 0;

                btnOk = new Button { Text = "OK", Left = 230, Width = 70, Top = 120, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 310, Width = 70, Top = 120, DialogResult = DialogResult.Cancel };

                Controls.Add(lblHangar);
                Controls.Add(cbHangar);
                Controls.Add(lblStart);
                Controls.Add(cbStart);
                Controls.Add(lblEnd);
                Controls.Add(cbEnd);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }

        private class MultiBookingDialog : Form
        {
            private ComboBox cbHangar;
            private ComboBox cbStart;
            private ComboBox cbEnd;
            private TextBox txtDesc;
            private Button btnOk;
            private Button btnCancel;

            public string SelectedHangar => cbHangar.SelectedItem?.ToString();
            public DateTime SelectedStartDate => DateTime.Parse(cbStart.SelectedItem.ToString().Split('|')[0].Trim());
            public DateTime SelectedEndDate => DateTime.Parse(cbEnd.SelectedItem.ToString().Split('|')[0].Trim());
            public string Description { get => txtDesc.Text; set => txtDesc.Text = value; }

            public MultiBookingDialog(string[] hangars, int days)
            {
                Text = "Multi-day Booking";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                Width = 460;
                Height = 260;

                Label lblHangar = new Label { Text = "Hangar:", Left = 10, Top = 12, Width = 60 };
                cbHangar = new ComboBox { Left = 80, Top = 8, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
                cbHangar.Items.AddRange(hangars);

                Label lblStart = new Label { Text = "Start:", Left = 10, Top = 46, Width = 60 };
                cbStart = new ComboBox { Left = 80, Top = 42, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };

                Label lblEnd = new Label { Text = "End:", Left = 10, Top = 82, Width = 60 };
                cbEnd = new ComboBox { Left = 80, Top = 78, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };

                Label lblDesc = new Label { Text = "Description:", Left = 10, Top = 118, Width = 80 };
                txtDesc = new TextBox { Left = 10, Top = 142, Width = 430, Height = 36, Multiline = true };

                btnOk = new Button { Text = "OK", Left = 270, Width = 80, Top = 188, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 360, Width = 80, Top = 188, DialogResult = DialogResult.Cancel };

                // populate date combos with visible days only
                for (int i = 0; i < days; i++)
                {
                    DateTime d = DateTime.Today.AddDays(i);
                    string item = $"{d:yyyy-MM-dd} | {d:ddd MMM dd}";
                    cbStart.Items.Add(item);
                    cbEnd.Items.Add(item);
                }

                if (cbHangar.Items.Count > 0) cbHangar.SelectedIndex = 0;
                if (cbStart.Items.Count > 0) cbStart.SelectedIndex = 0;
                if (cbEnd.Items.Count > 0) cbEnd.SelectedIndex = cbEnd.Items.Count - 1; // default end = last day

                Controls.Add(lblHangar);
                Controls.Add(cbHangar);
                Controls.Add(lblStart);
                Controls.Add(cbStart);
                Controls.Add(lblEnd);
                Controls.Add(cbEnd);
                Controls.Add(lblDesc);
                Controls.Add(txtDesc);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }

        // Simple modal dialog to pick one hangar from the current list
        private class HangarSelectDialog : Form
        {
            private ComboBox cbHangar;
            private Button btnOk;
            private Button btnCancel;

            public string SelectedHangar => cbHangar.SelectedItem?.ToString();

            public HangarSelectDialog(string[] hangars)
            {
                Text = "Select Hangar to Clear";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                Width = 360;
                Height = 140;

                Label lbl = new Label { Text = "Hangar:", Left = 10, Top = 14, Width = 60 };
                cbHangar = new ComboBox { Left = 80, Top = 10, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
                cbHangar.Items.AddRange(hangars);

                btnOk = new Button { Text = "OK", Left = 160, Width = 80, Top = 50, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 250, Width = 80, Top = 50, DialogResult = DialogResult.Cancel };

                Controls.Add(lbl);
                Controls.Add(cbHangar);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                if (cbHangar.Items.Count > 0)
                    cbHangar.SelectedIndex = 0;
            }
        }



    }
}
