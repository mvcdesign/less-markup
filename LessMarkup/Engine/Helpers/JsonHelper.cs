﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces;
using Newtonsoft.Json;

namespace LessMarkup.Engine.Helpers
{
    public static class JsonHelper
    {
        public static T Get<T>(this Dictionary<string, string> jsonObject, string name)
        {
            string str;
            if (!jsonObject.TryGetValue(name, out str))
            {
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(str);
        }

        public static string Get(this Dictionary<string, string> jsonObject, string name)
        {
            string str;
            if (!jsonObject.TryGetValue(name, out str))
            {
                return null;
            }
            return str;
        }

        public static object ResolveAndDeserializeObject(string text, Type type)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (type == typeof(string))
            {
                return text;
            }

            if (type == typeof(bool))
            {
                return bool.Parse(text);
            }

            if (!type.IsClass || type == typeof(DateTime) || type == typeof(object) || typeof(IEnumerable).IsAssignableFrom(type))
            {
                return JsonConvert.DeserializeObject(text, type);
            }

            var value = DependencyResolver.TryResolve(type);

            if (value == null)
            {
                return Activator.CreateInstance(type);
            }

            var valueData = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object propertyValue;
                if (!valueData.TryGetValue(property.Name.ToJsonCase(), out propertyValue) || propertyValue == null)
                {
                    continue;
                }

                if (property.PropertyType == propertyValue.GetType())
                {
                    property.SetValue(value, propertyValue);
                }
                else
                {
                    var serializedValue = JsonConvert.SerializeObject(propertyValue);
                    property.SetValue(value, JsonConvert.DeserializeObject(serializedValue, property.PropertyType));
                }
            }

            return value;
        }
    }
}
