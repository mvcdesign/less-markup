﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using LessMarkup.DataObjects.Structure;
using LessMarkup.DataObjects.User;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.System;

namespace LessMarkup.UserInterface.Model.Configuration
{
    [RecordModel(CollectionType = typeof(CollectionManager))]
    public class NodeAccessModel
    {
        public class CollectionManager : IEditableModelCollection<NodeAccessModel>
        {
            private long _nodeId;
            private long? _siteId;

            private readonly IDomainModelProvider _domainModelProvider;
            private readonly IChangeTracker _changeTracker;
            private readonly ISiteMapper _siteMapper;

            public CollectionManager(IDomainModelProvider domainModelProvider, IChangeTracker changeTracker, ISiteMapper siteMapper)
            {
                _domainModelProvider = domainModelProvider;
                _changeTracker = changeTracker;
                _siteMapper = siteMapper;
            }

            public void Initialize(long? siteId, long nodeId)
            {
                _nodeId = nodeId;
                _siteId = siteId;
            }

            public IQueryable<long> ReadIds(IDomainModel domainModel, string filter)
            {
                return domainModel.GetSiteCollection<PageAccess>(_siteId).Where(a => a.PageId == _nodeId).Select(a => a.PageAccessId);
            }

            public IQueryable<NodeAccessModel> Read(IDomainModel domainModel, List<long> ids)
            {
                return domainModel.GetSiteCollection<PageAccess>(_siteId).Where(a => a.PageId == _nodeId && ids.Contains(a.PageAccessId)).Select(a => new NodeAccessModel
                {
                    AccessType = a.AccessType,
                    User = a.User.Email,
                    Group = a.Group.Name,
                    AccessId = a.PageAccessId
                });
            }

            public bool Filtered { get { return false; } }

            public NodeAccessModel AddRecord(NodeAccessModel record, bool returnObject)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    var access = new PageAccess
                    {
                        AccessType = record.AccessType,
                        PageId = _nodeId,
                    };

                    var siteId = _siteId ?? _siteMapper.SiteId;

                    if (siteId.HasValue && !string.IsNullOrWhiteSpace(record.User))
                    {
                        access.UserId = domainModel.GetCollection<User>().Single(u => u.SiteId == _siteId.Value && u.Email == record.User).UserId;
                    }

                    if (siteId.HasValue && !string.IsNullOrWhiteSpace(record.Group))
                    {
                        access.GroupId = domainModel.GetSiteCollection<UserGroup>(_siteId).Single(g => g.Name == record.Group).UserGroupId;
                    }

                    domainModel.GetSiteCollection<PageAccess>(_siteId).Add(access);
                    _changeTracker.AddChange(_nodeId, EntityType.Page, EntityChangeType.Updated, domainModel);
                    domainModel.SaveChanges();
                    domainModel.CompleteTransaction();

                    record.AccessId = access.PageAccessId;
                }

                if (!returnObject)
                {
                    return null;
                }

                return record;
            }

            public NodeAccessModel UpdateRecord(NodeAccessModel record, bool returnObject)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    var access = domainModel.GetSiteCollection<PageAccess>(_siteId).Single(a => a.PageAccessId == record.AccessId);
                    access.AccessType = record.AccessType;

                    var siteId = _siteId ?? _siteMapper.SiteId;

                    if (siteId.HasValue && !string.IsNullOrWhiteSpace(record.User))
                    {
                        access.UserId = domainModel.GetCollection<User>().Single(u => u.SiteId == _siteId.Value && u.Email == record.User).UserId;
                    }
                    else
                    {
                        access.UserId = null;
                    }

                    if (siteId.HasValue && !string.IsNullOrWhiteSpace(record.Group))
                    {
                        access.GroupId = domainModel.GetSiteCollection<UserGroup>(_siteId).Single(g => g.Name == record.Group).UserGroupId;
                    }
                    else
                    {
                        access.GroupId = null;
                    }

                    _changeTracker.AddChange(_nodeId, EntityType.Page, EntityChangeType.Updated, domainModel);
                    domainModel.SaveChanges();
                    domainModel.CompleteTransaction();
                }

                return record;
            }

            public bool DeleteRecords(IEnumerable<long> recordIds)
            {
                using (var domainModel = _domainModelProvider.CreateWithTransaction())
                {
                    var hasChanges = false;

                    foreach (var record in domainModel.GetSiteCollection<PageAccess>(_siteId).Where(a => recordIds.Contains(a.PageAccessId)))
                    {
                        domainModel.GetSiteCollection<PageAccess>(_siteId).Remove(record);
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        _changeTracker.AddChange(_nodeId, EntityType.Page, EntityChangeType.Updated, domainModel);
                        domainModel.SaveChanges();
                        domainModel.CompleteTransaction();
                    }

                    return hasChanges;
                }
            }
        }

        public long AccessId { get; set; }

        [InputField(InputFieldType.Select, UserInterfaceTextIds.AccessType, DefaultValue = PageAccessType.Read)]
        [Column(UserInterfaceTextIds.AccessType)]
        public PageAccessType AccessType { get; set; }

        [InputField(InputFieldType.Typeahead, UserInterfaceTextIds.User)]
        [Column(UserInterfaceTextIds.User)]
        public string User { get; set; }

        [InputField(InputFieldType.Typeahead, UserInterfaceTextIds.Group)]
        [Column(UserInterfaceTextIds.Group)]
        public string Group { get; set; }
    }
}
