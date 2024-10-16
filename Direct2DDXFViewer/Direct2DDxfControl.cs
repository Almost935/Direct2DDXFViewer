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
using Direct2DDXFViewer.Helpers;
using System.Windows.Controls;
using static netDxf.Entities.HatchBoundaryPath;
using System.Windows.Media.Animation;

namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private const int _zoomPrecision = 3;
        private const int _bitmapReuseFactor = 2;
        private const float _zoomFactor = 1.3f;
        private const float _snappedThickness = 5;
        private const float _snappedOpacity = 0.35f;

        // Offscreen bitmap fields
        private BitmapRenderTarget _offscreenRenderTarget;
        private OffscreenBitmap _currentOffscreenBitmap;
        private Matrix _currentOffscreenBitmapTransform = new();
        /// <summary>
        /// Represents the size factor of the offscreen bitmap in relation to the screen size.
        /// </summary>
        private const float _offscreenBitmapSizeFactor = 4;
        private bool _offscreenBitmapIsDirty = true;
        private Vector _distFromOffscreenBitmapUpdate = new();
        private Vector _maxDistFromOffscreenBitmapUpdate;
        private (float x, float y) _offscreenBitmapCenteringOffset;
        private bool _updateOffscreenBitmapThreadRunning = false;

        // Zooming and panning matrices
        private RawMatrix3x2 _rawExtentsMatrix = new();
        private Matrix _transformMatrix = new();
        private Matrix _overallMatrix = new();

        private bool _isPanning = false;
        private bool _deviceContextIsDirty = true;
        private Point _lastTranslatePos = new();
        private bool _dxfLoaded = false;
        private int _dxfObjectCount;
        private Rect _currentView;
        private Rect _currentDxfView;
        private List<DrawingObject> _visibleDrawingObjects = new();
        private bool _visibleObjectsDirty = true;
        private int _objectDetailLevelTransitionNum = 500;
        private BitmapRenderTarget _interactiveRenderTarget;
        private QuadTreeCache _quadTreeCache;
        private DrawingObjectTree _drawingObjectTree;
        private Brush _highlightedBrush;
        private Brush _highlightedOuterEdgeBrush;

        // Layer bitmap rendering test fields
        private Dictionary<int, List<Bitmap>> _layerBitmaps = [];
        private const int initialBitmapLoad = 2;
        private bool _layerBitmapsDirty = true;
        private bool _currentOverallBitmapDirty = true;

        // Hit testing fields
        private Point _lastHitTestPos = new();
        private DrawingObjectNode _lastHitTestNode;
        private float _hittestStrokeThickness;

        private DxfDocument _dxfDoc;
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
            _overallMatrix = new((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22, (float)_overallMatrix.OffsetX, (float)_overallMatrix.OffsetY);

            UpdateDxfCoordsAsync();
            RunGetVisibleObjectsAsync();
            RunHitTestAsync();

            //Window window = Application.Current.MainWindow;
            //window.KeyUp += Window_KeyUp;
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void Initialize(DxfDocument dxfDoc)
        {
            if (dxfDoc is not null) { DxfDoc = dxfDoc; }
        }

        public void LoadDxf(Factory1 factory, DeviceContext1 deviceContext, ResourceCache resCache)
        {
            if (DxfDoc is not null)
            {
                _dxfLoaded = true;
                Extents = DxfHelpers.GetExtentsFromHeader(DxfDoc);
                ExtentsMatrix = GetInitialMatrix();
                _overallMatrix = ExtentsMatrix;
                _rawExtentsMatrix = new((float)ExtentsMatrix.M11, (float)ExtentsMatrix.M12, (float)ExtentsMatrix.M21, (float)ExtentsMatrix.M22, (float)ExtentsMatrix.OffsetX, (float)ExtentsMatrix.OffsetY);
                LayerManager = DxfHelpers.GetLayers(DxfDoc, deviceContext, factory, resCache);
                _hittestStrokeThickness = (float)(8 / ExtentsMatrix.M11);

                _dxfObjectCount = DxfHelpers.LoadDrawingObjects(DxfDoc, LayerManager, factory, deviceContext, resCache);
                foreach (var layer in LayerManager.Layers.Values)
                {
                    layer.LoadGeometryGroup();
                }

                _drawingObjectTree = new(LayerManager, Extents, 4);
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

            double left = centerX - scaledWidth / 2;
            double top = centerY - scaledHeight / 2;

            InitialView = new(left, top, scaledWidth, scaledHeight);
            _currentDxfView = InitialView;
            _currentView = new(0, 0, ActualWidth, ActualHeight);
        }

        public override void Render()
        {
            if (DxfDoc is not null)
            {
                GetResources(d2DDeviceContext);

                _offscreenRenderTarget ??= new(d2DDeviceContext, CompatibleRenderTargetOptions.None, new Size2F((float)ActualWidth * _offscreenBitmapSizeFactor,
                    (float)ActualHeight * _offscreenBitmapSizeFactor));
                _interactiveRenderTarget ??= new(d2DDeviceContext, CompatibleRenderTargetOptions.None, new Size2F((float)ActualWidth, (float)ActualHeight));

                if (!_dxfLoaded)
                {
                    LoadDxf(resCache.Factory, d2DDeviceContext, resCache);
                    GetInitialView();
                    GetVisibleObjects();

                    _maxDistFromOffscreenBitmapUpdate = new(((_offscreenRenderTarget.Size.Width / 2) - (d2DDeviceContext.Size.Width / 2)), ((_offscreenRenderTarget.Size.Height / 2) - (d2DDeviceContext.Size.Height / 2)));
                    _offscreenBitmapCenteringOffset = ((float)_maxDistFromOffscreenBitmapUpdate.X, (float)_maxDistFromOffscreenBitmapUpdate.Y);

                    UpdateOffscreenRenderTarget();

                    if (!_updateOffscreenBitmapThreadRunning) { RunUpdateOffscreenRenderTargetAsync(); }
                }

                if (d2DDeviceContext is null) { return; }
                if (d2DDeviceContext.IsDisposed) { return; }

                if (LayerManager is not null && d2DDeviceContext is not null && !d2DDeviceContext.IsDisposed && _deviceContextIsDirty)
                {
                    if (_currentOffscreenBitmap is null) { UpdateOffscreenRenderTarget(); }

                    d2DDeviceContext.Clear(new RawColor4(1, 1, 1, 1));

                    RenderOffscreenBitmap(d2DDeviceContext);
                    RenderInteractiveObjects(d2DDeviceContext, _interactiveRenderTarget);

                    _deviceContextIsDirty = false;
                }
            }
        }

        private async Task RunUpdateOffscreenRenderTargetAsync()
        {
            while (true)
            {
                await Task.Run(() => UpdateOffscreenRenderTarget());
                await Task.Delay(100);
            }
        }
        private void UpdateOffscreenRenderTarget()
        {
            if (_offscreenBitmapIsDirty && _offscreenRenderTarget is not null && !_offscreenRenderTarget.IsDisposed)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                _offscreenRenderTarget.BeginDraw();
                _offscreenRenderTarget.Clear(new RawColor4(1, 1, 1, 0));

                int zoomStep = _currentZoomStep;
                Matrix matrix = _overallMatrix;
                matrix.Translate(_offscreenBitmapCenteringOffset.x, _offscreenBitmapCenteringOffset.y); // Translation is to center the bitmap in the render target
                RawMatrix3x2 rawMatrix = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);
                _offscreenRenderTarget.Transform = rawMatrix;

                Parallel.ForEach(LayerManager.Layers.Values, layer =>
                {
                    if (layer.GeometryGroup is not null)
                    {
                        _offscreenRenderTarget.DrawGeometry(layer.GeometryGroup, layer.LayerBrush, 0.25f, layer.HairlineStrokeStyle);
                    }
                });

                _offscreenRenderTarget.EndDraw();

                var prevBitmap = _currentOffscreenBitmap;
                _currentOffscreenBitmap = new(zoomStep, _offscreenRenderTarget.Bitmap);
                _distFromOffscreenBitmapUpdate = new();
                _currentOffscreenBitmapTransform = new();
                _deviceContextIsDirty = true;
                prevBitmap?.Dispose();

                stopwatch.Stop();
                //Debug.WriteLine($"UpdateOffscreenRenderTarget Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");

                // Verify that the bitmap was updated correctly
                if (_currentOffscreenBitmap.ZoomStep == _currentZoomStep) { _offscreenBitmapIsDirty = false; }
            }
        }

        private void RenderOffscreenBitmap(DeviceContext1 deviceContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Matrix matrix = _currentOffscreenBitmapTransform;
            matrix.Translate(-_offscreenBitmapCenteringOffset.x, -_offscreenBitmapCenteringOffset.y); // Translation is to center the bitmap in the render target
            RawMatrix3x2 rawMatrix = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.OffsetX, (float)matrix.OffsetY);
            deviceContext.Transform = rawMatrix;

            (float X, float Y) sourceRectOffset = new(0, 0);
            RawRectangleF sourceRect = new(0 + sourceRectOffset.X, 0 + sourceRectOffset.Y, _currentOffscreenBitmap.Bitmap.Size.Width + sourceRectOffset.X, _currentOffscreenBitmap.Bitmap.Size.Height + sourceRectOffset.Y);

            deviceContext.DrawBitmap(_currentOffscreenBitmap.Bitmap, 1.0f, BitmapInterpolationMode.Linear, sourceRect);

            //Debug.WriteLine($"_currentOffscreenBitmap.ZoomStep: {_currentOffscreenBitmap.ZoomStep}");

            stopwatch.Stop();
            //Debug.WriteLine($"RenderOffscreenBitmap Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void RenderInteractiveObjects(DeviceContext1 deviceContext, BitmapRenderTarget renderTarget)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            deviceContext.Transform = new((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22, (float)_overallMatrix.OffsetX, (float)_overallMatrix.OffsetY);
            RenderSnappedObjects(deviceContext);
            RenderHighlightedObjects(deviceContext);

            stopwatch.Stop();
            //Debug.WriteLine($"DrawInteractiveObjects Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void RenderSnappedObjects(DeviceContext1 deviceContext)
        {
            var objCopy = SnappedObject;
            if (objCopy is not null)
            {
                //renderTarget.BeginDraw();
                //renderTarget.Clear(new RawColor4(1, 1, 1, 0));
                //renderTarget.Transform = _rawExtentsMatrix;
                //resCache.SnappedEffect.SetInput(0, renderTarget.Bitmap, true);
                //objCopy.DrawToRenderTarget(renderTarget, _snappedThickness, objCopy.Brush, objCopy.FixedStrokeStyle);
                //renderTarget.EndDraw();
                //deviceContext.DrawBitmap(renderTarget.Bitmap, _snappedOpacity, InterpolationMode.Linear);

                objCopy.DrawToDeviceContext(deviceContext, _snappedThickness, objCopy.OuterEdgeBrush, objCopy.FixedStrokeStyle);
            }
        }
        private void RenderHighlightedObjects(DeviceContext1 deviceContext)
        {
            var copy = HighlightedObjects.ToList();
            foreach (var obj in copy)
            {
                obj.DrawToDeviceContext(deviceContext, 2, _highlightedBrush, obj.FixedStrokeStyle);
            }
        }

        private void RenderLayersToDeviceContext(DeviceContext1 deviceContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            deviceContext.Transform = new((float)_overallMatrix.M11, (float)_overallMatrix.M12, (float)_overallMatrix.M21, (float)_overallMatrix.M22, (float)_overallMatrix.OffsetX, (float)_overallMatrix.OffsetY);

            Parallel.ForEach(LayerManager.Layers.Values, layer =>
            {
                if (layer.GeometryGroup is not null)
                {
                    deviceContext.DrawGeometry(layer.GeometryGroup, layer.LayerBrush, 1, layer.HairlineStrokeStyle);
                }
            });

            stopwatch.Stop();
            //Debug.WriteLine($"RenderLayersToDeviceContext Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void RenderQuadTree(DeviceContext1 deviceContext, QuadTree quadTree)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            var nodes = quadTree.GetIntersectingNodes(_currentView);

            foreach (var node in nodes)
            {
                var destRect = node.DestRect;
                destRect.Transform(_transformMatrix);
                var destRawRect = new RawRectangleF((float)destRect.Left, (float)destRect.Top, (float)destRect.Right, (float)destRect.Bottom);

                deviceContext.DrawBitmap(node.Bitmap, destRawRect, 1.0f, BitmapInterpolationMode.Linear);
            }

            stopwatch.Stop();
            //Debug.WriteLine($"RenderQuadTree Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }
        private void RenderLayerBitmaps(DeviceContext1 deviceContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            //var bitmaps = _layerBitmaps[_currentZoomStep];
            bool bitmapsExist = _layerBitmaps.TryGetValue(0, out var bitmaps);

            RawRectangleF sourceRect = new((float)(ActualWidth / 2), (float)(ActualWidth / 2), (float)(ActualWidth * 1.5), (float)(ActualHeight * 1.5));
            RawRectangleF testSourceRect = new(0, 0, (float)(ActualWidth * 2), (float)(ActualHeight * 2));
            RawRectangleF destRect = new(0, 0, (float)(ActualWidth), (float)(ActualHeight));

            if (bitmapsExist)
            {
                deviceContext.Transform = new((float)_transformMatrix.M11, (float)_transformMatrix.M12, (float)_transformMatrix.M21, (float)_transformMatrix.M22, (float)_transformMatrix.OffsetX, (float)_transformMatrix.OffsetY);

                Parallel.ForEach(bitmaps, bitmap =>
                {
                    deviceContext.DrawBitmap(bitmap, destRect, 1.0f, BitmapInterpolationMode.Linear);
                });
            }

            stopwatch.Stop();
            Debug.WriteLine($"RenderLayerBitmaps Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
        }


        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            UpdateDeviceContext(resCache.DeviceContext);

            _offscreenRenderTarget?.Dispose();
            _offscreenRenderTarget = null;
            _currentOffscreenBitmap?.Dispose();
            _currentOffscreenBitmap = null;

            _highlightedBrush?.Dispose();
            _highlightedBrush = null;
            _highlightedOuterEdgeBrush?.Dispose();
            _highlightedOuterEdgeBrush = null;

            ExtentsMatrix = GetInitialMatrix();
            _overallMatrix = ExtentsMatrix;

            _offscreenBitmapIsDirty = true;
            UpdateOffscreenRenderTarget();
            _deviceContextIsDirty = true;
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

                if (translate.LengthSquared < 1) { return; } //Prevent unneccessary translations

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
                ResetHighlightedObjects();
            }
        }

        private async void RunHitTestAsync()
        {
            while (true)
            {
                //if (_snapMode == SnapMode.Point)
                //{
                //    await Task.Run(() => HitTestPoints());
                //}
                if (_snapMode == SnapMode.Object)
                {
                    await Task.Run(() => HitTestGeometry());
                }
                await Task.Delay(10);
            }
        }
        private void HitTestGeometry()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (_offscreenBitmapIsDirty) { return; }
            if (_isPanning) { return; }
            if (LayerManager is null) { return; }

            var mousePos = DxfPointerCoords;
            var rawMousePos = new RawVector2((float)mousePos.X, (float)mousePos.Y);
            float thickness = (float)(_hittestStrokeThickness / _transformMatrix.M11);

            // Check if mouse is still over the same object
            var snappedCopy = SnappedObject;
            if (snappedCopy is not null)
            {
                if (snappedCopy.Hittest(rawMousePos, thickness))
                {
                    return;
                }
                else
                {
                    ResetSnappedObjects();
                    _deviceContextIsDirty = true;
                }
            }
            if (_lastHitTestNode is null)
            {
                _lastHitTestNode = _drawingObjectTree.GetIntersectingNode(mousePos);
                if (_lastHitTestNode is null) { return; }
            }
            else if (!_lastHitTestNode.Extents.Contains(mousePos))
            {
                _lastHitTestNode = _drawingObjectTree.GetIntersectingNode(mousePos);
                if (_lastHitTestNode is null) { return; }
            }

            Parallel.ForEach(_lastHitTestNode.DrawingObjects, obj =>
            {
                if (obj.Layer.IsVisible && obj.Bounds.Contains(mousePos))
                {
                    if (obj.Hittest(rawMousePos, thickness))
                    {
                        SnappedObject = obj;
                        SnappedObject.IsSnapped = true;
                        _deviceContextIsDirty = true;

                        return;
                    }
                }
            });

            stopwatch.Stop();
        }
        private void HitTestPoints()
        {

        }


        private async Task RunGetVisibleObjectsAsync()
        {
            while (true)
            {
                await Task.Delay(500);
                await Task.Run(() => GetVisibleObjects());
            }
        }
        private void GetVisibleObjects()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();


            if (LayerManager is not null && _visibleObjectsDirty && _drawingObjectTree is not null)
            {
                _visibleDrawingObjects.Clear();
                var views = _drawingObjectTree.GetIntersectingNodes(_currentDxfView);
                foreach (var view in views)
                {
                    foreach (var obj in view.DrawingObjects)
                    {
                        if (obj.Layer.IsVisible)
                        {
                            obj.IsInView = obj.DrawingObjectIsInRect(_currentDxfView);
                            if (obj.IsInView)
                            {
                                _visibleDrawingObjects.Add(obj);
                            }
                        }
                    }
                }
                //Debug.WriteLine($"New get visibile objects: {stopwatch.ElapsedMilliseconds}");

                _visibleObjectsDirty = false;
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
                ResetSnappedObjects();

                _visibleObjectsDirty = true;
                _deviceContextIsDirty = true;
                _offscreenBitmapIsDirty = true;
            }
        }
        private void UpdateTranslate(Vector translate)
        {
            if (translate.LengthSquared < 1) return; // Prevent unnecessary translations

            _overallMatrix.Translate(translate.X, translate.Y);
            _transformMatrix.Translate(translate.X, translate.Y);
            _currentOffscreenBitmapTransform.Translate(translate.X, translate.Y);
            _distFromOffscreenBitmapUpdate += translate;

            UpdateCurrentView();
            ResetSnappedObjects();

            _visibleObjectsDirty = true;
            _deviceContextIsDirty = true;

            if (Math.Abs(_distFromOffscreenBitmapUpdate.X) > _maxDistFromOffscreenBitmapUpdate.X + 200 ||
                Math.Abs(_distFromOffscreenBitmapUpdate.Y) > _maxDistFromOffscreenBitmapUpdate.Y + 200) { _offscreenBitmapIsDirty = true; }

            //Debug.WriteLine($"\n_distFromOffscreenBitmapUpdate.X and Y: {_distFromOffscreenBitmapUpdate.X}, {_distFromOffscreenBitmapUpdate.Y}" +
            //    $"\n_maxDistFromOffscreenBitmapUpdate: {_maxDistFromOffscreenBitmapUpdate.X}, {_maxDistFromOffscreenBitmapUpdate.Y}");
        }

        private void UpdateCurrentView()
        {
            if (resCache.RenderTarget is not null)
            {
                // Get the current view in dxf coordinates
                _currentDxfView = new(0, 0, this.ActualWidth, this.ActualHeight);
                var overallMatrix = _overallMatrix;
                overallMatrix.Invert();
                _currentDxfView.Transform(overallMatrix);

                // Get the current view in screen coordinates
                _currentView = new(0, 0, this.ActualWidth, this.ActualHeight);
                var transformMatrix = _transformMatrix;
                transformMatrix.Invert();
                _currentView.Transform(transformMatrix);
            }
        }
        private void GetResources(DeviceContext1 deviceContext)
        {
            _highlightedBrush ??= new SolidColorBrush(deviceContext, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f));

            _highlightedOuterEdgeBrush ??= new SolidColorBrush(deviceContext, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f))
            { Opacity = 0.2f };

        }
        private void UpdateDeviceContext(DeviceContext1 deviceContext)
        {
            if (LayerManager is null) { return; }

            foreach (var layer in LayerManager.Layers.Values)
            {
                foreach (var o in layer.DrawingObjects)
                {
                    o.UpdateDeviceContext(deviceContext);
                }
                layer.UpdateDeviceDependentResources(deviceContext);
            }
        }
        public void ZoomToExtents()
        {
            _overallMatrix = ExtentsMatrix;
            _transformMatrix = new();
        }
        public void ResetInteractiveObjects()
        {
            ResetHighlightedObjects();
            ResetSnappedObjects();
        }
        private void ResetHighlightedObjects()
        {
            var copy = HighlightedObjects.ToList();
            foreach (var o in HighlightedObjects)
            {
                o.IsHighlighted = false;
            }
            HighlightedObjects.Clear();
        }
        private void ResetSnappedObjects()
        {
            var snappedCopy = SnappedObject;
            if (snappedCopy is not null)
            {
                snappedCopy.IsSnapped = false;
                SnappedObject = null;
            }
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

