using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using Direct2DControl;
using System.Xml.Linq;
using System.Windows.Media;
using Direct2DDXFViewer.DrawingObjects;
using netDxf;
using Direct2DDXFViewer.Helpers;
using netDxf.Tables;
using Direct2DDXFViewer.BitmapHelpers;
using SharpDX.WIC;

using Bitmap = SharpDX.Direct2D1.Bitmap;
using BitmapInterpolationMode = SharpDX.Direct2D1.BitmapInterpolationMode;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using System.IO;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using System.Text.Json;

namespace Direct2DDXFViewer
{
    public class QuadTreeNode
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        private string _tempFileFolderPath;
        private string _filePath;
        private DataRectangle _dataRect;
        #endregion

        #region Properties
        public Bitmap Bitmap { get; set; }
        public SharpDX.WIC.Bitmap WicBitmap { get; set; }
        public Bitmap RootBitmap { get; set; }
        public List<DrawingObject> DrawingObjects { get; set; } = [];
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        public RawMatrix3x2 ExtentsMatrix { get; set; }
        public Rect Bounds { get; set; }
        public Rect DestRect { get; set; }
        public Rect SourceRect { get; set; }
        public Size2F Size { get; set; }
        public int Level { get; set; }
        public QuadTreeNode[] ChildNodes { get; set; }
        public bool BitmapSaved { get; set; } = false;
        #endregion

        #region Constructors
        public QuadTreeNode(Factory1 factory, DeviceContext1 deviceContext, List<DrawingObject> drawingObjects, int zoomStep, float zoom, RawMatrix3x2 extentsMatrix, Rect bounds, Rect destRect, Size2F size, int level, Bitmap rootBitmap, Rect srcRect, string tempFileFolderPath)
        {
            //Stopwatch stopwatch = Stopwatch.StartNew();
            //Debug.WriteLine($"\nQuadTreeNode Begin: ZoomStep: {zoomStep} Level: {Level}");

            _factory = factory;
            _deviceContext = deviceContext;
            DrawingObjects = drawingObjects;
            ZoomStep = zoomStep;
            Zoom = zoom;
            ExtentsMatrix = extentsMatrix;
            Bounds = bounds;
            DestRect = destRect;
            Size = size;
            Level = level;
            RootBitmap = rootBitmap;
            SourceRect = srcRect;
            tempFileFolderPath = tempFileFolderPath;
            _filePath = Path.Combine(tempFileFolderPath, $"{Guid.NewGuid()}.png");

            Subdivide();

            //stopwatch.Stop();
            //Debug.WriteLine($"QuadTreeNode End: ZoomStep: {zoomStep} Level: {Level} time: {stopwatch.ElapsedMilliseconds} ms");
        }
        #endregion

