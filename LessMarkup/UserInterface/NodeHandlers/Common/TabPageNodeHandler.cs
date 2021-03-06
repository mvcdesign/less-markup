﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using LessMarkup.Framework.NodeHandlers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.Structure;
using LessMarkup.UserInterface.Model.Structure;
using Newtonsoft.Json;

namespace LessMarkup.UserInterface.NodeHandlers.Common
{
    public class TabPageNodeHandler : AbstractNodeHandler
    {
        class TabPage
        {
            public string Title { get; set; }
            public Type HandlerType { get; set; }
            public object ViewData { get; set; }
            public string ViewBody { get; set; }
            public string Path { get; set; }
            public string FullPath { get; set; }
            public long? PageId { get; set; }
            public string Settings { get; set; }
            public NodeAccessType? AccessType { get; set; }
            public string UniqueId { get; set; }
        }

        private readonly List<TabPage> _pages = new List<TabPage>();
        private readonly IDataCache _dataCache;
        private readonly ICurrentUser _currentUser;
        private readonly List<string> _scripts = new List<string>();

        protected void AddPage<T>(string title, string path = null) where T : INodeHandler
        {
            _pages.Add(new TabPage { HandlerType = typeof(T), Title = title});
        }

        protected void AddPage(Type type, string title, string path = null, long? pageId = null)
        {
            _pages.Add(new TabPage { HandlerType = type, Title = title, PageId = pageId });
        }

        public TabPageNodeHandler(IDataCache dataCache, ICurrentUser currentUser)
        {
            AddScript("controllers/tabpage");
            _dataCache = dataCache;
            _currentUser = currentUser;
        }

        protected override object Initialize(object controller)
        {
            var nodeCache = _dataCache.Get<INodeCache>();

            var parentPath = "";

            if (ObjectId.HasValue)
            {
                var currentNode = nodeCache.GetNode(ObjectId.Value);

                parentPath = currentNode.FullPath;

                if (currentNode.Children != null)
                {
                    foreach (var child in currentNode.Children.Where(c => c.Visible))
                    {
                        var accessType = child.CheckRights(_currentUser);

                        if (accessType == NodeAccessType.NoAccess)
                        {
                            continue;
                        }

                        _pages.Add(new TabPage
                        {
                            HandlerType = child.HandlerType,
                            PageId = child.NodeId,
                            Settings = child.Settings,
                            Path = child.Path,
                            FullPath = child.FullPath,
                            Title = child.Title,
                            AccessType = accessType
                        });
                    }
                }
            }

            _pages.Sort((a, b) => String.Compare(a.Title, b.Title, StringComparison.Ordinal));

            var unknownPath = 1;
            var uniqueId = 1;

            foreach (var page in _pages)
            {
                var handler = CreateChildHandler(page.HandlerType);

                object nodeSettings = null;

                if (handler.SettingsModel != null && !string.IsNullOrEmpty(page.Settings))
                {
                    nodeSettings = JsonConvert.DeserializeObject(page.Settings, handler.SettingsModel);
                }

                if (string.IsNullOrWhiteSpace(page.Path))
                {
                    page.Path = string.Format("page_{0}", unknownPath++);
                    page.FullPath = page.Path;

                    if (!string.IsNullOrWhiteSpace(parentPath))
                    {
                        page.FullPath = parentPath + "/" + page.Path;
                    }
                }

                handler.Initialize(page.PageId, nodeSettings, controller, page.Path, page.FullPath, page.AccessType ?? AccessType);

                page.ViewData = handler.GetViewData();
                page.ViewBody = LoadNodeViewModel.GetViewTemplate(handler, _dataCache, (System.Web.Mvc.Controller)controller);
                page.UniqueId = string.Format("page_{0}", uniqueId++);

                var scripts = handler.Scripts;

                if (scripts != null)
                {
                    _scripts.AddRange(scripts);
                }
            }

            _pages.RemoveAll(p => p.ViewBody == null);

            return null;
        }

        protected override Dictionary<string, object> GetViewData()
        {
            return new Dictionary<string, object>
            {
                { "Pages", _pages.Select(p => new
                    {
                        p.PageId,
                        Path = p.FullPath,
                        p.Title,
                        p.ViewBody,
                        p.ViewData,
                        p.UniqueId
                    }) },
                { "Requires", _scripts }
            };
        }

        protected override string ViewType
        {
            get { return "TabPage"; }
        }

        protected override bool HasChildren
        {
            get { return true; }
        }

        protected override ChildHandlerSettings GetChildHandler(string path)
        {
            var page = _pages.FirstOrDefault(p => p.Path == path);

            if (page == null)
            {
                return null;
            }

            var handler = (INodeHandler)Interfaces.DependencyResolver.Resolve(page.HandlerType);
            object nodeSettings = null;

            if (handler.SettingsModel != null && !string.IsNullOrEmpty(page.Settings))
            {
                nodeSettings = JsonConvert.DeserializeObject(page.Settings, handler.SettingsModel);
            }

            handler.Initialize(page.PageId, nodeSettings, null, page.Path, page.FullPath, page.AccessType ?? AccessType);

            return new ChildHandlerSettings
            {
                Handler = handler,
                Path = path,
                Title = page.Title,
                Id = page.PageId,
            };
        }
    }
}
