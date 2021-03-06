﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace LessMarkup.Interfaces.System
{
    public interface ISpecialFolder
    {
        string ApplicationData { get; }

        string GeneratedAssemblies { get; }

        string BinaryFiles { get; }

        string RootFolder { get; }

        string GeneratedDataAssembly { get; }

        string GeneratedDataAssemblyNew { get; }

        string GeneratedViewAssembly { get; }

        string GeneratedViewAssemblyNew { get; }

        string LogFolder { get; }
    }
}
