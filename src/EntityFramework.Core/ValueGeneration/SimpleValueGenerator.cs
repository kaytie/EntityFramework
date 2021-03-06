// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Storage;

namespace Microsoft.Data.Entity.ValueGeneration
{
    public abstract class SimpleValueGenerator<TValue> : ValueGenerator<TValue>
    {
        public override TValue Next(DbContextService<DataStoreServices> dataStoreServices) => Next();

        public abstract TValue Next();

        public override bool GeneratesTemporaryValues => false;
    }
}
