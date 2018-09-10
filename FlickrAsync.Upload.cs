using FlickrNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Backupr
{
    partial class FlickrAsync
    {
        private void UploadDataAsync(Stream imageStream, string fileName, string contentType, Uri uploadUri, Dictionary<string, string> parameters, Action<FlickrResult<string>> callback)
        {
            string boundary = "FLICKR_MIME_" + DateTime.Now.ToString("yyyyMMddhhmmss", System.Globalization.DateTimeFormatInfo.InvariantInfo);

            string authHeader = FlickrResponder.OAuthCalculateAuthHeader(parameters);

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(uploadUri);
            req.Method = "POST";
            req.ContentType = "multipart/form-data; boundary=" + boundary;
            if (!String.IsNullOrEmpty(authHeader))
            {
                req.Headers["Authorization"] = authHeader;
            }

            req.BeginGetRequestStream(
                r =>
                {
                    using (Stream reqStream = req.EndGetRequestStream(r))
                    {
                        WriteUploadDataPrologueToStream(reqStream, fileName, contentType, parameters, boundary);
                        if (imageStream.CanSeek)
                        {
                            var buffer = new byte[1024 * 4]; //4kB buffer
                            var totalLength = imageStream.Length;
                            int uploadedSoFar = 0;
                            while (uploadedSoFar < totalLength)
                            {
                                var readed = imageStream.Read(buffer, uploadedSoFar, buffer.Length);
                                reqStream.Write(buffer, 0, readed);
                                uploadedSoFar += readed;
                                if (OnUploadProgress != null)
                                {
                                    UploadProgressEventArgs args = new UploadProgressEventArgs(uploadedSoFar, totalLength);
                                    OnUploadProgress(this, args);
                                }
                            }
                        }
                        else
                        {
                            //maybe it is better to not to report progress ?
                            imageStream.CopyTo(reqStream);
                        }
                        imageStream.CopyTo(reqStream);
                        WriteUploadDataEpilogueToStream(reqStream, boundary);
                        reqStream.Close();
                    }

                    req.BeginGetResponse(
                        r2 =>
                        {
                            FlickrResult<string> result = new FlickrResult<string>();

                            try
                            {
                                WebResponse res = req.EndGetResponse(r2);
                                StreamReader sr = new StreamReader(res.GetResponseStream());
                                string responseXml = sr.ReadToEnd();
                                sr.Close();

                                XmlReaderSettings settings = new XmlReaderSettings();
                                settings.IgnoreWhitespace = true;
                                XmlReader reader = XmlReader.Create(new StringReader(responseXml), settings);

                                if (!reader.ReadToDescendant("rsp"))
                                {
                                    throw new XmlException("Unable to find response element 'rsp' in Flickr response");
                                }
                                while (reader.MoveToNextAttribute())
                                {
                                    if (reader.LocalName == "stat" && reader.Value == "fail")
                                        throw ExceptionHandler.CreateResponseException(reader);
                                    continue;
                                }

                                reader.MoveToElement();
                                reader.Read();

                                UnknownResponse t = new UnknownResponse();
                                ((IFlickrParsable)t).Load(reader);
                                result.Result = t.GetElementValue("photoid");
                                result.HasError = false;
                            }
                            catch (Exception ex)
                            {
                                if (ex is WebException)
                                {
                                    OAuthException oauthEx = new OAuthException(ex);
                                    if (String.IsNullOrEmpty(oauthEx.Message))
                                        result.Error = ex;
                                    else
                                        result.Error = oauthEx;
                                }
                                else
                                {
                                    result.Error = ex;
                                }
                            }

                            callback(result);

                        },
                        this);
                },
                this);

        }

        private void WriteUploadDataPrologueToStream(Stream requestStream, string fileName, string contentType, Dictionary<string, string> parameters, string boundary)
        {
            var oAuth = parameters.ContainsKey("oauth_consumer_key");

            var keys = new string[parameters.Keys.Count];
            parameters.Keys.CopyTo(keys, 0);
            Array.Sort(keys);

            var hashStringBuilder = new StringBuilder(_flickrService.ApiSecret, 2 * 1024);
            using (var contentWriter = new StreamWriter(requestStream, Encoding.UTF8))
            {
                foreach (string key in keys)
                {

#if !SILVERLIGHT
                    // Silverlight < 5 doesn't support modification of the Authorization header, so all data must be sent in post body.
                    if (key.StartsWith("oauth")) continue;
#endif
                    hashStringBuilder.Append(key);
                    hashStringBuilder.Append(parameters[key]);
                    contentWriter.Write($"--{boundary}\r\n");
                    contentWriter.Write($"Content-Disposition: form-data; name=\"{key}\"\r\n");
                    contentWriter.Write($"\r\n");
                    contentWriter.Write($"{parameters[key]}\r\n");
                }

                if (!oAuth)
                {
                    contentWriter.Write($"--{boundary}\r\n");
                    contentWriter.Write($"Content-Disposition: form-data; name=\"api_sig\"\r\n");
                    contentWriter.Write($"\r\n");
                    contentWriter.Write($"{UtilityMethods.MD5Hash(hashStringBuilder.ToString())}\r\n");
                }

                // Photo
                contentWriter.Write($"--{boundary}\r\n");
                contentWriter.Write($"Content-Disposition: form-data; name=\"photo\"; filename=\"{Path.GetFileName(fileName)}\"\r\n");
                contentWriter.Write($"Content-Type: {contentType}\r\n");
                contentWriter.Write($"\r\n");
            }
        }

        private void WriteUploadDataEpilogueToStream(Stream requestStream, string boundary)
        {
            using (var contentWriter = new StreamWriter(requestStream, Encoding.UTF8))
            {
                contentWriter.Write($"\r\n--{boundary}--\r\n");
            }
        }

        class UtilityMethods
        {

            internal static string MD5Hash(string p)
            {
                throw new NotImplementedException();
            }
        }
    }
}
