﻿using MediaBrowser.Model.Dto;
using ServiceStack.ServiceHost;

namespace MediaBrowser.Api.Playback
{
    /// <summary>
    /// Class StreamRequest
    /// </summary>
    public class StreamRequest
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "GET")]
        public string Id { get; set; }

        [ApiMember(Name = "DeviceId", Description = "The device id of the client requesting. Used to stop encoding processes when needed.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string DeviceId { get; set; }
        
        /// <summary>
        /// Gets or sets the audio codec.
        /// </summary>
        /// <value>The audio codec.</value>
        [ApiMember(Name = "AudioCodec", Description = "Optional. Specify a audio codec to encode to, e.g. mp3. If omitted the server will auto-select using the url's extension. Options: aac, mp3, vorbis, wma.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public AudioCodecs? AudioCodec { get; set; }

        /// <summary>
        /// Gets or sets the start time ticks.
        /// </summary>
        /// <value>The start time ticks.</value>
        [ApiMember(Name = "StartTimeTicks", Description = "Optional. Specify a starting offset, in ticks. 1 tick = 10000 ms", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public long? StartTimeTicks { get; set; }

        /// <summary>
        /// Gets or sets the audio bit rate.
        /// </summary>
        /// <value>The audio bit rate.</value>
        [ApiMember(Name = "AudioBitRate", Description = "Optional. Specify an audio bitrate to encode to, e.g. 128000. If omitted this will be left to encoder defaults.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? AudioBitRate { get; set; }

        /// <summary>
        /// Gets or sets the audio channels.
        /// </summary>
        /// <value>The audio channels.</value>
        [ApiMember(Name = "AudioChannels", Description = "Optional. Specify a specific number of audio channels to encode to, e.g. 2", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? AudioChannels { get; set; }

        /// <summary>
        /// Gets or sets the audio sample rate.
        /// </summary>
        /// <value>The audio sample rate.</value>
        [ApiMember(Name = "AudioSampleRate", Description = "Optional. Specify a specific audio sample rate, e.g. 44100", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? AudioSampleRate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="StreamRequest" /> is static.
        /// </summary>
        /// <value><c>true</c> if static; otherwise, <c>false</c>.</value>
        [ApiMember(Name = "Static", Description = "Optional. If true, the original file will be streamed statically without any encoding. Use either no url extension or the original file extension. true/false", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "GET")]
        public bool Static { get; set; }

        /// <summary>
        /// This is an xbox 360 param that is used with dlna. If true the item's image should be returned instead of audio or video.
        /// No need to put this in api docs since it's dlna only
        /// </summary>
        public bool AlbumArt { get; set; }
    }

    public class VideoStreamRequest : StreamRequest
    {
        /// <summary>
        /// Gets or sets the video codec.
        /// </summary>
        /// <value>The video codec.</value>
        [ApiMember(Name = "VideoCodec", Description = "Optional. Specify a video codec to encode to, e.g. h264. If omitted the server will auto-select using the url's extension. Options: h264, mpeg4, theora, vpx, wmv.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public VideoCodecs? VideoCodec { get; set; }

        /// <summary>
        /// Gets or sets the video bit rate.
        /// </summary>
        /// <value>The video bit rate.</value>
        [ApiMember(Name = "VideoBitRate", Description = "Optional. Specify a video bitrate to encode to, e.g. 500000. If omitted this will be left to encoder defaults.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? VideoBitRate { get; set; }

        /// <summary>
        /// Gets or sets the index of the audio stream.
        /// </summary>
        /// <value>The index of the audio stream.</value>
        [ApiMember(Name = "AudioStreamIndex", Description = "Optional. The index of the audio stream to use. If omitted the first audio stream will be used.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? AudioStreamIndex { get; set; }

        /// <summary>
        /// Gets or sets the index of the video stream.
        /// </summary>
        /// <value>The index of the video stream.</value>
        [ApiMember(Name = "VideoStreamIndex", Description = "Optional. The index of the video stream to use. If omitted the first video stream will be used.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? VideoStreamIndex { get; set; }

        /// <summary>
        /// Gets or sets the index of the subtitle stream.
        /// </summary>
        /// <value>The index of the subtitle stream.</value>
        [ApiMember(Name = "SubtitleStreamIndex", Description = "Optional. The index of the subtitle stream to use. If omitted no subtitles will be used.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? SubtitleStreamIndex { get; set; }

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        /// <value>The width.</value>
        [ApiMember(Name = "Width", Description = "Optional. The fixed horizontal resolution of the encoded video.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        /// <value>The height.</value>
        [ApiMember(Name = "Height", Description = "Optional. The fixed vertical resolution of the encoded video.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? Height { get; set; }

        /// <summary>
        /// Gets or sets the width of the max.
        /// </summary>
        /// <value>The width of the max.</value>
        [ApiMember(Name = "MaxWidth", Description = "Optional. The maximum horizontal resolution of the encoded video.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? MaxWidth { get; set; }

        /// <summary>
        /// Gets or sets the height of the max.
        /// </summary>
        /// <value>The height of the max.</value>
        [ApiMember(Name = "MaxHeight", Description = "Optional. The maximum vertical resolution of the encoded video.", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int? MaxHeight { get; set; }

        /// <summary>
        /// Gets or sets the framerate.
        /// </summary>
        /// <value>The framerate.</value>
        [ApiMember(Name = "Framerate", Description = "Optional. A specific video framerate to encode to, e.g. 23.976. Generally this should be omitted unless the device has specific requirements.", IsRequired = false, DataType = "double", ParameterType = "query", Verb = "GET")]
        public double? Framerate { get; set; }

        /// <summary>
        /// Gets or sets the profile.
        /// </summary>
        /// <value>The profile.</value>
        [ApiMember(Name = "Profile", Description = "Optional. Specify a specific h264 profile, e.g. main, baseline, high.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string Profile { get; set; }

        /// <summary>
        /// Gets or sets the level.
        /// </summary>
        /// <value>The level.</value>
        [ApiMember(Name = "Level", Description = "Optional. Specify a level for the h264 profile, e.g. 3, 3.1.", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string Level { get; set; }
    }
}
