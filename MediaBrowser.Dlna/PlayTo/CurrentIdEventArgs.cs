﻿using System;

namespace MediaBrowser.Dlna.PlayTo
{
    public class CurrentIdEventArgs : EventArgs
    {
        public Guid Id { get;  set; }

        public CurrentIdEventArgs(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "0")
            {
                Id = Guid.Empty;
            }
            else
            {
                var guid = new Guid();
                Id = Guid.TryParse(id, out guid) ? guid : Guid.Empty;                
            }
        }
    }
}
