﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Savers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Movies
{
    /// <summary>
    /// Class MovieDbProvider
    /// </summary>
    public class MovieDbProvider : BaseMetadataProvider, IDisposable
    {
        protected static CultureInfo EnUs = new CultureInfo("en-US");

        protected readonly IProviderManager ProviderManager;

        /// <summary>
        /// The movie db
        /// </summary>
        internal readonly SemaphoreSlim MovieDbResourcePool = new SemaphoreSlim(1, 1);

        internal static MovieDbProvider Current { get; private set; }

        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        protected IJsonSerializer JsonSerializer { get; private set; }

        /// <summary>
        /// Gets the HTTP client.
        /// </summary>
        /// <value>The HTTP client.</value>
        protected IHttpClient HttpClient { get; private set; }
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="MovieDbProvider" /> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="configurationManager">The configuration manager.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="providerManager">The provider manager.</param>
        public MovieDbProvider(ILogManager logManager, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IHttpClient httpClient, IProviderManager providerManager, IFileSystem fileSystem)
            : base(logManager, configurationManager)
        {
            JsonSerializer = jsonSerializer;
            HttpClient = httpClient;
            ProviderManager = providerManager;
            _fileSystem = fileSystem;
            Current = this;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                MovieDbResourcePool.Dispose();
            }
        }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public override MetadataProviderPriority Priority
        {
            get { return MetadataProviderPriority.Third; }
        }

        /// <summary>
        /// Supportses the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public override bool Supports(BaseItem item)
        {
            var trailer = item as Trailer;

            if (trailer != null)
            {
                return !trailer.IsLocalTrailer;
            }

            // Don't support local trailers
            return item is Movie || item is BoxSet || item is MusicVideo;
        }

        /// <summary>
        /// Gets a value indicating whether [requires internet].
        /// </summary>
        /// <value><c>true</c> if [requires internet]; otherwise, <c>false</c>.</value>
        public override bool RequiresInternet
        {
            get
            {
                return true;
            }
        }

        protected override bool RefreshOnVersionChange
        {
            get
            {
                return true;
            }
        }

        protected override string ProviderVersion
        {
            get
            {
                return "3";
            }
        }

        /// <summary>
        /// The _TMDB settings task
        /// </summary>
        private TmdbSettingsResult _tmdbSettings;

        private readonly SemaphoreSlim _tmdbSettingsSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets the TMDB settings.
        /// </summary>
        /// <returns>Task{TmdbSettingsResult}.</returns>
        internal async Task<TmdbSettingsResult> GetTmdbSettings(CancellationToken cancellationToken)
        {
            if (_tmdbSettings != null)
            {
                return _tmdbSettings;
            }

            await _tmdbSettingsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Check again in case it got populated while we were waiting.
                if (_tmdbSettings != null)
                {
                    return _tmdbSettings;
                }

                using (var json = await GetMovieDbResponse(new HttpRequestOptions
                {
                    Url = string.Format(TmdbConfigUrl, ApiKey),
                    CancellationToken = cancellationToken,
                    AcceptHeader = AcceptHeader

                }).ConfigureAwait(false))
                {
                    _tmdbSettings = JsonSerializer.DeserializeFromStream<TmdbSettingsResult>(json);

                    return _tmdbSettings;
                }
            }
            finally
            {
                _tmdbSettingsSemaphore.Release();
            }
        }

        private const string TmdbConfigUrl = "http://api.themoviedb.org/3/configuration?api_key={0}";
        private const string Search3 = @"http://api.themoviedb.org/3/search/{3}?api_key={1}&query={0}&language={2}";
        private const string GetMovieInfo3 = @"http://api.themoviedb.org/3/movie/{0}?api_key={1}&append_to_response=casts,releases,images,keywords,trailers";
        private const string GetBoxSetInfo3 = @"http://api.themoviedb.org/3/collection/{0}?api_key={1}&append_to_response=images";

        internal static string ApiKey = "f6bd687ffa63cd282b6ff2c6877f2669";
        internal static string AcceptHeader = "application/json,image/*";

        protected override bool NeedsRefreshInternal(BaseItem item, BaseProviderInfo providerInfo)
        {
            if (string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.Tmdb)))
            {
                return true;
            }

            return base.NeedsRefreshInternal(item, providerInfo);
        }

        protected override bool NeedsRefreshBasedOnCompareDate(BaseItem item, BaseProviderInfo providerInfo)
        {
            var path = GetDataFilePath(item);

            if (!string.IsNullOrEmpty(path))
            {
                var fileInfo = new FileInfo(path);

                return !fileInfo.Exists || _fileSystem.GetLastWriteTimeUtc(fileInfo) > providerInfo.LastRefreshed;
            }

            return base.NeedsRefreshBasedOnCompareDate(item, providerInfo);
        }

        /// <summary>
        /// Gets the movie data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="isBoxSet">if set to <c>true</c> [is box set].</param>
        /// <param name="tmdbId">The TMDB id.</param>
        /// <returns>System.String.</returns>
        internal static string GetMovieDataPath(IApplicationPaths appPaths, bool isBoxSet, string tmdbId)
        {
            var dataPath = isBoxSet ? GetBoxSetsDataPath(appPaths) : GetMoviesDataPath(appPaths);

            return Path.Combine(dataPath, tmdbId);
        }

        internal static string GetMoviesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.DataPath, "tmdb-movies");

            return dataPath;
        }

        internal static string GetBoxSetsDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.DataPath, "tmdb-collections");

            return dataPath;
        }

        /// <summary>
        /// Fetches metadata and returns true or false indicating if any work that requires persistence was done
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task{System.Boolean}.</returns>
        public override async Task<bool> FetchAsync(BaseItem item, bool force, BaseProviderInfo providerInfo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = item.GetProviderId(MetadataProviders.Tmdb);

            if (string.IsNullOrEmpty(id))
            {
                id = item.GetProviderId(MetadataProviders.Imdb);
            }

            // Don't search for music video id's because it is very easy to misidentify. 
            if (string.IsNullOrEmpty(id) && !(item is MusicVideo))
            {
                id = await FindId(item, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(id))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await FetchMovieData(item, id, force, cancellationToken).ConfigureAwait(false);
            }

            SetLastRefreshed(item, DateTime.UtcNow, providerInfo);
            return true;
        }

        /// <summary>
        /// Determines whether [has alt meta] [the specified item].
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if [has alt meta] [the specified item]; otherwise, <c>false</c>.</returns>
        internal static bool HasAltMeta(BaseItem item)
        {
            if (item is BoxSet)
            {
                return item.LocationType == LocationType.FileSystem && item.ResolveArgs.ContainsMetaFileByName("collection.xml");
            }

            var path = MovieXmlSaver.GetMovieSavePath(item);

            if (item.LocationType == LocationType.FileSystem)
            {
                // If mixed with multiple movies in one folder, resolve args won't have the file system children
                return item.ResolveArgs.ContainsMetaFileByName(Path.GetFileName(path)) || File.Exists(path);
            }

            return false;
        }

        /// <summary>
        /// Finds the id.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task{System.String}.</returns>
        public async Task<string> FindId(BaseItem item, CancellationToken cancellationToken)
        {
            int? yearInName;
            string name = item.Name;
            NameParser.ParseName(name, out name, out yearInName);

            var year = item.ProductionYear ?? yearInName;

            Logger.Info("MovieDbProvider: Finding id for item: " + name);
            var language = item.GetPreferredMetadataLanguage().ToLower();

            //if we are a boxset - look at our first child
            var boxset = item as BoxSet;
            if (boxset != null)
            {
                // See if any movies have a collection id already
                var collId = boxset.Children.Concat(boxset.GetLinkedChildren()).OfType<Video>()
                    .Select(i => i.GetProviderId(MetadataProviders.TmdbCollection))
                   .FirstOrDefault(i => i != null);

                if (collId != null) return collId;

            }

            //nope - search for it
            var searchType = item is BoxSet ? "collection" : "movie";
            var id = await AttemptFindId(name, searchType, year, language, cancellationToken).ConfigureAwait(false);
            if (id == null)
            {
                //try in english if wasn't before
                if (language != "en")
                {
                    id = await AttemptFindId(name, searchType, year, "en", cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // try with dot and _ turned to space
                    var originalName = name;

                    name = name.Replace(",", " ");
                    name = name.Replace(".", " ");
                    name = name.Replace("_", " ");
                    name = name.Replace("-", " ");

                    // Search again if the new name is different
                    if (!string.Equals(name, originalName))
                    {
                        id = await AttemptFindId(name, searchType, year, language, cancellationToken).ConfigureAwait(false);

                        if (id == null && language != "en")
                        {
                            //one more time, in english
                            id = await AttemptFindId(name, searchType, year, "en", cancellationToken).ConfigureAwait(false);

                        }
                    }

                    if (id == null && item.LocationType == LocationType.FileSystem)
                    {
                        //last resort - try using the actual folder name
                        var pathName = Path.GetFileName(item.ResolveArgs.Path);

                        // Only search if it's a name we haven't already tried.
                        if (!string.Equals(pathName, name, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(pathName, originalName, StringComparison.OrdinalIgnoreCase))
                        {
                            id = await AttemptFindId(pathName, searchType, year, "en", cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            return id;
        }

        /// <summary>
        /// Attempts the find id.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">movie or collection</param>
        /// <param name="year">The year.</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task{System.String}.</returns>
        private async Task<string> AttemptFindId(string name, string type, int? year, string language, CancellationToken cancellationToken)
        {
            string url3 = string.Format(Search3, UrlEncode(name), ApiKey, language, type);
            TmdbMovieSearchResults searchResult = null;

            using (Stream json = await GetMovieDbResponse(new HttpRequestOptions
            {
                Url = url3,
                CancellationToken = cancellationToken,
                AcceptHeader = AcceptHeader

            }).ConfigureAwait(false))
            {
                searchResult = JsonSerializer.DeserializeFromStream<TmdbMovieSearchResults>(json);
            }

            if (searchResult != null)
            {
                return FindIdOfBestResult(searchResult.results, name, year);
            }

            return null;
        }

        private string FindIdOfBestResult(List<TmdbMovieSearchResult> results, string name, int? year)
        {
            if (year.HasValue)
            {
                // Take the first result from the same year
                var id = results.Where(i =>
                {
                    // Make sure it has a name
                    if (!string.IsNullOrEmpty(i.title ?? i.name))
                    {
                        DateTime r;

                        // These dates are always in this exact format
                        if (DateTime.TryParseExact(i.release_date, "yyyy-MM-dd", EnUs, DateTimeStyles.None, out r))
                        {
                            return r.Year == year.Value;
                        }
                    }

                    return false;
                })
                    .Select(i => i.id.ToString(CultureInfo.InvariantCulture))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }

                // Take the first result within one year
                id = results.Where(i =>
               {
                   // Make sure it has a name
                   if (!string.IsNullOrEmpty(i.title ?? i.name))
                   {
                       DateTime r;

                       // These dates are always in this exact format
                       if (DateTime.TryParseExact(i.release_date, "yyyy-MM-dd", EnUs, DateTimeStyles.None, out r))
                       {
                           return Math.Abs(r.Year - year.Value) <= 1;
                       }
                   }

                   return false;
               })
                   .Select(i => i.id.ToString(CultureInfo.InvariantCulture))
                   .FirstOrDefault();

                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }

            // Just take the first one
            return results.Where(i => !string.IsNullOrEmpty(i.title ?? i.name))
                .Select(i => i.id.ToString(CultureInfo.InvariantCulture))
                .FirstOrDefault();
        }

        /// <summary>
        /// URLs the encode.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        private static string UrlEncode(string name)
        {
            return WebUtility.UrlEncode(name);
        }

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        /// <summary>
        /// Fetches the movie data.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="id">The id.</param>
        /// <param name="isForcedRefresh">if set to <c>true</c> [is forced refresh].</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task.</returns>
        private async Task FetchMovieData(BaseItem item, string id, bool isForcedRefresh, CancellationToken cancellationToken)
        {
            // Id could be ImdbId or TmdbId

            var language = item.GetPreferredMetadataLanguage();
            var country = item.GetPreferredMetadataCountryCode();

            var dataFilePath = GetDataFilePath(item);

            var tmdbId = item.GetProviderId(MetadataProviders.Tmdb);

            var isBoxSet = item is BoxSet;

            if (string.IsNullOrEmpty(dataFilePath) || !File.Exists(dataFilePath))
            {
                var mainResult = await FetchMainResult(id, isBoxSet, language, cancellationToken).ConfigureAwait(false);

                if (mainResult == null) return;

                tmdbId = mainResult.id.ToString(_usCulture);

                dataFilePath = GetDataFilePath(isBoxSet, tmdbId, language);

                var directory = Path.GetDirectoryName(dataFilePath);

                Directory.CreateDirectory(directory);

                JsonSerializer.SerializeToFile(mainResult, dataFilePath);
            }

            if (isForcedRefresh || ConfigurationManager.Configuration.EnableTmdbUpdates || !HasAltMeta(item))
            {
                dataFilePath = GetDataFilePath(isBoxSet, tmdbId, language);

                if (!string.IsNullOrEmpty(dataFilePath))
                {
                    var mainResult = JsonSerializer.DeserializeFromFile<CompleteMovieData>(dataFilePath);

                    ProcessMainInfo(item, mainResult);
                }
            }
        }

        /// <summary>
        /// Downloads the movie info.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="isBoxSet">if set to <c>true</c> [is box set].</param>
        /// <param name="preferredMetadataLanguage">The preferred metadata language.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task DownloadMovieInfo(string id, bool isBoxSet, string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            var mainResult = await FetchMainResult(id, isBoxSet, preferredMetadataLanguage, cancellationToken).ConfigureAwait(false);

            if (mainResult == null) return;

            var dataFilePath = GetDataFilePath(isBoxSet, id, preferredMetadataLanguage);

            Directory.CreateDirectory(Path.GetDirectoryName(dataFilePath));

            JsonSerializer.SerializeToFile(mainResult, dataFilePath);
        }

        internal Task EnsureMovieInfo(BaseItem item, CancellationToken cancellationToken)
        {
            var path = GetDataFilePath(item);

            var fileInfo = _fileSystem.GetFileSystemInfo(path);

            if (fileInfo.Exists)
            {
                // If it's recent or automatic updates are enabled, don't re-download
                if (ConfigurationManager.Configuration.EnableTmdbUpdates || (DateTime.UtcNow - _fileSystem.GetLastWriteTimeUtc(fileInfo)).TotalDays <= 7)
                {
                    return Task.FromResult(true);
                }
            }

            var id = item.GetProviderId(MetadataProviders.Tmdb);

            if (string.IsNullOrEmpty(id))
            {
                return Task.FromResult(true);
            }

            return DownloadMovieInfo(id, item is BoxSet, item.GetPreferredMetadataLanguage(), cancellationToken);
        }

        /// <summary>
        /// Gets the data file path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        internal string GetDataFilePath(BaseItem item)
        {
            var id = item.GetProviderId(MetadataProviders.Tmdb);

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return GetDataFilePath(item is BoxSet, id, item.GetPreferredMetadataLanguage());
        }

        private string GetDataFilePath(bool isBoxset, string tmdbId, string preferredLanguage)
        {
            var path = GetMovieDataPath(ConfigurationManager.ApplicationPaths, isBoxset, tmdbId);

            var filename = string.Format("all-{0}.json",
                preferredLanguage ?? string.Empty);

            return Path.Combine(path, filename);
        }

        /// <summary>
        /// Fetches the main result.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="isBoxSet">if set to <c>true</c> [is box set].</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task{CompleteMovieData}.</returns>
        private async Task<CompleteMovieData> FetchMainResult(string id, bool isBoxSet, string language, CancellationToken cancellationToken)
        {
            var baseUrl = isBoxSet ? GetBoxSetInfo3 : GetMovieInfo3;

            var url = string.Format(baseUrl, id, ApiKey);

            // Get images in english and with no language
            url += "&include_image_language=en,null";

            if (!string.IsNullOrEmpty(language))
            {
                // If preferred language isn't english, get those images too
                if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
                {
                    url += string.Format(",{0}", language);
                }

                url += string.Format("&language={0}", language);
            }

            CompleteMovieData mainResult;

            cancellationToken.ThrowIfCancellationRequested();

            using (var json = await GetMovieDbResponse(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                AcceptHeader = AcceptHeader

            }).ConfigureAwait(false))
            {
                mainResult = JsonSerializer.DeserializeFromStream<CompleteMovieData>(json);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (mainResult != null && string.IsNullOrEmpty(mainResult.overview))
            {
                if (!string.IsNullOrEmpty(language) && !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("MovieDbProvider couldn't find meta for language " + language + ". Trying English...");

                    url = string.Format(baseUrl, id, ApiKey, "en");

                    using (var json = await GetMovieDbResponse(new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        AcceptHeader = AcceptHeader

                    }).ConfigureAwait(false))
                    {
                        mainResult = JsonSerializer.DeserializeFromStream<CompleteMovieData>(json);
                    }

                    if (String.IsNullOrEmpty(mainResult.overview))
                    {
                        Logger.Error("MovieDbProvider - Unable to find information for (id:" + id + ")");
                        return null;
                    }
                }
            }
            return mainResult;
        }

        /// <summary>
        /// Processes the main info.
        /// </summary>
        /// <param name="movie">The movie.</param>
        /// <param name="movieData">The movie data.</param>
        private void ProcessMainInfo(BaseItem movie, CompleteMovieData movieData)
        {
            if (!movie.LockedFields.Contains(MetadataFields.Name))
            {
                movie.Name = movieData.title ?? movieData.original_title ?? movieData.name ?? movie.Name;
            }
            if (!movie.LockedFields.Contains(MetadataFields.Overview))
            {
                movie.Overview = WebUtility.HtmlDecode(movieData.overview);
                movie.Overview = movie.Overview != null ? movie.Overview.Replace("\n\n", "\n") : null;
            }
            movie.HomePageUrl = movieData.homepage;

            var hasBudget = movie as IHasBudget;
            if (hasBudget != null)
            {
                hasBudget.Budget = movieData.budget;
                hasBudget.Revenue = movieData.revenue;
            }

            if (!string.IsNullOrEmpty(movieData.tagline))
            {
                var hasTagline = movie as IHasTaglines;
                if (hasTagline != null)
                {
                    hasTagline.Taglines.Clear();
                    hasTagline.AddTagline(movieData.tagline);
                }
            }

            movie.SetProviderId(MetadataProviders.Tmdb, movieData.id.ToString(_usCulture));
            movie.SetProviderId(MetadataProviders.Imdb, movieData.imdb_id);

            if (movieData.belongs_to_collection != null)
            {
                movie.SetProviderId(MetadataProviders.TmdbCollection,
                                    movieData.belongs_to_collection.id.ToString(CultureInfo.InvariantCulture));

                var movieItem = movie as Movie;

                if (movieItem != null)
                {
                    movieItem.TmdbCollectionName = movieData.belongs_to_collection.name;
                }
            }
            else
            {
                movie.SetProviderId(MetadataProviders.TmdbCollection, null); // clear out any old entry
            }

            float rating;
            string voteAvg = movieData.vote_average.ToString(CultureInfo.InvariantCulture);

            // tmdb appears to have unified their numbers to always report "7.3" regardless of country
            // so I removed the culture-specific processing here because it was not working for other countries -ebr
            // Movies get this from imdb
            if (!(movie is Movie) && float.TryParse(voteAvg, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out rating))
            {
                movie.CommunityRating = rating;
            }

            // Movies get this from imdb
            if (!(movie is Movie))
            {
                movie.VoteCount = movieData.vote_count;
            }

            var preferredCountryCode = movie.GetPreferredMetadataCountryCode();

            //release date and certification are retrieved based on configured country and we fall back on US if not there and to minimun release date if still no match
            if (movieData.releases != null && movieData.releases.countries != null)
            {
                var ourRelease = movieData.releases.countries.FirstOrDefault(c => c.iso_3166_1.Equals(preferredCountryCode, StringComparison.OrdinalIgnoreCase)) ?? new Country();
                var usRelease = movieData.releases.countries.FirstOrDefault(c => c.iso_3166_1.Equals("US", StringComparison.OrdinalIgnoreCase)) ?? new Country();
                var minimunRelease = movieData.releases.countries.OrderBy(c => c.release_date).FirstOrDefault() ?? new Country();

                if (!movie.LockedFields.Contains(MetadataFields.OfficialRating))
                {
                    var ratingPrefix = string.Equals(preferredCountryCode, "us", StringComparison.OrdinalIgnoreCase) ? "" : preferredCountryCode + "-";
                    movie.OfficialRating = !string.IsNullOrEmpty(ourRelease.certification)
                                               ? ratingPrefix + ourRelease.certification
                                               : !string.IsNullOrEmpty(usRelease.certification)
                                                     ? usRelease.certification
                                                     : !string.IsNullOrEmpty(minimunRelease.certification)
                                                           ? minimunRelease.iso_3166_1 + "-" + minimunRelease.certification
                                                           : null;
                }
            }

            if (movieData.release_date.Year != 1)
            {
                //no specific country release info at all
                movie.PremiereDate = movieData.release_date.ToUniversalTime();
                movie.ProductionYear = movieData.release_date.Year;
            }

            // If that didn't find a rating and we are a boxset, use the one from our first child
            if (movie.OfficialRating == null && movie is BoxSet && !movie.LockedFields.Contains(MetadataFields.OfficialRating))
            {
                var boxset = movie as BoxSet;
                Logger.Info("MovieDbProvider - Using rating of first child of boxset...");

                var firstChild = boxset.Children.Concat(boxset.GetLinkedChildren()).FirstOrDefault();

                boxset.OfficialRating = firstChild != null ? firstChild.OfficialRating : null;
            }

            if (movieData.runtime > 0)
                movie.OriginalRunTimeTicks = TimeSpan.FromMinutes(movieData.runtime).Ticks;

            //studios
            if (movieData.production_companies != null && !movie.LockedFields.Contains(MetadataFields.Studios))
            {
                movie.Studios.Clear();

                foreach (var studio in movieData.production_companies.Select(c => c.name))
                {
                    movie.AddStudio(studio);
                }
            }

            // genres
            // Movies get this from imdb
            var genres = movieData.genres ?? new List<GenreItem>();
            if (!movie.LockedFields.Contains(MetadataFields.Genres))
            {
                // Only grab them if a boxset or there are no genres.
                // For movies and trailers we'll use imdb via omdb
                // But omdb data is for english users only so fetch if language is not english
                if (!(movie is Movie) || movie.Genres.Count == 0 || !string.Equals(movie.GetPreferredMetadataLanguage(), "en", StringComparison.OrdinalIgnoreCase))
                {
                    movie.Genres.Clear();

                    foreach (var genre in genres.Select(g => g.name))
                    {
                        movie.AddGenre(genre);
                    }
                }
            }

            if (!movie.LockedFields.Contains(MetadataFields.Cast))
            {
                movie.People.Clear();

                //Actors, Directors, Writers - all in People
                //actors come from cast
                if (movieData.casts != null && movieData.casts.cast != null)
                {
                    foreach (var actor in movieData.casts.cast.OrderBy(a => a.order)) movie.AddPerson(new PersonInfo { Name = actor.name.Trim(), Role = actor.character, Type = PersonType.Actor, SortOrder = actor.order });
                }

                //and the rest from crew
                if (movieData.casts != null && movieData.casts.crew != null)
                {
                    foreach (var person in movieData.casts.crew) movie.AddPerson(new PersonInfo { Name = person.name.Trim(), Role = person.job, Type = person.department });
                }
            }

            if (movieData.keywords != null && movieData.keywords.keywords != null && !movie.LockedFields.Contains(MetadataFields.Keywords))
            {
                var hasTags = movie as IHasKeywords;
                if (hasTags != null)
                {
                    hasTags.Keywords = movieData.keywords.keywords.Select(i => i.name).ToList();
                }
            }

            if (movieData.trailers != null && movieData.trailers.youtube != null &&
                movieData.trailers.youtube.Count > 0)
            {
                var hasTrailers = movie as IHasTrailers;
                if (hasTrailers != null)
                {
                    hasTrailers.RemoteTrailers = movieData.trailers.youtube.Select(i => new MediaUrl
                    {
                        Url = string.Format("http://www.youtube.com/watch?v={0}", i.source),
                        IsDirectLink = false,
                        Name = i.name,
                        VideoSize = string.Equals("hd", i.size, StringComparison.OrdinalIgnoreCase) ? VideoSize.HighDefinition : VideoSize.StandardDefinition

                    }).ToList();
                }
            }
        }

        private DateTime _lastRequestDate = DateTime.MinValue;

        /// <summary>
        /// Gets the movie db response.
        /// </summary>
        internal async Task<Stream> GetMovieDbResponse(HttpRequestOptions options)
        {
            var cancellationToken = options.CancellationToken;

            await MovieDbResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Limit to three requests per second
                var diff = 340 - (DateTime.Now - _lastRequestDate).TotalMilliseconds;

                if (diff > 0)
                {
                    await Task.Delay(Convert.ToInt32(diff), cancellationToken).ConfigureAwait(false);
                }

                _lastRequestDate = DateTime.Now;

                return await HttpClient.Get(options).ConfigureAwait(false);
            }
            finally
            {
                _lastRequestDate = DateTime.Now;

                MovieDbResourcePool.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Class TmdbTitle
        /// </summary>
        internal class TmdbTitle
        {
            /// <summary>
            /// Gets or sets the iso_3166_1.
            /// </summary>
            /// <value>The iso_3166_1.</value>
            public string iso_3166_1 { get; set; }
            /// <summary>
            /// Gets or sets the title.
            /// </summary>
            /// <value>The title.</value>
            public string title { get; set; }
        }

        /// <summary>
        /// Class TmdbAltTitleResults
        /// </summary>
        internal class TmdbAltTitleResults
        {
            /// <summary>
            /// Gets or sets the id.
            /// </summary>
            /// <value>The id.</value>
            public int id { get; set; }
            /// <summary>
            /// Gets or sets the titles.
            /// </summary>
            /// <value>The titles.</value>
            public List<TmdbTitle> titles { get; set; }
        }

        /// <summary>
        /// Class TmdbMovieSearchResult
        /// </summary>
        internal class TmdbMovieSearchResult
        {
            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="TmdbMovieSearchResult" /> is adult.
            /// </summary>
            /// <value><c>true</c> if adult; otherwise, <c>false</c>.</value>
            public bool adult { get; set; }
            /// <summary>
            /// Gets or sets the backdrop_path.
            /// </summary>
            /// <value>The backdrop_path.</value>
            public string backdrop_path { get; set; }
            /// <summary>
            /// Gets or sets the id.
            /// </summary>
            /// <value>The id.</value>
            public int id { get; set; }
            /// <summary>
            /// Gets or sets the original_title.
            /// </summary>
            /// <value>The original_title.</value>
            public string original_title { get; set; }
            /// <summary>
            /// Gets or sets the release_date.
            /// </summary>
            /// <value>The release_date.</value>
            public string release_date { get; set; }
            /// <summary>
            /// Gets or sets the poster_path.
            /// </summary>
            /// <value>The poster_path.</value>
            public string poster_path { get; set; }
            /// <summary>
            /// Gets or sets the popularity.
            /// </summary>
            /// <value>The popularity.</value>
            public double popularity { get; set; }
            /// <summary>
            /// Gets or sets the title.
            /// </summary>
            /// <value>The title.</value>
            public string title { get; set; }
            /// <summary>
            /// Gets or sets the vote_average.
            /// </summary>
            /// <value>The vote_average.</value>
            public double vote_average { get; set; }
            /// <summary>
            /// For collection search results
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// Gets or sets the vote_count.
            /// </summary>
            /// <value>The vote_count.</value>
            public int vote_count { get; set; }
        }

        /// <summary>
        /// Class TmdbMovieSearchResults
        /// </summary>
        internal class TmdbMovieSearchResults
        {
            /// <summary>
            /// Gets or sets the page.
            /// </summary>
            /// <value>The page.</value>
            public int page { get; set; }
            /// <summary>
            /// Gets or sets the results.
            /// </summary>
            /// <value>The results.</value>
            public List<TmdbMovieSearchResult> results { get; set; }
            /// <summary>
            /// Gets or sets the total_pages.
            /// </summary>
            /// <value>The total_pages.</value>
            public int total_pages { get; set; }
            /// <summary>
            /// Gets or sets the total_results.
            /// </summary>
            /// <value>The total_results.</value>
            public int total_results { get; set; }
        }

        internal class BelongsToCollection
        {
            public int id { get; set; }
            public string name { get; set; }
            public string poster_path { get; set; }
            public string backdrop_path { get; set; }
        }

        internal class GenreItem
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        internal class ProductionCompany
        {
            public string name { get; set; }
            public int id { get; set; }
        }

        internal class ProductionCountry
        {
            public string iso_3166_1 { get; set; }
            public string name { get; set; }
        }

        internal class SpokenLanguage
        {
            public string iso_639_1 { get; set; }
            public string name { get; set; }
        }

        internal class Cast
        {
            public int id { get; set; }
            public string name { get; set; }
            public string character { get; set; }
            public int order { get; set; }
            public int cast_id { get; set; }
            public string profile_path { get; set; }
        }

        internal class Crew
        {
            public int id { get; set; }
            public string name { get; set; }
            public string department { get; set; }
            public string job { get; set; }
            public string profile_path { get; set; }
        }

        internal class Casts
        {
            public List<Cast> cast { get; set; }
            public List<Crew> crew { get; set; }
        }

        internal class Country
        {
            public string iso_3166_1 { get; set; }
            public string certification { get; set; }
            public DateTime release_date { get; set; }
        }

        internal class Releases
        {
            public List<Country> countries { get; set; }
        }

        internal class Backdrop
        {
            public string file_path { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public object iso_639_1 { get; set; }
            public double aspect_ratio { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
        }

        internal class Poster
        {
            public string file_path { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public string iso_639_1 { get; set; }
            public double aspect_ratio { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
        }

        internal class Images
        {
            public List<Backdrop> backdrops { get; set; }
            public List<Poster> posters { get; set; }
        }

        internal class Keyword
        {
            public int id { get; set; }
            public string name { get; set; }
        }

        internal class Keywords
        {
            public List<Keyword> keywords { get; set; }
        }

        internal class Youtube
        {
            public string name { get; set; }
            public string size { get; set; }
            public string source { get; set; }
        }

        internal class Trailers
        {
            public List<object> quicktime { get; set; }
            public List<Youtube> youtube { get; set; }
        }

        internal class CompleteMovieData
        {
            public bool adult { get; set; }
            public string backdrop_path { get; set; }
            public BelongsToCollection belongs_to_collection { get; set; }
            public int budget { get; set; }
            public List<GenreItem> genres { get; set; }
            public string homepage { get; set; }
            public int id { get; set; }
            public string imdb_id { get; set; }
            public string original_title { get; set; }
            public string overview { get; set; }
            public double popularity { get; set; }
            public string poster_path { get; set; }
            public List<ProductionCompany> production_companies { get; set; }
            public List<ProductionCountry> production_countries { get; set; }
            public DateTime release_date { get; set; }
            public int revenue { get; set; }
            public int runtime { get; set; }
            public List<SpokenLanguage> spoken_languages { get; set; }
            public string status { get; set; }
            public string tagline { get; set; }
            public string title { get; set; }
            public string name { get; set; }
            public double vote_average { get; set; }
            public int vote_count { get; set; }
            public Casts casts { get; set; }
            public Releases releases { get; set; }
            public Images images { get; set; }
            public Keywords keywords { get; set; }
            public Trailers trailers { get; set; }
        }

        internal class TmdbImageSettings
        {
            public List<string> backdrop_sizes { get; set; }
            public string base_url { get; set; }
            public List<string> poster_sizes { get; set; }
            public List<string> profile_sizes { get; set; }
        }

        internal class TmdbSettingsResult
        {
            public TmdbImageSettings images { get; set; }
        }
    }
}
