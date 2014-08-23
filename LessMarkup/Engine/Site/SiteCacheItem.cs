﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;

namespace LessMarkup.Engine.Site
{
    public class SiteCacheItem
    {
        public long? SiteId { get; set; }
        public HashSet<string> Hosts { get; set; }
        public string Title { get; set; }
        public bool Enabled { get; set; }
        public HashSet<string> ModuleTypes { get; set; }
        public string Properties { get; set; }
    }
}