        #region Methods
        public List<QuadTreeNode> GetIntersectingQuadTreeNodes(Rect view)
        {
            List<QuadTreeNode> intersectingNodes = [];

            if (MathHelpers.RectsIntersect(view, DestRect))
            {
                if (ChildNodes is null)
                {
                    intersectingNodes.Add(this);
                }
                else
                {
                    foreach (var child in ChildNodes)
                    {
                        intersectingNodes.AddRange(child.GetIntersectingQuadTreeNodes(view));
                    }
                }
            }
            return intersectingNodes;
        }
        public List<QuadTreeNode> GetNodeAtPoint(Point p)
        {
            List<QuadTreeNode> nodes = new();

            if (Bounds.Contains(p))
            {
                if (Level == 0)
                {
                    nodes.Add(this);
                }
                else
                {
                    foreach (var child in ChildNodes)
                    {
                        nodes.AddRange(child.GetNodeAtPoint(p));
                    }
                }
            }
            return nodes;
        }
        public void DrawBitmap()
        {
            //Stopwatch stopwatch = Stopwatch.StartNew();
            //Debug.WriteLine($"\nDrawBitmap Begin: ZoomStep: {ZoomStep}");

            //BitmapRenderTarget bitmapRenderTarget = new(_deviceContext, CompatibleRenderTargetOptions.None, Size)
            //{
            //    AntialiasMode = AntialiasMode.PerPrimitive
            //};

            //bitmapRenderTarget.BeginDraw();
            //bitmapRenderTarget.Clear(new RawColor4());

            //RawRectangleF sourceRect = new((float)SourceRect.Left, (float)SourceRect.Top, (float)SourceRect.Right, (float)SourceRect.Bottom);
            //bitmapRenderTarget.DrawBitmap(RootBitmap, 1, BitmapInterpolationMode.Linear, sourceRect);

            ////Debug.WriteLineIf(ZoomStep == 0, $"QuadTree: {SourceRect} {DestRect}");

            //Bitmap = bitmapRenderTarget.Bitmap;
            //bitmapRenderTarget.EndDraw();
            //bitmapRenderTarget.Dispose();

            //SaveBitmap();

            //stopwatch.Stop();
            //Debug.WriteLine($"DrawBitmap End: ZoomStep: {ZoomStep} time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void Subdivide()
        {
            if (Level > 0)
            {
                ChildNodes = new QuadTreeNode[4];

                // Represents which quandrant each of the 1-4 is in
                Point factor1 = new(0, 0);
                Point factor2 = new(1, 0);
                Point factor3 = new(0, 1);
                Point factor4 = new(1, 1);

                // Represents the dxf coordinate bounds of each quadrant.
                Size halfBoundsSize = new(Bounds.Width / 2, Bounds.Height / 2);
                Rect bounds1 = new(Bounds.Left + (halfBoundsSize.Width * factor1.X), Bounds.Top + (halfBoundsSize.Height * factor1.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds2 = new(Bounds.Left + (halfBoundsSize.Width * factor2.X), Bounds.Top + (halfBoundsSize.Height * factor2.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds3 = new(Bounds.Left + (halfBoundsSize.Width * factor3.X), Bounds.Top + (halfBoundsSize.Height * factor3.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds4 = new(Bounds.Left + (halfBoundsSize.Width * factor4.X), Bounds.Top + (halfBoundsSize.Height * factor4.Y), halfBoundsSize.Width, halfBoundsSize.Height);

                // Represents the destination rectangle of each quadrant.
                Size halfDestRectSize = new(DestRect.Width / 2, DestRect.Height / 2);
                Rect destRect1 = new(DestRect.Left + (halfDestRectSize.Width * factor1.X), DestRect.Top + (halfDestRectSize.Height * factor1.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect2 = new(DestRect.Left + (halfDestRectSize.Width * factor2.X), DestRect.Top + (halfDestRectSize.Height * factor2.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect3 = new(DestRect.Left + (halfDestRectSize.Width * factor3.X), DestRect.Top + (halfDestRectSize.Height * factor3.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect4 = new(DestRect.Left + (halfDestRectSize.Width * factor4.X), DestRect.Top + (halfDestRectSize.Height * factor4.Y), halfDestRectSize.Width, halfDestRectSize.Height);

                // Represents the source rectangle of each quadrant4
                Size bitmapSize = new(RootBitmap.Size.Width, RootBitmap.Size.Height);
                Size halfSrcSize = new(SourceRect.Width / 2, SourceRect.Height / 2);
                Rect srcRect1 = new(SourceRect.Left + (halfSrcSize.Width * factor1.X), SourceRect.Top + (halfSrcSize.Height * factor1.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect2 = new(SourceRect.Left + (halfSrcSize.Width * factor2.X), SourceRect.Top + (halfSrcSize.Height * factor2.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect3 = new(SourceRect.Left + (halfSrcSize.Width * factor3.X), SourceRect.Top + (halfSrcSize.Height * factor3.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect4 = new(SourceRect.Left + (halfSrcSize.Width * factor4.X), SourceRect.Top + (halfSrcSize.Height * factor4.Y), halfSrcSize.Width, halfSrcSize.Height);

                // Matrices to make each quadrant's drawing objects appear in the correct location
                RawMatrix3x2 m1 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor1.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor1.Y));
                RawMatrix3x2 m2 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor2.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor2.Y));
                RawMatrix3x2 m3 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor3.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor3.Y));
                RawMatrix3x2 m4 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor4.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor4.Y));

                List<DrawingObject> objects1 = [];
                List<DrawingObject> objects2 = [];
                List<DrawingObject> objects3 = [];
                List<DrawingObject> objects4 = [];

                //Stopwatch stopwatch = Stopwatch.StartNew();
                //Debug.WriteLine($"\nDrawingObject Seperation Begin: ZoomStep: {ZoomStep} Level: {Level}");

                foreach (var drawingObject in DrawingObjects)
                {
                    if (drawingObject.DrawingObjectIsInRect(bounds1))
                    {
                        objects1.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds2))
                    {
                        objects2.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds3))
                    {
                        objects3.Add(drawingObject);
                    }
                    if (drawingObject.DrawingObjectIsInRect(bounds4))
                    {
                        objects4.Add(drawingObject);
                    }
                }

                //stopwatch.Stop();
                //Debug.WriteLine($"DrawingObject Seperation End: ZoomStep: {ZoomStep} time: {stopwatch.ElapsedMilliseconds} ms");

                Size2F size = new(Size.Width / 2, Size.Height / 2);

                ChildNodes[0] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m1, bounds1, destRect1, size, Level - 1, RootBitmap, srcRect1, _tempFileFolderPath);
                ChildNodes[1] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m2, bounds2, destRect2, size, Level - 1, RootBitmap, srcRect2, _tempFileFolderPath);
                ChildNodes[2] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m3, bounds3, destRect3, size, Level - 1, RootBitmap, srcRect3, _tempFileFolderPath);
                ChildNodes[3] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m4, bounds4, destRect4, size, Level - 1, RootBitmap, srcRect4, _tempFileFolderPath);
            }
            else // if Level == 0, this means the node is the final leaf node and thus will be used to draw
            {
                DrawBitmap();
            }
        }
        public void SaveBitmap()
        {
            //Size2 size = Bitmap.PixelSize;
            //var pixelFormat1 = Bitmap.PixelFormat.Format; // Use the original bitmap's pixel format
            //var bitmapProperties = new BitmapProperties1
            //{
            //    PixelFormat = new PixelFormat(pixelFormat1, AlphaMode.Premultiplied),
            //    DpiX = 96 * Zoom,
            //    DpiY = 96 * Zoom
            //};
            //Bitmap1 bitmap = new(_deviceContext, size, bitmapProperties);

            //Bitmap1 bitmap1 = new(_deviceContext, Bitmap.PixelSize, bitmapProperties);
            //int x = 0;
            //bitmap1.CopyFromBitmap(Bitmap);

            var wicFactory = new ImagingFactory();
            SharpDX.WIC.Bitmap wicBitmap = new(wicFactory, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height,
                SharpDX.WIC.PixelFormat.Format32bppPRGBA, BitmapCreateCacheOption.CacheOnLoad);
            var rtp = new RenderTargetProperties(new PixelFormat(SharpDX.DXGI.Format.B8G8R8X8_UNorm, AlphaMode.Premultiplied));

            WicRenderTarget wicRenderTarget = new(_factory, wicBitmap, rtp);
            wicRenderTarget.BeginDraw();
            wicRenderTarget.DrawBitmap(Bitmap, 1, BitmapInterpolationMode.Linear);
            wicRenderTarget.EndDraw();

            MemoryStream memoryStream = new();
            WICStream wicStream = new(wicFactory, memoryStream);

            BitmapEncoder encoder = new(wicFactory, ContainerFormatGuids.Png, wicStream);
            encoder.Initialize(wicStream);

            BitmapFrameEncode frameEncoder = new(encoder);
            frameEncoder.Initialize();
            frameEncoder.SetSize(Bitmap.PixelSize.Width, Bitmap.PixelSize.Height);
            var pixelFormat = SharpDX.WIC.PixelFormat.FormatDontCare;
            frameEncoder.SetPixelFormat(ref pixelFormat);

            frameEncoder.WriteSource(wicBitmap);
            frameEncoder.Commit();
            encoder.Commit();

            using (var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write))
            {
                memoryStream.CopyTo(fileStream);
            }

            frameEncoder.Dispose();
            encoder.Dispose();
            wicStream.Dispose();
            wicBitmap.Dispose();
            wicRenderTarget.Dispose();
            wicFactory.Dispose();
        }
        public void LoadBitmap()
        {

        }
        #endregion
    }
}

