using Direct2DControl;
using Direct2DDXFViewer.DrawingObjects;
using SharpDX.Direct2D1;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace Direct2DDXFViewer
{
    public class QuadTreeCache
    {
        #region Fields
        RenderTarget _renderTarget;
        Factory1 _factory;
        ObjectLayerManager _layerManager;
        Rect _extents;
        Size2F _renderTargetSize;
        Matrix _extentsMatrix;
        ResourceCache _resCache;
        #endregion

        #region Properties
        public Dictionary<float, QuadTree> QuadTrees { get; private set; }
        #endregion

        #region Constructors
        public QuadTreeCache(RenderTarget renderTarget, Factory1 factory, ObjectLayerManager layerManager, Rect extents, Size2F renderTargetSize, Matrix extentsMatrix, ResourceCache resCache)
        {
            _renderTarget = renderTarget;
            _layerManager = layerManager;
            _extents = extents;
            _factory = factory;
            _renderTargetSize = renderTargetSize;
            _extentsMatrix = extentsMatrix;
            _resCache = resCache;

            QuadTrees = new Dictionary<float, QuadTree>();
        }
        #endregion

        #region Methods
        public void GetQuadTree(float zoom)
        {
            if (QuadTrees.ContainsKey(zoom)) { return; }

            using (var childRenderTarget = new BitmapRenderTarget(_renderTarget, CompatibleRenderTargetOptions.None, _renderTargetSize))
            {
                childRenderTarget.BeginDraw();
                childRenderTarget.Transform = _extentsMatrix;
                _layerManager.Draw(childRenderTarget, _extents, zoom, _resCache);
                childRenderTarget.EndDraw();
                childRenderTarget.Flush();

                QuadTrees.Add(zoom, new QuadTree(childRenderTarget, 3));
            }
        }
        #endregion
    }
}
