using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache
    {
        #region Fields
        private readonly RenderTarget _renderTarget;
        private readonly Factory1 _factory;
        private Matrix _matrix;
        #endregion

        #region Properties
        public BitmapRenderTarget BitmapRenderTarget { get; set; }
        public float ZoomFactor { get; set; } = 1.0f;
        public Rect Extents { get; set; }
        public ObjectLayerManager LayerManager { get; set; } = new();
        public Size2F RenderTargetSize { get; set; }
        public float Scale { get; set; } = 1.0f;
        #endregion

        #region Constructors
        public BitmapCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Size2F renderTargetSize)
        {
            _renderTarget = renderTarget;
            LayerManager = layerManager;
            Extents = extents;
            _factory = factory;
            RenderTargetSize = renderTargetSize;

            InitializeBitmap();
        }
        #endregion

        #region Methods
        public void InitializeBitmap()
        {
            BitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, RenderTargetSize);

            GetInitialMatrix();
            DrawBitmapObjects();
        }
        public void ZoomToExtents()
        {

        }
        public void DrawBitmapObjects()
        {
            BitmapRenderTarget.BeginDraw();
            BitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 0.0f, 1.0f));
            BitmapRenderTarget.Transform = new RawMatrix3x2((float)_matrix.M11, (float)_matrix.M12, (float)_matrix.M21, (float)_matrix.M22, (float)_matrix.OffsetX, (float)_matrix.OffsetY);

            foreach (var layer in LayerManager.Layers.Values)
            {
                if (layer.IsVisible)
                {
                    foreach (var o in layer.DrawingObjects)
                    {
                        if (o is DrawingLine drawingLine)
                        {
                            BitmapRenderTarget.DrawLine(drawingLine.StartPoint, drawingLine.EndPoint, drawingLine.Brush, 1.0f, drawingLine.StrokeStyle);
                        }
                    }
                }
            }
            BitmapRenderTarget.EndDraw();
        }
        public void GetInitialMatrix() 
        {
            _matrix = new();

            double scaleX = RenderTargetSize.Width / Extents.Width;
            double scaleY = RenderTargetSize.Height / Extents.Height;

            _matrix.Translate(-Extents.Left, -Extents.Top);

            double centerX;
            double centerY;
            if (scaleX < scaleY)
            {
                Scale = (float)scaleX;

                centerX = Extents.Width * 0.5 * Scale;
                centerY = Extents.Height * 0.5 * Scale;
            }
            else
            {
                Scale = (float)scaleY;

                centerX = Extents.Width * 0.5 * Scale;
                centerY = Extents.Height * 0.5 * Scale;
            }
            double testCenterX = 0;
            double testCenterY = Extents.Height / 1.5;

            //_matrix.ScaleAt(Scale, -Scale, Extents.Width / 2, Extents.Height / 2);
            //_matrix.ScaleAt(Scale, -Scale, testCenterX, testCenterY);
            _matrix.ScaleAt(2, -2, 0, Extents.Height);
        }
        public void Dispose()
        {
            BitmapRenderTarget.Dispose();
        }
        #endregion
    }
}
