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
using System.Drawing;
using System.Xml.Linq;

namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private Matrix _transformMatrix = new();
        private Matrix _overallMatrix = new();
        private readonly float _zoomFactor = 1.3f;
        private bool _isPanning = false;
        private bool _isRendering = false;
        private Point _lastTranslatePos = new();
        private bool _dxfLoaded = false;
        private Rect _currentView;
        private BitmapCache _bitmapCache;
        private QuadTreeCache _quadTreeCache;
        private bool _bitmapLoaded = false;
        private Rect[] _bounds;
        private bool _disposed = false;

        private DxfDocument _dxfDoc;
        private string _filePath = @"DXF\SmallDxf.dxf";
        private Point _pointerCoords = new();
        private Point _dxfPointerCoords = new();
        private Rect _extents = new();
        private ObjectLayerManager _layerManager;
        private DrawingObject _snappedObject;
        private List<DrawingObject> _visibleObjects = new();

        private enum SnapMode { Point, Object };
        private SnapMode _snapMode = SnapMode.Point;
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

        public List<DrawingObject> HighlightedObjects { get; set; } = new();
        public Matrix ExtentsMatrix { get; set; } = new();
        public Rect InitialView { get; set; }
        #endregion

        #region Constructor
        public Direct2DDxfControl()
        {
            UpdateDxfCoordsAsync();
            UpdateCurrentView();
            //RunHitTestAsync();
            //RunGetVisibleObjectsAsync();

            //Window window = Application.Current.MainWindow;
            //window.KeyUp += Window_KeyUp;
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

            _bounds = new[] {
                new Rect(0, 0, (ActualWidth / 2), (ActualHeight / 2)),
                new Rect((ActualWidth / 2), 0, (ActualWidth / 2), (ActualHeight / 2)),
                new Rect(0, (ActualHeight / 2), (ActualWidth / 2), (ActualHeight / 2)),
                new((ActualWidth / 2), (ActualHeight / 2), (ActualWidth / 2), (ActualHeight / 2))
                };
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
            double centerX = 0.5 * (Extents.Left + Extents.Right);
            double centerY = 0.5 * (Extents.Top + Extents.Bottom);
            Rect rect = new(centerX - 0.5 * ActualWidth, centerY - 0.5 * ActualHeight, ActualWidth, ActualHeight);
            Matrix matrix = new();
            matrix.ScaleAt(1 / ExtentsMatrix.M11, 1 / ExtentsMatrix.M11, centerX, centerY);
            rect.Transform(matrix);
            InitialView = rect;

            _currentView = new(0, 0, this.ActualWidth, this.ActualHeight);
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
                    GetInitialView();
                }

                if (!_bitmapLoaded)
                {
                    timer.Restart();
                    InitializeBitmapCaches(target);
                    InitializeQuadTreeCache(target);
                    _bitmapLoaded = true;
                }

                UpdateCurrentView();
                //var viewport = new RawRectangleF((float)_currentView.Left, (float)_currentView.Top,
                //    (float)_currentView.Right, (float)_currentView.Bottom);

                //target.PushAxisAlignedClip(viewport,
                //    AntialiasMode.PerPrimitive);

                timer.Restart();
                RenderBitmaps(target);


                //target.PopAxisAlignedClip();

                _isRendering = false;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            UpdateTargetAndFactory(resCache.RenderTarget, resCache.Factory);

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

                if (translate.LengthSquared < 0.5) { return; } //Prevent unneccessary translations

                UpdateTranslate(translate);
                _lastTranslatePos = PointerCoords;
                RenderTargetIsDirty = true;
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
                    RenderTargetIsDirty = true;
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
                RenderTargetIsDirty = true;
            }
        }

        private void InitializeBitmapCaches(RenderTarget target)
        {
            if (_bitmapCache is not null)
            {
                _bitmapCache.Dispose();
                _bitmapCache = null;
            }
            _bitmapCache = new(target, resCache.Factory, LayerManager, Extents, target.Size, ExtentsMatrix, resCache);
        }
        private void InitializeQuadTreeCache(RenderTarget target)
        {
            _quadTreeCache = new(target, resCache.Factory, LayerManager, Extents, ExtentsMatrix, resCache, _zoomFactor);
        }
        private void RenderBitmaps(RenderTarget target)
        {
            if (_quadTreeCache is null) { return; }

            target.Clear(new RawColor4(0, 0, 0, 0));
            //target.Transform = new((float)_transformMatrix.M11, (float)_transformMatrix.M12, (float)_transformMatrix.M21, (float)_transformMatrix.M22,
            //        (float)_transformMatrix.OffsetX, (float)_transformMatrix.OffsetY);
            RenderQuadTree(target, _quadTreeCache.CurrentQuadTrees);
        }
        private void RenderQuadTree(RenderTarget target, QuadTree quadTree)
        {
            List<QuadTreeNode> quadTreeNodes = quadTree.GetQuadTreeView(_currentView);

            for (int i = 0; i < quadTreeNodes.Count; i++)
            {
                Rect bounds = quadTreeNodes[i].DestRect;
                bounds.Transform(_transformMatrix);

                RawRectangleF destRect = new((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
                RawRectangleF sourceRect = new((float)quadTreeNodes[i].Bounds.Left, (float)quadTreeNodes[i].Bounds.Top, (float)quadTreeNodes[i].Bounds.Right, (float)quadTreeNodes[i].Bounds.Bottom);

                target.DrawBitmap(quadTree.OverallBitmap, destRect, 1.0f, BitmapInterpolationMode.NearestNeighbor, sourceRect);
            }
        }

        private void RenderInteractiveObjects(RenderTarget target)
        {
            if (_bitmapCache is not null)
            {
                target.Transform = new((float)_transformMatrix.M11, (float)_transformMatrix.M12, (float)_transformMatrix.M21, (float)_transformMatrix.M22, (float)_transformMatrix.OffsetX, (float)_transformMatrix.OffsetY);

                RawRectangleF destRect = new(0, 0, (float)ActualWidth, (float)ActualHeight);

                _bitmapCache.UpdateInteractiveObjects(SnappedObject, HighlightedObjects);

                target.DrawBitmap(_bitmapCache.InteractiveZoomBitmap.BitmapRenderTarget.Bitmap, destRect, 1.0f, BitmapInterpolationMode.Linear, _bitmapCache.InteractiveZoomBitmap.Rect);
            }
        }
        private async void RunHitTestAsync()
        {
            while (true)
            {
                await Task.Delay(100);

                if (_snapMode == SnapMode.Point)
                {
                    await Task.Run(() => HitTestPoints());
                }
                else if (_snapMode == SnapMode.Object)
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
                            RenderTargetIsDirty = true;

                            return;
                        }
                    }
                }
            }

            if (SnappedObject is null) { RenderTargetIsDirty = true; }
        }
        private void HitTestPoints()
        {

        }
        private async void RunGetVisibleObjectsAsync()
        {
            while (true)
            {
                await Task.Delay(2000);
                await Task.Run(() => GetVisibleObjects());
            }
        }
        private void GetVisibleObjects()
        {
            var coordMatrix = _overallMatrix;
            coordMatrix.Invert();

            Point topLeft = coordMatrix.Transform(new Point(0, 0));
            Point bottomRight = coordMatrix.Transform(new Point(ActualWidth, ActualHeight));
            Double width = Math.Abs(bottomRight.X - topLeft.X);
            Double height = Math.Abs(bottomRight.Y - topLeft.Y);
            Rect rect = new(topLeft.X, topLeft.Y, width, height);

            foreach (var layer in LayerManager.Layers.Values)
            {
                if (layer.IsVisible)
                {

                }
            }
        }
        private async void UpdateDxfCoordsAsync()
        {
            while (true)
            {
                await Task.Delay(75);
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

                _quadTreeCache.UpdateCurrentQuadTree((float)_transformMatrix.M11);
            }
        }
        private void UpdateTranslate(Vector translate)
        {
            _overallMatrix.Translate(translate.X, translate.Y);
            _transformMatrix.Translate(translate.X, translate.Y);
        }
        private void UpdateCurrentView()
        {
            if (resCache.RenderTarget is not null)
            {
                _currentView = new(0, 0, resCache.RenderTarget.Size.Width, resCache.RenderTarget.Size.Height);
                var matrix = _transformMatrix;
                matrix.Invert();
                _currentView.Transform(matrix);

                //var rawMatrix = resCache.RenderTarget.Transform;
                //Matrix matrix = new(rawMatrix.M11, rawMatrix.M12, rawMatrix.M21, rawMatrix.M22, rawMatrix.M31, rawMatrix.M32);
                //matrix.Invert();
                //_currentView.Transform(matrix);

                //Matrix testMatrix = new(_overallMatrix.M11, _overallMatrix.M12, _overallMatrix.M21, _overallMatrix.M22, _overallMatrix.OffsetX, _overallMatrix.OffsetY);
                //testMatrix.Invert();
                //Rect testCurrentView = new(0, 0, resCache.RenderTarget.Size.Width, resCache.RenderTarget.Size.Height);
                //testCurrentView.Transform(testMatrix);
            }
        }
        private void GetBrushes(RenderTarget target)
        {
            resCache.HighlightedBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f));

            resCache.HighlightedOuterEdgeBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f))
            { Opacity = 0.2f };
        }
        private void UpdateTargetAndFactory(RenderTarget target, Factory1 factory)
        {
            if (LayerManager is null) { return; }

            foreach (var layer in LayerManager.Layers.Values)
            {
                foreach (var o in layer.DrawingObjects)
                {
                    o.UpdateFactory(factory);
                    o.UpdateTarget(target);
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
            _bitmapCache.ZoomToExtents();
            RenderTargetIsDirty = true;
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
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _bitmapCache?.Dispose();
                    _quadTreeCache?.Dispose();
                    _bitmapCache = null;
                    _quadTreeCache = null;

                    // Dispose other managed resources if any
                }

                // Free unmanaged resources if any

                _disposed = true;
            }
        }

        ~Direct2DDxfControl()
        {
            Dispose(false);
        }
        #endregion
    }
}

