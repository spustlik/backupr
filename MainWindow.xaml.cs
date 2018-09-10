using Backupr.Properties;
using FlickrNet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

namespace Backupr
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Flickr _flickrService;
        private FlickrAsync _flickrAsync;
        private List<Func<string, bool>> _disablers;
        private List<Photoset> _flickrPhotosets;
        private List<FlickrPhotoState> _flickrPhotos;
        private List<LocalPhotoState> _localPhotos;
        private Stopwatch stopwatch;
        public MainWindow()
        {
            Loaded += MainWindow_Loaded;
            InitializeComponent();
            DataContext = this;
            _flickrService = FlickrManager.GetAuthInstance();
            _flickrAsync = new FlickrAsync(_flickrService);
            stopwatch = new Stopwatch();
        }

        #region IsReady dependency property
        public bool IsReady
        {
            get { return (bool)GetValue(IsReadyProperty); }
            set { SetValue(IsReadyProperty, value); }
        }

        public static readonly DependencyProperty IsReadyProperty =
            DependencyProperty.Register("IsReady", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
        #endregion
          
        #region Elapsed dependency property
        public TimeSpan Elapsed
        {
            get { return (TimeSpan)GetValue(ElapsedProperty); }
            set { SetValue(ElapsedProperty, value); }
        }

        public static readonly DependencyProperty ElapsedProperty = 
            DependencyProperty.Register("Elapsed", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata());
        #endregion
          
        #region Estimated dependency property
        public TimeSpan Estimated
        {
            get { return (TimeSpan)GetValue(EstimatedProperty); }
            set { SetValue(EstimatedProperty, value); }
        }

        public static readonly DependencyProperty EstimatedProperty = 
            DependencyProperty.Register("Estimated", typeof(TimeSpan), typeof(MainWindow), new PropertyMetadata());
        #endregion

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AuthWindow();
            if (dlg.ShowDialog() == true)
            {
                IsReady = false;
                var authtoken = await _flickrAsync.AuthOAuthCheckToken();
                login.Text = "Logged in as " + authtoken.User.FullName;
                await GetFlickrState();
            }
        }

        private async void Folder_click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.InitialDirectory = Settings.Default.SourceFolder;
            dlg.CheckFileExists = false;
            dlg.FileName = "select folder";
            if (dlg.ShowDialog() == true)
            {
                Settings.Default.SourceFolder = System.IO.Path.GetDirectoryName(dlg.FileName);
                Settings.Default.Save();
                IsReady = false;
                var localTask = StartLocalFilesCrawling();
                await localTask;
                debug.AppendText($"{_localPhotos.Count} local photos found in {_localPhotos.Select(x => System.IO.Path.GetDirectoryName(x.FullName)).Distinct().Count()} folders\n");
                IsReady = true;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            progressText.Text = "Connecting...";
            if (Environment.GetCommandLineArgs().Select(a => a.ToLower()).Contains("disableinit"))
            {
                var token = await _flickrAsync.AuthOAuthCheckToken();
                login.Text = "Logged in as " + token.User.FullName;
                return;
            }
            stopwatch.Restart();
            IsReady = false;
            //START local folder crawling
            var localTask = StartLocalFilesCrawling();

            //AUTHENTICATE
            var authtoken = await _flickrAsync.AuthOAuthCheckToken();
            login.Text = "Logged in as " + authtoken.User.FullName;
            await GetFlickrState();

            progressText.Text = "Getting local files";
            //WAIT FOR FOLDER CRAWLING
            await localTask;
            debug.AppendText($"{_localPhotos.Count} local photos found in {_localPhotos.Select(x => System.IO.Path.GetDirectoryName(x.FullName)).Distinct().Count()} folders, total size: {BytesToString(_localPhotos.Sum(x => x.Length))}\n");
            stopwatch.Stop();
            progressText.Text = $"startup: {stopwatch.Elapsed}\n";
            IsReady = true;
        }

        public static string BytesToString(long value)
        {
            return ToDecimalUnits(value, 1024, 2000, "{0:###.#}B", "{0:###.#}KB", "{0:###.#}MB", "{0:###.#}GB", "{0:###.#}TB");
        }

        public static string ToDecimalUnits(double value, long rank, long treshold, params string[] units)
        {
            int i = 0;
            do
            {
                if (Math.Abs(value) < treshold || i == units.Length)
                    return String.Format(units[i], value);
                value = value / rank;
                i++;
            } while (true);
        }

        private Task StartLocalFilesCrawling()
        {
            folder.Text = Settings.Default.SourceFolder;
            _localPhotos = new List<LocalPhotoState>();
            var localTask = Task.Factory.StartNew(() =>
            {
                var dir = new DirectoryInfo(Settings.Default.SourceFolder);
                ReadFolderRecursivelly(_localPhotos, dir, new string[0]);
            });

            return localTask;
        }

        private async Task GetFlickrState()
        {
            progressText.Text = "Getting flickr status";
            //GET SETS
            _flickrPhotosets = (await _flickrAsync.PhotosetsGetList()).ToList();
            //GET PHOTOS in SETS
            progress.Maximum = _flickrPhotosets.Count;
            progress.Value = 0;

            var photosInSet = await RunMultiTasks(20, _flickrPhotosets.Count, i => ReadPhotoSet(_flickrPhotosets[i]));

            var photos = new List<FlickrPhotoState>();
            foreach (var item in photosInSet)
            {
                photos.AddRange(item.Select(x => new FlickrPhotoState(x, item.PhotosetId)));
            }
            debug.AppendText($"Read {photos.Count} total photos\n");
            //GET REST PHOTOS
            var notinset = await _flickrAsync.PhotosGetNotInSet(0, 0);
            var getlist = Enumerable.Range(0, notinset.Pages + 1).Select(page => _flickrAsync.PhotosGetNotInSet(page, notinset.PerPage));
            {
                var result = await Task.WhenAll(getlist.ToArray());
                foreach (var item in result)
                {
                    photos.AddRange(item.Select(x => new FlickrPhotoState(x, photosetId: null)));
                }
            }
            //MERGE PHOTOSETS - but each photo can be only in one photoset, so this is for sure diong merge
            _flickrPhotos = new List<FlickrPhotoState>();
            foreach (var item in photos.GroupBy(p => p.PhotoId))
            {
                var p = item.First();
                foreach (var p2 in item.Skip(1))
                {
                    p.MergeFrom(p2);
                }
                _flickrPhotos.Add(p);
            }
            debug.AppendText($"{_flickrPhotos.Count} total photos ({_flickrPhotosets.Count} sets with {_flickrPhotosets.Sum(_s => _s.NumberOfPhotos)} photos, {notinset.Total} photos not in set)\n");
            progressText.Text = null;
        }

        private async Task<List<TR>> RunMultiTasks<TR>(int paralel, int count, Func<int,Task<TR>> factory)
        {
            var result = new List<TR>();
            var tasks = new List<Task<TR>>();
            for (int i = 0; i < count; i++)
            {
                if (tasks.Count > paralel)
                {
                    await Task.WhenAny(tasks);
                    foreach (var t in tasks.Where(x => x.IsCompleted).ToArray())
                    {
                        result.Add(t.Result);
                        tasks.Remove(t);
                    }
                }
                tasks.Add(factory(i));
            }
            result.AddRange(await Task.WhenAll(tasks));
            return result;
        }

        private async Task<PhotosetPhotoCollection> ReadPhotoSet(Photoset photoset)
        {
            var r = await _flickrAsync.PhotosetsGetPhotos(photoset.PhotosetId);
            ReportProgress(photoset.Title, r);
            return r;
        }

        private void ReadFolderRecursivelly(List<LocalPhotoState> localPhotos, DirectoryInfo dir, IEnumerable<string> folders)
        {
            var filters =  Settings.Default.FileSpec.Split(',');

            var files = new List<FileInfo>();
            foreach (var filter in filters)
            {
                files.AddRange(dir.GetFiles(filter.Trim(), SearchOption.TopDirectoryOnly));
            }
            localPhotos.AddRange(files.Where(f => IsNotFilteredOut(f.FullName)).Select(f => new LocalPhotoState(f.FullName, f.Length, f.CreationTimeUtc, folders)));
            foreach (var subdir in dir.GetDirectories().Where(f => IsNotFilteredOut(f.FullName)))
            {
                ReadFolderRecursivelly(localPhotos, subdir, folders.Concat(new[] { subdir.Name }));
            }
        }


        private bool IsNotFilteredOut(string fullName)
        {
            if (_disablers == null)
            {
                _disablers = new List<Func<string, bool>>();
                var filters = Settings.Default.IgnoreFilter.Split(',');
                foreach (var filter in filters)
                {
                    //TODO: regex with * and ?, maybe folders "\" and "/", folder1/**/folder2
                    _disablers.Add(x => x == filter);
                }
            }
            var name = System.IO.Path.GetFileName(fullName).ToLower();
            foreach (var disabler in _disablers)
            {
                if (disabler(name))
                    return false;
            }
            return true;
        }

        private T ReportProgress<T>(string text, T result)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                progress.Value++;
                var estimateTotal = TimeSpan.FromMilliseconds(stopwatch.Elapsed.TotalMilliseconds * progress.Maximum / progress.Value);
                progressText.Text = $"{text} ({progress.Value} of {progress.Maximum})";
                Elapsed = stopwatch.Elapsed;
                Estimated = estimateTotal - Elapsed;
            }));
            return result;
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            stopwatch.Restart();
            IsReady = false;
            //COMPARE LOCAL AND FLICKR PHOTOS
            var titleIndex = _flickrPhotos.GroupBy(x => getFlickPathNormalized(x)).ToDictionary(x => x.Key);

            _localPhotos.RemoveAll(x => x.Folders.Count == 0);

            foreach (var localPhoto in _localPhotos.ToArray())
            {
                var path = getLocalPathNormalized(localPhoto);
                if (titleIndex.TryGetValue(path, out var found))
                {
                    //if (found.Any(x=>x.GetSetsAndTitle()==localPhoto.GetFoldersAndTitle()))
                    //TODO: detection of collisions
                    _localPhotos.Remove(localPhoto);
                }
            }

            //UPLOAD DIFFS
            debug.AppendText($"{_localPhotos.Count} photos will be uploaded ({Settings.Default.ParallelUploads} in parallel), total size:{BytesToString(_localPhotos.Sum(x => x.Length))}\n");
            progress.Maximum = _localPhotos.Count;
            progress.Value = 0;
            progressText.Text = "Uploading...";
            stopwatch.Start();

            //var cancellation = new CancellationTokenSource();
            //"PIPING" n parallel uploads
            //?TODO: replace with RunMultiTasks
            var todo = _localPhotos.ToList();
            var uploadingTasks = new List<Task>();
            while (todo.Count > 0 || uploadingTasks.Count > 0)
            {
                int MAX = Settings.Default.ParallelUploads;
                var batch = todo.Take(MAX - uploadingTasks.Count).ToArray();
                if (batch.Count() > 0)
                    todo.RemoveRange(0, batch.Count());
                var uploads = batch.Select(x => UploadFile(x)).ToArray();
                uploadingTasks.AddRange(uploads);
                try
                {
                    var done = await Task.WhenAny(uploadingTasks.ToArray());
                    uploadingTasks.Remove(done);
                }
                catch (Exception ex)
                {
                    debug.AppendText($"ERROR:" + ex);
                    break;
                }
                //debug.AppendText(done.Result + " uploaded\n");
            }
            /*
            foreach (var page in todo.GetPaged(Settings.Default.ParallelUploads))
            {
                var uploadTasks = page
                    .Select(async x =>
                    {
                        var t = await _flickrAsync.UploadPicture(x.GetUploadSource());
                        ReportProgress(x.ToString(), t);
                    }).ToArray();
                await Task.WhenAll(uploadTasks);
            }
            */

            //TODO: sync debug.AppendText("Adding photos to sets\n");

            await GetFlickrState();

            debug.AppendText("WORK DONE\n");
            IsReady = true;
        }

        private async Task<string> UploadFile(LocalPhotoState x)
        {
            try
            {
                //there is problem in flickr lib, that if it throws error (for example too many connections to server)
                // it exists whole application, because it is in some threadpool-thread and it is not well error-prove
                // :( probably can be solved by rewriting myself
                var t = await _flickrAsync.UploadPicture(x.GetUploadSource());
                ReportProgress(x.ToString(), t);
                return t;
            }
            catch(Exception e)
            {
                debug.AppendText($"ERROR:{e}");
                return null;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DeleteWindow();
            dlg.Owner = this;
            if (dlg.ShowDialog() != true)
                return;
            stopwatch.Restart();
            IsReady = false;
            if (dlg.DeletePhotosets)
            {
                progressText.Text = "DELETING photosets";
                stopwatch.Restart();
                progress.Maximum = _flickrPhotosets.Count;
                progress.Value = 0;
                {
                    var todo = _flickrPhotosets.Select(set => _flickrAsync.PhotosetsDelete(set.PhotosetId).ContinueWith(t => ReportProgress(set.Title, set.PhotosetId))).ToArray();
                    await Task.WhenAll(todo);
                    debug.AppendText($"{_flickrPhotosets.Count} photosets DELETED\n");
                }
            }

            if (dlg.DeletePhotos)
            {
                progressText.Text = "DELETING photos";
                stopwatch.Restart();
                progress.Value = 0;
                progress.Maximum = _flickrPhotos.Count;
                {
                    var todo = _flickrPhotos.Select(p => _flickrAsync.PhotoDelete(p.PhotoId).ContinueWith(t => ReportProgress(p.Title, p.PhotoId))).ToArray();
                    await Task.WhenAll(todo);
                    debug.AppendText($"{_flickrPhotos.Count} photos DELETED\n");
                }
            }
            await GetFlickrState();
            IsReady = true;
        }

        private async void MakeSets_Click(object sender, RoutedEventArgs e)
        {
            stopwatch.Restart();
            IsReady = false;
            var sets = _flickrPhotos
                                .Where(x => x.OriginalLocation != null)
                                .GroupBy(x => PhotosetTitleFromLocationTag(x))
                                .Select(g => new { Title = g.Key, PhotoId = g.First().PhotoId })
                                .ToArray();

            var missing = sets
                .Where(s => !_flickrPhotosets.Any(s2 => s2.Title == s.Title))
                .Select(s => _flickrAsync.PhotosetsCreate(s.Title, s.PhotoId))
                .ToArray();
            progress.Maximum = missing.Length;
            progress.Value = 0;
            progressText.Text = $"Creating new {missing.Length} sets";
            var created = await Task.WhenAll(missing.ToArray());
            debug.AppendText($"Created {missing.Length} sets\n");

            await GetFlickrState();

            var photosNotInSet = _flickrPhotos
                .Where(p => String.IsNullOrEmpty(p.PhotosetId))
                .Where(p => p.OriginalLocation != null)
                .ToArray();
            debug.AppendText($"{photosNotInSet.Length} photos not in set will be added");
            progress.Maximum = photosNotInSet.Length;
            progress.Value = 0;
            var result = await RunMultiTasks(20, photosNotInSet.Length, i => AddPhotoToSet(photosNotInSet[i]));
            debug.AppendText($"{result.Sum()} Photos added to sets\n");
            await GetFlickrState();
            IsReady = true;
        }

        private async Task<int> AddPhotoToSet(FlickrPhotoState p)
        {
            var found = _flickrPhotosets.FirstOrDefault(s => s.Title == PhotosetTitleFromLocationTag(p));
            if (found == null)
                return 0;
            var t = await _flickrAsync.PhotosetsAddPhoto(found.PhotosetId, p.PhotoId);
            ReportProgress("", t);
            return 1;
        }

        private static string PhotosetTitleFromLocationTag(FlickrPhotoState x)
        {
            return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(x.OriginalLocation));
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            stopwatch.Restart();
            IsReady = false;
            await GetFlickrState();
            IsReady = true;
        }



        internal void AddError(System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {

            if (e.Exception is AggregateException)
            {
                var ae = (AggregateException)e.Exception;
                var s = ae.InnerExceptions.First().ToString();
                if (ae.InnerExceptions.Count > 0)
                {
                    s = "\n";
                    foreach (var item in ae.InnerExceptions.GroupBy(ie=>ie.Message))
                    {
                        s += $"  {item.Count()} error(s): {item.Key}\n";
                    }
                }
                debug.Text+=$"{ae.InnerExceptions.Count} errors: {s}\n";
                return;
            }
            debug.Text += e.Exception.ToString() + "\n";
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            File.WriteAllLines(System.IO.Path.Combine(path, "_localphotos.txt"), _localPhotos.Select(p => getLocalPathNormalized(p)).OrderBy(p => p).ToArray());
            File.WriteAllLines(System.IO.Path.Combine(path, "_flickrphotos.txt"), _flickrPhotos.Select(p => getFlickPathNormalized(p)).OrderBy(p => p).ToArray());
            MessageBox.Show($"_flickrPhotos.txt and _localPhotos.txt was written to {path}");

        }

        private static string getFlickPathNormalized(FlickrPhotoState p)
        {
            return p.OriginalLocation.ToLower().Replace("/","\\");
        }

        private static string getLocalPathNormalized(LocalPhotoState p)
        {
            var x = p.FullName;
            var sourcePath = Settings.Default.SourceFolder;
            if (!sourcePath.EndsWith("\\"))
            {
                sourcePath = sourcePath + "\\";
            }
            if (x.StartsWith(sourcePath, StringComparison.InvariantCultureIgnoreCase))
                x = x.Substring(sourcePath.Length);
            return x.Replace("/","\\").ToLower();
        }

        private async void OrderByDate_Click(object sender, RoutedEventArgs e)
        {
            stopwatch.Restart();
            IsReady = false;
            try
            {
                debug.AppendText($"Reordering {_flickrPhotosets.Count} sets\n");
                var ordered = _flickrPhotos.GroupBy(x => x.PhotosetId).OrderByDescending(g => g.Min(x => x.DateTaken)).Select(g => g.Key).ToArray();
                
                await _flickrAsync.PhotosetsReorder(ordered);
                debug.AppendText($"Reordering photos in all sets\n");
                var sets = _flickrPhotos.GroupBy(x => x.PhotosetId).ToArray();
                progress.Maximum = sets.Length;
                progress.Value = 0;
                await RunMultiTasks(20, sets.Length, i => ReorderPhotos(sets[i]));
                debug.AppendText($"Reordered\n");
                await GetFlickrState();
            }
            finally
            {
                IsReady = true;
            }
        }

        private async Task<Boolean> ReorderPhotos(IGrouping<string, FlickrPhotoState> set)
        {
            //todo: do not reorder if already in right order
            var photos = set.OrderBy(x => x.DateTaken).Select(x => x.PhotoId).ToArray();
            await _flickrAsync.PhotosetReorderPhotos(set.Key, photos);
            ReportProgress(set.Key, true);
            return true;
        }
    }
    
    
}
