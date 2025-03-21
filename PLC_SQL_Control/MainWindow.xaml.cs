using System.Text;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using libplctag;
using libplctag.DataTypes.Simple;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using System.Data;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using TextBox = Wpf.Ui.Controls.TextBox;
using System.IO;
using libplctag.NativeImport;



namespace PLC_SQL_Control;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    #region declaring Global Variables
    private static readonly Regex _regex = new Regex("[^0-9.]"); //regex that matches disallowed text
    private static bool IsTextAllowed(string text)=>!_regex.IsMatch(text);
    private void IPOnly(object sender, TextCompositionEventArgs e) => e.Handled = !IsTextAllowed(e.Text);
    private string connectionstring = string.Empty;
    private DataTable PDT = new DataTable();
    private DataTable NDT = new DataTable();
    private string PLC_IP = string.Empty;
    private Dictionary<string, object> PLC_Tag = new Dictionary<string, object>();
    private Dictionary<string, TextBox> Filter1 = new Dictionary<string, TextBox>();
    private Dictionary<string, ToggleSwitch> Filter2 = new Dictionary<string, ToggleSwitch>();

    #endregion

    public MainWindow()
    {
        InitializeComponent();
        Setting_Initialize();
        PLCIP_tb.PreviewTextInput += new TextCompositionEventHandler(IPOnly);
        Empty_Table();

    }

    #region Other Initializations
    private void Setting_Initialize()
    {
        if (Setting.Default.Theme)
        {
            Theme_Switch.IsChecked = true;
            Theme_Switch.Content = "Light Mode";
            ApplicationThemeManager.Apply(ApplicationTheme.Light);
        }
        else
        {
            Theme_Switch.IsChecked = false;
            Theme_Switch.Content = "Dark Mode";
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }
    }

    private void Theme_Switch_Checked(object sender, RoutedEventArgs e)
    {
        Theme_Switch.Content = "Light Mode";
        Setting.Default.Theme = true;
        Setting.Default.Save();
        ApplicationThemeManager.Apply(ApplicationTheme.Light);
    }

    private void Theme_Switch_Unchecked(object sender, RoutedEventArgs e)
    {
        Theme_Switch.Content = "Dark Mode";
        Setting.Default.Theme = false;
        Setting.Default.Save();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
    }

    private void TitleBar_CloseClicked(TitleBar sender, RoutedEventArgs args)
    {
        Application.Current.Shutdown();
    }
    private void btn_RT_Click(object sender, RoutedEventArgs e)
    {
        string? table_name = cb_Table.SelectedItem.ToString();
        if (table_name == null) return;

        RealTimeViewer rTV = new RealTimeViewer();
        rTV.tablename = table_name;
        rTV.connectionstring = this.connectionstring;
        rTV.TittleBar_modification.Title = rTV.tablename;
        rTV.Top10Generate();
        rTV.Show();
    }

    #endregion

    #region PLC Connect Switch
    private async void PLC_Connect_Switch_Checked(object sender, RoutedEventArgs e)
    {
        string[] ipaddress = PLCIP_tb.Text.Split(".");
        MessageBox messageBox = new MessageBox();
        messageBox.Content = "Invalid IP Address";


        if (PLC_Connect_Switch.IsChecked == false)
        {
            PLC_Connect_Switch.Content = "Disconnected";
            PLC_Connect_Switch.IsEnabled = true;
            PLC_Connect_Switch.IsChecked = false;
            PLCIP_tb.IsEnabled = true;
            cb_Table.SelectedIndex = -1;
            cb_Table.Items.Clear();
        }


        #region Check IP Address Validity
        if (ipaddress.Length != 4)
        {
            await messageBox.ShowDialogAsync();
            PLC_Connect_Switch.IsChecked = false;
            return;
        }

        foreach(string s in ipaddress)
        {
            if (!int.TryParse(s, out int i)||s.Length>3||!(int.Parse(s) <= 255))
            {
                await messageBox.ShowDialogAsync();
                PLC_Connect_Switch.IsChecked = false;
                return;
            }
        }
        #endregion


        PLCIP_tb.IsEnabled = false;
        DBIP_tb.IsEnabled = false;
        DB_Connect_Switch.IsEnabled = false;
        PLC_Connect_Switch.IsEnabled = false;

        PLC_Connect_Switch.Content = "Connecting...";
        PLC_IP = PLCIP_tb.Text;

        await Task.Run( () =>
            {
                bool read = false;
                PLC_Tag["PLC_IP"] = new TagBool()
                {
                    Name = "CSharp_Connected",
                    Gateway = PLC_IP,
                    Path = "1,0",
                    PlcType = PlcType.Omron,
                    Protocol = Protocol.ab_eip,
                    Timeout = TimeSpan.FromSeconds(5)
                };
                PLC_Tag["DB_Name"] = new TagString()
                {
                    Name = "DB_Name",
                    Gateway = PLC_IP,
                    Path = "1,0",
                    PlcType = PlcType.Omron,
                    Protocol = Protocol.ab_eip,
                    Timeout = TimeSpan.FromSeconds(5)
                };
                PLC_Tag["DB_IP"] = new TagString()
                {
                    Name = "DB_IPAddress",
                    Gateway = PLC_IP,
                    Path = "1,0",
                    PlcType = PlcType.Omron,
                    Protocol = Protocol.ab_eip,
                    Timeout = TimeSpan.FromSeconds(5)
                };

                try { read = ((TagBool)PLC_Tag["PLC_IP"]).Read(); } catch { }

                Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (read)
                        {
                            DBName_tb.Text = ((TagString)PLC_Tag["DB_Name"]).Read();
                            DBIP_tb.Text = ((TagString)PLC_Tag["DB_IP"]).Read();
                            DB_Connect_Switch.IsEnabled = true;
                            PLC_Connect_Switch.IsEnabled = true;
                            PLC_Connect_Switch.Content = "Connected";

                            Create_ConnectionString();
                        }
                        else
                        {
                            PLC_Connect_Switch.IsEnabled = true;
                            PLC_Connect_Switch.IsChecked = false;
                            PLCIP_tb.IsEnabled = true;
                            PLC_Connect_Switch.Content = "Connection Time Out";
                        }
                    }
                );
            }
        );
    }

    #endregion

    #region Database Connection

    private void Create_ConnectionString()
    {
        PLC_Tag["User"] = new TagString()
        {
            Name = "DB_Login_Name",
            Gateway = PLC_IP,
            Path = "1,0",
            PlcType = PlcType.Omron,
            Protocol = Protocol.ab_eip,
            Timeout = TimeSpan.FromSeconds(5)
        };
        PLC_Tag["Password"] = new TagString()
        {
            Name = "DB_Login_Password",
            Gateway = PLC_IP,
            Path = "1,0",
            PlcType = PlcType.Omron,
            Protocol = Protocol.ab_eip,
            Timeout = TimeSpan.FromSeconds(5)
        };
        string IP = DBIP_tb.Text;
        string database = DBName_tb.Text;
        string username = ((TagString)PLC_Tag["User"]).Read();
        string password = ((TagString)PLC_Tag["Password"]).Read();


        connectionstring = 
            $"Data Source={IP};" +
            $"Initial Catalog={database};" +
            $"Persist Security Info=True;" +
            $"User ID={username};" +
            $"Password={password};" +
            $"Trust Server Certificate=True";

    }

    private async void DB_Connect_Switch_Checked(object sender, RoutedEventArgs e)
    {
        DB_Connect_Switch.IsEnabled = false;
        DB_Connect_Switch.Content = "Connecting";

        await Task.Run(() => 
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    DataTable dt = new DataTable();
                    using (SqlConnection cnn = new SqlConnection(connectionstring))
                    {
                        using (SqlCommand cmd = new SqlCommand("Select name from sys.tables"))
                        {
                            cmd.Connection = cnn;
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                    foreach (DataRow dr in dt.Rows)
                    {
                        cb_Table.Items.Add(dr[0]);
                    }
                    DB_Connect_Switch.Content = "Connected";

                }
                catch (Exception)
                {
                    DB_Connect_Switch.Content = "Database Error";
                    DB_Connect_Switch.IsEnabled = true;
                    DB_Connect_Switch.IsChecked = false;
                }
            });
        });
    }

    #endregion

    #region Table Generate
    private async void cb_Table_Selected(object sender, RoutedEventArgs e)
    {
        string? table_name = cb_Table.SelectedItem.ToString();
        if (table_name == null) return;
        btn_RT.IsEnabled = true;
        await Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SQL_DG.ItemsSource = PDT.DefaultView;
                SP_Filter.Children.Clear();
                SP_Filter.Children.Add(tb_Filter);
                SP_Filter.Children.Add(btn_RT);
                SP_Filter.Children.Add(LN_Switch);
                SP_Filter.Children.Add(cb_LN);

                DataTable cbdt = new DataTable();

                Filter1.Clear();
                Filter2.Clear();
                try
                {
                    DataTable dt = new DataTable();
                    DataTable Columns_Name = new DataTable();
                    using (SqlConnection cnn = new SqlConnection(connectionstring))
                    {
                        using (SqlCommand cmd = new SqlCommand($"SELECT * FROM [{table_name}]"))
                        {
                            cmd.Connection = cnn;
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(dt);
                            }
                        }
                        using (SqlCommand cmd = new SqlCommand($"SELECT DISTINCT LOTNUMBER FROM [{table_name}]"))
                        {
                            cmd.Connection = cnn;
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(cbdt);
                            }
                        }
                        using (SqlCommand cmd = new SqlCommand($"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'[{table_name}]'"))
                        {
                            cmd.Connection = cnn;
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                adapter.Fill(Columns_Name);
                            }
                        }

                    }


                    PDT = dt;
                    NDT = dt;
                    SQL_DG.ItemsSource = dt.DefaultView;

                    foreach(DataRow dr in cbdt.Rows)
                    {
                        cb_LN.Items.Add(dr[0]);
                    }

                    foreach (DataRow dr in Columns_Name.Rows)
                    {
                        string? Datatype = dr.ItemArray[1]?.ToString();
                        if (Datatype?.Contains("date") == true || Datatype?.Contains("char") == true)
                        {
                            string? key = dr.ItemArray[0]?.ToString();
                            if (key == null) continue; 

                            Filter1[key] = new TextBox();
                            Filter1[key].IsEnabled = false;
                            Filter1[key].Margin = new Thickness(5, 5, 5, 5);

                            Filter2[key] = new ToggleSwitch();
                            Filter2[key].IsChecked = false;
                            Filter2[key].Content = dr.ItemArray[0]?.ToString();
                            Filter2[key].Margin = new Thickness(5, 5, 5, 5);
                            Filter2[key].Click += ((object s, RoutedEventArgs rea) => { Filter1[key].IsEnabled = !Filter1[key].IsEnabled; });

                            SP_Filter.Children.Add(Filter2[key]);
                            SP_Filter.Children.Add(Filter1[key]);
                        }
                    }
                }
                catch { }
            });
        });

        if(table_name == "CALIBRATION")
        {
            btn_Export.IsEnabled = false;
        }

    }

    private void Empty_Table()
    {
        DataTable dt = new DataTable();

        dt.Columns.Add("Column1");
        dt.Columns.Add("Column2");
        dt.Columns.Add("Column3");

        dt.Rows.Add("A1","A2","A3");
        dt.Rows.Add("B1","B2","B3");
        dt.Rows.Add("C1","C2","C3");

        SQL_DG.ItemsSource = dt.DefaultView;
    }

    private void Button_Search_Click(object sender, RoutedEventArgs e)
    {
        NDT = PDT;
        while(LN_Switch.IsChecked==true)
        {
            if (cb_LN.SelectedItem == null) break;
            string? keyword = cb_LN.SelectedItem.ToString();
            if(keyword == null) break;
            try
            {
                NDT = (from row in NDT.AsEnumerable()
                       where row.Field<string>("LOTNUMBER")?.Contains(keyword) == true
                       select row).CopyToDataTable();
            }
            catch (InvalidOperationException)
            {
                NDT = new DataTable();
            }
            break;
        }
        foreach (KeyValuePair<string, TextBox> kvp in Filter1)
        {
            if (Filter2[kvp.Key].IsChecked == false) continue;
            string keyword = kvp.Value.Text;
            string column = kvp.Key;
            try
            {
                NDT = (from row in NDT.AsEnumerable()
                       where row.Field<string>(column)?.Contains(keyword) == true
                       select row).CopyToDataTable();
            }
            catch (InvalidOperationException)
            {
                NDT = new DataTable();
            }
            catch (InvalidCastException)
            {
                try
                {
                    NDT = (from row in NDT.AsEnumerable()
                           where (row.Field<DateTime>(column)).ToString("d/M/yyyy").Contains(keyword)
                           select row).CopyToDataTable();
                }
                catch (InvalidOperationException)
                {
                    NDT = new DataTable();
                }
            }
        }
        SQL_DG.ItemsSource = NDT.AsDataView();
    }

    private void Button_Clear_Click(object sender, RoutedEventArgs e)
    {
        LN_Switch.IsChecked = false;
        cb_LN.SelectedIndex = -1;
        foreach (KeyValuePair <string,TextBox> kvp in Filter1)
        {
            Filter2[kvp.Key].IsChecked = false;
            kvp.Value.IsEnabled = false;
            kvp.Value.Text = "";
        }
        SQL_DG.ItemsSource = PDT.AsDataView();
    }
    
    private void Button_Export_Click(object sender, RoutedEventArgs e)
    {
        string? lotNumber = cb_LN.SelectedItem.ToString();
        string? stationName = cb_Table.SelectedItem.ToString();
        string moduleName = DBName_tb.Text;

        ExportCSV cSV = new ExportCSV();
        cSV.LotNumbers = cb_LN.Items.Cast<string>().ToList();
        cSV.ShowDialog();
        if (!cSV.isExport) return;

        lotNumber = cSV.lotNumber;

        if (stationName == null||lotNumber==null) return;

        Directory.CreateDirectory(cSV.path + $"/{moduleName}/{DateTime.Now.ToString("MMMM yyyy")}/{stationName}/");

        int num = cSV.dPF;

        string path = cSV.path + $"/{moduleName}/{DateTime.Now.ToString("MMMM yyyy")}/{stationName}/";

        Dictionary<string, string> rawReport = generateRawReport(lotNumber, num);
        foreach (KeyValuePair<string, string> kvp in rawReport)
        {
            File.WriteAllText(path + $"/{stationName} {lotNumber}{kvp.Key}.csv", kvp.Value);
        }

        string summaryReport = generateSummaryReport(lotNumber);
        File.WriteAllText(path + $"/Summary of {stationName} - {DateTime.Now.ToShortDateString()}.csv", summaryReport);

       ( bool check, string calibrationReport) = generateCalibrationReport(stationName);
        if (!check) return;
        File.WriteAllText(path + $"/Calibration - {stationName} - {DateTime.Now.ToShortDateString()}.csv", calibrationReport);

    }

    private Dictionary<string,string> generateRawReport(string LotNumber,int num)
    {
        Dictionary<int, DataTable> t_Report = new Dictionary<int, DataTable>();
        Dictionary<string, string> Report = new Dictionary<string, string>();
        DataTable dt = PDT.AsEnumerable().Where(x => x["LOTNUMBER"].ToString() == LotNumber.ToString()).CopyToDataTable();
        int count = dt.AsEnumerable().Count();
        int splits = (int)Math.Ceiling((double)count / num);
        int first = dt.AsEnumerable().First().Field<int>("ID");
        for(int i = 1; i <= splits; i++)
        {
            int start = first + (i - 1) * num;
            int end = first + i * num;
            if (end > dt.AsEnumerable().Last().Field<int>("ID")) end = dt.AsEnumerable().Last().Field<int>("ID");
            t_Report[i] = dt.AsEnumerable().Where(x => x.Field<int>("ID") >= start && x.Field<int>("ID") < end).CopyToDataTable();
        }
        foreach (KeyValuePair<int, DataTable> kvp in t_Report)
        {
            int temp = kvp.Key;
            List<int> base24 = new List<int>();
            while (temp > 0)
            {
                int i = temp % 24;
                if (i >=8) i++;
                if(i >= 14) i++;
                base24.Add(i);
                temp /= 24;
            }
            List<string> base24str = new List<string>();
            foreach (int i in base24)
            {
                base24str.Add(((char)(i + 'A'-1)).ToString());
            }
            string code = string.Empty;
            foreach (string s in base24str)
            {
                code = s + code;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Lot Number:," + LotNumber.ToString()+code + Environment.NewLine);
            sb.Append(Environment.NewLine);

            foreach (DataColumn col in kvp.Value.Columns)
            {
                if (col.ColumnName.ToUpper() == "LOTNUMBER") continue;
                sb.Append($"{col.ColumnName},");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(Environment.NewLine);
            foreach (DataRow dr in kvp.Value.Rows)
            {
                foreach (DataColumn col in kvp.Value.Columns)
                {
                    if (col.ColumnName.ToUpper() == "LOTNUMBER") continue;
                    sb.Append($"{dr[col].ToString()},");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append(Environment.NewLine);
            }
            Report[code] = sb.ToString();
        }
        return Report;
    }

    private string generateSummaryReport(string LotNumber)
    {
        string result = string.Empty;
        foreach (DataColumn col in PDT.Columns)
        {
            if(col.ColumnName.ToLower().Contains("result"))
            {
                result = col.ColumnName.ToUpper();
                break;
            }
        }

        StringBuilder stringBuilder = new StringBuilder();
        int count = PDT.AsEnumerable().Where(x => x["LOTNUMBER"].ToString() == LotNumber).Count();
        int pass = PDT.AsEnumerable().Where(x => x["LOTNUMBER"].ToString() == LotNumber && x[result].ToString() == "PASS").Count();
        List<DateTime> dateTimes = PDT.AsEnumerable()
            .Where(x => x["LOTNUMBER"].ToString() == LotNumber)
            .Select(x => x.Field<DateTime>("DATE")).ToList();
        List<TimeSpan> timeSpans = new List<TimeSpan>();
        for (int i = 0; i < dateTimes.Count - 1; i++)
        {
            timeSpans.Add(dateTimes[i + 1] - dateTimes[i]);
        }
        List<double> seconds = new List<double>() { timeSpans[0].TotalSeconds };
        for(int i = 1;i< timeSpans.Count; i++)
        {
            if (((timeSpans[i].TotalSeconds - timeSpans[i - 1].TotalSeconds) / timeSpans[i-1].TotalSeconds)>1) continue;
            seconds.Add(timeSpans[i].TotalSeconds);
        }
        

        double uph = 3600 / seconds.Average();// units per hour
        stringBuilder.Append("Lot Number:," + LotNumber + Environment.NewLine);
        stringBuilder.Append("Lot Quantity:," + count.ToString() + Environment.NewLine);
        stringBuilder.Append("Total Input:," + count.ToString() + Environment.NewLine);
        stringBuilder.Append("Total Output:," + (pass/count*100).ToString()+ Environment.NewLine);
        stringBuilder.Append("Total Output(pass):," + pass.ToString()+ Environment.NewLine);
        stringBuilder.Append("Total Output(fail):,"+ (count - pass).ToString() + Environment.NewLine);
        stringBuilder.Append("Units Per Hour:," + uph.ToString() + Environment.NewLine);


        return stringBuilder.ToString();
    }

    private (bool,string) generateCalibrationReport(string stationName)
    {
        DateTime today = DateTime.Now;
        StringBuilder sb = new StringBuilder();

        DataTable dt = new DataTable();


        using (SqlConnection cnn = new SqlConnection(connectionstring))
        {
            using (SqlCommand cmd = new SqlCommand($"SELECT * FROM [CALIBRATION]"))
            {
                cmd.Connection = cnn;
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
        }

        dt = dt.AsEnumerable()
            .Where(x => x.Field<DateTime>("DATETIME").ToString("d/MM/yyyy") == today.ToString("d/MM/yyyy"))
            .Where(x => x.Field<string>("STATIONNAME")?.ToString().ToLower()==stationName.ToLower())
            .CopyToDataTable();

        if (dt.Rows.Count == 0) return (false, "");

        foreach (DataColumn col in dt.Columns)
        {
            if (col.ColumnName=="STATIONNAME") continue;
            sb.Append($"{col.ColumnName},");
        }
        sb.Remove(sb.Length - 1, 1);
        sb.Append(Environment.NewLine);
        foreach (DataRow dr in dt.Rows)
        {
            foreach (DataColumn col in dt.Columns)
            {
                if (col.ColumnName == "STATIONNAME") continue;
                sb.Append($"{dr[col].ToString()},");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(Environment.NewLine);
        }
        return (true,sb.ToString());
    }


    #endregion

    private void LN_Switch_Click(object sender, RoutedEventArgs e)
    {
        bool check = LN_Switch.IsChecked == true;
        cb_LN.IsEnabled = check;
    }
}