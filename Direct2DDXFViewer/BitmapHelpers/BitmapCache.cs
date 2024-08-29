using Direct2DDXFViewer.DrawingObjects;
using Direct2DDXFViewer.Helpers;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private const int _initializationFactor = 10;

        private DxfBitmapView[] _zoomedInLoadedBitmaps = new DxfBitmapView[_initializationFactor];
        private DxfBitmapView[] _zoomedOutLoadedBitmaps = new DxfBitmapView[_initializationFactor];
        private bool _bitmapsInitialized = false;
        private Dictionary<int, DxfBitmapView> _createdBitmaps = new();
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ObjectLayerManager _layerManager;
        private Rect _extents;
        private RawMatrix3x2 _extentsMatrix;
        private readonly float _zoomFactor;
        private readonly int _zoomPrecision;
        private DxfBitmapView _lastUpdateBitmap;
        private string _tempFolderPath;
        private bool _disposed = false;
        private int _levels;
        private int _maxBitmapSize;
        #endregion

        #region Properties
        public DxfBitmapView CurrentBitmap { get; set; }
        public int MaxZoomStep { get; set; }
        public int MinZoomStep => -1 * MaxZoomStep;
        #endregion

        #region
        public BitmapCache(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect extents, RawMatrix3x2 extentsMatrix, float zoomFactor, int zoomPrecision, int levels, int maxBitmapSize)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extents = extents;
            _extentsMatrix = extentsMatrix;
            _zoomFactor = zoomFactor;
            _zoomPrecision = zoomPrecision;
            _levels = levels;
            _maxBitmapSize = maxBitmapSize;

            GetMaxZoomStep();
            CreateTempFolder();
            InitializeBitmaps();
            CallUpdateBitmapsAsync();
        }
        #endregion

        #region Methods
        private void GetMaxZoomStep()
        {
            if (_deviceContext.Size.Width > _deviceContext.Size.Height)
            {
                MaxZoomStep = (int)Math.Floor(_maxBitmapSize / _deviceContext.Size.Width);
            }
        }
        public void InitializeBitmaps()
        {
            CurrentBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, 1, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < MaxZoomStep; i++)
            {
                //Debug.WriteLine($"INITIALIZATION: i: {i + 1}");

                DxfBitmapView bitmap = GetBitmap(i + 1);
                _zoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < MaxZoomStep; i++)
            {
                //Debug.WriteLine($"INITIALIZATION: i: {-1 * (i + 1)}");

                DxfBitmapView bitmap = GetBitmap(-1 * (i + 1));
                _zoomedOutLoadedBitmaps[i] = bitmap;
            }

            _bitmapsInitialized = true;
        }
        public DxfBitmapView GetBitmap(int zoomStep)
        {
            if (!_bitmapsInitialized)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoomStep, out DxfBitmapView newBitmap);

                //Debug.WriteLineIf(bitmapExists, $"\nINITIALIZATION: Bitmap exists. zoomStep = {zoomStep}");
                //Debug.WriteLineIf(bitmapExists, $"\nINITIALIZATION: Bitmap doesn't exist. New bitmap created. zoomStep = {zoomStep}");

                if (!bitmapExists)
                {
                    newBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
                    _createdBitmaps.TryAdd(zoomStep, newBitmap);
                }

                return newBitmap;
            }

            DxfBitmapView bitmap = _zoomedInLoadedBitmaps.FirstOrDefault(x => x is not null && x.Zoom == zoomStep);
            bitmap ??= _zoomedOutLoadedBitmaps.FirstOrDefault(x => x is not null && x.Zoom == zoomStep);

            Debug.WriteLineIf(bitmap is not null, $"\nBitmap exists and is currently loaded, No new bitmap created. zoomStep = {zoomStep}");

            if (bitmap is null)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoomStep, out bitmap);

                Debug.WriteLineIf(bitmapExists, $"\nBitmap exists but is not loaded, No new bitmap created. zoomStep = {zoomStep}");
                Debug.WriteLineIf(!bitmapExists, $"\nBitmap does not exist. New bitmap created. zoomStep = {zoomStep}");

                if (!bitmapExists)
                {
                    bitmap = new (_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
                    _createdBitmaps.TryAdd(zoomStep, bitmap);
                }
            }
            return bitmap;
        }
        public void SetCurrentDxfBitmap(int zoomStep)
        {
            CurrentBitmap = GetBitmap(zoomStep);
        }
        private async Task CallUpdateBitmapsAsync()
        {
            while (true)
            {
                await Task.Run(() => UpdateBitmaps());
                await Task.Delay(20);
            }
        }
        private void UpdateBitmaps()
        {
            if (_lastUpdateBitmap is not null)
            {
                if (_lastUpdateBitmap == CurrentBitmap) 
                {
                    return; 
                }
            }
            DxfBitmapView[] newZoomedInLoadedBitmaps = new DxfBitmapView[_initializationFactor];
            DxfBitmapView[] newZoomedOutLoadedBitmaps = new DxfBitmapView[_initializationFactor];

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                DxfBitmapView bitmap = GetBitmap(i + 1 + CurrentBitmap.ZoomStep);
                newZoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                DxfBitmapView bitmap = GetBitmap(-1 * (i + 1 + CurrentBitmap.ZoomStep));
                newZoomedOutLoadedBitmaps[i] = bitmap;
            }

            // Iterate through current bitmaps and dispose of those that are no longer needed
            float upperLimit = _initializationFactor;
            float lowerLimit = -1 * _initializationFactor;

            foreach (var bitmap in _zoomedInLoadedBitmaps)
            {
                if (bitmap is not null)
                {
                    if (bitmap.ZoomStep < lowerLimit || bitmap.ZoomStep > upperLimit) 
                    {
                        bitmap.Dispose();
                    }
                }
            }
            foreach (var bitmap in _zoomedOutLoadedBitmaps)
            {
                if (bitmap is not null)
                {
                    if (bitmap.ZoomStep < lowerLimit || bitmap.ZoomStep > upperLimit) 
                    { 
                        bitmap.Dispose();
                    }
                }
            }
            _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
            _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
            _lastUpdateBitmap = CurrentBitmap;

            return;
        }

        public void CreateTempFolder()
        {
            // Get the path to the temporary files directory
            string tempPath = Path.GetTempPath();
            string folderName = "CadViewer";

            // Combine the temporary path with the folder name
            _tempFolderPath = Path.Combine(tempPath, folderName);

            // Check if the directory already exists
            if (Directory.Exists(_tempFolderPath))
            {
                Directory.Delete(_tempFolderPath, true);
            }
            Directory.CreateDirectory(_tempFolderPath);
        }

        private void DeleteTempFolder()
        {
            if (!string.IsNullOrEmpty(_tempFolderPath) && Directory.Exists(_tempFolderPath))
            {
                Directory.Delete(_tempFolderPath, true);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    foreach (var bitmap in _zoomedInLoadedBitmaps)
                    {
                        bitmap?.Dispose();
                    }
                    foreach (var bitmap in _zoomedOutLoadedBitmaps)
                    {
                        bitmap?.Dispose();
                    }
                    foreach (var bitmap in _createdBitmaps.Values)
                    {
                        bitmap?.Dispose();
                    }
                    DeleteTempFolder();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BitmapCache()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }
        #endregion
    }
}
