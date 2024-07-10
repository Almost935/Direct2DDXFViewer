using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.Mathematics;
using SharpDX.DXGI;
using netDxf;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Input;
using SharpDX.Direct2D1.Effects;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Direct2DDXFViewer.DrawingObjects;
using netDxf.Entities;
using Direct2DDXFViewer.BitmapHelpers;

using Point = System.Windows.Point;
using Brush = SharpDX.Direct2D1.Brush;
using Geometry = SharpDX.Direct2D1.Geometry;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;
using RectangleGeometry = SharpDX.Direct2D1.RectangleGeometry;
using DashStyle = SharpDX.Direct2D1.DashStyle;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Factory1 = SharpDX.Direct2D1.Factory1;
using BitmapCache = Direct2DDXFViewer.BitmapHelpers.BitmapCache;

namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged
    {
        #region Fields
        private Matrix _transformMatrix = new();
        private Matrix _overallMatrix = new();
        private float _zoomFactor = 1.3f;
        private bool _isPanning = false;
        private bool _isRendering = false;
        private Point _lastTranslatePos = new();
        private bool _dxfLoaded = false;
        private Rect _currentView = new();
        private BackgroundWorker _snapBackgroundWorker;
        private BitmapCache _bitmapCache;
        private BitmapRenderTarget _currentBitmapRenderTarget;
        private bool _bitmapLoaded = false;
        private bool _bitmapRenderTargetNeedsUpdate = true;
        private Brush _highlightedBrush = null;
        private Brush _snappedBrush = null;
        private Brush _snappedHighlightedBrush = null;

        private DxfDocument _dxfDoc;
        private string _filePath = @"DXF\SmallDxf.dxf";
        private Point _pointerCoords = new();
        private Point _dxfPointerCoords = new();
        private Rect _extents = new();
        private ObjectLayerManager _layerManager;
        private DrawingObject _snappedObject;
        #endregion

        #region Properties
        public DxfDocument DxfDoc
        {
            get { return _dxfDoc; }
            set
            {
                _dxfDoc = value;
                OnPropertyChanged(nameof(DxfDoc));
            }
        }
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }
        public Point PointerCoords
        {
            get { return _pointerCoords; }
            set
            {
                _pointerCoords = value;
                OnPropertyChanged(nameof(PointerCoords));
            }
        }
        public Point DxfPointerCoords
        {
            get { return _dxfPointerCoords; }
            set
            {
                _dxfPointerCoords = value;
                OnPropertyChanged(nameof(DxfPointerCoords));
            }
        }
        public Rect Extents
        {
            get { return _extents; }
            set
            {
                _extents = value;
                OnPropertyChanged(nameof(Extents));
            }
        }
        public ObjectLayerManager LayerManager
        {
            get { return _layerManager; }
            set
            {
                _layerManager = value;
                OnPropertyChanged(nameof(LayerManager));
            }
        }
        public DrawingObject SnappedObject
        {
            get { return _snappedObject; }
            set
            {
                _snappedObject = value;
                OnPropertyChanged(nameof(SnappedObject));
            }
        }

        public Matrix ExtentsMatrix { get; set; } = new();
        #endregion

        #region Constructor
        public Direct2DDxfControl()
        {
            _snapBackgroundWorker = new();
            _snapBackgroundWorker.DoWork += SnapBackgroundWorker_DoWork;
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void LoadDxf(Factory1 factory, RenderTarget target)
        {
            DxfDoc = DxfDocument.Load(FilePath);
            if (DxfDoc is not null)
            {
                _dxfLoaded = true;
                Extents = DxfHelpers.GetExtentsFromHeader(DxfDoc);
                LayerManager = DxfHelpers.GetLayers(DxfDoc);
                DxfHelpers.LoadDrawingObjects(DxfDoc, LayerManager, factory, target);
            }
        }

        public Matrix GetInitialMatrix()
        {
            if (Extents == Rect.Empty)
            {
                return new Matrix();
            }
            else
            {
                Matrix matrix = new();

                double scaleX = this.ActualWidth / Extents.Width;
                double scaleY = this.ActualHeight / Extents.Height;

                double centerX = Extents.Left - (this.ActualWidth - Extents.Width) * 0.5;
                double centerY = Extents.Top - (this.ActualHeight - Extents.Height) * 0.5;
                matrix.Translate(-centerX, -centerY);

                if (scaleX < scaleY)
                {
                    matrix.ScaleAt(scaleX, -scaleX, this.ActualWidth / 2, this.ActualHeight / 2);
                }
                else
                {
                    matrix.ScaleAt(scaleY, -scaleY, this.ActualWidth / 2, this.ActualHeight / 2);
                }

                return matrix;
            }
        }

        public override void Render(RenderTarget target, DeviceContext1 deviceContext)
        {
            if (!_isRendering)
            {
                Stopwatch timer = new();

                _isRendering = true;

                GetBrushes(target);

                if (!_dxfLoaded)
                {
                    LoadDxf(resCache.Factory, target);
                    ExtentsMatrix = GetInitialMatrix();
                    _overallMatrix = ExtentsMatrix;
                }

                if (!_bitmapLoaded)
                {
                    timer.Restart();
                    InitializeBitmapCache(target);
                    _bitmapLoaded = true;
                    Debug.WriteLine($"bitmap initial load time: {timer.ElapsedMilliseconds} ms");
                }

                UpdateCurrentView();
                var viewport = new RawRectangleF((float)_currentView.Left, (float)_currentView.Top,
                    (float)_currentView.Right, (float)_currentView.Bottom);

                //target.PushAxisAlignedClip(viewport,
                //    AntialiasMode.PerPrimitive);

                timer.Restart();
                RenderBitmap(target);
                //target.PopAxisAlignedClip();

                Debug.WriteLine($"Bitmap render time: {timer.ElapsedMilliseconds} ms");

                _isRendering = false;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            UpdateTargetAndFactory(resCache.RenderTarget, resCache.Factory);

            _bitmapLoaded = false;
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float zoom;

            if (e.Delta > 0)
            {
                zoom = _zoomFactor;
            }
            else
            {
                zoom = 1f / _zoomFactor;
            }

            UpdateZoom(zoom);

            RenderTargetIsDirty = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointerCoords = e.GetPosition(this);

            if (_isPanning)
            {
                var translate = PointerCoords - _lastTranslatePos;
                UpdateTranslate(translate);
                _lastTranslatePos = PointerCoords;
                RenderTargetIsDirty = true;
            }
            //Update DxfPointerCoords in background thread
            if (!_snapBackgroundWorker.IsBusy)
            {
                _snapBackgroundWorker.RunWorkerAsync();
            }
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _lastTranslatePos = e.GetPosition(this);
            }
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = false;
                RenderTargetIsDirty = true;
            }
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _isPanning = false;
        }
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (Mouse.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
            }
        }

        private void InitializeBitmapCache(RenderTarget target)
        {
            if (_bitmapCache is not null)
            {
                _bitmapCache.Dispose();
                _bitmapCache = null;
            }
            _bitmapCache = new(target, resCache.Factory, LayerManager, Extents, target.Size, ExtentsMatrix);
        }
        private void RenderBitmap(RenderTarget target)
        {
            if (_bitmapCache is null) { return; }

            target.Transform = new((float)_transformMatrix.M11, (float)_transformMatrix.M12, (float)_transformMatrix.M21, (float)_transformMatrix.M22,
                    (float)_transformMatrix.OffsetX, (float)_transformMatrix.OffsetY);
            
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            
            RawRectangleF destRect = new(0, 0, (float)ActualWidth, (float)ActualHeight);
            RawRectangleF sourceRect = new(0, 0, (float)(ActualWidth) * 3, (float)(ActualHeight) * 3);

            target.DrawBitmap(_bitmapCache.InitialBitmapRenderTarget.Bitmap, destRect, 1.0f, BitmapInterpolationMode.Linear, sourceRect);
        }
        private void SnapBackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            UpdateDxfPointerCoords();
            //HitTestGeometry();
        }
        private void HitTestGeometry()
        {
            foreach (var layer in LayerManager.Layers.Values)
            {
                if (layer.IsVisible)
                {
                    foreach (var o in layer.DrawingObjects)
                    {
                        if (o.Geometry.StrokeContainsPoint(new RawVector2((float)DxfPointerCoords.X, (float)DxfPointerCoords.Y), 3))
                        {
                            SnappedObject = o;
                            SnappedObject.IsSnapped = true;

                            return;
                        }
                    }
                }
            }
        }
        private void UpdateDxfPointerCoords()
        {
            var newMatrix = _overallMatrix;
            newMatrix.Invert();
            DxfPointerCoords = newMatrix.Transform(PointerCoords);
        }
        private void UpdateZoom(float zoom)
        {
            if (!_isPanning)
            {
                _overallMatrix.ScaleAt(zoom, zoom, PointerCoords.X, PointerCoords.Y);
                _transformMatrix.ScaleAt(zoom, zoom, PointerCoords.X, PointerCoords.Y);

                //resCache.RenderTarget.Transform = new((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22,
                //    (float)_overallMatrix.OffsetX, (float)_overallMatrix.OffsetY);

                UpdateCurrentView();
            }
        }
        private void UpdateTranslate(Vector translate)
        {
            _overallMatrix.Translate(translate.X, translate.Y);
            _transformMatrix.Translate(translate.X, translate.Y);

            //resCache.RenderTarget.Transform = new((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22,
            //(float)(_overallMatrix.OffsetX), (float)(_overallMatrix.OffsetY));

            UpdateCurrentView();
        }
        private void UpdateCurrentView()
        {
            if (resCache.RenderTarget is not null)
            {
                _currentView = new(0, 0, resCache.RenderTarget.Size.Width, resCache.RenderTarget.Size.Height);
                var rawMatrix = resCache.RenderTarget.Transform;
                Matrix matrix = new(rawMatrix.M11, rawMatrix.M12, rawMatrix.M21, rawMatrix.M22, rawMatrix.M31, rawMatrix.M32);
                matrix.Invert();
                _currentView.Transform(matrix);
            }
        }
        private void GetBrushes(RenderTarget target)
        {
            _highlightedBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f));
            _snappedBrush ??= new SolidColorBrush(target, new RawColor4((109 / 255), 1.0f, (float)(139 / 255), 1.0f));
            _snappedHighlightedBrush ??= new SolidColorBrush(target, new RawColor4((150 / 255), 1.0f, (float)(171 / 255), 1.0f));
        }
        public void ZoomToExtents()
        {
            //_matrix = ExtentsMatrix;
            //resCache.RenderTarget.Transform = new((float)_matrix.M11, (float)_matrix.M12, (float)_matrix.M21, (float)_matrix.M22,
            //    (float)_matrix.OffsetX, (float)_matrix.OffsetY);
            //UpdateCurrentView();
            //RenderTargetIsDirty = true;
        }
        public void UpdateTargetAndFactory(RenderTarget target, Factory1 factory)
        {
            if (LayerManager is null) { return; }

            foreach (var layer in LayerManager.Layers.Values)
            {
                foreach (var o in layer.DrawingObjects)
                {
                    o.UpdateFactory(factory);
                    o.UpdateTarget(target);
                }
            }
        }


        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}

