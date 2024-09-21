using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using netDxf.Tables;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
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
    public class QuadTree
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
        private int _maxBitmapSize;
        private string _tempPath;
        private string _tempFileFolderPath;
        private Dictionary<(byte r, byte g, byte b, byte a), Brush> _brushes = new();

        #endregion

        #region Properties
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect Bounds { get; set; }
        public Rect DestRect { get; set; }
        public Size2F OverallSize { get; set; }
        public int Levels { get; set; }
        public List<QuadTreeNode> Roots { get; set; } = [];
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        public Bitmap RootBitmap { get; set; }
        public SharpDX.WIC.Bitmap WicBitmap { get; set; }
        #endregion

        #region Constructors
        public QuadTree(Factory1 factory, DeviceContext1 deviceContext, ObjectLayerManager layerManager, Size2F overallSize, RawMatrix3x2 extentsMatrix, Rect bounds, Rect destRect, int zoomStep, float zoom, int maxBitmapSize, string tempPath)
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
            _tempPath = tempPath;

            GetTempFilePath();
            GetRequiredLevels();
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
            float limitingDim = Math.Max(OverallSize.Width, OverallSize.Height);
            int bitmapSplit = 0;
            while (limitingDim > _maxBitmapSize)
            {
                limitingDim /= 2;
                bitmapSplit++;
            }

            int divisions = (int)(Math.Pow(2, bitmapSplit));
            float bitmapWidth = (float)(OverallSize.Width / divisions);
            float bitmapHeight = (float)(OverallSize.Height / divisions);
            double destWidth = DestRect.Width / divisions;
            double destHeight = DestRect.Height / divisions;

            double boundsWidth = Bounds.Width / divisions;
            double boundsHeight = Bounds.Height / divisions;

            for (int w = 0; w < divisions; w++) // width
            {
                for (int h = 0; h < divisions; h++) // height
                {
                    //ImagingFactory imagingFactory = new ImagingFactory();
                    //WicBitmap = new(imagingFactory, (int)bitmapWidth, (int)bitmapHeight, SharpDX.WIC.PixelFormat.Format32bppPRGBA, BitmapCreateCacheOption.CacheOnDemand);
                    //RenderTargetProperties properties = new RenderTargetProperties();
                    //WicRenderTarget target = new(_factory, WicBitmap, properties)
                    //{
                    //    DotsPerInch = new(96 * Zoom, 96 * Zoom),
                    //    AntialiasMode = AntialiasMode.PerPrimitive
                    //};
                    //Rect destRect = new(DestRect.Left + destWidth * w, DestRect.Top + destHeight * h, destWidth, destHeight);
                    //Rect srcRect = new(bitmapWidth * w, bitmapHeight * h, bitmapWidth, bitmapHeight);
                    //Rect bounds = new(Bounds.Left + boundsWidth * w, Bounds.Top + boundsHeight * h, boundsWidth, boundsHeight);

                    //List<DrawingObject> objects = _layerManager.GetDrawingObjectsinRect(bounds);

                    //RawMatrix3x2 matrix = new((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)(ExtentsMatrix.M31 - bitmapWidth * w), (float)(ExtentsMatrix.M32 - bitmapHeight * h));
                    //target.BeginDraw();
                    //target.Transform = matrix;

                    //foreach (var obj in objects)
                    //{
                    //    Brush brush = GetDrawingObjectBrush(obj.Entity, target);
                    //    obj.DrawToRenderTarget(target, 1, brush, obj.HairlineStrokeStyle);
                    //}
                    //target.EndDraw();

                    //QuadTreeNode node = new(_factory, _deviceContext, objects, ZoomStep, Zoom, ExtentsMatrix, bounds, destRect, OverallSize, Levels, WicBitmap, srcRect, _tempFileFolderPath);
                    //Roots.Add(node);

                    //target.Dispose();
                    //foreach (var brush in _brushes.Values) { brush.Dispose(); }
                    //_brushes.Clear();


                    BitmapRenderTarget target = new(_deviceContext, CompatibleRenderTargetOptions.None, new Size2F(bitmapWidth, bitmapHeight))
                    {
                        DotsPerInch = new(96 * Zoom, 96 * Zoom),
                        AntialiasMode = AntialiasMode.PerPrimitive
                    };
                    Rect destRect = new(DestRect.Left + destWidth * w, DestRect.Top + destHeight * h, destWidth, destHeight);
                    Rect srcRect = new(bitmapWidth * w, bitmapHeight * h, bitmapWidth, bitmapHeight);
                    Rect bounds = new(Bounds.Left + boundsWidth * w, Bounds.Top + boundsHeight * h, boundsWidth, boundsHeight);

                    List<DrawingObject> objects = _layerManager.GetDrawingObjectsinRect(bounds);

                    RawMatrix3x2 matrix = new((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)(ExtentsMatrix.M31 - bitmapWidth * w), (float)(ExtentsMatrix.M32 - bitmapHeight * h));
                    target.BeginDraw();
                    target.Transform = matrix;
                    foreach (var obj in objects)
                    {
                        obj.DrawToRenderTarget(target, 1, obj.Brush, obj.HairlineStrokeStyle);
                    }
                    target.EndDraw();

                    RootBitmap = target.Bitmap;
                    QuadTreeNode node = new(_factory, _deviceContext, objects, ZoomStep, Zoom, ExtentsMatrix, bounds, destRect, OverallSize, Levels, target.Bitmap, srcRect, _tempFileFolderPath);

                    Roots.Add(node);

                    target.Dispose();
                }
            }
        }

        private Brush GetDrawingObjectBrush(EntityObject entity, WicRenderTarget target)
        {
            byte r, g, b, a;
            if (entity.Color.IsByLayer)
            {
                if (entity.Layer.Color.R == 255 && entity.Layer.Color.G == 255 && entity.Layer.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Layer.Color.R; g = entity.Layer.Color.G; b = entity.Layer.Color.B; a = 255;
                }
            }
            else
            {
                if (entity.Color.R == 255 && entity.Color.G == 255 && entity.Color.B == 255)
                {
                    r = g = b = 0; a = 255;
                }
                else
                {
                    r = entity.Color.R; g = entity.Color.G; b = entity.Color.B; a = 255;
                }
            }

            bool brushExists = _brushes.TryGetValue((r, g, b, a), out Brush brush);
            if (!brushExists)
            {
                brush = new SolidColorBrush(target, new RawColor4((float)r / 255, (float)g / 255, (float)b / 255, 1.0f));
                _brushes.Add((r, g, b, a), brush);
            }

            return brush;
        }
        public void GetRequiredLevels()
        {
            double limitingFactor = new List<double>() { Zoom * _deviceContext.Size.Width, Zoom * _deviceContext.Size.Height }.Max();

            Levels = (int)Math.Floor(limitingFactor / _maxBitmapSize);
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
            List<QuadTreeNode> quadTreeNodes = [];

            foreach (var root in Roots)
            {
                quadTreeNodes.AddRange(root.GetIntersectingQuadTreeNodes(view));
            }

            return quadTreeNodes;
        }
        public List<QuadTreeNode> GetIntersectingNodes(Point p)
        {
            List<QuadTreeNode> quadTreeNodes = [];

            foreach (var root in Roots)
            {
                quadTreeNodes.AddRange(root.GetNodeAtPoint(p));
            }

            return quadTreeNodes;
        }
        #endregion
    }
}
