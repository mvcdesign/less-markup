﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Web.Mvc;
using LessMarkup.Framework.Helpers;
using LessMarkup.Framework.Language;
using LessMarkup.Interfaces.Module;
using Newtonsoft.Json;

namespace LessMarkup.UserInterface.Model
{
    public class ErrorModel
    {
        public string Error { get; set; }

        public void Initialize(Exception exception)
        {
            try
            {
                Error = LanguageHelper.GetText(ModuleType.Core, CoreTextIds.UnknownErrorOccurred);
            }
            catch (Exception)
            {
                Error = "Unknown Error Occurred";
            }
        }

        public ActionResult GetResult()
        {
            var content = JsonConvert.SerializeObject(
                new
                {
                    Success = false,
                    Error,
                });

            return new ContentResult
            {
                ContentType = "application/json",
                Content = content
            };
        }
    }
}
