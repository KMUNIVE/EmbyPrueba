﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MediaBrowser.Controller.LiveTv
{
    public class LiveTvAudioRecording : Audio, ILiveTvRecording
    {
        public string ExternalId { get; set; }
        public string ProviderImagePath { get; set; }
        public string ProviderImageUrl { get; set; }
        public string EpisodeTitle { get; set; }
        public bool IsSeries { get; set; }
        public string SeriesTimerId { get; set; }
        public DateTime StartDate { get; set; }
        public RecordingStatus Status { get; set; }
        public bool IsSports { get; set; }
        public bool IsNews { get; set; }
        public bool IsKids { get; set; }
        public bool IsRepeat { get; set; }
        public bool IsMovie { get; set; }
        public bool? IsHD { get; set; }
        public bool IsLive { get; set; }
        public bool IsPremiere { get; set; }
        public ChannelType ChannelType { get; set; }
        public string ProgramId { get; set; }
        public ProgramAudio? Audio { get; set; }
        public DateTime? OriginalAirDate { get; set; }

        /// <summary>
        /// Gets the user data key.
        /// </summary>
        /// <returns>System.String.</returns>
        protected override string CreateUserDataKey()
        {
            var name = GetClientTypeName();

            if (!string.IsNullOrEmpty(ProgramId))
            {
                return name + "-" + ProgramId;
            }

            return name + "-" + Name + (EpisodeTitle ?? string.Empty);
        }

        public string ServiceName { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is owned item.
        /// </summary>
        /// <value><c>true</c> if this instance is owned item; otherwise, <c>false</c>.</value>
        [IgnoreDataMember]
        public override bool IsOwnedItem
        {
            get
            {
                return false;
            }
        }

        [IgnoreDataMember]
        public override string MediaType
        {
            get
            {
                return Model.Entities.MediaType.Audio;
            }
        }

        [IgnoreDataMember]
        public override LocationType LocationType
        {
            get
            {
                if (!string.IsNullOrEmpty(Path))
                {
                    return base.LocationType;
                }

                return LocationType.Remote;
            }
        }

        public override string GetClientTypeName()
        {
            return "Recording";
        }

        public override bool IsSaveLocalMetadataEnabled()
        {
            return false;
        }

        [IgnoreDataMember]
        public override bool SupportsLocalMetadata
        {
            get
            {
                return false;
            }
        }

        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            return config.BlockUnratedItems.Contains(UnratedItem.LiveTvProgram);
        }

        protected override string GetInternalMetadataPath(string basePath)
        {
            return System.IO.Path.Combine(basePath, "livetv", Id.ToString("N"));
        }

        public override bool CanDelete()
        {
            return true;
        }

        public override bool IsAuthorizedToDelete(User user)
        {
            return user.Policy.EnableLiveTvManagement;
        }

        public override IEnumerable<MediaSourceInfo> GetMediaSources(bool enablePathSubstitution)
        {
            var list = base.GetMediaSources(enablePathSubstitution).ToList();

            foreach (var mediaSource in list)
            {
                if (string.IsNullOrWhiteSpace(mediaSource.Path))
                {
                    mediaSource.Type = MediaSourceType.Placeholder;
                }
            }

            return list;
        }
    }
}
