using FlickrNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backupr
{
    public partial class FlickrAsync
    {
        private Flickr _flickrService;
        public FlickrAsync(Flickr flickrService)
        {
            _flickrService = flickrService;
        }
        //TaskCompletionSource<int>

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

        public class Args<TResult>
        {
            private TaskCompletionSource<TResult> tcs;

            public Args(TaskCompletionSource<TResult> tcs, Flickr flickr)
            {
                this.tcs = tcs;
                this.Service = flickr;
            }
            public Flickr Service { get; private set; }

            public void SetResult(FlickrResult<TResult> args)
            {
                setResult(tcs, args);
            }
        }

        private Task<TResult> CallSvc<TResult>(Action<Args<TResult>> caller)
        {
            var task = callSvcOnce<TResult>(caller);
            return task.ContinueWith<TResult>(t => t.IsFaulted ? callSvcOnce<TResult>(caller).Result : t.Result);
        }

        private Task<TResult> callSvcOnce<TResult>(Action<Args<TResult>> caller)
        {
            var tcs = new TaskCompletionSource<TResult>();
            caller(new Args<TResult>(tcs, _flickrService));
            return tcs.Task;
        }

        public Task<Auth> AuthOAuthCheckToken()
        {
            return CallSvc<Auth>(a => a.Service.AuthOAuthCheckTokenAsync(a.SetResult));
        }

        public Task<PhotosetCollection> PhotosetsGetList()
        {
            return CallSvc<PhotosetCollection>(a => a.Service.PhotosetsGetListAsync(a.SetResult));
        }

        public Task<PhotosetPhotoCollection> PhotosetsGetPhotos(string photosetId)
        {
            return CallSvc<PhotosetPhotoCollection>(a => a.Service.PhotosetsGetPhotosAsync(photosetId, PhotoSearchExtras.Tags | PhotoSearchExtras.Description, a.SetResult));
        }

        public Task<PhotoCollection> PhotosGetNotInSet(int page, int perPage)
        {
            return CallSvc<PhotoCollection>(a => a.Service.PhotosGetNotInSetAsync(page, perPage, PhotoSearchExtras.Tags | PhotoSearchExtras.Description, a.SetResult));
        }

        public Task<NoResponse> PhotosetsAddPhoto(string photosetId, string photoId)
        {
            return CallSvc<NoResponse>(a => a.Service.PhotosetsAddPhotoAsync(photosetId, photoId, a.SetResult));
        }

        public Task<NoResponse> PhotosetsDelete(string photosetId)
        {
            return CallSvc<NoResponse>(a => a.Service.PhotosetsDeleteAsync(photosetId, a.SetResult));
        }

        public Task<NoResponse> PhotoDelete(string photoId)
        {
            return CallSvc<NoResponse>(a => a.Service.PhotosDeleteAsync(photoId, a.SetResult));
        }

        public Task<Photoset> PhotosetsCreate(string title, string primaryPhotoId)
        {
            return CallSvc<Photoset>(a => a.Service.PhotosetsCreateAsync(title, primaryPhotoId, a.SetResult));
        }

        public Task<string> UploadPicture(IUploadPictureParams upload)
        {
            var s = upload.GetStream();
            return CallSvc<string>(a => 
                a.Service.UploadPictureAsync(s, upload.FileName, upload.Title, upload.Description, upload.Tags, upload.IsPublic, upload.IsFamily, upload.IsFriend, upload.ContentType, upload.SafetyLevel, upload.HiddenFromSearch,
                args =>
                {
                    a.SetResult(args);
                    s.Dispose();
                    s = null;
                }));
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
