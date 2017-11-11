using System;
using System.Collections.Generic;
using System.Linq;
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


namespace AIS_AddIn
{
    /// <summary>
    /// Interaction logic for CustomControl1View.xaml
    /// </summary>
    public partial class CustomControl1View : UserControl
    {
        public CustomControl1View()
        {
            InitializeComponent();
            // Set data context of XAML to the corresponding view model
            this.DataContext = new CustomControl1ViewModel();
        }
    }
}
