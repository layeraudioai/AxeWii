using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AxeWii.Core;
using AxeWii.Hardware;

namespace AxeWii.Graphics
{
    using NoteEvent = MidiNoteEvent;

    /// <summary>
    /// High-Visibility 3D Rhythm Highway Renderer for MonoGame.
    /// Renders the 3D perspective board, lane dividers, beat markers, strike receptors,
    /// and oncoming notes smoothly rolling DOWNWARDS from the horizon towards the strike line.
    /// </summary>
    public class HighwayRenderer3D
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;

        public float HighwayLookaheadSeconds { get; set; } = 2.6f;
        public float CurrentSongTime { get; set; } = 0f;
        public int Bpm { get; set; } = 120;

        // Fret lane colors (matching classic Guitar Hero layout)
        public static readonly Color ColorGreen = new Color(34, 197, 94);
        public static readonly Color ColorRed = new Color(239, 68, 68);
        public static readonly Color ColorYellow = new Color(234, 179, 8);
        public static readonly Color ColorBlue = new Color(59, 130, 246);
        public static readonly Color ColorOrange = new Color(249, 115, 22);
        public static readonly Color ColorOpen = new Color(168, 85, 247);

        public HighwayRenderer3D(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(55f), graphicsDevice.Viewport.AspectRatio, 0.1f, 100f),
                View = Matrix.CreateLookAt(new Vector3(0, 3.8f, -4.2f), new Vector3(0, 0.6f, 2.5f), Vector3.Up),
                World = Matrix.Identity
            };
        }

        public void Draw(SpriteBatch spriteBatch, GuitarState controllerState, List<NoteEvent> notes)
        {
            // CRITICAL: Restore 3D states that SpriteBatch alters!
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.BlendState = BlendState.NonPremultiplied;

            float strikeZ = 0.5f;
            float horizonZ = 9.0f;

            // 1. Draw 3D Highway Surface (Quad)
            DrawHighwayBoard(strikeZ, horizonZ);

            // 2. Draw 4 Lane Divider Lines
            DrawLaneDividers(strikeZ, horizonZ);

            // 3. Draw Beat Lines
            DrawBeatLines(strikeZ, horizonZ);

            // 4. Draw Strike Line Receptors at strikeZ
            DrawStrikeReceptors(strikeZ, controllerState);

            // 5. Draw Oncoming Notes Rolling DOWN towards strike bar
            DrawNotes(notes, strikeZ, horizonZ, controllerState);
        }

        private void DrawHighwayBoard(float strikeZ, float horizonZ)
        {
            float nearHalfWidth = 2.4f;
            float farHalfWidth = 1.1f;

            VertexPositionColor[] boardVertices = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-nearHalfWidth, 0, strikeZ), new Color(18, 24, 38, 240)),
                new VertexPositionColor(new Vector3(-farHalfWidth, 0, horizonZ), new Color(8, 12, 18, 240)),
                new VertexPositionColor(new Vector3(farHalfWidth, 0, horizonZ), new Color(8, 12, 18, 240)),

                new VertexPositionColor(new Vector3(-nearHalfWidth, 0, strikeZ), new Color(18, 24, 38, 240)),
                new VertexPositionColor(new Vector3(farHalfWidth, 0, horizonZ), new Color(8, 12, 18, 240)),
                new VertexPositionColor(new Vector3(nearHalfWidth, 0, strikeZ), new Color(18, 24, 38, 240)),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, boardVertices, 0, 2);
            }
        }

        private void DrawLaneDividers(float strikeZ, float horizonZ)
        {
            for (int i = 1; i < 5; i++)
            {
                float frac = i / 5.0f;
                float nearX = MathHelper.Lerp(-2.4f, 2.4f, frac);
                float farX = MathHelper.Lerp(-1.1f, 1.1f, frac);

                VertexPositionColor[] line = new VertexPositionColor[2]
                {
                    new VertexPositionColor(new Vector3(nearX, 0.01f, strikeZ), new Color(255, 255, 255, 30)),
                    new VertexPositionColor(new Vector3(farX, 0.01f, horizonZ), new Color(255, 255, 255, 15))
                };

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, line, 0, 1);
                }
            }
        }

        private void DrawBeatLines(float strikeZ, float horizonZ)
        {
            float beatInterval = 60f / Math.Max(30, Bpm);
            float startBeat = (float)Math.Floor(CurrentSongTime / beatInterval);
            float endBeat = (float)Math.Ceiling((CurrentSongTime + HighwayLookaheadSeconds) / beatInterval);

            for (float b = startBeat; b <= endBeat; b++)
            {
                float beatTime = b * beatInterval;
                float delta = beatTime - CurrentSongTime;
                float frac = delta / HighwayLookaheadSeconds;

                if (frac >= 0f && frac <= 1f)
                {
                    float progress = MathF.Pow(1f - frac, 1.6f);
                    float z = MathHelper.Lerp(horizonZ, strikeZ, progress);
                    float hw = MathHelper.Lerp(1.1f, 2.4f, progress);

                    bool isMeasure = ((int)Math.Round(b) % 4) == 0;
                    Color lineColor = isMeasure ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 45);

                    VertexPositionColor[] line = new VertexPositionColor[2]
                    {
                        new VertexPositionColor(new Vector3(-hw, 0.015f, z), lineColor),
                        new VertexPositionColor(new Vector3(hw, 0.015f, z), lineColor),
                    };

                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, line, 0, 1);
                    }
                }
            }
        }

        private void DrawStrikeReceptors(float strikeZ, GuitarState guitar)
        {
            // Translucent strike line baseline
            VertexPositionColor[] baseline = new VertexPositionColor[2]
            {
                new VertexPositionColor(new Vector3(-2.4f, 0.02f, strikeZ), new Color(255, 255, 255, 180)),
                new VertexPositionColor(new Vector3(2.4f, 0.02f, strikeZ), new Color(255, 255, 255, 180))
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, baseline, 0, 1);
            }

            // 5 Receptor circles at strike position
            bool[] states = {
                guitar.Frets.Green,
                guitar.Frets.Red,
                guitar.Frets.Yellow,
                guitar.Frets.Blue,
                guitar.Frets.Orange
            };

            Color[] colors = { ColorGreen, ColorRed, ColorYellow, ColorBlue, ColorOrange };

            for (int i = 0; i < 5; i++)
            {
                float x = MathHelper.Lerp(-2.4f, 2.4f, (i + 0.5f) / 5f);
                bool held = states[i];
                Color c = held ? Color.White : colors[i] * 0.5f;
                float r = held ? 0.22f : 0.17f;

                DrawReceptorQuad(x, strikeZ, r, c, held ? colors[i] : Color.Black);
            }
        }

        private void DrawReceptorQuad(float x, float z, float r, Color outerColor, Color innerColor)
        {
            VertexPositionColor[] quad = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(x - r, 0.025f, z - r), outerColor),
                new VertexPositionColor(new Vector3(x, 0.035f, z + r), innerColor),
                new VertexPositionColor(new Vector3(x + r, 0.025f, z - r), outerColor),

                new VertexPositionColor(new Vector3(x - r, 0.025f, z - r), outerColor),
                new VertexPositionColor(new Vector3(x + r, 0.025f, z - r), outerColor),
                new VertexPositionColor(new Vector3(x, 0.02f, z - r * 1.5f), outerColor * 0.5f),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
            }
        }

        private void DrawNotes(List<NoteEvent> notes, float strikeZ, float horizonZ, GuitarState controller)
        {
            foreach (var note in notes)
            {
                float delta = note.Time - CurrentSongTime;
                float frac = delta / HighwayLookaheadSeconds;

                // Render if within lookahead window (from horizon down past strike line slightly)
                if (frac >= -0.12f && frac <= 1.05f)
                {
                    float progress = MathF.Pow(Math.Clamp(1f - frac, 0f, 1.25f), 1.6f);
                    float z = MathHelper.Lerp(horizonZ, strikeZ, progress);
                    float halfWidth = MathHelper.Lerp(1.1f, 2.4f, progress);

                    Color gemColor = note.Fret.ToLower() switch
                    {
                        "green" => ColorGreen,
                        "red" => ColorRed,
                        "yellow" => ColorYellow,
                        "blue" => ColorBlue,
                        "orange" => ColorOrange,
                        _ => ColorOpen
                    };

                    if (note.Fret.Equals("open", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawOpenNoteBar(z, halfWidth);
                    }
                    else
                    {
                        int laneIndex = GetLaneIndex(note.Fret);
                        float laneX = MathHelper.Lerp(-halfWidth, halfWidth, (laneIndex + 0.5f) / 5f);
                        float gemRadius = MathHelper.Lerp(0.12f, 0.28f, progress);

                        DrawNoteGem(laneX, z, gemRadius, gemColor);

                        // Sustain tail if note has duration
                        if (note.Duration > 0.15f)
                        {
                            float endDelta = note.Time + note.Duration - CurrentSongTime;
                            float endFrac = endDelta / HighwayLookaheadSeconds;
                            float endProgress = MathF.Pow(Math.Clamp(1f - endFrac, 0f, 1f), 1.6f);
                            float endZ = MathHelper.Lerp(horizonZ, strikeZ, endProgress);
                            DrawSustainRibbon(laneX, z, endZ, gemColor);
                        }
                    }
                }
            }
        }

        private void DrawNoteGem(float x, float z, float radius, Color color)
        {
            // 3D Diamond / Jewel facet
            VertexPositionColor[] gem = new VertexPositionColor[12]
            {
                // Top facet
                new VertexPositionColor(new Vector3(x, 0.16f, z), Color.White),
                new VertexPositionColor(new Vector3(x - radius, 0.05f, z - radius * 0.4f), color),
                new VertexPositionColor(new Vector3(x, 0.05f, z + radius * 0.6f), color),

                new VertexPositionColor(new Vector3(x, 0.16f, z), Color.White),
                new VertexPositionColor(new Vector3(x, 0.05f, z + radius * 0.6f), color),
                new VertexPositionColor(new Vector3(x + radius, 0.05f, z - radius * 0.4f), color),

                // Back facets
                new VertexPositionColor(new Vector3(x, 0.16f, z), Color.White),
                new VertexPositionColor(new Vector3(x + radius, 0.05f, z - radius * 0.4f), color * 0.8f),
                new VertexPositionColor(new Vector3(x, 0.04f, z - radius * 0.8f), color * 0.6f),

                new VertexPositionColor(new Vector3(x, 0.16f, z), Color.White),
                new VertexPositionColor(new Vector3(x, 0.04f, z - radius * 0.8f), color * 0.6f),
                new VertexPositionColor(new Vector3(x - radius, 0.05f, z - radius * 0.4f), color * 0.8f),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, gem, 0, 4);
            }
        }

        private void DrawOpenNoteBar(float z, float halfWidth)
        {
            float w = halfWidth * 0.94f;
            VertexPositionColor[] bar = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-w, 0.04f, z - 0.06f), ColorOpen),
                new VertexPositionColor(new Vector3(-w, 0.08f, z + 0.06f), Color.White),
                new VertexPositionColor(new Vector3(w, 0.08f, z + 0.06f), Color.White),

                new VertexPositionColor(new Vector3(-w, 0.04f, z - 0.06f), ColorOpen),
                new VertexPositionColor(new Vector3(w, 0.08f, z + 0.06f), Color.White),
                new VertexPositionColor(new Vector3(w, 0.04f, z - 0.06f), ColorOpen),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, bar, 0, 2);
            }
        }

        private void DrawSustainRibbon(float x, float headZ, float endZ, Color color)
        {
            float rw = 0.06f;
            VertexPositionColor[] ribbon = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(x - rw, 0.035f, headZ), color * 0.8f),
                new VertexPositionColor(new Vector3(x - rw, 0.035f, endZ), color * 0.5f),
                new VertexPositionColor(new Vector3(x + rw, 0.035f, endZ), color * 0.5f),

                new VertexPositionColor(new Vector3(x - rw, 0.035f, headZ), color * 0.8f),
                new VertexPositionColor(new Vector3(x + rw, 0.035f, endZ), color * 0.5f),
                new VertexPositionColor(new Vector3(x + rw, 0.035f, headZ), color * 0.8f),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, ribbon, 0, 2);
            }
        }

        private static int GetLaneIndex(string fret)
        {
            return fret.ToLower() switch
            {
                "green" => 0,
                "red" => 1,
                "yellow" => 2,
                "blue" => 3,
                "orange" => 4,
                _ => 0
            };
        }
    }
}
