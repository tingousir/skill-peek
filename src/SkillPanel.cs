using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SkillPeek
{
    internal sealed class SkillPanel : Panel
    {
        private static readonly string[] PageNames = { "工作", "规划", "写作" };
        private static readonly Color Ink = Color.FromArgb(25, 39, 33);
        private static readonly Color Muted = Color.FromArgb(77, 95, 86);
        private static readonly Color Green = Color.FromArgb(28, 103, 72);
        private static readonly Color Line = Color.FromArgb(213, 222, 215);
        private static readonly Color Canvas = Color.FromArgb(245, 247, 243);
        private static readonly Color SelectedSurface = Color.FromArgb(232, 243, 235);
        private static readonly Color HoverSurface = Color.FromArgb(240, 246, 240);
        private static readonly Color UnavailableSurface = Color.FromArgb(240, 243, 239);
        private const int Inset = 24;
        private const int CardHeight = 116;
        private const int Gap = 8;
        private const TextFormatFlags SingleLine =
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        private readonly float scale;
        private readonly Action<string> pick;
        private readonly Panel grid;
        private readonly Tile[] tabs;
        private readonly Tile[] cards;
        private readonly Tile refresh;
        private readonly Tile hold;
        private readonly Tile once;
        private readonly Tile close;
        private readonly Font body = new Font("Segoe UI", 9f);
        private readonly Font heading = new Font("Segoe UI", 16f, FontStyle.Bold);
        private readonly Font purpose = new Font("Segoe UI", 10f, FontStyle.Bold);
        private readonly Font mono = new Font("Consolas", 9.25f);
        private readonly Skill[][] pages;
        private readonly int maxCards;
        private HashSet<string> installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int page;
        private int selected;
        private int columns;
        private int footerTop;
        private long lastWheel;
        private Point lastPointer;
        private string notice;

        public SkillPanel(float scale, Action<string> pick, Action refreshSkills, Action<string> changeMode, Action closePanel)
        {
            this.scale = scale;
            this.pick = pick;
            DoubleBuffered = true;
            BackColor = Canvas;
            AccessibleName = "技能面板";

            var catalog = new List<Skill>();
            using (Stream stream = typeof(SkillPanel).Assembly.GetManifestResourceStream("SkillPeek.skills.tsv"))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    catalog.Add(new Skill(fields[0], fields[1], fields[2], fields[3], ColorTranslator.FromHtml(fields[4])));
                }
            }
            pages = PageNames.Select(name => catalog.Where(skill => skill.Page == name).ToArray()).ToArray();
            maxCards = pages.Max(items => items.Length);

            grid = new Panel { AutoScroll = true, BackColor = Canvas, AccessibleName = "技能", TabStop = false };
            grid.MouseWheel += OnPageWheel;
            Controls.Add(grid);
            cards = new Tile[maxCards];
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                Tile card = new Tile(this);
                card.Click += delegate { SelectCard(index, false); Pick(); };
                card.MouseMove += delegate
                {
                    Point pointer = Cursor.Position;
                    if (pointer == lastPointer) { return; }
                    lastPointer = pointer;
                    if (selected != index) { SelectCard(index, false); }
                };
                card.MouseWheel += OnPageWheel;
                cards[i] = card;
                grid.Controls.Add(card);
            }

            tabs = new Tile[PageNames.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i] = AddButton(PageNames[i], delegate { ChangePage(index); });
            }
            refresh = AddButton("↻", refreshSkills);
            refresh.AccessibleName = "重新扫描本地技能";
            hold = AddButton("按住", delegate { changeMode("hold"); });
            once = AddButton("一次", delegate { changeMode("once"); });
            close = AddButton("Esc", closePanel);
            close.AccessibleName = "收起面板";
            UpdateCards();
        }

        private Tile AddButton(string text, Action action)
        {
            var button = new Tile(this) { Text = text };
            button.Click += delegate { action(); };
            button.MouseWheel += OnPageWheel;
            Controls.Add(button);
            return button;
        }

        private int S(int size) { return (int)Math.Round(size * scale); }
        private int ColumnCount(int width) { return Math.Max(2, Math.Min(8, (width + S(Gap)) / S(134))); }
        private int GridHeight(int count, int cols) { return ((count + cols - 1) / cols) * S(CardHeight + Gap) - S(Gap); }
        private int HeaderHeight(int width) { return width >= S(672) ? S(76) : S(119); }
        public int PreferredPanelWidth { get { return S(1192); } }

        public int PreferredPanelHeight(int width, int limit)
        {
            return Math.Min(limit, HeaderHeight(width) + GridHeight(maxCards, ColumnCount(width - 2 * S(Inset))) + S(80));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (close == null || ClientSize.Width <= 0) { return; }
            int width = ClientSize.Width;
            int left = S(Inset);
            int right = width - left;
            int buttonHeight = S(36);
            int headerY = S(20);
            close.SetBounds(right - S(44), headerY, S(44), buttonHeight);
            once.SetBounds(close.Left - S(58), headerY, S(48), buttonHeight);
            hold.SetBounds(once.Left - S(51), headerY, S(48), buttonHeight);
            refresh.SetBounds(hold.Left - S(47), headerY, S(36), buttonHeight);
            bool wide = width >= S(672);
            int tabLeft = wide ? S(139) : left;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].SetBounds(tabLeft + i * S(104), wide ? headerY : S(63), S(98), buttonHeight);
            }

            footerTop = Math.Max(HeaderHeight(width), ClientSize.Height - S(64));
            int gridWidth = Math.Max(1, right - left);
            int gridHeight = Math.Max(1, footerTop - S(16) - HeaderHeight(width));
            grid.SetBounds(left, HeaderHeight(width), gridWidth, gridHeight);
            LayoutCards();
        }

        private void LayoutCards()
        {
            int width = grid.Width;
            columns = ColumnCount(width);
            if (GridHeight(pages[page].Length, columns) > grid.Height)
            {
                width -= SystemInformation.VerticalScrollBarWidth;
                columns = ColumnCount(width);
            }
            int gap = S(Gap);
            Point scroll = grid.AutoScrollPosition;
            for (int i = 0; i < pages[page].Length; i++)
            {
                int column = i % columns;
                int left = column * (width + gap) / columns;
                int right = (column + 1) * (width + gap) / columns - gap;
                cards[i].SetBounds(scroll.X + left, scroll.Y + i / columns * S(CardHeight + Gap),
                    Math.Max(1, right - left), S(CardHeight));
            }
        }

        public void SetInstalled(HashSet<string> names)
        {
            installed = names;
            UpdateCards();
        }

        public void SetMode(string mode)
        {
            hold.Active = mode == "hold";
            once.Active = !hold.Active;
            hold.Invalidate();
            once.Invalidate();
        }

        public void Reset()
        {
            page = 0;
            selected = 0;
            notice = null;
            lastWheel = 0;
            lastPointer = Cursor.Position;
            grid.AutoScrollPosition = Point.Empty;
            UpdateCards();
        }

        public void FocusSelection() { cards[selected].Focus(); }

        private void ChangePage(int index)
        {
            page = (index + pages.Length) % pages.Length;
            selected = 0;
            notice = null;
            lastPointer = Cursor.Position;
            grid.AutoScrollPosition = Point.Empty;
            UpdateCards();
            FocusSelection();
        }

        private bool IsInstalled(Skill skill) { return installed.Count == 0 || installed.Contains(skill.Name); }

        private void UpdateCards()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Counter = pages[i].Count(IsInstalled) + "/" + pages[i].Length;
                tabs[i].AccessibleName = PageNames[i] + "，已安装 " + tabs[i].Counter;
                tabs[i].Active = page == i;
                tabs[i].Invalidate();
            }
            Skill[] current = pages[page];
            grid.SuspendLayout();
            for (int i = 0; i < cards.Length; i++)
            {
                Tile card = cards[i];
                if (i >= current.Length) { card.Visible = false; continue; }
                card.Skill = current[i];
                card.Available = IsInstalled(current[i]);
                card.Text = current[i].Name;
                card.AccessibleDescription = current[i].Category + "，" + current[i].Description +
                    (card.Available ? "" : "，未安装");
                card.Cursor = card.Available ? Cursors.Hand : Cursors.Default;
                card.Active = i == selected;
                card.Visible = true;
                card.Invalidate();
            }
            LayoutCards();
            grid.ResumeLayout();
            Invalidate();
        }

        private void SelectCard(int index, bool focus)
        {
            cards[selected].Active = false;
            cards[selected].Invalidate();
            selected = index;
            cards[selected].Active = true;
            cards[selected].Invalidate();
            notice = null;
            if (focus)
            {
                lastPointer = Cursor.Position;
                FocusSelection();
            }
            Invalidate();
        }

        public void ShowNotice(string text)
        {
            notice = text;
            Invalidate();
        }

        private void Pick()
        {
            Skill skill = pages[page][selected];
            if (IsInstalled(skill)) { pick(skill.Name); }
            else { ShowNotice("未安装 · 装好后点右上角 ↻ 刷新"); }
        }

        private void OnPageWheel(object sender, MouseEventArgs e)
        {
            var handled = e as HandledMouseEventArgs;
            if (handled != null) { handled.Handled = true; }
            long now = DateTime.UtcNow.Ticks;
            if (e.Delta == 0 || now - lastWheel < TimeSpan.TicksPerMillisecond * 230) { return; }
            lastWheel = now;
            ChangePage(page + (e.Delta < 0 ? 1 : -1));
        }

        public bool HandleKey(Keys key)
        {
            int last = pages[page].Length - 1;
            int index = selected;
            switch (key)
            {
                case Keys.Escape: close.PerformClick(); return true;
                case Keys.Tab: ChangePage(page + 1); return true;
                case Keys.Shift | Keys.Tab: ChangePage(page - 1); return true;
                case Keys.Enter: Pick(); return true;
                case Keys.Home: index = 0; break;
                case Keys.End: index = last; break;
                case Keys.Left: if (selected % columns > 0) { index--; } break;
                case Keys.Right: if (selected % columns < columns - 1 && selected < last) { index++; } break;
                case Keys.Up: if (selected >= columns) { index -= columns; } break;
                case Keys.Down: if (selected / columns < last / columns) { index = Math.Min(last, selected + columns); } break;
                default: return false;
            }
            SelectCard(index, true);
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (close == null) { return; }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var accent = new SolidBrush(Green))
            {
                g.FillEllipse(accent, S(Inset), S(28), S(20), S(20));
            }
            TextRenderer.DrawText(g, "↗", body, new Rectangle(S(Inset), S(27), S(20), S(20)), Color.White,
                SingleLine | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "Skills", heading, new Rectangle(S(52), S(18), S(80), S(40)), Ink, SingleLine);

            int width = Math.Max(1, ClientSize.Width - 2 * S(Inset));
            var footer = new Rectangle(S(Inset), footerTop, width, S(48));
            using (var divider = new Pen(Line))
            {
                g.DrawLine(divider, footer.Left, footer.Top, footer.Right, footer.Top);
            }
            Skill skill = pages[page][selected];
            int helpWidth = ClientSize.Width >= S(710) ? S(312) : 0;
            var detail = new Rectangle(footer.Left + S(12), footer.Top + S(4), Math.Max(1, width - S(24) - helpWidth), S(20));
            TextRenderer.DrawText(g, skill.Name, mono, detail, Ink, SingleLine);
            detail.Y += S(19);
            TextRenderer.DrawText(g, notice ?? (IsInstalled(skill) ? skill.Description : "未安装 · 装好后点右上角 ↻ 刷新"),
                body, detail, Muted, SingleLine);
            if (helpWidth > 0)
            {
                var help = new Rectangle(footer.Right - helpWidth - S(12), footer.Top, helpWidth, footer.Height);
                TextRenderer.DrawText(g, "方向键 选择    Enter 插入    Tab / 滚轮 翻页", body, help, Muted,
                    SingleLine | TextFormatFlags.Right);
            }
        }

        private static void DrawSurface(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
                if (d <= 1) { return; }
                path.AddArc(r.Left, r.Top, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) { return; }
            body.Dispose();
            heading.Dispose();
            purpose.Dispose();
            mono.Dispose();
        }

        private sealed class Skill
        {
            public readonly string Page, Name, Category, Description;
            public readonly Color Color;

            public Skill(string page, string name, string category, string description, Color color)
            {
                Page = page;
                Name = name;
                Category = category;
                Description = description;
                Color = color;
            }
        }

        private sealed class Tile : Button
        {
            private readonly SkillPanel panel;
            private bool hover;
            public Skill Skill;
            public string Counter;
            public bool Active;
            public bool Available;

            public Tile(SkillPanel panel)
            {
                this.panel = panel;
                SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
                FlatStyle = FlatStyle.Flat;
                Cursor = Cursors.Hand;
                BackColor = Canvas;
                UseVisualStyleBackColor = false;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hover = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                // Paint the whole buffer before the rounded surface, including its four outer corners.
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool disabled = Skill != null && !Available;
                Color fill = Active ? SelectedSurface : disabled ? UnavailableSurface : hover ? HoverSurface : Color.White;
                Color border = Active || (Focused && ShowFocusCues && Skill == null) ? Green : Line;
                var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
                DrawSurface(g, bounds, panel.S(8), fill, border);
                if (Skill == null)
                {
                    if (Counter == null)
                    {
                        TextRenderer.DrawText(g, Text, panel.body, bounds, Active ? Green : Ink,
                            SingleLine | TextFormatFlags.HorizontalCenter);
                    }
                    else
                    {
                        var row = Rectangle.Inflate(bounds, -panel.S(10), 0);
                        TextRenderer.DrawText(g, Text, panel.body, row, Active ? Green : Ink, SingleLine);
                        TextRenderer.DrawText(g, Counter, panel.mono, row, Active ? Green : Muted,
                            SingleLine | TextFormatFlags.Right);
                    }
                    return;
                }

                int inset = panel.S(12);
                int textWidth = Math.Max(1, Width - 2 * inset);
                int marker = panel.S(5);
                using (var accent = new SolidBrush(disabled ? Muted : Skill.Color))
                {
                    g.FillEllipse(accent, Width - inset - marker, panel.S(16), marker, marker);
                }
                var metadata = new Rectangle(inset, panel.S(10), Math.Max(1, textWidth - panel.S(12)), panel.S(17));
                TextRenderer.DrawText(g, Skill.Category, panel.body, metadata, Muted, SingleLine);
                if (disabled)
                {
                    TextRenderer.DrawText(g, "未安装", panel.body, metadata, Muted, SingleLine | TextFormatFlags.Right);
                }
                DrawPurpose(g, inset, textWidth, disabled ? Muted : Ink);
                DrawName(g, inset, textWidth);
            }

            private void DrawPurpose(Graphics g, int left, int width, Color color)
            {
                string description = Skill.Description;
                int end = description.Length - 1;
                const TextFormatFlags measure = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                var measureSize = new Size(4096, panel.S(38));
                // Keep the last two Chinese characters together when natural wrapping would leave one alone.
                if (end >= 2 && IsHan(description[end]) && IsHan(description[end - 1]) &&
                    TextRenderer.MeasureText(g, description, panel.purpose, measureSize, measure).Width > width &&
                    TextRenderer.MeasureText(g, description.Substring(0, end), panel.purpose, measureSize, measure).Width <= width)
                {
                    description = description.Insert(end - 1, "\n");
                }
                TextRenderer.DrawText(g, description, panel.purpose,
                    new Rectangle(left, panel.S(32), width, panel.S(38)), color,
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            private static bool IsHan(char c) { return c >= '\u4E00' && c <= '\u9FFF'; }

            private void DrawName(Graphics g, int left, int width)
            {
                const string sample = "0000000000";
                Size unrestricted = new Size(4096, panel.S(32));
                int sampleWidth = TextRenderer.MeasureText(g, sample, panel.mono, unrestricted,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                int capacity = Math.Max(1, width * sample.Length / Math.Max(1, sampleWidth));
                string remaining = Skill.Name;
                int lineHeight = panel.S(13);
                int y = panel.S(72);
                for (int line = 0; line < 3 && remaining.Length > 0; line++)
                {
                    if (remaining.Length <= capacity || line == 2)
                    {
                        TextRenderer.DrawText(g, remaining, panel.mono, new Rectangle(left, y, width, lineHeight), Muted, SingleLine);
                        break;
                    }
                    int split = remaining.LastIndexOf('-', capacity - 1) + 1;
                    if (split == 0 || remaining.Length - split > capacity * (2 - line)) { split = capacity; }
                    TextRenderer.DrawText(g, remaining.Substring(0, split), panel.mono,
                        new Rectangle(left, y, width, lineHeight), Muted, SingleLine);
                    remaining = remaining.Substring(split);
                    y += lineHeight;
                }
            }
        }
    }
}
