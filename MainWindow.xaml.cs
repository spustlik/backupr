﻿using Backupr.Properties;
using FlickrNet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private List<PhotoState> _flickrPhotos;
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
                debug.AppendText(String.Format("{0} local photos found in {1} folders\n", _localPhotos.Count, _localPhotos.Select(x => System.IO.Path.GetDirectoryName(x.FullName)).Distinct().Count()));
                IsReady = true;
            }
        }

        async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            progressText.Text = "Connecting...";
            stopwatch.Restart();
            IsReady = false;
            //START local folder crawling
            var localTask = StartLocalFilesCrawling();

            //AUTHENTICATE
            var authtoken = await _flickrAsync.AuthOAuthCheckToken();
            login.Text = "Logged in as " + authtoken.User.FullName;
            await GetFlickrState();

            //WAIT FOR FOLDER CRAWLING
            await localTask;
            debug.AppendText(String.Format("{0} local photos found in {1} folders\n", _localPhotos.Count, _localPhotos.Select(x => System.IO.Path.GetDirectoryName(x.FullName)).Distinct().Count()));
            stopwatch.Stop();
            debug.AppendText(String.Format("startup: {0}\n", stopwatch.Elapsed));
            IsReady = true;
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
            var tasks = _flickrPhotosets.Select(photoset => _flickrAsync.PhotosetsGetPhotos(photoset.PhotosetId).ContinueWith(t=>ReportProgress(photoset.Title, t.Result))).ToArray();
            //WAIT FOR ALL
            var photos = new List<PhotoState>();
            {
                var result = await Task.WhenAll(tasks);
                foreach (var item in result)
                {
                    photos.AddRange(item.Select(x => new PhotoState(x, item.PhotosetId)));
                }
            }
            //GET REST PHOTOS
            var notinset = await _flickrAsync.PhotosGetNotInSet(0, 0);
            var getlist = Enumerable.Range(0, notinset.Pages + 1).Select(page => _flickrAsync.PhotosGetNotInSet(page, notinset.PerPage));
            {
                var result = await Task.WhenAll(getlist.ToArray());
                foreach (var item in result)
                {
                    photos.AddRange(item.Select(x => new PhotoState(x)));
                }
            }
            //MERGE PHOTOSETS
            _flickrPhotos = new List<PhotoState>();
            foreach (var item in photos.GroupBy(p => p.PhotoId))
            {
                var p = item.First();
                foreach (var p2 in item.Skip(1))
                {
                    p.MergeFrom(p2);
                }
                _flickrPhotos.Add(p);
            }
            debug.AppendText(String.Format("{0} sets with {1} photos, {2} photos not in set\n", _flickrPhotosets.Count, _flickrPhotos.Count, notinset.Total));
            progressText.Text = null;
        }
        
        private void ReadFolderRecursivelly(List<LocalPhotoState> localPhotos, DirectoryInfo dir, IEnumerable<string> folders)
        {
            var files = dir.GetFiles(Settings.Default.FileSpec, SearchOption.TopDirectoryOnly);
            localPhotos.AddRange(files.Where(f => IsNotFilteredOut(f.FullName)).Select(f => new LocalPhotoState(f.FullName, folders)));
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
                var filters = Settings.Default.IgnoreFilter.Split('|');
                foreach (var filter in filters)
                {
                    //TODO: regex with * and ?, maybe folders "\" and "/"
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
                progressText.Text = String.Format("{2} ({0} of {1})", progress.Value, progress.Maximum, text);
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
            var titleIndex = _flickrPhotos.GroupBy(x => x.Title.ToLower()).ToDictionary(x => x.Key);

            _localPhotos.RemoveAll(x => x.Folders.Count == 0);

            foreach (var localPhoto in _localPhotos.ToArray())
            {
                IGrouping<string, PhotoState> found;
                if (titleIndex.TryGetValue(localPhoto.Name.ToLower(), out found))
                {
                    //if (found.Any(x=>x.GetSetsAndTitle()==localPhoto.GetFoldersAndTitle()))
                    //TODO: detection of collisions
                    _localPhotos.Remove(localPhoto);
                }
            }

            //UPLOAD DIFFS
            debug.AppendText(_localPhotos.Count + " photos will be uploaded\n");
            progress.Maximum = _localPhotos.Count;
            progress.Value = 0;
            progressText.Text = "Uploading...";
            stopwatch.Start();

            //var cancellation = new CancellationTokenSource();

            var todo = _localPhotos.ToList();
            var uploadingTasks = new List<Task>();
            while (todo.Count > 0 || uploadingTasks.Count > 0)
            {
                int MAX = 50;
                var batch = todo.Take(MAX - uploadingTasks.Count).ToArray();
                if (batch.Count() > 0)
                    todo.RemoveRange(0, batch.Count());
                var uploads = batch.Select(x => _flickrAsync
                    .UploadPicture(x.GetUploadSource())
                    .ContinueWith(t => ReportProgress(x.ToString(), t))
                    ).ToArray();
                uploadingTasks.AddRange(uploads);
                var done = await Task.WhenAny(uploadingTasks.ToArray());
                uploadingTasks.Remove(done);
                //debug.AppendText(done.Result + " uploaded\n");
            }
            
            //TODO: sync debug.AppendText("Adding photos to sets\n");

            await GetFlickrState();

            debug.AppendText("WORK DONE\n");
            IsReady = true;
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
                    debug.AppendText(String.Format("{0} photosets DELETED\n", _flickrPhotosets.Count));
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
                    debug.AppendText(String.Format("{0} photos DELETED\n", _flickrPhotos.Count));
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
            progressText.Text = String.Format("Creating new {0} sets", missing.Length);
            var created = await Task.WhenAll(missing.ToArray());
            debug.AppendText(String.Format("Created {0} sets\n", missing.Length));

            await GetFlickrState();

            var photosNotInSet = _flickrPhotos
                .Where(p => String.IsNullOrEmpty(p.PhotosetId))
                .Where(p => p.OriginalLocation != null);
            progress.Maximum = photosNotInSet.Count();
            progress.Value = 0;
            var adders = photosNotInSet.Select(p =>
            {
                var found = _flickrPhotosets.FirstOrDefault(s => s.Title == PhotosetTitleFromLocationTag(p));
                if (found != null)
                {
                    return (Task)_flickrAsync
                        .PhotosetsAddPhoto(found.PhotosetId, p.PhotoId)
                        .ContinueWith(t => ReportProgress("", t));
                }
                return Task.Factory.StartNew(() => new NoResponse());
            });
            progressText.Text = String.Format("Adding {0} photos to sets", adders.Count());
            await Task.WhenAll(adders.ToArray());
            debug.AppendText(String.Format("{0} photos added to sets\n", adders.Count()));
            await GetFlickrState();
            IsReady = true;
        }

        private static string PhotosetTitleFromLocationTag(PhotoState x)
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


    }
    class LocalPhotoState : IUploadPictureParams
    {
        public LocalPhotoState()
        {
            Folders = new HashSet<string>();
        }

        public LocalPhotoState(string fileName, IEnumerable<string> folders) : this()
        {
            Name = System.IO.Path.GetFileName(fileName);
            FullName = fileName;
            foreach (var item in folders)
            {
                Folders.Add(item);
            }
        }
        public string Name { get; set; }
        public string FullName { get; set; }
        public HashSet<string> Folders { get; private set; }

        internal IUploadPictureParams GetUploadSource()
        {
            return this;
        }

        public override string ToString()
        {
            return String.Join("/", Folders) + "/" + Name;
        }

        #region IUploadPictureParams
        string IUploadPictureParams.FileName
        {
            get { return Name; }
        }

        string IUploadPictureParams.Title
        {
            get { return Name; }
        }

        string IUploadPictureParams.Description
        {
            get { return String.Join("/", Folders) + "/" + Name; }
        }

        string IUploadPictureParams.Tags
        {
            get { return String.Join(",", new[] { "#/" + String.Join("/", Folders) }.Concat(Folders)); }
        }

        bool IUploadPictureParams.IsPublic
        {
            get { return false; }
        }

        bool IUploadPictureParams.IsFamily
        {
            get { return false; }
        }

        bool IUploadPictureParams.IsFriend
        {
            get { return false; }
        }

        ContentType IUploadPictureParams.ContentType
        {
            get { return ContentType.Photo; }
        }

        SafetyLevel IUploadPictureParams.SafetyLevel
        {
            get { return SafetyLevel.None; }
        }

        HiddenFromSearch IUploadPictureParams.HiddenFromSearch
        {
            get { return HiddenFromSearch.None; }
        }

        Stream IUploadPictureParams.GetStream()
        {
            return new FileStream(FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        #endregion
    }
    class PhotoState
    {
        public PhotoState()
        {
            Tags = new HashSet<string>();
        }
        public PhotoState(Photo p)
            : this()
        {
            PhotoId = p.PhotoId;
            Title = p.Title;
            Description = p.Description;
            Tags = new HashSet<string>(p.Tags);
        }

        public PhotoState(Photo p, string photosetId) : this(p)
        {
            PhotosetId = photosetId;
        }
        public string PhotoId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string PhotosetId { get; set; }
        public HashSet<string> Tags { get; private set; }
        public string OriginalLocation
        {
            get
            {
                //tags are somehow mangled by server... return Tags.FirstOrDefault(t => t.StartsWith("#"));
                return Description;
            }
        }

        public void MergeFrom(PhotoState p2)
        {
            if (String.IsNullOrEmpty(Title))
                Title = p2.Title;
            foreach (var tag in p2.Tags)
            {
                Tags.Add(tag);
            }
            PhotosetId = p2.PhotosetId;
        }

    }
}
