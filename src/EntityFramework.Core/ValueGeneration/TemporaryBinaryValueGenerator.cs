// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Data.Entity.ValueGeneration
{
    public class TemporaryBinaryValueGenerator : SimpleTemporaryValueGenerator<byte[]>
    {
        public override byte[] Next() => Guid.NewGuid().ToByteArray();
    }
}
