using System.Drawing.Drawing2D;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    public partial class Form1 : Form
    {
        private void DrawModeSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 44, FontStyle.Italic))
            {
                const string gameTitle = "Clash Arena";
                float titleX = (w / 2f) - (g.MeasureString(gameTitle, titleF).Width / 2f);
                g.DrawString(gameTitle, titleF, Brushes.OrangeRed, titleX + 3, 36);
                g.DrawString(gameTitle, titleF, Brushes.Gold, titleX, 33);
            }
            using (Font subF = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                DrawTextCentered(g, "SELECT BATTLE MODE", subF, Brushes.Cyan, w, 140);
            }

            int cardW = 320, cardH = 260, gap = 80, y = 190;
            int startX = (w - ((cardW * 2) + gap)) / 2;
            DrawModeCard(g, startX, y, cardW, cardH, "2-PLAYER BATTLE", "Player 1 vs Player 2\n(Local PVP)", selectedMode == 0, Color.Cyan);
            DrawModeCard(g, startX + cardW + gap, y, cardW, cardH, "ARCADE MODE", "Player 1 vs Computer AI Opponent\n(VS CPU)", selectedMode == 1, Color.Crimson);

            using (Font promptF = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                DrawTextCentered(g, "[ ARROW KEYS ] Choose Mode   |   [ ENTER ] Confirm", promptF, Brushes.Gold, w, h - 70);
            }
        }

        // One selectable card. The title, text and "selected" label are all centered inside the card
        // and the text wraps, so nothing runs past the edge.
        private void DrawModeCard(Graphics g, int x, int y, int w, int h, string title, string sub, bool isSelected, Color neon, string selectedText = "★ READY TO FIGHT ★")
        {
            using (Pen p = new Pen(isSelected ? neon : Color.FromArgb(50, 50, 65), isSelected ? 4 : 2)) g.DrawRectangle(p, x, y, w, h);
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(x, y, w, h),
                isSelected ? Color.FromArgb(30, 25, 45) : Color.FromArgb(15, 15, 20),
                Color.FromArgb(10, 10, 15), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, x + 2, y + 2, w - 4, h - 4);
            }

            const float pad = 16f;
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center })
            using (StringFormat centerOneLine = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                // Title: shrinks until it fits inside the card
                float size = 22f;
                Font tf = new Font("Impact", size, FontStyle.Italic);
                while (size > 12f && g.MeasureString(title, tf).Width > w - (pad * 2))
                {
                    tf.Dispose();
                    size -= 1f;
                    tf = new Font("Impact", size, FontStyle.Italic);
                }
                g.DrawString(title, tf, isSelected ? Brushes.White : Brushes.Gray, new RectangleF(x + pad, y + 36, w - (pad * 2), 44), centerOneLine);
                tf.Dispose();

                using (Font sf = new Font("Segoe UI", 10))
                {
                    g.DrawString(sub, sf, Brushes.Silver, new RectangleF(x + pad, y + 92, w - (pad * 2), h - 150), center);
                }

                if (isSelected)
                {
                    using (Font selF = new Font("Impact", 16))
                    using (SolidBrush neonBrush = new SolidBrush(neon))
                    {
                        g.DrawString(selectedText, selF, neonBrush, new RectangleF(x + pad, y + h - 56, w - (pad * 2), 30), centerOneLine);
                    }
                }
            }
        }

        private void DrawDifficultySelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 34, FontStyle.Italic))
            {
                DrawTextCentered(g, "CHOOSE CPU DIFFICULTY", titleF, Brushes.Gold, w, 45);
            }

            string[] names = { "EASY", "MEDIUM", "HARD" };
            string[] notes =
            {
                "Slow to react and rarely defends. Never uses Chrono-Shadow. Good for learning the controls.",
                "A balanced opponent that blocks, ducks and mixes up its attacks. Uses Chrono-Shadow now and then.",
                "Reacts fast, blocks and ducks smartly, dashes in often, chains long strings and drops Chrono-Shadows on you."
            };
            Color[] neon = { Color.LimeGreen, Color.Gold, Color.Crimson };

            int cardW = 270, cardH = 260, gap = 30, y = 140;
            int startX = (w - ((cardW * 3) + (gap * 2))) / 2;
            for (int i = 0; i < 3; i++)
            {
                DrawModeCard(g, startX + (i * (cardW + gap)), y, cardW, cardH, names[i], notes[i], (int)cpuDifficulty == i, neon[i], "★ SELECTED ★");
            }

            using (Font promptF = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                DrawTextCentered(g, "[ ARROWS ] Choose Difficulty   |   [ ENTER ] Confirm   |   [ BACKSPACE ] Back", promptF, Brushes.Gold, w, h - 70);
            }
        }

        private void DrawCharacterSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 34, FontStyle.Italic))
            {
                string header = isVsCPU ? "CHOOSE YOUR FIGHTER (VS CPU)" : "CHARACTER SELECT (PVP)";
                DrawTextCentered(g, header, titleF, Brushes.Gold, w, 25);
            }

            int cardW = 210, cardH = 340, startY = 100, spacing = 240;
            int startX = (w - (((GameData.Roster.Length - 1) * spacing) + cardW)) / 2;

            using (Font nameF = new Font("Impact", 13))
            using (Font styleF = new Font("Segoe UI", 8))
            using (Font tagF = new Font("Impact", 11))
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter })
            {
                for (int i = 0; i < GameData.Roster.Length; i++)
                {
                    int cx = startX + (i * spacing);
                    bool isP1 = p1CharIndex == i;
                    bool isP2 = p2CharIndex == i;

                    Color border = isP1 ? Color.Cyan : (isP2 ? Color.Crimson : Color.FromArgb(50, 50, 60));
                    using (Pen bp = new Pen(border, (isP1 || isP2) ? 4 : 2)) g.DrawRectangle(bp, cx, startY, cardW, cardH);

                    using (LinearGradientBrush cb = new LinearGradientBrush(new Rectangle(cx, startY, cardW, cardH),
                        Color.FromArgb(28, 28, 42), Color.FromArgb(10, 10, 15), LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(cb, cx + 2, startY + 2, cardW - 4, cardH - 4);
                    }

                    DrawArticulatedFighter(g, cx + 50, startY + 70, 0, true, GameData.Roster[i]);

                    using (SolidBrush b = new SolidBrush(Color.Black)) g.FillRectangle(b, cx + 2, startY + cardH - 75, cardW - 4, 73);
                    g.DrawString(GameData.Roster[i].Name, nameF, Brushes.White, new RectangleF(cx + 6, startY + cardH - 70, cardW - 12, 22), center);
                    g.DrawString(GameData.Roster[i].Style, styleF, Brushes.LightGray, new RectangleF(cx + 6, startY + cardH - 46, cardW - 12, 16), center);

                    if (isP1) g.DrawString(p1Locked ? "★ P1 LOCKED" : "► P1 SELECT", tagF, Brushes.Cyan, new RectangleF(cx, startY + 12, cardW, 18), center);
                    if (isP2 && p1Locked && !isVsCPU) g.DrawString("► P2 SELECT", tagF, Brushes.Crimson, new RectangleF(cx, startY + 30, cardW, 18), center);
                }
            }

            string prompt = isVsCPU
                ? "[ ARROWS ] Choose Character   |   [ ENTER ] Confirm and Choose Arena   |   [ BACKSPACE ] Back"
                : (!p1Locked ? "[ ARROWS ] Choose P1  |  [ ENTER ] Lock P1" : "[ ARROWS ] Choose P2  |  [ ENTER ] Choose Arena");
            using (Font promptF = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                DrawTextCentered(g, prompt, promptF, Brushes.Gold, w, h - 50);
            }
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - (inv * inv * inv);
        }

        private void ChangeArena(int direction)
        {
            arenaPrevIndex = selectedArenaIndex;
            arenaTransitionDir = direction;
            selectedArenaIndex = (selectedArenaIndex + direction + GameData.Arenas.Length) % GameData.Arenas.Length;
            arenaTransition = 0f;
            Sfx.Play(520, 40);
        }

        private void DrawArenaPreview(Graphics g, int arenaIndex, Rectangle box, float offsetX)
        {
            GraphicsState state = g.Save();
            g.SetClip(box);
            g.TranslateTransform(box.X + offsetX, box.Y);
            float scale = box.Width / 1024f;
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(0, 20);

            DrawDynamicArenaScenery(g, 512f, arenaIndex);

            ArenaInfo arena = GameData.Arenas[arenaIndex];
            foreach (var m in ambientMotes)
            {
                if (arena.BackgroundFile != null) break;
                using (SolidBrush mote = new SolidBrush(Color.FromArgb(m.Alpha, arena.ParticleColor)))
                {
                    g.FillEllipse(mote, m.X, m.Y, m.Size, m.Size);
                }
            }

            g.Restore(state);
        }

        private void DrawArenaSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using Font titleF = new Font("Impact", 30, FontStyle.Italic);
            using Font nameF = new Font("Impact", 16);
            using Font cardF = new Font("Impact", 11);
            using Font subF = new Font("Segoe UI", 9);
            using Font promptF = new Font("Segoe UI", 12, FontStyle.Bold);

            const string header = "SELECT BATTLE STAGE";
            SizeF headerSize = g.MeasureString(header, titleF);
            g.DrawString(header, titleF, Brushes.Cyan, (w - headerSize.Width) / 2f, 12);

            ArenaInfo current = GameData.Arenas[selectedArenaIndex];

            Rectangle box = new Rectangle((w - 520) / 2, 64, 520, 315);
            int glowAlpha = 130;
            using (Pen glow = new Pen(Color.FromArgb(glowAlpha, current.RingNeon), 10))
            {
                g.DrawRectangle(glow, box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6);
            }

            if (arenaTransition < 1f)
            {
                float eased = EaseOutCubic(arenaTransition);
                DrawArenaPreview(g, arenaPrevIndex, box, -arenaTransitionDir * eased * box.Width);
                DrawArenaPreview(g, selectedArenaIndex, box, arenaTransitionDir * (1f - eased) * box.Width);
            }
            else
            {
                DrawArenaPreview(g, selectedArenaIndex, box, 0f);
            }

            Rectangle infoBar = new Rectangle(box.X, box.Bottom - 50, box.Width, 50);
            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
            using (SolidBrush nameBrush = new SolidBrush(current.RingNeon))
            {
                g.FillRectangle(barBrush, infoBar);
                g.DrawString(current.Name, nameF, nameBrush, infoBar.X + 12, infoBar.Y + 4);
                g.DrawString(current.Subtitle, subF, Brushes.LightGray, infoBar.X + 12, infoBar.Y + 29);
            }
            using (Pen boxBorder = new Pen(current.RingNeon, 3))
            {
                g.DrawRectangle(boxBorder, box);
            }

            int cardW = 210, cardH = 110, spacing = 260, baseY = 410;
            const float hoverLift = 16f;
            int startX = (w - ((spacing * (GameData.Arenas.Length - 1)) + cardW)) / 2;

            for (int i = 0; i < GameData.Arenas.Length; i++)
            {
                ArenaInfo arena = GameData.Arenas[i];
                float near = Math.Max(0f, 1f - Math.Abs(i - arenaSlide));
                RectangleF card = new RectangleF(startX + (i * spacing), baseY - (near * hoverLift), cardW, cardH);

                Bitmap? thumb = ArenaImages.Get(arena);
                if (thumb != null)
                {
                    RectangleF art = new RectangleF(card.X + 2, card.Y + 2, card.Width - 4, card.Height - 34);
                    float srcH = 1024f * art.Height / art.Width;
                    g.DrawImage(thumb, art, new RectangleF(0, 150, 1024, srcH), GraphicsUnit.Pixel);
                }

                using (SolidBrush nameBar = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(nameBar, card.X + 2, card.Bottom - 32, card.Width - 4, 30);
                }
                g.DrawString(arena.Name, cardF, i == selectedArenaIndex ? Brushes.White : Brushes.Gray, card.X + 8, card.Bottom - 26);

                bool highlighted = near > 0.5f;
                using (Pen border = new Pen(highlighted ? arena.RingNeon : Color.FromArgb(50, 50, 65), highlighted ? 4 : 2))
                {
                    g.DrawRectangle(border, card.X, card.Y, card.Width, card.Height);
                }
            }

            float frameX = startX + (arenaSlide * spacing);
            using (Pen frame = new Pen(current.RingNeon, 3))
            {
                g.DrawRectangle(frame, frameX - 8, baseY - hoverLift - 8, cardW + 16, cardH + 16);
            }

            const string prompt = "[ ARROWS ] Choose Stage  |  [ ENTER ] START MATCH  |  [ BACKSPACE ] Back";
            SizeF promptSize = g.MeasureString(prompt, promptF);
            g.DrawString(prompt, promptF, Brushes.Gold, (w - promptSize.Width) / 2f, h - 48);
        }
    }
}