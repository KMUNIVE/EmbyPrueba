﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MediaBrowser.Controller.LiveTv
{
    public class LiveTvChannel : BaseItem, IItemByName
    {
        public LiveTvChannel()
        {
            UserItemCountList = new List<ItemByNameCounts>();
        }

        /// <summary>
        /// Gets the user data key.
        /// </summary>
        /// <returns>System.String.</returns>
        public override string GetUserDataKey()
        {
            return GetClientTypeName() + "-" + Name;
        }

        [IgnoreDataMember]
        public List<ItemByNameCounts> UserItemCountList { get; set; }

        /// <summary>
        /// Gets or sets the number.
        /// </summary>
        /// <value>The number.</value>
        public string Number { get; set; }

        /// <summary>
        /// Gets or sets the external identifier.
        /// </summary>
        /// <value>The external identifier.</value>
        public string ExternalId { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the channel.
        /// </summary>
        /// <value>The type of the channel.</value>
        public ChannelType ChannelType { get; set; }

        public string ServiceName { get; set; }

        /// <summary>
        /// Supply the image path if it can be accessed directly from the file system
        /// </summary>
        /// <value>The image path.</value>
        public string ProviderImagePath { get; set; }

        /// <summary>
        /// Supply the image url if it can be downloaded
        /// </summary>
        /// <value>The image URL.</value>
        public string ProviderImageUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has image.
        /// </summary>
        /// <value><c>null</c> if [has image] contains no value, <c>true</c> if [has image]; otherwise, <c>false</c>.</value>
        public bool? HasProviderImage { get; set; }

        protected override string CreateSortName()
        {
            double number = 0;

            if (!string.IsNullOrEmpty(Number))
            {
                double.TryParse(Number, out number);
            }

            return number.ToString("000-") + (Name ?? string.Empty);
        }

        public override string MediaType
        {
            get
            {
                return ChannelType == ChannelType.Radio ? Model.Entities.MediaType.Audio : Model.Entities.MediaType.Video;
            }
        }

        public override string GetClientTypeName()
        {
            return "Channel";
        }
    }
}
