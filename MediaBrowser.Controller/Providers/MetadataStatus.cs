﻿using System;

namespace MediaBrowser.Controller.Providers
{
    public class MetadataStatus
    {
        /// <summary>
        /// Gets or sets the item identifier.
        /// </summary>
        /// <value>The item identifier.</value>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the name of the item.
        /// </summary>
        /// <value>The name of the item.</value>
        public string ItemName { get; set; }

        /// <summary>
        /// Gets or sets the type of the item.
        /// </summary>
        /// <value>The type of the item.</value>
        public string ItemType { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the series.
        /// </summary>
        /// <value>The name of the series.</value>
        public string SeriesName { get; set; }

        /// <summary>
        /// Gets or sets the date last metadata refresh.
        /// </summary>
        /// <value>The date last metadata refresh.</value>
        public DateTime? DateLastMetadataRefresh { get; set; }

        /// <summary>
        /// Gets or sets the date last images refresh.
        /// </summary>
        /// <value>The date last images refresh.</value>
        public DateTime? DateLastImagesRefresh { get; set; }

        /// <summary>
        /// Gets or sets the last result.
        /// </summary>
        /// <value>The last result.</value>
        public ProviderRefreshStatus LastStatus { get; set; }

        /// <summary>
        /// Gets or sets the last result error message.
        /// </summary>
        /// <value>The last result error message.</value>
        public string LastErrorMessage { get; set; }

        public DateTime? ItemDateModified { get; set; }

        public void AddStatus(ProviderRefreshStatus status, string errorMessage)
        {
            if (LastStatus != status)
            {
                IsDirty = true;
            }

            if (string.IsNullOrEmpty(LastErrorMessage))
            {
                LastErrorMessage = errorMessage;
            }
            if (LastStatus == ProviderRefreshStatus.Success)
            {
                LastStatus = status;
            }
        }

        public MetadataStatus()
        {
            LastStatus = ProviderRefreshStatus.Success;
        }

        public bool IsDirty { get; private set; }

        public void SetDateLastMetadataRefresh(DateTime date)
        {
            if (date != (DateLastMetadataRefresh ?? DateTime.MinValue))
            {
                IsDirty = true;
            }

            DateLastMetadataRefresh = date;
        }

        public void SetDateLastImagesRefresh(DateTime date)
        {
            if (date != (DateLastImagesRefresh ?? DateTime.MinValue))
            {
                IsDirty = true;
            }

            DateLastImagesRefresh = date;
        }
    }
}
