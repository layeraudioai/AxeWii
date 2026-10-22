using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AxeWii.UI
{
    /// <summary>
    /// Lightweight, zero-dependency procedural font renderer for MonoGame.
    /// Eliminates any need for pre-compiled .spritefont or .xnb files.
    /// </summary>
    public class VectorFontRenderer
    {
        private readonly Texture2D _pixel;

        // 5x7 ASCII bitmap glyph definitions (characters 32 to 126)
        private static readonly Dictionary<char, byte[]> Glyphs = new()
        {
            [' '] = new byte[] { 0, 0, 0, 0, 0 },
            ['!'] = new byte[] { 0x00, 0x5F, 0x00 },
            ['"'] = new byte[] { 0x07, 0x00, 0x07 },
            ['#'] = new byte[] { 0x14, 0x7F, 0x14, 0x7F, 0x14 },
            ['$'] = new byte[] { 0x24, 0x2A, 0x7F, 0x2A, 0x12 },
            ['%'] = new byte[] { 0x23, 0x13, 0x08, 0x64, 0x62 },
            ['&'] = new byte[] { 0x36, 0x49, 0x55, 0x22, 0x50 },
            ['\''] = new byte[] { 0x00, 0x05, 0x03, 0x00 },
            ['('] = new byte[] { 0x1C, 0x22, 0x41 },
            [')'] = new byte[] { 0x41, 0x22, 0x1C },
            ['*'] = new byte[] { 0x14, 0x08, 0x3E, 0x08, 0x14 },
            ['+'] = new byte[] { 0x08, 0x08, 0x3E, 0x08, 0x08 },
            [','] = new byte[] { 0x00, 0x50, 0x30, 0x00 },
            ['-'] = new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 },
            ['.'] = new byte[] { 0x00, 0x60, 0x60, 0x00 },
            ['/'] = new byte[] { 0x20, 0x10, 0x08, 0x04, 0x02 },
            ['0'] = new byte[] { 0x3E, 0x51, 0x49, 0x45, 0x3E },
            ['1'] = new byte[] { 0x00, 0x42, 0x7F, 0x40, 0x00 },
            ['2'] = new byte[] { 0x42, 0x61, 0x51, 0x49, 0x46 },
            ['3'] = new byte[] { 0x21, 0x41, 0x45, 0x4B, 0x31 },
            ['4'] = new byte[] { 0x18, 0x14, 0x12, 0x7F, 0x10 },
            ['5'] = new byte[] { 0x27, 0x45, 0x45, 0x45, 0x39 },
            ['6'] = new byte[] { 0x3C, 0x4A, 0x49, 0x49, 0x30 },
            ['7'] = new byte[] { 0x01, 0x71, 0x09, 0x05, 0x03 },
            ['8'] = new byte[] { 0x36, 0x49, 0x49, 0x49, 0x36 },
            ['9'] = new byte[] { 0x06, 0x49, 0x49, 0x29, 0x1E },
            [':'] = new byte[] { 0x00, 0x36, 0x36, 0x00 },
            [';'] = new byte[] { 0x00, 0x56, 0x36, 0x00 },
            ['<'] = new byte[] { 0x08, 0x14, 0x22, 0x41 },
            ['='] = new byte[] { 0x14, 0x14, 0x14, 0x14, 0x14 },
            ['>'] = new byte[] { 0x41, 0x22, 0x14, 0x08 },
            ['?'] = new byte[] { 0x02, 0x01, 0x51, 0x09, 0x06 },
            ['@'] = new byte[] { 0x32, 0x49, 0x79, 0x41, 0x3E },
            ['A'] = new byte[] { 0x7E, 0x11, 0x11, 0x11, 0x7E },
            ['B'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 },
            ['C'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 },
            ['D'] = new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C },
            ['E'] = new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 },
            ['F'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 },
            ['G'] = new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A },
            ['H'] = new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F },
            ['I'] = new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 },
            ['J'] = new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 },
            ['K'] = new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 },
            ['L'] = new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 },
            ['M'] = new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F },
            ['N'] = new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F },
            ['O'] = new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E },
            ['P'] = new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 },
            ['Q'] = new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E },
            ['R'] = new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 },
            ['S'] = new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 },
            ['T'] = new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 },
            ['U'] = new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F },
            ['V'] = new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F },
            ['W'] = new byte[] { 0x7F, 0x20, 0x18, 0x20, 0x7F },
            ['X'] = new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 },
            ['Y'] = new byte[] { 0x07, 0x08, 0x70, 0x08, 0x07 },
            ['Z'] = new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 },
            ['['] = new byte[] { 0x7F, 0x41, 0x41 },
            ['\\'] = new byte[] { 0x02, 0x04, 0x08, 0x10, 0x20 },
            [']'] = new byte[] { 0x41, 0x41, 0x7F },
            ['^'] = new byte[] { 0x04, 0x02, 0x01, 0x02, 0x04 },
            ['_'] = new byte[] { 0x40, 0x40, 0x40, 0x40, 0x40 },
            ['|'] = new byte[] { 0x00, 0x7F, 0x00 }
        };

        public VectorFontRenderer(GraphicsDevice graphicsDevice)
        {
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            float curX = position.X;
            float curY = position.Y;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    curX = position.X;
                    curY += 9 * scale;
                    continue;
                }

                char upper = char.ToUpperInvariant(c);
                if (!Glyphs.TryGetValue(upper, out var cols))
                {
                    cols = Glyphs[' '];
                }

                for (int col = 0; col < cols.Length; col++)
                {
                    byte colData = cols[col];
                    for (int row = 0; row < 7; row++)
                    {
                        if ((colData & (1 << row)) != 0)
                        {
                            spriteBatch.Draw(_pixel, new Rectangle(
                                (int)(curX + col * scale),
                                (int)(curY + row * scale),
                                (int)Math.Max(1, scale),
                                (int)Math.Max(1, scale)
                            ), color);
                        }
                    }
                }
                curX += (cols.Length + 1) * scale;
            }
        }

        public void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            spriteBatch.Draw(_pixel, rect, color);
        }

        public void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 1)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
