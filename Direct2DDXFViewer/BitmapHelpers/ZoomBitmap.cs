using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class ZoomBitmap : IDisposable
    {
        #region Fields
        private bool _disposed = false;
        #endregion

        #region Properties
        public BitmapRenderTarget BitmapRenderTarget { get; set; }
        public float Zoom { get; set; }
        public RawRectangleF Rect { get; set; }
        public Size2F Size { get; set; }
        public Size2F Dpi { get; set; }
        #endregion

        #region Constructors
        public ZoomBitmap() { }
        #endregion

        #region Methods
        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state (managed objects).
                BitmapRenderTarget?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.

            _disposed = true;
        }

        // Destructor
        ~ZoomBitmap()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }
        #endregion
    }

}
