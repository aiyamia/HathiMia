using System;
using System.Windows;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Configuration;

namespace HathiFullViewArchive_1_Wpf
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        List<DownloadItem> items;
        List<int> list;
        string pathSaveBase;
        string tempFolder;
        List<int> pageRangeList;
        CancellationTokenSource tokenSource;
        public MainWindow()
        {
            InitializeComponent();
            RestoreSettings();
        }
        void RestoreSettings()
        {
            var appSettings = ConfigurationManager.AppSettings;
            LinkTextBox.Text = appSettings["Url"];
            PathTextBox.Text = appSettings["Path"];
            StartTextBox.Text = appSettings["Start"];
            EndTextBox.Text = appSettings["End"];
        }
        void UpdateAppSettings()
        {
            Configuration configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection settings = configFile.AppSettings.Settings;
            settings["Url"].Value = LinkTextBox.Text;
            settings["Path"].Value = PathTextBox.Text;
            settings["Start"].Value = StartTextBox.Text;
            settings["End"].Value = EndTextBox.Text;
            configFile.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
        }
        void Window_Closing(object sender, CancelEventArgs e)
        {
            UpdateAppSettings();
        }
        private async void StartDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((string)StartDownloadBtn.Content == "暂停下载")
            {
                tokenSource.Cancel();
                ListRow.Height = 0;
                StartDownloadBtn.Content = "继续下载";
            }
            else { 
                ListRow.Height = 400;
                pathSaveBase = Path.GetDirectoryName(PathTextBox.Text).Replace(@"\", "\\");
                tempFolder = Path.GetFileNameWithoutExtension(PathTextBox.Text) + "_temp";
                tokenSource = new CancellationTokenSource();
                CancellationToken token = tokenSource.Token;
                if ((string)StartDownloadBtn.Content == "开始下载")
                {
                    UpdateAppSettings();
                    pageRangeList = SpecifiedList();
                    Directory.CreateDirectory(pathSaveBase + "\\" + tempFolder);
                }
                list = GetFileIdList(pageRangeList, Path.GetDirectoryName(PathTextBox.Text).Replace(@"\", "\\") + "\\" + tempFolder);
                StatusTextBlock.Text = "开始下载...";
                //StartDownloadBtn.IsEnabled = false;
                StartDownloadBtn.Content = "暂停下载";
                string url = LinkTextBox.Text;
                await GetResource(url, list, token);
            }
        }
        private async Task GetResource(string _url,List<int>_list, CancellationToken cancellationToken)
        {
            Match m = Regex.Match(_url, @"id=([^&]+)&");
            string urlBase = @"https://babel.hathitrust.org/cgi/imgsrv/download/pdf?" + m.Value + @"attachment=1&seq=";
            string urlRequest;
            string pathSave;
            
            List<Task> tasks = new List<Task>();
            items = new List<DownloadItem>();
            int index;
            int length = _list.Count;
            int iter = 0;
            int del = 10;
            int remain = length - del * iter;
            int n_iter = length / del;
            while (iter < n_iter + 1)
            {
                DownloadItemsControl.ItemsSource = null;
                for (int i = del * iter; i < (iter == n_iter ? length: del * (iter+1)); i++)
                {
                    index = _list[i];
                    urlRequest = urlBase + index;
                    pathSave = pathSaveBase + "\\" + tempFolder +"\\temp_" + index + ".pdf";
                    try
                    {
                        items.Add(new DownloadItem("temp_" + index + ".pdf","等待下载"));
                        tasks.Add(FileDownloadAsync(items[i], urlRequest, pathSave, cancellationToken));
                    }
                    catch (Exception)
                    {
                        items[i].DownloadItemCompletion = "×出错";
                    }
                
                }
                DownloadItemsControl.ItemsSource = items;
            
                await Task.WhenAll(tasks.GetRange(del * iter, iter == n_iter ? length % del : del));
                //StatusTextBlock.Text = "已下好10个文件数：" + (length - GetFileIdList(pageRangeList, pathSaveBase + "\\" + tempFolder).Count) + "，歇5秒钟";
                StatusTextBlock.Text = "已下好"+ del * iter + "个文件，歇5秒钟";
                await Task.Delay(5000);
                iter++;
            }
            

            if (GetFileIdList(pageRangeList, pathSaveBase + "\\" + tempFolder).Count==0)
            {
                ListRow.Height = 0;
                StatusTextBlock.Text = "全部完成";
                MergePdf(pageRangeList, PathTextBox.Text);
                //StartDownloadBtn.IsEnabled = true;
                StartDownloadBtn.Content = "开始下载";
            }
            else
            {
                ListRow.Height = 0;
                StatusTextBlock.Text = "处理结束，部分页面需要重新下载，点击“继续下载”";
                //StartDownloadBtn.IsEnabled = true;
                StartDownloadBtn.Content = "继续下载";
            }
            //Process.Start(pathSaveBase + "\\" + tempFolder);
        }
        async Task FileDownloadAsync(DownloadItem _item,string urlRequest, string pathSave, CancellationToken cancellationToken)
        {
            try
            {
                using (WebClient client = new WebClient())
                using (cancellationToken.Register(client.CancelAsync))
                {
                    client.DownloadProgressChanged += new DownloadProgressChangedEventHandler((sender, e) => Client_DownloadProgressChanged(sender, e, _item));
                    client.DownloadFileCompleted += new AsyncCompletedEventHandler((sender, e) => Client_DownloadDownloadFileCompleted(sender, e, _item));
                    await client.DownloadFileTaskAsync(urlRequest, pathSave);
                }
            }
            catch (Exception)
            {
                _item.DownloadItemCompletion = "×出错";
            }
            
        }
        void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e, DownloadItem _item)
        {
            _item.DownloadItemCompletion = "开始下载";
            //DownloadItemsControl.ItemsSource = items;
        }
        void Client_DownloadDownloadFileCompleted(object sender, AsyncCompletedEventArgs e, DownloadItem _item)
        {
            _item.DownloadItemCompletion = "√已完成";
            Task.Delay(1000);
            _item.DownloadItemVisibility = "Collapsed";

        }
        
        List<int> SpecifiedList()
        {
            int start = int.Parse(StartTextBox.Text);
            int end = int.Parse(EndTextBox.Text);
            return Enumerable.Range(start, end - start + 1).ToList();
        }
        List<int> GetFileIdList(List<int> _specifiedList,string path)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            FileInfo[] fileInfos = dirInfo.GetFiles("*.pdf");
            List<int> existList = new List<int>();
            foreach (FileInfo info in fileInfos)
            {
                if (info.Length > 1000 && IsValidPdf(info.FullName))
                    existList.Add(int.Parse(info.Name.Substring(5, info.Name.Length - 9)));
            }
            return _specifiedList.Except(existList).ToList();
        }
        bool IsValidPdf(string path)
        {
            bool isValid = true;
            try
            {
                PdfDocument inputDocument = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                PdfPage page = inputDocument.Pages[0];
            }
            catch
            {
                isValid = false;
            }
            return isValid;
        }
        void MergePdf(List<int> listToMerge,string path)
        {
            PdfDocument outputDocument = new PdfDocument();
            string fileBasePath = Path.GetDirectoryName(path).Replace(@"\", "\\") + "\\" + tempFolder + "\\temp_";
            foreach (int file_index in listToMerge)
            {
                PdfDocument inputDocument = PdfReader.Open(fileBasePath + file_index + ".pdf", PdfDocumentOpenMode.Import);
                PdfPage page = inputDocument.Pages[0];
                outputDocument.AddPage(page);
            }
            string filename = path.Replace(@"\", "\\");
            outputDocument.Save(filename);
            Process.Start(filename);
        }

        private void PathBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog
            {
                //saveFileDialog1.InitialDirectory = @"C:\";//与RestoreDirectory冲突
                Title = "保存pdf文件",
                //saveFileDialog1.CheckFileExists = true;
                //saveFileDialog1.CheckPathExists = true;
                //saveFileDialog1.AddExtension = true;
                //CreatePrompt = true,
                OverwritePrompt = true,
                FileName = "新建文件",
                DefaultExt = ".pdf",
                Filter = "PDF文件(*.pdf)|*.pdf|所有文件(*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };
            if (saveFileDialog1.ShowDialog() == true)
            {
                PathTextBox.Text = saveFileDialog1.FileName;
            }
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            //Calculate half of the offset to move the form

            if (sizeInfo.HeightChanged)
                Top += (sizeInfo.PreviousSize.Height - sizeInfo.NewSize.Height) / 2;

            if (sizeInfo.WidthChanged)
                Left += (sizeInfo.PreviousSize.Width - sizeInfo.NewSize.Width) / 2;
        }
    }
    public class DownloadItem : INotifyPropertyChanged
    {
        private string completion;
        private string visibility;
        public event PropertyChangedEventHandler PropertyChanged;
        public DownloadItem()
        {
        }
        public DownloadItem(string _title, string _completion)
        {
            DownloadItemTitle = _title;
            DownloadItemCompletion = _completion;
            DownloadItemVisibility = "Visible";
        }
        public string DownloadItemTitle { get; set; }
        public string DownloadItemCompletion
        {
            get { return completion; }
            set
            {
                completion = value;
                OnPropertyChanged("DownloadItemCompletion");
            }
        }
        public string DownloadItemVisibility
        {
            get { return visibility; }
            set
            {
                visibility = value;
                OnPropertyChanged("DownloadItemVisibility");
            }
        }
        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
