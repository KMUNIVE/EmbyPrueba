﻿using MediaBrowser.Model.Entities;

namespace MediaBrowser.Model.Session
{
    /// <summary>
    /// Class PlaybackProgressInfo.
    /// </summary>
    public class PlaybackProgressInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether this instance can seek.
        /// </summary>
        /// <value><c>true</c> if this instance can seek; otherwise, <c>false</c>.</value>
        public bool CanSeek { get; set; }

        /// <summary>
        /// Gets or sets the item.
        /// </summary>
        /// <value>The item.</value>
        public BaseItemInfo Item { get; set; }

        /// <summary>
        /// Gets or sets the item identifier.
        /// </summary>
        /// <value>The item identifier.</value>
        public string ItemId { get; set; }
        
        /// <summary>
        /// Gets or sets the session id.
        /// </summary>
        /// <value>The session id.</value>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the media version identifier.
        /// </summary>
        /// <value>The media version identifier.</value>
        public string MediaSourceId { get; set; }

        /// <summary>
        /// Gets or sets the index of the audio stream.
        /// </summary>
        /// <value>The index of the audio stream.</value>
        public int? AudioStreamIndex { get; set; }

        /// <summary>
        /// Gets or sets the index of the subtitle stream.
        /// </summary>
        /// <value>The index of the subtitle stream.</value>
        public int? SubtitleStreamIndex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is paused.
        /// </summary>
        /// <value><c>true</c> if this instance is paused; otherwise, <c>false</c>.</value>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is muted.
        /// </summary>
        /// <value><c>true</c> if this instance is muted; otherwise, <c>false</c>.</value>
        public bool IsMuted { get; set; }

        /// <summary>
        /// Gets or sets the position ticks.
        /// </summary>
        /// <value>The position ticks.</value>
        public long? PositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the volume level.
        /// </summary>
        /// <value>The volume level.</value>
        public int? VolumeLevel { get; set; }

        /// <summary>
        /// Gets or sets the play method.
        /// </summary>
        /// <value>The play method.</value>
        public PlayMethod PlayMethod { get; set; }
        /// <summary>
        /// Gets or sets the live stream identifier.
        /// </summary>
        /// <value>The live stream identifier.</value>
        public string LiveStreamId { get; set; }
        /// <summary>
        /// Gets or sets the play session identifier.
        /// </summary>
        /// <value>The play session identifier.</value>
        public string PlaySessionId { get; set; }
    }
}