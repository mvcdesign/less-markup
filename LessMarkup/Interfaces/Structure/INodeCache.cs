﻿using System;
using System.Collections.Generic;
using LessMarkup.Interfaces.Cache;

namespace LessMarkup.Interfaces.Structure
{
    public interface INodeCache : ICacheHandler
    {
        ICachedNodeInformation GetNode(long nodeId);
        void GetNode(string path, out ICachedNodeInformation node, out string rest);
        ICachedNodeInformation RootNode { get; }
        IReadOnlyList<ICachedNodeInformation> Nodes { get; }
        INodeHandler GetNodeHandler(string path, object controller = null, Func<INodeHandler, string, string, string, bool> preprocessFunc = null);
    }
}
