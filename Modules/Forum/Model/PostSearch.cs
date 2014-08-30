﻿using System.Linq;
using LessMarkup.Forum.DataObjects;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Module;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.Structure;
using LessMarkup.Interfaces.System;

namespace LessMarkup.Forum.Model
{
    public class PostSearch : IEntitySearch
    {
        private readonly IDataCache _dataCache;
        private readonly ICurrentUser _currentUser;

        public PostSearch(IDataCache dataCache, ICurrentUser currentUser)
        {
            _dataCache = dataCache;
            _currentUser = currentUser;
        }

        public string ValidateAndGetUrl(int collectionId, long entityId, IDomainModel domainModel)
        {
            var post = domainModel.GetSiteCollection<Post>().Where(p => p.Id == entityId).Select(p => new { p.Thread.ForumId, p.Thread.Path, p.ThreadId }).FirstOrDefault();
            if (post == null)
            {
                return null;
            }

            var nodeCache = _dataCache.Get<INodeCache>();

            var node = nodeCache.GetNode(post.ForumId);

            if (node == null)
            {
                return null;

            }
            
            var rights = node.CheckRights(_currentUser);

            if (rights == NodeAccessType.NoAccess)
            {
                return null;
            }

            var url = string.Format("{0}/{1}", node.FullPath, post.Path);

            var postsQuery = domainModel.GetSiteCollection<Post>().Where(p => p.ThreadId == post.ThreadId);

            if (rights != NodeAccessType.Manage)
            {
                postsQuery = postsQuery.Where(p => !p.Removed);
            }

            var posts = postsQuery.Select(p => p.Id).ToList();

            var recordsPerPage = _dataCache.Get<ISiteConfiguration>().RecordsPerPage;

            var postIndex = posts.IndexOf(entityId);

            if (postIndex < 0)
            {
                postIndex = 0;
            }

            var page = (postIndex / recordsPerPage) + 1;

            return RecordListHelper.PageLink(url, page);
        }

        public string GetFriendlyName(int collectionId)
        {
            return LanguageHelper.GetText(Constants.ModuleType.Forum, ForumTextIds.PostName);
        }
    }
}
