using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using netDxf;
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
        private SharpDX.WIC.Bitmap _wicBitmap;
        private WicRenderTarget _wicRenderTarget;
        private ImagingFactory _imagingFactory;
        private string _tempFileFolderPath;
        private bool _disposed = false;
        private int _maxBitmapSize;
        private int _numOfDivisions;
        #endregion

        #region Properties
        public int ZoomStep { get; set; }
        public float ZoomFactor { get; set; }
        public int ZoomPrecision { get; set; }
        public float Zoom { get; set; }
        public List<DxfBitmap> Bitmaps { get; set; } = new();
        //public DxfBitmap TopRightBitmap { get; set; }
        //public DxfBitmap TopLeftBitmap { get; set; }
        //public DxfBitmap BottomRightBitmap { get; set; }
        //public DxfBitmap BottomLeftBitmap { get; set; }
        //public DxfBitmap[] Bitmaps => new DxfBitmap[] { TopLeftBitmap, TopRightBitmap, BottomLeftBitmap, BottomRightBitmap };
        //public bool IsBitmapOversized { get; set; } = false;
        public bool BitmapsLoaded { get; set; } = false;
        #endregion

        #region Constructor
        public DxfBitmapView(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect extents, RawMatrix3x2 extentsMatrix, int zoomStep, float zoomFactor, int zoomPrecision, string tempFileFolderPath, int maxBitmapSize, int numOfDivisions)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extents = extents;
            _extentsMatrix = extentsMatrix;
            _maxBitmapSize = maxBitmapSize;
            _numOfDivisions = numOfDivisions;

            ZoomFactor = zoomFactor;
            ZoomStep = zoomStep;
            ZoomPrecision = zoomPrecision;
            Zoom = MathHelpers.GetZoom(ZoomFactor, ZoomStep, ZoomPrecision);

            CreateViewFolder(tempFileFolderPath);
            GetDxfBitmaps();
        }
        #endregion

        #region Methods
        public void GetDxfBitmaps()
        {
            Size overallSize = new(Zoom * _deviceContext.Size.Width, Zoom * _deviceContext.Size.Height);
            double width = overallSize.Width / _numOfDivisions;
            double height = overallSize.Height / _numOfDivisions;
            Size2 size = new((int)(width), (int)(height));
            double destWidth = _deviceContext.Size.Width / _numOfDivisions;
            double destHeight = _deviceContext.Size.Height / _numOfDivisions;
            double extentsWidth = _extents.Width / _numOfDivisions;
            double extentsHeight = _extents.Height / _numOfDivisions;

            for (int i = 0; i < _numOfDivisions; i++) // Width increment corresponds to i in this loop
            {
                for (int j = 0; j < _numOfDivisions; j++) // Height increment corresponds to j in this loop
                {
                    Rect extents = new(extentsWidth * i, extentsHeight * j, extentsWidth, extentsHeight);
                    Rect dest = new((int)(destWidth * i), (int)(destHeight * j), destWidth, destHeight);

                    RawMatrix3x2 matrix = new(_extentsMatrix.M11, _extentsMatrix.M12, _extentsMatrix.M21, _extentsMatrix.M22, 
                        _extentsMatrix.M31 - (float)(destWidth * i), _extentsMatrix.M32 - (float)(destHeight * j));

                    DxfBitmap bitmap = new(_deviceContext, _factory, _layerManager, dest, extents, matrix, ZoomStep, Zoom, _tempFileFolderPath, size, _maxBitmapSize);
                    Bitmaps.Add(bitmap);
                }
            }
        }
        public void CreateViewFolder(string path)
        {
            // Get the path to the temporary files directory
            string folderName = $"{ZoomStep}";

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
            if (!BitmapsLoaded)
            {
                foreach (var bitmap in Bitmaps)
                {
                    bitmap.GetBitmap();
                }
                BitmapsLoaded = true;
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
        public void DisposeBitmaps()
        {
            foreach (var bitmap in Bitmaps)
            {
                bitmap.DisposeBitmap();
            }
            BitmapsLoaded = false;
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
                    DisposeBitmaps();

                    BitmapsLoaded = false;
                }

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
