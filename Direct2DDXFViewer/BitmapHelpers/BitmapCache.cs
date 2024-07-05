using Direct2DDXFViewer.DrawingObjects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache
    {
        #region Fields
        private readonly RenderTarget _renderTarget;
        
        private BitmapRenderTarget _bitmapRenderTarget;
        #endregion

        #region Properties
        public float ZoomFactor { get; set; }
        public ObjectLayerManager LayerManager { get; set; } = new();
        #endregion

        #region Constructors
        BitmapCache(RenderTarget renderTarget, ObjectLayerManager layerManager, float zoomFactor)
        {
            _renderTarget = renderTarget;
            LayerManager = layerManager;
            ZoomFactor = zoomFactor;
        }
        #endregion

        #region Methods
        public void InitializeBitmap()
        {
           _bitmapRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, new Size2(1000, 1000));
        }
        public void Dispose()
        {
            _bitmapRenderTarget.Dispose();
        }
        #endregion
    }
}
