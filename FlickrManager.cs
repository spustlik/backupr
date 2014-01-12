using System;
using System.Net;
using System.Windows;
using FlickrNet;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;

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


}
