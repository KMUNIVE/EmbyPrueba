﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Manager
{
    public abstract class MetadataService<TItemType> : IMetadataService
        where TItemType : IHasMetadata
    {
        protected readonly IServerConfigurationManager ServerConfigurationManager;
        protected readonly ILogger Logger;
        protected readonly IProviderManager ProviderManager;
        private readonly IProviderRepository _providerRepo;

        private IMetadataProvider<TItemType>[] _providers = { };

        private IImageProvider[] _imageProviders = { };

        protected MetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo)
        {
            ServerConfigurationManager = serverConfigurationManager;
            Logger = logger;
            ProviderManager = providerManager;
            _providerRepo = providerRepo;
        }

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="providers">The providers.</param>
        /// <param name="imageProviders">The image providers.</param>
        public void AddParts(IEnumerable<IMetadataProvider> providers, IEnumerable<IImageProvider> imageProviders)
        {
            _providers = providers.OfType<IMetadataProvider<TItemType>>()
                .ToArray();

            _imageProviders = imageProviders.OrderBy(i => i.Order).ToArray();
        }

        /// <summary>
        /// Saves the provider result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>Task.</returns>
        protected Task SaveProviderResult(MetadataStatus result)
        {
            return _providerRepo.SaveMetadataStatus(result, CancellationToken.None);
        }

        /// <summary>
        /// Gets the last result.
        /// </summary>
        /// <param name="itemId">The item identifier.</param>
        /// <returns>ProviderResult.</returns>
        protected MetadataStatus GetLastResult(Guid itemId)
        {
            return _providerRepo.GetMetadataStatus(itemId) ?? new MetadataStatus { ItemId = itemId };
        }

        public async Task RefreshMetadata(IHasMetadata item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            var itemOfType = (TItemType)item;

            var updateType = ItemUpdateType.Unspecified;
            var lastResult = GetLastResult(item.Id);
            var refreshResult = lastResult;
            refreshResult.LastErrorMessage = string.Empty;
            refreshResult.LastStatus = ProviderRefreshStatus.Success;

            var itemImageProvider = new ItemImageProvider(Logger, ProviderManager, ServerConfigurationManager);
            var localImagesFailed = false;

            // Start by validating images
            try
            {
                // Always validate images and check for new locally stored ones.
                if (itemImageProvider.ValidateImages(item, GetLocalImageProviders(item)))
                {
                    updateType = updateType | ItemUpdateType.ImageUpdate;
                }
            }
            catch (Exception ex)
            {
                localImagesFailed = true;
                Logger.ErrorException("Error validating images for {0}", ex, item.Path ?? item.Name);
                refreshResult.AddStatus(ProviderRefreshStatus.Failure, ex.Message);
            }

            // Next run metadata providers
            if (options.MetadataRefreshMode != MetadataRefreshMode.None)
            {
                var providers = GetProviders(item, lastResult.DateLastMetadataRefresh.HasValue, options).ToList();

                if (providers.Count > 0)
                {
                    var result = await RefreshWithProviders(itemOfType, options, providers, cancellationToken).ConfigureAwait(false);

                    updateType = updateType | result.UpdateType;
                    refreshResult.AddStatus(result.Status, result.ErrorMessage);
                    refreshResult.SetDateLastMetadataRefresh(DateTime.UtcNow);
                    refreshResult.AddImageProvidersRefreshed(result.Providers);
                }
            }

            // Next run remote image providers, but only if local image providers didn't throw an exception
            if (!localImagesFailed && options.ImageRefreshMode != MetadataRefreshMode.None)
            {
                var providers = GetNonLocalImageProviders(item, lastResult.DateLastImagesRefresh.HasValue, options).ToList();

                if (providers.Count > 0)
                {
                    var result = await itemImageProvider.RefreshImages(itemOfType, providers, options, cancellationToken).ConfigureAwait(false);

                    updateType = updateType | result.UpdateType;
                    refreshResult.AddStatus(result.Status, result.ErrorMessage);
                    refreshResult.SetDateLastImagesRefresh(DateTime.UtcNow);
                    refreshResult.AddImageProvidersRefreshed(result.Providers);
                }
            }

            var providersHadChanges = updateType > ItemUpdateType.Unspecified;

            if (options.ForceSave || providersHadChanges)
            {
                if (string.IsNullOrEmpty(item.Name))
                {
                    throw new InvalidOperationException("Item has no name");
                }

                // Save to database
                await SaveItem(itemOfType, updateType, cancellationToken);
            }

            if (providersHadChanges || refreshResult.IsDirty)
            {
                await SaveProviderResult(refreshResult).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the providers.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="hasRefreshedMetadata">if set to <c>true</c> [has refreshed metadata].</param>
        /// <param name="options">The options.</param>
        /// <returns>IEnumerable{`0}.</returns>
        protected virtual IEnumerable<IMetadataProvider> GetProviders(IHasMetadata item, bool hasRefreshedMetadata, MetadataRefreshOptions options)
        {
            // Get providers to refresh
            var providers = _providers.Where(i => CanRefresh(i, item)).ToList();

            // Run all if either of these flags are true
            var runAllProviders = options.ReplaceAllMetadata || options.MetadataRefreshMode == MetadataRefreshMode.FullRefresh || !hasRefreshedMetadata;

            if (!runAllProviders)
            {
                // Avoid implicitly captured closure
                var currentItem = item;

                var providersWithChanges = providers.OfType<IHasChangeMonitor>()
                    .Where(i => i.HasChanged(currentItem, currentItem.DateLastSaved))
                    .ToList();

                // If local providers are the only ones with changes, then just run those
                if (providersWithChanges.All(i => i is ILocalMetadataProvider))
                {
                    providers = providersWithChanges.Count == 0 ?
                        new List<IMetadataProvider<TItemType>>() :
                        providers.Where(i => i is ILocalMetadataProvider).ToList();
                }
            }

            return providers;
        }

        protected virtual IEnumerable<IImageProvider> GetNonLocalImageProviders(IHasMetadata item, bool hasRefreshedImages, ImageRefreshOptions options)
        {
            // Get providers to refresh
            var providers = _imageProviders.Where(i =>
            {
                try
                {
                    return !(i is ILocalImageProvider) && i.Supports(item);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in ImageProvider.Supports", ex, i.Name);

                    return false;
                }
            }).ToList();

            // Run all if either of these flags are true
            var runAllProviders = options.ImageRefreshMode == MetadataRefreshMode.FullRefresh || !hasRefreshedImages;

            if (!runAllProviders)
            {
                // Avoid implicitly captured closure
                var currentItem = item;

                providers = providers.OfType<IHasChangeMonitor>()
                    .Where(i => i.HasChanged(currentItem, currentItem.DateLastSaved))
                    .Cast<IImageProvider>()
                    .ToList();
            }

            return providers;
        }
        
        /// <summary>
        /// Determines whether this instance can refresh the specified provider.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if this instance can refresh the specified provider; otherwise, <c>false</c>.</returns>
        protected bool CanRefresh(IMetadataProvider provider, IHasMetadata item)
        {
            if (!ServerConfigurationManager.Configuration.EnableInternetProviders && provider is IRemoteMetadataProvider)
            {
                return false;
            }

            if (item.LocationType != LocationType.FileSystem && provider is ILocalMetadataProvider)
            {
                return false;
            }

            return true;
        }

        protected abstract Task SaveItem(TItemType item, ItemUpdateType reason, CancellationToken cancellationToken);

        protected virtual ItemId GetId(IHasMetadata item)
        {
            return new ItemId
            {
                MetadataCountryCode = item.GetPreferredMetadataCountryCode(),
                MetadataLanguage = item.GetPreferredMetadataLanguage(),
                Name = item.Name,
                ProviderIds = item.ProviderIds
            };
        }

        public bool CanRefresh(IHasMetadata item)
        {
            return item is TItemType;
        }

        protected virtual async Task<RefreshResult> RefreshWithProviders(TItemType item, MetadataRefreshOptions options, List<IMetadataProvider> providers, CancellationToken cancellationToken)
        {
            var refreshResult = new RefreshResult
            {
                UpdateType = ItemUpdateType.Unspecified,
                Providers = providers.Select(i => i.GetType().FullName.GetMD5()).ToList()
            };

            var temp = CreateNew();

            // If replacing all metadata, run internet providers first
            if (options.ReplaceAllMetadata)
            {
                await ExecuteRemoteProviders(item, temp, providers.OfType<IRemoteMetadataProvider<TItemType>>(), refreshResult, cancellationToken).ConfigureAwait(false);
            }

            var hasLocalMetadata = false;

            foreach (var provider in providers.OfType<ILocalMetadataProvider<TItemType>>())
            {
                Logger.Debug("Running {0} for {1}", provider.GetType().Name, item.Path ?? item.Name);

                try
                {
                    var localItem = await provider.GetMetadata(item.Path, cancellationToken).ConfigureAwait(false);

                    if (localItem.HasMetadata)
                    {
                        MergeData(localItem.Item, temp, new List<MetadataFields>(), false, true);
                        refreshResult.UpdateType = refreshResult.UpdateType | ItemUpdateType.MetadataImport;

                        // Only one local provider allowed per item
                        hasLocalMetadata = true;
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // If a local provider fails, consider that a failure
                    refreshResult.Status = ProviderRefreshStatus.Failure;
                    refreshResult.ErrorMessage = ex.Message;
                    Logger.ErrorException("Error in {0}", ex, provider.Name);

                    // If the local provider fails don't continue with remote providers because the user's saved metadata could be lost
                    return refreshResult;
                }
            }

            if (!options.ReplaceAllMetadata && !hasLocalMetadata)
            {
                await ExecuteRemoteProviders(item, temp, providers.OfType<IRemoteMetadataProvider<TItemType>>(), refreshResult, cancellationToken).ConfigureAwait(false);
            }

            MergeData(temp, item, item.LockedFields, true, true);

            return refreshResult;
        }

        protected abstract TItemType CreateNew();

        private async Task ExecuteRemoteProviders(TItemType item, TItemType temp, IEnumerable<IRemoteMetadataProvider<TItemType>> providers, RefreshResult refreshResult, CancellationToken cancellationToken)
        {
            var id = GetId(item);

            foreach (var provider in providers)
            {
                Logger.Debug("Running {0} for {1}", provider.GetType().Name, item.Path ?? item.Name);

                try
                {
                    var result = await provider.GetMetadata(id, cancellationToken).ConfigureAwait(false);

                    if (result.HasMetadata)
                    {
                        MergeData(result.Item, temp, new List<MetadataFields>(), false, false);

                        refreshResult.UpdateType = refreshResult.UpdateType | ItemUpdateType.MetadataDownload;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    refreshResult.Status = ProviderRefreshStatus.CompletedWithErrors;
                    refreshResult.ErrorMessage = ex.Message;
                    Logger.ErrorException("Error in {0}", ex, provider.Name);
                }
            }
        }

        protected abstract void MergeData(TItemType source, TItemType target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings);

        public virtual int Order
        {
            get
            {
                return 0;
            }
        }

        private IEnumerable<ILocalImageProvider> GetLocalImageProviders(IHasImages item)
        {
            return _imageProviders.OfType<ILocalImageProvider>().Where(i =>
            {
                try
                {
                    return i.Supports(item);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in ImageProvider.Supports", ex, i.Name);

                    return false;
                }
            });
        }
    }

    public class RefreshResult
    {
        public ItemUpdateType UpdateType { get; set; }
        public ProviderRefreshStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public List<Guid> Providers { get; set; }
    }
}
