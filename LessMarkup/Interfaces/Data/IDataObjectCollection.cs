﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.ComponentModel;
using System.Data.Entity;
using System.Linq;

namespace LessMarkup.Interfaces.Data
{
    public interface IDataObjectCollection<T> : IDbSet<T>, IDataObject, IListSource, IOrderedQueryable<T> where T : class
    {
        DbSet<T> InnerCollection { get; }
    }
}
