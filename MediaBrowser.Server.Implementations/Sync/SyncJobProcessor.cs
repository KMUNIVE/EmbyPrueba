﻿using MediaBrowser.Common.Progress;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Sync;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Sync
{
    public class SyncJobProcessor
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ISyncRepository _syncRepo;
        private readonly ISyncManager _syncManager;
        private readonly ILogger _logger;
        private readonly IUserManager _userManager;
        private readonly ITVSeriesManager _tvSeriesManager;
        private readonly IMediaEncoder _mediaEncoder;

        public SyncJobProcessor(ILibraryManager libraryManager, ISyncRepository syncRepo, ISyncManager syncManager, ILogger logger, IUserManager userManager, ITVSeriesManager tvSeriesManager, IMediaEncoder mediaEncoder)
        {
            _libraryManager = libraryManager;
            _syncRepo = syncRepo;
            _syncManager = syncManager;
            _logger = logger;
            _userManager = userManager;
            _tvSeriesManager = tvSeriesManager;
            _mediaEncoder = mediaEncoder;
        }

        public async Task EnsureJobItems(SyncJob job)
        {
            var user = _userManager.GetUserById(job.UserId);

            if (user == null)
            {
                throw new InvalidOperationException("Cannot proceed with sync because user no longer exists.");
            }

            var items = (await GetItemsForSync(job.Category, job.ParentId, job.RequestedItemIds, user, job.UnwatchedOnly).ConfigureAwait(false))
                .ToList();

            var jobItems = _syncRepo.GetJobItems(new SyncJobItemQuery
            {
                JobId = job.Id

            }).Items.ToList();

            foreach (var item in items)
            {
                // Respect ItemLimit, if set
                if (job.ItemLimit.HasValue)
                {
                    if (jobItems.Count(j => j.Status != SyncJobItemStatus.RemovedFromDevice && j.Status != SyncJobItemStatus.Failed) >= job.ItemLimit.Value)
                    {
                        break;
                    }
                }

                var itemId = item.Id.ToString("N");

                var jobItem = jobItems.FirstOrDefault(i => string.Equals(i.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

                if (jobItem != null)
                {
                    continue;
                }

                jobItem = new SyncJobItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    ItemName = GetSyncJobItemName(item),
                    JobId = job.Id,
                    TargetId = job.TargetId,
                    DateCreated = DateTime.UtcNow
                };

                await _syncRepo.Create(jobItem).ConfigureAwait(false);

                jobItems.Add(jobItem);
            }

            jobItems = jobItems
                .OrderBy(i => i.DateCreated)
                .ToList();

            await UpdateJobStatus(job, jobItems).ConfigureAwait(false);
        }

        private string GetSyncJobItemName(BaseItem item)
        {
            return item.Name;
        }

        public Task UpdateJobStatus(string id)
        {
            var job = _syncRepo.GetJob(id);

            return UpdateJobStatus(job);
        }

        private Task UpdateJobStatus(SyncJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException("job");
            }

            var result = _syncRepo.GetJobItems(new SyncJobItemQuery
            {
                JobId = job.Id
            });

            return UpdateJobStatus(job, result.Items.ToList());
        }

        private Task UpdateJobStatus(SyncJob job, List<SyncJobItem> jobItems)
        {
            job.ItemCount = jobItems.Count;

            double pct = 0;

            foreach (var item in jobItems)
            {
                if (item.Status == SyncJobItemStatus.Failed || item.Status == SyncJobItemStatus.Synced || item.Status == SyncJobItemStatus.RemovedFromDevice)
                {
                    pct += 100;
                }
                else
                {
                    pct += item.Progress ?? 0;
                }
            }

            if (job.ItemCount > 0)
            {
                pct /= job.ItemCount;
                job.Progress = pct;
            }
            else
            {
                job.Progress = null;
            }

            if (pct >= 100)
            {
                if (jobItems.Any(i => i.Status == SyncJobItemStatus.Failed))
                {
                    job.Status = SyncJobStatus.CompletedWithError;
                }
                else
                {
                    job.Status = SyncJobStatus.Completed;
                }
            }
            else if (pct.Equals(0))
            {
                job.Status = SyncJobStatus.Queued;
            }
            else
            {
                job.Status = SyncJobStatus.InProgress;
            }

            return _syncRepo.Update(job);
        }

        public async Task<IEnumerable<BaseItem>> GetItemsForSync(SyncCategory? category, string parentId, IEnumerable<string> itemIds, User user, bool unwatchedOnly)
        {
            var items = category.HasValue ?
                await GetItemsForSync(category.Value, parentId, user).ConfigureAwait(false) :
                itemIds.SelectMany(i => GetItemsForSync(i, user))
                .Where(_syncManager.SupportsSync);

            if (unwatchedOnly)
            {
                // Avoid implicitly captured closure
                var currentUser = user;

                items = items.Where(i =>
                {
                    var video = i as Video;

                    if (video != null)
                    {
                        return !video.IsPlayed(currentUser);
                    }

                    return true;
                });
            }

            return items.DistinctBy(i => i.Id);
        }

        private async Task<IEnumerable<BaseItem>> GetItemsForSync(SyncCategory category, string parentId, User user)
        {
            var parent = string.IsNullOrWhiteSpace(parentId)
                ? user.RootFolder
                : (Folder)_libraryManager.GetItemById(parentId);

            InternalItemsQuery query;

            switch (category)
            {
                case SyncCategory.Latest:
                    query = new InternalItemsQuery
                    {
                        IsFolder = false,
                        SortBy = new[] { ItemSortBy.DateCreated, ItemSortBy.SortName },
                        SortOrder = SortOrder.Descending,
                        Recursive = true
                    };
                    break;
                case SyncCategory.Resume:
                    query = new InternalItemsQuery
                    {
                        IsFolder = false,
                        SortBy = new[] { ItemSortBy.DatePlayed, ItemSortBy.SortName },
                        SortOrder = SortOrder.Descending,
                        Recursive = true,
                        IsResumable = true,
                        MediaTypes = new[] { MediaType.Video }
                    };
                    break;

                case SyncCategory.NextUp:
                    return _tvSeriesManager.GetNextUp(new NextUpQuery
                    {
                        ParentId = parentId,
                        UserId = user.Id.ToString("N")
                    }).Items;

                default:
                    throw new ArgumentException("Unrecognized category: " + category);
            }

            query.User = user;

            var result = await parent.GetItems(query).ConfigureAwait(false);
            return result.Items;
        }

        private IEnumerable<BaseItem> GetItemsForSync(string id, User user)
        {
            var item = _libraryManager.GetItemById(id);

            if (item == null)
            {
                return new List<BaseItem>();
            }

            return GetItemsForSync(item, user);
        }

        private IEnumerable<BaseItem> GetItemsForSync(BaseItem item, User user)
        {
            var itemByName = item as IItemByName;
            if (itemByName != null)
            {
                var items = user.RootFolder
                    .GetRecursiveChildren(user);

                return itemByName.GetTaggedItems(items);
            }

            if (item.IsFolder)
            {
                var folder = (Folder)item;
                var items = folder.GetRecursiveChildren(user);

                items = items.Where(i => !i.IsFolder);

                if (!folder.IsPreSorted)
                {
                    items = items.OrderBy(i => i.SortName);
                }

                return items;
            }

            return new[] { item };
        }

        public async Task EnsureSyncJobs(CancellationToken cancellationToken)
        {
            var jobResult = _syncRepo.GetJobs(new SyncJobQuery
            {
                IsCompleted = false
            });

            foreach (var job in jobResult.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (job.SyncNewContent)
                {
                    await EnsureJobItems(job).ConfigureAwait(false);
                }
            }
        }

        public async Task Sync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await EnsureSyncJobs(cancellationToken).ConfigureAwait(false);

            // If it already has a converting status then is must have been aborted during conversion
            var result = _syncRepo.GetJobItems(new SyncJobItemQuery
            {
                Statuses = new List<SyncJobItemStatus> { SyncJobItemStatus.Queued, SyncJobItemStatus.Converting }
            });

            var jobItems = result.Items;
            var index = 0;

            foreach (var item in jobItems)
            {
                double percent = index;
                percent /= result.TotalRecordCount;

                progress.Report(100 * percent);

                cancellationToken.ThrowIfCancellationRequested();

                var innerProgress = new ActionableProgress<double>();

                var job = _syncRepo.GetJob(item.JobId);
                await ProcessJobItem(job, item, innerProgress, cancellationToken).ConfigureAwait(false);

                job = _syncRepo.GetJob(item.JobId);
                await UpdateJobStatus(job).ConfigureAwait(false);

                index++;
            }
        }

        private async Task ProcessJobItem(SyncJob job, SyncJobItem jobItem, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var item = _libraryManager.GetItemById(jobItem.ItemId);
            if (item == null)
            {
                jobItem.Status = SyncJobItemStatus.Failed;
                _logger.Error("Unable to locate library item for JobItem {0}, ItemId {1}", jobItem.Id, jobItem.ItemId);
                await _syncRepo.Update(jobItem).ConfigureAwait(false);
                return;
            }

            var deviceProfile = _syncManager.GetDeviceProfile(jobItem.TargetId);
            if (deviceProfile == null)
            {
                jobItem.Status = SyncJobItemStatus.Failed;
                _logger.Error("Unable to locate SyncTarget for JobItem {0}, SyncTargetId {1}", jobItem.Id, jobItem.TargetId);
                await _syncRepo.Update(jobItem).ConfigureAwait(false);
                return;
            }

            jobItem.Progress = 0;
            jobItem.Status = SyncJobItemStatus.Converting;

            var user = _userManager.GetUserById(job.UserId);

            var video = item as Video;
            if (video != null)
            {
                await Sync(jobItem, video, user, deviceProfile, progress, cancellationToken).ConfigureAwait(false);
            }

            else if (item is Audio)
            {
                await Sync(jobItem, (Audio)item, user, deviceProfile, progress, cancellationToken).ConfigureAwait(false);
            }

            else if (item is Photo)
            {
                await Sync(jobItem, (Photo)item, deviceProfile, cancellationToken).ConfigureAwait(false);
            }

            else
            {
                await SyncGeneric(jobItem, item, deviceProfile, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task Sync(SyncJobItem jobItem, Video item, User user, DeviceProfile profile, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var options = new VideoOptions
            {
                Context = EncodingContext.Static,
                ItemId = item.Id.ToString("N"),
                DeviceId = jobItem.TargetId,
                Profile = profile,
                MediaSources = item.GetMediaSources(false, user).ToList()
            };

            var streamInfo = new StreamBuilder().BuildVideoItem(options);
            var mediaSource = streamInfo.MediaSource;

            jobItem.MediaSourceId = streamInfo.MediaSourceId;

            if (streamInfo.PlayMethod == PlayMethod.Transcode)
            {
                jobItem.Status = SyncJobItemStatus.Converting;
                jobItem.RequiresConversion = true;
                await _syncRepo.Update(jobItem).ConfigureAwait(false);

                try
                {
                    jobItem.OutputPath = await _mediaEncoder.EncodeVideo(new EncodingJobOptions(streamInfo, profile), progress,
                                cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    jobItem.Status = SyncJobItemStatus.Queued;
                }
                catch (Exception ex)
                {
                    jobItem.Status = SyncJobItemStatus.Failed;
                    _logger.ErrorException("Error during sync transcoding", ex);
                }

                if (jobItem.Status == SyncJobItemStatus.Failed || jobItem.Status == SyncJobItemStatus.Queued)
                {
                    await _syncRepo.Update(jobItem).ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                jobItem.RequiresConversion = false;
                
                if (mediaSource.Protocol == MediaProtocol.File)
                {
                    jobItem.OutputPath = mediaSource.Path;
                }
                else if (mediaSource.Protocol == MediaProtocol.Http)
                {
                    jobItem.OutputPath = await DownloadFile(jobItem, mediaSource, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException(string.Format("Cannot direct stream {0} protocol", mediaSource.Protocol));
                }
            }

            jobItem.Progress = 50;
            jobItem.Status = SyncJobItemStatus.Transferring;
            await _syncRepo.Update(jobItem).ConfigureAwait(false);
        }

        private async Task Sync(SyncJobItem jobItem, Audio item, User user, DeviceProfile profile, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var options = new AudioOptions
            {
                Context = EncodingContext.Static,
                ItemId = item.Id.ToString("N"),
                DeviceId = jobItem.TargetId,
                Profile = profile,
                MediaSources = item.GetMediaSources(false, user).ToList()
            };

            var streamInfo = new StreamBuilder().BuildAudioItem(options);
            var mediaSource = streamInfo.MediaSource;

            jobItem.MediaSourceId = streamInfo.MediaSourceId;

            if (streamInfo.PlayMethod == PlayMethod.Transcode)
            {
                jobItem.Status = SyncJobItemStatus.Converting;
                jobItem.RequiresConversion = true;
                await _syncRepo.Update(jobItem).ConfigureAwait(false);

                try
                {
                    jobItem.OutputPath = await _mediaEncoder.EncodeAudio(new EncodingJobOptions(streamInfo, profile), progress, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    jobItem.Status = SyncJobItemStatus.Queued;
                }
                catch (Exception ex)
                {
                    jobItem.Status = SyncJobItemStatus.Failed;
                    _logger.ErrorException("Error during sync transcoding", ex);
                }

                if (jobItem.Status == SyncJobItemStatus.Failed || jobItem.Status == SyncJobItemStatus.Queued)
                {
                    await _syncRepo.Update(jobItem).ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                jobItem.RequiresConversion = false;
                
                if (mediaSource.Protocol == MediaProtocol.File)
                {
                    jobItem.OutputPath = mediaSource.Path;
                }
                else if (mediaSource.Protocol == MediaProtocol.Http)
                {
                    jobItem.OutputPath = await DownloadFile(jobItem, mediaSource, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException(string.Format("Cannot direct stream {0} protocol", mediaSource.Protocol));
                }
            }

            jobItem.Progress = 50;
            jobItem.Status = SyncJobItemStatus.Transferring;
            await _syncRepo.Update(jobItem).ConfigureAwait(false);
        }

        private async Task Sync(SyncJobItem jobItem, Photo item, DeviceProfile profile, CancellationToken cancellationToken)
        {
            jobItem.OutputPath = item.Path;

            jobItem.Progress = 50;
            jobItem.Status = SyncJobItemStatus.Transferring;
            await _syncRepo.Update(jobItem).ConfigureAwait(false);
        }

        private async Task SyncGeneric(SyncJobItem jobItem, BaseItem item, DeviceProfile profile, CancellationToken cancellationToken)
        {
            jobItem.OutputPath = item.Path;

            jobItem.Progress = 50;
            jobItem.Status = SyncJobItemStatus.Transferring;
            await _syncRepo.Update(jobItem).ConfigureAwait(false);
        }

        private async Task<string> DownloadFile(SyncJobItem jobItem, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        {
            // TODO: Download
            return mediaSource.Path;
        }
    }
}
