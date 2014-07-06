﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using LessMarkup.Interfaces.Structure;

namespace LessMarkup.UserInterface.NodeHandlers
{
    public class DefaultRootNodeHandler : AbstractNodeHandler
    {
        public override object GetViewData(long objectId, object settings, object controller)
        {
            return null;
        }

        public override bool IsStatic
        {
            get { return true; }
        }
    }
}