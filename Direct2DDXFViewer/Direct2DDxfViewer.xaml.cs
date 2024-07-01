using netDxf;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Direct2DDXFViewer
{
    /// <summary>
    /// Interaction logic for Direct2DDxfViewer.xaml
    /// </summary>
    public partial class Direct2DDxfViewer : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private Point dxfPointerCoords = new();
        #endregion

        #region Properties
        public Point DxfPointerCoords
        {
            get { return dxfPointerCoords; }
            set
            {
                dxfPointerCoords = value;
                OnPropertyChanged(nameof(DxfPointerCoords));
            }
        }
        #endregion

        #region Constructor
        public Direct2DDxfViewer()
        {
            InitializeComponent();

            dxfControl.PropertyChanged += DxfControl_PropertyChanged; ;
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

        private void DxfControl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(dxfControl.PointerCoords))
            {
                DxfPointerCoords = dxfControl.DxfPointerCoords;
            }
        }

        public void ZoomToExtents()
        {
            dxfControl.ZoomToExtents();
        }
        #endregion
    }
}
