backupr
=======

Tool for backuping your photos to Flickr

Requirements
------------
You will need Windows and .NET Framework 4.0 (or higher)

In app.config you can configure:

**FileSpec** - specification of photos to search (for example *.jpg)

**IgnoreFilter** - pipe separated names of files and folders to ignore (for example .picasaoriginals)


Description
-----------
This utility was created because of lack **FAST** tool for uploading my 80GB archive of photos. All tools are calling Flickr API in sync mode, so they are slow.
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
