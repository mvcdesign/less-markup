﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using LessMarkup.DataFramework;
using LessMarkup.Engine.Logging;
using LessMarkup.Engine.Site;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Module;
using LessMarkup.Interfaces.System;
using DependencyResolver = LessMarkup.Interfaces.DependencyResolver;

namespace LessMarkup.Engine.Build
{
    class ControllerFactory : DefaultControllerFactory, Interfaces.System.IControllerFactory
    {
        private readonly IModuleProvider _moduleProvider;
        private readonly IDataCache _dataCache;

        public ControllerFactory(IModuleProvider moduleProvider, IDataCache dataCache)
        {
            _moduleProvider = moduleProvider;
            _dataCache = dataCache;
        }

        public ControllerFactory(IControllerActivator controllerActivator) : base(controllerActivator)
        {
        }

        public void Initialize()
        {
            ControllerBuilder.Current.SetControllerFactory(this);
        }

        public override IController CreateController(RequestContext requestContext, string controllerName)
        {
            string originalControllerName = controllerName;

            if (requestContext == null)
            {
                throw new ArgumentNullException("requestContext");
            }

            if (string.IsNullOrEmpty(controllerName))
            {
                this.LogDebug("Cannot handle null or empty controllerName");
                throw new ArgumentException(@"ArgumentNullOrEmpty", "controllerName");
            }

            if (controllerName == "favicon.ico")
            {
                controllerName = "Image";
            }

            var controllerType = GetControllerType(requestContext, controllerName);

            if (controllerType == null)
            {
                this.LogDebug("Cannot determine controller type for given controller name '" + controllerName + "'");
                throw new HttpException(404, string.Format(CultureInfo.CurrentCulture, "NoControllerFound", new object[] { requestContext.HttpContext.Request.Path }));
            }

            if (LoggingHelper.Level == LogLevel.Debug)
            {
                this.LogDebug("Controller name '" + originalControllerName + "' mapped to controller type '" + controllerType.FullName + "'");
            }

            var ret = GetControllerInstance(requestContext, controllerType);

            return ret;
        }

        protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            var moduleType = Constants.ModuleType.None;

            if (controllerType != null)
            {
                moduleType = _moduleProvider.GetControllerModuleType(controllerType);
            }

            if (moduleType == Constants.ModuleType.None)
            {
                this.LogDebug("Cannot handle null controller type");
                throw new HttpException(404, string.Format(CultureInfo.CurrentCulture, "NoControllerFound", new object[] { requestContext.HttpContext.Request.Path }));
            }

            if (!typeof (Controller).IsAssignableFrom(controllerType))
            {
                this.LogDebug("Cannot handle controller type which is not derived from AbstractController class");
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "NotSubclassOfIController", new object[] { controllerType }), "controllerType");
            }

            var engineConfiguration = DependencyResolver.Resolve<IEngineConfiguration>();

            if ((!engineConfiguration.SafeMode || engineConfiguration.DisableSafeMode) && !string.IsNullOrWhiteSpace(engineConfiguration.Database))
            {
                var moduleCache = _dataCache.Get<ModuleCache>();

                if (!moduleCache.ModuleTypes.Contains(moduleType))
                {
                    throw new HttpException(404, string.Format(CultureInfo.CurrentCulture, "NoControllerFound", new object[] { requestContext.HttpContext.Request.Path }));
                }
            }

            var ret = (Controller) DependencyResolver.Resolve(controllerType);
            return ret;
        }

        protected override Type GetControllerType(RequestContext requestContext, string controllerName)
        {
            return _moduleProvider.GetControllerType(controllerName);
        }
    }
}
