﻿using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Library
{
    public class UserViewManager : IUserViewManager
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILocalizationManager _localizationManager;
        private readonly IUserManager _userManager;

        private readonly IChannelManager _channelManager;
        private readonly ILiveTvManager _liveTvManager;
        private readonly IPlaylistManager _playlists;
        private readonly ICollectionManager _collectionManager;
        private readonly IServerConfigurationManager _config;

        public UserViewManager(ILibraryManager libraryManager, ILocalizationManager localizationManager, IUserManager userManager, IChannelManager channelManager, ILiveTvManager liveTvManager, IPlaylistManager playlists, ICollectionManager collectionManager, IServerConfigurationManager config)
        {
            _libraryManager = libraryManager;
            _localizationManager = localizationManager;
            _userManager = userManager;
            _channelManager = channelManager;
            _liveTvManager = liveTvManager;
            _playlists = playlists;
            _collectionManager = collectionManager;
            _config = config;
        }

        public async Task<IEnumerable<Folder>> GetUserViews(UserViewQuery query, CancellationToken cancellationToken)
        {
            var user = _userManager.GetUserById(query.UserId);

            var folders = user.RootFolder
                .GetChildren(user, true)
                .OfType<Folder>()
                .ToList();

            var excludeFolderIds = user.Configuration.ExcludeFoldersFromGrouping.Select(i => new Guid(i)).ToList();
            var plainFolderIds = user.Configuration.PlainFolderViews.Select(i => new Guid(i)).ToList();

            var standaloneFolders = folders
                .Where(i => UserView.IsExcludedFromGrouping(i) || excludeFolderIds.Contains(i.Id))
                .ToList();

            var foldersWithViewTypes = folders
                .Except(standaloneFolders)
                .OfType<ICollectionFolder>()
                .ToList();

            var list = new List<Folder>();

            if (_config.Configuration.EnableUserSpecificUserViews)
            {
                foreach (var folder in standaloneFolders)
                {
                    var collectionFolder = folder as ICollectionFolder;
                    var folderViewType = collectionFolder == null ? null : collectionFolder.CollectionType;

                    if (plainFolderIds.Contains(folder.Id))
                    {
                        list.Add(await GetUserView(folder.Id, folder.Name, folderViewType, false, string.Empty, user, cancellationToken).ConfigureAwait(false));
                    }
                    else if (!string.IsNullOrWhiteSpace(folderViewType))
                    {
                        list.Add(await GetUserView(folder.Id, folder.Name, folderViewType, true, string.Empty, user, cancellationToken).ConfigureAwait(false));
                    }
                    else
                    {
                        list.Add(folder);
                    }
                }
            }
            else
            {
                list.AddRange(standaloneFolders);
            }

            var parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(i.CollectionType))
                .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.TvShows, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.Music, StringComparison.OrdinalIgnoreCase) || string.Equals(i.CollectionType, CollectionType.MusicVideos, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(i.CollectionType))
                .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.Music, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.Movies, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(i.CollectionType))
               .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.Movies, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.Games, StringComparison.OrdinalIgnoreCase))
               .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.Games, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.BoxSets, StringComparison.OrdinalIgnoreCase))
               .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.BoxSets, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            parents = foldersWithViewTypes.Where(i => string.Equals(i.CollectionType, CollectionType.Playlists, StringComparison.OrdinalIgnoreCase))
               .ToList();

            if (parents.Count > 0)
            {
                list.Add(await GetUserView(parents, list, CollectionType.Playlists, string.Empty, user, cancellationToken).ConfigureAwait(false));
            }

            if (user.Configuration.DisplayFoldersView)
            {
                list.Add(await GetUserView(new List<ICollectionFolder>(), list, CollectionType.Folders, "zz_" + CollectionType.Folders, user, cancellationToken).ConfigureAwait(false));
            }

            if (query.IncludeExternalContent)
            {
                var channelResult = await _channelManager.GetChannels(new ChannelQuery
                {
                    UserId = query.UserId

                }, cancellationToken).ConfigureAwait(false);

                var channels = channelResult.Items;

                var embeddedChannels = channels
                    .Where(i => user.Configuration.DisplayChannelsWithinViews.Contains(i.Id))
                    .ToList();

                list.AddRange(embeddedChannels.Select(i => _channelManager.GetChannel(i.Id)));

                if (channels.Length > embeddedChannels.Count)
                {
                    list.Add(await _channelManager.GetInternalChannelFolder(query.UserId, cancellationToken).ConfigureAwait(false));
                }

                if (_liveTvManager.GetEnabledUsers().Select(i => i.Id.ToString("N")).Contains(query.UserId))
                {
                    //list.Add(await _liveTvManager.GetInternalLiveTvFolder(query.UserId, cancellationToken).ConfigureAwait(false));
                    list.Add(await GetUserView(new List<ICollectionFolder>(), list, CollectionType.LiveTv, string.Empty, user, cancellationToken).ConfigureAwait(false));
                }
            }

            var sorted = _libraryManager.Sort(list, user, new[] { ItemSortBy.SortName }, SortOrder.Ascending).ToList();

            var orders = user.Configuration.OrderedViews.ToList();

            return list
                .OrderBy(i =>
                {
                    var index = orders.IndexOf(i.Id.ToString("N"));

                    return index == -1 ? int.MaxValue : index;
                })
                .ThenBy(sorted.IndexOf)
                .ThenBy(i => i.SortName);
        }

        public Task<UserView> GetUserSubView(string name, string parentId, string type, User user, string sortName, CancellationToken cancellationToken)
        {
            var uniqueId = parentId + "subview" + type;

            return _libraryManager.GetNamedView(user, name, parentId, type, sortName, uniqueId, cancellationToken);
        }

        public Task<UserView> GetUserSubView(string parentId, string type, User user, string sortName, CancellationToken cancellationToken)
        {
            var name = _localizationManager.GetLocalizedString("ViewType" + type);

            return GetUserSubView(name, parentId, type, user, sortName, cancellationToken);
        }

        public async Task<UserView> GetUserView(List<ICollectionFolder> parents, List<Folder> currentViews, string viewType, string sortName, User user, CancellationToken cancellationToken)
        {
            var name = _localizationManager.GetLocalizedString("ViewType" + viewType);

            if (parents.Count == 1 && parents.All(i => string.Equals(i.CollectionType, viewType, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(parents[0].Name))
                {
                    name = parents[0].Name;
                }

                var parentId = parents[0].Id;

                var enableRichView = !user.Configuration.PlainFolderViews.Contains(parentId.ToString("N"), StringComparer.OrdinalIgnoreCase);

                if (!enableRichView || currentViews.OfType<UserView>().Any(i => string.Equals(i.ViewType, viewType, StringComparison.OrdinalIgnoreCase) || string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return await GetUserView(parentId, name, viewType, enableRichView, sortName, user, cancellationToken).ConfigureAwait(false);
                }

                if (_config.Configuration.EnableUserSpecificUserViews)
                {
                    viewType = enableRichView ? viewType : null;
                    var view = await _libraryManager.GetNamedView(user, name, viewType, sortName, cancellationToken).ConfigureAwait(false);

                    if (view.ParentId != parentId)
                    {
                        view.ParentId = parentId;
                        await view.UpdateToRepository(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                    }
                    return view;
                }

                return await _libraryManager.GetNamedView(user, name, viewType, sortName, cancellationToken).ConfigureAwait(false);
            }

            return await _libraryManager.GetNamedView(user, name, viewType, sortName, cancellationToken).ConfigureAwait(false);
        }

        public Task<UserView> GetUserView(Guid parentId, string name, string viewType, bool enableRichView, string sortName, User user, CancellationToken cancellationToken)
        {
            viewType = enableRichView ? viewType : null;
            return _libraryManager.GetNamedView(user, name, parentId.ToString("N"), viewType, sortName, null, cancellationToken);
        }

        public List<Tuple<BaseItem, List<BaseItem>>> GetLatestItems(LatestItemsQuery request)
        {
            var user = _userManager.GetUserById(request.UserId);

            var includeTypes = request.IncludeItemTypes;

            var currentUser = user;

            Func<BaseItem, bool> filter = i =>
            {
                if (includeTypes.Length > 0)
                {
                    if (!includeTypes.Contains(i.GetType().Name, StringComparer.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                if (request.IsPlayed.HasValue)
                {
                    var val = request.IsPlayed.Value;
                    if (i is Video && i.IsPlayed(currentUser) != val)
                    {
                        return false;
                    }
                }

                return i.LocationType != LocationType.Virtual && !i.IsFolder;
            };

            // Avoid implicitly captured closure
            var libraryItems = string.IsNullOrEmpty(request.ParentId) && user != null ?
                GetItemsConfiguredForLatest(user, filter) :
                GetAllLibraryItems(request.UserId, _userManager, _libraryManager, request.ParentId, filter);

            libraryItems = libraryItems.OrderByDescending(i => i.DateCreated);

            if (request.IsPlayed.HasValue)
            {
                var takeLimit = (request.Limit ?? 20) * 20;
                libraryItems = libraryItems.Take(takeLimit);
            }

            // Avoid implicitly captured closure
            var items = libraryItems
                .ToList();

            var list = new List<Tuple<BaseItem, List<BaseItem>>>();

            foreach (var item in items)
            {
                // Only grab the index container for media
                var container = item.IsFolder || !request.GroupItems ? null : item.LatestItemsIndexContainer;

                if (container == null)
                {
                    list.Add(new Tuple<BaseItem, List<BaseItem>>(null, new List<BaseItem> { item }));
                }
                else
                {
                    var current = list.FirstOrDefault(i => i.Item1 != null && i.Item1.Id == container.Id);

                    if (current != null)
                    {
                        current.Item2.Add(item);
                    }
                    else
                    {
                        list.Add(new Tuple<BaseItem, List<BaseItem>>(container, new List<BaseItem> { item }));
                    }
                }

                if (list.Count >= request.Limit)
                {
                    break;
                }
            }

            return list;
        }

        protected IList<BaseItem> GetAllLibraryItems(string userId, IUserManager userManager, ILibraryManager libraryManager, string parentId, Func<BaseItem, bool> filter)
        {
            if (!string.IsNullOrEmpty(parentId))
            {
                var folder = (Folder)libraryManager.GetItemById(new Guid(parentId));

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var user = userManager.GetUserById(userId);

                    if (user == null)
                    {
                        throw new ArgumentException("User not found");
                    }

                    return folder
                        .GetRecursiveChildren(user, filter)
                        .ToList();
                }

                return folder
                    .GetRecursiveChildren(filter);
            }
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var user = userManager.GetUserById(userId);

                if (user == null)
                {
                    throw new ArgumentException("User not found");
                }

                return user
                    .RootFolder
                    .GetRecursiveChildren(user, filter)
                    .ToList();
            }

            return libraryManager
                .RootFolder
                .GetRecursiveChildren(filter);
        }

        private IEnumerable<BaseItem> GetItemsConfiguredForLatest(User user, Func<BaseItem, bool> filter)
        {
            // Avoid implicitly captured closure
            var currentUser = user;

            return user.RootFolder.GetChildren(user, true)
                .OfType<Folder>()
                .Where(i => !user.Configuration.LatestItemsExcludes.Contains(i.Id.ToString("N")))
                .SelectMany(i => i.GetRecursiveChildren(currentUser, filter))
                .DistinctBy(i => i.Id);
        }
    }
}
