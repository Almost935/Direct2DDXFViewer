using Direct2DDXFViewer.DrawingObjects;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
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
        private Dictionary<float, DxfBitmap> _bitmaps = new();
        private DeviceContext1 _deviceContext;
        private ObjectLayerManager _layerManager;
        private float _currentZoom;
        private RawMatrix3x2 _extentsMatrix;
        private float _zoomFactor;

        private const int initializationFactor = 5;
        #endregion

        #region Properties
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
        public BitmapCache(DeviceContext1 deviceContext, ObjectLayerManager layerManager, RawMatrix3x2 extentsMatrix, float zoomFactor)
        {
            _deviceContext = deviceContext;
            _layerManager = layerManager;
            _extentsMatrix = extentsMatrix;
            _zoomFactor = zoomFactor;

            InitializeBitmaps();    
        }
        #endregion

        #region Methods
        public DxfBitmap GetDxfBitmap(float zoom)
        {
            _bitmaps.TryGetValue(zoom, out DxfBitmap bitmap);
            return bitmap;
        }

        public void InitializeBitmaps()
        {
            if (_layerManager is not null && _deviceContext is not null)
            {
                // Initialize first view
                _bitmaps.TryAdd((float)Math.Round((double)1, 3), new DxfBitmap(_deviceContext, 1, RenderBitmap(_deviceContext, 1)));

                // Initialize zoomed in bitmaps
                float zoom = 1;
                for (int i = 1; i <= initializationFactor; i++)
                {
                    zoom *= _zoomFactor;
                    _bitmaps.TryAdd((float)Math.Round((double)zoom, 3), new DxfBitmap(_deviceContext, zoom, RenderBitmap(_deviceContext, zoom)));
                }

                // Initialize zoomed out bitmaps
                zoom = 1;
                for (int i = 1; i <= initializationFactor; i++)
                {
                    zoom *= (1 / _zoomFactor);
                    _bitmaps.TryAdd((float)Math.Round((double)zoom, 3), new DxfBitmap(_deviceContext, zoom, RenderBitmap(_deviceContext, zoom)));
                }
            }
        }

        private void UpdateBitmapsDictionary(float intialZoom)
        {
            if (_layerManager is not null && _deviceContext is not null)
            {
                // Initialize first view
                _bitmaps.TryAdd((float)Math.Round((double)1, 3), new DxfBitmap(_deviceContext, 1, RenderBitmap(_deviceContext, 1)));

                // Initialize zoomed in bitmaps
                float zoom = 1;
                for (int i = 1; i <= initializationFactor; i++)
                {
                    zoom *= _zoomFactor;

                    _bitmaps.TryAdd((float)Math.Round((double)zoom, 3), new DxfBitmap(_deviceContext, zoom, RenderBitmap(_deviceContext, zoom)));
                }

                // Initialize zoomed out bitmaps
                zoom = 1;
                for (int i = 1; i <= initializationFactor; i++)
                {
                    zoom *= (1 / _zoomFactor);
                    _bitmaps.TryAdd((float)Math.Round((double)zoom, 3), new DxfBitmap(_deviceContext, zoom, RenderBitmap(_deviceContext, zoom)));
                }
            }
        }

        private Bitmap RenderBitmap(DeviceContext1 deviceContext, float zoom)
        {
            Size2F size = new(deviceContext.Size.Width * zoom, deviceContext.Size.Height * zoom);
            BitmapRenderTarget bitmapRenderTarget = new(deviceContext, CompatibleRenderTargetOptions.None, size)
            {
                DotsPerInch = new Size2F(96.0f * zoom, 96.0f * zoom),
                AntialiasMode = AntialiasMode.Aliased
            };

            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Transform = _extentsMatrix;
            _layerManager.DrawToRenderTarget(bitmapRenderTarget, 1);
            bitmapRenderTarget.EndDraw();
            return bitmapRenderTarget.Bitmap;
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
