using System;
using System.IO;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.WIC;

using Bitmap = SharpDX.Direct2D1.Bitmap;

namespace Direct2DDXFViewer.BitmapHelpers
{
    public class DxfBitmap
    {
        #region Fields
        private DeviceContext1 _deviceContext;
        private string _filepath;
        #endregion

        #region Properties
        public float Zoom { get; set; }
        public Bitmap Bitmap { get; set; }
        public bool BitmapLoaded => Bitmap != null;
        public bool BitmapSaved = false;
        #endregion

        #region Constructor
        public DxfBitmap(DeviceContext1 deviceContext, float zoom, Bitmap bitmap)
        {
            _deviceContext = deviceContext;
            Zoom = zoom;
            Bitmap = bitmap;
        }
        #endregion

        #region Methods
        public void LoadBitmapFromFile(string filePath)
        {
            if (BitmapSaved)
            {
                ImagingFactory imagingFactory = new();
                // Load the image using WIC
                using (var decoder = new BitmapDecoder(imagingFactory, filePath, DecodeOptions.CacheOnLoad))
                using (var frame = decoder.GetFrame(0))
                using (var converter = new FormatConverter(imagingFactory))
                {
                    // Convert the image format to 32bppPBGRA which is compatible with Direct2D
                    converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppPBGRA);

                    // Create a Direct2D Bitmap from the WIC Bitmap
                    Bitmap = Bitmap.FromWicBitmap(_deviceContext, converter);
                }
                imagingFactory.Dispose();
            }
        }


        public void SaveBitmapToTemporaryFile()
        {
            if (!BitmapSaved)
            {
                ImagingFactory imagingFactory = new();
                // Create a WIC Bitmap
                var wicBitmap = new SharpDX.WIC.Bitmap(imagingFactory, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height,
                                            SharpDX.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);

                // Create a RenderTarget for the WIC Bitmap
                using (var renderTarget = new WicRenderTarget(Bitmap.Factory, wicBitmap, new RenderTargetProperties()))
                {
                    // Copy the Direct2D Bitmap to the WIC Bitmap
                    renderTarget.BeginDraw();
                    renderTarget.DrawBitmap(Bitmap, 1.0f, SharpDX.Direct2D1.BitmapInterpolationMode.NearestNeighbor);
                    renderTarget.EndDraw();
                }

                _filepath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

                // Select encoder based on file extension
                BitmapEncoder encoder;
                using (var stream = new WICStream(imagingFactory, _filepath, SharpDX.IO.NativeFileAccess.Write))
                {
                    encoder = new PngBitmapEncoder(imagingFactory, stream);
                    var frame = new BitmapFrameEncode(encoder);
                    frame.Initialize();
                    frame.SetSize(wicBitmap.Size.Width, wicBitmap.Size.Height);
                    var guid = SharpDX.WIC.PixelFormat.Format32bppPBGRA;
                    frame.SetPixelFormat(ref guid);
                    frame.WriteSource(wicBitmap);
                    frame.Commit();
                    encoder.Commit();
                }
                wicBitmap.Dispose();
                imagingFactory.Dispose();

                BitmapSaved = true;
            }
        }
        #endregion
    }
}
