backupr
=======

Tool for backuping your photos to Flickr

Requirements
------------
You will need Windows and .NET Framework 4.0 (or higher)

In app.config you can configure:

**FileSpec** - specification of photos to search, separated by comma (",") (for example *.jpg,*.mov)

**IgnoreFilter** - specification of files and folders to ignore, separated by comma (",") (for example .picasaoriginals,Backup)

**ParallelUploads** - number of maximum paralel TASKS uploading file (default = 50). Due of implementation of Flickr.NET library, content of uploading file is stored in memory in the base64 form (so it is ~133% larger than original file). This can be too much for your computer memory.

Description
-----------
This utility was created because of lack of **FAST** tool for uploading my 80GB archive of photos. All tools are calling Flickr API in synchronous way, so they are slow.
This program is written using TASK API and ASYNC, so some things are then happening and I dont know how to solve them :-)


Quick help
----------
When launched, program try to connect to flickr. If App is not yet authorized, authorization process will occur and authorization token is stored in settings.
You must restart it after authorization.
Then program loads list of photos on your flickr and in your local folder.

**Refresh** - will refresh list of photos on flickr.

**Upload** - will upload all local photos not on flickr. Description is used for storing original (relative) path. Each folder on the path is added to Tags.

**Make sets** - will create photo sets for each folder and adds photos to it.

**Delete** - asks for what to delete and permanently deletes it from flickr.

**Export list** - exports list of local files and flickr files to text files

Things to do
------------
 
 * support of video files (you can speficy *.mov, but will they act in Flickr photos api ?)
 * Download of files (paralel, of course)
 * IgnoreFilter wildchars (Backup* for example)
 * service / resident mode
