﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;

namespace LessMarkup.Interfaces.Data
{
    public class Site : NonSiteDataObject
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Host { get; set; }
        public bool Enabled { get; set; }
        public DateTime Created { get; set; }
        public string Properties { get; set; }

        public List<SiteModule> Modules { get; set; }
    }
}
