using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Shapes;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace PLC_SQL_Control
{
    /// <summary>
    /// Interaction logic for ExportCSV.xaml
    /// </summary>
    public partial class ExportCSV : FluentWindow
    {
        public string? lotNumber = string.Empty;
        public string path = string.Empty;
        public int dPF = 2000;
        public bool isExport = true;

        public List<string> LotNumbers = new List<string>();

        public ExportCSV()
        {
            InitializeComponent();
            cb_LN.ItemsSource = LotNumbers;
        }

        private void browse_Folder(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Multiselect = false;
            ofd.InitialDirectory = Setting.Default.I_Directory == String.Empty ?
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Setting.Default.I_Directory;

            if (ofd.ShowDialog() == true)
            {
                Setting.Default.I_Directory = ofd.InitialDirectory;
                Setting.Default.Save();
                path = ofd.FolderName;
            }
            else return;
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            isExport = false;
            Close();
        }

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            isExport = true;
            lotNumber = cb_LN.SelectedItem.ToString();
            dPF = int.Parse(tb_DataPerFile.Text);
            if (lotNumber == null || lotNumber == string.Empty)
            {
                MessageBox.Show("Please select a lot number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Close();
        }
    }
}
