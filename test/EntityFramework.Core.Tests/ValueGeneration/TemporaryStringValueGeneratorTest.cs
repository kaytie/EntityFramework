// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.ValueGeneration;
using Xunit;

namespace Microsoft.Data.Entity.Tests.ValueGeneration
{
    public class TemporaryStringValueGeneratorTest
    {
        [Fact]
        public void Creates_GUID_strings()
        {
            var generator = new TemporaryStringValueGenerator();

            var values = new HashSet<Guid>();
            for (var i = 0; i < 100; i++)
            {
                var generatedValue = generator.Next(new DbContextService<DataStoreServices>(() => null));

                values.Add(Guid.Parse(generatedValue));
            }

            Assert.Equal(100, values.Count);
        }

        [Fact]
        public void Generates_temp_values()
        {
            Assert.True(new TemporaryStringValueGenerator().GeneratesTemporaryValues);
        }
    }
}
