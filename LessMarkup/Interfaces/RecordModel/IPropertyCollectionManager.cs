﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using LessMarkup.Interfaces.Data;

namespace LessMarkup.Interfaces.RecordModel
{
    public interface IPropertyCollectionManager
    {
        IReadOnlyCollection<string> GetCollection(IDomainModel domainModel, string property, string searchText);
    }
}
