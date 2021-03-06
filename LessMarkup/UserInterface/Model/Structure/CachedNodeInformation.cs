﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.Structure;
using LessMarkup.Interfaces.System;

namespace LessMarkup.UserInterface.Model.Structure
{
    public class CachedNodeInformation : ICachedNodeInformation
    {
        private readonly List<ICachedNodeInformation> _children = new List<ICachedNodeInformation>(); 

        public long NodeId { get; set; }
        public bool Enabled { get; set; }
        public string Path { get; set; }
        public int Order { get; set; }
        public int Level { get; set; }
        public string Title { get; set; }
        public string HandlerId { get; set; }
        public long? ParentNodeId { get; set; }
        public ICachedNodeInformation Parent { get; set; }
        public List<CachedNodeAccess> AccessList { get; set; }
        public IReadOnlyList<ICachedNodeInformation> Children { get { return _children; } }
        public string FullPath { get; set; }
        public Type HandlerType { get; set; }
        public string HandlerModuleType { get; set; }
        public string Settings { get; set; }
        public ICachedNodeInformation Root { get; set; }
        public bool Visible { get; set; }
        public bool AddToMenu { get; set; }
        public bool LoggedIn { get; set; }
        public string Description { get; set; }

        public void AddChild(ICachedNodeInformation node)
        {
            _children.Add(node);
        }

        private static bool AppliesTo(CachedNodeAccess nodeAccess, long? userId, IEnumerable<long> groupIds)
        {
            if (!userId.HasValue)
            {
                return !nodeAccess.UserId.HasValue && !nodeAccess.GroupId.HasValue;
            }

            if (nodeAccess.UserId.HasValue)
            {
                return nodeAccess.UserId.Value == userId.Value;
            }

            if (nodeAccess.GroupId.HasValue)
            {
                return groupIds != null && groupIds.Contains(nodeAccess.GroupId.Value);
            }

            return false;
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private void CheckRights(long? userId, IReadOnlyList<long> groupIds, ref NodeAccessType? accessType)
        {
            if (Parent != null)
            {
                ((CachedNodeInformation)Parent).CheckRights(userId, groupIds, ref accessType);
            }

            if (AccessList == null)
            {
                return;
            }

            var nodeAccess = AccessList.Where(a => AppliesTo(a, userId, groupIds)).Max(a => (NodeAccessType?)a.AccessType);

            if (nodeAccess.HasValue && (!accessType.HasValue || accessType.Value > nodeAccess.Value))
            {
                accessType = nodeAccess;
            }
        }

        public NodeAccessType CheckRights(IUserCache userCache, long? userId, NodeAccessType defaultAccessType = NodeAccessType.Read)
        {
            if (userCache.IsAdministrator)
            {
                return NodeAccessType.Manage;
            }

            NodeAccessType? accessType = null;
            CheckRights(userId, userCache.Groups, ref accessType);

            if (accessType.HasValue && accessType.Value != NodeAccessType.NoAccess && (!userCache.IsApproved || !userCache.EmailConfirmed))
            {
                accessType = NodeAccessType.Read;
            }

            return accessType ?? defaultAccessType;
        }

        public NodeAccessType CheckRights(ICurrentUser currentUser, NodeAccessType defaultAccessType = NodeAccessType.Read)
        {
            if (currentUser.IsAdministrator)
            {
                return NodeAccessType.Manage;
            }

            NodeAccessType? accessType = null;
            CheckRights(currentUser.UserId, currentUser.Groups, ref accessType);

            if (accessType.HasValue && accessType.Value != NodeAccessType.NoAccess && (!currentUser.IsApproved || !currentUser.EmailConfirmed))
            {
                accessType = NodeAccessType.Read;
            }

            return accessType ?? defaultAccessType;
        }
    }
}
