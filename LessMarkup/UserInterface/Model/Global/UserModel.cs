﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.System;

namespace LessMarkup.UserInterface.Model.Global
{
    [RecordModel(CollectionType = typeof(Collection))]
    public class UserModel
    {
        public class Collection : IEditableModelCollection<UserModel>
        {
            private long? _siteId;
            private readonly IDomainModelProvider _domainModelProvider;
            private readonly IUserSecurity _userSecurity;
            private readonly IChangeTracker _changeTracker;
            private readonly ISiteMapper _siteMapper;

            public Collection(IDomainModelProvider domainModelProvider, IUserSecurity userSecurity, IChangeTracker changeTracker, ISiteMapper siteMapper)
            {
                _domainModelProvider = domainModelProvider;
                _userSecurity = userSecurity;
                _changeTracker = changeTracker;
                _siteMapper = siteMapper;
            }

            public void Initialize(long? siteId)
            {
                _siteId = siteId;
            }

            private long SiteId
            {
                get
                {
                    var siteId = _siteId ?? _siteMapper.SiteId;
                    if (siteId.HasValue)
                    {
                        return siteId.Value;
                    }

                    throw new Exception("Unknown site id");
                }
            }

            public IQueryable<long> ReadIds(IDomainModel domainModel, string filter)
            {
                return
                    domainModel.GetCollection<DataObjects.User.User>().Where(u => !u.IsRemoved && u.SiteId.Value == SiteId).Select(u => u.UserId);
            }

            public IQueryable<UserModel> Read(IDomainModel domainModel, List<long> ids)
            {
                return domainModel.GetCollection<DataObjects.User.User>().Where(u => !u.IsRemoved && u.SiteId.Value == SiteId && ids.Contains(u.UserId)).Select(u => new UserModel
                    {
                        Email = u.Email,
                        Id = u.UserId,
                        Name = u.Name,
                        IsAdministrator = u.IsAdministrator,
                        Password = ""
                    });
            }

            public bool Filtered { get { return false; } }

            public UserModel AddRecord(UserModel record, bool returnObject)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    var user = new DataObjects.User.User
                    {
                        Email = record.Email,
                        Name = record.Name,
                        Registered = DateTime.UtcNow,
                        IsBlocked = false,
                        IsValidated = true,
                        LastPasswordChanged = DateTime.UtcNow,
                        IsAdministrator = record.IsAdministrator,
                        SiteId = SiteId
                    };

                    string userSalt, encodedPassword;
                    _userSecurity.ChangePassword(record.Password, out userSalt, out encodedPassword);

                    user.Password = encodedPassword;
                    user.Salt = userSalt;
                    user.PasswordAutoGenerated = false;

                    domainModel.GetCollection<DataObjects.User.User>().Add(user);
                    domainModel.SaveChanges();

                    _changeTracker.AddChange(user.UserId, EntityType.User, EntityChangeType.Added, domainModel);
                    domainModel.SaveChanges();

                    domainModel.CompleteTransaction();

                    user.Password = null;

                    if (!returnObject)
                    {
                        return null;
                    }

                    return new UserModel
                    {
                        Email = user.Email,
                        Name = user.Name,
                        Id = user.UserId,
                        IsAdministrator = user.IsAdministrator
                    };
                }
            }

            public UserModel UpdateRecord(UserModel record, bool returnObject)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    var user = domainModel.GetCollection<DataObjects.User.User>().First(u => !u.IsRemoved && u.SiteId.Value == SiteId && u.UserId == record.Id);

                    user.Name = record.Name;
                    user.Email = record.Email;
                    user.IsAdministrator = record.IsAdministrator;

                    if (!string.IsNullOrWhiteSpace(record.Password))
                    {
                        string userSalt, encodedPassword;
                        _userSecurity.ChangePassword(record.Password, out userSalt, out encodedPassword);

                        user.Password = encodedPassword;
                        user.Salt = userSalt;
                        user.PasswordAutoGenerated = false;
                        user.LastPasswordChanged = DateTime.UtcNow;
                    }

                    _changeTracker.AddChange(user.UserId, EntityType.User, EntityChangeType.Updated, domainModel);
                    domainModel.SaveChanges();
                    domainModel.CompleteTransaction();

                    if (!returnObject)
                    {
                        return null;
                    }

                    return new UserModel
                    {
                        Email = user.Email,
                        Name = user.Name,
                        Id = user.UserId,
                        IsAdministrator = user.IsAdministrator
                    };
                }
            }

            public bool DeleteRecords(IEnumerable<long> recordIds)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    foreach (var userId in recordIds)
                    {
                        var user = domainModel.GetCollection<DataObjects.User.User>().First(u => !u.IsRemoved && u.SiteId == SiteId && u.UserId == userId);
                        domainModel.GetCollection<DataObjects.User.User>().Remove(user);
                        _changeTracker.AddChange(userId, EntityType.User, EntityChangeType.Removed, domainModel);
                    }
                    domainModel.SaveChanges();
                    domainModel.CompleteTransaction();
                }
                return true;
            }

            public bool DeleteOnly { get { return false; } }
        }

        public long Id { get; set; }

        [Column(UserInterfaceTextIds.UserName)]
        [InputField(InputFieldType.Text, UserInterfaceTextIds.UserName, Required = true)]
        public string Name { get; set; }

        [Column(UserInterfaceTextIds.UserEmail)]
        [InputField(InputFieldType.Email, UserInterfaceTextIds.UserEmail, Required = true)]
        public string Email { get; set; }

        [InputField(InputFieldType.Password, UserInterfaceTextIds.Password, Required = true)]
        public string Password { get; set; }

        [Column(UserInterfaceTextIds.IsAdministrator)]
        [InputField(InputFieldType.CheckBox, UserInterfaceTextIds.IsAdministrator, DefaultValue = false)]
        public bool IsAdministrator { get; set; }
    }
}
