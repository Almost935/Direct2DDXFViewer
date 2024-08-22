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
        #endregion

        #region Properties
        public float Zoom;
        public Rect OverallBounds { get; set; }
        public DxfBitmap TopRightBitmap { get; set; }
        public DxfBitmap TopLeftBitmap { get; set; }
        public DxfBitmap BottomRightBitmap { get; set; }
        public DxfBitmap BottomLeftBitmap { get; set; }
        public DxfBitmap[] Bitmaps => new DxfBitmap[] { TopLeftBitmap, TopRightBitmap, BottomLeftBitmap, BottomRightBitmap };
        #endregion

        #region Constructor
        public DxfBitmapView(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect extents, RawMatrix3x2 extentsMatrix, float zoom, string tempFileFolderPath)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extents = extents;
            _extentsMatrix = extentsMatrix;
            Zoom = zoom;
            OverallBounds = new(new Point(0, 0), new Size(_deviceContext.Size.Width * Zoom, _deviceContext.Size.Height * Zoom));

            CreateViewFolder(tempFileFolderPath);
            GetDxfBitmaps();
        }
        #endregion

        #region Methods
        public void GetDxfBitmaps()
        {
            Size overallSize = new(_deviceContext.Size.Width * Zoom, _deviceContext.Size.Height * Zoom);
            Size destSize = new((_deviceContext.Size.Width * Zoom ) / 2, (_deviceContext.Size.Height * Zoom) / 2);

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

            TopLeftBitmap = new(_deviceContext, _factory, _layerManager, topLeftDest, topLeftExtents, topLeftMatrix, Zoom, _tempFileFolderPath, new Size2((int)destSize.Width, (int)destSize.Height), DxfBitmap.Quadrants.TopLeft);
            TopRightBitmap = new(_deviceContext, _factory, _layerManager, topRightDest, topRightExtents, topRightMatrix, Zoom, _tempFileFolderPath, new Size2((int)destSize.Width, (int)destSize.Height), DxfBitmap.Quadrants.TopRight);
            BottomLeftBitmap = new(_deviceContext, _factory, _layerManager, bottomLeftDest, bottomLeftExtents, bottomLeftMatrix, Zoom, _tempFileFolderPath, new Size2((int)destSize.Width, (int)destSize.Height), DxfBitmap.Quadrants.BottomLeft); 
            BottomRightBitmap = new(_deviceContext, _factory, _layerManager, bottomRightDest, bottomRightExtents, bottomRightMatrix, Zoom, _tempFileFolderPath, new Size2((int)destSize.Width, (int)destSize.Height), DxfBitmap.Quadrants.BottomRight);

            Debug.WriteLine($"\nZoom: {Zoom}");
            Debug.WriteLine($"topLeftMatrix: {topLeftMatrix.M11} {topLeftMatrix.M22} {topLeftMatrix.M31} {topLeftMatrix.M32}");
            Debug.WriteLine($"topRightMatrix: {topRightMatrix.M11} {topRightMatrix.M22} {topRightMatrix.M31} {topRightMatrix.M32}");
            Debug.WriteLine($"bottomLeftMatrix: {bottomLeftMatrix.M11} {bottomLeftMatrix.M22} {bottomLeftMatrix.M31} {bottomLeftMatrix.M32}");
            Debug.WriteLine($"bottomRightMatrix: {bottomRightMatrix.M11} {bottomRightMatrix.M22} {bottomRightMatrix.M31} {bottomRightMatrix.M32}");
        }
        public void CreateViewFolder(string path)
        {
            // Get the path to the temporary files directory
            string tempPath = Path.GetTempPath();
            string folderName = $"{Zoom}";

            // Combine the temporary path with the folder name
            _tempFileFolderPath = Path.Combine(tempPath, folderName);

            // Check if the directory already exists
            if (Directory.Exists(_tempFileFolderPath))
            {
                Directory.Delete(_tempFileFolderPath, true);
            }
            Directory.CreateDirectory(_tempFileFolderPath);
        }
        public void LoadDxfBitmaps()
        {

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
