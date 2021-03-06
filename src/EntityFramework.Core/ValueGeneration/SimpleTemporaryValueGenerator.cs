// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.ValueGeneration
{
    public abstract class SimpleTemporaryValueGenerator<TValue> : SimpleValueGenerator<TValue>
    {
        public override bool GeneratesTemporaryValues => true;
    }
}
