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
        private Matrix _transform;
        private TextLayout _textLayout;
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
            //deviceContext.DrawText(DxfMtext.PlainText(), _textFormat, new RawRectangleF((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom), Brush);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
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
            _transform = new();
            _transform.ScaleAt(-1, -1, DxfMtext.Position.X, DxfMtext.Position.Y);
        }
        public void GetTextLayout()
        {
            RawMatrix3x2 transform = new((float)_transform.M11, (float)_transform.M12, (float)_transform.M21, (float)_transform.M22, (float)_transform.OffsetX, (float)_transform.OffsetY);
            _textLayout = new(_factoryWrite, DxfMtext.PlainText(), _textFormat, (float)Bounds.Width, (float)Bounds.Height, 96, transform, true);
        }
        #endregion
    }
}
