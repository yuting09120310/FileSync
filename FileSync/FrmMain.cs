using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace FileSync {
    public partial class FrmMain : Form {
        // 表單變數 START
        private string appName = "FileSync";      // 應用程式名稱
        private string appPath = Application.StartupPath;  // 程式所在位置
        private List<string> SyncFolder = new List<string>();
        private string FtpHost = ConfigurationManager.AppSettings["ftpHost"].ToString();
        private string FtpUsername = ConfigurationManager.AppSettings["ftpUsername"].ToString();
        private string FtpPassword = ConfigurationManager.AppSettings["ftpPassword"].ToString();
        // 表單變數 END


        // 表單初始化
        public FrmMain() {
            InitializeComponent();
        }


        // 表單載入
        private void FrmMain_Load(object sender, EventArgs e) {
            try {
                // 設定程式在螢幕正中間
                int x = (SystemInformation.WorkingArea.Width - this.Size.Width) / 2;
                int y = (SystemInformation.WorkingArea.Height - this.Size.Height) / 2;
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(x, y);
                this.Text = appName;

                // 防止程式重覆執行 (本機所有使用者)
                string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                System.Diagnostics.Process[] myProcess = System.Diagnostics.Process.GetProcessesByName(processName);
                if (myProcess.Length > 1) { Environment.Exit(0); }

                // ListView 初始化
                lv_Info.View = View.Details;
                lv_Info.GridLines = true;
                lv_Info.LabelEdit = false;
                lv_Info.FullRowSelect = true;
                lv_Info.Columns.Clear();
                lv_Info.Columns.Add("時間", 160, HorizontalAlignment.Center);
                lv_Info.Columns.Add("訊息", 330, HorizontalAlignment.Left);

                // 讀取要監控的資料夾路徑列表
                var folderPaths = ConfigurationManager.AppSettings["SyncFolder"];
                if (!string.IsNullOrEmpty(folderPaths)) {
                    SyncFolder.AddRange(folderPaths.Split(','));
                }

                UI_Message("程式啟動完成");

                // 啟動監控多個資料夾的工作
                foreach (var folderPath in SyncFolder) {
                    Task.Run(() => MonitorFolder(folderPath));
                }

            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, appName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // 監控資料夾
        private void MonitorFolder(string folderPath) {
            List<string> listFiles = new List<string>();

            bool i = false;

            while (true) {
                try {
                    List<string> currentFiles = Directory.GetFiles(folderPath).ToList();

                    // 第一次
                    if (i == false) {
                        foreach (string file in currentFiles) {
                            listFiles.Add(file);
                        }
                        i = true;
                    }
                    // 第一次之後
                    else {
                        // 如果檔案有異動
                        if (currentFiles.SequenceEqual(listFiles) == false) {
                            var addFiles = currentFiles.Except(listFiles).ToList();
                            var delFiles = listFiles.Except(currentFiles).ToList();

                            if (addFiles.Any() == true) {
                                foreach (string file in addFiles) {
                                    string tmpFile = file.Replace(folderPath + "\\", "");

                                    int len = file.Split(new string[] { "\\" }, StringSplitOptions.None).Length - 1;
                                    string type = file.Split(new string[] { "\\" }, StringSplitOptions.None)[len - 1];

                                    // 發送檔案
                                    if (tmpFile.Contains("__") == false) {
                                        try {
                                            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + FtpHost + "/" + type + "/__" + tmpFile);
                                            request.Method = WebRequestMethods.Ftp.UploadFile;
                                            request.Credentials = new NetworkCredential(FtpUsername, FtpPassword);

                                            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                                            Stream fileStream = File.OpenRead(file);
                                            Stream ftpStream = request.GetRequestStream();
                                            fileStream.CopyTo(ftpStream);

                                            response.Dispose();
                                            ftpStream.Dispose();
                                            fileStream.Dispose();

                                            listFiles.Add(file);
                                            UI_Message("新增 : " + type + @"\" + tmpFile);
                                        }
                                        catch (Exception ex) {
                                            UI_Message("FTP 同步時發生錯誤 : " + type + @"\" + tmpFile + " , " + ex.Message);
                                        }
                                    }

                                    // 接收檔案
                                    else {
                                        tmpFile = tmpFile.Replace("__", "");

                                        try {
                                            File.Move(file, file.Replace("__", ""));

                                            listFiles.Add(file.Replace("__", ""));
                                            UI_Message("接收 : " + type + @"\" + tmpFile);
                                        }
                                        catch {
                                            continue;
                                        }
                                    }
                                }
                            }
                            else {
                                foreach (string file in delFiles) {
                                    try {
                                        string tmpFile = file.Replace(folderPath + "\\", "");

                                        int len = file.Split(new string[] { "\\" }, StringSplitOptions.None).Length - 1;
                                        string type = file.Split(new string[] { "\\" }, StringSplitOptions.None)[len - 1];

                                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://" + FtpHost + "/" + type + "/" + tmpFile);
                                        request.Method = WebRequestMethods.Ftp.DeleteFile;
                                        request.Credentials = new NetworkCredential(FtpUsername, FtpPassword);

                                        FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                                        response.Dispose();

                                        listFiles.Remove(file);
                                        UI_Message("移除 : " + type + @"\" + tmpFile);
                                    }
                                    catch {
                                        continue;
                                    }
                                }
                            }
                        }
                    }

                    Task.Delay(1000).Wait();

                }
                catch (Exception ex) {
                    UI_Message(ex.Message);
                }
            }
        }


        // 表單關閉
        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e) {
            try {

            }
            catch {
            }

            this.Dispose();
            this.Close();
            Environment.Exit(0);
        }


        // 訊息顯示
        private void UI_Message(string message) {
            try {
                // 寫實體 Log 檔
                LogMessage(message);

                // UI 顯示訊息
                if (lv_Info.Items.Count >= 8) {
                    lv_Info.Items.Clear();
                }

                string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                ListViewItem itmData;
                itmData = new ListViewItem(nowTime);
                itmData.SubItems.Add(message);
                lv_Info.Items.Add(itmData);
                Application.DoEvents();

            }
            catch {
            }
        }


        // 記錄 Log 副程式 (要記錄的訊息)
        public void LogMessage(string message = "") {
            try {
                string logDir = "Log";
                if (appPath.Length == 0) { return; }
                if (appPath.EndsWith("\\") == false) { appPath += "\\"; }

                // 檢查 Log 目錄
                if (!Directory.Exists(appPath + logDir)) {
                    Directory.CreateDirectory(appPath + logDir);
                }

                // Log 訊息內容
                string logMsg = "";
                logMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message;

                // 寫入 Log
                FileStream fs = File.Open(appPath + logDir + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(logMsg);
                sw.Dispose();
                fs.Dispose();
            }
            catch {
            }
        }




    }
}
