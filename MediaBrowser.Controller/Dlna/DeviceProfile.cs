﻿using MediaBrowser.Model.Entities;
using System;
using System.Linq;

namespace MediaBrowser.Controller.Dlna
{
    public class DeviceProfile
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the transcoding profiles.
        /// </summary>
        /// <value>The transcoding profiles.</value>
        public TranscodingProfile[] TranscodingProfiles { get; set; }

        /// <summary>
        /// Gets or sets the direct play profiles.
        /// </summary>
        /// <value>The direct play profiles.</value>
        public DirectPlayProfile[] DirectPlayProfiles { get; set; }

        public ContainerProfile[] ContainerProfiles { get; set; }

        /// <summary>
        /// Gets or sets the identification.
        /// </summary>
        /// <value>The identification.</value>
        public DeviceIdentification Identification { get; set; }

        public string FriendlyName { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerUrl { get; set; }
        public string ModelName { get; set; }
        public string ModelDescription { get; set; }
        public string ModelNumber { get; set; }
        public string ModelUrl { get; set; }
        public bool IgnoreTranscodeByteRangeRequests { get; set; }
        public bool SupportsAlbumArtInDidl { get; set; }

        /// <summary>
        /// Controls the content of the X_DLNADOC element in the urn:schemas-dlna-org:device-1-0 namespace.
        /// </summary>
        public string XDlnaDoc { get; set; }
        /// <summary>
        /// Controls the content of the X_DLNACAP element in the urn:schemas-dlna-org:device-1-0 namespace.
        /// </summary>
        public string XDlnaCap { get; set; }
        /// <summary>
        /// Controls the content of the aggregationFlags element in the urn:schemas-sonycom:av.
        /// </summary>
        public string SonyAggregationFlags { get; set; }

        public string ProtocolInfo { get; set; }

        public MediaProfile[] MediaProfiles { get; set; }
        public CodecProfile[] CodecProfiles { get; set; }

        public int TimelineOffsetSeconds { get; set; }

        public bool RequiresPlainVideoItems { get; set; }
        public bool RequiresPlainFolders { get; set; }

        public DeviceProfile()
        {
            DirectPlayProfiles = new DirectPlayProfile[] { };
            TranscodingProfiles = new TranscodingProfile[] { };
            MediaProfiles = new MediaProfile[] { };
            CodecProfiles = new CodecProfile[] { };
            ContainerProfiles = new ContainerProfile[] { };
        }

        public TranscodingProfile GetAudioTranscodingProfile(string container, string audioCodec)
        {
            container = (container ?? string.Empty).TrimStart('.');

            return TranscodingProfiles.FirstOrDefault(i =>
            {
                if (i.Type != DlnaProfileType.Audio)
                {
                    return false;
                }

                if (!string.Equals(container, i.Container, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!i.GetAudioCodecs().Contains(audioCodec ?? string.Empty))
                {
                    return false;
                }

                return true;
            });
        }

        public TranscodingProfile GetVideoTranscodingProfile(string container, string audioCodec, string videoCodec)
        {
            container = (container ?? string.Empty).TrimStart('.');

            return TranscodingProfiles.FirstOrDefault(i =>
            {
                if (i.Type != DlnaProfileType.Video)
                {
                    return false;
                }

                if (!string.Equals(container, i.Container, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!i.GetAudioCodecs().Contains(audioCodec ?? string.Empty))
                {
                    return false;
                }

                if (!string.Equals(videoCodec, i.VideoCodec, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            });
        }

        public MediaProfile GetAudioMediaProfile(string container, string audioCodec, MediaStream audioStream)
        {
            container = (container ?? string.Empty).TrimStart('.');

            return MediaProfiles.FirstOrDefault(i =>
            {
                if (i.Type != DlnaProfileType.Audio)
                {
                    return false;
                }

                var containers = i.GetContainers().ToList();
                if (containers.Count > 0 && !containers.Contains(container))
                {
                    return false;
                }

                var audioCodecs = i.GetAudioCodecs().ToList();
                if (audioCodecs.Count > 0 && !audioCodecs.Contains(audioCodec ?? string.Empty))
                {
                    return false;
                }

                return true;
            });
        }

        public MediaProfile GetVideoMediaProfile(string container, string audioCodec, string videoCodec, MediaStream audioStream, MediaStream videoStream)
        {
            container = (container ?? string.Empty).TrimStart('.');

            return MediaProfiles.FirstOrDefault(i =>
            {
                if (i.Type != DlnaProfileType.Video)
                {
                    return false;
                }

                var containers = i.GetContainers().ToList();
                if (containers.Count > 0 && !containers.Contains(container))
                {
                    return false;
                }

                var audioCodecs = i.GetAudioCodecs().ToList();
                if (audioCodecs.Count > 0 && !audioCodecs.Contains(audioCodec ?? string.Empty))
                {
                    return false;
                }

                var videoCodecs = i.GetVideoCodecs().ToList();
                if (videoCodecs.Count > 0 && !videoCodecs.Contains(videoCodec ?? string.Empty))
                {
                    return false;
                }

                return true;
            });
        }

        public MediaProfile GetPhotoMediaProfile(string container)
        {
            container = (container ?? string.Empty).TrimStart('.');

            return MediaProfiles.FirstOrDefault(i =>
            {
                if (i.Type != DlnaProfileType.Photo)
                {
                    return false;
                }

                var containers = i.GetContainers().ToList();
                if (containers.Count > 0 && !containers.Contains(container))
                {
                    return false;
                }

                return true;
            });
        }
    }
}
