using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingLine : DrawingObject
    {
        #region Fields
        private Line _dxfLine;
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
        #endregion

        #region Constructor
        public DrawingLine(Line dxfLine, Factory factory)
        {
            DxfLine = dxfLine;
            Factory = factory;
        }
        #endregion

        #region Methods
        public override void UpdateGeometry()
        {
            using (PathGeometry pathGeometry = new(Factory))
            {
                using (var sink = pathGeometry.Open())
                {
                    sink.BeginFigure(new RawVector2((float)DxfLine.StartPoint.X, (float)DxfLine.StartPoint.Y), FigureBegin.Filled);
                    sink.AddLine(new RawVector2((float)DxfLine.EndPoint.X, (float)DxfLine.EndPoint.Y));
                    sink.EndFigure(FigureEnd.Open);
                    sink.Close();

                    Geometry = pathGeometry;
                }
            }
        }
        #endregion
    }
}
