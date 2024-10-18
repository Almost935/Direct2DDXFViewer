using netDxf;
using System.Text;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string dxfFilePath = @"C:\Users\Tim\source\repos\Direct2DDXFViewer\Direct2DDXFViewer\bin\Debug\net8.0-windows\DXF\SmallDxf.dxf";
        private string dxfDocument;

        public MainWindow()
        {
            InitializeComponent();

            DxfDocument dxfDoc = DxfDocument.Load(dxfFilePath);
            if (dxfDoc is not null)
            {
                dxfViewer.LoadDxfControl(dxfDoc);
            }
        }

        private void mainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set window state maximum here because otherwise the D3DImage doesn't show 
            mainWindow.WindowState = WindowState.Maximized;
        }

        private void ZoomToExtents_Click(object sender, RoutedEventArgs e)
        {
            if (dxfViewer is not null)
            {
                dxfViewer.ZoomToExtents();
            }
        }
    }
}