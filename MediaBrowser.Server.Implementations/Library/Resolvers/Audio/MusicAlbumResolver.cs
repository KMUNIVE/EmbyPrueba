﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Naming.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Naming.Common;

namespace MediaBrowser.Server.Implementations.Library.Resolvers.Audio
{
    /// <summary>
    /// Class MusicAlbumResolver
    /// </summary>
    public class MusicAlbumResolver : ItemResolver<MusicAlbum>
    {
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;

        public MusicAlbumResolver(ILogger logger, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
        }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public override ResolverPriority Priority
        {
            get { return ResolverPriority.Third; } // we need to be ahead of the generic folder resolver but behind the movie one
        }

        /// <summary>
        /// Resolves the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>MusicAlbum.</returns>
        protected override MusicAlbum Resolve(ItemResolveArgs args)
        {
            if (!args.IsDirectory) return null;

            //Avoid mis-identifying top folders
            if (args.Parent == null) return null;
            if (args.Parent.IsRoot) return null;
            if (args.Parent is MusicAlbum) return null;

            // Optimization
            if (args.Parent is BoxSet || args.Parent is Series || args.Parent is Season)
            {
                return null;
            }

            var collectionType = args.GetCollectionType();

            var isMusicMediaFolder = string.Equals(collectionType, CollectionType.Music,
                StringComparison.OrdinalIgnoreCase);

            // If there's a collection type and it's not music, don't allow it.
            if (!isMusicMediaFolder)
            {
                return null;
            }

            return IsMusicAlbum(args) ? new MusicAlbum() : null;
        }


        /// <summary>
        /// Determine if the supplied file data points to a music album
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="directoryService">The directory service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <returns><c>true</c> if [is music album] [the specified data]; otherwise, <c>false</c>.</returns>
        public static bool IsMusicAlbum(string path, IDirectoryService directoryService, ILogger logger, IFileSystem fileSystem,
            ILibraryManager libraryManager)
        {
            return ContainsMusic(directoryService.GetFileSystemEntries(path), true, directoryService, logger, fileSystem, libraryManager);
        }

        /// <summary>
        /// Determine if the supplied resolve args should be considered a music album
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns><c>true</c> if [is music album] [the specified args]; otherwise, <c>false</c>.</returns>
        private bool IsMusicAlbum(ItemResolveArgs args)
        {
            // Args points to an album if parent is an Artist folder or it directly contains music
            if (args.IsDirectory)
            {
                //if (args.Parent is MusicArtist) return true;  //saves us from testing children twice
                if (ContainsMusic(args.FileSystemChildren, true, args.DirectoryService, _logger, _fileSystem, _libraryManager)) return true;
            }

            return false;
        }

        /// <summary>
        /// Determine if the supplied list contains what we should consider music
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="allowSubfolders">if set to <c>true</c> [allow subfolders].</param>
        /// <param name="directoryService">The directory service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <returns><c>true</c> if the specified list contains music; otherwise, <c>false</c>.</returns>
        private static bool ContainsMusic(IEnumerable<FileSystemInfo> list,
            bool allowSubfolders,
            IDirectoryService directoryService,
            ILogger logger,
            IFileSystem fileSystem,
            ILibraryManager libraryManager)
        {
            var discSubfolderCount = 0;

            foreach (var fileSystemInfo in list)
            {
                if ((fileSystemInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    if (allowSubfolders && IsAlbumSubfolder(fileSystemInfo, directoryService, logger, fileSystem, libraryManager))
                    {
                        discSubfolderCount++;
                    }
                }

                var fullName = fileSystemInfo.FullName;

                if (libraryManager.IsAudioFile(fullName))
                {
                    return true;
                }
            }

            return discSubfolderCount > 0;
        }

        private static bool IsAlbumSubfolder(FileSystemInfo directory, IDirectoryService directoryService, ILogger logger, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            var path = directory.FullName;

            if (IsMultiDiscFolder(path))
            {
                logger.Debug("Found multi-disc folder: " + path);

                return ContainsMusic(directoryService.GetFileSystemEntries(path), false, directoryService, logger, fileSystem, libraryManager);
            }

            return false;
        }

        public static bool IsMultiDiscFolder(string path)
        {
            return IsMultiDiscAlbumFolder(path);
        }

        /// <summary>
        /// Determines whether [is multi disc album folder] [the specified path].
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if [is multi disc album folder] [the specified path]; otherwise, <c>false</c>.</returns>
        private static bool IsMultiDiscAlbumFolder(string path)
        {
            var parser = new AlbumParser(new ExtendedNamingOptions(), new Naming.Logging.NullLogger());
            var result = parser.ParseMultiPart(path);

            return result.IsMultiPart;
        }
    }
}
