﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using LessMarkup.Forum.DataObjects;
using LessMarkup.Interfaces;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.Structure;

namespace LessMarkup.Forum.Model
{
    [RecordModel(CollectionType = typeof(Collection))]
    public class ThreadModel
    {
        public class Collection : IModelCollection<ThreadModel>
        {
            private long _forumId;

            public IQueryable<long> ReadIds(IDomainModel domainModel, string filter)
            {
                return domainModel.GetSiteCollection<Thread>().Where(t => t.ForumId == _forumId).Select(t => t.ThreadId);
            }

            public IQueryable<ThreadModel> Read(IDomainModel domainModel, List<long> ids)
            {
                return
                    domainModel.GetSiteCollection<Post>()
                        .Where(p => p.Thread.ForumId == _forumId && ids.Contains(p.ThreadId))
                        .GroupBy(p => p.Thread)
                        .Select(p => new ThreadModel
                        {
                            ThreadId = p.Key.ThreadId,
                            Name = p.Key.Name,
                            Description = p.Key.Description,
                            Created = p.Key.Created,
                            Updated = p.Key.Updated,
                            Path = p.Key.Path,
                            Removed = p.Key.Removed,
                            Closed = p.Key.Closed,
                            Posts = p.Count()
                        });
            }

            public bool Filtered { get { return false; } }

            public void Initialize(long? objectId, NodeAccessType accessType)
            {
                if (!objectId.HasValue)
                {
                    throw new NullReferenceException("objectId");
                }

                _forumId = objectId.Value;
            }
        }

        private readonly IDomainModelProvider _domainModelProvider;
        private readonly IChangeTracker _changeTracker;

        ThreadModel()
        {
        }

        public ThreadModel(IDomainModelProvider domainModelProvider, IChangeTracker changeTracker)
        {
            _domainModelProvider = domainModelProvider;
            _changeTracker = changeTracker;
        }

        public static ThreadModel GetByPath(long forumId, string path, IDomainModelProvider domainModelProvider)
        {
            using (var domainModel = domainModelProvider.Create())
            {
                var thread = domainModel.GetSiteCollection<Thread>().FirstOrDefault(t => t.ForumId == forumId && t.Path == path);
                if (thread == null)
                {
                    return null;
                }
                return new ThreadModel
                {
                    ThreadId = thread.ThreadId,
                    Path = thread.Path,
                    Name = thread.Name,
                    Description = thread.Description
                };
            }
        }

        public long ThreadId { get; set; }

        [Column(ForumTextIds.Name, CellTemplate = "~/Views/ThreadNameCell.html")]
        public string Name { get; set; }

        public string Description { get; set; }

        public string Path { get; set; }

        public bool Removed { get; set; }

        public bool Closed { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        [Column(ForumTextIds.Posts)]
        public int Posts { get; set; }

        public object Delete(NodeAccessType accessType, long forumId, long threadId, bool isManager)
        {
            using (var domainModel = _domainModelProvider.Create())
            {
                var thread = domainModel.GetSiteCollection<Thread>().First(t => t.ThreadId == threadId && t.ForumId == forumId);
                thread.Removed = true;
                _changeTracker.AddChange(threadId, EntityType.ForumThread, EntityChangeType.Removed, domainModel);
                domainModel.SaveChanges();

                if (!isManager)
                {
                    return new {removed = true};
                }

                var collection = DependencyResolver.Resolve<Collection>();
                collection.Initialize(forumId, accessType);

                var model = collection.Read(domainModel, new List<long> {threadId}).First();

                return new {record = model};
            }
        }

        public object Restore(NodeAccessType accessType, long forumId, long threadId, bool isManager)
        {
            using (var domainModel = _domainModelProvider.Create())
            {
                var thread = domainModel.GetSiteCollection<Thread>().First(t => t.ThreadId == threadId);
                thread.Removed = false;
                _changeTracker.AddChange(threadId, EntityType.ForumThread, EntityChangeType.Added, domainModel);
                domainModel.SaveChanges();

                var collection = DependencyResolver.Resolve<Collection>();
                collection.Initialize(forumId, accessType);

                var model = collection.Read(domainModel, new List<long> { threadId }).First();

                return new { record = model };
            }
        }

        public object Close(NodeAccessType accessType, long forumId, long threadId, bool isManager)
        {
            using (var domainModel = _domainModelProvider.Create())
            {
                var thread = domainModel.GetSiteCollection<Thread>().First(t => t.ThreadId == threadId);
                thread.Closed = true;
                _changeTracker.AddChange(threadId, EntityType.ForumThread, EntityChangeType.Added, domainModel);
                domainModel.SaveChanges();

                var collection = DependencyResolver.Resolve<Collection>();
                collection.Initialize(forumId, accessType);

                var model = collection.Read(domainModel, new List<long> { threadId }).First();

                return new { record = model };
            }
        }

        public object Open(NodeAccessType accessType, long forumId, long threadId, bool isManager)
        {
            using (var domainModel = _domainModelProvider.Create())
            {
                var thread = domainModel.GetSiteCollection<Thread>().First(t => t.ThreadId == threadId);
                thread.Closed = false;
                _changeTracker.AddChange(threadId, EntityType.ForumThread, EntityChangeType.Added, domainModel);
                domainModel.SaveChanges();

                var collection = DependencyResolver.Resolve<Collection>();
                collection.Initialize(forumId, accessType);

                var model = collection.Read(domainModel, new List<long> { threadId }).First();

                return new { record = model };
            }
        }
    }
}