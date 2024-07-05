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
using System.Formats.Tar;
using Direct2DDXFViewer.Helpers;

namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged
    {
        #region Fields
        private Matrix matrix = new();
        private bool isPanning = false;
        private bool isRendering = false;
        private Point lastTranslatePos = new();
        private float currentThickness;
        private bool dxfLoaded = false;
        private Rect currentView = new();
        private BackgroundWorker snapBackgroundWorker;
        private List<(Geometry, Brush)> geometries = new();
        private Bitmap bitmapCache;
        private BitmapRenderTarget bitmapRenderTarget;
        private bool bitmapLoaded = false;
        private bool bitmapRenderTargetNeedsUpdate = true;
        private Brush highlightedBrush = null;
        private Brush snappedBrush = null;
        private Brush snappedHighlightedBrush = null;

        private DxfDocument _dxfDoc;
        private string _filePath = @"DXF\MediumDxf.dxf";
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
            snapBackgroundWorker = new();
            snapBackgroundWorker.DoWork += SnapBackgroundWorker_DoWork;
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
                dxfLoaded = true;
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
                    //currentThickness /= (float)scaleX;
                }
                else
                {
                    matrix.ScaleAt(scaleY, -scaleY, this.ActualWidth / 2, this.ActualHeight / 2);
                    //currentThickness /= (float)scaleY;
                }

                return matrix;
            }
        }

        public override void Render(RenderTarget target)
        {
            if (!isRendering)
            {
                isRendering = true;

                DeviceContext1 deviceContext = target.QueryInterface<DeviceContext1>();
                deviceContext.AntialiasMode = AntialiasMode.Aliased;
                deviceContext.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

                target.AntialiasMode = AntialiasMode.Aliased;
                target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));

                if (!dxfLoaded)
                {
                    LoadDxf(resCache.Factory, target);
                    ExtentsMatrix = GetInitialMatrix();
                    matrix = ExtentsMatrix;
                }
                // target.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                //(float)matrix.OffsetX, (float)matrix.OffsetY);
                deviceContext.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                (float)matrix.OffsetX, (float)matrix.OffsetY);

                GetBrushes(target);
                currentThickness = AssortedHelpers.GetLineThickness(1.0f, target, (float)matrix.M11);

                UpdateCurrentView();
                var viewport = new RawRectangleF((float)currentView.Left, (float)currentView.Top,
                    (float)currentView.Right, (float)currentView.Bottom);

                //target.PushAxisAlignedClip(viewport,
                //    AntialiasMode.PerPrimitive);
                deviceContext.PushAxisAlignedClip(viewport,
                   AntialiasMode.PerPrimitive);

                if (!bitmapLoaded)
                {
                    LoadBitmap(target);
                }

                Stopwatch timer = new();
                timer.Start();
                foreach (var layer in LayerManager.Layers.Values)
                {
                    if (layer.IsVisible)
                    {
                        foreach (var o in layer.DrawingObjects)
                        {
                            if (o is DrawingLine drawingLine)
                            {
                                if (AssortedHelpers.IsGeometryInRect(viewport, drawingLine.Geometry, currentThickness))
                                {
                                    deviceContext.DrawGeometryRealization(drawingLine.GeometryRealization, GetDrawingObjectBrush(drawingLine));
                                    //DxfHelpers.DrawLine(drawingLine, resCache.Factory, target, currentThickness, GetDrawingObjectBrush(drawingLine));
                                }
                            }
                            //if (o is DrawingPolyline2D drawingPolyline2D)
                            //{
                            //    if (AssortedHelpers.IsGeometryInRect(viewport, drawingPolyline2D.Geometry, currentThickness))
                            //    {
                            //        DxfHelpers.DrawPolyline(drawingPolyline2D, target.Factory, target, currentThickness, GetDrawingObjectBrush(drawingPolyline2D));
                            //    }
                            //}
                            //if (o is DrawingPolyline3D drawingPolyline3D)
                            //{
                            //    if (AssortedHelpers.IsGeometryInRect(viewport, drawingPolyline3D.Geometry, currentThickness))
                            //    {
                            //        DxfHelpers.DrawPolyline(drawingPolyline3D, target.Factory, target, currentThickness, GetDrawingObjectBrush(drawingPolyline3D));
                            //    }
                            //}
                        }
                    }
                }
                //target.PopAxisAlignedClip();
                deviceContext.PopAxisAlignedClip();

                Debug.WriteLine($"Render time: {timer.ElapsedMilliseconds} ms");

                isRendering = false;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            bitmapRenderTargetNeedsUpdate = true;
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float zoom;

            if (e.Delta > 0)
            {
                zoom = 1.3f;
            }
            else
            {
                zoom = 1f / 1.3f;
            }

            UpdateZoom(zoom);

            RenderTargetIsDirty = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointerCoords = e.GetPosition(this);

            if (isPanning)
            {
                var translate = PointerCoords - lastTranslatePos;
                UpdateTranslate(translate);
                lastTranslatePos = PointerCoords;
                RenderTargetIsDirty = true;
            }
            //Update DxfPointerCoords in background thread
            if (!snapBackgroundWorker.IsBusy)
            {
                snapBackgroundWorker.RunWorkerAsync();
            }
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                isPanning = true;
                lastTranslatePos = e.GetPosition(this);
            }
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                isPanning = false;
                RenderTargetIsDirty = true;
            }
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            isPanning = false;
        }
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (Mouse.MiddleButton == MouseButtonState.Pressed)
            {
                isPanning = true;
            }
        }

        private void LoadBitmap(RenderTarget target)
        {
            if (LayerManager is null)
            {
                return;
            }
            if (bitmapRenderTarget is not null)
            {
                bitmapRenderTarget.Dispose();
                bitmapRenderTarget = null;
            }

            bitmapRenderTarget = new(target, CompatibleRenderTargetOptions.None, target.Size);
            bitmapRenderTarget.BeginDraw();
            bitmapRenderTarget.Clear(new RawColor4(1.0f, 1.0f, 0f, 1.0f));
            //bitmapRenderTarget.Transform = new(1, 0, 0, 1, -(float)Extents.Left, -(float)Extents.Top);

            foreach (var layer in LayerManager.Layers.Values)
            {
                if (layer.IsVisible)
                {
                    foreach (var o in layer.DrawingObjects)
                    {
                        if (o is DrawingLine drawingLine)
                        {
                            drawingLine.UpdateBrush(drawingLine.DxfLine, bitmapRenderTarget);
                            DxfHelpers.DrawLine(drawingLine, resCache.Factory, bitmapRenderTarget, currentThickness, GetDrawingObjectBrush(drawingLine));
                        }
                    }
                }
            }

            bitmapRenderTarget.EndDraw();
            bitmapRenderTargetNeedsUpdate = false;
            bitmapLoaded = true;
        }
        private void RenderBitmap(RenderTarget target)
        {
            if (bitmapRenderTarget is not null)
            {
                if (bitmapRenderTargetNeedsUpdate)
                {
                    LoadBitmap(target);
                }

                RawRectangleF sourceRect = new((float)Extents.Left, (float)Extents.Top, (float)(Extents.Left + target.Size.Width), (float)(Extents.Top + target.Size.Height));
                RawRectangleF testSourceRect = new(0, 0, 100, 100);

                RawRectangleF destinationRect = new((float)Extents.Left, (float)Extents.Top, (float)(Extents.Left + target.Size.Width), (float)(Extents.Top + target.Size.Height));
                RawRectangleF testDestinationRect = new(0, 0, 50, 50);

                //renderTarget.DrawBitmap(bitmap, 1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear, new SharpDX.RectangleF(offsetX, offsetY, offsetX + bitmap.Size.Width, offsetY + bitmap.Size.Height));

                target.DrawBitmap(bitmapRenderTarget.Bitmap, testDestinationRect, 1.0f, BitmapInterpolationMode.Linear, testSourceRect);
            }
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
            var newMatrix = matrix;
            newMatrix.Invert();
            DxfPointerCoords = newMatrix.Transform(PointerCoords);
        }
        private void UpdateZoom(float zoom)
        {
            if (!isPanning)
            {
                matrix.ScaleAt(zoom, zoom, PointerCoords.X, PointerCoords.Y);
                //currentThickness /= zoom;

                resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                    (float)matrix.OffsetX, (float)matrix.OffsetY);
                //bitmapRenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                //   (float)matrix.OffsetX, (float)matrix.OffsetY);

                UpdateCurrentView();
            }
        }
        private void UpdateTranslate(Vector translate)
        {
            matrix.Translate(translate.X, translate.Y);

            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
            (float)(matrix.OffsetX), (float)(matrix.OffsetY));
            //bitmapRenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
            //(float)(matrix.OffsetX), (float)(matrix.OffsetY));

            UpdateCurrentView();
        }
        private void UpdateCurrentView()
        {
            if (resCache.RenderTarget is not null)
            {
                currentView = new(0, 0, resCache.RenderTarget.Size.Width, resCache.RenderTarget.Size.Height);
                var rawMatrix = resCache.RenderTarget.Transform;
                Matrix matrix = new(rawMatrix.M11, rawMatrix.M12, rawMatrix.M21, rawMatrix.M22, rawMatrix.M31, rawMatrix.M32);
                matrix.Invert();
                currentView.Transform(matrix);
            }
        }
        private void GetBrushes(RenderTarget target)
        {
            highlightedBrush ??= new SolidColorBrush(target, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f));
            snappedBrush ??= new SolidColorBrush(target, new RawColor4((109 / 255), 1.0f, (float)(139 / 255), 1.0f));
            snappedHighlightedBrush ??= new SolidColorBrush(target, new RawColor4((150 / 255), 1.0f, (float)(171 / 255), 1.0f));
        }
        private Brush GetDrawingObjectBrush(DrawingObject drawingObject)
        {
            if (drawingObject.IsHighlighted && drawingObject.IsSnapped)
            {
                return snappedHighlightedBrush;
            }
            else if (drawingObject.IsHighlighted)
            {
                return highlightedBrush;
            }
            else if (drawingObject.IsSnapped)
            {
                return snappedBrush;
            }
            else
            {
                return drawingObject.Brush;
            }
        }

        public void ZoomToExtents()
        {
            matrix = ExtentsMatrix;
            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                (float)matrix.OffsetX, (float)matrix.OffsetY);
            UpdateCurrentView();
            RenderTargetIsDirty = true;
        }
        

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}

