﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Metadata.ModelConventions
{
    public class KeyConvention : IKeyConvention
    {
        public virtual InternalKeyBuilder Apply(InternalKeyBuilder keyBuilder)
        {
            Check.NotNull(keyBuilder, "keyBuilder");

            foreach (var property in keyBuilder.Metadata.Properties)
            {
                var entityBuilder = keyBuilder.ModelBuilder.Entity(property.EntityType.Name, ConfigurationSource.Convention);
                ConfigureKeyProperty(entityBuilder.Property(property.PropertyType, property.Name, ConfigurationSource.Convention));
            }
            return keyBuilder;
        }

        protected virtual void ConfigureKeyProperty([NotNull] InternalPropertyBuilder propertyBuilder)
        {
            Check.NotNull(propertyBuilder, "propertyBuilder");

            propertyBuilder.GenerateValueOnAdd(true, ConfigurationSource.Convention);

            // TODO: Nullable, Sequence
            // Issue #213
        }
    }
}
