using netDxf.Entities;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class DrawingPolyline : DrawingObject
    {
        #region Fields
        #endregion

        #region Properties
        public ObservableCollection<DrawingSegment> DrawingSegments { get; set; } = new();
        #endregion

        #region Methods
        public override bool DrawingObjectIsInRect(Rect rect)
        {
            // Implement logic to determine if the polyline is within the given rectangle
            throw new NotImplementedException();
        }

        public override void UpdateGeometry()
        {
            // Implement logic to update the geometry of the polyline
            throw new NotImplementedException();
        }

        public override void DrawToDeviceContext(DeviceContext1 deviceContext, float strokeWidth, Brush brush)
        {
            // Implement logic to draw the polyline to the device context
            throw new NotImplementedException();
        }

        public override void DrawToRenderTarget(RenderTarget renderTarget, float strokeWidth, Brush brush)
        {
            // Implement logic to draw the polyline to the render target
            throw new NotImplementedException();
        }
        #endregion
    }
}
