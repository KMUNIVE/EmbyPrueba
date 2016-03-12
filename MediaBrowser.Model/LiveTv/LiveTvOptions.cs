﻿using System.Collections.Generic;

namespace MediaBrowser.Model.LiveTv
{
    public class LiveTvOptions
    {
        public int? GuideDays { get; set; }
        public bool EnableMovieProviders { get; set; }
        public string RecordingPath { get; set; }
        public bool EnableAutoOrganize { get; set; }
        public bool EnableRecordingEncoding { get; set; }

        public List<TunerHostInfo> TunerHosts { get; set; }
        public List<ListingsProviderInfo> ListingProviders { get; set; }

        public int PrePaddingSeconds { get; set; }
        public int PostPaddingSeconds { get; set; }

        public LiveTvOptions()
        {
            EnableMovieProviders = true;
            TunerHosts = new List<TunerHostInfo>();
            ListingProviders = new List<ListingsProviderInfo>();
        }
    }

    public class TunerHostInfo
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
        public string DeviceId { get; set; }
        public bool ImportFavoritesOnly { get; set; }
        public bool IsEnabled { get; set; }

        public int DataVersion { get; set; }

        public TunerHostInfo()
        {
            IsEnabled = true;
        }
    }

    public class ListingsProviderInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ListingsId { get; set; }
        public string ZipCode { get; set; }
        public string Country { get; set; }
        public string Path { get; set; }

        public string[] EnabledTuners { get; set; }
        public bool EnableAllTuners { get; set; }

        public ListingsProviderInfo()
        {
            EnabledTuners = new string[] { };
            EnableAllTuners = true;
        }
    }
}