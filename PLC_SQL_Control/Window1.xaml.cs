using System;
using System.Collections.Generic;
using System.Data;
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
using Microsoft.Data.SqlClient;
using Wpf.Ui.Controls;

namespace PLC_SQL_Control
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : FluentWindow
    {
        public string connectionstring = string.Empty;
        public string tablename = string.Empty;
        private string sql_query = string.Empty;

        public Window1()
        {
            InitializeComponent();
        }

        private async void ToggleSwitch_RealTime_Click(object sender, RoutedEventArgs e)
        {
            while (RealTime_Switch.IsChecked==true)
            {
                Top10Generate();
                await Task.Delay(1000);
            }
        }

        public void Top10Generate()
        {
            sql_query = 
                $"SELECT * INTO #TEMPTABLE FROM " +
                $"(SELECT TOP 10 * FROM [{tablename}] ORDER  BY ID DESC)a " +
                $"ALTER TABLE #TEMPTABLE DROP COLUMN ID " +
                $"SELECT * FROM #TEMPTABLE " +
                $"DROP TABLE #TEMPTABLE";

            DataTable dt = new DataTable();
            using (SqlConnection cnn = new SqlConnection(connectionstring))
            {
                using (SqlCommand cmd = new SqlCommand(sql_query))
                {
                    cmd.Connection = cnn;
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            SQL_DG.ItemsSource = dt.DefaultView;

            tb_LastUpdate.Text = "Last Update: " + DateTime.Now.ToString();
        }

        private void TittleBar_modification_CloseClicked(TitleBar sender, RoutedEventArgs args)
        {
            this.Close();
        }
    }
}
