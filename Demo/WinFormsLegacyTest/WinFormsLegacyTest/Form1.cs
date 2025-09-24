using System; // Basic system namespace
using System.Collections.Generic; // Data structures like dictionary
using System.Drawing; // For colors, fonts, and graphics
using System.Windows.Forms; // WinForms namespace

namespace WinFormsLegacyTest
{
    public partial class Form1 : Form
    {
        // state
        private readonly string[] hangars = { "Hangar A", "Hangar B", "Hangar C", "Hangar D" };             // Predefined hangars
        private readonly int days = 7;                                                                      // Show 7 days from today
        private readonly Dictionary<string, Booking> bookings = new Dictionary<string, Booking>();          // Key(hangar/date) -> booking
        private readonly Dictionary<string, Button> slotButtons = new Dictionary<string, Button>();         // Key(hangar/date) -> button

        public Form1()
        {
            InitializeComponent();                                                                          // Keeps the form alive
            SetupUI();                                                                                      // Custom UI setup
        }

        // Booking data container
        private class Booking
        {
            // Note: public ... { get; set; } is shorthand for a property with a getter and setter.
            public string Hangar { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
        }

        private void SetupUI()
        {
            // --- Main layout ---
            TableLayoutPanel mainLayout = new TableLayoutPanel                                          // Create main layout (rows, columns)
            {
                Dock = DockStyle.Fill,                                                                  // Fills the form
                ColumnCount = 1,
                RowCount = 2
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));                              // Top row for buttons                    
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));                              // Grid(Schedule) fills remaining space
            this.Controls.Add(mainLayout);

            // --- Button row ---
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            Button addButton = new Button { Text = "Add", Width = 75 };                                
            Button editButton = new Button { Text = "Edit", Width = 75 };                              
            Button deleteButton = new Button { Text = "Delete", Width = 75 };

            // Hooks the button’s Click event to a function (AddButton_Click, etc.)
            addButton.Click += AddButton_Click;
            editButton.Click += EditButton_Click;
            deleteButton.Click += DeleteButton_Click;

            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(deleteButton);

            mainLayout.Controls.Add(buttonPanel, 0, 0);

