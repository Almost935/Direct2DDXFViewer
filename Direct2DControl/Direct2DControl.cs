using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using DeviceContext1 = SharpDX.Direct2D1.DeviceContext1;
using FeatureLevel = SharpDX.Direct3D.FeatureLevel;

namespace Direct2DControl
{
    public abstract class Direct2DControl : System.Windows.Controls.Image
    {
        // - field -----------------------------------------------------------------------

        private SharpDX.Direct3D11.Device device;
        private Texture2D renderTarget;
        private Dx11ImageSource d3DSurface;
        private RenderTarget d2DRenderTarget;
        private DeviceContext1 d2DDeviceContext;
        private SharpDX.Direct2D1.Factory1 d2DFactory;
        private SharpDX.DirectWrite.Factory dWriteFactory;

        private readonly Stopwatch renderTimer = new();

        protected ResourceCache resCache = new();

        private long lastFrameTime = 0;
        private long lastRenderTime = 0;
        private int frameCount = 0;
        private int frameCountHistTotal = 0;
        private Queue<int> frameCountHist = new();

        // - property --------------------------------------------------------------------

        /// <summary>
        /// Decides whether or not the image needs to be refreshed.
        /// </summary>

        public static bool IsInDesignMode
        {
            get
            {
                var prop = DesignerProperties.IsInDesignModeProperty;
                var isDesignMode = (bool)DependencyPropertyDescriptor.FromProperty(prop, typeof(FrameworkElement)).Metadata.DefaultValue;
                return isDesignMode;
            }
        }
        public bool IsRendering = false;

        private static readonly DependencyPropertyKey FpsPropertyKey = DependencyProperty.RegisterReadOnly(
            "Fps",
            typeof(int),
            typeof(Direct2DControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.None)
            );

        public static readonly DependencyProperty FpsProperty = FpsPropertyKey.DependencyProperty;

        public int Fps
        {
            get { return (int)GetValue(FpsProperty); }
            protected set { SetValue(FpsPropertyKey, value); }
        }

        public static DependencyProperty RenderWaitProperty = DependencyProperty.Register(
            "RenderWait",
            typeof(int),
            typeof(Direct2DControl),
            new FrameworkPropertyMetadata(2, OnRenderWaitChanged)
            );

        public int RenderWait
        {
            get { return (int)GetValue(RenderWaitProperty); }
            set { SetValue(RenderWaitProperty, value); }
        }

        // - public methods --------------------------------------------------------------

        public Direct2DControl()
        {
            base.Loaded += Window_Loaded;
            base.Unloaded += Window_Closing;

            base.Stretch = System.Windows.Media.Stretch.Fill;
        }

        public abstract void Render(RenderTarget target, DeviceContext1 deviceContext);

        // - event handler ---------------------------------------------------------------

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Direct2DControl.IsInDesignMode)
            {
                return;
            }

