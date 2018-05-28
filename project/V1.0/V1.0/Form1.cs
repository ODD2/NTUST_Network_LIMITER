#define ODDDEBUG_Lv1

//#define ODDDEBUG_LV2
using System;
using System.Windows.Forms;
using System;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.IO;


namespace V1._0
{
    public partial class Form1 : Form
    {

        IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
        NetworkInterface adapter;
        string NoNICmsg = "沒有找到連接台科網路的網卡。\n請檢查網路裝置後再使用本軟體\n";

        string folder_location = Environment.GetEnvironmentVariable("userprofile") + "\\Documents\\" + "\\ntustnet\\";

        string customTimefmt = "yyyy/MM/dd-HH:mm:ss.";
        string debugTimefmt = "MM/dd-HH:mm:ss.";

        DateTime NetCurrentTime;

        double Restrict = 0.0;
        double Runner = 0.0;
        double DeltaRunner = 0.0;
        double Start = 0.0;
        bool unlock = false;


        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            notifyIcon1.Icon = this.Icon;
            notifyIcon1.Visible = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            //初始化
            //Textbox1的外觀
            textBox1.BorderStyle = BorderStyle.Fixed3D;
            double textBox1_offsetY = 17.727272727272 * 6;

            label1.Text += computerProperties.HostName + "." + computerProperties.DomainName;


            //顯示沒有網卡
            if (nics == null || nics.Length < 1)
            {
                label1.Text += "沒有找到網卡。\n";
                label1.Text += NoNICmsg;
                return;
            }


            label1.Text += "的網卡資訊:(找到" + nics.Length + "張網卡)\n";
            int adap_index = 1;

            //列出所有網卡
            label1.Text += "==========================================\n";
            foreach (NetworkInterface i in nics)
            {
                label1.Text += "  (" + adap_index + ")->" + i.Name + "\n";
                adap_index += 1;
                textBox1_offsetY += 17.727272727272;
            }
            label1.Text += "==========================================\n";

            //偵測台科網路用的網卡
            adap_index = 0;
            foreach (NetworkInterface i in nics)
            {

                if (i.OperationalStatus.ToString() == "Up")
                {
                    if (String.Compare(i.GetIPProperties().UnicastAddresses[1].Address.ToString().Substring(0, 7), "140.118") == 0)
                    {
                        label1.Text += "找到台科網卡:\n";
                        label1.Text += "    [" + i.Name + "]----[台科網路]\n";
                        adapter = i;
                        unlock = true;
                        break;
                    }
                }
                adap_index += 1;
            }
            label1.Text += "==========================================\n";


            //沒有找到台科網卡的狀況
            if (adap_index == nics.Length)
            {
                label1.Text += NoNICmsg;
                return;
            }

            WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "Application Started!");

            //輸入流量方塊
            label1.Text += "流量限制:                     (輸入完請按enter)\n";
            label1.Text += "==========================================\n";
            textBox1.Location = new System.Drawing.Point(79, (int)textBox1_offsetY);
            textBox1.Visible = true;


            //確保需要的檔案存在
            CheckFileFolder();


            //到台科流量網站以及本地檔案更新目前總流量
            RunnerInitializer();
            BackupStatisticToFile();

#if (ODDDEBUG_Lv1)
            WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "Initialized Runner:" + Runner.ToString() + "\n");
