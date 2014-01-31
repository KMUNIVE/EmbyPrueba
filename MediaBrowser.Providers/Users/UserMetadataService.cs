﻿using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Providers.Manager;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Users
{
    public class UserMetadataService : ConcreteMetadataService<User>
    {
        private readonly IUserManager _userManager;

        public UserMetadataService(IServerConfigurationManager serverConfigurationManager, ILogger logger, IProviderManager providerManager, IProviderRepository providerRepo, ILibraryManager libraryManager, IUserManager userManager)
            : base(serverConfigurationManager, logger, providerManager, providerRepo)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Merges the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="lockedFields">The locked fields.</param>
        /// <param name="replaceData">if set to <c>true</c> [replace data].</param>
        protected override void MergeData(User source, User target, List<MetadataFields> lockedFields, bool replaceData, bool mergeMetadataSettings)
        {
            ProviderUtils.MergeBaseItemData(source, target, lockedFields, replaceData, mergeMetadataSettings);
        }

        protected override Task SaveItem(User item, ItemUpdateType reason, CancellationToken cancellationToken)
        {
            return _userManager.UpdateUser(item);
        }
    }
}
