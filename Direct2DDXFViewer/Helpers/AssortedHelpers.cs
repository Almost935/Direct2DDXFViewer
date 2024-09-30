using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.Helpers
{
    public static class AssortedHelpers
    {
        public static float GetLineWidth(RenderTarget target, int numOfPixels, float currentZoomLevel = 1)
        {
            // Get the DPI of the render target
            float dpiX = target.DotsPerInch.Width;
            float dpiY = target.DotsPerInch.Height;

            // Calculate the thickness in DIPs for the requested number of pixels wide line
            float thicknessX = numOfPixels / (dpiX / 96.0f);
            float thicknessY = numOfPixels / (dpiY / 96.0f);

            // Use the smaller thickness to ensure the line is 1 pixel wide in both dimensions
            return Math.Min(thicknessX, thicknessY) / currentZoomLevel;
        }
    }
}
