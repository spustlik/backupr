using System;
using System.Net;
using System.Windows;
using FlickrNet;
using System.Threading.Tasks;
using System.IO;

namespace Backupr
{
    public class FlickrManager
    {
        public const string ApiKey = "ef1a18ae69ef655405c304e4bd6d1c50";
        public const string SharedSecret = "ba2d470e7339f83b";

        public static Flickr GetInstance()
        {
            return new Flickr(ApiKey, SharedSecret);
        }

        public static Flickr GetAuthInstance()
        {
            var f = GetInstance();
            f.OAuthAccessToken = OAuthToken.Token;
            f.OAuthAccessTokenSecret = OAuthToken.TokenSecret;
            return f;
        }

        public static OAuthAccessToken OAuthToken
        {
            get
            {
                return Properties.Settings.Default.OAuthToken;
            }
            set
            {
                Properties.Settings.Default.OAuthToken = value;
                Properties.Settings.Default.Save();
            }
        }

    }

    public class FlickrAsync
    {
        private Flickr _flickrService;
        public FlickrAsync(Flickr flickrService)
        {
            _flickrService = flickrService;
        }
        //TaskCompletionSource<int>

        public Task<Auth> AuthOAuthCheckToken()
        {
            var tcs = new TaskCompletionSource<Auth>();
            _flickrService.AuthOAuthCheckTokenAsync(args =>
                {
                    setResult(tcs, args);
                });
            return tcs.Task;
        }

        private static void setResult<T>(TaskCompletionSource<T> tcs, FlickrResult<T> args)
        {
            if (args.HasError)
            {
                tcs.SetException(args.Error);
                return;
            }
            if (tcs.Task.IsCanceled)
            {
                tcs.SetCanceled();
                return;
            }
            try
            {
                tcs.SetResult(args.Result);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        }

        public Task<PhotosetCollection> PhotosetsGetList()
        {
            var tcs = new TaskCompletionSource<PhotosetCollection>();
            _flickrService.PhotosetsGetListAsync(args =>
                {
                    setResult(tcs, args);
                });
            return tcs.Task;
        }

        public Task<PhotosetPhotoCollection> PhotosetsGetPhotos(string photosetId)
        {
            var tcs = new TaskCompletionSource<PhotosetPhotoCollection>();
            _flickrService.PhotosetsGetPhotosAsync(photosetId, PhotoSearchExtras.Tags | PhotoSearchExtras.Description, args =>
                {
                    setResult(tcs, args);
                });
            return tcs.Task;
        }

        public Task<PhotoCollection> PhotosGetNotInSet(int page, int perPage)
        {
            var tcs = new TaskCompletionSource<PhotoCollection>();
            _flickrService.PhotosGetNotInSetAsync(page, perPage, PhotoSearchExtras.Tags | PhotoSearchExtras.Description, args =>
                {
                    setResult(tcs, args);
                });
            return tcs.Task;
        }

        public Task<NoResponse> PhotosetsAddPhoto(string photosetId, string photoId)
        {
            var tcs = new TaskCompletionSource<NoResponse>();

            _flickrService.PhotosetsAddPhotoAsync(photosetId, photoId, args =>
                {
                    setResult(tcs, args);
                });
            return tcs.Task;
        }

        public Task<NoResponse> PhotosetsDelete(string photosetId)
        {
            var tcs = new TaskCompletionSource<NoResponse>();

            _flickrService.PhotosetsDeleteAsync(photosetId, args =>
            {
                setResult(tcs, args);
            });
            return tcs.Task;
        }

        public Task<NoResponse> PhotoDelete(string photoId)
        {
            var tcs = new TaskCompletionSource<NoResponse>();

            _flickrService.PhotosDeleteAsync(photoId, args =>
            {
                setResult(tcs, args);
            });
            return tcs.Task;
        }

        public Task<Photoset> PhotosetsCreate(string title, string primaryPhotoId)
        {
            var tcs = new TaskCompletionSource<Photoset>();

            _flickrService.PhotosetsCreateAsync(title, primaryPhotoId, args =>
            {
                setResult(tcs, args);
            });
            return tcs.Task;
        }

        public Task<string> UploadPicture(IUploadPictureParams upload)
        {
            var tcs = new TaskCompletionSource<string>();
            var s = upload.GetStream();
            _flickrService.UploadPictureAsync(s, upload.FileName, upload.Title, upload.Description, upload.Tags, upload.IsPublic, upload.IsFamily, upload.IsFriend, upload.ContentType, upload.SafetyLevel, upload.HiddenFromSearch,
                args =>
                {
                    setResult(tcs, args);
                    s.Dispose();
                    s = null;
                });
            return tcs.Task;
        }

        
    }

    public interface IUploadPictureParams
    {
        string FileName { get; }
        string Title { get; }
        string Description { get; }
        string Tags { get; }
        bool IsPublic { get; }
        bool IsFamily { get; }
        bool IsFriend { get; }
        ContentType ContentType { get; }
        SafetyLevel SafetyLevel { get; }
        HiddenFromSearch HiddenFromSearch { get; }

        Stream GetStream();
    }
}
