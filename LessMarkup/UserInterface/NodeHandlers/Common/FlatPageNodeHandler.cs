﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using LessMarkup.Framework.NodeHandlers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Structure;
using LessMarkup.UserInterface.Model.Common;
using LessMarkup.UserInterface.Model.Structure;
using Newtonsoft.Json;

namespace LessMarkup.UserInterface.NodeHandlers.Common
{
    public class FlatPageNodeHandler : AbstractNodeHandler
    {
        private readonly IDataCache _dataCache;

        class FlatNodeEntry
        {
            public string Title { get; set; }
            public object ViewData { get; set; }
            public string ViewBody { get; set; }
            public Type HandlerType { get; set; }
            public long NodeId { get; set; }
            public string Settings { get; set; }
            public string Anchor { get; set; }
            public string UniqueId { get; set; }
            public int Level { get; set; }
            public string Path { get; set; }
        }

        class TreeNodeEntry
        {
            public string Anchor { get; set; }
            public string Title { get; set; }

            public List<TreeNodeEntry> Children { get; set; }
        }

        public FlatPageNodeHandler(IDataCache dataCache)
        {
            _dataCache = dataCache;
        }

        private void FillFlatList(CachedNodeInformation parent, List<FlatNodeEntry> nodes, TreeNodeEntry parentTreeNode, string anchor = "", int level = 1, int maxLevel = 2)
        {
            if (parent.Children == null)
            {
                return;
            }

            foreach (var child in parent.Children)
            {
                if (child.HandlerType == typeof (FlatPageNodeHandler))
                {
                    continue;
                }

                var childAnchor = string.IsNullOrEmpty(anchor) ? "" : anchor + "_";

                var entry = new FlatNodeEntry
                {
                    Title = child.Title,
                    HandlerType = child.HandlerType,
                    NodeId = child.NodeId,
                    Settings = child.Settings,
                    Anchor = childAnchor + child.Path,
                    Level = level,
                    Path = child.FullPath
                };

                var treeNode = new TreeNodeEntry
                {
                    Title = entry.Title,
                    Anchor = entry.Anchor,
                    Children = new List<TreeNodeEntry>()
                };

                parentTreeNode.Children.Add(treeNode);

                nodes.Add(entry);

                if (level < maxLevel)
                {
                    FillFlatList(child, nodes, treeNode, entry.Anchor, level + 1, maxLevel);
                }
            }
        }

        public override object GetViewData(long objectId, object settings, object controller)
        {
            var settingsModel = (FlatPageSettingsModel) settings;

            var nodeCache = _dataCache.Get<NodeCache>();

            var currentNode = nodeCache.GetNode(objectId);

            var flatNodeList = new List<FlatNodeEntry>();
            var treeRoot = new TreeNodeEntry {Children = new List<TreeNodeEntry>()};

            var maxLevel = 2;

            FillFlatList(currentNode, flatNodeList, treeRoot, "", 1, maxLevel);

            if (settingsModel == null || settingsModel.LoadOnShow)
            {
                foreach (var node in flatNodeList)
                {
                    var handler = (INodeHandler) Interfaces.DependencyResolver.Resolve(node.HandlerType);
                    object nodeSettings = null;

                    if (handler.SettingsModel != null && !string.IsNullOrEmpty(node.Settings))
                    {
                        nodeSettings = JsonConvert.DeserializeObject(node.Settings, handler.SettingsModel);
                    }

                    node.ViewData = handler.GetViewData(node.NodeId, nodeSettings, controller);
                    node.ViewBody = LoadNodeViewModel.GetViewTemplate(handler, _dataCache, (System.Web.Mvc.Controller) controller);
                }
            }

            var pageIndex = 1;

            foreach (var node in flatNodeList)
            {
                node.UniqueId = string.Format("flatpage{0}", pageIndex++);
            }

            return new
            {
                Tree = treeRoot.Children,
                Flat = flatNodeList,
                Position = settingsModel != null ? settingsModel.Position : FlatPagePosition.Right
            };
        }

        public override Type SettingsModel
        {
            get { return typeof(FlatPageSettingsModel); }
        }
    }
}
