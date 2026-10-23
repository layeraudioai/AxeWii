using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AxeWii.Core;
using AxeWii.Hardware;
using AxeWii.Motion;

namespace AxeWii.Graphics
{
    /// <summary>
    /// 3D Spatial Guitar Hero Controller Visualizer for MonoGame.
    /// Renders 3D guitar body, neck, headstock, fret buttons, strum bar, whammy bar,
    /// and dynamically tilts the 3D instrument to match the physical Wiimote's pitch & roll!
    /// </summary>
    public class GuitarVisualizer3D
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;

        private static readonly Color ColorBody = new Color(20, 24, 32);
        private static readonly Color ColorNeck = new Color(45, 35, 25);
        private static readonly Color ColorHeadstock = new Color(28, 30, 38);
        private static readonly Color ColorPickguard = new Color(210, 210, 210);
        private static readonly Color ColorStrum = new Color(15, 15, 15);
        private static readonly Color ColorWhammy = new Color(192, 192, 192);

        // Fret button colors (active / dim)
        private static readonly Color ColorGreen = new Color(34, 197, 94);
        private static readonly Color ColorRed = new Color(239, 68, 68);
        private static readonly Color ColorYellow = new Color(234, 179, 8);
        private static readonly Color ColorBlue = new Color(59, 130, 246);
        private static readonly Color ColorOrange = new Color(249, 115, 22);

