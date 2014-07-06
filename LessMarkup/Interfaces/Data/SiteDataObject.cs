﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.ComponentModel.DataAnnotations.Schema;

namespace LessMarkup.Interfaces.Data
{
    public abstract class SiteDataObject : DataObject, ISiteDataObject
    {
        [ForeignKey("Site")]
        public long? SiteId { get; set; }
        public Site Site { get; set; }
    }
}