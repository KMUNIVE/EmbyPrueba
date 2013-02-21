﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Providers
{
    public class BaseProviderInfo
    {
        public Guid ProviderId { get; set; }
        public DateTime LastRefreshed { get; set; }

    }
}
