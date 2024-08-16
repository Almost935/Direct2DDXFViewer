﻿using Direct2DDXFViewer.DrawingObjects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class BitmapCache : INotifyPropertyChanged
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
        private bool _isUpdatingBitmaps = false;
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

            InitializeBitmaps();    
        }
        #endregion

        #region Methods
        public void InitializeBitmaps()
        {
            CurrentBitmap = new(_deviceContext, _factory, _layerManager, _extentsMatrix, 1);
            
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
                    newBitmap = new DxfBitmap(_deviceContext, _factory, _layerManager, _extentsMatrix, zoom);
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
                    bitmap = new DxfBitmap(_deviceContext, _factory, _layerManager, _extentsMatrix, zoom);
                    _createdBitmaps.Add(zoom, bitmap);
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
            CallUpdateBitmapsAsync(CurrentBitmap);
        }
        private async Task CallUpdateBitmapsAsync(DxfBitmap bitmap)
        {
            if (!_isUpdatingBitmaps)
            {
                await Task.Run(() => UpdateBitmaps());
            }
        }
        private void UpdateBitmaps()
        { 
            _isUpdatingBitmaps = true;

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
            float upperLimit = CurrentBitmap.Zoom * (float)Math.Pow(_zoomFactor, _initializationFactor);
            float lowerLimit = CurrentBitmap.Zoom * (1 / (float)Math.Pow(_zoomFactor, _initializationFactor));

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

            //foreach (var bitmap in _zoomedOutLoadedBitmaps)
            //{
            //    Debug.WriteLine($"New _zoomedOutLoadedBitmaps: {bitmap.Zoom}");
            //}
            //foreach (var bitmap in _zoomedInLoadedBitmaps)
            //{
            //    Debug.WriteLine($"New _zoomedInLoadedBitmaps: {bitmap.Zoom}");
            //}

            _isUpdatingBitmaps = false;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion
    }
}
