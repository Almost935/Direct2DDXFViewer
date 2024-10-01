                                                                                         using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using netDxf.Tables;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Bitmap = SharpDX.Direct2D1.Bitmap;
using Point = System.Windows.Point;

namespace Direct2DDXFViewer
{
    public class QuadTree : IDisposable
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
        private int _maxBitmapSize;
        private string _tempPath;
        private string _tempFileFolderPath;
        private List<Bitmap> _overallBitmaps = new();
        private bool _disposed = false;
        private int _maxQuadNodeSize;
        private int _bitmapReuseFactor;
        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = new();
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect Bounds { get; set; }
        public Rect DestRect { get; set; }
        public Size2F OverallSize { get; set; }
        public List<QuadTreeNode> Roots { get; set; } = new();
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        public bool BitmapsLoaded { get; set; } = false;
        #endregion

        #region Constructors
        public QuadTree(Factory1 factory, DeviceContext1 deviceContext, ObjectLayerManager layerManager, Size2F overallSize, RawMatrix3x2 extentsMatrix, Rect bounds, Rect destRect, int zoomStep, float zoom, int maxBitmapSize, int maxQuadNodeSize, int bitmapReuseFactor, string tempPath)
        {
            _factory = factory;
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            OverallSize = overallSize;
            ExtentsMatrix = extentsMatrix;
            Bounds = bounds;
            DestRect = destRect;
            ZoomStep = zoomStep;
            Zoom = zoom;
            _maxBitmapSize = maxBitmapSize;
            _maxQuadNodeSize = maxQuadNodeSize;
            _bitmapReuseFactor = bitmapReuseFactor;
            _tempPath = tempPath;

            GetTempFilePath();
            Initialize();
        }
        #endregion

        #region Methods
        private void Initialize()
        {
            GetRoots();
        }
        private void GetTempFilePath()
        {
            // Get the path to the temporary files directory
            string folderName = $"{ZoomStep}";

            // Combine the temporary path with the folder name
            _tempFileFolderPath = Path.Combine(_tempPath, folderName);

            // Check if the directory already exists
            if (Directory.Exists(_tempFileFolderPath))
            {
                Directory.Delete(_tempFileFolderPath, true);
            }
            Directory.CreateDirectory(_tempFileFolderPath);
        }
        public void GetRoots()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            //// Calculate number of divisions required to be below the min in each direction
            //(int x, int y) divisions = MathHelpers.GetRequiredQuadTreeLevels(OverallSize, _maxQuadNodeSize);

            //float bitmapWidth = (float)(OverallSize.Width / (divisions.x + 1));
            //float bitmapHeight = (float)(OverallSize.Height / (divisions.y + 1));
            //Size2F size = new(bitmapWidth, bitmapHeight);

            //double destWidth = DestRect.Width / divisions.x;
            //double destHeight = DestRect.Height / divisions.y;

            //double boundsWidth = Bounds.Width / divisions.x;
            //double boundsHeight = Bounds.Height / divisions.y;

            (int x, int y) divisions = (1, 1);
            float wDim = OverallSize.Width;
            float hDim = OverallSize.Height;
            while (wDim >= _maxBitmapSize)
            {
                wDim /= 2;
                divisions.x++;
            }
            while (hDim >= _maxBitmapSize)
            {
                hDim /= 2;
                divisions.y++;
            }

            //float limitingDim = Math.Max(OverallSize.Width, OverallSize.Height);
            //int bitmapSplit = 0;
            //while (limitingDim > _maxBitmapSize)
            //{
            //    limitingDim /= 2;
            //    bitmapSplit++;
            //}
            //int numOfOverallBitmaps = (int)(Math.Pow(2, bitmapSplit));

            float bitmapWidth = (float)(OverallSize.Width / divisions.x);
            float bitmapHeight = (float)(OverallSize.Height / divisions.y);
            Size2F size = new(bitmapWidth, bitmapHeight);

