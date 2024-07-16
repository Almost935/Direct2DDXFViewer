using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.DrawingObjects
{
    public class ObjectLayer : INotifyPropertyChanged
    {
        #region Fields
        private string _name;
        private List<DrawingObject> _drawingObjects = new();
        private bool isVisible = true;
        #endregion

        #region Properties
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public List<DrawingObject> DrawingObjects
        {
            get { return _drawingObjects; }
            set
            {
                _drawingObjects = value;
                OnPropertyChanged(nameof(DrawingObjects));
            }
        }
        public bool IsVisible
        {
            get { return isVisible; }
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Draw(RenderTarget renderTarget, float thickness)
        {
            if (!IsVisible) { return; }

            foreach (var drawingObject in DrawingObjects)
            {
                drawingObject.Draw(renderTarget, thickness, drawingObject.Brush);
            }
        }
        #endregion
    }
}
