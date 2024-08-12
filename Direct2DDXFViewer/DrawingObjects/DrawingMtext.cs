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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

            _textFormat = GetTextFormat();
            UpdateGeometry();
            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float thickness, Brush brush)
        {
            deviceContext.DrawText(DxfMtext.PlainText(), _textFormat, new RawRectangleF((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom), Brush);
        }
        public override void DrawToRenderTarget(RenderTarget target, float thickness, Brush brush)
        {
            target.DrawText(DxfMtext.PlainText(), _textFormat, new RawRectangleF((float)Bounds.Left, (float)Bounds.Top, (float)Bounds.Right, (float)Bounds.Bottom), Brush);
        }
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            return Bounds.IntersectsWith(rect) || Bounds.Contains(rect);
        }
        public override void UpdateGeometry()
        {
            Bounds = new(DxfMtext.Position.X, DxfMtext.Position.Y, DxfMtext.RectangleWidth, DxfMtext.Height);
        }
        public TextFormat GetTextFormat()
        {
            return new TextFormat(_factoryWrite, DxfMtext.Style.FontFamilyName, (float)(DxfMtext.Height * 0.125));
        }
        #endregion
    }
}
