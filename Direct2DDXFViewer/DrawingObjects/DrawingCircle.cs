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

using Ellipse = SharpDX.Direct2D1.Ellipse;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingCircle : DrawingObject
    {
        #region Fields
        private Circle _dxfCircle;
        #endregion

        #region Properties
        public Circle DxfCircle
        {
            get { return _dxfCircle; }
            set
            {
                _dxfCircle = value;
                OnPropertyChanged(nameof(DxfCircle));
            }
        }

        RenderTarget Target { get; set; }
        #endregion

        #region Constructor
        public DrawingCircle(Circle dxfCircle, Factory factory, RenderTarget renderTarget)
        {
            DxfCircle = dxfCircle;
            Factory = factory; 

            UpdateBrush(dxfCircle, renderTarget);
        }
        #endregion

        #region Methods
        public override void UpdateGeometry()
        {
            Ellipse ellipse = new(new RawVector2((float)DxfCircle.Center.X, (float)DxfCircle.Center.Y), (float)DxfCircle.Radius, (float)DxfCircle.Radius);
            EllipseGeometry ellipseGeometry = new(Factory, ellipse);

            Geometry = ellipseGeometry;
        }
        #endregion
    }
}
