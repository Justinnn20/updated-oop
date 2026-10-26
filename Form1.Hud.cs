using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// In-match HUD: health bars, age tags, round wins, Chrono-Shadow meters, combo text, and announcements.
    /// </summary>
    public partial class Form1 : Form
    {
        private void DrawModernHUD(Graphics g)
        {
            int w = this.ClientSize.Width;
            int barW = 380, barH = 26, topY = 32;

            DrawFighterPanel(g, 1, 50, barW, barH, topY);
            DrawFighterPanel(g, 2, w - 50 - barW, barW, barH, topY);

            // Round timer
            string t = roundTimer.ToString();
            using (Font tf = new Font("Impact", 30, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(t, tf);
                g.DrawString(t, tf, Brushes.Yellow, (w / 2f) - (sz.Width / 2f), topY - 8);
            }

            // Round number and round wins (two pips each, filled once a round is won)
            using (Font rf = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                string roundLabel = RoundLabel();
                SizeF rs = g.MeasureString(roundLabel, rf);
                g.DrawString(roundLabel, rf, Brushes.Silver, (w / 2f) - (rs.Width / 2f), topY + 48);
            }
            for (int i = 0; i < GameConfig.RoundsToWin; i++)
            {
                DrawWinPip(g, (w / 2f) - 30 - (i * 18), topY + 68, i < p1RoundWins, Color.Cyan);
                DrawWinPip(g, (w / 2f) + 16 + (i * 18), topY + 68, i < p2RoundWins, Color.OrangeRed);
            }

            if (p1ComboHits > 1)
            {
                using (Font cf = new Font("Impact", 28, FontStyle.Italic))
                {
                    g.DrawString($"{p1ComboHits} HITS COMBO!", cf, Brushes.Red, 60, 130);
                    g.DrawString($"{p1ComboHits} HITS COMBO!", cf, Brushes.Gold, 58, 128);
                }
            }
            if (p2ComboHits > 1)
            {
                using (Font cf = new Font("Impact", 28, FontStyle.Italic))
                {
                    string comboText = $"{p2ComboHits} HITS COMBO!";
                    float comboX = w - 50 - g.MeasureString(comboText, cf).Width;   // right edge lines up with the health bar
                    g.DrawString(comboText, cf, Brushes.Red, comboX + 2, 130);
                    g.DrawString(comboText, cf, Brushes.Gold, comboX, 128);
                }
            }

            DrawHeroSkillBar(g, 1, true);
            DrawHeroSkillBar(g, 2, false);

            // Controls help: two centered lines at the very bottom
            using (Font gf = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                int hh = this.ClientSize.Height;
                string line1 = isVsCPU
                    ? "[A/D] Move (2x = Dash)   [W] Jump   [S] Duck   [Q] Attack   [E] Kick   [R] Skill   [T] Block"
                    : "P1: [A/D/W/S] Move  [Q] Attack  [E] Kick  [R] Skill  [T] Block     |     P2: [J/L/I/K] Move  [U] Attack  [O] Kick  [P] Skill  [Y] Block";
                string line2 = isVsCPU
                    ? "Skill:  [R] = 1st    [S+R] = 2nd    [T+R] = ULTIMATE (needs full HEAT)     |     [R] in the middle of an attack = Chrono-Shadow"
                    : "Skill:  [R / P] = 1st    [S+R / K+P] = 2nd    [T+R / Y+P] = ULTIMATE (full HEAT)     |     Skill key in the middle of an attack = Chrono-Shadow";
                DrawTextCentered(g, line1, gf, Brushes.Silver, w, hh - 50);
                DrawTextCentered(g, line2, gf, Brushes.Silver, w, hh - 34);
            }
        }

        // Names of the three hero skills of each kit (see HeroKitByRoster in Form1.Battle.cs)
        private static readonly string[][] HeroSkillNames =
        {
            new[] { "Fireball", "Electric Fist", "Ultimate Strike" },     // kit 0: Kazama style
            new[] { "Devil Laser", "Shadow Step", "Laser Explosion" },    // kit 1: Mishima style (teleport)
            new[] { "Flame Fireball", "Flame Barrier", "Phoenix Blast" }, // kit 2: Phoenix style
            new[] { "Kick Beam", "Hawk Kick", "Kick Flurry" }             // kit 3: Hworang style
        };

        // Three small boxes above the controls help: how to press the skill, its name and a cooldown cover.
        // Each row lines up with the edge of its health bar (player 1 on the left, player 2 on the right).
        private void DrawHeroSkillBar(Graphics g, int who, bool left)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            const int boxW = 122, boxH = 40, gap = 6;
            int total = (boxW * 3) + (gap * 2);
            int startX = left ? 44 : w - 44 - total;
            int y = h - 102;
            int kit = HeroKit(who);
            string[] combos = left ? new[] { "R", "S + R", "T + R" } : new[] { "P", "K + P", "Y + P" };
            bool showKeys = left || !isVsCPU;

            using (Font keyFont = new Font("Impact", 9))
            using (Font nameFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    Rectangle r = new Rectangle(startX + (slot * (boxW + gap)), y, boxW, boxH);
                    int cd = heroCd[who, slot];
                    int max = Math.Max(1, heroCdMax[who, slot]);
                    bool isUltimate = slot == 2;
                    int heat = HeatOf(who);
                    bool ready = isUltimate ? heat >= MaxHeat : cd <= 0;   // the ultimate waits for a full HEAT bar
                    Color accent = ready ? (left ? Color.Cyan : Color.OrangeRed) : Color.Gray;

                    using (SolidBrush back = new SolidBrush(Color.FromArgb(ready ? 190 : 150, 15, 15, 25))) g.FillRectangle(back, r);
                    if (!ready && !isUltimate)
                    {
                        // Dark overlay that shrinks as the skill recharges
                        int coverW = (int)(boxW * ((float)cd / max));
                        using (SolidBrush cover = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(cover, r.X, r.Y, coverW, boxH);
                    }
                    using (Pen edge = new Pen(accent, ready ? 2 : 1)) g.DrawRectangle(edge, r);

                    if (isUltimate)
                    {
                        // HEAT bar along the bottom of the ultimate box
                        int fillW = (int)((boxW - 4) * (heat / (float)MaxHeat));
                        using (SolidBrush heatBrush = new SolidBrush(ready ? Color.Gold : Color.OrangeRed)) g.FillRectangle(heatBrush, r.X + 2, r.Bottom - 6, fillW, 4);
                    }

                    Brush nameBrush = ready ? Brushes.White : Brushes.Gray;
                    if (showKeys)
                    {
                        g.DrawString(combos[slot], keyFont, ready ? Brushes.Gold : Brushes.DimGray, new RectangleF(r.X, r.Y + 2, r.Width, 16), center);
                        g.DrawString(HeroSkillNames[kit][slot], nameFont, nameBrush, new RectangleF(r.X, r.Y + 18, r.Width, 20), center);
                    }
                    else
                    {
                        g.DrawString(HeroSkillNames[kit][slot], nameFont, nameBrush, r, center);   // CPU: no keys to show
                    }
                }
            }
        }

        private static void DrawWinPip(Graphics g, float x, float y, bool won, Color color)
        {
            RectangleF r = new RectangleF(x, y, 14, 14);
            if (won)
            {
                using (SolidBrush fill = new SolidBrush(color)) g.FillEllipse(fill, r);
            }
            using (Pen outline = new Pen(won ? Color.White : Color.Gray, 2)) g.DrawEllipse(outline, r);
        }

        // Health bar, age tag and Chrono-Shadow meter for one fighter. Player 1 is on the left, player 2 on the right.
        private void DrawFighterPanel(Graphics g, int who, int barX, int barW, int barH, int topY)
        {
            bool left = who == 1;
            Player sp = SpriteOf(who);
            int health = left ? p1Health : p2Health;
            int redHealth = left ? p1RedHealth : p2RedHealth;

            using (SolidBrush glass = new SolidBrush(Color.FromArgb(160, 20, 20, 30)))
            {
                g.FillRectangle(glass, barX - 6, topY - 6, barW + 12, barH + 66);
            }
            using (Pen frame = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                g.DrawRectangle(frame, barX - 6, topY - 6, barW + 12, barH + 66);
            }

            // The bars fill toward the center. A fighter who has aged has a smaller health pool, so the
            // outer part of the bar is greyed out: that is the max HP lost to The Mortal Toll.
            int redW = (int)(barW * ((float)redHealth / MaxHealth));
            int curW = (int)(barW * ((float)health / MaxHealth));
            int maxW = (int)(barW * (sp.MaxHealth / MaxHealth));
            int lostW = barW - maxW;

            int redX = left ? barX + (barW - redW) : barX;
            g.FillRectangle(Brushes.DarkRed, redX, topY, redW, barH);

            if (curW > 0)
            {
                int curX = left ? barX + (barW - curW) : barX;
                Color c1 = left ? Color.Gold : Color.OrangeRed;
                Color c2 = left ? Color.OrangeRed : Color.Gold;
                using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(barX, topY, barW, barH), c1, c2, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(gb, curX, topY, curW, barH);
                }
            }

            if (lostW > 0)
            {
                int lostX = left ? barX : barX + barW - lostW;
                using (SolidBrush lost = new SolidBrush(Color.FromArgb(200, 25, 25, 35)))
                using (HatchBrush hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(90, 120, 120, 140), Color.FromArgb(200, 25, 25, 35)))
                {
                    g.FillRectangle(hatch, lostX, topY, lostW, barH);
                }
            }
            using (Pen border = new Pen(Color.White, 2)) g.DrawRectangle(border, barX, topY, barW, barH);

            // Name and age
            string name = FighterDisplayName(who);
            string age = CombatRules.AgeName(sp.CurrentAge);
            Color ageColor = sp.CurrentAge == AgeStage.Youth ? Color.LimeGreen : (sp.CurrentAge == AgeStage.Veteran ? Color.Orange : Color.Crimson);
            using (Font nf = new Font("Impact", 14, FontStyle.Italic))
            using (Font af = new Font("Impact", 11))
            using (SolidBrush ageBrush = new SolidBrush(ageColor))
            {
                SizeF ns = g.MeasureString(name, nf);
                SizeF ags = g.MeasureString(age, af);
                if (left)
                {
                    g.DrawString(name, nf, Brushes.White, barX, topY + barH + 2);
                    g.DrawString(age, af, ageBrush, barX + ns.Width, topY + barH + 5);
                }
                else
                {
                    g.DrawString(name, nf, Brushes.White, barX + barW - ns.Width, topY + barH + 2);
                    g.DrawString(age, af, ageBrush, barX + barW - ns.Width - ags.Width, topY + barH + 5);
                }
            }

            // Chrono-Shadow meter. The notch marks how much one phantom costs.
            int meterY = topY + barH + 24;
            int meterH = 16;
            int charges = (int)(sp.ShadowMeter / GameConfig.ShadowCost);
            bool ready = charges > 0;

            using (SolidBrush back = new SolidBrush(Color.FromArgb(255, 10, 20, 40)))
            {
                g.FillRectangle(back, barX, meterY, barW, meterH);
            }

            int meterW = (int)(barW * (sp.ShadowMeter / GameConfig.MaxShadowMeter));
            if (meterW > 0)
            {
                int meterX = left ? barX + (barW - meterW) : barX;
                Color m1 = ready ? Color.DeepSkyBlue : Color.SteelBlue;
                Color m2 = ready ? Color.White : Color.LightSteelBlue;
                using (LinearGradientBrush mb = new LinearGradientBrush(new Rectangle(meterX, meterY, Math.Max(1, meterW), meterH), m1, m2, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(mb, meterX, meterY, meterW, meterH);
                }
            }

            int costW = (int)(barW * (GameConfig.ShadowCost / GameConfig.MaxShadowMeter));
            int notchX = left ? barX + barW - costW : barX + costW;
            g.DrawLine(Pens.White, notchX, meterY - 3, notchX, meterY + meterH + 3);
            using (Pen meterBorder = new Pen(ready ? Color.DeepSkyBlue : Color.LightSteelBlue, 2))
            {
                g.DrawRectangle(meterBorder, barX, meterY, barW, meterH);
            }

            using (Font lf = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                string label = ready ? $"CHRONO-SHADOW  x{charges}  READY" : "CHRONO-SHADOW";
                SizeF ls = g.MeasureString(label, lf);
                float lx = left ? barX : barX + barW - ls.Width;
                float ly = meterY + meterH + 3;
                Brush main = ready ? Brushes.DeepSkyBlue : Brushes.White;
                g.DrawString(label, lf, Brushes.Black, lx + 1, ly + 1);   // outline so it reads on any background
                g.DrawString(label, lf, main, lx, ly);
            }
        }

        private void DrawTextCentered(Graphics g, string text, Font font, Brush brush, int width, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (width / 2f) - (size.Width / 2f), y);
        }

        // Small PAUSE button at the top of the screen, and the dimmed pause menu
        private void DrawPauseUi(Graphics g, int w, int h)
        {
            Rectangle b = PauseButtonRect;
            using (SolidBrush back = new SolidBrush(Color.FromArgb(170, 15, 15, 25)))
            using (Pen edge = new Pen(Color.FromArgb(200, 255, 255, 255), 1))
            using (Font f = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.FillRectangle(back, b);
                g.DrawRectangle(edge, b);
                DrawTextCentered(g, paused ? "RESUME" : "II PAUSE", f, Brushes.White, w, b.Y + 3);
            }

            if (!paused) return;

            using (SolidBrush dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.FillRectangle(dim, 0, 0, w, h);
            }
            using (Font big = new Font("Impact", 54, FontStyle.Italic))
            using (Font small = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                DrawTextCentered(g, "PAUSED", big, Brushes.Gold, w, 190);
                DrawTextCentered(g, "[ ESC ] or [ ENTER ]  Resume", small, Brushes.White, w, 300);
                DrawTextCentered(g, "[ R ]  Quit to menu", small, Brushes.LightGray, w, 332);
            }
        }

        private void DrawAnnouncement(Graphics g, string text, Color color)
        {
            int w = this.ClientSize.Width;
            using (Font af = new Font("Impact", 60, FontStyle.Italic))
            using (SolidBrush brush = new SolidBrush(color))
            {
                SizeF sz = g.MeasureString(text, af);
                g.DrawString(text, af, Brushes.DarkRed, (w / 2f) - (sz.Width / 2f) + 4, 154);
                g.DrawString(text, af, brush, (w / 2f) - (sz.Width / 2f), 150);
            }
        }
    }
}