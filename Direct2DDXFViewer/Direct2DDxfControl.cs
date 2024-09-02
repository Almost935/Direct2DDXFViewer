﻿using SharpDX;
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
using System.Drawing;
using System.Xml.Linq;
using System.IO;
using Direct2DControl;
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
using Bitmap = SharpDX.Direct2D1.Bitmap;
using BitmapCache = Direct2DDXFViewer.BitmapHelpers.BitmapCache;

namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private const int _zoomPrecision = 3;

        private Matrix _transformMatrix = new();
        private Matrix _overallMatrix = new();
        private readonly float _zoomFactor = 1.25f;
        private bool _isPanning = false;
        private bool _isRendering = false;
        private BitmapCache _bitmapCache;
        private bool _deviceContextIsDirty = true;
        private Point _lastTranslatePos = new();
        private bool _dxfLoaded = false;
        private Rect _currentDxfView;
        private bool _bitmapLoaded = false;
        private bool _disposed = false;
        private List<DrawingObject> _visibleDrawingObjects = new();
        private bool _visibleObjectsDirty = true;
        private int _objectDetailLevelTransitionNum = 500;
        private int _bitmapLevels;
        private BitmapRenderTarget _offscreenRenderTarget;

        private DxfDocument _dxfDoc;
        private string _filePath = @"DXF\LargeDxf.dxf";
        private Point _pointerCoords = new();
        private Point _dxfPointerCoords = new();
        private Rect _extents = new();
        private ObjectLayerManager _layerManager;
        private DrawingObject _snappedObject;
        private int _currentZoomStep = 0;

        private enum SnapMode { Point, Object };
        private SnapMode _snapMode = SnapMode.Object;
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
        /// <summary>
        /// The extents of the drawing objects in the DXF file.
        /// </summary>
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
        public int CurrentZoomStep
        {
            get { return _currentZoomStep; }
            set
            {
                _currentZoomStep = value;
                OnPropertyChanged(nameof(CurrentZoomStep));
            }
        }

        public List<DrawingObject> HighlightedObjects { get; set; } = new();
        public Matrix ExtentsMatrix { get; set; } = new();
        public Rect InitialView { get; set; }
        #endregion

        #region Constructor
        public Direct2DDxfControl()
        {
            UpdateDxfCoordsAsync();
            //RunHitTestAsync();

            //Window window = Application.Current.MainWindow;
            //window.KeyUp += Window_KeyUp;
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void LoadDxf(Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            DxfDoc = DxfDocument.Load(FilePath);
            if (DxfDoc is not null)
            {
                _dxfLoaded = true;
                Extents = DxfHelpers.GetExtentsFromHeader(DxfDoc);
                LayerManager = DxfHelpers.GetLayers(DxfDoc);
                
                int count = DxfHelpers.LoadDrawingObjects(DxfDoc, LayerManager, factory, deviceContext, resCache);
                _bitmapLevels = count / 1000;
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
        public void GetInitialView()
        {
            double centerX = (Extents.Left + Extents.Right) * 0.5;
            double centerY = (Extents.Top + Extents.Bottom) * 0.5;
            double scaledWidth = Math.Abs(this.ActualWidth / ExtentsMatrix.M11);
            double scaledHeight = Math.Abs(this.ActualHeight / ExtentsMatrix.M22);

            InitialView = new(centerX - 0.5 * scaledWidth, centerY - 0.5 * scaledHeight, scaledWidth, scaledHeight);
            _currentDxfView = InitialView;
        }
        public void InitializeBitmapCache(DeviceContext1 deviceContext, Factory1 factory)
        {
            RawMatrix3x2 extentsMatrix = new((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);
            _bitmapCache = new(deviceContext, factory, LayerManager, InitialView, extentsMatrix, _zoomFactor, _zoomPrecision, _bitmapLevels, resCache.MaxBitmapSize);
        }

        public override void Render(RenderTarget target, DeviceContext1 deviceContext)
        {
            GetBrushes(target);
            _offscreenRenderTarget ??= new(deviceContext, CompatibleRenderTargetOptions.None, new Size2F((float)ActualWidth, (float)ActualHeight));

            if (!_dxfLoaded)
            {
                LoadDxf(resCache.Factory, deviceContext, resCache);
                ExtentsMatrix = GetInitialMatrix();
                _overallMatrix = ExtentsMatrix;
                GetInitialView();
                GetVisibleObjects();
            }

            if (!_bitmapLoaded)
            {
                InitializeBitmapCache(deviceContext, resCache.Factory);
                _bitmapLoaded = true;
            }

            if (!_isRendering && deviceContext is not null && _dxfLoaded)
            {
                _isRendering = true;
                RenderAsync(deviceContext);
            }
        }

        private async void RenderAsync(DeviceContext1 deviceContext)
        {
            Stopwatch stopwatch = new();

            while (_isRendering)
            {
                stopwatch.Restart();
                int delay = 17;

                if (LayerManager is not null && deviceContext is not null && _deviceContextIsDirty)
                {
                    deviceContext.BeginDraw();
                    deviceContext.Clear(new RawColor4(1, 1, 1, 1));

                    if (CurrentZoomStep > _bitmapCache.MaxZoomStep)
                    {
                        if (_visibleObjectsDirty)
                        {
                            GetVisibleObjects();
                        }

                        RenderVisibleObjectsToBitmap(_offscreenRenderTarget);
                        deviceContext.DrawBitmap(_offscreenRenderTarget.Bitmap, new RawRectangleF(0, 0, (float)ActualWidth, (float)ActualHeight), 1.0f, BitmapInterpolationMode.Linear);
                    }
                    else
                    {
                        RenderBitmaps(deviceContext);
                    }

                    deviceContext.EndDraw();
                    resCache.Device.ImmediateContext.Flush();

                    stopwatch.Stop();
                    int elapsedTime = (int)stopwatch.ElapsedMilliseconds;
                    delay = 17 - elapsedTime;
                    _deviceContextIsDirty = false;
                }

                if (delay > 0)
                {
                    await Task.Delay(delay);
                }
            }
        }

        private void RenderBitmaps(DeviceContext1 deviceContext)
        {
            var rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            foreach (var dxfBitmap in _bitmapCache.CurrentBitmap.Bitmaps)
            {
                var destRect = dxfBitmap.DestRect;
                destRect.Transform(_transformMatrix);

                if (!rect.Contains(destRect) && !rect.IntersectsWith(destRect)) continue;

                var destRawRect = new RawRectangleF((float)destRect.Left, (float)destRect.Top, (float)destRect.Right, (float)destRect.Bottom);
                deviceContext.DrawBitmap(dxfBitmap.Bitmap, destRawRect, 1.0f, BitmapInterpolationMode.Linear);
            }
        }
        private void RenderVisibleObjectsToBitmap(RenderTarget renderTarget)
        { 
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(1, 1, 1, 1));  

            renderTarget.Transform = new RawMatrix3x2((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22, (float)_overallMatrix.OffsetX, (float)_overallMatrix.OffsetY);

            if (_visibleDrawingObjects.Count >= _objectDetailLevelTransitionNum) { renderTarget.AntialiasMode = AntialiasMode.Aliased; }
            else { renderTarget.AntialiasMode = AntialiasMode.PerPrimitive; }

            foreach (var drawingObject in _visibleDrawingObjects)
            {
                drawingObject.DrawToRenderTarget(renderTarget, 1, drawingObject.Brush);
            }
            renderTarget.EndDraw();
        }
        private void RenderInteractiveObjects(RenderTarget target)
        {

        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            UpdateDeviceContextAndFactory(resCache.DeviceContext, resCache.Factory);

            ExtentsMatrix = GetInitialMatrix();
            _overallMatrix = ExtentsMatrix;
            _bitmapLoaded = false;
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float zoom;

            if (e.Delta > 0)
            {
                zoom = _zoomFactor;
                CurrentZoomStep += 1;
            }
            else
            {
                zoom = 1 / _zoomFactor;
                CurrentZoomStep -= 1;
            }

            UpdateZoom(zoom);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointerCoords = e.GetPosition(this);

            if (_isPanning)
            {
                var translate = PointerCoords - _lastTranslatePos;

                if (translate.LengthSquared < 0.5) { return; } //Prevent unneccessary translations

                UpdateTranslate(translate);
                _lastTranslatePos = PointerCoords;
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
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                if (SnappedObject is not null)
                {
                    if (HighlightedObjects.Contains(SnappedObject))
                    {
                        SnappedObject.IsHighlighted = false;
                        HighlightedObjects.Remove(SnappedObject);
                    }
                    else
                    {
                        SnappedObject.IsHighlighted = true;
                        HighlightedObjects.Add(SnappedObject);
                    }
                }
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
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResetSelectedObjects();
            }
        }

        private async void RunHitTestAsync()
        {
            while (true)
            {
                await Task.Delay(100);

                //if (_snapMode == SnapMode.Point)
                //{
                //    await Task.Run(() => HitTestPoints());
                //}
                if (_snapMode == SnapMode.Object)
                {
                    await Task.Run(() => HitTestGeometry());
                }
            }
        }
        private void HitTestGeometry()
        {
            if (LayerManager is null) { return; }

            float thickness = (float)(2 / _transformMatrix.M11);

            // Check if mouse is still over the same object
            if (SnappedObject is not null)
            {
                if (SnappedObject.Geometry.StrokeContainsPoint(new RawVector2((float)DxfPointerCoords.X, (float)DxfPointerCoords.Y), thickness, SnappedObject.StrokeStyle))
                {
                    return;
                }
                else
                {
                    SnappedObject.IsSnapped = false;
                    SnappedObject = null;
                }
            }

            foreach (var layer in LayerManager.Layers.Values)
            {
                if (layer.IsVisible)
                {
                    foreach (var o in layer.DrawingObjects)
                    {
                        if (o.Geometry.StrokeContainsPoint(new RawVector2((float)DxfPointerCoords.X, (float)DxfPointerCoords.Y), thickness, o.StrokeStyle))
                        {
                            SnappedObject = o;
                            SnappedObject.IsSnapped = true;

                            return;
                        }
                    }
                }
            }
        }
        private void HitTestPoints()
        {

        }
        private void GetVisibleObjects()
        {
            if (LayerManager is not null)
            {
                _visibleDrawingObjects.Clear();

                int count = 0;

                

                foreach (var layer in LayerManager.Layers.Values)
                {
                    if (layer is not null)
                    {
                        foreach (var obj in layer.DrawingObjects)
                        {
                            Rect view = _currentDxfView;
                            view.Inflate(_currentDxfView.Width * 0.25, _currentDxfView.Height * 0.25);
                            obj.IsInView = obj.DrawingObjectIsInRect(_currentDxfView);

                            if (obj.IsInView)
                            {
                                _visibleDrawingObjects.Add(obj);
                                count++;
                            }
                        }
                    }
                }
            }
        }
        private async void UpdateDxfCoordsAsync()
        {
            while (true)
            {
                await Task.Delay(50);
                await Task.Run(() => UpdateDxfPointerCoords());
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
                UpdateCurrentView();
                _visibleObjectsDirty = true;
                _deviceContextIsDirty = true;

                if (CurrentZoomStep <= _bitmapCache.MaxZoomStep)
                {
                    _bitmapCache.SetCurrentDxfBitmap(CurrentZoomStep);
                }
            }
        }
        private void UpdateTranslate(Vector translate)
        {
            _overallMatrix.Translate(translate.X, translate.Y);
            _transformMatrix.Translate(translate.X, translate.Y);
            UpdateCurrentView();
            _visibleObjectsDirty = true;
            _deviceContextIsDirty = true;
        }

        private void UpdateCurrentView()
        {
            if (resCache.RenderTarget is not null)
            {
                _currentDxfView = new(0, 0, this.ActualWidth, this.ActualHeight);
                var matrix = _overallMatrix;
                matrix.Invert();
                _currentDxfView.Transform(matrix);
            }
        }
        private void GetBrushes(RenderTarget target)
        {
            resCache.HighlightedBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f));

            resCache.HighlightedOuterEdgeBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f))
            { Opacity = 0.2f };
        }
        private void UpdateDeviceContextAndFactory(DeviceContext1 deviceContext, Factory1 factory)
        {
            if (LayerManager is null) { return; }

            foreach (var layer in LayerManager.Layers.Values)
            {
                foreach (var o in layer.DrawingObjects)
                {
                    o.UpdateFactory(factory);
                    o.UpdateDeviceContext(deviceContext);
                    o.UpdateGeometry();
                }
            }
        }
        private void ResetSelectedObjects()
        {
            foreach (var o in HighlightedObjects)
            {
                o.IsHighlighted = false;
            }
            HighlightedObjects.Clear();
        }

        public void ZoomToExtents()
        {
            _overallMatrix = ExtentsMatrix;
            _transformMatrix = new();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {

        }

        ~Direct2DDxfControl()
        {
            Dispose(false);
        }
        #endregion
    }
}

