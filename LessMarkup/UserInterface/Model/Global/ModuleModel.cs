﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using LessMarkup.DataFramework.DataAccess;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.Structure;
using LessMarkup.Interfaces.System;

namespace LessMarkup.UserInterface.Model.Global
{
    [RecordModel(CollectionType = typeof(Collection))]
    public class ModuleModel
    {
        public class Collection : IModelCollection<ModuleModel>
        {
            private long? _siteId;
            private readonly ISiteMapper _siteMapper;

            public Collection(ISiteMapper siteMapper)
            {
                _siteMapper = siteMapper;
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

                    throw new Exception("Unknown site");
                }
            }

            public IQueryable<long> ReadIds(IDomainModel domainModel, string filter, bool ignoreOrder)
            {
                return domainModel.GetCollection<Module>().Where(m => !m.Removed && !m.System && m.Enabled).Select(m => m.Id);
            }

            public int CollectionId { get { return AbstractDomainModel.GetCollectionIdVerified<Module>(); } }

            public IQueryable<ModuleModel> Read(IDomainModel domainModel, List<long> ids)
            {
                var modules =
                    domainModel.GetCollection<Module>()
                        .Where(m => ids.Contains(m.Id) && !m.Removed && !m.System && m.Enabled)
                        .Select(m => new ModuleModel
                        {
                            Name = m.Name,
                            ModuleId = m.Id,
                        }).ToList();

                foreach (var moduleId in domainModel.GetCollection<SiteModule>().Where(s => s.SiteId == SiteId && ids.Contains(s.ModuleId)).Select(s => s.ModuleId))
                {
                    var module = modules.First(m => m.ModuleId == moduleId);
                    module.Enabled = true;
                }

                return modules.AsQueryable();
            }

            public bool Filtered { get { return false; } }

            public void Initialize(long? objectId, NodeAccessType nodeAccessType)
            {
                _siteId = objectId;
            }
        }

        public long ModuleId { get; set; }

        [Column(UserInterfaceTextIds.Name)]
        public string Name { get; set; }

        [Column(UserInterfaceTextIds.Enabled)]
        public bool Enabled { get; set; }
    }
}
