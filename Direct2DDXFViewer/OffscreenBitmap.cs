using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer
{
    public class OffscreenBitmap : IDisposable
    {
        #region Properties
        public int ZoomStep { get; set; }
        public Bitmap Bitmap { get; set; }
        #endregion

        #region Constructors
        public OffscreenBitmap(int zoomStep, Bitmap bitmap)
        {
            ZoomStep = zoomStep;
            Bitmap = bitmap;
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Bitmap?.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
