﻿using MediaBrowser.Model.Dto;
using System.Collections.Generic;

namespace MediaBrowser.Model.Sync
{
    public class LocalItem
    {
        /// <summary>
        /// Gets or sets the item.
        /// </summary>
        /// <value>The item.</value>
        public BaseItemDto Item { get; set; }
        /// <summary>
        /// Gets or sets the local path.
        /// </summary>
        /// <value>The local path.</value>
        public string LocalPath { get; set; }
        /// <summary>
        /// Gets or sets the server identifier.
        /// </summary>
        /// <value>The server identifier.</value>
        public string ServerId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        /// <value>The unique identifier.</value>
        public string Id { get; set; }
        /// <summary>
        /// Gets or sets the item identifier.
        /// </summary>
        /// <value>The item identifier.</value>
        public string ItemId { get; set; }
        /// <summary>
        /// Gets or sets the user ids with access.
        /// </summary>
        /// <value>The user ids with access.</value>
        public List<string> UserIdsWithAccess { get; set; }

        public LocalItem()
        {
            UserIdsWithAccess = new List<string>();
        }
    }
}