            double destWidth = DestRect.Width / divisions.x;
            double destHeight = DestRect.Height / divisions.y;

            double boundsWidth = Bounds.Width / divisions.x;
            double boundsHeight = Bounds.Height / divisions.y;

            for (int w = 0; w < divisions.x; w++) // width
            {
                for (int h = 0; h < divisions.y; h++) // height
                {
                    BitmapRenderTarget target = new(_deviceContext, CompatibleRenderTargetOptions.None, size)
                    {
                        DotsPerInch = new(96 * Zoom, 96 * Zoom),
                        AntialiasMode = AntialiasMode.PerPrimitive
                    };
                    Rect destRect = new(DestRect.Left + destWidth * w, DestRect.Top + destHeight * h, destWidth, destHeight);
                    Rect srcRect = new(bitmapWidth * w, bitmapHeight * h, bitmapWidth, bitmapHeight);
                    Rect bounds = new(Bounds.Left + boundsWidth * w, Bounds.Top + boundsHeight * h, boundsWidth, boundsHeight);

                    List<DrawingObject> objects = _layerManager.GetDrawingObjectsinRect(bounds);
                    RawMatrix3x2 matrix = new((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, 
                        (float)(ExtentsMatrix.M31 - bitmapWidth * w), (float)(ExtentsMatrix.M32 - bitmapHeight * h));
                    target.BeginDraw();
                    target.Transform = matrix;
                    
                    foreach (var obj in objects)
                    {
                        obj.DrawToRenderTarget(target, 1, obj.Brush, obj.HairlineStrokeStyle);
                    }
                    target.EndDraw();

                    _overallBitmaps.Add(target.Bitmap);

                    (int x, int y) nodeDivisions = MathHelpers.GetRequiredQuadTreeLevels(size, _maxBitmapSize);
                    QuadTreeNode node = new(_factory, _deviceContext, objects, ZoomStep, Zoom, ExtentsMatrix, bounds, destRect, size, 
                        (nodeDivisions.x > nodeDivisions.y) ? nodeDivisions.x : nodeDivisions.y, 
                        target.Bitmap, srcRect, _tempFileFolderPath);

                    Roots.Add(node);

                    target.Dispose();
                }
            }
            Debug.WriteLine($"Roots.Count: {Roots.Count}");
            foreach (var bitmap in _overallBitmaps) { bitmap.Dispose(); }
            _overallBitmaps.Clear();

            BitmapsLoaded = true;

            stopwatch.Stop();
            Debug.WriteLine($"GetRoots Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        public void DisposeBitmaps()
        {
            foreach (var root in Roots)
            {
                root.DisposeBitmaps();
            }
            BitmapsLoaded = false;
        }
        public void LoadBitmaps()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (var root in Roots)
            {
                root.LoadBitmap();
            }
            BitmapsLoaded = true;

            stopwatch.Stop();
            //Debug.WriteLine($"LoadBitmaps Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void GetDrawingObjects()
        {
            foreach (var layer in _layerManager.Layers.Values)
            {
                DrawingObjects.AddRange(layer.DrawingObjects);
            }
        }
        public List<QuadTreeNode> GetIntersectingNodes(Rect view)
        {
            List<QuadTreeNode> quadTreeNodes = new();

            foreach (var root in Roots)
            {
                quadTreeNodes.AddRange(root.GetIntersectingQuadTreeNodes(view));
            }

            return quadTreeNodes;
        }
        public List<QuadTreeNode> GetIntersectingNodes(Point p)
        {
            List<QuadTreeNode> quadTreeNodes = new();

            foreach (var root in Roots)
            {
                quadTreeNodes.AddRange(root.GetNodeAtPoint(p));
            }

            return quadTreeNodes;
        }
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    foreach (var root in Roots)
                    {
                        root.Dispose();
                    }
                    Roots.Clear();
                }

                // Free unmanaged resources (if any)

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
