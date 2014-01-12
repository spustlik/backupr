using FlickrNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backupr
{
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

        public PhotoState(Photo p, string photosetId)
            : this(p)
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
