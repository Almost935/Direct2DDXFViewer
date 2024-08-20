using Direct2DDXFViewer.DrawingObjects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Bitmap = SharpDX.Direct2D1.Bitmap;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmapView
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private RawMatrix3x2 _extentsMatrix;
        private Bitmap _overallBitmap;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        #endregion

        #region Properties
        float Zoom;
        DxfBitmap TopRightBitmap { get; set; }
        DxfBitmap TopLeftBitmap { get; set; }
        DxfBitmap BottomRightBitmap { get; set; }
        DxfBitmap BottomLeftBitmap { get; set; }
        #endregion

        #region Constructor
        public DxfBitmapView(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, RawMatrix3x2 extentsMatrix, float zoom)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extentsMatrix = extentsMatrix;
            Zoom = zoom;
        }
        #endregion

        #region Methods
        public DxfBitmap[] GetDxfBitmaps()
        {
            return new DxfBitmap[] { TopRightBitmap, TopLeftBitmap, BottomRightBitmap, BottomLeftBitmap };
        }
        public void DrawOverallBitmap()
        {
            _imagingFactory = new();
            _wicBitmap = new SharpDX.WIC.Bitmap(_imagingFactory, (int)(_deviceContext.Size.Width * Zoom), (int)(_deviceContext.Size.Height * Zoom), SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);

            //Debug.WriteLine($"_wicBitmap.Size: {_wicBitmap.Size.Width} {_wicBitmap.Size.Height}");

            _wicRenderTarget = new(_factory, _wicBitmap, new RenderTargetProperties())
            {
                DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            _wicRenderTarget.BeginDraw();
            _wicRenderTarget.Transform = _extentsMatrix;

            foreach (var layer in _layerManager.Layers.Values)
            {
                foreach (var drawingObject in layer.DrawingObjects)
                {
                    drawingObject.DrawToRenderTarget(_wicRenderTarget, drawingObject.Thickness, GetDrawingObjectBrush(drawingObject.Entity, _wicRenderTarget));
                }
            }
            _wicRenderTarget.EndDraw();

            Bitmap = Bitmap.FromWicBitmap(_deviceContext, _wicBitmap);
        }
        public void LoadQuadrantBitmaps()
        {

        }
        #endregion
    }
}
