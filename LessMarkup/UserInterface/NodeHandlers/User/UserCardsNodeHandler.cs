﻿using System.Linq;
using LessMarkup.Interfaces;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.Structure;
using LessMarkup.Interfaces.System;
using LessMarkup.UserInterface.Model.User;
using LessMarkup.UserInterface.NodeHandlers.Common;

namespace LessMarkup.UserInterface.NodeHandlers.User
{
    public class UserCardsNodeHandler : RecordListNodeHandler<UserCardModel>
    {
        private readonly IDataCache _dataCache;

        public UserCardsNodeHandler(IDomainModelProvider domainModelProvider, IDataCache dataCache, ICurrentUser currentUser) : base(domainModelProvider, dataCache, currentUser)
        {
            _dataCache = dataCache;
        }

        protected override bool HasChildren
        {
            get { return true; }
        }

        protected override ChildHandlerSettings GetChildHandler(string path)
        {
            var parts = path.Split(new[] {'/'});
            if (parts.Length == 0)
            {
                return null;
            }

            long userId;
            if (!long.TryParse(parts[0], out userId))
            {
                return null;
            }

            var handler = DependencyResolver.Resolve<UserCardNodeHandler>();

            handler.Initialize(userId);

            ((INodeHandler)handler).Initialize(null, null, null, parts[0], "", AccessType);

            var userCache = _dataCache.Get<IUserCache>(userId);

            return new ChildHandlerSettings
            {
                Handler = handler,
                Id = userId,
                Path = parts[0],
                Rest = string.Join("/", parts.Skip(1)),
                Title = userCache.Name
            };
        }
    }
}
