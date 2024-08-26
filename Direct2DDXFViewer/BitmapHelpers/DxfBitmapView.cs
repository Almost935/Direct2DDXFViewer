using Direct2DDXFViewer.DrawingObjects;
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
using System.Windows.Data;
using System.Windows.Media;
using Bitmap = SharpDX.Direct2D1.Bitmap;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmapView : IDisposable
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private Factory1 _factory;
        private ObjectLayerManager _layerManager;
        private Rect _extents;
        private RawMatrix3x2 _extentsMatrix;
        private Bitmap _overallBitmap;
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        private string _tempFileFolderPath;
        private bool _disposed = false;
        private int _levels;
        #endregion

        #region Properties
        public float Zoom;
        public DxfBitmap TopRightBitmap { get; set; }
        public DxfBitmap TopLeftBitmap { get; set; }
        public DxfBitmap BottomRightBitmap { get; set; }
        public DxfBitmap BottomLeftBitmap { get; set; }
        public DxfBitmap[] Bitmaps => new DxfBitmap[] { TopLeftBitmap, TopRightBitmap, BottomLeftBitmap, BottomRightBitmap };
        #endregion

        #region Constructor
        public DxfBitmapView(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect extents, RawMatrix3x2 extentsMatrix, float zoom, string tempFileFolderPath, int levels)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extents = extents;
            _extentsMatrix = extentsMatrix;
            Zoom = zoom;
            _levels = levels;

            CreateViewFolder(tempFileFolderPath);
            GetDxfBitmaps();
        }
        #endregion

        #region Methods
        public void GetDxfBitmaps()
        {
            Size2 size = new((int)(_deviceContext.Size.Width * Zoom) / 2, (int)(_deviceContext.Size.Height * Zoom) / 2);
            Size destSize = new((_deviceContext.Size.Width) / 2, (_deviceContext.Size.Height) / 2);

            Rect topLeftExtents = new(_extents.Left, _extents.Top, _extents.Width * 0.5, _extents.Height * 0.5);
            Rect topRightExtents = new(_extents.Left + (_extents.Width * 0.5), _extents.Top, _extents.Width * 0.5, _extents.Height * 0.5);
            Rect bottomLeftExtents = new(_extents.Left, _extents.Top + (_extents.Height * 0.5), _extents.Width * 0.5, _extents.Height * 0.5);
            Rect bottomRightExtents = new(_extents.Left + (_extents.Width * 0.5), _extents.Top + (_extents.Height * 0.5), _extents.Width * 0.5, _extents.Height * 0.5);

            Rect topLeftDest = new(0, 0, destSize.Width, destSize.Height);
            Rect topRightDest = new(destSize.Width, 0, destSize.Width, destSize.Height);
            Rect bottomLeftDest = new(0, destSize.Height, destSize.Width, destSize.Height);
            Rect bottomRightDest = new(destSize.Width, destSize.Height, destSize.Width, destSize.Height);

            RawMatrix3x2 topLeftMatrix = new(_extentsMatrix.M11, _extentsMatrix.M12, _extentsMatrix.M21, _extentsMatrix.M22, _extentsMatrix.M31, _extentsMatrix.M32);
            RawMatrix3x2 topRightMatrix = new(_extentsMatrix.M11, _extentsMatrix.M12, _extentsMatrix.M21, _extentsMatrix.M22, _extentsMatrix.M31 - (float)(destSize.Width),
                _extentsMatrix.M32);
            RawMatrix3x2 bottomLeftMatrix = new(_extentsMatrix.M11, _extentsMatrix.M12, _extentsMatrix.M21, _extentsMatrix.M22, _extentsMatrix.M31,
                _extentsMatrix.M32 - (float)(destSize.Height));
            RawMatrix3x2 bottomRightMatrix = new(_extentsMatrix.M11, _extentsMatrix.M12, _extentsMatrix.M21, _extentsMatrix.M22, _extentsMatrix.M31 - (float)(destSize.Width),
                _extentsMatrix.M32 - (float)(destSize.Height));

            TopLeftBitmap = new(_deviceContext, _factory, _layerManager, topLeftDest, topLeftExtents, topLeftMatrix, Zoom, _tempFileFolderPath, size, DxfBitmap.Quadrants.TopLeft, _levels);
            TopRightBitmap = new(_deviceContext, _factory, _layerManager, topRightDest, topRightExtents, topRightMatrix, Zoom, _tempFileFolderPath, size, DxfBitmap.Quadrants.TopRight, _levels);
            BottomLeftBitmap = new(_deviceContext, _factory, _layerManager, bottomLeftDest, bottomLeftExtents, bottomLeftMatrix, Zoom, _tempFileFolderPath, size, DxfBitmap.Quadrants.BottomLeft, _levels);
            BottomRightBitmap = new(_deviceContext, _factory, _layerManager, bottomRightDest, bottomRightExtents, bottomRightMatrix, Zoom, _tempFileFolderPath, size, DxfBitmap.Quadrants.BottomRight, _levels);
        }
        public void CreateViewFolder(string path)
        {
            // Get the path to the temporary files directory
            string folderName = $"{Zoom}";

            // Combine the temporary path with the folder name
            _tempFileFolderPath = Path.Combine(path, folderName);

            // Check if the directory already exists
            if (Directory.Exists(_tempFileFolderPath))
            {
                Directory.Delete(_tempFileFolderPath, true);
            }
            Directory.CreateDirectory(_tempFileFolderPath);
        }
        public void LoadDxfBitmaps()
        {
            foreach (var bitmap in Bitmaps)
            {
                if (bitmap.IsDisposed)
                {
                    bitmap.GetBitmap();
                }
            }
        }
        public List<DxfBitmap> GetVisibleBitmaps(Rect view)
        {
            List<DxfBitmap> bitmaps = new();

            foreach (var bitmap in Bitmaps)
            {
                if (bitmap.BitmapInView(view))
                {
                    bitmaps.Add(bitmap);
                }
            }
            return bitmaps;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _overallBitmap?.Dispose();
                    _wicBitmap?.Dispose();
                    _wicRenderTarget?.Dispose();
                    _imagingFactory?.Dispose();
                    TopLeftBitmap?.Dispose();
                    TopRightBitmap?.Dispose();
                    BottomLeftBitmap?.Dispose();
                    BottomRightBitmap?.Dispose();
                }

                // Dispose unmanaged resources

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DxfBitmapView()
        {
            Dispose(false);
        }
        #endregion
    }
}
