using Direct2DControl;
using Direct2DDXFViewer.Helpers;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Brush = SharpDX.Direct2D1.Brush;
using DeviceContext1 = SharpDX.Direct2D1.DeviceContext1;
using Factory1 = SharpDX.DirectWrite.Factory1;
using Point = System.Windows.Point;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingMtext : DrawingSegment
    {
        #region Fields
        private MText _dxfMtext;

        private Factory1 _factoryWrite;
        private TextFormat _textFormat;
        private RawMatrix3x2 _transform;
        private TextLayout _textLayout;
        private TextMetrics _textMetrics;
        private Point _topLeft;
        #endregion

        #region Properties
        public MText DxfMtext
        {
            get { return _dxfMtext; }
            set
            {
                _dxfMtext = value;
                OnPropertyChanged(nameof(DxfMtext));
            }
        }
        #endregion

        #region Constructor
        public DrawingMtext(MText dxfMtext, SharpDX.Direct2D1.Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache, ObjectLayer layer, Factory1 factoryWrite)
        {
            DxfMtext = dxfMtext;
            Entity = dxfMtext;
            Factory = factory;
            DeviceContext = deviceContext;
            ResCache = resCache;
            Layer = layer;
            _factoryWrite = factoryWrite;
            EntityCount = 1;

            UpdateGeometry();
            GetStrokeStyle();
            UpdateBrush();

            GetTransform();
            GetTextFormat();
            GetTextLayout();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            deviceContext.DrawTextLayout(new RawVector2((float)DxfMtext.Position.X, (float)DxfMtext.Position.Y), _textLayout, brush);
        }
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            //    var transform = deviceContext.Transform;
            //    deviceContext.Transform = _transform;
            deviceContext.DrawTextLayout(new RawVector2((float)_topLeft.X, (float)_topLeft.Y), _textLayout, brush);
            //deviceContext.DrawText(DxfMtext.PlainText(), _textFormat, new RawRectangleF((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom), Brush);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            var transform = target.Transform;
            transform *= _transform;
            target.Transform = transform;
            target.DrawTextLayout(new RawVector2((float)DxfMtext.Position.X, -(float)DxfMtext.Position.Y), _textLayout, brush);
            transform.M22 *= -1;
            target.Transform = transform;
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush, StrokeStyle1 strokeStyle)
        {
            var transform = target.Transform;
            transform.M22 *= -1;
            target.Transform = transform;
            target.DrawTextLayout(new RawVector2((float)DxfMtext.Position.X, -(float)DxfMtext.Position.Y), _textLayout, brush);
            transform.M22 *= -1;
            target.Transform = transform;
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }
        public override void UpdateGeometry()
        {
            Bounds = new(DxfMtext.Position.X, DxfMtext.Position.Y, DxfMtext.RectangleWidth, DxfMtext.Height);
        }
        public void GetTextFormat()
        {
            _textFormat = new(_factoryWrite, DxfMtext.Style.FontFamilyName, (float)(DxfMtext.Height));
        }
        public void GetTransform()
        {
            _topLeft = GetTextOrigin(DxfMtext, Bounds, new Point(DxfMtext.Position.X, DxfMtext.Position.Y));
            Matrix matrix = new();
            matrix.ScaleAt(-1, -1, _topLeft.X, _topLeft.Y);
            matrix.RotateAt(-(float)DxfMtext.Rotation, (float)_topLeft.X, (float)_topLeft.Y);
            _transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);
            Debug.WriteLine($"DxfMtext.PlainText(): {DxfMtext.PlainText()} DxfMtext.Rotation {DxfMtext.Rotation}");
        }
        public void GetTextLayout()
        {
            //RawMatrix3x2 transform = new((float)_transform.M11, (float)_transform.M12, (float)_transform.M21, (float)_transform.M22, (float)_transform.OffsetX, (float)_transform.OffsetY);
            _textLayout = new(_factoryWrite, DxfMtext.PlainText(), _textFormat, (float)Bounds.Width, (float)Bounds.Height);
            _textMetrics = _textLayout.Metrics;
        }
        public override bool Hittest(RawVector2 p, float thickness)
        {
            return Bounds.Contains(p.X, p.Y);
        }

        /// <summary>
        /// Gets the upper left point of the MText.
        /// </summary>
        /// <param name="mText"></param>
        /// <param name="rect"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public Point GetTextOrigin(MText mText, Rect rect, Point position)
        {
            Point adjustedPos = new();

            switch (mText.AttachmentPoint)
            {
                case MTextAttachmentPoint.TopLeft:
                    adjustedPos = position;
                    break;

                case MTextAttachmentPoint.TopCenter:
                    adjustedPos = new Point(position.X - (rect.Width) / 2,
                        position.Y);
                    break;

                case MTextAttachmentPoint.TopRight:
                    adjustedPos = new Point(position.X - (rect.Width),
                        position.Y);
                    break;

                case MTextAttachmentPoint.MiddleLeft:
                    adjustedPos = new Point(position.X,
                        position.Y - (rect.Height / 2));
                    break;

                case MTextAttachmentPoint.MiddleCenter:
                    adjustedPos = new Point(position.X - (rect.Width) / 2,
                        position.Y - (rect.Height / 2));
                    break;

                case MTextAttachmentPoint.MiddleRight:
                    adjustedPos = new Point(position.X - (rect.Width),
                        position.Y - (rect.Height / 2));
                    break;

                case MTextAttachmentPoint.BottomLeft:
                    adjustedPos = new Point(position.X,
                        position.Y - (rect.Height));
                    break;

                case MTextAttachmentPoint.BottomCenter:
                    adjustedPos = new Point(position.X - (rect.Width) / 2,
                        position.Y - (rect.Height));
                    break;

                case MTextAttachmentPoint.BottomRight:
                    adjustedPos = new Point(position.X - (rect.Width),
                        position.Y - (rect.Height));
                    break;

                default:
                    adjustedPos = position;
                    break;
            }

            return adjustedPos;
        }
        #endregion
    }
}
