﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.FunctionalTests;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class SqlServerFixture
    {
        public readonly IServiceProvider ServiceProvider;

        public SqlServerFixture()
        {
            ServiceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer()
                .ServiceCollection()
                .BuildServiceProvider();
        }
    }
}
