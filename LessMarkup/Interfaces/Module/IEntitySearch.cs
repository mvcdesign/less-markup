﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using LessMarkup.Interfaces.Data;

namespace LessMarkup.Interfaces.Module
{
    public interface IEntitySearch
    {
        string ValidateAndGetUrl(int collectionId, long entityId, IDomainModel domainModel);
        string GetFriendlyName(int collectionId);
    }
}
