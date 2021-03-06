﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using LessMarkup.Framework.NodeHandlers;
using LessMarkup.MainModule.Model;

namespace LessMarkup.MainModule.NodeHandlers
{
    public class ArticleNodeHandler : AbstractNodeHandler
    {
        protected override Dictionary<string, object> GetViewData()
        {
            var settings = GetSettings<ArticleModel>();

            return new Dictionary<string, object>
            {
                { "Body", settings != null ? settings.Body : "" }
            };
        }

        protected override Type SettingsModel
        {
            get { return typeof(ArticleModel); }
        }
    }
}
