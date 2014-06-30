﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using LessMarkup.DataObjects.Structure;

namespace LessMarkup.UserInterface.Model.Structure
{
    public class CachedPageAccess
    {
        public PageAccessType AccessType { get; set; }
        public long? UserId { get; set; }
        public long? GroupId { get; set; }
    }
}
