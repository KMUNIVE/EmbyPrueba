﻿using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Events;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Sync;
using MediaBrowser.Model.Users;
using MoreLinq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Sync
{
    public class SyncManager : ISyncManager
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ISyncRepository _repo;
        private readonly IImageProcessor _imageProcessor;
        private readonly ILogger _logger;
        private readonly IUserManager _userManager;
        private readonly Func<IDtoService> _dtoService;
        private readonly IApplicationHost _appHost;
        private readonly ITVSeriesManager _tvSeriesManager;
        private readonly Func<IMediaEncoder> _mediaEncoder;
        private readonly IFileSystem _fileSystem;
        private readonly Func<ISubtitleEncoder> _subtitleEncoder;
        private readonly IConfigurationManager _config;
        private readonly IUserDataManager _userDataManager;
        private readonly Func<IMediaSourceManager> _mediaSourceManager;
        private readonly IJsonSerializer _json;

        private ISyncProvider[] _providers = { };

        public event EventHandler<GenericEventArgs<SyncJobCreationResult>> SyncJobCreated;
        public event EventHandler<GenericEventArgs<SyncJob>> SyncJobCancelled;
        public event EventHandler<GenericEventArgs<SyncJob>> SyncJobUpdated;
        public event EventHandler<GenericEventArgs<SyncJobItem>> SyncJobItemUpdated;
        public event EventHandler<GenericEventArgs<SyncJobItem>> SyncJobItemCreated;

        public SyncManager(ILibraryManager libraryManager, ISyncRepository repo, IImageProcessor imageProcessor, ILogger logger, IUserManager userManager, Func<IDtoService> dtoService, IApplicationHost appHost, ITVSeriesManager tvSeriesManager, Func<IMediaEncoder> mediaEncoder, IFileSystem fileSystem, Func<ISubtitleEncoder> subtitleEncoder, IConfigurationManager config, IUserDataManager userDataManager, Func<IMediaSourceManager> mediaSourceManager, IJsonSerializer json)
        {
            _libraryManager = libraryManager;
            _repo = repo;
            _imageProcessor = imageProcessor;
            _logger = logger;
            _userManager = userManager;
            _dtoService = dtoService;
            _appHost = appHost;
            _tvSeriesManager = tvSeriesManager;
            _mediaEncoder = mediaEncoder;
            _fileSystem = fileSystem;
            _subtitleEncoder = subtitleEncoder;
            _config = config;
            _userDataManager = userDataManager;
            _mediaSourceManager = mediaSourceManager;
            _json = json;
        }

        public void AddParts(IEnumerable<ISyncProvider> providers)
        {
            _providers = providers.ToArray();
        }

        public IEnumerable<IServerSyncProvider> ServerSyncProviders
        {
            get { return _providers.OfType<IServerSyncProvider>(); }
        }

        private readonly ConcurrentDictionary<string, ISyncDataProvider> _dataProviders =
            new ConcurrentDictionary<string, ISyncDataProvider>(StringComparer.OrdinalIgnoreCase);
 
        public ISyncDataProvider GetDataProvider(IServerSyncProvider provider, SyncTarget target)
        {
            return _dataProviders.GetOrAdd(target.Id, key => new TargetDataProvider(provider, target, _appHost.SystemId, _logger, _json, _fileSystem, _config.CommonApplicationPaths));
        }

        public async Task<SyncJobCreationResult> CreateJob(SyncJobRequest request)
        {
            var processor = GetSyncJobProcessor();

            var user = _userManager.GetUserById(request.UserId);

            var items = (await processor
                .GetItemsForSync(request.Category, request.ParentId, request.ItemIds, user, request.UnwatchedOnly).ConfigureAwait(false))
                .ToList();

            if (items.Any(i => !SupportsSync(i)))
            {
                throw new ArgumentException("Item does not support sync.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.ItemIds.Count == 1)
                {
                    request.Name = GetDefaultName(_libraryManager.GetItemById(request.ItemIds[0]));
                }
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Please supply a name for the sync job.");
            }

            var target = GetSyncTargets(request.UserId)
                .FirstOrDefault(i => string.Equals(request.TargetId, i.Id));

            if (target == null)
            {
                throw new ArgumentException("Sync target not found.");
            }

            var jobId = Guid.NewGuid().ToString("N");

            var job = new SyncJob
            {
                Id = jobId,
                Name = request.Name,
                TargetId = target.Id,
                UserId = request.UserId,
                UnwatchedOnly = request.UnwatchedOnly,
                ItemLimit = request.ItemLimit,
                RequestedItemIds = request.ItemIds ?? new List<string> { },
                DateCreated = DateTime.UtcNow,
                DateLastModified = DateTime.UtcNow,
                SyncNewContent = request.SyncNewContent,
                ItemCount = items.Count,
                Category = request.Category,
                ParentId = request.ParentId
            };

            if (!string.IsNullOrWhiteSpace(request.Quality))
            {
                job.Quality = (SyncQuality)Enum.Parse(typeof(SyncQuality), request.Quality, true);
            }

            if (!request.Category.HasValue && request.ItemIds != null)
            {
                var requestedItems = request.ItemIds
                    .Select(_libraryManager.GetItemById)
                    .Where(i => i != null);

                // It's just a static list
                if (!requestedItems.Any(i => i.IsFolder || i is IItemByName))
                {
                    job.SyncNewContent = false;
                }
            }

            await _repo.Create(job).ConfigureAwait(false);

            await processor.EnsureJobItems(job).ConfigureAwait(false);

            // If it already has a converting status then is must have been aborted during conversion
            var jobItemsResult = _repo.GetJobItems(new SyncJobItemQuery
            {
                Statuses = new List<SyncJobItemStatus> { SyncJobItemStatus.Queued, SyncJobItemStatus.Converting },
                JobId = jobId
            });

            await processor.SyncJobItems(jobItemsResult.Items, false, new Progress<double>(), CancellationToken.None)
                    .ConfigureAwait(false);

            jobItemsResult = _repo.GetJobItems(new SyncJobItemQuery
            {
                Statuses = new List<SyncJobItemStatus> { SyncJobItemStatus.Queued, SyncJobItemStatus.Converting },
                JobId = jobId
            });

            var returnResult = new SyncJobCreationResult
            {
                Job = GetJob(jobId),
                JobItems = jobItemsResult.Items.ToList()
            };

            if (SyncJobCreated != null)
            {
                EventHelper.FireEventIfNotNull(SyncJobCreated, this, new GenericEventArgs<SyncJobCreationResult>
                {
                    Argument = returnResult

                }, _logger);
            }

            return returnResult;
        }

        public async Task UpdateJob(SyncJob job)
        {
            // Get fresh from the db and only update the fields that are supported to be changed.
            var instance = _repo.GetJob(job.Id);

            instance.Name = job.Name;
            instance.Quality = job.Quality;
            instance.UnwatchedOnly = job.UnwatchedOnly;
            instance.SyncNewContent = job.SyncNewContent;
            instance.ItemLimit = job.ItemLimit;

            await _repo.Update(instance).ConfigureAwait(false);

            OnSyncJobUpdated(instance);
        }

        internal void OnSyncJobUpdated(SyncJob job)
        {
            if (SyncJobUpdated != null)
            {
                EventHelper.FireEventIfNotNull(SyncJobUpdated, this, new GenericEventArgs<SyncJob>
                {
                    Argument = job

                }, _logger);
            }
        }

        internal async Task UpdateSyncJobItemInternal(SyncJobItem jobItem)
        {
            await _repo.Update(jobItem).ConfigureAwait(false);

            if (SyncJobUpdated != null)
            {
                EventHelper.FireEventIfNotNull(SyncJobItemUpdated, this, new GenericEventArgs<SyncJobItem>
                {
                    Argument = jobItem

                }, _logger);
            }
        }

        internal void OnSyncJobItemCreated(SyncJobItem job)
        {
            if (SyncJobUpdated != null)
            {
                EventHelper.FireEventIfNotNull(SyncJobItemCreated, this, new GenericEventArgs<SyncJobItem>
                {
                    Argument = job

                }, _logger);
            }
        }

        public async Task<QueryResult<SyncJob>> GetJobs(SyncJobQuery query)
        {
            var result = _repo.GetJobs(query);

            foreach (var item in result.Items)
            {
                await FillMetadata(item).ConfigureAwait(false);
            }

            return result;
        }

        private async Task FillMetadata(SyncJob job)
        {
            var target = GetSyncTargets(job.UserId)
                .FirstOrDefault(i => string.Equals(i.Id, job.TargetId, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                job.TargetName = target.Name;
            }

            var item = job.RequestedItemIds
                .Select(_libraryManager.GetItemById)
                .FirstOrDefault(i => i != null);

            if (item == null)
            {
                var processor = GetSyncJobProcessor();

                var user = _userManager.GetUserById(job.UserId);

                item = (await processor
                    .GetItemsForSync(job.Category, job.ParentId, job.RequestedItemIds, user, job.UnwatchedOnly).ConfigureAwait(false))
                    .FirstOrDefault();
            }

            if (item != null)
            {
                var hasSeries = item as IHasSeries;
                if (hasSeries != null)
                {
                    job.ParentName = hasSeries.SeriesName;
                }

                var hasAlbumArtist = item as IHasAlbumArtist;
                if (hasAlbumArtist != null)
                {
                    job.ParentName = hasAlbumArtist.AlbumArtists.FirstOrDefault();
                }

                var primaryImage = item.GetImageInfo(ImageType.Primary, 0);
                var itemWithImage = item;

                if (primaryImage == null)
                {
                    var parentWithImage = item.Parents.FirstOrDefault(i => i.HasImage(ImageType.Primary));

                    if (parentWithImage != null)
                    {
                        itemWithImage = parentWithImage;
                        primaryImage = parentWithImage.GetImageInfo(ImageType.Primary, 0);
                    }
                }

                if (primaryImage != null)
                {
                    try
                    {
                        job.PrimaryImageTag = _imageProcessor.GetImageCacheTag(itemWithImage, ImageType.Primary);
                        job.PrimaryImageItemId = itemWithImage.Id.ToString("N");

                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("Error getting image info", ex);
                    }
                }
            }
        }

        private void FillMetadata(SyncJobItem jobItem)
        {
            var item = _libraryManager.GetItemById(jobItem.ItemId);

            if (item == null)
            {
                return;
            }

            var primaryImage = item.GetImageInfo(ImageType.Primary, 0);
            var itemWithImage = item;

            if (primaryImage == null)
            {
                var parentWithImage = item.Parents.FirstOrDefault(i => i.HasImage(ImageType.Primary));

                if (parentWithImage != null)
                {
                    itemWithImage = parentWithImage;
                    primaryImage = parentWithImage.GetImageInfo(ImageType.Primary, 0);
                }
            }

            if (primaryImage != null)
            {
                try
                {
                    jobItem.PrimaryImageTag = _imageProcessor.GetImageCacheTag(itemWithImage, ImageType.Primary);
                    jobItem.PrimaryImageItemId = itemWithImage.Id.ToString("N");

                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error getting image info", ex);
                }
            }
        }

        public async Task CancelJob(string id)
        {
            var job = GetJob(id);

            if (job == null)
            {
                throw new ArgumentException("Job not found.");
            }

            await _repo.DeleteJob(id).ConfigureAwait(false);

            var path = GetSyncJobProcessor().GetTemporaryPath(id);

            try
            {
                _fileSystem.DeleteDirectory(path, true);
            }
            catch (DirectoryNotFoundException)
            {

            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error deleting directory {0}", ex, path);
            }

            if (SyncJobCancelled != null)
            {
                EventHelper.FireEventIfNotNull(SyncJobCancelled, this, new GenericEventArgs<SyncJob>
                {
                    Argument = job

                }, _logger);
            }
        }

        public SyncJob GetJob(string id)
        {
            return _repo.GetJob(id);
        }

        public IEnumerable<SyncTarget> GetSyncTargets(string userId)
        {
            return _providers
                .SelectMany(i => GetSyncTargets(i, userId))
                .OrderBy(i => i.Name);
        }

        private IEnumerable<SyncTarget> GetSyncTargets(ISyncProvider provider)
        {
            return provider.GetAllSyncTargets().Select(i => new SyncTarget
            {
                Name = i.Name,
                Id = GetSyncTargetId(provider, i)
            });
        }

        private IEnumerable<SyncTarget> GetSyncTargets(ISyncProvider provider, string userId)
        {
            return provider.GetSyncTargets(userId).Select(i => new SyncTarget
            {
                Name = i.Name,
                Id = GetSyncTargetId(provider, i)
            });
        }

        private string GetSyncTargetId(ISyncProvider provider, SyncTarget target)
        {
            var hasUniqueId = provider as IHasUniqueTargetIds;

            if (hasUniqueId != null)
            {
                return target.Id;
            }

            return target.Id;
            //var providerId = GetSyncProviderId(provider);
            //return (providerId + "-" + target.Id).GetMD5().ToString("N");
        }

        private string GetSyncProviderId(ISyncProvider provider)
        {
            return (provider.GetType().Name).GetMD5().ToString("N");
        }

        public bool SupportsSync(BaseItem item)
        {
            if (item is Playlist)
            {
                return true;
            }

            if (item is Person)
            {
                return false;
            }

            if (item is Year)
            {
                return false;
            }

            if (string.Equals(item.MediaType, MediaType.Video, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.MediaType, MediaType.Audio, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.MediaType, MediaType.Photo, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.MediaType, MediaType.Game, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.MediaType, MediaType.Book, StringComparison.OrdinalIgnoreCase))
            {
                if (item.LocationType == LocationType.Virtual)
                {
                    return false;
                }

                if (!item.RunTimeTicks.HasValue)
                {
                    return false;
                }

                var video = item as Video;
                if (video != null)
                {
                    if (video.VideoType == VideoType.Iso)
                    {
                        return false;
                    }

                    if (video.VideoType == VideoType.BluRay || video.VideoType == VideoType.Dvd || video.VideoType == VideoType.HdDvd)
                    {
                        return false;
                    }

                    if (video.IsPlaceHolder)
                    {
                        return false;
                    }

                    if (video.IsArchive)
                    {
                        return false;
                    }

                    if (video.IsStacked)
                    {
                        return false;
                    }

                    if (video.IsShortcut)
                    {
                        return false;
                    }
                }

                var game = item as Game;
                if (game != null)
                {
                    if (game.IsMultiPart)
                    {
                        return false;
                    }
                }

                if (item is LiveTvChannel || item is IChannelItem || item is ILiveTvRecording)
                {
                    return false;
                }

                // It would be nice to support these later
                if (item is Game || item is Book)
                {
                    return false;
                }

                return true;
            }

            return item.LocationType == LocationType.FileSystem || item is Season || item is ILiveTvRecording;
        }

        private string GetDefaultName(BaseItem item)
        {
            return item.Name;
        }

        public DeviceProfile GetDeviceProfile(string targetId)
        {
            foreach (var provider in _providers)
            {
                foreach (var target in GetSyncTargets(provider))
                {
                    if (string.Equals(target.Id, targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetDeviceProfile(provider, target);
                    }
                }
            }

            return null;
        }

        public DeviceProfile GetDeviceProfile(ISyncProvider provider, SyncTarget target)
        {
            var hasProfile = provider as IHasSyncProfile;

            if (hasProfile != null)
            {
                return hasProfile.GetDeviceProfile(target);
            }

            return new CloudSyncProfile(true, false);
        }

        public async Task ReportSyncJobItemTransferred(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            jobItem.Status = SyncJobItemStatus.Synced;
            jobItem.Progress = 100;

            if (!string.IsNullOrWhiteSpace(jobItem.TemporaryPath))
            {
                try
                {
                    _fileSystem.DeleteDirectory(jobItem.TemporaryPath, true);
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error deleting temporary job file: {0}", ex, jobItem.OutputPath);
                }
            }

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        private SyncJobProcessor GetSyncJobProcessor()
        {
            return new SyncJobProcessor(_libraryManager, _repo, this, _logger, _userManager, _tvSeriesManager, _mediaEncoder(), _subtitleEncoder(), _config, _fileSystem, _mediaSourceManager());
        }

        public SyncJobItem GetJobItem(string id)
        {
            return _repo.GetJobItem(id);
        }

        public QueryResult<SyncJobItem> GetJobItems(SyncJobItemQuery query)
        {
            var result = _repo.GetJobItems(query);

            if (query.AddMetadata)
            {
                result.Items.ForEach(FillMetadata);
            }

            return result;
        }

        private SyncedItem GetJobItemInfo(SyncJobItem jobItem)
        {
            var job = _repo.GetJob(jobItem.JobId);

            if (job == null)
            {
                _logger.Error("GetJobItemInfo job id {0} no longer exists", jobItem.JobId);
                return null;
            }

            var libraryItem = _libraryManager.GetItemById(jobItem.ItemId);

            if (libraryItem == null)
            {
                _logger.Error("GetJobItemInfo library item with id {0} no longer exists", jobItem.ItemId);
                return null;
            }

            var syncedItem = new SyncedItem
            {
                SyncJobId = jobItem.JobId,
                SyncJobItemId = jobItem.Id,
                ServerId = _appHost.SystemId,
                UserId = job.UserId,
                AdditionalFiles = jobItem.AdditionalFiles.Select(i => new ItemFileInfo
                {
                    ImageType = i.ImageType,
                    Name = i.Name,
                    Type = i.Type,
                    Index = i.Index

                }).ToList()
            };

            var dtoOptions = new DtoOptions();

            // Remove some bloat
            dtoOptions.Fields.Remove(ItemFields.MediaStreams);
            dtoOptions.Fields.Remove(ItemFields.IndexOptions);
            dtoOptions.Fields.Remove(ItemFields.MediaSourceCount);
            dtoOptions.Fields.Remove(ItemFields.OriginalPrimaryImageAspectRatio);
            dtoOptions.Fields.Remove(ItemFields.Path);
            dtoOptions.Fields.Remove(ItemFields.SeriesGenres);
            dtoOptions.Fields.Remove(ItemFields.Settings);
            dtoOptions.Fields.Remove(ItemFields.SyncInfo);

            syncedItem.Item = _dtoService().GetBaseItemDto(libraryItem, dtoOptions);

            var mediaSource = jobItem.MediaSource;

            syncedItem.Item.MediaSources = new List<MediaSourceInfo>();

            // This will be null for items that are not audio/video
            if (mediaSource == null)
            {
                syncedItem.OriginalFileName = Path.GetFileName(libraryItem.Path);
            }
            else
            {
                syncedItem.OriginalFileName = Path.GetFileName(mediaSource.Path);
                syncedItem.Item.MediaSources.Add(mediaSource);
            }

            return syncedItem;
        }

        public Task ReportOfflineAction(UserAction action)
        {
            switch (action.Type)
            {
                case UserActionType.PlayedItem:
                    return ReportOfflinePlayedItem(action);
                default:
                    throw new ArgumentException("Unexpected action type");
            }
        }

        private Task ReportOfflinePlayedItem(UserAction action)
        {
            var item = _libraryManager.GetItemById(action.ItemId);
            var userData = _userDataManager.GetUserData(new Guid(action.UserId), item.GetUserDataKey());

            userData.LastPlayedDate = action.Date;
            _userDataManager.UpdatePlayState(item, userData, action.PositionTicks);

            return _userDataManager.SaveUserData(new Guid(action.UserId), item, userData, UserDataSaveReason.Import, CancellationToken.None);
        }

        public async Task<List<SyncedItem>> GetReadySyncItems(string targetId)
        {
            var processor = GetSyncJobProcessor();

            await processor.SyncJobItems(targetId, false, new Progress<double>(), CancellationToken.None).ConfigureAwait(false);

            var jobItemResult = GetJobItems(new SyncJobItemQuery
            {
                TargetId = targetId,
                Statuses = new List<SyncJobItemStatus>
                {
                    SyncJobItemStatus.ReadyToTransfer
                }
            });

            return jobItemResult.Items
                .Select(GetJobItemInfo)
                .Where(i => i != null)
                .ToList();
        }

        public async Task<SyncDataResponse> SyncData(SyncDataRequest request)
        {
            var jobItemResult = GetJobItems(new SyncJobItemQuery
            {
                TargetId = request.TargetId,
                Statuses = new List<SyncJobItemStatus> { SyncJobItemStatus.Synced }
            });

            var response = new SyncDataResponse();

            foreach (var jobItem in jobItemResult.Items)
            {
                if (request.LocalItemIds.Contains(jobItem.ItemId, StringComparer.OrdinalIgnoreCase))
                {
                    var job = _repo.GetJob(jobItem.JobId);
                    var user = _userManager.GetUserById(job.UserId);

                    if (jobItem.IsMarkedForRemoval)
                    {
                        // Tell the device to remove it since it has been marked for removal
                        response.ItemIdsToRemove.Add(jobItem.ItemId);
                    }
                    else if (user == null)
                    {
                        // Tell the device to remove it since the user is gone now
                        response.ItemIdsToRemove.Add(jobItem.ItemId);
                    }
                    else if (job.UnwatchedOnly)
                    {
                        var libraryItem = _libraryManager.GetItemById(jobItem.ItemId);

                        if (IsLibraryItemAvailable(libraryItem))
                        {
                            if (libraryItem.IsPlayed(user) && libraryItem is Video)
                            {
                                // Tell the device to remove it since it has been played
                                response.ItemIdsToRemove.Add(jobItem.ItemId);
                            }
                        }
                        else
                        {
                            // Tell the device to remove it since it's no longer available
                            response.ItemIdsToRemove.Add(jobItem.ItemId);
                        }
                    }
                }
                else
                {
                    // Content is no longer on the device
                    jobItem.Status = SyncJobItemStatus.RemovedFromDevice;
                    await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);
                }
            }

            // Now check each item that's on the device
            foreach (var itemId in request.LocalItemIds)
            {
                // See if it's already marked for removal
                if (response.ItemIdsToRemove.Contains(itemId, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // If there isn't a sync job for this item, mark it for removal
                if (!jobItemResult.Items.Any(i => string.Equals(itemId, i.ItemId, StringComparison.OrdinalIgnoreCase)))
                {
                    response.ItemIdsToRemove.Add(itemId);
                }
            }

            response.ItemIdsToRemove = response.ItemIdsToRemove.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var itemsOnDevice = request.LocalItemIds
                .Except(response.ItemIdsToRemove)
                .ToList();

            SetUserAccess(request, response, itemsOnDevice);

            return response;
        }

        private void SetUserAccess(SyncDataRequest request, SyncDataResponse response, List<string> itemIds)
        {
            var users = request.OfflineUserIds
                .Select(_userManager.GetUserById)
                .Where(i => i != null)
                .ToList();

            foreach (var itemId in itemIds)
            {
                var item = _libraryManager.GetItemById(itemId);

                if (item != null)
                {
                    var usersWithAccess = new List<User>();

                    foreach (var user in users)
                    {
                        if (IsUserVisible(item, user))
                        {
                            usersWithAccess.Add(user);
                        }
                    }

                    response.ItemUserAccess[itemId] = users
                        .Select(i => i.Id.ToString("N"))
                        .OrderBy(i => i)
                        .ToList();
                }
            }
        }

        private bool IsUserVisible(BaseItem item, User user)
        {
            return item.IsVisibleStandalone(user);
        }

        private bool IsLibraryItemAvailable(BaseItem item)
        {
            if (item == null)
            {
                return false;
            }

            // TODO: Make sure it hasn't been deleted

            return true;
        }

        public async Task ReEnableJobItem(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            if (jobItem.Status != SyncJobItemStatus.Failed && jobItem.Status != SyncJobItemStatus.Cancelled)
            {
                throw new ArgumentException("Operation is not valid for this job item");
            }

            jobItem.Status = SyncJobItemStatus.Queued;
            jobItem.Progress = 0;
            jobItem.IsMarkedForRemoval = false;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        public async Task CancelJobItem(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            if (jobItem.Status != SyncJobItemStatus.Queued && jobItem.Status != SyncJobItemStatus.ReadyToTransfer && jobItem.Status != SyncJobItemStatus.Converting)
            {
                throw new ArgumentException("Operation is not valid for this job item");
            }

            jobItem.Status = SyncJobItemStatus.Cancelled;
            jobItem.Progress = 0;
            jobItem.IsMarkedForRemoval = true;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);

            var path = processor.GetTemporaryPath(jobItem);

            try
            {
                _fileSystem.DeleteDirectory(path, true);
            }
            catch (DirectoryNotFoundException)
            {

            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error deleting directory {0}", ex, path);
            }
        }

        public async Task MarkJobItemForRemoval(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            if (jobItem.Status != SyncJobItemStatus.Synced)
            {
                throw new ArgumentException("Operation is not valid for this job item");
            }

            jobItem.IsMarkedForRemoval = true;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        public async Task UnmarkJobItemForRemoval(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            if (jobItem.Status != SyncJobItemStatus.Synced)
            {
                throw new ArgumentException("Operation is not valid for this job item");
            }

            jobItem.IsMarkedForRemoval = false;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        public async Task ReportSyncJobItemTransferBeginning(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            jobItem.Status = SyncJobItemStatus.Transferring;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        public async Task ReportSyncJobItemTransferFailed(string id)
        {
            var jobItem = _repo.GetJobItem(id);

            jobItem.Status = SyncJobItemStatus.ReadyToTransfer;

            await UpdateSyncJobItemInternal(jobItem).ConfigureAwait(false);

            var processor = GetSyncJobProcessor();

            await processor.UpdateJobStatus(jobItem.JobId).ConfigureAwait(false);
        }

        public QueryResult<string> GetLibraryItemIds(SyncJobItemQuery query)
        {
            return _repo.GetLibraryItemIds(query);
        }

        public AudioOptions GetAudioOptions(SyncJobItem jobItem)
        {
            var profile = GetDeviceProfile(jobItem.TargetId);

            return new AudioOptions
            {
                Profile = profile
            };
        }

        public VideoOptions GetVideoOptions(SyncJobItem jobItem, SyncJob job)
        {
            var profile = GetDeviceProfile(jobItem.TargetId);
            var maxBitrate = profile.MaxStaticBitrate;

            if (maxBitrate.HasValue)
            {
                if (job.Quality == SyncQuality.Medium)
                {
                    maxBitrate = Convert.ToInt32(maxBitrate.Value * .75);
                }
                else if (job.Quality == SyncQuality.Low)
                {
                    maxBitrate = Convert.ToInt32(maxBitrate.Value * .5);
                }
            }

            return new VideoOptions
            {
                Profile = profile,
                MaxBitrate = maxBitrate
            };
        }
    }
}