#endif
            //更新本機網卡的流量基礎
            InitInterfaceStatisticBase();

            //啟用限流核心
            timer1.Enabled = true;


            //啟動下面板的顯示
            label5.Visible = true;
            label6.Visible = true;
            label3.Visible = true;
            label4.Visible = true;

            return;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (unlock)
            {
                BackupStatisticToFile();
                WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "Application Closed!");
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.notifyIcon1.Visible = true;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.notifyIcon1.Visible = false;
        }


        /// <summary>
        /// TEXTBOX
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.BackColor = System.Drawing.Color.Red;
            textBox1.BorderStyle = BorderStyle.None;
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == '\r')
            {
                //textBox1.BorderStyle = BorderStyle.Fixed3D;
                try
                {
                    Restrict = Convert.ToDouble(textBox1.Text);

                }
                catch (Exception e_2)
                {
                    WriteDebugToFileLog("Error,Wrong Float Number Format!!\n");
                    Restrict = 0.0;
                }

                if (Restrict == 0.0)
                {
                    textBox1.Text = "無限制";
                }
                //每次使用者按下enter就更新流量上限
                textBox1.BackColor = System.Drawing.Color.Black;
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void textBox1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar < '0' || e.KeyChar > '9')
            {
                if (e.KeyChar == '\r' || e.KeyChar == '.' || e.KeyChar == '\b') ;
                else e.Handled = true;
            }
        }

        private void textBox1_MouseClick(object sender, MouseEventArgs e)
        {
            textBox1.SelectAll();
        }


        /// <summary>
        /// TIMER(CORE)
        /// </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            
            NetCurrentTime= NetCurrentTime.AddSeconds(1);
            
            //Initialize Runner Every Time A Day Passed.
            if (NetCurrentTime.Hour == 0 && NetCurrentTime.Minute == 0 && NetCurrentTime.Second == 0)
            {
                UpdateInterfaceStatistics();
                WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "Day Changed!");
                BackupStatisticToFile();
                Runner = 0.0;
                return;
            }

            //Update Runner And DeltaRunner
            UpdateInterfaceStatistics();

            //Restrict the Network Flow only When There's a Restrict
            if (Restrict != 0)
            {
                if (Runner >= Restrict - 0.1)
                {
#if (ODDDEBUG_Lv1)
                    WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "MEET RESTRICT LIMIT:" + Runner.ToString() + "\n");
#endif
                    DisableInterface(adapter.Name);

                    timer1.Enabled = false;
                    BackupStatisticToFile();
                    return;
                }
            }



            //每30秒備份到檔案裡.
            if (NetCurrentTime.Second==30 || DeltaRunner >= 10000.0)
            {
                BackupStatisticToFile();
            }
        }



        /// <summary>
        /// STATISTIC 
        /// </summary>
        private void UpdateInterfaceStatistics()
        {
#if (ODDDEBUG_Lv2)
            WriteDebugToFileLog("RUNNER:" + Runner.ToString() + "\n");
#endif

            DeltaRunner = GetInterfaceStatistics() - Start;
            Start += DeltaRunner;
            Runner += DeltaRunner;
            RunnersDisplay();
        }

        private double GetInterfaceStatistics()
        {
            double RecGB = (double)adapter.GetIPv4Statistics().BytesReceived / (1024 * 1024 * 1024);
            double SendGB = (double)adapter.GetIPv4Statistics().BytesSent / (1024 * 1024 * 1024);
            return RecGB + SendGB;
        }

        private void InitInterfaceStatisticBase()
        {
            Start = GetInterfaceStatistics();
#if (ODDDEBUG_Lv1)
            WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "Initialized Start:" + Start.ToString() + "\n");