        public GuitarVisualizer3D(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), graphicsDevice.Viewport.AspectRatio, 0.1f, 100f),
                View = Matrix.CreateLookAt(new Vector3(0, 0.8f, 5.2f), new Vector3(0, 0, 0), Vector3.Up),
                World = Matrix.Identity
            };
        }

        public void Draw(GuitarState guitar, SpatialMotionState motion, bool isLeftHanded = false)
        {
            // Apply physical Wiimote Pitch (tilt elevation) & Roll (rotation) to 3D model
            float pitchRad = MathHelper.ToRadians(motion.Pitch * 0.7f);
            float rollRad = MathHelper.ToRadians((isLeftHanded ? motion.Roll : -motion.Roll) * 0.7f);

            Matrix mirror = isLeftHanded ? Matrix.CreateScale(-1f, 1f, 1f) : Matrix.Identity;
            float posX = isLeftHanded ? -2.4f : 2.4f;

            Matrix guitarTransform = mirror * 
                                    Matrix.CreateRotationZ(rollRad) * 
                                    Matrix.CreateRotationX(pitchRad) * 
                                    Matrix.CreateTranslation(new Vector3(posX, -0.6f, -1.0f));

            _effect.World = guitarTransform;

            // 1. Draw Guitar Body
            DrawBody();

            // 2. Draw Guitar Neck
            DrawNeck();

            // 3. Draw 5 Fret Buttons
            DrawFrets(guitar.Frets);

            // 4. Draw Strum Bar & Whammy Bar
            DrawStrumBar(guitar.StrumUp, guitar.StrumDown);
            DrawWhammyBar(guitar.Whammy);
        }

        private void DrawBody()
        {
            // Les Paul style contour
            VertexPositionColor[] bodyVerts = new VertexPositionColor[12]
            {
                // Lower bout
                new VertexPositionColor(new Vector3(-0.9f, -1.2f, 0), ColorBody),
                new VertexPositionColor(new Vector3(-1.1f, -0.4f, 0), ColorBody),
                new VertexPositionColor(new Vector3(1.1f, -0.4f, 0), ColorBody),

                new VertexPositionColor(new Vector3(-0.9f, -1.2f, 0), ColorBody),
                new VertexPositionColor(new Vector3(1.1f, -0.4f, 0), ColorBody),
                new VertexPositionColor(new Vector3(0.9f, -1.2f, 0), ColorBody),

                // Waist to Upper bout
                new VertexPositionColor(new Vector3(-0.8f, -0.4f, 0), ColorBody),
                new VertexPositionColor(new Vector3(-0.7f, 0.5f, 0), ColorBody),
                new VertexPositionColor(new Vector3(0.7f, 0.5f, 0), ColorBody),

                new VertexPositionColor(new Vector3(-0.8f, -0.4f, 0), ColorBody),
                new VertexPositionColor(new Vector3(0.7f, 0.5f, 0), ColorBody),
                new VertexPositionColor(new Vector3(0.8f, -0.4f, 0), ColorBody),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, bodyVerts, 0, 4);
            }
        }

        private void DrawNeck()
        {
            // Fretboard neck extending upward
            VertexPositionColor[] neckVerts = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-0.2f, 0.4f, 0.02f), ColorNeck),
                new VertexPositionColor(new Vector3(-0.16f, 2.6f, 0.02f), ColorNeck),
                new VertexPositionColor(new Vector3(0.16f, 2.6f, 0.02f), ColorNeck),

                new VertexPositionColor(new Vector3(-0.2f, 0.4f, 0.02f), ColorNeck),
                new VertexPositionColor(new Vector3(0.16f, 2.6f, 0.02f), ColorNeck),
                new VertexPositionColor(new Vector3(0.2f, 0.4f, 0.02f), ColorNeck),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, neckVerts, 0, 2);
            }
        }

        private void DrawFrets(GuitarFrets frets)
        {
            float[] fretY = { 2.3f, 1.95f, 1.6f, 1.25f, 0.9f };
            bool[] states = { frets.Green, frets.Red, frets.Yellow, frets.Blue, frets.Orange };
            Color[] colors = { ColorGreen, ColorRed, ColorYellow, ColorBlue, ColorOrange };

            for (int i = 0; i < 5; i++)
            {
                float y = fretY[i];
                Color c = states[i] ? Color.White : colors[i] * 0.6f;
                Color baseC = states[i] ? colors[i] : colors[i] * 0.3f;

                VertexPositionColor[] button = new VertexPositionColor[6]
                {
                    new VertexPositionColor(new Vector3(-0.14f, y - 0.08f, 0.05f), baseC),
                    new VertexPositionColor(new Vector3(-0.14f, y + 0.08f, 0.05f), c),
                    new VertexPositionColor(new Vector3(0.14f, y + 0.08f, 0.05f), c),

                    new VertexPositionColor(new Vector3(-0.14f, y - 0.08f, 0.05f), baseC),
                    new VertexPositionColor(new Vector3(0.14f, y + 0.08f, 0.05f), c),
                    new VertexPositionColor(new Vector3(0.14f, y - 0.08f, 0.05f), baseC),
                };

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, button, 0, 2);
                }
            }
        }

        private void DrawStrumBar(bool strumUp, bool strumDown)
        {
            float tilt = strumUp ? 0.04f : strumDown ? -0.04f : 0f;
            Color strumColor = (strumUp || strumDown) ? Color.White : ColorStrum;

            VertexPositionColor[] strum = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-0.06f, -0.45f + tilt, 0.06f), strumColor),
                new VertexPositionColor(new Vector3(-0.06f, -0.15f + tilt, 0.06f), strumColor),
                new VertexPositionColor(new Vector3(0.06f, -0.15f + tilt, 0.06f), strumColor),

                new VertexPositionColor(new Vector3(-0.06f, -0.45f + tilt, 0.06f), strumColor),
                new VertexPositionColor(new Vector3(0.06f, -0.15f + tilt, 0.06f), strumColor),
                new VertexPositionColor(new Vector3(0.06f, -0.45f + tilt, 0.06f), strumColor),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, strum, 0, 2);
            }
        }

        private void DrawWhammyBar(float whammy)
        {
            float deflection = whammy * -0.2f;
            VertexPositionColor[] bar = new VertexPositionColor[2]
            {
                new VertexPositionColor(new Vector3(0.35f, -0.5f, 0.04f), ColorWhammy),
                new VertexPositionColor(new Vector3(0.55f, -0.9f, 0.04f + deflection), ColorWhammy)
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, bar, 0, 1);
            }
        }
    }
}
