﻿using MediaBrowser.Model.Sync;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Sync
{
    public interface ICloudSyncProvider
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        string Name { get; }

        /// <summary>
        /// Gets the synchronize targets.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>IEnumerable&lt;SyncTarget&gt;.</returns>
        IEnumerable<SyncTarget> GetSyncTargets(string userId);
    }
}
