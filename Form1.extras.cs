using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// HEAT meter (fills as you hit and get hit; a full bar unlocks the ultimate) and the floating texts
    /// (damage numbers, BLOCK, skill names, "NEED FULL HEAT!").
    /// </summary>
    public partial class Form1 : Form
    {
        // =====================================================================
        //  HEAT
        // =====================================================================
        private const int MaxHeat = 100;
        private int p1Heat = 50, p2Heat = 50;   // both fighters start every round at 50

        private ref int HeatOf(int w) { if (w == 1) return ref p1Heat; return ref p2Heat; }

        private void AddHeat(int w, int amount)
        {
            HeatOf(w) = Math.Clamp(HeatOf(w) + amount, 0, MaxHeat);
        }

        private void ResetHeat()
        {
            p1Heat = 50;
            p2Heat = 50;
        }

        // =====================================================================
        //  FLOATING TEXTS
        // =====================================================================
        private sealed class FloatText
        {
            public float X, Y, Vy;
            public string Text = "";
            public Color Color;
            public int Life, MaxLife;
        }

        private readonly List<FloatText> floatTexts = new List<FloatText>();

        private void AddFloatText(float x, float y, string text, Color color)
        {
            if (floatTexts.Count > 40) floatTexts.RemoveAt(0);
            floatTexts.Add(new FloatText
            {
                X = x + rng.Next(-12, 13),
                Y = y,
                Vy = -1.2f,
                Text = text,
                Color = color,
                Life = 45,
                MaxLife = 45
            });
        }

        private void UpdateFloatTexts()
        {
            for (int i = floatTexts.Count - 1; i >= 0; i--)
            {
                floatTexts[i].Y += floatTexts[i].Vy;
                floatTexts[i].Life--;
                if (floatTexts[i].Life <= 0) floatTexts.RemoveAt(i);
            }
        }

        private void DrawFloatTexts(Graphics g)
        {
            if (floatTexts.Count == 0) return;
            using (Font f = new Font("Impact", 15, FontStyle.Italic))
            {
                foreach (FloatText t in floatTexts)
                {
                    int alpha = Math.Clamp(t.Life * 6, 0, 255);
                    float width = g.MeasureString(t.Text, f).Width;
                    float x = t.X - (width / 2f);
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    using (SolidBrush main = new SolidBrush(Color.FromArgb(alpha, t.Color)))
                    {
                        g.DrawString(t.Text, f, shadow, x + 2, t.Y + 2);
                        g.DrawString(t.Text, f, main, x, t.Y);
                    }
                }
            }
        }
    }
}