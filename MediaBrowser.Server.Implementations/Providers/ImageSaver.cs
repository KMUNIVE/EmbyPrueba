﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.IO;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Providers
{
    /// <summary>
    /// Class ImageSaver
    /// </summary>
    public class ImageSaver
    {
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        /// <summary>
        /// The _config
        /// </summary>
        private readonly IServerConfigurationManager _config;

        /// <summary>
        /// The remote image cache
        /// </summary>
        private readonly FileSystemRepository _remoteImageCache;
        /// <summary>
        /// The _directory watchers
        /// </summary>
        private readonly IDirectoryWatchers _directoryWatchers;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSaver"/> class.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="directoryWatchers">The directory watchers.</param>
        public ImageSaver(IServerConfigurationManager config, IDirectoryWatchers directoryWatchers, IFileSystem fileSystem, ILogger logger)
        {
            _config = config;
            _directoryWatchers = directoryWatchers;
            _fileSystem = fileSystem;
            _logger = logger;
            _remoteImageCache = new FileSystemRepository(config.ApplicationPaths.DownloadedImagesDataPath);
        }

        /// <summary>
        /// Saves the image.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="source">The source.</param>
        /// <param name="mimeType">Type of the MIME.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="sourceUrl">The source URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">mimeType</exception>
        public async Task SaveImage(BaseItem item, Stream source, string mimeType, ImageType type, int? imageIndex, string sourceUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                throw new ArgumentNullException("mimeType");
            }

            var saveLocally = _config.Configuration.SaveLocalMeta && item.Parent != null && !(item is Audio);

            if (item is IItemByName || item is User)
            {
                saveLocally = true;
            }

            if (type != ImageType.Primary && item is Episode)
            {
                saveLocally = false;
            }

            var locationType = item.LocationType;
            if (locationType == LocationType.Remote || locationType == LocationType.Virtual)
            {
                saveLocally = false;

                var season = item as Season;

                // If season is virtual under a physical series, save locally if using compatible convention
                if (season != null && _config.Configuration.ImageSavingConvention == ImageSavingConvention.Compatible)
                {
                    var series = season.Series;

                    if (series != null)
                    {
                        var seriesLocationType = series.LocationType;
                        if (seriesLocationType == LocationType.FileSystem || seriesLocationType == LocationType.Offline)
                        {
                            saveLocally = true;
                        }
                    }
                }
            }

            if (type == ImageType.Backdrop && imageIndex == null)
            {
                imageIndex = item.BackdropImagePaths.Count;
            }
            else if (type == ImageType.Screenshot && imageIndex == null)
            {
                var hasScreenshots = (IHasScreenshots)item;
                imageIndex = hasScreenshots.ScreenshotImagePaths.Count;
            }

            var index = imageIndex ?? 0;

            var paths = GetSavePaths(item, type, imageIndex, mimeType, saveLocally);

            // If there are more than one output paths, the stream will need to be seekable
            if (paths.Length > 1 && !source.CanSeek)
            {
                var memoryStream = new MemoryStream();
                using (source)
                {
                    await source.CopyToAsync(memoryStream).ConfigureAwait(false);
                }
                memoryStream.Position = 0;
                source = memoryStream;
            }

            var currentPath = GetCurrentImagePath(item, type, index);

            using (source)
            {
                var isFirst = true;

                foreach (var path in paths)
                {
                    // Seek back to the beginning
                    if (!isFirst)
                    {
                        source.Position = 0;
                    }

                    await SaveImageToLocation(source, path, cancellationToken).ConfigureAwait(false);

                    isFirst = false;
                }
            }

            // Set the path into the item
            SetImagePath(item, type, imageIndex, paths[0], sourceUrl);

            // Delete the current path
            if (!string.IsNullOrEmpty(currentPath) && !paths.Contains(currentPath, StringComparer.OrdinalIgnoreCase))
            {
                _directoryWatchers.TemporarilyIgnore(currentPath);

                try
                {
                    var currentFile = new FileInfo(currentPath);

                    // This will fail if the file is hidden
                    if (currentFile.Exists)
                    {
                        if ((currentFile.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        {
                            currentFile.Attributes &= ~FileAttributes.Hidden;
                        }

                        currentFile.Delete();
                    }
                }
                finally
                {
                    _directoryWatchers.RemoveTempIgnore(currentPath);
                }
            }
        }

        /// <summary>
        /// Saves the image to location.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="path">The path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task SaveImageToLocation(Stream source, string path, CancellationToken cancellationToken)
        {
            _logger.Debug("Saving image to {0}", path);

            var parentFolder = Path.GetDirectoryName(path);

            _directoryWatchers.TemporarilyIgnore(path);
            _directoryWatchers.TemporarilyIgnore(parentFolder);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // If the file is currently hidden we'll have to remove that or the save will fail
                var file = new FileInfo(path);

                // This will fail if the file is hidden
                if (file.Exists)
                {
                    if ((file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        file.Attributes &= ~FileAttributes.Hidden;
                    }
                }

                using (var fs = _fileSystem.GetFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, true))
                {
                    await source.CopyToAsync(fs, StreamDefaults.DefaultCopyToBufferSize, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _directoryWatchers.RemoveTempIgnore(path);
                _directoryWatchers.RemoveTempIgnore(parentFolder);
            }
        }

        /// <summary>
        /// Gets the save paths.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="mimeType">Type of the MIME.</param>
        /// <param name="saveLocally">if set to <c>true</c> [save locally].</param>
        /// <returns>IEnumerable{System.String}.</returns>
        private string[] GetSavePaths(BaseItem item, ImageType type, int? imageIndex, string mimeType, bool saveLocally)
        {
            if (_config.Configuration.ImageSavingConvention == ImageSavingConvention.Legacy || !saveLocally)
            {
                return new[] { GetStandardSavePath(item, type, imageIndex, mimeType, saveLocally) };
            }

            return GetCompatibleSavePaths(item, type, imageIndex, mimeType);
        }

        /// <summary>
        /// Gets the current image path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// imageIndex
        /// or
        /// imageIndex
        /// </exception>
        private string GetCurrentImagePath(IHasImages item, ImageType type, int imageIndex)
        {
            return item.GetImagePath(type, imageIndex);
        }

        /// <summary>
        /// Sets the image path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="path">The path.</param>
        /// <param name="sourceUrl">The source URL.</param>
        /// <exception cref="System.ArgumentNullException">imageIndex
        /// or
        /// imageIndex</exception>
        private void SetImagePath(BaseItem item, ImageType type, int? imageIndex, string path, string sourceUrl)
        {
            switch (type)
            {
                case ImageType.Screenshot:

                    if (!imageIndex.HasValue)
                    {
                        throw new ArgumentNullException("imageIndex");
                    }

                    var hasScreenshots = (IHasScreenshots)item;
                    if (hasScreenshots.ScreenshotImagePaths.Count > imageIndex.Value)
                    {
                        hasScreenshots.ScreenshotImagePaths[imageIndex.Value] = path;
                    }
                    else if (!hasScreenshots.ScreenshotImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        hasScreenshots.ScreenshotImagePaths.Add(path);
                    }
                    break;
                case ImageType.Backdrop:
                    if (!imageIndex.HasValue)
                    {
                        throw new ArgumentNullException("imageIndex");
                    }
                    if (item.BackdropImagePaths.Count > imageIndex.Value)
                    {
                        item.BackdropImagePaths[imageIndex.Value] = path;
                    }
                    else if (!item.BackdropImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        item.BackdropImagePaths.Add(path);
                    }

                    if (string.IsNullOrEmpty(sourceUrl))
                    {
                        item.RemoveImageSourceForPath(path);
                    }
                    else
                    {
                        item.AddImageSource(path, sourceUrl);
                    }
                    break;
                default:
                    item.SetImagePath(type, path);
                    break;
            }
        }

        /// <summary>
        /// Gets the save path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="mimeType">Type of the MIME.</param>
        /// <param name="saveLocally">if set to <c>true</c> [save locally].</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// imageIndex
        /// or
        /// imageIndex
        /// </exception>
        private string GetStandardSavePath(BaseItem item, ImageType type, int? imageIndex, string mimeType, bool saveLocally)
        {
            string filename;

            switch (type)
            {
                case ImageType.Art:
                    filename = "clearart";
                    break;
                case ImageType.Disc:
                    filename = item is MusicAlbum ? "cdart" : "disc";
                    break;
                case ImageType.Primary:
                    filename = item is Episode ? Path.GetFileNameWithoutExtension(item.Path) : "folder";
                    break;
                case ImageType.Backdrop:
                    if (!imageIndex.HasValue)
                    {
                        throw new ArgumentNullException("imageIndex");
                    }
                    filename = GetBackdropSaveFilename(item.BackdropImagePaths, "backdrop", "backdrop", imageIndex.Value);
                    break;
                case ImageType.Screenshot:
                    if (!imageIndex.HasValue)
                    {
                        throw new ArgumentNullException("imageIndex");
                    }
                    var hasScreenshots = (IHasScreenshots)item;
                    filename = GetBackdropSaveFilename(hasScreenshots.ScreenshotImagePaths, "screenshot", "screenshot", imageIndex.Value);
                    break;
                default:
                    filename = type.ToString().ToLower();
                    break;
            }

            var extension = mimeType.Split('/').Last();

            if (string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase))
            {
                extension = "jpg";
            }

            extension = "." + extension.ToLower();

            string path = null;

            if (saveLocally)
            {
                if (item.IsInMixedFolder && !(item is Episode))
                {
                    path = GetSavePathForItemInMixedFolder(item, type, filename, extension);
                }

                if (string.IsNullOrEmpty(path))
                {
                    path = Path.Combine(item.MetaLocation, filename + extension);
                }
            }

            // None of the save local conditions passed, so store it in our internal folders
            if (string.IsNullOrEmpty(path))
            {
                path = _remoteImageCache.GetResourcePath(item.GetType().FullName + item.Id, filename + extension);
            }

            return path;
        }

        private string GetBackdropSaveFilename(IEnumerable<string> images, string zeroIndexFilename, string numberedIndexPrefix, int index)
        {
            if (index == 0)
            {
                return zeroIndexFilename;
            }

            var filenames = images.Select(Path.GetFileNameWithoutExtension).ToList();

            var current = index;
            while (filenames.Contains(numberedIndexPrefix + current.ToString(UsCulture), StringComparer.OrdinalIgnoreCase))
            {
                current++;
            }

            return numberedIndexPrefix + current.ToString(UsCulture);
        }

        /// <summary>
        /// Gets the compatible save paths.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <param name="mimeType">Type of the MIME.</param>
        /// <returns>IEnumerable{System.String}.</returns>
        /// <exception cref="System.ArgumentNullException">imageIndex</exception>
        private string[] GetCompatibleSavePaths(BaseItem item, ImageType type, int? imageIndex, string mimeType)
        {
            var season = item as Season;

            var extension = mimeType.Split('/').Last();

            if (string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase))
            {
                extension = "jpg";
            }
            extension = "." + extension.ToLower();

            // Backdrop paths
            if (type == ImageType.Backdrop)
            {
                if (!imageIndex.HasValue)
                {
                    throw new ArgumentNullException("imageIndex");
                }

                if (imageIndex.Value == 0)
                {
                    if (item.IsInMixedFolder)
                    {
                        return new[] { GetSavePathForItemInMixedFolder(item, type, "fanart", extension) };
                    }

                    if (season != null && item.IndexNumber.HasValue)
                    {
                        var seriesFolder = season.SeriesPath;

                        var seasonMarker = item.IndexNumber.Value == 0
                                               ? "-specials"
                                               : item.IndexNumber.Value.ToString("00", UsCulture);

                        var imageFilename = "season" + seasonMarker + "-fanart" + extension;

                        return new[] { Path.Combine(seriesFolder, imageFilename) };
                    }

                    return new[]
                        {
                            Path.Combine(item.MetaLocation, "fanart" + extension)
                        };
                }

                var outputIndex = imageIndex.Value;

                if (item.IsInMixedFolder)
                {
                    return new[] { GetSavePathForItemInMixedFolder(item, type, "fanart" + outputIndex.ToString(UsCulture), extension) };
                }
                
                var extraFanartFilename = GetBackdropSaveFilename(item.BackdropImagePaths, "fanart", "fanart", outputIndex);

                return new[]
                    {
                        Path.Combine(item.MetaLocation, "extrafanart", extraFanartFilename + extension),
                        Path.Combine(item.MetaLocation, "extrathumbs", "thumb" + outputIndex.ToString(UsCulture) + extension)
                    };
            }

            if (type == ImageType.Primary)
            {
                if (season != null && item.IndexNumber.HasValue)
                {
                    var seriesFolder = season.SeriesPath;

                    var seasonMarker = item.IndexNumber.Value == 0
                                           ? "-specials"
                                           : item.IndexNumber.Value.ToString("00", UsCulture);

                    var imageFilename = "season" + seasonMarker + "-poster" + extension;

                    return new[] { Path.Combine(seriesFolder, imageFilename) };
                }

                if (item is Episode)
                {
                    var seasonFolder = Path.GetDirectoryName(item.Path);

                    var imageFilename = Path.GetFileNameWithoutExtension(item.Path) + "-thumb" + extension;

                    return new[] { Path.Combine(seasonFolder, imageFilename) };
                }

                if (item.IsInMixedFolder || item is MusicVideo)
                {
                    return new[] { GetSavePathForItemInMixedFolder(item, type, string.Empty, extension) };
                }

                if (item is MusicAlbum || item is MusicArtist)
                {
                    return new[] { Path.Combine(item.MetaLocation, "folder" + extension) };
                }

                return new[] { Path.Combine(item.MetaLocation, "poster" + extension) };
            }

            if (type == ImageType.Banner)
            {
                if (season != null && item.IndexNumber.HasValue)
                {
                    var seriesFolder = season.SeriesPath;

                    var seasonMarker = item.IndexNumber.Value == 0
                                           ? "-specials"
                                           : item.IndexNumber.Value.ToString("00", UsCulture);

                    var imageFilename = "season" + seasonMarker + "-banner" + extension;

                    return new[] { Path.Combine(seriesFolder, imageFilename) };
                }
            }

            if (type == ImageType.Thumb)
            {
                if (season != null && item.IndexNumber.HasValue)
                {
                    var seriesFolder = season.SeriesPath;

                    var seasonMarker = item.IndexNumber.Value == 0
                                           ? "-specials"
                                           : item.IndexNumber.Value.ToString("00", UsCulture);

                    var imageFilename = "season" + seasonMarker + "-landscape" + extension;

                    return new[] { Path.Combine(seriesFolder, imageFilename) };
                }
            }

            // All other paths are the same
            return new[] { GetStandardSavePath(item, type, imageIndex, mimeType, true) };
        }

        /// <summary>
        /// Gets the save path for item in mixed folder.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="type">The type.</param>
        /// <param name="imageFilename">The image filename.</param>
        /// <param name="extension">The extension.</param>
        /// <returns>System.String.</returns>
        private string GetSavePathForItemInMixedFolder(IHasImages item, ImageType type, string imageFilename, string extension)
        {
            if (type == ImageType.Primary)
            {
                imageFilename = "poster";
            }
            var folder = Path.GetDirectoryName(item.Path);

            return Path.Combine(folder, Path.GetFileNameWithoutExtension(item.Path) + "-" + imageFilename + extension);
        }
    }
}
