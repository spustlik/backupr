using FlickrNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backupr
{
    class LocalPhotoState : IUploadPictureParams
    {
        public LocalPhotoState()
        {
            Folders = new HashSet<string>();
        }

        public LocalPhotoState(string fileName, long length, DateTime dateTime, IEnumerable<string> folders)
            : this()
        {
            Name = System.IO.Path.GetFileName(fileName);
            FullName = fileName;
            DateTime = dateTime;
            Length = length;
            foreach (var item in folders)
            {
                Folders.Add(item);
            }
        }
        public string Name { get; set; }
        public string FullName { get; set; }
        public DateTime DateTime { get; set; }
        public long Length { get; set; }

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
}