#endif
        }


        /// <summary>
        /// Runner Initialization
        /// </summary>
        /// 
        private void RunnerInitializer()
        {
            double http = UpdateRealTotalFromHttp();
            double file = UpdateRealTotalFromFile();
            Runner = file > http ? file : http;
        }

        private double UpdateRealTotalFromHttp()
        {
            try
            {
                WebClient client = new WebClient();

                Stream data = client.OpenRead("https://network.ntust.edu.tw/");
                StreamReader reader = new StreamReader(data);
                string s = reader.ReadToEnd();
                data.Close();
                reader.Close();
                int index = s.IndexOf("總計");
                for (int i = 0; i < 4; i++)
                {
                    index = s.IndexOf("<td>", index);
                    index += 4;
                }

                while (!(s[index] >= 48 && s[index] <= 57)) index += 1;
                string total_KB_string = "";
                while (s[index] != ' ')
                {
                    if (s[index] >= '0' && s[index] <= '9')
                    {
                        total_KB_string += s[index];
                    }
                    index += 1;
                }
                return Convert.ToDouble(total_KB_string) / (1024 * 1024);
            }
            catch (Exception e)
            {
                WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "UpdateRealTotalFromHttp->Exception:" , e.ToString() + "\n");
                return 0.0;
            }

        }

        private double UpdateRealTotalFromFile()
        {
            string[] STAT_TXT = File.ReadAllLines(folder_location + "STAT.txt");
            //如果是新建立的檔案
            if (STAT_TXT.Length == 0) { return 0.0; }



            //如果不是新建立的檔案
            NetCurrentTime = GetNetworkTime();
            if (String.Compare(NetCurrentTime.ToString(customTimefmt), STAT_TXT[0]) < 0)
            {
#if (ODDDEBUG_Lv1)
                WriteDebugToFileLog(GetNetworkTime().ToString(debugTimefmt), "File Date is Faster than Current Date!!" + NetCurrentTime.ToString(customTimefmt) + "<" + STAT_TXT[0] + "\n");
#endif
                return 0.0;
            }
            else if (String.Compare(NetCurrentTime.ToString(customTimefmt).Substring(0, 10), STAT_TXT[0].Substring(0, 10)) > 0)
            {
                //如果檔案的日期比較舊，就回傳0
                return 0.0;
            }
            else
            {
                return Convert.ToDouble(STAT_TXT[1]);
            }
        }

        /// <summary>
        /// FILEIO
        /// </summary>
        private void CheckFileFolder()
        {
            if (!Directory.Exists(folder_location))
                Directory.CreateDirectory(folder_location);
            if (!File.Exists(folder_location + "LOG.txt"))
                using (File.CreateText(folder_location + "LOG.txt")) { }
            if (!File.Exists(folder_location + "STAT.txt"))
                using (File.CreateText(folder_location + "STAT.txt")) { }
        }

        private void BackupStatisticToFile()
        {
            NetCurrentTime = GetNetworkTime();
            string Content = NetCurrentTime.ToString(customTimefmt) + "\n" + Runner.ToString();
            File.WriteAllText(folder_location + "STAT.txt", Content);
        }

        private void WriteDebugToFileLog(string msg)
        {
            using (StreamWriter sw = File.AppendText(folder_location + "LOG.txt"))
            {
                sw.Write(msg);
            }
        }

        private void WriteDebugToFileLog(string title, string msg)
        {
            using (StreamWriter sw = File.AppendText(folder_location + "LOG.txt"))
            {
                sw.Write(title+ "\n" + "\t" +StringFMT_TAB(msg));
            }
        }

        private void WriteDebugToFileLog(string time, string title,string msg)
        {
            using (StreamWriter sw = File.AppendText(folder_location + "LOG.txt"))
            {
                sw.Write(time + "\n" + "\t" + title+"\n\t\t"+StringFMT_TAB(StringFMT_TAB(msg)));
            }
        }

        private string StringFMT_TAB(string msg)
        {
            int index = 0;
            if (msg[msg.Length - 1] != '\n') { msg =  msg.Insert(msg.Length, "\n"); };
            while ((index = msg.IndexOf('\n', index)) != -1)
            {
                if (index == msg.Length - 1) break;
                msg = msg.Insert(++index, "\t");
            }
            return msg;
        }


        /// <summary>
        /// Interface Control
        /// </summary>
        /// <param name="name"></param>
        private void DisableInterface(string name)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C netsh interface set interface name=\"" + name + "\" admin=DISABLE";
            process.StartInfo = startInfo;
            process.Start();
        }

        private void EnableInterface(string name)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C netsh interface set interface name=\"" + name + "\" admin=ENABLE";
            process.StartInfo = startInfo;
            process.Start();
        }


        /// <summary>
        /// Display
        /// </summary>
        private void RunnersDisplay()
        {
            double DeltaInKb = DeltaRunner * 1000 * 1000;
            if (this.Visible == true)
            {
                label3.Text = Math.Round(DeltaInKb, 2).ToString();
                label4.Text = Math.Round(Runner, 2).ToString();
            }
            else if (notifyIcon1.Visible == true)
            {
                notifyIcon1.Text = "限制: " + textBox1.Text + "(GB)\n" +
                                   "目前: " + Math.Round(Runner, 2).ToString() + "(GB)";
            }
        }


        /// <summary>
        /// Time Server Get Time;
        /// </summary>
        /// <returns></returns>
        private DateTime GetNetworkTime()
        {
            try
            {
                //default Windows time server
                const string ntpServer = "time.windows.com";

                // NTP message size - 16 bytes of the digest (RFC 2030)
                var ntpData = new byte[48];

                //Setting the Leap Indicator, Version Number and Mode values
                ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

                var addresses = Dns.GetHostEntry(ntpServer).AddressList;

                //The UDP port number assigned to NTP is 123
                var ipEndPoint = new IPEndPoint(addresses[0], 123);
                //NTP uses UDP

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {

                    socket.Connect(ipEndPoint);

                    //Stops code hang if NTP is blocked
                    socket.ReceiveTimeout = 3000;

                    socket.Send(ntpData);
                    socket.Receive(ntpData);
                    socket.Close();
                }

                //Offset to get to the "Transmit Timestamp" field (time at which the reply 
                //departed the server for the client, in 64-bit timestamp format."
                const byte serverReplyTime = 40;

                //Get the seconds part
                ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

                //Get the seconds fraction
                ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

                //Convert From big-endian to little-endian
                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                //**UTC** time
                var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);
                return networkDateTime.ToLocalTime();
            }
            catch (Exception e)
            {
                WriteDebugToFileLog("GetNetworkTime->Exception",e.ToString() + "\n");
                return NetCurrentTime;
            }
        }

        private uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }

      
    }
}
