using netDxf.Entities;
using netDxf.Units;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingLine : DrawingObject
    {
        #region Fields
        private Line _dxfLine;
        private RawVector2 _startPoint;
        private RawVector2 _endPoint;
        #endregion

        #region Properties
        public Line DxfLine
        {
            get { return _dxfLine; }
            set
            {
                _dxfLine = value;
                OnPropertyChanged(nameof(DxfLine));
            }
        }
        public RawVector2 StartPoint
        {
            get { return _startPoint; }
            set
            {
                _startPoint = value;
                OnPropertyChanged(nameof(StartPoint));
            }
        }
        public RawVector2 EndPoint
        {
            get { return _endPoint; }
            set
            {
                _endPoint = value;
                OnPropertyChanged(nameof(EndPoint));
            }
        }

        public float CurrentScale { get; set; } = 1;
        #endregion

        #region Constructor
        public DrawingLine(Line dxfLine, Factory1 factory, RenderTarget renderTarget)
        {
            DxfLine = dxfLine;
            Entity = dxfLine;
            Factory = factory; 
            Target = renderTarget;
            StartPoint = new((float)dxfLine.StartPoint.X, (float)dxfLine.StartPoint.Y); 
            EndPoint = new((float)dxfLine.EndPoint.X, (float)dxfLine.EndPoint.Y);

            GetStrokeStyle();
            UpdateBrush();
        }
        #endregion

        #region Methods
        public override void Draw(RenderTarget target, float thickness, Brush brush)
        {
            target.DrawLine(StartPoint, EndPoint, brush, thickness, StrokeStyle);
        }
        public override bool DrawingObjectIsInView(Rect rect)
        {
            return true;
        }
        public override void UpdateGeometry()
        {
            PathGeometry pathGeometry = new(Factory);
            using (var sink = pathGeometry.Open())
            {
                sink.BeginFigure(new RawVector2((float)DxfLine.StartPoint.X, (float)DxfLine.StartPoint.Y), FigureBegin.Filled);
                sink.AddLine(new RawVector2((float)DxfLine.EndPoint.X, (float)DxfLine.EndPoint.Y));
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
                
                Geometry = pathGeometry;
            }
        }
        #endregion
    }
}
