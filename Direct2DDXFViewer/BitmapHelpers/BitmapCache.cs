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
        private const int _loadedBitmapsCount = 5;

        private DxfBitmapView[] _zoomedInLoadedBitmaps;
        private DxfBitmapView[] _zoomedOutLoadedBitmaps;
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
            _zoomedInLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];
            _zoomedOutLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];

            CreateTempFolder();
            InitializeBitmaps();
            UpdateLoadedBitmaps();
            CallUpdateLoadedBitmapsAsync();
        }
        #endregion

        #region Methods
        private void GetMaxZoomStep()
        {
            // Calculate the maximum zoom step based on the size of the device context. Equation found using these identities:
            // _maxBitmapSize = 0.5 * (Zoom * _deviceContext.Size.Width)
            // Zoom = _zoomFactor ^ ZoomStep
            if (_deviceContext.Size.Width > _deviceContext.Size.Height)
            {
                MaxZoomStep = (int)Math.Floor((Math.Log10((_maxBitmapSize / 2) / _deviceContext.Size.Width)) / (Math.Log10(_zoomFactor)));
            }
            else
            {
                MaxZoomStep = (int)Math.Floor((Math.Log10((_maxBitmapSize / 2) / _deviceContext.Size.Height)) / (Math.Log10(_zoomFactor)));
            }
        }
        public void InitializeBitmaps()
        {
            CurrentBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, 0, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
            _createdBitmaps.TryAdd(0, CurrentBitmap);

            // Iterate through next initializationFactor amount of zoomed in bitmaps
            for (int i = 0; i < MaxZoomStep; i++)
            {
                DxfBitmapView bitmapView = GetBitmap(i + 1);
                if (Math.Abs(bitmapView.ZoomStep - CurrentBitmap.ZoomStep) > _loadedBitmapsCount)
                {
                    bitmapView.DisposeBitmaps();
                }
                else
                {
                    _zoomedInLoadedBitmaps[i] = bitmapView;
                }
            }
            // Iterate through next initializationFactor amount of zoomed out bitmaps
            for (int i = 0; i < MaxZoomStep; i++)
            {
                DxfBitmapView bitmapView = GetBitmap(-1 * (i + 1));
                if (Math.Abs(bitmapView.ZoomStep - CurrentBitmap.ZoomStep) > _loadedBitmapsCount)
                {
                    bitmapView.DisposeBitmaps();
                }
                else
                {
                    _zoomedOutLoadedBitmaps[i] = bitmapView;
                }
            }
            _bitmapsInitialized = true;
        }

        public DxfBitmapView GetBitmap(int zoomStep)
        {
            if (!_bitmapsInitialized)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoomStep, out DxfBitmapView newBitmap);

                if (!bitmapExists)
                {
                    newBitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
                    _createdBitmaps.TryAdd(zoomStep, newBitmap);
                }

                return newBitmap;
            }

            DxfBitmapView bitmap = _zoomedInLoadedBitmaps.FirstOrDefault(x => x is not null && x.ZoomStep == zoomStep);
            bitmap ??= _zoomedOutLoadedBitmaps.FirstOrDefault(x => x is not null && x.ZoomStep == zoomStep);

            //Debug.WriteLineIf(bitmap is not null, $"DxfBitmapView found in loaded bitmaps. zoomStep: {zoomStep}");

            if (bitmap is null)
            {
                bool bitmapExists = _createdBitmaps.TryGetValue(zoomStep, out bitmap);

                //Debug.WriteLineIf(bitmapExists, $"DxfBitmapView found in _createdBitmaps but not yet loaded. zoomStep: {zoomStep}");

                if (!bitmapExists)
                {
                    bitmap = new(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
                    _createdBitmaps.TryAdd(zoomStep, bitmap);

                    //Debug.WriteLine($"New DxfBitmapView created. zoomStep: {zoomStep}");
                }
                else
                {
                    bitmap.LoadDxfBitmaps();
                }
            }
            return bitmap;
        }
        public void SetCurrentDxfBitmap(int zoomStep)
        {
            CurrentBitmap = GetBitmap(zoomStep);
            CurrentBitmap.LoadDxfBitmaps();
        }
        private async Task CallUpdateLoadedBitmapsAsync()
        {
            while (true)
            {
                await Task.Run(() => UpdateLoadedBitmaps());
                await Task.Delay(20);
            }
        }
        private void UpdateLoadedBitmaps()
        {
            if (_lastUpdateBitmap is not null && _lastUpdateBitmap == CurrentBitmap)
            {
                return;
            }

            DxfBitmapView[] newZoomedInLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];
            DxfBitmapView[] newZoomedOutLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];
            
            // Obtain new DxfBitmapView objects and verify their bitmaps are loaded
            for (int i = 0; i < _loadedBitmapsCount; i++)
            {
                var bitmapView = GetBitmap(CurrentBitmap.ZoomStep + (i + 1));
                bitmapView.LoadDxfBitmaps();
                newZoomedInLoadedBitmaps[i] = bitmapView;
            }
            for (int i = 0; i < _loadedBitmapsCount; i++)
            {
                int zoomStep = CurrentBitmap.ZoomStep - (i + 1);
                var bitmapView = GetBitmap(zoomStep);
                bitmapView.LoadDxfBitmaps();
                newZoomedOutLoadedBitmaps[i] = bitmapView;
            }

            // Iterate through previouse loaded bitmaps and dispose of those that are no longer needed
            foreach (var bitmapView in _zoomedInLoadedBitmaps)
            {
                if (Math.Abs(bitmapView.ZoomStep - CurrentBitmap.ZoomStep) > _loadedBitmapsCount)
                {
                    bitmapView.DisposeBitmaps();
                }
            }
            foreach (var bitmapView in _zoomedOutLoadedBitmaps)
            {
                if (Math.Abs(bitmapView.ZoomStep - CurrentBitmap.ZoomStep) > _loadedBitmapsCount)
                {
                    bitmapView.DisposeBitmaps();
                }
            }

            _lastUpdateBitmap = CurrentBitmap;
            _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
            _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
        }

        //private void UpdateLoadedBitmaps()
        //{
        //    if (_lastUpdateBitmap is not null)
        //    {
        //        if (_lastUpdateBitmap == CurrentBitmap) 
        //        {
        //            return; 
        //        }
        //    }

        //    //Debug.WriteLine($"\n");

        //    DxfBitmapView[] newZoomedInLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];
        //    DxfBitmapView[] newZoomedOutLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];

        //    // Iterate through next initializationFactor amount of zoomed in bitmaps
        //    for (int i = 0; i < _loadedBitmapsCount; i++)
        //    {
        //        if (CurrentBitmap.ZoomStep + i + 1 > MaxZoomStep) { continue; }

        //        DxfBitmapView bitmap = GetBitmap(CurrentBitmap.ZoomStep + (i + 1));
        //        bitmap.LoadDxfBitmaps();
        //        newZoomedInLoadedBitmaps[i] = bitmap;
        //    }
        //    // Iterate through next initializationFactor amount of zoomed out bitmaps
        //    for (int i = 0; i < _loadedBitmapsCount; i++)
        //    {
        //        DxfBitmapView bitmap = GetBitmap(CurrentBitmap.ZoomStep - (i + 1));
        //        bitmap.LoadDxfBitmaps();
        //        newZoomedOutLoadedBitmaps[i] = bitmap;
        //    }

        //    // Iterate through current bitmaps and dispose of those that are no longer needed
        //    float upperLimit = CurrentBitmap.ZoomStep + _loadedBitmapsCount;
        //    float lowerLimit = CurrentBitmap.ZoomStep - _loadedBitmapsCount;

        //    foreach (var bitmap in _zoomedInLoadedBitmaps)
        //    {
        //        if (bitmap is not null)
        //        {
        //            if (bitmap.ZoomStep < lowerLimit || bitmap.ZoomStep > upperLimit) 
        //            {
        //                bitmap.DisposeBitmaps();
        //            }
        //        }
        //    }
        //    foreach (var bitmap in _zoomedOutLoadedBitmaps)
        //    {
        //        if (bitmap is not null)
        //        {
        //            if (bitmap.ZoomStep < lowerLimit || bitmap.ZoomStep > upperLimit) 
        //            { 
        //                bitmap.DisposeBitmaps();
        //            }
        //        }
        //    }
        //    _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
        //    _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
        //    _lastUpdateBitmap = CurrentBitmap;

        //    return;
        //}

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