            StartD3D();
            StartRendering();
        }

        private void Window_Closing(object sender, RoutedEventArgs e)
        {
            if (Direct2DControl.IsInDesignMode)
            {
                return;
            }

            StopRendering();
            EndD3D();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!renderTimer.IsRunning)
            {
                return;
            }
            PrepareAndCallRender();
            d3DSurface.InvalidateD3DImage();
            lastRenderTime = renderTimer.ElapsedMilliseconds;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            StopRendering();

            CreateAndBindTargets();
            base.OnRenderSizeChanged(sizeInfo);

            StartRendering();
        }

        private void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (d3DSurface.IsFrontBufferAvailable)
            {
                StartRendering();
            }
            else
            {
                StopRendering();
            }
        }

        private static void OnRenderWaitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (Direct2DControl)d;
            control.d3DSurface.RenderWait = (int)e.NewValue;
        }

        // - private methods -------------------------------------------------------------

        private void StartD3D()
        {
            device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            resCache.Device = device;
            d3DSurface = new Dx11ImageSource();
            d3DSurface.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;

            CreateAndBindTargets();

            base.Source = d3DSurface;
        }

        private void EndD3D()
        {
            d3DSurface.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
            base.Source = null;

            Disposer.SafeDispose(ref d2DRenderTarget);
            Disposer.SafeDispose(ref d2DFactory);
            Disposer.SafeDispose(ref d3DSurface);
            Disposer.SafeDispose(ref renderTarget);
            Disposer.SafeDispose(ref device);
        }

        private void CreateAndBindTargets()
        {
            if (d3DSurface == null)
            {
                return;
            }

            d3DSurface.SetRenderTarget(null);

            Disposer.SafeDispose(ref d2DRenderTarget);
            Disposer.SafeDispose(ref d2DDeviceContext);
            Disposer.SafeDispose(ref d2DFactory);
            Disposer.SafeDispose(ref renderTarget);

            var width = Math.Max((int)ActualWidth, 100);
            var height = Math.Max((int)ActualHeight, 100);

            var renderDesc = new Texture2DDescription
            {
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                OptionFlags = ResourceOptionFlags.Shared,
                CpuAccessFlags = CpuAccessFlags.None,
                ArraySize = 1
            };

            renderTarget = new Texture2D(device, renderDesc);
            resCache.MaxBitmapSize = GetMaxSize(renderTarget.Device.FeatureLevel);
            var surface = renderTarget.QueryInterface<Surface>();

            if (d2DFactory is null)
            {
                d2DFactory = new SharpDX.Direct2D1.Factory1(FactoryType.MultiThreaded, DebugLevel.Information);
                resCache.Factory = d2DFactory;
            }
            if (resCache.FactoryWrite is null)
            {
                var factory = new SharpDX.DirectWrite.Factory1(SharpDX.DirectWrite.FactoryType.Shared);
                resCache.FactoryWrite = factory;
            }
            
            var rtp = new RenderTargetProperties(new PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied));
            d2DRenderTarget = new(d2DFactory, surface, rtp);
            resCache.RenderTarget = d2DRenderTarget;
            d2DDeviceContext = d2DRenderTarget.QueryInterface<DeviceContext1>();
            resCache.DeviceContext = d2DDeviceContext;
            
            d3DSurface.SetRenderTarget(renderTarget);

            device.ImmediateContext.Rasterizer.SetViewport(0, 0, width, height, 0.0f, 1.0f);
        }

        private void StartRendering()
        {
            if (renderTimer.IsRunning)
            {
                return;
            }

            IsRendering = true;
            System.Windows.Media.CompositionTarget.Rendering += OnRendering;
            renderTimer.Start();
        }

        private void StopRendering()
        {
            if (!renderTimer.IsRunning)
            {
                return;
            }

            IsRendering = false;
            System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
            renderTimer.Stop();
        }

        private void PrepareAndCallRender()
        {
            if (device == null)
            {
                return;
            }

            //d2DRenderTarget.BeginDraw();
            Render(d2DRenderTarget, d2DDeviceContext);
            //d2DRenderTarget.EndDraw();

            //CalcFps();

            //device.ImmediateContext.Flush();
        }

        private void CalcFps()
        {
            frameCount++;
            if (renderTimer.ElapsedMilliseconds - lastFrameTime > 1000)
            {
                frameCountHist.Enqueue(frameCount);
                frameCountHistTotal += frameCount;
                if (frameCountHist.Count > 5)
                {
                    frameCountHistTotal -= frameCountHist.Dequeue();
                }

                Fps = frameCountHistTotal / frameCountHist.Count;

                frameCount = 0;
                lastFrameTime = renderTimer.ElapsedMilliseconds;
            }
        }

        private static int GetMaxSize(FeatureLevel featureLevel)
        {
            switch (featureLevel)
            {
                case FeatureLevel.Level_10_0:
                case FeatureLevel.Level_10_1:
                    return 8192;
                case FeatureLevel.Level_11_0:
                case FeatureLevel.Level_11_1:
                case FeatureLevel.Level_12_0:
                case FeatureLevel.Level_12_1:
                    return 16384;
                default:
                    throw new NotSupportedException("Unsupported feature level");
            }

        }
    }
}
