using Direct2DDXFViewer.DrawingObjects;
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
using System.Windows.Media;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private const int _initializationFactor = 5;

        private DxfBitmap[] _zoomedInLoadedBitmaps = new DxfBitmap[_initializationFactor];
        private DxfBitmap[] _zoomedOutLoadedBitmaps = new DxfBitmap[_initializationFactor];
        private bool _bitmapsInitialized = false;
        private Dictionary<float, DxfBitmap> _createdBitmaps = new();
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ObjectLayerManager _layerManager;
        private float _currentZoom;
        private RawMatrix3x2 _extentsMatrix;
        private readonly float _zoomFactor;
        private DxfBitmap _lastUpdateBitmap;
        private string _tempFolderPath;
        private bool _disposed = false; 
        #endregion

        #region Properties
        public DxfBitmap CurrentBitmap { get; set; }
        public float CurrentZoom
        {
            get => _currentZoom;
            set
            {
                if (_currentZoom != value)
                {
                    _currentZoom = value;
                    OnPropertyChanged(nameof(CurrentZoom));
                }
            }
        }
        #endregion

        #region
        public BitmapCache(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, RawMatrix3x2 extentsMatrix, float zoomFactor)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extentsMatrix = extentsMatrix;
            _zoomFactor = zoomFactor;

            CreateTempFolder();
            InitializeBitmaps();
            CallUpdateBitmapsAsync();
        }
        #endregion

        #region Methods
        public void InitializeBitmaps()
        {
            CurrentBitmap = new(_deviceContext, _factory, _layerManager, _extentsMatrix, 1, _tempFolderPath);

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, (i + 1)), 3);
                DxfBitmap bitmap = GetBitmap(zoom);
                _zoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, (i + 1))), 3);
                DxfBitmap bitmap = GetBitmap(zoom);
                _zoomedOutLoadedBitmaps[i] = bitmap;
            }

            _bitmapsInitialized = true;
        }
        public DxfBitmap GetBitmap(float zoom)
        {
            zoom = (float)Math.Round(zoom, 3);

            if (!_bitmapsInitialized)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoom, out DxfBitmap newBitmap);

                if (!bitmapExists)
                {
                    newBitmap = new DxfBitmap(_deviceContext, _factory, _layerManager, _extentsMatrix, zoom, _tempFolderPath);
                    _createdBitmaps.Add(zoom, newBitmap);
                }

                return newBitmap;
            }

            DxfBitmap bitmap = _zoomedInLoadedBitmaps.FirstOrDefault(x => x.Zoom == zoom);
            bitmap ??= _zoomedOutLoadedBitmaps.FirstOrDefault(x => x.Zoom == zoom);

            if (bitmap is null)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoom, out bitmap);
                if (!bitmapExists)
                {
                    bitmap = new DxfBitmap(_deviceContext, _factory, _layerManager, _extentsMatrix, zoom, _tempFolderPath);
                    _createdBitmaps.TryAdd(zoom, bitmap);
                }
                else
                {
                    if (bitmap.Bitmap.IsDisposed)
                    {
                        bitmap.GetBitmap();
                    }
                }
            }

            return bitmap;
        }
        public void SetCurrentDxfBitmap(float zoom)
        {
            CurrentBitmap = GetBitmap(zoom);
        }
        private async Task CallUpdateBitmapsAsync()
        {
            while (true)
            {
                await Task.Run(() => UpdateBitmaps());
                await Task.Delay(10);
            }
        }
        private void UpdateBitmaps()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (_lastUpdateBitmap is not null)
            {
                if (_lastUpdateBitmap == CurrentBitmap) { return; }
            }

            DxfBitmap[] newZoomedInLoadedBitmaps = new DxfBitmap[_initializationFactor];
            DxfBitmap[] newZoomedOutLoadedBitmaps = new DxfBitmap[_initializationFactor];

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, (i + 1)), 3);
                DxfBitmap bitmap = GetBitmap(zoom);
                newZoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, (i + 1))), 3);
                DxfBitmap bitmap = GetBitmap(zoom);
                newZoomedOutLoadedBitmaps[i] = bitmap;
            }

            // Iterate through current bitmaps and dispose of those that are no longer needed
            float upperLimit = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, _initializationFactor), 3);
            float lowerLimit = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, _initializationFactor)), 3);

            foreach (var bitmap in _zoomedInLoadedBitmaps)
            {
                if (bitmap is not null)
                {
                    if (bitmap.Zoom < lowerLimit || bitmap.Zoom > upperLimit) { bitmap.Dispose(); }
                }
            }
            foreach (var bitmap in _zoomedOutLoadedBitmaps)
            {
                if (bitmap is not null)
                {
                    if (bitmap.Zoom < lowerLimit || bitmap.Zoom > upperLimit) { bitmap.Dispose(); }
                }
            }
            _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
            _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
            _lastUpdateBitmap = CurrentBitmap;

            stopwatch.Stop();
            Debug.WriteLine($"UpdateBitmaps: {stopwatch.ElapsedMilliseconds} ms");

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

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

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
