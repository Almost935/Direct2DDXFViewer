﻿using Direct2DControl;
using Direct2DDXFViewer.BitmapHelpers;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
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
using System.Windows.Media;
using Bitmap = SharpDX.Direct2D1.Bitmap;

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
                   
                    //WicRenderTarget wicRenderTarget = new(_factory, WicBitmap, properties,);
                    //wicRenderTarget.BeginDraw();
                    //RawRectangleF sourceRect = new((float)Bounds.Left, (float)Bounds.Top, (float)(Bounds.Left + (w * boundsWidth)), (float)(Bounds.Top + (h * boundsHeight)));
                    //wicRenderTarget.DrawBitmap(RootBitmap, 1, SharpDX.WIC.BitmapInterpolationMode.Linear, sourceRect);
                    //wicRenderTarget.EndDraw();


                    ImagingFactory imagingFactory = new ImagingFactory();
                    WicBitmap = new(imagingFactory, (int)bitmapWidth, (int)bitmapHeight, SharpDX.WIC.PixelFormat.Format32bppPRGBA, BitmapCreateCacheOption.CacheOnDemand);
                    RenderTargetProperties properties = new RenderTargetProperties();
                    WicRenderTarget target = new(_factory, WicBitmap, properties)
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

                    QuadTreeNode node = new(_factory, _deviceContext, objects, ZoomStep, Zoom, ExtentsMatrix, bounds, destRect, OverallSize, Levels, target.Bitmap, srcRect, _tempFileFolderPath);

                    Roots.Add(node);

                    target.Dispose();


                    //BitmapRenderTarget target = new(_deviceContext, CompatibleRenderTargetOptions.None, new Size2F(bitmapWidth, bitmapHeight))
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
                    //    obj.DrawToRenderTarget(target, 1, obj.Brush, obj.HairlineStrokeStyle);
                    //}
                    //target.EndDraw();

                    //RootBitmap = target.Bitmap;
                    //QuadTreeNode node = new(_factory, _deviceContext, objects, ZoomStep, Zoom, ExtentsMatrix, bounds, destRect, OverallSize, Levels, target.Bitmap, srcRect, _tempFileFolderPath);

                    //Roots.Add(node);

                    //target.Dispose();
                }
            }
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
