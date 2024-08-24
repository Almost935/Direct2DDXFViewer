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
using System.Windows;
using System.Windows.Media;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private const int _initializationFactor = 5;

        private DxfBitmapView[] _zoomedInLoadedBitmaps = new DxfBitmapView[_initializationFactor];
        private DxfBitmapView[] _zoomedOutLoadedBitmaps = new DxfBitmapView[_initializationFactor];
        private bool _bitmapsInitialized = false;
        private Dictionary<float, DxfBitmapView> _createdBitmaps = new();
        private readonly DeviceContext1 _deviceContext;
        private readonly Factory1 _factory;
        private readonly ObjectLayerManager _layerManager;
        private float _currentZoom;
        private Rect _extents;
        private RawMatrix3x2 _extentsMatrix;
        private readonly float _zoomFactor;
        private DxfBitmapView _lastUpdateBitmap;
        private string _tempFolderPath;
        private bool _disposed = false; 
        #endregion

        #region Properties
        public DxfBitmapView CurrentBitmap { get; set; }
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
        public BitmapCache(DeviceContext1 deviceContext, Factory1 factory, ObjectLayerManager layerManager, Rect extents, RawMatrix3x2 extentsMatrix, float zoomFactor)
        {
            _deviceContext = deviceContext;
            _factory = factory;
            _layerManager = layerManager;
            _extents = extents;
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
            CurrentBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, 1, _tempFolderPath);

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, (i + 1)), 3);
                DxfBitmapView bitmap = GetBitmap(zoom);
                _zoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, (i + 1))), 3);
                DxfBitmapView bitmap = GetBitmap(zoom);
                _zoomedOutLoadedBitmaps[i] = bitmap;
            }

            _bitmapsInitialized = true;
        }
        public DxfBitmapView GetBitmap(float zoom)
        {
            zoom = (float)Math.Round(zoom, 3);

            if (!_bitmapsInitialized)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoom, out DxfBitmapView newBitmap);

                if (!bitmapExists)
                {
                    newBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoom, _tempFolderPath);
                    _createdBitmaps.Add(zoom, newBitmap);
                }

                return newBitmap;
            }

            DxfBitmapView bitmap = _zoomedInLoadedBitmaps.FirstOrDefault(x => x.Zoom == zoom);
            bitmap ??= _zoomedOutLoadedBitmaps.FirstOrDefault(x => x.Zoom == zoom);

            if (bitmap is null)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoom, out bitmap);
                if (!bitmapExists)
                {
                    bitmap = new (_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoom, _tempFolderPath);
                    _createdBitmaps.TryAdd(zoom, bitmap);
                }
                else
                {
                   bitmap.LoadDxfBitmaps();
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
                await Task.Delay(20);
            }
        }
        private void UpdateBitmaps()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
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
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, (i + 1)), 3);
                DxfBitmapView bitmap = GetBitmap(zoom);
                newZoomedInLoadedBitmaps[i] = bitmap;
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < _initializationFactor; i++)
            {
                float zoom = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, (i + 1))), 3);
                DxfBitmapView bitmap = GetBitmap(zoom);
                newZoomedOutLoadedBitmaps[i] = bitmap;
            }

            // Iterate through current bitmaps and dispose of those that are no longer needed
            float upperLimit = (float)Math.Round(CurrentBitmap.Zoom * Math.Pow(_zoomFactor, _initializationFactor), 3);
            float lowerLimit = (float)Math.Round(CurrentBitmap.Zoom * (1 / Math.Pow(_zoomFactor, _initializationFactor)), 3);

            Debug.WriteLine($"\n\n\nCurrentBitmap.Zoom: {CurrentBitmap.Zoom}");
            Debug.WriteLine($"Upper Limit: {upperLimit}");
            Debug.WriteLine($"Lower Limit: {lowerLimit}");

            foreach (var bitmap in _zoomedInLoadedBitmaps)
            {
                Debug.WriteLine($"Zoomed In: {bitmap.Zoom}");
                if (bitmap is not null)
                {
                    if (bitmap.Zoom < lowerLimit || bitmap.Zoom > upperLimit) 
                    {
                        bitmap.Dispose(); 
                        Debug.WriteLine($"\nDispose Zoomed In: {bitmap.Zoom}\n");
                    }
                }
            }
            foreach (var bitmap in _zoomedOutLoadedBitmaps)
            {
                Debug.WriteLine($"Zoomed Out: {bitmap.Zoom}");
                if (bitmap is not null)
                {
                    if (bitmap.Zoom < lowerLimit || bitmap.Zoom > upperLimit) 
                    { 
                        bitmap.Dispose(); 
                        Debug.WriteLine($"\nDispose Zoomed Out: {bitmap.Zoom}\n");
                    }
                }
            }
            _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
            _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
            _lastUpdateBitmap = CurrentBitmap;

            stopwatch.Stop();
            //Debug.WriteLine($"UpdateBitmaps: {stopwatch.ElapsedMilliseconds} ms");

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
