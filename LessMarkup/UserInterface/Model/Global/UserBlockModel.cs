﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using LessMarkup.DataObjects.Security;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.Security;

namespace LessMarkup.UserInterface.Model.Global
{
    [RecordModel(TitleTextId = UserInterfaceTextIds.BlockUser)]
    public class UserBlockModel
    {
        [InputField(InputFieldType.Text, UserInterfaceTextIds.BlockReason)]
        public string Reason { get; set; }

        public string InternalReason { get; set; }

        [InputField(InputFieldType.Date, UserInterfaceTextIds.UnblockTime)]
        public DateTime? UnblockTime { get; set; }

        private readonly IDomainModelProvider _domainModelProvider;
        private readonly IChangeTracker _changeTracker;
        private readonly ICurrentUser _currentUser;

        public UserBlockModel(IDomainModelProvider domainModelProvider, IChangeTracker changeTracker, ICurrentUser currentUser)
        {
            _domainModelProvider = domainModelProvider;
            _changeTracker = changeTracker;
            _currentUser = currentUser;
        }

        public void BlockUser(long userId)
        {
            var currentUserId = _currentUser.UserId;

            if (!currentUserId.HasValue)
            {
                throw new UnauthorizedAccessException();
            }

            using (var domainModel = _domainModelProvider.Create())
            {
                var user = domainModel.Query().From<DataObjects.Security.User>().Find<DataObjects.Security.User>(userId);
                user.IsBlocked = true;
                user.BlockReason = Reason;
                if (UnblockTime.HasValue && UnblockTime.Value < DateTime.UtcNow)
                {
                    UnblockTime = null;
                }
                user.UnblockTime = UnblockTime;
                user.LastBlock = DateTime.UtcNow;

                var blockHistory = new UserBlockHistory
                {
                    BlockedByUserId = currentUserId.Value,
                    BlockedToTime = UnblockTime,
                    Reason = Reason,
                    UserId = userId,
                    Created = user.LastBlock.Value,
                    InternalReason = InternalReason,
                };

                domainModel.Create(blockHistory);
                _changeTracker.AddChange(user, EntityChangeType.Updated, domainModel);
            }
        }

        public void UnblockUser(long userId)
        {
            using (var domainModel = _domainModelProvider.Create())
            {
                var user = domainModel.Query().From<DataObjects.Security.User>().Find<DataObjects.Security.User>(userId);

                if (!user.IsBlocked)
                {
                    return;
                }

                user.IsBlocked = false;

                foreach (var history in domainModel.Query().From<UserBlockHistory>().Where("UserId = $ AND IsUnblocked = $", userId, false).ToList<UserBlockHistory>())
                {
                    history.IsUnblocked = true;
                    domainModel.Update(history);
                }

                _changeTracker.AddChange(user, EntityChangeType.Updated, domainModel);
            }
        }
    }
}
