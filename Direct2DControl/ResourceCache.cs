using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DControl
{
    public class ResourceCache : IDisposable
    {
        // - field -----------------------------------------------------------------------
        private Dictionary<string, Func<RenderTarget, object>> generators = new Dictionary<string, Func<RenderTarget, object>>();
        private Dictionary<string, object> resources = new Dictionary<string, object>();

        private SharpDX.Direct3D11.Device device = null;
        private RenderTarget renderTarget = null;
        private DeviceContext1 deviceContext = null;
        private bool _disposed = false;
        // - property --------------------------------------------------------------------
        public SharpDX.Direct3D11.Device Device
        {
            get { return device; }
            set { device = value; UpdateResources(); }
        }
        public RenderTarget RenderTarget
        {
            get { return renderTarget; }
            set { renderTarget = value; UpdateResources(); }
        }
        public DeviceContext1 DeviceContext
        {
            get { return deviceContext; }
            set { deviceContext = value; UpdateResources(); }
        }

        public int Count
        {
            get { return resources.Count; }
        }

        public Factory1 Factory { get; set; }
        public int MaxBitmapSize { get; set; }
        public Brush HighlightedBrush { get; set; }
        public Brush HighlightedOuterEdgeBrush { get; set; }
        public Dictionary<(byte r, byte g, byte b, byte a), Brush> Brushes { get; set; } = new();

        public enum LineType { Solid, Dash };
        public Dictionary<LineType, StrokeStyle1> StrokeStyles { get; set; } = new();

        public object this[string key]
        {
            get { return resources[key]; }
        }

        public Dictionary<string, object>.KeyCollection Keys
        {
            get { return resources.Keys; }
        }

        public Dictionary<string, object>.ValueCollection Values
        {
            get { return resources.Values; }
        }

        // - public methods --------------------------------------------------------------

        public void Add(string key, Func<RenderTarget, object> gen)
        {
            object resOld;
            if (resources.TryGetValue(key, out resOld))
            {
                Disposer.SafeDispose(ref resOld);
                generators.Remove(key);
                resources.Remove(key);
            }

            if (renderTarget == null)
            {
                generators.Add(key, gen);
                resources.Add(key, null);
            }
            else
            {
                var res = gen(renderTarget);
                generators.Add(key, gen);
                resources.Add(key, res);
            }
        }

        public void Clear()
        {
            foreach (var key in resources.Keys)
            {
                var res = resources[key];
                Disposer.SafeDispose(ref res);
            }
            generators.Clear();
            resources.Clear();
        }

        public bool ContainsKey(string key)
        {
            return resources.ContainsKey(key);
        }

        public bool ContainsValue(object val)
        {
            return resources.ContainsValue(val);
        }

        public Dictionary<string, object>.Enumerator GetEnumerator()
        {
            return resources.GetEnumerator();
        }

        public bool Remove(string key)
        {
            object res;
            if (resources.TryGetValue(key, out res))
            {
                Disposer.SafeDispose(ref res);
                generators.Remove(key);
                resources.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetValue(string key, out object res)
        {
            return resources.TryGetValue(key, out res);
        }

        // - private methods -------------------------------------------------------------

        private void UpdateResources()
        {
            if (renderTarget == null) { return; }

            foreach (var g in generators)
            {
                var key = g.Key;
                var gen = g.Value;
                var res = gen(renderTarget);

                object resOld;
                if (resources.TryGetValue(key, out resOld))
                {
                    Disposer.SafeDispose(ref resOld);
                    resources.Remove(key);
                }

                resources.Add(key, res);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    Clear();
                    Disposer.SafeDispose(ref renderTarget);
                    Disposer.SafeDispose(ref deviceContext);
                    HighlightedBrush.Dispose();
                    HighlightedOuterEdgeBrush.Dispose();
                }

                // Free unmanaged resources (if any)

                _disposed = true;
            }
        }

        ~ResourceCache()
        {
            Dispose(false);
        }
    }
}
