﻿using MediaBrowser.Controller.Dlna;

namespace MediaBrowser.Dlna.Profiles
{
    public class LinksysDMA2100Profile : DefaultProfile
    {
        public LinksysDMA2100Profile()
        {
            // Linksys DMA2100us does not need any transcoding of the formats we support statically
            Name = "Linksys DMA2100";

            ProfileId = "dma2100";

            Identification = new DeviceIdentification
            {
                ModelName = "DMA2100us"
            };

            DirectPlayProfiles = new[]
            {
                new DirectPlayProfile
                {
                    Container = "mp3,flac,m4a,wma",
                    Type = DlnaProfileType.Audio
                },

                new DirectPlayProfile
                {
                    Container = "avi,mp4,mkv,ts",
                    Type = DlnaProfileType.Video
                }
            };
        }
    }
}
