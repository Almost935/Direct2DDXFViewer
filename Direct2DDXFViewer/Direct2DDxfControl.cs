﻿using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.Mathematics;
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

using Point = System.Windows.Point;
using Brush = SharpDX.Direct2D1.Brush;
using Geometry = SharpDX.Direct2D1.Geometry;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using PathGeometry = SharpDX.Direct2D1.PathGeometry;
using Direct2DDXFViewer.DrawingObjects;


namespace Direct2DDXFViewer
{
    public class Direct2DDxfControl : Direct2DControl.Direct2DControl, INotifyPropertyChanged
    {
        #region Fields
        private Matrix matrix = new();
        private bool isPanning = false;
        private Point lastTranslatePos = new();
        private float currentThickness = 0.5f;
        private bool dxfLoaded = false;
        private Rect currentView = new();
        private BackgroundWorker snapBackgroundWorker;

        private DxfDocument _dxfDoc;
        private string _filePath = @"DXF\ACAD-SP1-21 Points.dxf";
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
        #endregion

        #region Constructor
        public Direct2DDxfControl()
        {
            resCache.Add("SnappedBrush", t => new SolidColorBrush(t, new RawColor4((97 / 255), 1.0f, 0.0f, 1.0f)));
            resCache.Add("HighlightedBrush", t => new SolidColorBrush(t, new RawColor4((109 / 255), 1.0f, (float)(139 / 255), 1.0f)));
            resCache.Add("SnappedHighlightedBrush", t => new SolidColorBrush(t, new RawColor4((150 / 255), 1.0f, (float)(171 / 255), 1.0f)));

            snapBackgroundWorker = new();
            snapBackgroundWorker.DoWork += SnapBackgroundWorker_DoWork;
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void LoadDxf(Factory factory, RenderTarget target)
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
                Matrix matrix = new Matrix();
                
                double scaleX = this.ActualWidth / Extents.Width;
                double scaleY = this.ActualHeight / Extents.Height;

                double centerX = Extents.Left - (this.ActualWidth - Extents.Width) * 0.5;
                double centerY = Extents.Top - (this.ActualHeight - Extents.Height) * 0.5;
                matrix.Translate(-centerX, -centerY);

                if (scaleX < scaleY)
                {
                    matrix.ScaleAt(scaleX, -scaleX, this.ActualWidth / 2, this.ActualHeight / 2);
                    currentThickness /= (float)scaleX;
                }
                else
                {
                    matrix.ScaleAt(scaleY, -scaleY, this.ActualWidth / 2, this.ActualHeight / 2);
                    currentThickness /= (float)scaleY;
                }

                return matrix;
            }
        }

        public override void Render(RenderTarget target)
        {
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            
            if (!dxfLoaded)
            {
                LoadDxf(target.Factory, target);
                matrix = GetInitialMatrix();
            }

            target.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
           (float)matrix.OffsetX, (float)matrix.OffsetY);

            UpdateCurrentView();

            target.PushAxisAlignedClip(new RawRectangleF((float)currentView.Left, (float)currentView.Top,
                (float)currentView.Right, (float)currentView.Bottom),
                AntialiasMode.PerPrimitive);

            foreach (var layer in LayerManager.Layers.Values) 
            {
                if (layer.IsVisible)
                {
                    foreach (var o in layer.DrawingObjects)
                    {
                        if (o is DrawingLine drawingLine)
                        {
                            drawingLine.UpdateBrush(drawingLine.DxfLine, target);
                            DxfHelpers.DrawLine(drawingLine, target.Factory, target, currentThickness);
                            Render(resCache.RenderTarget);
                        }
                    }
                }
            }

            target.PopAxisAlignedClip();
            NeedsUpdate = true;
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

            NeedsUpdate = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointerCoords = e.GetPosition(this);
            UpdateDxfPointerCoords();

            if (isPanning)
            {
                var translate = PointerCoords - lastTranslatePos;

                UpdateTranslate(translate);

                lastTranslatePos = PointerCoords;

                NeedsUpdate = true;
            }

            // Update DxfPointerCoords in background thread
            if (!snapBackgroundWorker.IsBusy)
            {
                snapBackgroundWorker.RunWorkerAsync();
            }
        }

        private void SnapBackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            HitTestGeometry();
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

            if (newMatrix.HasInverse)
            {
                newMatrix.Invert();
                DxfPointerCoords = newMatrix.Transform(PointerCoords);
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

        private void UpdateZoom(float zoom)
        {
            matrix.ScaleAt(zoom, zoom, PointerCoords.X, PointerCoords.Y);
            currentThickness /= zoom;

            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
                (float)matrix.OffsetX, (float)matrix.OffsetY);
            UpdateCurrentView();
        }
        private void UpdateTranslate(Vector translate)
        {
            matrix.Translate(translate.X, translate.Y);

            resCache.RenderTarget.Transform = new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22,
            (float)matrix.OffsetX, (float)matrix.OffsetY);
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

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}

