﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using LessMarkup.DataFramework.DataObjects;

namespace LessMarkup.DataObjects.Common
{
    public class SiteCustomization : SiteDataObject
    {
        public long SiteCustomizationId { get; set; }
        public string Path { get; set; }
        public SiteCustomizationType Type { get; set; }
        public byte[] Body { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
    }
}
