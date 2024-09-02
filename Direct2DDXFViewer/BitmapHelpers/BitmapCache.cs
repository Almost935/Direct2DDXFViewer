﻿using Direct2DDXFViewer.DrawingObjects;
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
        private const int _initializationFactor = 5;
        private const int _loadedBitmapsCount = 5;
        private const int _bitmapReuseFactor = 2;

        private DxfBitmapView[] _zoomedInLoadedBitmaps;
        private DxfBitmapView[] _zoomedOutLoadedBitmaps;
        private bool _bitmapsInitialized = false;
        private bool _asyncBitmapsInitialized = false;
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
        public int MaxZoomStep { get; private set; }
        public int MinZoomStep => -1 * MaxZoomStep;
        #endregion

        #region Constructor
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
            //RunLoadBitmapsAsync();
            UpdateLoadedBitmaps();
            CallUpdateLoadedBitmapsAsync();
        }
        #endregion

        #region Methods
        private void GetMaxZoomStep()
        {
            var size = Math.Max(_deviceContext.Size.Width, _deviceContext.Size.Height);
            MaxZoomStep = (int)Math.Floor(Math.Log10((2 * _maxBitmapSize) / size) / Math.Log10(_zoomFactor));
        }

        private async Task RunLoadBitmapsAsync()
        {
            while (!_asyncBitmapsInitialized)
            {
                await Task.Run(LoadBitmapsAsync);
            }
        }

        private void LoadBitmapsAsync()
        {
            Stopwatch overallStopwatch = Stopwatch.StartNew();

            CurrentBitmap = new DxfBitmapView(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, 0, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
            _createdBitmaps[0] = CurrentBitmap;

            Parallel.For(_initializationFactor, MaxZoomStep, i =>
            {
                CreateAndAddBitmapView(i + 1);
                CreateAndAddBitmapView(-1 * (i + 1));
            });

            overallStopwatch.Stop();
            Debug.WriteLine($"ASYNC: Bitmaps initialized in {overallStopwatch.ElapsedMilliseconds} ms");

            _asyncBitmapsInitialized = true;
        }

        private void CreateAndAddBitmapView(int zoomStep)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            DxfBitmapView bitmapView = new DxfBitmapView(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);

            stopwatch.Stop();
            Debug.WriteLine($"ASYNC: bitmapView.ZoomStep: {bitmapView.ZoomStep} created in {stopwatch.ElapsedMilliseconds} ms");

            if (!_createdBitmaps.TryAdd(bitmapView.ZoomStep, bitmapView))
            {
                bitmapView.Dispose();
            }
        }

        public void InitializeBitmaps()
        {
            Stopwatch overallStopwatch = Stopwatch.StartNew();

            CurrentBitmap = new DxfBitmapView(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, 0, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);
            _createdBitmaps[0] = CurrentBitmap;

            Parallel.For(0, MaxZoomStep, i =>
            {
                if (i < _initializationFactor)
                {
                    CreateAndAddBitmapView(i + 1, _zoomedInLoadedBitmaps, i);
                    CreateAndAddBitmapView(-1 * (i + 1), _zoomedOutLoadedBitmaps, i);
                }
                else
                {
                    CreateAndAddBitmapView(i + 1);
                    CreateAndAddBitmapView(-1 * (i + 1));
                }
            });

            //for (int i = 0; i < _initializationFactor; i++)
            //{
            //    CreateAndAddBitmapView(i + 1, _zoomedInLoadedBitmaps, i);
            //    CreateAndAddBitmapView(-1 * (i + 1), _zoomedOutLoadedBitmaps, i);
            //}

            _bitmapsInitialized = true;
            overallStopwatch.Stop();
            Debug.WriteLine($"Bitmaps initialized in {overallStopwatch.ElapsedMilliseconds} ms");
        }

        private void CreateAndAddBitmapView(int zoomStep, DxfBitmapView[] bitmapArray, int index)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            DxfBitmapView bitmapView = new DxfBitmapView(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);

            stopwatch.Stop();
            Debug.WriteLine($"bitmapView.ZoomStep: {bitmapView.ZoomStep} created in {stopwatch.ElapsedMilliseconds} ms");

            if (_createdBitmaps.TryAdd(bitmapView.ZoomStep, bitmapView))
            {
                bitmapArray[index] = bitmapView;
            }
            else
            {
                bitmapView.Dispose();
            }
        }

        public DxfBitmapView GetBitmap(int zoomStep, bool isLoaded)
        {
            DxfBitmapView bitmapView = _zoomedInLoadedBitmaps.FirstOrDefault(b => b?.ZoomStep == zoomStep) ??
                                       _zoomedOutLoadedBitmaps.FirstOrDefault(b => b?.ZoomStep == zoomStep) ??
                                       _createdBitmaps.GetValueOrDefault(zoomStep) ??
                                       new DxfBitmapView(_deviceContext, _factory, _layerManager, _extents, _extentsMatrix, zoomStep, _zoomFactor, _zoomPrecision, _tempFolderPath, _levels, _maxBitmapSize);

            if (!_createdBitmaps.ContainsKey(zoomStep))
            {
                _createdBitmaps[zoomStep] = bitmapView;
            }

            if (isLoaded)
            {
                bitmapView.LoadDxfBitmaps();
            }
            else
            {
                bitmapView.DisposeBitmaps();
            }

            return bitmapView;
        }

        public void SetCurrentDxfBitmap(int zoomStep)
        {
            CurrentBitmap = GetBitmap(zoomStep, true);
        }

        private async Task CallUpdateLoadedBitmapsAsync()
        {
            while (true)
            {
                await Task.Run(UpdateLoadedBitmaps);
                await Task.Delay(20);
            }
        }

        private void UpdateLoadedBitmaps()
        {
            if (_lastUpdateBitmap == CurrentBitmap)
            {
                return;
            }

            DxfBitmapView[] newZoomedInLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];
            DxfBitmapView[] newZoomedOutLoadedBitmaps = new DxfBitmapView[_loadedBitmapsCount];

            for (int i = 0; i < _loadedBitmapsCount; i++)
            {
                int zoomStep = CurrentBitmap.ZoomStep + (i + 1);
                if (zoomStep <= MaxZoomStep)
                {
                    newZoomedInLoadedBitmaps[i] = GetBitmap(zoomStep, true);
                }
            }

            for (int i = 0; i < _loadedBitmapsCount; i++)
            {
                newZoomedOutLoadedBitmaps[i] = GetBitmap(CurrentBitmap.ZoomStep - (i + 1), true);
            }

            DisposeUnusedBitmaps(_zoomedInLoadedBitmaps);
            DisposeUnusedBitmaps(_zoomedOutLoadedBitmaps);

            _lastUpdateBitmap = CurrentBitmap;
            _zoomedInLoadedBitmaps = newZoomedInLoadedBitmaps;
            _zoomedOutLoadedBitmaps = newZoomedOutLoadedBitmaps;
        }

        private void DisposeUnusedBitmaps(DxfBitmapView[] bitmapArray)
        {
            foreach (var bitmapView in bitmapArray)
            {
                if (bitmapView != null && Math.Abs(bitmapView.ZoomStep - CurrentBitmap.ZoomStep) > _loadedBitmapsCount)
                {
                    bitmapView.DisposeBitmaps();
                }
            }
        }

        public void CreateTempFolder()
        {
            _tempFolderPath = Path.Combine(Path.GetTempPath(), "CadViewer");

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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BitmapCache()
        {
            Dispose(false);
        }
        #endregion
    }
}