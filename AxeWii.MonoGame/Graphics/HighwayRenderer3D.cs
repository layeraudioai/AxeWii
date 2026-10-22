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
    /// 3D Perspective Rhythm Highway Renderer for MonoGame.
    /// Notes roll from the horizon (far distance) DOWNWARDS towards the strike line (near player).
    /// </summary>
    public class HighwayRenderer3D
    {
        private readonly GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Texture2D _pixelTexture;

        public float HighwayLookaheadSeconds { get; set; } = 1.4f;
        public float CurrentSongTime { get; set; } = 0f;
        public int Bpm { get; set; } = 120;

        // Fret lane colors
        private static readonly Color ColorGreen = new Color(34, 197, 94);
        private static readonly Color ColorRed = new Color(239, 68, 68);
        private static readonly Color ColorYellow = new Color(234, 179, 8);
        private static readonly Color ColorBlue = new Color(59, 130, 246);
        private static readonly Color ColorOrange = new Color(249, 115, 22);
        private static readonly Color ColorOpen = new Color(168, 85, 247);

        public HighwayRenderer3D(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60f), graphicsDevice.Viewport.AspectRatio, 0.1f, 100f),
                View = Matrix.CreateLookAt(new Vector3(0, 3.5f, -4.5f), new Vector3(0, 0.5f, 2f), Vector3.Up),
                World = Matrix.Identity
            };

            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public void Draw(SpriteBatch spriteBatch, GuitarState controllerState, List<NoteEvent> notes)
        {
            float strikeZ = 0.5f;
            float horizonZ = 8.5f;

            // Draw 3D Highway Surface (Quad)
            DrawHighwayBoard(strikeZ, horizonZ);

            // Draw Beat Lines
            DrawBeatLines(strikeZ, horizonZ);

            // Draw Notes Rolling DOWN from horizon to strike line
            DrawNotes(notes, strikeZ, horizonZ, controllerState);
        }

        private void DrawHighwayBoard(float strikeZ, float horizonZ)
        {
            float nearHalfWidth = 2.4f;
            float farHalfWidth = 1.1f;

            VertexPositionColor[] boardVertices = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-nearHalfWidth, 0, strikeZ), new Color(18, 24, 38)),
                new VertexPositionColor(new Vector3(-farHalfWidth, 0, horizonZ), new Color(8, 12, 18)),
                new VertexPositionColor(new Vector3(farHalfWidth, 0, horizonZ), new Color(8, 12, 18)),

                new VertexPositionColor(new Vector3(-nearHalfWidth, 0, strikeZ), new Color(18, 24, 38)),
                new VertexPositionColor(new Vector3(farHalfWidth, 0, horizonZ), new Color(8, 12, 18)),
                new VertexPositionColor(new Vector3(nearHalfWidth, 0, strikeZ), new Color(18, 24, 38)),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, boardVertices, 0, 2);
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
                    // Notes & beats travel from horizon (frac=1) DOWN to strike bar (frac=0)
                    float progress = MathF.Pow(1f - frac, 1.8f);
                    float z = MathHelper.Lerp(horizonZ, strikeZ, progress);
                    float hw = MathHelper.Lerp(1.1f, 2.4f, progress);

                    VertexPositionColor[] line = new VertexPositionColor[2]
                    {
                        new VertexPositionColor(new Vector3(-hw, 0.01f, z), (int)b % 4 == 0 ? Color.White * 0.4f : Color.White * 0.15f),
                        new VertexPositionColor(new Vector3(hw, 0.01f, z), (int)b % 4 == 0 ? Color.White * 0.4f : Color.White * 0.15f),
                    };

                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, line, 0, 1);
                    }
                }
            }
        }

        private void DrawNotes(List<NoteEvent> notes, float strikeZ, float horizonZ, GuitarState controller)
        {
            foreach (var note in notes)
            {
                float delta = note.Time - CurrentSongTime;
                float frac = delta / HighwayLookaheadSeconds;

                // Render note if within lookahead window
                if (frac >= -0.1f && frac <= 1.05f)
                {
                    // Correct direction: note travels from horizon towards strike line
                    float progress = MathF.Pow(Math.Clamp(1f - frac, 0f, 1.2f), 1.8f);
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

                    if (note.Fret.ToLower() == "open")
                    {
                        // Open note bar spanning all 5 lanes
                        DrawOpenNoteBar(z, halfWidth);
                    }
                    else
                    {
                        int laneIndex = GetLaneIndex(note.Fret);
                        float laneX = MathHelper.Lerp(-halfWidth, halfWidth, (laneIndex + 0.5f) / 5f);
                        float gemRadius = MathHelper.Lerp(0.08f, 0.22f, progress);

                        DrawNoteGem(laneX, z, gemRadius, gemColor);

                        // Sustain tail if note has duration
                        if (note.Duration > 0.15f)
                        {
                            float endDelta = note.Time + note.Duration - CurrentSongTime;
                            float endFrac = endDelta / HighwayLookaheadSeconds;
                            float endProgress = MathF.Pow(Math.Clamp(1f - endFrac, 0f, 1f), 1.8f);
                            float endZ = MathHelper.Lerp(horizonZ, strikeZ, endProgress);
                            DrawSustainRibbon(laneX, z, endZ, gemColor);
                        }
                    }
                }
            }
        }

        private void DrawNoteGem(float x, float z, float radius, Color color)
        {
            VertexPositionColor[] gem = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(x - radius, 0.05f, z - radius * 0.5f), color),
                new VertexPositionColor(new Vector3(x, 0.12f, z + radius * 0.5f), Color.White),
                new VertexPositionColor(new Vector3(x + radius, 0.05f, z - radius * 0.5f), color),

                new VertexPositionColor(new Vector3(x - radius, 0.05f, z - radius * 0.5f), color * 0.8f),
                new VertexPositionColor(new Vector3(x + radius, 0.05f, z - radius * 0.5f), color * 0.8f),
                new VertexPositionColor(new Vector3(x, 0.02f, z - radius), color * 0.6f),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, gem, 0, 2);
            }
        }

        private void DrawOpenNoteBar(float z, float halfWidth)
        {
            VertexPositionColor[] bar = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(-halfWidth * 0.95f, 0.04f, z - 0.04f), ColorOpen),
                new VertexPositionColor(new Vector3(-halfWidth * 0.95f, 0.06f, z + 0.04f), Color.White),
                new VertexPositionColor(new Vector3(halfWidth * 0.95f, 0.06f, z + 0.04f), Color.White),

                new VertexPositionColor(new Vector3(-halfWidth * 0.95f, 0.04f, z - 0.04f), ColorOpen),
                new VertexPositionColor(new Vector3(halfWidth * 0.95f, 0.06f, z + 0.04f), Color.White),
                new VertexPositionColor(new Vector3(halfWidth * 0.95f, 0.04f, z - 0.04f), ColorOpen),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, bar, 0, 2);
            }
        }

        private void DrawSustainRibbon(float x, float headZ, float endZ, Color color)
        {
            float ribbonW = 0.04f;
            VertexPositionColor[] ribbon = new VertexPositionColor[6]
            {
                new VertexPositionColor(new Vector3(x - ribbonW, 0.03f, headZ), color * 0.6f),
                new VertexPositionColor(new Vector3(x - ribbonW, 0.03f, endZ), color * 0.4f),
                new VertexPositionColor(new Vector3(x + ribbonW, 0.03f, endZ), color * 0.4f),

                new VertexPositionColor(new Vector3(x - ribbonW, 0.03f, headZ), color * 0.6f),
                new VertexPositionColor(new Vector3(x + ribbonW, 0.03f, endZ), color * 0.4f),
                new VertexPositionColor(new Vector3(x + ribbonW, 0.03f, headZ), color * 0.6f),
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
