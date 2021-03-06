﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Web.Mvc;
using LessMarkup.DataFramework;
using LessMarkup.Framework.Helpers;

namespace LessMarkup.UserInterface.Model.Structure
{
    public class NodeErrorModel
    {
        public void Initialize(Exception e)
        {
        }

        public ActionResult CreateResult(System.Web.Mvc.Controller controller)
        {
            return new ContentResult
            {
                // TBD: Make smarter error description
                Content = LanguageHelper.GetText(Constants.ModuleType.UserInterface, UserInterfaceTextIds.UnknownError),
            };
        }
    }
}
