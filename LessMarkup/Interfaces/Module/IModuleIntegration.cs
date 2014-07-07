﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Structure;
using LessMarkup.Interfaces.Text;

namespace LessMarkup.Interfaces.Module
{
    public interface IModuleIntegration
    {
        void RegisterBackgroundJobHandler(IBackgroundJobHandler backgroundJobHandler);
        bool DoBackgroundJobs(UrlHelper urlHelper);
        void RegisterSearchResultValidator(EntityType entityType, ISearchResultValidator validator);
        bool IsSearchResultValid(SearchResult searchResult);
        void RegisterNodeHandler<T>(string id) where T : INodeHandler;
        Tuple<Type, string> GetNodeHandler(string id);
        IEnumerable<string> GetNodeHandlers();
    }
}
