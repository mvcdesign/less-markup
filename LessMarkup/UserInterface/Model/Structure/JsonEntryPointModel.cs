﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LessMarkup.Engine.FileSystem;
using LessMarkup.Engine.Helpers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Exceptions;
using LessMarkup.Interfaces.Security;
using LessMarkup.UserInterface.Exceptions;
using LessMarkup.UserInterface.Model.RecordModel;
using LessMarkup.UserInterface.Model.User;
using Newtonsoft.Json;
using DependencyResolver = LessMarkup.Interfaces.DependencyResolver;

namespace LessMarkup.UserInterface.Model.Structure
{
    public class JsonEntryPointModel
    {
        private readonly IDataCache _dataCache;
        private readonly ICurrentUser _currentUser;

        public JsonEntryPointModel(IDataCache dataCache, ICurrentUser currentUser)
        {
            _dataCache = dataCache;
            _currentUser = currentUser;
        }

        public static bool AppliesToRequest(HttpRequestBase request, string path)
        {
            return request.HttpMethod == "POST" && request.ContentType.StartsWith("application/json;");
        }

        public ActionResult HandleRequest(System.Web.Mvc.Controller controller, string path)
        {
            HttpContext.Current.Request.InputStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(HttpContext.Current.Request.InputStream, HttpContext.Current.Request.ContentEncoding))
            {
                var requestText = reader.ReadToEnd();
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestText).ToDictionary(k => k.Key, v => v.Value);
                object result;
                try
                {
                    var resultData = HandleDataRequest(data, controller);

                    result = new
                    {
                        Success = true,
                        Data = resultData,
                        UserLoggedIn = _currentUser.UserId.HasValue,
                        UserNotVerified = !_currentUser.IsValidated || !_currentUser.IsApproved
                    };
                }
                catch (Exception e)
                {
                    while (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }

                    result = new
                    {
                        Success = false,
                        e.Message,
                        UserLoggedIn = _currentUser.UserId.HasValue,
                        UserNotVerified = !_currentUser.IsValidated || !_currentUser.IsApproved
                    };
                }

                return new ContentResult
                {
                    ContentType = "application/json",
                    Content = JsonConvert.SerializeObject(result)
                };
            }
        }

        private object HandleDataRequest(Dictionary<string, object> data, System.Web.Mvc.Controller controller)
        {
            var command = data["-command-"].ToString();

            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            switch (command)
            {
                case "InputFormTemplate":
                {
                    var resourceCache = _dataCache.Get<ResourceCache>();
                    var templateBody = resourceCache.ReadText("~/Views/InputFormTemplate.cshtml");
                    return templateBody;
                }

                case "InputFormDefinition":
                {
                    var model = DependencyResolver.Resolve<InputFormDefinitionModel>();
                    model.Initialize(data["-id-"].ToString());
                    return model;
                }

                case "View":
                {
                    var model = DependencyResolver.Resolve<LoadNodeViewModel>();
                    if (!model.Initialize(data["-path-"].ToString(), JsonConvert.DeserializeObject<List<string>>(data["-cached-"].ToString()), controller, true))
                    {
                        throw new ObjectNotFoundException("Unknown path");
                    }
                    return model;
                }

                case "Action":
                {
                    var model = DependencyResolver.Resolve<ExecuteActionModel>();
                    return model.HandleRequest(data, controller);
                }

                case "LoginStage1":
                {
                    var model = DependencyResolver.Resolve<LoginModel>();
                    return model.HandleStage1Request(data);
                }

                case "LoginStage2":
                {
                    var model = DependencyResolver.Resolve<LoginModel>();
                    return model.HandleStage2Request(data);
                }

                case "Logout":
                {
                    var model = DependencyResolver.Resolve<LoginModel>();
                    return model.HandleLogout();
                }

                case "Typeahead":
                {
                    var model = DependencyResolver.Resolve<TypeaheadModel>();
                    model.Initialize(data["-path-"].ToString(), data["property"].ToString(), data["searchText"].ToString());
                    return model;
                }

                case "GetRegisterObject":
                {
                    var registerModel = DependencyResolver.Resolve<RegisterModel>();
                    return registerModel.GetRegisterObject();
                }

                case "Register":
                {
                    var registerModel = (RegisterModel) JsonHelper.ResolveAndDeserializeObject(data["user"].ToString(), typeof (RegisterModel));
                    return registerModel.Register(controller, data["user"].ToString());
                }

                case "GetNotifications":
                {
                    var notificationsModel = DependencyResolver.Resolve<NotificationsModel>();
                    return notificationsModel.Handle(data["notifications"].ToString(), controller);
                }

                default:
                    throw new UnknownActionException();
            }
        }
    }
}
