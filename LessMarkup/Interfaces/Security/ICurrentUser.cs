﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using LessMarkup.Interfaces.Data;

namespace LessMarkup.Interfaces.Security
{
    public interface ICurrentUser
    {
        long? UserId { get; }
        IReadOnlyList<long> Groups { get; }
        IReadOnlyDictionary<string, string> Properties { get; }
        bool IsAdministrator { get; }
        bool IsApproved { get; }
        bool IsFakeUser { get; }
        bool EmailConfirmed { get; }
        void MapCurrentUser();
        string Email { get; }
        string Name { get; }
        void Logout();
        bool LoginWithPassword(string email, string password, bool savePassword, bool allowAdmin, bool allowRegular, string address, string encodedPassword);
        bool LoginWithOAuth(string provider, string providerUserId, bool savePassword, bool allowAdmin, bool allowRegular, string address);
        void DeleteSelf(string password);
        bool CheckPassword(IDomainModel domainModel, string password, string address);
        Tuple<string, string> LoginHash(string userName);
    }
}
