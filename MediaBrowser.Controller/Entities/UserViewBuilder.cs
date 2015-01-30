﻿using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoreLinq;

namespace MediaBrowser.Controller.Entities
{
    public class UserViewBuilder
    {
        private readonly IChannelManager _channelManager;
        private readonly ILiveTvManager _liveTvManager;
        private readonly IUserViewManager _userViewManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IUserDataManager _userDataManager;
        private readonly ITVSeriesManager _tvSeriesManager;
        private readonly ICollectionManager _collectionManager;

        public UserViewBuilder(IUserViewManager userViewManager, ILiveTvManager liveTvManager, IChannelManager channelManager, ILibraryManager libraryManager, ILogger logger, IUserDataManager userDataManager, ITVSeriesManager tvSeriesManager, ICollectionManager collectionManager)
        {
            _userViewManager = userViewManager;
            _liveTvManager = liveTvManager;
            _channelManager = channelManager;
            _libraryManager = libraryManager;
            _logger = logger;
            _userDataManager = userDataManager;
            _tvSeriesManager = tvSeriesManager;
            _collectionManager = collectionManager;
        }

        public async Task<QueryResult<BaseItem>> GetUserItems(Folder queryParent, Folder displayParent, string viewType, InternalItemsQuery query)
        {
            var user = query.User;

            switch (viewType)
            {
                case CollectionType.Channels:
                    {
                        var result = await _channelManager.GetChannelsInternal(new ChannelQuery
                        {
                            UserId = user.Id.ToString("N"),
                            Limit = query.Limit,
                            StartIndex = query.StartIndex

                        }, CancellationToken.None).ConfigureAwait(false);

                        return GetResult(result);
                    }

                case SpecialFolder.LiveTvChannels:
                    {
                        var result = await _liveTvManager.GetInternalChannels(new LiveTvChannelQuery
                        {
                            UserId = query.User.Id.ToString("N"),
                            Limit = query.Limit,
                            StartIndex = query.StartIndex

                        }, CancellationToken.None).ConfigureAwait(false);

                        return GetResult(result);
                    }

                case SpecialFolder.LiveTvNowPlaying:
                    {
                        var result = await _liveTvManager.GetRecommendedProgramsInternal(new RecommendedProgramQuery
                        {
                            UserId = query.User.Id.ToString("N"),
                            Limit = query.Limit,
                            IsAiring = true

                        }, CancellationToken.None).ConfigureAwait(false);

                        return GetResult(result);
                    }

                case SpecialFolder.LiveTvRecordingGroups:
                    {
                        var result = await _liveTvManager.GetInternalRecordings(new RecordingQuery
                        {
                            UserId = query.User.Id.ToString("N"),
                            Status = RecordingStatus.Completed,
                            Limit = query.Limit,
                            StartIndex = query.StartIndex

                        }, CancellationToken.None).ConfigureAwait(false);

                        return GetResult(result);
                    }

                case CollectionType.LiveTv:
                    {
                        var result = await GetLiveTvFolders(user).ConfigureAwait(false);

                        return GetResult(result, queryParent, query);
                    }

                case CollectionType.Folders:
                    return GetResult(user.RootFolder.GetChildren(user, true), queryParent, query);

                case CollectionType.Games:
                    return await GetGameView(user, queryParent, query).ConfigureAwait(false);

                case CollectionType.BoxSets:
                    return await GetBoxsetView(queryParent, user, query).ConfigureAwait(false);

                case CollectionType.TvShows:
                    return await GetTvView(queryParent, user, query).ConfigureAwait(false);

                case CollectionType.Music:
                    return await GetMusicFolders(queryParent, user, query).ConfigureAwait(false);

                case CollectionType.Movies:
                    return await GetMovieFolders(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.MusicGenres:
                    return await GetMusicGenres(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.MusicGenre:
                    return await GetMusicGenreItems(queryParent, displayParent, user, query).ConfigureAwait(false);

                case SpecialFolder.GameGenres:
                    return await GetGameGenres(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.GameGenre:
                    return await GetGameGenreItems(queryParent, displayParent, user, query).ConfigureAwait(false);

                case SpecialFolder.GameSystems:
                    return GetGameSystems(queryParent, user, query);

                case SpecialFolder.LatestGames:
                    return GetLatestGames(queryParent, user, query);

                case SpecialFolder.RecentlyPlayedGames:
                    return GetRecentlyPlayedGames(queryParent, user, query);

                case SpecialFolder.GameFavorites:
                    return GetFavoriteGames(queryParent, user, query);

                case SpecialFolder.TvShowSeries:
                    return GetTvSeries(queryParent, user, query);

                case SpecialFolder.TvGenres:
                    return await GetTvGenres(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.TvGenre:
                    return await GetTvGenreItems(queryParent, displayParent, user, query).ConfigureAwait(false);

                case SpecialFolder.TvResume:
                    return GetTvResume(queryParent, user, query);

                case SpecialFolder.TvNextUp:
                    return GetTvNextUp(queryParent, query);

                case SpecialFolder.TvLatest:
                    return GetTvLatest(queryParent, user, query);

                case SpecialFolder.MovieFavorites:
                    return GetFavoriteMovies(queryParent, user, query);

                case SpecialFolder.MovieLatest:
                    return GetMovieLatest(queryParent, user, query);

                case SpecialFolder.MovieGenres:
                    return await GetMovieGenres(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.MovieGenre:
                    return await GetMovieGenreItems(queryParent, displayParent, user, query).ConfigureAwait(false);

                case SpecialFolder.MovieResume:
                    return GetMovieResume(queryParent, user, query);

                case SpecialFolder.MovieMovies:
                    return GetMovieMovies(queryParent, user, query);

                case SpecialFolder.MovieCollections:
                    return GetMovieCollections(queryParent, user, query);

                case SpecialFolder.MusicLatest:
                    return GetMusicLatest(queryParent, user, query);

                case SpecialFolder.MusicAlbums:
                    return GetMusicAlbums(queryParent, user, query);

                case SpecialFolder.MusicAlbumArtists:
                    return GetMusicAlbumArtists(queryParent, user, query);

                case SpecialFolder.MusicArtists:
                    return GetMusicArtists(queryParent, user, query);

                case SpecialFolder.MusicSongs:
                    return GetMusicSongs(queryParent, user, query);

                case SpecialFolder.TvFavoriteEpisodes:
                    return GetFavoriteEpisodes(queryParent, user, query);

                case SpecialFolder.TvFavoriteSeries:
                    return GetFavoriteSeries(queryParent, user, query);

                case SpecialFolder.MusicFavorites:
                    return await GetMusicFavorites(queryParent, user, query).ConfigureAwait(false);

                case SpecialFolder.MusicFavoriteAlbums:
                    return GetFavoriteAlbums(queryParent, user, query);

                case SpecialFolder.MusicFavoriteArtists:
                    return GetFavoriteArtists(queryParent, user, query);

                case SpecialFolder.MusicFavoriteSongs:
                    return GetFavoriteSongs(queryParent, user, query);

                default:
                    return GetResult(GetMediaFolders(user).SelectMany(i => i.GetChildren(user, true)), queryParent, query);
            }
        }

        private int GetSpecialItemsLimit()
        {
            return 50;
        }

        private async Task<QueryResult<BaseItem>> GetMusicFolders(Folder parent, User user, InternalItemsQuery query)
        {
            if (query.Recursive)
            {
                var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos }, i => FilterItem(i, query));

                return PostFilterAndSort(items, parent, null, query);
            }

            var list = new List<BaseItem>();

            list.Add(await GetUserView(SpecialFolder.MusicLatest, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicAlbums, user, "1", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicAlbumArtists, user, "2", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicArtists, user, "3", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicSongs, user, "4", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicGenres, user, "5", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicFavorites, user, "6", parent).ConfigureAwait(false));

            return GetResult(list, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetMusicFavorites(Folder parent, User user, InternalItemsQuery query)
        {
            var list = new List<BaseItem>();

            list.Add(await GetUserView(SpecialFolder.MusicFavoriteAlbums, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicFavoriteArtists, user, "1", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MusicFavoriteSongs, user, "2", parent).ConfigureAwait(false));

            return GetResult(list, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetMusicGenres(Folder parent, User user, InternalItemsQuery query)
        {
            var tasks = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos })
                .Where(i => !i.IsFolder)
                .SelectMany(i => i.Genres)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetMusicGenre(i);
                    }
                    catch
                    {
                        // Full exception logged at lower levels
                        _logger.Error("Error getting genre");
                        return null;
                    }

                })
                .Where(i => i != null)
                .Select(i => GetUserView(i.Name, SpecialFolder.MusicGenre, user, i.SortName, parent));

            var genres = await Task.WhenAll(tasks).ConfigureAwait(false);

            return GetResult(genres, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetMusicGenreItems(Folder queryParent, Folder displayParent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(queryParent, user, new[] { CollectionType.Music, CollectionType.MusicVideos })
                .Where(i => !i.IsFolder)
                .Where(i => i.Genres.Contains(displayParent.Name, StringComparer.OrdinalIgnoreCase))
                .OfType<IHasAlbumArtist>()
                .SelectMany(i => i.AlbumArtists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetArtist(i);
                    }
                    catch
                    {
                        // Already logged at lower levels
                        return null;
                    }
                })
                .Where(i => i != null);

            return GetResult(items, queryParent, query);
        }

        private QueryResult<BaseItem> GetMusicAlbumArtists(Folder parent, User user, InternalItemsQuery query)
        {
            var artists = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos })
                .Where(i => !i.IsFolder)
                .OfType<IHasAlbumArtist>()
                .SelectMany(i => i.AlbumArtists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetArtist(i);
                    }
                    catch
                    {
                        // Already logged at lower levels
                        return null;
                    }
                })
                .Where(i => i != null);

