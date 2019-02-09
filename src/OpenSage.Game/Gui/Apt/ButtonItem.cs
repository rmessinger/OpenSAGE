﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using OpenSage.Data.Apt.Characters;
using OpenSage.Gui.Apt.ActionScript;
using OpenSage.Mathematics;
using Veldrid;

namespace OpenSage.Gui.Apt
{
    internal class ButtonItem : DisplayItem
    {
        private bool _isHovered = false;
        private bool _isDown = false;
        private ItemTransform _curTransform;
        private List<InstructionCollection> _actionList;
        public Texture Texture { get; set; }

        public override void Create(Character chararacter, AptContext context, SpriteItem parent = null)
        {
            Character = chararacter;
            Context = context;
            Parent = parent;
            Name = "";
            Visible = true;

            var button = Character as Button;

            _actionList = new List<InstructionCollection>();
        }

        public override bool HandleInput(Point2D mousePos, bool mouseDown)
        {
            var button = Character as Button;

            var transform = _curTransform.GeometryRotation;
            transform.Translation = _curTransform.GeometryTranslation;// * scaling;
            ApplyCurrentRecord(ref transform);

            var verts = button.Vertices;
            var mouse = new Point2D(mousePos.X, mousePos.Y);

            foreach (var tri in button.Triangles)
            {
                var v1 = Vector2.Transform(verts[tri.IDX0], transform);
                var v2 = Vector2.Transform(verts[tri.IDX1], transform);
                var v3 = Vector2.Transform(verts[tri.IDX2], transform);

                if (TriangleUtility.IsPointInside(v1, v2, v3, mouse))
                {
                    if (!_isHovered)
                    {
                        Debug.WriteLine("Hit: " + mousePos.X + "-" + mousePos.Y);
                        var idx = button.Actions.FindIndex(ba => ba.Flags.HasFlag(ButtonActionFlags.IdleToOverUp));
                        if (idx != -1)
                        {
                            _actionList.Add(button.Actions[idx].Instructions);
                        }
                        _isHovered = true;
                    }

                    if(_isHovered && mouseDown && !_isDown)
                    {
                        Debug.WriteLine("Down: " + mousePos.X + "-" + mousePos.Y);
                        var idx = button.Actions.FindIndex(ba => ba.Flags.HasFlag(ButtonActionFlags.OverUpToOverDown));
                        if (idx != -1)
                        {
                            _actionList.Add(button.Actions[idx].Instructions);
                        }
                        _isDown = true;
                    }

                    if(_isHovered && !mouseDown && _isDown)
                    {
                        Debug.WriteLine("Up: " + mousePos.X + "-" + mousePos.Y);
                        var idx = button.Actions.FindIndex(ba => ba.Flags.HasFlag(ButtonActionFlags.OverDownToOverUp));
                        if (idx != -1)
                        {
                            _actionList.Add(button.Actions[idx].Instructions);
                        }
                        _isDown = false;
                    }

                    return true;
                }
            }

            if (_isHovered)
            {
                var idx = button.Actions.FindIndex(ba => ba.Flags.HasFlag(ButtonActionFlags.OverUpToIdle));
                if (idx != -1)
                {
                    _actionList.Add(button.Actions[idx].Instructions);
                }
                _isHovered = false;
                Debug.WriteLine("Unhovered: " + mousePos.X + "-" + mousePos.Y);
            }
            return false;
        }

        private void ApplyCurrentRecord(ref Matrix3x2 t)
        {
            var button = Character as Button;
            var idx = button.Records.FindIndex(br => br.Flags.HasFlag(ButtonRecordFlags.StateHit));
            if(idx != -1)
            {
                var br = button.Records[idx];

                var a = new Matrix3x2(t.M11,t.M12, t.M21, t.M22,t.M31,t.M32);
                var b = new Matrix3x2(br.RotScale.M11, br.RotScale.M12, br.RotScale.M21, br.RotScale.M22,0,0);
                var c = Matrix3x2.Multiply(a, b);

                t.M11 = c.M11;
                t.M12 = c.M12;
                t.M21 = c.M21;
                t.M22 = c.M22;

                t.M31 += br.Translation.X;
                t.M32 += br.Translation.Y;
            }    
        }

        public override void Render(AptRenderer renderer, ItemTransform pTransform, DrawingContext2D dc)
        {
            var button = Character as Button;
            _curTransform = pTransform * Transform;
            _curTransform.GeometryTranslation *= renderer.Window.GetScaling();
            _curTransform.GeometryRotation.M11 *= renderer.Window.GetScaling().X;
            _curTransform.GeometryRotation.M22 *= renderer.Window.GetScaling().Y;

            var transform = _curTransform.GeometryRotation;
            transform.Translation = _curTransform.GeometryTranslation;
            ApplyCurrentRecord(ref transform);

            var verts = button.Vertices;

            foreach (var tri in button.Triangles)
            {
                var v1 = Vector2.Transform(verts[tri.IDX0], transform);
                var v2 = Vector2.Transform(verts[tri.IDX1], transform);
                var v3 = Vector2.Transform(verts[tri.IDX2], transform);

                var color = ColorRgbaF.White;

                if (button.IsMenu)
                {
                    color = new ColorRgbaF(1.0f, 0.0f, 0.0f, 1.0f);
                }

                if (_isHovered)
                {
                    color = new ColorRgbaF(0.0f, 1.0f, 1.0f, 1.0f);
                }

                dc.DrawLine(new Line2D(v1, v2), 1.0f, color);
                dc.DrawLine(new Line2D(v2, v3), 1.0f, color);
                dc.DrawLine(new Line2D(v3, v1), 1.0f, color);
            }
        }

        public override void RunActions(TimeInterval gt)
        {
            foreach (var action in _actionList)
            {
                Context.Avm.Execute(action, Parent.ScriptObject);
            }
            _actionList.Clear();
        }
    }
}