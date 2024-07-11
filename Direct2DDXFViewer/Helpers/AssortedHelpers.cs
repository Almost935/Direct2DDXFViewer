using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;

using FeatureLevel = SharpDX.Direct3D.FeatureLevel;

namespace Direct2DDXFViewer.Helpers
{
    public static class AssortedHelpers
    {
        public static bool IsGeometryInRect(RawRectangleF viewport, Geometry geometry, float strokeThickness)
        {
            // Attempt to get the bounds of the geometry
            var bounds = geometry.GetWidenedBounds(strokeThickness);

            // Check if the bounds intersect with the viewport
            return bounds.Left < viewport.Right &&
                   bounds.Right > viewport.Left &&
                   bounds.Top < viewport.Bottom &&
                   bounds.Bottom > viewport.Top;
        }
        public static float GetLineThickness(float pixelWidth, RenderTarget renderTarget, float zoom)
        {             
            // Get the DPI of the render target
            var dpi = renderTarget.DotsPerInch.Width;

            // Calculate the thickness of the line
            return (pixelWidth * dpi) / (96 * zoom);
        }
    }
}