            return GetResult(artists, parent, query);
        }

        private QueryResult<BaseItem> GetMusicArtists(Folder parent, User user, InternalItemsQuery query)
        {
            var artists = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos })
                .Where(i => !i.IsFolder)
                .OfType<IHasArtist>()
                .SelectMany(i => i.Artists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetArtist(i);
                    }
                    catch
                    {
                        // Already logged at lower levels
                        return null;
                    }
                })
                .Where(i => i != null);

            return GetResult(artists, parent, query);
        }

        private QueryResult<BaseItem> GetFavoriteArtists(Folder parent, User user, InternalItemsQuery query)
        {
            var artists = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos })
                .Where(i => !i.IsFolder)
                .OfType<IHasAlbumArtist>()
                .SelectMany(i => i.AlbumArtists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetArtist(i);
                    }
                    catch
                    {
                        // Already logged at lower levels
                        return null;
                    }
                })
                .Where(i => i != null && _userDataManager.GetUserData(user.Id, i.GetUserDataKey()).IsFavorite);

            return GetResult(artists, parent, query);
        }

        private QueryResult<BaseItem> GetMusicAlbums(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos }, i => (i is MusicAlbum) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetMusicSongs(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos }, i => (i is Audio.Audio) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetMusicLatest(Folder parent, User user, InternalItemsQuery query)
        {
            var items = _userViewManager.GetLatestItems(new LatestItemsQuery
            {
                UserId = user.Id.ToString("N"),
                Limit = GetSpecialItemsLimit(),
                IncludeItemTypes = new[] { typeof(Audio.Audio).Name },
                ParentId = (parent == null ? null : parent.Id.ToString("N")),
                GroupItems = true

            }).Select(i => i.Item1);

            query.SortBy = new string[] { };

            //var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music, CollectionType.MusicVideos }, i => i is MusicVideo || i is Audio.Audio && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private async Task<QueryResult<BaseItem>> GetMovieFolders(Folder parent, User user, InternalItemsQuery query)
        {
            if (query.Recursive)
            {
                var recursiveItems = GetRecursiveChildren(parent, user,
                    new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty })
                    .Where(i => i is Movie || i is BoxSet);

                //var collections = _collectionManager.CollapseItemsWithinBoxSets(recursiveItems, user).ToList();

                //if (collections.Count > 0)
                //{
                //    recursiveItems.AddRange(_collectionManager.CollapseItemsWithinBoxSets(recursiveItems, user));
                //    recursiveItems = recursiveItems.DistinctBy(i => i.Id).ToList();
                //}

                return GetResult(recursiveItems, parent, query);
            }

            var list = new List<BaseItem>();

            list.Add(await GetUserView(SpecialFolder.MovieResume, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MovieLatest, user, "1", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MovieMovies, user, "2", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MovieCollections, user, "3", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MovieFavorites, user, "4", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.MovieGenres, user, "5", parent).ConfigureAwait(false));

            return GetResult(list, parent, query);
        }

        private QueryResult<BaseItem> GetFavoriteMovies(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty }, i => (i is Movie) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetFavoriteSeries(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => (i is Series) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetFavoriteEpisodes(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => (i is Episode) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetFavoriteSongs(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music }, i => (i is Audio.Audio) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetFavoriteAlbums(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Music }, i => (i is MusicAlbum) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetMovieMovies(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty }, i => (i is Movie) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetMovieCollections(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty }, i => (i is BoxSet) && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetMovieLatest(Folder parent, User user, InternalItemsQuery query)
        {
            query.SortBy = new[] { ItemSortBy.DateCreated, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty }, i => i is Movie && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private QueryResult<BaseItem> GetMovieResume(Folder parent, User user, InternalItemsQuery query)
        {
            query.SortBy = new[] { ItemSortBy.DatePlayed, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;
            query.IsResumable = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty }, i => i is Movie && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private async Task<QueryResult<BaseItem>> GetMovieGenres(Folder parent, User user, InternalItemsQuery query)
        {
            var tasks = GetRecursiveChildren(parent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty })
                .Where(i => i is Movie)
                .SelectMany(i => i.Genres)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetGenre(i);
                    }
                    catch
                    {
                        // Full exception logged at lower levels
                        _logger.Error("Error getting genre");
                        return null;
                    }

                })
                .Where(i => i != null)
                .Select(i => GetUserView(i.Name, SpecialFolder.MovieGenre, user, i.SortName, parent));

            var genres = await Task.WhenAll(tasks).ConfigureAwait(false);

            return GetResult(genres, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetMovieGenreItems(Folder queryParent, Folder displayParent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(queryParent, user, new[] { CollectionType.Movies, CollectionType.BoxSets, string.Empty })
                .Where(i => i is Movie)
                .Where(i => i.Genres.Contains(displayParent.Name, StringComparer.OrdinalIgnoreCase));

            return GetResult(items, queryParent, query);
        }

        private async Task<QueryResult<BaseItem>> GetBoxsetView(Folder parent, User user, InternalItemsQuery query)
        {
            return GetResult(GetMediaFolders(user).SelectMany(i =>
            {
                var hasCollectionType = i as ICollectionFolder;
                Func<BaseItem, bool> filter = b => b is BoxSet;

                if (hasCollectionType != null && string.Equals(hasCollectionType.CollectionType, CollectionType.BoxSets, StringComparison.OrdinalIgnoreCase))
                {
                    return i.GetChildren(user, true).Where(filter);
                }

                return i.GetRecursiveChildren(user, filter);

            }), parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetTvView(Folder parent, User user, InternalItemsQuery query)
        {
            if (query.Recursive)
            {
                var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => (i is Series || i is Season || i is Episode) && FilterItem(i, query));

                return PostFilterAndSort(items, parent, null, query);
            }

            var list = new List<BaseItem>();

            list.Add(await GetUserView(SpecialFolder.TvResume, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvNextUp, user, "1", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvLatest, user, "2", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvShowSeries, user, "3", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvFavoriteSeries, user, "4", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvFavoriteEpisodes, user, "5", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.TvGenres, user, "6", parent).ConfigureAwait(false));

            return GetResult(list, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetGameView(User user, Folder parent, InternalItemsQuery query)
        {
            if (query.Recursive)
            {
                var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Games }, i => FilterItem(i, query));
                return PostFilterAndSort(items, parent, null, query);
            }

            var list = new List<BaseItem>();

            list.Add(await GetUserView(SpecialFolder.LatestGames, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.RecentlyPlayedGames, user, "1", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.GameFavorites, user, "2", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.GameSystems, user, "3", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.GameGenres, user, "4", parent).ConfigureAwait(false));

            return GetResult(list, parent, query);
        }

        private QueryResult<BaseItem> GetLatestGames(Folder parent, User user, InternalItemsQuery query)
        {
            query.SortBy = new[] { ItemSortBy.DateCreated, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Games }, i => i is Game && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private QueryResult<BaseItem> GetRecentlyPlayedGames(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsPlayed = true;
            query.SortBy = new[] { ItemSortBy.DatePlayed, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Games }, i => i is Game && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private QueryResult<BaseItem> GetFavoriteGames(Folder parent, User user, InternalItemsQuery query)
        {
            query.IsFavorite = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Games }, i => i is Game && FilterItem(i, query));
            return PostFilterAndSort(items, parent, null, query);
        }

        private QueryResult<BaseItem> GetTvLatest(Folder parent, User user, InternalItemsQuery query)
        {
            query.SortBy = new[] { ItemSortBy.DateCreated, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => i is Episode && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private QueryResult<BaseItem> GetTvNextUp(Folder parent, InternalItemsQuery query)
        {
            var parentFolders = GetMediaFolders(parent, query.User, new[] { CollectionType.TvShows, string.Empty });

            var result = _tvSeriesManager.GetNextUp(new NextUpQuery
            {
                Limit = query.Limit,
                StartIndex = query.StartIndex,
                UserId = query.User.Id.ToString("N")

            }, parentFolders);

            return result;
        }

        private QueryResult<BaseItem> GetTvResume(Folder parent, User user, InternalItemsQuery query)
        {
            query.SortBy = new[] { ItemSortBy.DatePlayed, ItemSortBy.SortName };
            query.SortOrder = SortOrder.Descending;
            query.IsResumable = true;

            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => i is Episode && FilterItem(i, query));

            return PostFilterAndSort(items, parent, GetSpecialItemsLimit(), query);
        }

        private QueryResult<BaseItem> GetTvSeries(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty }, i => i is Series && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private async Task<QueryResult<BaseItem>> GetTvGenres(Folder parent, User user, InternalItemsQuery query)
        {
            var tasks = GetRecursiveChildren(parent, user, new[] { CollectionType.TvShows, string.Empty })
                .OfType<Series>()
                .SelectMany(i => i.Genres)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetGenre(i);
                    }
                    catch
                    {
                        // Full exception logged at lower levels
                        _logger.Error("Error getting genre");
                        return null;
                    }

                })
                .Where(i => i != null)
                .Select(i => GetUserView(i.Name, SpecialFolder.TvGenre, user, i.SortName, parent));

            var genres = await Task.WhenAll(tasks).ConfigureAwait(false);

            return GetResult(genres, parent, query);
        }

        private async Task<QueryResult<BaseItem>> GetTvGenreItems(Folder queryParent, Folder displayParent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(queryParent, user, new[] { CollectionType.TvShows, string.Empty })
                .Where(i => i is Series)
                .Where(i => i.Genres.Contains(displayParent.Name, StringComparer.OrdinalIgnoreCase));

            return GetResult(items, queryParent, query);
        }

        private QueryResult<BaseItem> GetGameSystems(Folder parent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(parent, user, new[] { CollectionType.Games }, i => i is GameSystem && FilterItem(i, query));

            return PostFilterAndSort(items, parent, null, query);
        }

        private async Task<QueryResult<BaseItem>> GetGameGenreItems(Folder queryParent, Folder displayParent, User user, InternalItemsQuery query)
        {
            var items = GetRecursiveChildren(queryParent, user, new[] { CollectionType.Games },
                i => i is Game && i.Genres.Contains(displayParent.Name, StringComparer.OrdinalIgnoreCase));

            return GetResult(items, queryParent, query);
        }

        private async Task<QueryResult<BaseItem>> GetGameGenres(Folder parent, User user, InternalItemsQuery query)
        {
            var tasks = GetRecursiveChildren(parent, user, new[] { CollectionType.Games })
                .OfType<Game>()
                .SelectMany(i => i.Genres)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(i =>
                {
                    try
                    {
                        return _libraryManager.GetGameGenre(i);
                    }
                    catch
                    {
                        // Full exception logged at lower levels
                        _logger.Error("Error getting game genre");
                        return null;
                    }

                })
                .Where(i => i != null)
                .Select(i => GetUserView(i.Name, SpecialFolder.GameGenre, user, i.SortName, parent));

            var genres = await Task.WhenAll(tasks).ConfigureAwait(false);

            return GetResult(genres, parent, query);
        }

        private QueryResult<BaseItem> GetResult<T>(QueryResult<T> result)
            where T : BaseItem
        {
            return new QueryResult<BaseItem>
            {
                Items = result.Items,
                TotalRecordCount = result.TotalRecordCount
            };
        }

        private QueryResult<BaseItem> GetResult<T>(IEnumerable<T> items,
            BaseItem queryParent,
            InternalItemsQuery query)
            where T : BaseItem
        {
            items = items.Where(i => Filter(i, query.User, query, _userDataManager, _libraryManager));

            return PostFilterAndSort(items, queryParent, null, query, _libraryManager);
        }

        public bool FilterItem(BaseItem item, InternalItemsQuery query)
        {
            return Filter(item, query.User, query, _userDataManager, _libraryManager);
        }

        private QueryResult<BaseItem> PostFilterAndSort(IEnumerable<BaseItem> items,
            BaseItem queryParent,
            int? totalRecordLimit,
            InternalItemsQuery query)
        {
            return PostFilterAndSort(items, queryParent, totalRecordLimit, query, _libraryManager);
        }

        public static QueryResult<BaseItem> PostFilterAndSort(IEnumerable<BaseItem> items,
            BaseItem queryParent,
            int? totalRecordLimit,
            InternalItemsQuery query,
            ILibraryManager libraryManager)
        {
            var user = query.User;

            items = FilterVirtualEpisodes(items,
                query.IsMissing,
                query.IsVirtualUnaired,
                query.IsUnaired);

            items = CollapseBoxSetItemsIfNeeded(items, query, queryParent, user);

            // This must be the last filter
            if (!string.IsNullOrEmpty(query.AdjacentTo))
            {
                items = FilterForAdjacency(items, query.AdjacentTo);
            }

            return Sort(items, totalRecordLimit, query, libraryManager);
        }

        public static IEnumerable<BaseItem> CollapseBoxSetItemsIfNeeded(IEnumerable<BaseItem> items,
            InternalItemsQuery query,
            BaseItem queryParent,
            User user)
        {
            if (CollapseBoxSetItems(query, queryParent, user))
            {
                items = BaseItem.CollectionManager.CollapseItemsWithinBoxSets(items, user);
            }

            items = ApplyPostCollectionCollapseFilters(query, items, user);

            return items;
        }

        private static IEnumerable<BaseItem> ApplyPostCollectionCollapseFilters(InternalItemsQuery request,
            IEnumerable<BaseItem> items,
            User user)
        {
            if (!string.IsNullOrEmpty(request.NameStartsWithOrGreater))
            {
                items = items.Where(i => string.Compare(request.NameStartsWithOrGreater, i.SortName, StringComparison.CurrentCultureIgnoreCase) < 1);
            }
            if (!string.IsNullOrEmpty(request.NameStartsWith))
            {
                items = items.Where(i => string.Compare(request.NameStartsWith, i.SortName.Substring(0, 1), StringComparison.CurrentCultureIgnoreCase) == 0);
            }

            if (!string.IsNullOrEmpty(request.NameLessThan))
            {
                items = items.Where(i => string.Compare(request.NameLessThan, i.SortName, StringComparison.CurrentCultureIgnoreCase) == 1);
            }

            return items;
        }

        private static bool CollapseBoxSetItems(InternalItemsQuery query,
            BaseItem queryParent,
            User user)
        {
            // Could end up stuck in a loop like this
            if (queryParent is BoxSet)
            {
                return false;
            }

            var param = query.CollapseBoxSetItems;

            if (!param.HasValue)
            {
                if (user != null && !user.Configuration.GroupMoviesIntoBoxSets)
                {
                    return false;
                }

                if (query.IncludeItemTypes.Contains("Movie", StringComparer.OrdinalIgnoreCase))
                {
                    param = true;
                }
            }

            return param.HasValue && param.Value && AllowBoxSetCollapsing(query);
        }

        private static bool AllowBoxSetCollapsing(InternalItemsQuery request)
        {
            if (request.IsFavorite.HasValue)
            {
                return false;
            }
            if (request.IsFavoriteOrLiked.HasValue)
            {
                return false;
            }
            if (request.IsLiked.HasValue)
            {
                return false;
            }
            if (request.IsPlayed.HasValue)
            {
                return false;
            }
            if (request.IsResumable.HasValue)
            {
                return false;
            }
            if (request.IsFolder.HasValue)
            {
                return false;
            }

            if (request.AllGenres.Length > 0)
            {
                return false;
            }

            if (request.Genres.Length > 0)
            {
                return false;
            }

            if (request.HasImdbId.HasValue)
            {
                return false;
            }

            if (request.HasOfficialRating.HasValue)
            {
                return false;
            }

            if (request.HasOverview.HasValue)
            {
                return false;
            }

            if (request.HasParentalRating.HasValue)
            {
                return false;
            }

            if (request.HasSpecialFeature.HasValue)
            {
                return false;
            }

            if (request.HasSubtitles.HasValue)
            {
                return false;
            }

            if (request.HasThemeSong.HasValue)
            {
                return false;
            }

            if (request.HasThemeVideo.HasValue)
            {
                return false;
            }

            if (request.HasTmdbId.HasValue)
            {
                return false;
            }

            if (request.HasTrailer.HasValue)
            {
                return false;
            }

            if (request.ImageTypes.Length > 0)
            {
                return false;
            }

            if (request.Is3D.HasValue)
            {
                return false;
            }

            if (request.IsHD.HasValue)
            {
                return false;
            }

            if (request.IsInBoxSet.HasValue)
            {
                return false;
            }

            if (request.IsLocked.HasValue)
            {
                return false;
            }

            if (request.IsPlaceHolder.HasValue)
            {
                return false;
            }

            if (request.IsPlayed.HasValue)
            {
                return false;
            }

            if (request.IsUnidentified.HasValue)
            {
                return false;
            }

            if (request.IsYearMismatched.HasValue)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.Person))
            {
                return false;
            }

            if (request.Studios.Length > 0)
            {
                return false;
            }

            if (request.VideoTypes.Length > 0)
            {
                return false;
            }

            if (request.Years.Length > 0)
            {
                return false;
            }

            if (request.Tags.Length > 0)
            {
                return false;
            }

            if (request.OfficialRatings.Length > 0)
            {
                return false;
            }

            return true;
        }

        public static IEnumerable<BaseItem> FilterVirtualEpisodes(
            IEnumerable<BaseItem> items,
            bool? isMissing,
            bool? isVirtualUnaired,
            bool? isUnaired)
        {
            items = FilterVirtualSeasons(items, isMissing, isVirtualUnaired, isUnaired);

            if (isMissing.HasValue)
            {
                var val = isMissing.Value;
                items = items.Where(i =>
                {
                    var e = i as Episode;
                    if (e != null)
                    {
                        return e.IsMissingEpisode == val;
                    }
                    return true;
                });
            }

            if (isUnaired.HasValue)
            {
                var val = isUnaired.Value;
                items = items.Where(i =>
                {
                    var e = i as Episode;
                    if (e != null)
                    {
                        return e.IsUnaired == val;
                    }
                    return true;
                });
            }

            if (isVirtualUnaired.HasValue)
            {
                var val = isVirtualUnaired.Value;
                items = items.Where(i =>
                {
                    var e = i as Episode;
                    if (e != null)
                    {
                        return e.IsVirtualUnaired == val;
                    }
                    return true;
                });
            }

            return items;
        }

        private static IEnumerable<BaseItem> FilterVirtualSeasons(
            IEnumerable<BaseItem> items,
            bool? isMissing,
            bool? isVirtualUnaired,
            bool? isUnaired)
        {
            if (isMissing.HasValue && isVirtualUnaired.HasValue)
            {
                if (!isMissing.Value && !isVirtualUnaired.Value)
                {
                    return items.Where(i =>
                    {
                        var e = i as Season;
                        if (e != null)
                        {
                            return !e.IsMissingOrVirtualUnaired;
                        }
                        return true;
                    });
                }
            }

            if (isMissing.HasValue)
            {
                var val = isMissing.Value;
                items = items.Where(i =>
                {
                    var e = i as Season;
                    if (e != null)
                    {
                        return e.IsMissingSeason == val;
                    }
                    return true;
                });
            }

            if (isUnaired.HasValue)
            {
                var val = isUnaired.Value;
                items = items.Where(i =>
                {
                    var e = i as Season;
                    if (e != null)
                    {
                        return e.IsUnaired == val;
                    }
                    return true;
                });
            }

            if (isVirtualUnaired.HasValue)
            {
                var val = isVirtualUnaired.Value;
                items = items.Where(i =>
                {
                    var e = i as Season;
                    if (e != null)
                    {
                        return e.IsVirtualUnaired == val;
                    }
                    return true;
                });
            }

            return items;
        }

        public static QueryResult<BaseItem> Sort(IEnumerable<BaseItem> items,
            int? totalRecordLimit,
            InternalItemsQuery query,
            ILibraryManager libraryManager)
        {
            var user = query.User;

            items = libraryManager.ReplaceVideosWithPrimaryVersions(items);

            if (query.SortBy.Length > 0)
            {
                items = libraryManager.Sort(items, user, query.SortBy, query.SortOrder);
            }

            var itemsArray = totalRecordLimit.HasValue ? items.Take(totalRecordLimit.Value).ToArray() : items.ToArray();
            var totalCount = itemsArray.Length;

            if (query.Limit.HasValue)
            {
                itemsArray = itemsArray.Skip(query.StartIndex ?? 0).Take(query.Limit.Value).ToArray();
            }
            else if (query.StartIndex.HasValue)
            {
                itemsArray = itemsArray.Skip(query.StartIndex.Value).ToArray();
            }

            return new QueryResult<BaseItem>
            {
                TotalRecordCount = totalCount,
                Items = itemsArray
            };
        }

        public static bool Filter(BaseItem item, User user, InternalItemsQuery query, IUserDataManager userDataManager, ILibraryManager libraryManager)
        {
            if (query.MediaTypes.Length > 0 && !query.MediaTypes.Contains(item.MediaType ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.IncludeItemTypes.Length > 0 && !query.IncludeItemTypes.Contains(item.GetClientTypeName(), StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.ExcludeItemTypes.Length > 0 && query.ExcludeItemTypes.Contains(item.GetClientTypeName(), StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.IsFolder.HasValue && query.IsFolder.Value != item.IsFolder)
            {
                return false;
            }

            if (query.Filter != null && !query.Filter(item))
            {
                return false;
            }

            UserItemData userData = null;

            if (query.IsLiked.HasValue)
            {
                userData = userData ?? userDataManager.GetUserData(user.Id, item.GetUserDataKey());

                if (!userData.Likes.HasValue || userData.Likes != query.IsLiked.Value)
                {
                    return false;
                }
            }

            if (query.IsFavoriteOrLiked.HasValue)
            {
                userData = userData ?? userDataManager.GetUserData(user.Id, item.GetUserDataKey());
                var isFavoriteOrLiked = userData.IsFavorite || (userData.Likes ?? false);

                if (isFavoriteOrLiked != query.IsFavoriteOrLiked.Value)
                {
                    return false;
                }
            }

            if (query.IsFavorite.HasValue)
            {
                userData = userData ?? userDataManager.GetUserData(user.Id, item.GetUserDataKey());

                if (userData.IsFavorite != query.IsFavorite.Value)
                {
                    return false;
                }
            }

            if (query.IsResumable.HasValue)
            {
                userData = userData ?? userDataManager.GetUserData(user.Id, item.GetUserDataKey());
                var isResumable = userData.PlaybackPositionTicks > 0;

                if (isResumable != query.IsResumable.Value)
                {
                    return false;
                }
            }

            if (query.IsPlayed.HasValue)
            {
                if (item.IsPlayed(user) != query.IsPlayed.Value)
                {
                    return false;
                }
            }

            if (query.IsInBoxSet.HasValue)
            {
                var val = query.IsInBoxSet.Value;
                if (item.Parents.OfType<BoxSet>().Any() != val)
                {
                    return false;
                }
            }

            // Filter by Video3DFormat
            if (query.Is3D.HasValue)
            {
                var val = query.Is3D.Value;
                var video = item as Video;

                if (video == null || val != video.Video3DFormat.HasValue)
                {
                    return false;
                }
            }

            if (query.IsHD.HasValue)
            {
                var val = query.IsHD.Value;
                var video = item as Video;

                if (video == null || val != video.IsHD)
                {
                    return false;
                }
            }

            if (query.IsUnidentified.HasValue)
            {
                var val = query.IsUnidentified.Value;
                if (item.IsUnidentified != val)
                {
                    return false;
                }
            }

            if (query.IsLocked.HasValue)
            {
                var val = query.IsLocked.Value;
                if (item.IsLocked != val)
                {
                    return false;
                }
            }

            if (query.HasOverview.HasValue)
            {
                var filterValue = query.HasOverview.Value;

                var hasValue = !string.IsNullOrEmpty(item.Overview);

                if (hasValue != filterValue)
                {
                    return false;
                }
            }

            if (query.HasImdbId.HasValue)
            {
                var filterValue = query.HasImdbId.Value;

                var hasValue = !string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.Imdb));

                if (hasValue != filterValue)
                {
                    return false;
                }
            }

            if (query.HasTmdbId.HasValue)
            {
                var filterValue = query.HasTmdbId.Value;

                var hasValue = !string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.Tmdb));

                if (hasValue != filterValue)
                {
                    return false;
                }
            }

            if (query.HasTvdbId.HasValue)
            {
                var filterValue = query.HasTvdbId.Value;

                var hasValue = !string.IsNullOrEmpty(item.GetProviderId(MetadataProviders.Tvdb));

                if (hasValue != filterValue)
                {
                    return false;
                }
            }

            if (query.IsYearMismatched.HasValue)
            {
                var filterValue = query.IsYearMismatched.Value;

                if (IsYearMismatched(item, libraryManager) != filterValue)
                {
                    return false;
                }
            }

            if (query.HasOfficialRating.HasValue)
            {
                var filterValue = query.HasOfficialRating.Value;

                var hasValue = !string.IsNullOrEmpty(item.OfficialRating);

                if (hasValue != filterValue)
                {
                    return false;
                }
            }

            if (query.IsPlaceHolder.HasValue)
            {
                var filterValue = query.IsPlaceHolder.Value;

                var isPlaceHolder = false;

                var hasPlaceHolder = item as ISupportsPlaceHolders;

                if (hasPlaceHolder != null)
                {
                    isPlaceHolder = hasPlaceHolder.IsPlaceHolder;
                }

                if (isPlaceHolder != filterValue)
                {
                    return false;
                }
            }

            if (query.HasSpecialFeature.HasValue)
            {
                var filterValue = query.HasSpecialFeature.Value;

                var movie = item as IHasSpecialFeatures;

                if (movie != null)
                {
                    var ok = filterValue
                        ? movie.SpecialFeatureIds.Count > 0
                        : movie.SpecialFeatureIds.Count == 0;

                    if (!ok)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (query.HasSubtitles.HasValue)
            {
                var val = query.HasSubtitles.Value;

                var video = item as Video;

                if (video == null || val != video.HasSubtitles)
                {
                    return false;
                }
            }

            if (query.HasParentalRating.HasValue)
            {
                var val = query.HasParentalRating.Value;

                var rating = item.CustomRating;

                if (string.IsNullOrEmpty(rating))
                {
                    rating = item.OfficialRating;
                }

                if (val)
                {
                    if (string.IsNullOrEmpty(rating))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(rating))
                    {
                        return false;
                    }
                }
            }

            if (query.HasTrailer.HasValue)
            {
                var val = query.HasTrailer.Value;
                var trailerCount = 0;

                var hasTrailers = item as IHasTrailers;
                if (hasTrailers != null)
                {
                    trailerCount = hasTrailers.GetTrailerIds().Count;
                }

                var ok = val ? trailerCount > 0 : trailerCount == 0;

                if (!ok)
                {
                    return false;
                }
            }

            if (query.HasThemeSong.HasValue)
            {
                var filterValue = query.HasThemeSong.Value;

                var themeCount = 0;
                var iHasThemeMedia = item as IHasThemeMedia;

                if (iHasThemeMedia != null)
                {
                    themeCount = iHasThemeMedia.ThemeSongIds.Count;
                }
                var ok = filterValue ? themeCount > 0 : themeCount == 0;

                if (!ok)
                {
                    return false;
                }
            }

            if (query.HasThemeVideo.HasValue)
            {
                var filterValue = query.HasThemeVideo.Value;

                var themeCount = 0;
                var iHasThemeMedia = item as IHasThemeMedia;

                if (iHasThemeMedia != null)
                {
                    themeCount = iHasThemeMedia.ThemeVideoIds.Count;
                }
                var ok = filterValue ? themeCount > 0 : themeCount == 0;

                if (!ok)
                {
                    return false;
                }
            }

            // Apply genre filter
            if (query.Genres.Length > 0 && !(query.Genres.Any(v => item.Genres.Contains(v, StringComparer.OrdinalIgnoreCase))))
            {
                return false;
            }

            // Apply genre filter
            if (query.AllGenres.Length > 0 && !query.AllGenres.All(v => item.Genres.Contains(v, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Filter by VideoType
            if (query.VideoTypes.Length > 0)
            {
                var video = item as Video;
                if (video == null || !query.VideoTypes.Contains(video.VideoType))
                {
                    return false;
                }
            }

            if (query.ImageTypes.Length > 0 && !query.ImageTypes.Any(item.HasImage))
            {
                return false;
            }

            // Apply studio filter
            if (query.Studios.Length > 0 && !query.Studios.Any(v => item.Studios.Contains(v, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Apply year filter
            if (query.Years.Length > 0)
            {
                if (!(item.ProductionYear.HasValue && query.Years.Contains(item.ProductionYear.Value)))
                {
                    return false;
                }
            }

            // Apply official rating filter
            if (query.OfficialRatings.Length > 0 && !query.OfficialRatings.Contains(item.OfficialRating ?? string.Empty))
            {
                return false;
            }

            // Apply person filter
            if (!string.IsNullOrEmpty(query.Person))
            {
                var personTypes = query.PersonTypes;

                if (personTypes.Length == 0)
                {
                    if (!(item.People.Any(p => string.Equals(p.Name, query.Person, StringComparison.OrdinalIgnoreCase))))
                    {
                        return false;
                    }
                }
                else
                {
                    var types = personTypes;

                    var ok = new[] { item }.Any(i =>
                            i.People != null &&
                            i.People.Any(p =>
                                p.Name.Equals(query.Person, StringComparison.OrdinalIgnoreCase) && (types.Contains(p.Type, StringComparer.OrdinalIgnoreCase) || types.Contains(p.Role, StringComparer.OrdinalIgnoreCase))));

                    if (!ok)
                    {
                        return false;
                    }
                }
            }

            // Apply tag filter
            var tags = query.Tags;
            if (tags.Length > 0)
            {
                var hasTags = item as IHasTags;
                if (hasTags == null)
                {
                    return false;
                }
                if (!(tags.Any(v => hasTags.Tags.Contains(v, StringComparer.OrdinalIgnoreCase))))
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<Folder> GetMediaFolders(User user)
        {
            var excludeFolderIds = user.Configuration.ExcludeFoldersFromGrouping.Select(i => new Guid(i)).ToList();

            return user.RootFolder
                .GetChildren(user, true, true)
                .OfType<Folder>()
                .Where(i => !excludeFolderIds.Contains(i.Id) && !UserView.IsExcludedFromGrouping(i));
        }

        private IEnumerable<Folder> GetMediaFolders(User user, IEnumerable<string> viewTypes)
        {
            return GetMediaFolders(user)
                .Where(i =>
                {
                    var folder = i as ICollectionFolder;

                    return folder != null && viewTypes.Contains(folder.CollectionType ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                });
        }

        private IEnumerable<Folder> GetMediaFolders(Folder parent, User user, IEnumerable<string> viewTypes)
        {
            if (parent == null || parent is UserView)
            {
                return GetMediaFolders(user, viewTypes);
            }

            return new[] { parent };
        }

        private IEnumerable<BaseItem> GetRecursiveChildren(Folder parent, User user, IEnumerable<string> viewTypes)
        {
            if (parent == null || parent is UserView)
            {
                return GetMediaFolders(user, viewTypes).SelectMany(i => i.GetRecursiveChildren(user));
            }

            return parent.GetRecursiveChildren(user);
        }

        private IEnumerable<BaseItem> GetRecursiveChildren(Folder parent, User user, IEnumerable<string> viewTypes, Func<BaseItem, bool> filter)
        {
            if (parent == null || parent is UserView)
            {
                return GetMediaFolders(user, viewTypes).SelectMany(i => i.GetRecursiveChildren(user, filter));
            }

            return parent.GetRecursiveChildren(user, filter);
        }

        private async Task<IEnumerable<BaseItem>> GetLiveTvFolders(User user)
        {
            var list = new List<BaseItem>();

            var parent = user.RootFolder;

            //list.Add(await GetUserView(SpecialFolder.LiveTvNowPlaying, user, "0", parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.LiveTvChannels, user, string.Empty, parent).ConfigureAwait(false));
            list.Add(await GetUserView(SpecialFolder.LiveTvRecordingGroups, user, string.Empty, parent).ConfigureAwait(false));

            return list;
        }

        private async Task<UserView> GetUserView(string name, string type, User user, string sortName, BaseItem parent)
        {
            var view = await _userViewManager.GetUserView(name, parent.Id.ToString("N"), type, user, sortName, CancellationToken.None)
                        .ConfigureAwait(false);

            return view;
        }

        private async Task<UserView> GetUserView(string type, User user, string sortName, BaseItem parent)
        {
            var view = await _userViewManager.GetUserView(parent.Id.ToString("N"), type, user, sortName, CancellationToken.None)
                        .ConfigureAwait(false);

            return view;
        }

        public static bool IsYearMismatched(BaseItem item, ILibraryManager libraryManager)
        {
            if (item.ProductionYear.HasValue)
            {
                var path = item.Path;

                if (!string.IsNullOrEmpty(path))
                {
                    var info = libraryManager.ParseName(Path.GetFileName(path));
                    var yearInName = info.Year;

                    // Go up a level if we didn't get a year
                    if (!yearInName.HasValue)
                    {
                        info = libraryManager.ParseName(Path.GetFileName(Path.GetDirectoryName(path)));
                        yearInName = info.Year;
                    }

                    if (yearInName.HasValue)
                    {
                        return yearInName.Value != item.ProductionYear.Value;
                    }
                }
            }

            return false;
        }

        public static IEnumerable<BaseItem> FilterForAdjacency(IEnumerable<BaseItem> items, string adjacentToId)
        {
            var list = items.ToList();

            var adjacentToIdGuid = new Guid(adjacentToId);
            var adjacentToItem = list.FirstOrDefault(i => i.Id == adjacentToIdGuid);

            var index = list.IndexOf(adjacentToItem);

            var previousId = Guid.Empty;
            var nextId = Guid.Empty;

            if (index > 0)
            {
                previousId = list[index - 1].Id;
            }

            if (index < list.Count - 1)
            {
                nextId = list[index + 1].Id;
            }

            return list.Where(i => i.Id == previousId || i.Id == nextId || i.Id == adjacentToIdGuid);
        }
    }
}