            // --- Schedule grid ---
            TableLayoutPanel scheduleGrid = new TableLayoutPanel                                        // Create the grid for schedule (hangar x days)
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                AutoSize = false                                                                        // Means it will stretch instead of shrinking to fit.
            };

            scheduleGrid.ColumnCount = 1 + days;
            scheduleGrid.RowCount = 1 + hangars.Length;

            scheduleGrid.ColumnStyles.Clear();
            scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));                     // First column fixed width for hangar names
            for (int i = 0; i < days; i++)
                scheduleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / days));          // Share horizontal space equally

            scheduleGrid.RowStyles.Clear();
            scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));                            // Header (date)
            for (int r = 0; r < hangars.Length; r++)
                scheduleGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / hangars.Length));      // Share vertical space equally

            // Header label
            Label headerHangar = new Label
            {
                Text = "Hangar/Date",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            scheduleGrid.Controls.Add(headerHangar, 0, 0);

            for (int d = 0; d < days; d++)
            {
                Label dateLabel = new Label
                {
                    Text = DateTime.Today.AddDays(d).ToString("MM/dd"),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Margin = new Padding(0)
                };
                scheduleGrid.Controls.Add(dateLabel, d + 1, 0);
            }

            // Body: hangar name + slots
            for (int r = 0; r < hangars.Length; r++)
            {
                Label hangarLabel = new Label
                {
                    Text = hangars[r],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0)
                };
                scheduleGrid.Controls.Add(hangarLabel, 0, r + 1);

                for (int c = 0; c < days; c++)
                {
                    DateTime date = DateTime.Today.AddDays(c);
                    string key = SlotKey(hangars[r], date);

                    Button slot = new Button // Creates buttons in each hangar x date
                    {
                        Dock = DockStyle.Fill,
                        Text = "",
                        Tag = key,
                        Margin = new Padding(3),
                        UseVisualStyleBackColor = false // Required to apply BackColor
                    };

                    // *Click* -> calls Slot_Click
                    slot.Click += Slot_Click;

                    scheduleGrid.Controls.Add(slot, c + 1, r + 1);
                    slotButtons[key] = slot;
                }
            }

            mainLayout.Controls.Add(scheduleGrid, 0, 1);

            // Initialize visuals
            RefreshSlotVisuals();
        }

        // Generate the dictionary key used for mapping a hangar + date to things
        private string SlotKey(string hangar, DateTime date)
        {
            return $"{hangar}-{date:yyyy-MM-dd}";
        }

        // Repaint all slots based on bookings dictionary
        private void RefreshSlotVisuals()
        {
            foreach (var kv in slotButtons)
            {
                string key = kv.Key;
                Button b = kv.Value;

                if (bookings.ContainsKey(key))
                {
                    // Booked -> red background, show short text
                    b.BackColor = Color.LightCoral;
                    string desc = bookings[key].Description ?? "";
                    b.Text = Truncate(desc, 30); // Keep cell text compact
                }
                else
                {
                    // Available -> green
                    b.BackColor = Color.LightGreen;
                    b.Text = "";
                }
            }
        }

        // Helper to truncate long descriptions
        private string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return (s.Length <= max) ? s : s.Substring(0, max - 1) + "…";
        }

        // Add button handler: opens dialog to create a booking
        private void AddButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new BookingDialog(hangars, days))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var booking = new Booking
                    {
                        Hangar = dlg.SelectedHangar,
                        Date = dlg.SelectedDate,
                        Description = dlg.Description
                    };
                    string key = SlotKey(booking.Hangar, booking.Date);
                    bookings[key] = booking;
                    RefreshSlotVisuals();
                }
            }
        }

        // Edit button handler: asks user to click a slot they want to edit, then opens dialog
        private void EditButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Click the slot you want to edit (or add) and the editor will open.", "Edit Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // Next slot click will open the editor; Slot_Click handles prefilled editing
            // (We don't need extra state—Slot_Click always opens an editor for that slot.)
        }

        // Delete button: instructs to click slot to remove booking
        private void DeleteButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Click the slot you want to delete booking from. In the editor press 'Remove' to delete.", "Delete Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Clicking a slot opens the dialog pre-filled for that hangar/date (edit or create)
        private void Slot_Click(object sender, EventArgs e)
        {
            var b = (Button)sender;
            string key = (string)b.Tag;
            // Parse key back to hangar + date
            int idx = key.LastIndexOf('-');
            // Safe parse: hangar is prefix until last '-' and date is yyyy-MM-dd
            // but hangar names may contain '-', so better split from right
            int lastDash = key.LastIndexOf('-');
            // Date is last 10 chars (yyyy-MM-dd)
            string datePart = key.Substring(key.Length - 10);
            string hangarPart = key.Substring(0, key.Length - 11); // remove '-' + 10 chars
            if (!DateTime.TryParse(datePart, out DateTime date))
            {
                MessageBox.Show("Invalid slot key date parsing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Create dialog pre-filled
            using (var dlg = new BookingDialog(hangars, days, preselectedHangar: hangarPart, preselectedDate: date))
            {
                // If there is already a booking, show its description in the dialog
                if (bookings.ContainsKey(key))
                    dlg.Description = bookings[key].Description;

                var dr = dlg.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    // save or update booking
                    var updated = new Booking
                    {
                        Hangar = dlg.SelectedHangar,
                        Date = dlg.SelectedDate,
                        Description = dlg.Description
                    };
                    string newKey = SlotKey(updated.Hangar, updated.Date);
                    bookings[newKey] = updated;

                    // If user changed hangar/date, remove old booking if key changed
                    if (newKey != key && bookings.ContainsKey(key))
                        bookings.Remove(key);

                    RefreshSlotVisuals();
                }
                // Abort used by dialog to signal removal
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

        // --- Simple modal dialog used for creating/editing bookings ---
        // Dialog returns DialogResult.OK on save, DialogResult.Cancel on cancel, and DialogResult.Abort on Remove.
        private class BookingDialog : Form
        {
            private ComboBox cbHangar;
            private ComboBox cbDate;

            private TextBox txtDesc;

            private Button btnOk;
            private Button btnCancel;
            private Button btnRemove;

            // Properties for reading user input when the dialog closes
            public string SelectedHangar => cbHangar.SelectedItem?.ToString();
            public DateTime SelectedDate => DateTime.Parse(cbDate.SelectedItem.ToString().Split('|')[0].Trim());
            public string Description { get => txtDesc.Text; set => txtDesc.Text = value; }

            public BookingDialog(string[] hangars, int days, string preselectedHangar = null, DateTime? preselectedDate = null)
            {
                // build UI
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
                    // store date in the item string as "yyyy-MM-dd | Tue 23" so we can parse it back
                    cbDate.Items.Add($"{d:yyyy-MM-dd} | {d:ddd MMM dd}");
                }

                Label lblDesc = new Label { Text = "Description:", Left = 10, Top = 82, Width = 80 };
                txtDesc = new TextBox { Left = 10, Top = 105, Width = 370, Height = 30 };

                btnOk = new Button { Text = "OK", Left = 220, Width = 70, Top = 140, DialogResult = DialogResult.OK };
                btnCancel = new Button { Text = "Cancel", Left = 300, Width = 70, Top = 140, DialogResult = DialogResult.Cancel };
                btnRemove = new Button { Text = "Remove", Left = 140, Width = 70, Top = 140 };

                // If "Remove" is clicked → closes with Abort status, which Slot_Click interprets as "delete this booking"
                btnRemove.Click += (s, e) => { this.DialogResult = DialogResult.Abort; this.Close(); };

                Controls.Add(lblHangar);
                Controls.Add(cbHangar);
                Controls.Add(lblDate);
                Controls.Add(cbDate);
                Controls.Add(lblDesc);
                Controls.Add(txtDesc);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);
                Controls.Add(btnRemove);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                // Preselect
                //If you clicked an already-booked slot, the dialog would open empty every time. This way:
                //Add button → empty dialog for a new booking.
                //Click existing slot → dialog is pre - filled with that booking so you can edit or remove it.

                if (!string.IsNullOrEmpty(preselectedHangar))
                {
                    cbHangar.SelectedItem = preselectedHangar;
                }
                else if (cbHangar.Items.Count > 0)
                {
                    cbHangar.SelectedIndex = 0;
                }

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
    }
}
