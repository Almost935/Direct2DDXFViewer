﻿using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf.Entities;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

using Bitmap = SharpDX.Direct2D1.Bitmap;
using BitmapInterpolationMode = SharpDX.Direct2D1.BitmapInterpolationMode;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmap : IDisposable
    {
        #region Fields
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ObjectLayerManager _layerManager;
        private RawMatrix3x2 _transform;
        private readonly string _tempFileFolderPath;
        private string _filepath;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        private bool _disposed = false;
        private int _maxBitmapSize;
        #endregion

        #region Properties
        public int ZoomStep { get; set; }
        public float Zoom { get; set; }
        public Size2 Size { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapSaved = false;
        public Rect DestRect { get; set; }
        public Rect Extents { get; set; }
        public Dictionary<(byte r, byte g, byte b, byte a), Brush> Brushes { get; set; } = new();
        public enum Quadrants { TopRight, TopLeft, BottomRight, BottomLeft }
        public Quadrants Quadrant { get; set; }
        public bool IsDisposed => _disposed;
        public int Level { get; set; }
        public DxfBitmap[] DxfBitmaps { get; set; }
        public bool IsBitmapOversized { get; set; } = false;
        #endregion

        #region Constructor
        public DxfBitmap(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect destRect, Rect extents, RawMatrix3x2 extentsMatrix, int zoomStep, float zoom, string tempFileFolder, Size2 size, Quadrants quadrant, int level, int maxBitmapSize)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            DestRect = destRect;
            Extents = extents;
            _transform = extentsMatrix;
            ZoomStep = zoomStep;
            Zoom = zoom;
            _tempFileFolderPath = tempFileFolder;
            Size = size;
            Quadrant = quadrant;
            Level = level;
            _maxBitmapSize = maxBitmapSize;

            RenderBitmap();
            SaveBitmapToTemporaryFile();
        }
        #endregion

        #region Methods
        private void LoadBitmapFromFile()
        {
            if (BitmapSaved && !IsBitmapOversized)
            {
                bool fileInUse = FileHelpers.IsFileInUse(_filepath);
                if (fileInUse)
                {
                    Stopwatch timer = new();
                    timer.Start();
                    // Wait for the file to be released by the other process (if any)
                    while (FileHelpers.IsFileInUse(_filepath) || timer.ElapsedMilliseconds < 5000)
                    {
                        Thread.Sleep(500);
                    }
                }

                using (var imagingFactory = new ImagingFactory()) // Use 'using' to ensure disposal
                {
                    // Load the image using WIC
                    using (var decoder = new BitmapDecoder(imagingFactory, _filepath, DecodeOptions.CacheOnLoad))
                    using (var frame = decoder.GetFrame(0))
                    using (var converter = new FormatConverter(imagingFactory))
                    {
                        // Convert the image format to 32bppPBGRA which is compatible with Direct2D
                        converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPBGRA);

                        // Create a Direct2D Bitmap from the WIC Bitmap
                        Bitmap = Bitmap.FromWicBitmap(_deviceContext, converter);
                    }
                }
            }
        }

        public void SaveBitmapToTemporaryFile()
        {
            if (!BitmapSaved && !IsBitmapOversized)
            {
                if (_imagingFactory == null || _wicBitmap == null || _tempFileFolderPath == null)
                {
                    Debug.WriteLine($"\nError in SaveBitmapToTemporaryFile, ZoomStep: {ZoomStep} Quadrant: {Quadrant}\n");
                    throw new InvalidOperationException("Necessary objects are not initialized.");
                }

                _filepath = Path.Combine(_tempFileFolderPath, $"{Quadrant}.png");

                using (var stream = new WICStream(_imagingFactory, _filepath, SharpDX.IO.NativeFileAccess.Write))
                using (var encoder = new PngBitmapEncoder(_imagingFactory, stream))
                using (var frame = new BitmapFrameEncode(encoder))
                {
                    frame.Initialize();
                    frame.SetSize(_wicBitmap.Size.Width, _wicBitmap.Size.Height);
                    var guid = SharpDX.WIC.PixelFormat.Format32bppPBGRA;
                    frame.SetPixelFormat(ref guid);
                    frame.WriteSource(_wicBitmap);
                    frame.Commit();
                    encoder.Commit();
                }
                _wicBitmap.Dispose();
                _imagingFactory.Dispose();

                BitmapSaved = true;
            }
        }
        public bool BitmapInView(Rect view)
        {
            if (DestRect.IntersectsWith(view) || view.Contains(DestRect))
            {
                return true;
            }

            return false;
        }
        private void RenderBitmap()
        {
            if (Size.Width > _maxBitmapSize || Size.Height > _maxBitmapSize)
            {
                IsBitmapOversized = true;
                return;
            }

            _imagingFactory = new();
            _wicBitmap = new(_imagingFactory, Size.Width, Size.Height, SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);

            _wicRenderTarget = new(_factory, _wicBitmap, new RenderTargetProperties())
            {
                DotsPerInch = new Size2F(96.0f * Zoom, 96.0f * Zoom),
                AntialiasMode = AntialiasMode.PerPrimitive
            };
            _wicRenderTarget.BeginDraw();
            _wicRenderTarget.Transform = _transform;

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

            bool brushExists = Brushes.TryGetValue((r, g, b, a), out Brush brush);
            if (!brushExists)
            {
                brush = new SolidColorBrush(target, new RawColor4((float)r / 255, (float)g / 255, (float)b / 255, 1.0f));
                Brushes.Add((r, g, b, a), brush);
            }

            return brush;
        }

        public Bitmap GetBitmap()
        {
            if (IsBitmapOversized)
            {
                return null;
            }

            if (Bitmap.IsDisposed)
            {
                LoadBitmapFromFile();
            }

            return Bitmap;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _wicBitmap?.Dispose();
                    _wicRenderTarget?.Dispose();
                    _imagingFactory?.Dispose();
                    Bitmap?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            //Debug.WriteLine($"DISPOSE: {Zoom}");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DxfBitmap()
        {
            Dispose(false);
        }
    }
    #endregion
}
