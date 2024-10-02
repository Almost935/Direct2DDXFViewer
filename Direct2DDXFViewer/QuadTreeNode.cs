using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX;
using System;
using System.Drawing.Imaging;
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

using Bitmap = SharpDX.Direct2D1.Bitmap;
using BitmapInterpolationMode = SharpDX.Direct2D1.BitmapInterpolationMode;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using System.IO;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpDX.IO;

namespace Direct2DDXFViewer
{
    public class QuadTreeNode : IDisposable
    {
        #region Fields
        private Factory1 _factory;
        private DeviceContext1 _deviceContext;
        private string _tempFileFolderPath;
        private string _filePath = null;
        private int _pitch;
        private bool _disposed = false; // To detect redundant calls
        #endregion

        #region Properties
        public Bitmap1 Bitmap { get; set; }
        public Bitmap RootBitmap { get; set; }
        public List<DrawingObject> DrawingObjects { get; set; } = new();
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
            _tempFileFolderPath = tempFileFolderPath;

            Subdivide();
        }
        #endregion

        #region Methods
        public List<QuadTreeNode> GetIntersectingQuadTreeNodes(Rect view)
        {
            List<QuadTreeNode> intersectingNodes = new();

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
            Stopwatch stopwatch = Stopwatch.StartNew();

            BitmapProperties1 bitmapProperties = new(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            Bitmap = new(_deviceContext, new Size2((int)Size.Width, (int)Size.Height), bitmapProperties);
            
            RawPoint destPoint = new(0, 0);  
            RawRectangle sourceRect = new((int)SourceRect.Left, (int)SourceRect.Top, (int)SourceRect.Right, (int)SourceRect.Bottom);
            Bitmap.CopyFromBitmap(RootBitmap, destPoint, sourceRect);

            SaveBitmap();

            stopwatch.Stop();
            Debug.WriteLine($"ZoomStep {ZoomStep} DrawBitmap time: {stopwatch.ElapsedMilliseconds}");
        }

        private void SaveBitmap()
        {
            _filePath = Path.Combine(_tempFileFolderPath, $"{Guid.NewGuid()}.bmp");

            DataRectangle dataRectangle = Bitmap.Map(MapOptions.Read);
            _pitch = dataRectangle.Pitch;

            using (DataStream dataStream = new(dataRectangle.DataPointer, dataRectangle.Pitch * Bitmap.PixelSize.Height, true, false))
            {
                using (FileStream fileStream = new(_filePath, FileMode.Create, FileAccess.Write))
                {
                    dataStream.CopyTo(fileStream);
                }
            }
            Bitmap.Unmap();

            BitmapSaved = true;

            LoadBitmap(); // Reload bitmap from file with CpuRead and CannotDraw flag removed so that i
        }

        public void LoadBitmap()
        {
            if (Level == 0 && _filePath is not null)
            {
                using FileStream fileStream = new(_filePath, FileMode.Open, FileAccess.Read);
                using DataStream dataStream = new((int)fileStream.Length, true, true);

                fileStream.CopyTo(dataStream);
                dataStream.Position = 0;

                BitmapProperties1 bitmapProperties = new(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96);
                Bitmap = new(_deviceContext, new Size2((int)Size.Width, (int)Size.Height), dataStream, _pitch, bitmapProperties);
            }
            else
            {
                foreach (var child in ChildNodes)
                {
                    child.LoadBitmap();
                }
            }
        }

        public void DisposeBitmaps()
        {
            if (Bitmap != null)
            {
                if (!BitmapSaved)
                {
                    SaveBitmap();
                }

                Bitmap.Dispose();
                Bitmap = null;
            }
            if (ChildNodes is not null)
            {
                foreach (var child in ChildNodes)
                {
                    child.DisposeBitmaps();
                }
            }
        }

        private void Subdivide()
        {
            if (Level > 0)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                ChildNodes = new QuadTreeNode[4];

                Point factor1 = new(0, 0);
                Point factor2 = new(1, 0);
                Point factor3 = new(0, 1);
                Point factor4 = new(1, 1);

                Size halfBoundsSize = new(Bounds.Width / 2, Bounds.Height / 2);
                Rect bounds1 = new(Bounds.Left + (halfBoundsSize.Width * factor1.X), Bounds.Top + (halfBoundsSize.Height * factor1.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds2 = new(Bounds.Left + (halfBoundsSize.Width * factor2.X), Bounds.Top + (halfBoundsSize.Height * factor2.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds3 = new(Bounds.Left + (halfBoundsSize.Width * factor3.X), Bounds.Top + (halfBoundsSize.Height * factor3.Y), halfBoundsSize.Width, halfBoundsSize.Height);
                Rect bounds4 = new(Bounds.Left + (halfBoundsSize.Width * factor4.X), Bounds.Top + (halfBoundsSize.Height * factor4.Y), halfBoundsSize.Width, halfBoundsSize.Height);

                Size halfDestRectSize = new(DestRect.Width / 2, DestRect.Height / 2);
                Rect destRect1 = new(DestRect.Left + (halfDestRectSize.Width * factor1.X), DestRect.Top + (halfDestRectSize.Height * factor1.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect2 = new(DestRect.Left + (halfDestRectSize.Width * factor2.X), DestRect.Top + (halfDestRectSize.Height * factor2.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect3 = new(DestRect.Left + (halfDestRectSize.Width * factor3.X), DestRect.Top + (halfDestRectSize.Height * factor3.Y), halfDestRectSize.Width, halfDestRectSize.Height);
                Rect destRect4 = new(DestRect.Left + (halfDestRectSize.Width * factor4.X), DestRect.Top + (halfDestRectSize.Height * factor4.Y), halfDestRectSize.Width, halfDestRectSize.Height);

                Size halfSrcSize = new(SourceRect.Width / 2, SourceRect.Height / 2);
                Rect srcRect1 = new(SourceRect.Left + (halfSrcSize.Width * factor1.X), SourceRect.Top + (halfSrcSize.Height * factor1.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect2 = new(SourceRect.Left + (halfSrcSize.Width * factor2.X), SourceRect.Top + (halfSrcSize.Height * factor2.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect3 = new(SourceRect.Left + (halfSrcSize.Width * factor3.X), SourceRect.Top + (halfSrcSize.Height * factor3.Y), halfSrcSize.Width, halfSrcSize.Height);
                Rect srcRect4 = new(SourceRect.Left + (halfSrcSize.Width * factor4.X), SourceRect.Top + (halfSrcSize.Height * factor4.Y), halfSrcSize.Width, halfSrcSize.Height);

                RawMatrix3x2 m1 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor1.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor1.Y));
                RawMatrix3x2 m2 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor2.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor2.Y));
                RawMatrix3x2 m3 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor3.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor3.Y));
                RawMatrix3x2 m4 = new(ExtentsMatrix.M11, ExtentsMatrix.M12, ExtentsMatrix.M21, ExtentsMatrix.M22,
                            ExtentsMatrix.M31 - (float)(halfDestRectSize.Width * factor4.X), ExtentsMatrix.M32 - (float)(halfDestRectSize.Height * factor4.Y));

                List<DrawingObject> objects1 = new();
                List<DrawingObject> objects2 = new();
                List<DrawingObject> objects3 = new();
                List<DrawingObject> objects4 = new();

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

                Size2F size = new(Size.Width / 2, Size.Height / 2);

                ChildNodes[0] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m1, bounds1, destRect1, size, Level - 1, RootBitmap, srcRect1, _tempFileFolderPath);
                ChildNodes[1] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m2, bounds2, destRect2, size, Level - 1, RootBitmap, srcRect2, _tempFileFolderPath);
                ChildNodes[2] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m3, bounds3, destRect3, size, Level - 1, RootBitmap, srcRect3, _tempFileFolderPath);
                ChildNodes[3] = new(_factory, _deviceContext, DrawingObjects, ZoomStep, Zoom, m4, bounds4, destRect4, size, Level - 1, RootBitmap, srcRect4, _tempFileFolderPath);

                stopwatch.Stop();
                Debug.WriteLine($"ZoomStep {ZoomStep} Level: {Level} Subdivide time: {stopwatch.ElapsedMilliseconds}");
            }
            else
            {
                DrawBitmap();
            }
        } 

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    DisposeBitmaps();
                    if (ChildNodes != null)
                    {
                        foreach (var child in ChildNodes)
                        {
                            child.Dispose();
                        }
                    }
                }
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