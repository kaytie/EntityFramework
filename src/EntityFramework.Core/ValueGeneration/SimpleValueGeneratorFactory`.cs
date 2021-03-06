// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.ValueGeneration
{
    public class SimpleValueGeneratorFactory<TValueGenerator> : SimpleValueGeneratorFactory
        where TValueGenerator : ValueGenerator, new()
    {
        public override ValueGenerator Create(IProperty property)
        {
            Check.NotNull(property, nameof(property));

            return new TValueGenerator();
        }
    }
}
