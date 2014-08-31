﻿using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Module;

namespace LessMarkup.DataObjects.Security
{
    public class UserPropertyDefinition : SiteDataObject
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public UserPropertyType Type { get; set; }
        public bool Required { get; set; }
    }
}