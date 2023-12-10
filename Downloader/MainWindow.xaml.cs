using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
using Path = System.IO.Path;

namespace Downloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? versionCode;
        private AssetsBundleInfo? info;
        private ObservableCollection<MainTreeViewItem> ItemsSource { get; } = new();
        private string assetsRootPath = "assets";
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            MainTreeView.ItemsSource = this.ItemsSource;
        }

        private async void UpdateInfoBtn_Click(object sender, RoutedEventArgs e)
        {
            var verCode = VersionCodeTB.Text;
            if (string.IsNullOrEmpty(verCode)) return;

            try
            {
                var http = new HttpClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "UnityPlayer/2021.3.22f1 (UnityWebRequest/1.0, libcurl/7.84.0-DEV)");
                using var s = await http.GetStreamAsync(verCode.BuildAssetPath("AssetBundleInfo"));
                var info = await AssetsBundleInfoParser.ParseAsync(s, verCode);
                var j = JsonSerializer.Serialize(info);
                await File.WriteAllTextAsync($"AssetBundleInfo_{info.Version}.json", j);
                MessageBox.Show($"更新完成，版本代码：\n{verCode}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void InfoFileCB_DropDownOpened(object sender, EventArgs e)
        {
            var cb = sender as ComboBox;
            var files = Directory.GetFiles(".").Where(x => System.IO.Path.GetFileName(x).StartsWith("AssetBundleInfo_")).ToList();
            cb!.ItemsSource = files;
        }

        private void InfoFileCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var path = InfoFileCB.SelectedItem as string;
            if (!File.Exists(path))
            {
                MessageBox.Show($"{path}不存在");
                return;
            }
            var info = JsonSerializer.Deserialize<AssetsBundleInfo>(File.ReadAllText(path));
            this.info = info;
            this.VersionTB.Text = info!.Version;
            this.versionCode = info!.VersionCode;
            InfoToTreeViewItems(info);
        }

        private static MainTreeViewItem GetOrCreateItem(ObservableCollection<MainTreeViewItem> source, string name)
        {
            if (source.Any(x => x.Name == name))
                return source.First(x => x.Name == name);
            else
            {
                var item = new MainTreeViewItem() // path item
                {
                    Name = name,
                    IsEntity = false,
                    Items = new ObservableCollection<MainTreeViewItem>(),
                    IsDownloaded = false
                };
                source.Add(item);
                return item;
            }
        }

        private bool CheckIsDownloaded(string name)
        {
            return File.Exists(System.IO.Path.Combine(assetsRootPath, name));
        }

        private void InfoToTreeViewItems(AssetsBundleInfo info) 
        {
            ItemsSource.Clear();
            foreach(var b in info.Bundles)
            {
                var names = b.Name.Split('/');
                if(names.Length == 1) // root item
                {
                    ItemsSource.Add(new()
                    {
                        Name = b.Name,
                        IsEntity = true,
                        Bundle = b,
                        IsDownloaded = CheckIsDownloaded(b.Name)
                    });
                    continue;
                }

                var currentDepth = 0;
                var currentItem = GetOrCreateItem(ItemsSource, names[currentDepth]);
                while (currentDepth + 2 != names.Length)
                {
                    currentDepth++;
                    currentItem = GetOrCreateItem(currentItem.Items!, names[currentDepth]);
                }
                currentItem.Items!.Add(new()
                {
                    Name = names[^1],
                    IsEntity = true,
                    Bundle = b,
                    IsDownloaded = CheckIsDownloaded(b.Name)
                });
            }
        }

        private async void ItemDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            var a = (e.Source as Button)!.DataContext as MainTreeViewItem;
            if (a!.IsDownloaded) return;
            var b = a!.Bundle!;
            var url = versionCode!.BuildAssetPath(b.Name);
            try
            {
                (e.Source as Button)!.IsEnabled = false;
                var http = new HttpClient();
                var resp = await http.GetStreamAsync(url);
                var path = Path.Combine(assetsRootPath, b.Name);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var file = File.OpenWrite(path);
                await resp.CopyToAsync(file);
                await file.DisposeAsync();
                await resp.DisposeAsync();

                var names = b.Name.Split('/');
                var currentItem = ItemsSource.Where(x => x.Name == names[0]).First();
                for (int i = 0; i < names.Length; i++)
                {
                    if (i == 0)
                        currentItem = ItemsSource.Where(x => x.Name == names[0]).First();
                    else
                        currentItem = currentItem.Items!.Where(x => x.Name == names[i]).First();
                }
                currentItem.IsDownloaded = true;
                if(BundleNameTB.Text == currentItem.Bundle!.Name)
                {
                    BundleFolderOpenBtn.Visibility = Visibility.Visible;
                }
            }catch(Exception ex)
            {
                MessageBox.Show(url + "\n" + ex.Message);
                (e.Source as Button)!.IsEnabled = true;
            }
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = MainTreeView.SelectedItem as MainTreeViewItem;
            if (!item!.IsEntity) return;
            var b = item.Bundle!;
            BundleNameTB.Text = b.Name;
            BundleHashTB.Text = b.Hash;
            BundleSizeTB.Text = b.Size.ToString();
            BundleCategoryTB.Text = b.Category.ToString();
            BundleFolderOpenBtn.Visibility = item.IsDownloaded ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BundleFolderOpenBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = BundleNameTB.Text;
            var path = Path.Combine(assetsRootPath, name);
            path = Path.GetFullPath(path);
            System.Diagnostics.Process.Start("Explorer", $"/select,{path}");
        }
    }
    internal class MainTreeViewItem:INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public bool IsEntity { get; set; }
        public Bundle? Bundle { get; set; }
        public ObservableCollection<MainTreeViewItem>? Items { get; set; }
        private bool isDownloaded;
        public bool IsDownloaded
        {
            get => isDownloaded;
            set
            {
                isDownloaded = value;
                PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IsDownloaded)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    public class BoolVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = (bool)value;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolDownloadedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = (bool)value;
            return b ? "✅" : "❌";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolCanDownloadConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = (bool)value;
            return !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
