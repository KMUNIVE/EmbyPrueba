﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Users;

namespace MediaBrowser.Controller.LiveTv
{
    public class RecordingGroup : Folder
    {
        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            // Don't block. 
            return false;
        }

        public override bool SupportsLocalMetadata
        {
            get
            {
                return false;
            }
        }
    }
}
