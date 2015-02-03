﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational.Design.CodeGeneration;
using Microsoft.Data.Entity.Relational.Design.ReverseEngineering;
using Microsoft.Data.Entity.SqlServer.ReverseEngineering.Model;
using Microsoft.Data.Entity.SqlServer.Utilities;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.SqlServer.ReverseEngineering
{
    public class SqlServerMetadataModelProvider : IDatabaseMetadataModelProvider
    {
        public static readonly string AnnotationPrefix = "SqlServerMetadataModelProvider:";
        public static readonly string AnnotationNameTableId = AnnotationPrefix + "TableId";
        public static readonly string AnnotationNameTableIdSchemaTableSeparator = ".";
        public static readonly string AnnotationNameColumnId = AnnotationPrefix + "ColumnId";
        public static readonly string AnnotationNameColumnName = AnnotationPrefix + "ColumnName";
        public static readonly string AnnotationNamePrimaryKeyOrdinal = AnnotationPrefix + "PrimaryKeyOrdinalPosition";
        public static readonly string AnnotationNameForeignKeyConstraints = AnnotationPrefix + "ForeignKeyConstraints";
        public static readonly string AnnotationFormatForeignKey = AnnotationPrefix + "ForeignKey[{0}]{1}"; // {O} = ConstraintId, {1} = Descriptor
        public static readonly string AnnotationFormatForeignKeyConstraintSeparator = ",";
        public static readonly string AnnotationDescriptorForeignKeyOrdinal = "Ordinal";
        public static readonly string AnnotationDescriptorForeignKeyTargetEntityType = "TargetEntityType";
        public static readonly string AnnotationDescriptorForeignKeyTargetProperty = "TargetProperty";
        public static readonly string AnnotationNamePrecision = AnnotationPrefix + "Precision";
        public static readonly string AnnotationNameMaxLength = AnnotationPrefix + "MaxLength";
        public static readonly string AnnotationNameScale = AnnotationPrefix + "Scale";
        public static readonly string AnnotationNameIsIdentity = AnnotationPrefix + "IsIdentity";
        public static readonly string AnnotationNameIsNullable = AnnotationPrefix + "IsNullable";

        private Dictionary<IEntityType, string> _entityTypeToClassNameMap = new Dictionary<IEntityType, string>();
        private Dictionary<IProperty, string> _propertyToPropertyNameMap = new Dictionary<IProperty, string>();

        private ILogger _logger;

        public SqlServerMetadataModelProvider([NotNull] IServiceProvider serviceProvider)
        {
            _logger = (ILogger)serviceProvider.GetService(typeof(ILogger));
        }

        public IModel GenerateMetadataModel(string connectionString, string filters)
        {
            Dictionary<string, Table> tables;
            Dictionary<string, TableColumn> tableColumns;
            Dictionary<string, TableConstraintColumn> tableConstraintColumns;
            Dictionary<string, ForeignKeyColumnMapping> foreignKeyColumnMappings;
            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    tables = LoadData<Table>(conn, Table.Query, Table.CreateFromReader, t => t.Id);
                    tableColumns = LoadData<TableColumn>(conn, TableColumn.Query, TableColumn.CreateFromReader, tc => tc.Id);
                    tableConstraintColumns = LoadData<TableConstraintColumn>(
                        conn, TableConstraintColumn.Query, TableConstraintColumn.CreateFromReader, tcc => tcc.Id);
                    foreignKeyColumnMappings = LoadData<ForeignKeyColumnMapping>(
                        conn, ForeignKeyColumnMapping.Query, ForeignKeyColumnMapping.CreateFromReader, fkcm => fkcm.Id);
                }
                finally
                {
                    if (conn != null)
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            try
                            {
                                conn.Close();
                            }
                            catch (SqlException)
                            {
                                // do nothing if attempt to close connection fails
                            }
                        }
                    }
                }
            }

            //_logger.WriteInformation("Tables");
            //foreach (var t in tables)
            //{
            //    var table = t.Value;
            //    _logger.WriteInformation(table.ToString());
            //}

            //_logger.WriteInformation(Environment.NewLine + "Columns");
            //foreach (var tc in tableColumns)
            //{
            //    _logger.WriteInformation(tc.Value.ToString());
            //}

            //_logger.WriteInformation(Environment.NewLine + "Constraint Columns");
            //foreach (var tc in tableConstraintColumns)
            //{
            //    _logger.WriteInformation(tc.Value.ToString());
            //}

            //_logger.WriteInformation(Environment.NewLine + "Foreign Key Column Mappings");
            //foreach (var fkcm in foreignKeyColumnMappings)
            //{
            //    _logger.WriteInformation(fkcm.Value.ToString());
            //}

            Dictionary<string, int> primaryKeyOrdinals;
            Dictionary<string, Dictionary<string, int>> foreignKeyOrdinals;
            CreatePrimaryAndForeignKeyMaps(
                tableConstraintColumns, out primaryKeyOrdinals, out foreignKeyOrdinals);

            return CreateModel(tables, tableColumns,
                primaryKeyOrdinals, foreignKeyOrdinals, foreignKeyColumnMappings);
        }

        public static Dictionary<string, T> LoadData<T>(
            SqlConnection conn, string query,
            Func<SqlDataReader, T> createFromReader, Func<T, string> identifier)
        {
            var data = new Dictionary<string, T>();
            var sqlCommand = new SqlCommand(query);
            sqlCommand.Connection = conn;

            using (var reader = sqlCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var item = createFromReader(reader);
                    data.Add(identifier(item), item);
                }
            }

            return data;
        }

        /// <summary>
        /// Output two Dictionaries
        /// 
        /// The first one maps (for all primary keys)
        ///   ColumnId -> Ordinal at which that column appears in the primary key for the table in which it is defined
        /// 
        /// The second one maps (for all foreign keys)
        ///   ColumnId -> a Dictionary which maps ConstraintId -> Ordinal at which that Column appears within that FK constraint
        /// 
        /// </summary>
        /// <returns></returns>
        public void CreatePrimaryAndForeignKeyMaps(
            Dictionary<string, TableConstraintColumn> tableConstraintColumns,
            out Dictionary<string, int> primaryKeyOrdinals,
            out Dictionary<string, Dictionary<string, int>> foreignKeyOrdinals)
        {
            primaryKeyOrdinals = new Dictionary<string, int>();
            foreignKeyOrdinals = new Dictionary<string, Dictionary<string, int>>();

            foreach (var tableConstraintColumn in tableConstraintColumns.Values)
            {
                if (tableConstraintColumn.ConstraintType == "PRIMARY KEY")
                {
                    primaryKeyOrdinals.Add(tableConstraintColumn.ColumnId, tableConstraintColumn.Ordinal);
                }
                else if (tableConstraintColumn.ConstraintType == "FOREIGN KEY")
                {
                    Dictionary<string, int> constraintNameToOrdinalMap;
                    if (!foreignKeyOrdinals.TryGetValue(tableConstraintColumn.ColumnId, out constraintNameToOrdinalMap))
                    {
                        constraintNameToOrdinalMap = new Dictionary<string, int>();
                        foreignKeyOrdinals[tableConstraintColumn.ColumnId] = constraintNameToOrdinalMap;
                    }

                    constraintNameToOrdinalMap[tableConstraintColumn.ConstraintId] = tableConstraintColumn.Ordinal;
                }
                else
                {
                    _logger.WriteInformation("Unknown Constraint Type for " + tableConstraintColumn);
                }
            }
        }

        public IModel CreateModel(
            Dictionary<string, Table> tables,
            Dictionary<string, TableColumn> tableColumns,
            Dictionary<string, int> primaryKeyOrdinals,
            Dictionary<string, Dictionary<string, int>> foreignKeyOrdinals,
            Dictionary<string, ForeignKeyColumnMapping> foreignKeyColumnMappings)
        {
            // the relationalModel is an IModel, but not the one that will be returned
            // it's just directly from the database - EntiyType = table, Property = column
            // etc with no attempt to hook up foreign key columns or make the
            // names fit CSharp conventions etc.
            var relationalModel = new Microsoft.Data.Entity.Metadata.Model();
            foreach (var table in tables.Values)
            {
                relationalModel.AddEntityType(table.Id);
            }

            var relationalColumnIdToRelationalPropertyMap = new Dictionary<string, Property>();
            foreach (var tc in tableColumns.Values)
            {
                var entityType = relationalModel.TryGetEntityType(tc.TableId);
                if (entityType == null)
                {
                    _logger.WriteError("Could not find table with TableId " + tc.TableId);
                    continue;
                }

                // IModel will not allow Properties to be created without a Type, so map to CLR type here.
                // This means if we come across a column with a SQL Server type which we can't map we will ignore it.
                // Note: foreign key properties appear just like any other property in the relational model.
                Type clrPropertyType;
                if (!SqlServerTypeMapping._sqlTypeToClrTypeMap.TryGetValue(tc.DataType, out clrPropertyType))
                {
                    // _logger.WriteError("Could not find type mapping for SQL Server type " + tc.DataType);
                    continue;
                }

                if (tc.IsNullable)
                {
                    clrPropertyType = clrPropertyType.MakeNullable();
                }

                var relationalProperty = entityType.AddProperty(tc.Id, clrPropertyType, shadowProperty: true);
                relationalColumnIdToRelationalPropertyMap[tc.Id] = relationalProperty;
            }

            // construct maps mapping of relationalModel's IEntityTypes to the names they will have in the CodeGen Model
            var nameMapper = new SqlServerNameMapper(relationalModel, entity => entity.SimpleName, property => property.Name);

            // create all codeGenModel EntityTypes and Properties
            var relationalEntityTypeToCodeGenEntityTypeMap = new Dictionary<EntityType, EntityType>();
            var relationalPropertyToCodeGenPropertyMap = new Dictionary<Property, Property>();
            var relationalEntityTypeToForeignKeyConstraintsMap = new Dictionary<EntityType, Dictionary<string, List<Property>>>(); // string is ConstraintId
            var codeGenModel = new Microsoft.Data.Entity.Metadata.Model();
            foreach (var relationalEntityType in relationalModel.EntityTypes.Cast<EntityType>())
            {
                var codeGenEntityType = codeGenModel.AddEntityType(nameMapper.EntityTypeToClassNameMap[relationalEntityType]);
                relationalEntityTypeToCodeGenEntityTypeMap[relationalEntityType] = codeGenEntityType;

                // Loop over properties in each relational EntityType.
                // If property is part of a foreign key (and not part of a primary key)
                // add to list of constraints to be added in later.
                // Otherwise construct matching property.
                var primaryKeyProperties = new List<Property>();
                var constraints = new Dictionary<string, List<Property>>();
                relationalEntityTypeToForeignKeyConstraintsMap[relationalEntityType] = constraints;
                foreach (var relationalProperty in relationalEntityType.Properties.OrderBy(p => nameMapper.PropertyToPropertyNameMap[p]))
                {
                    var isPartOfPrimaryKey = false;
                    int primaryKeyOrdinal;
                    if ((isPartOfPrimaryKey =
                        primaryKeyOrdinals.TryGetValue(relationalProperty.Name, out primaryKeyOrdinal)))
                    {
                        // add _relational_ property so we can order on the ordinal later
                        primaryKeyProperties.Add(relationalProperty);
                    }

                    var isPartOfForeignKey = false;
                    Dictionary<string, int> foreignKeyConstraintIdOrdinalMap;
                    if ((isPartOfForeignKey =
                        foreignKeyOrdinals.TryGetValue(relationalProperty.Name, out foreignKeyConstraintIdOrdinalMap)))
                    {
                        // relationalProperty represents (part of) a foreign key 
                        foreach (var constraintId in foreignKeyOrdinals.Keys)
                        {
                            List<Property> constraintProperties;
                            if (!constraints.TryGetValue(constraintId, out constraintProperties))
                            {
                                constraintProperties = new List<Property>();
                                constraints.Add(constraintId, constraintProperties);
                            }
                            constraintProperties.Add(relationalProperty);
                        }
                    }

                    if (!isPartOfForeignKey || isPartOfPrimaryKey)
                    {
                        var codeGenProperty = codeGenEntityType.AddProperty(
                            nameMapper.PropertyToPropertyNameMap[relationalProperty],
                            relationalProperty.PropertyType,
                            shadowProperty: true);
                        relationalPropertyToCodeGenPropertyMap[relationalProperty] = codeGenProperty;
                    }
                } // end of loop over all relational properties for given EntityType

                if (primaryKeyProperties.Count() > 0)
                {
                    // order the relational properties by their primaryKeyOrdinal, then return a list
                    // of the codeGen properties mapped to each relational property in that order
                    codeGenEntityType.SetPrimaryKey(
                        primaryKeyProperties
                        .OrderBy(p => primaryKeyOrdinals[p.Name]) // note: for relational property p.Name is its columnId
                        .Select(p => relationalPropertyToCodeGenPropertyMap[p])
                        .ToList());
                }
            } // end of loop over all relational EntityTypes

            // Loop over all FK constraints adding in ForeignKeys
            foreach(var keyValuePair in relationalEntityTypeToForeignKeyConstraintsMap)
            {
                var fromColumnrelationalEntityType = keyValuePair.Key;
                var codeGenEntityType = relationalEntityTypeToCodeGenEntityTypeMap[fromColumnrelationalEntityType];
                foreach (var foreignKeyConstraintMap in keyValuePair.Value)
                {
                    var foreignKeyConstraintId = foreignKeyConstraintMap.Key;
                    var foreignKeyConstraintRelationalPropertyList = foreignKeyConstraintMap.Value;

                    var targetRelationalProperty = FindTargetColumn(
                        foreignKeyColumnMappings,
                        tableColumns,
                        relationalColumnIdToRelationalPropertyMap,
                        foreignKeyConstraintId,
                        foreignKeyConstraintRelationalPropertyList[0].Name);
                    if (targetRelationalProperty != null)
                    {
                        var targetRelationalEntityType = targetRelationalProperty.EntityType;
                        var targetCodeGenEntityType = relationalEntityTypeToCodeGenEntityTypeMap[targetRelationalEntityType];
                        var targetPrimaryKey = targetCodeGenEntityType.GetPrimaryKey();

                        // to construct foreign key need the properties representing the foreign key columns in the codeGen model
                        // in the order they appear in the target's key
                        var foreignKeyCodeGenProperties =
                            foreignKeyConstraintRelationalPropertyList
                                .OrderBy(p => foreignKeyOrdinals[p.Name][foreignKeyConstraintId]) // relational property's name is the columnId
                                .Select(p => relationalPropertyToCodeGenPropertyMap[p])
                                .ToList();

                        var codeGenForeignKey = codeGenEntityType.AddForeignKey(foreignKeyCodeGenProperties, targetPrimaryKey);
                        //TODO: make ForeignKey unique based on constraints

                        // try without navigation property - add in CodeGen
                        //////TODO: what if multiple Navs to same target?
                        ////codeGenEntityType.AddNavigation(targetCodeGenEntityType.Name, codeGenForeignKey, pointsToPrincipal: false);
                    }
                }

            }

            return codeGenModel;
        }

        private Property FindTargetColumn(
            Dictionary<string, ForeignKeyColumnMapping> foreignKeyColumnMappings,
            Dictionary<string, TableColumn> tableColumns,
            Dictionary<string, Property> relationalColumnIdToRelationalPropertyMap,
            string foreignKeyConstraintId,
            string fromColumnId)
        {
            ForeignKeyColumnMapping foreignKeyColumnMapping;
            if (!foreignKeyColumnMappings.TryGetValue(
                foreignKeyConstraintId + fromColumnId, out foreignKeyColumnMapping))
            {
                //_logger.WriteError("Could not find foreignKeyMapping for ConstraintId " + foreignKeyConstraintId
                //    + " FromColumn " + fromColumnId);
                return null;
            }

            TableColumn toColumn;
            if (!tableColumns.TryGetValue(foreignKeyColumnMapping.ToColumnId, out toColumn))
            {
                //_logger.WriteError("Could not find toColumn with ColumnId " + foreignKeyColumnMapping.ToColumnId);
                return null;
            }

            Property toColumnRelationalProperty;
            if (!relationalColumnIdToRelationalPropertyMap.TryGetValue(toColumn.Id, out toColumnRelationalProperty))
            {
                //_logger.WriteError("Could not find relational property for toColumn with ColumnId " + toColumn.Id);
                return null;
            }

            return toColumnRelationalProperty;
        }

        //TODO - this works around the fact that string.Split() does not exist in ASPNETCORE50
        public static string[] SplitString(char[] delimiters, string input)
        {
            var output = new List<string>();

            var workingString = input;
            int firstIndex = -1;
            do
            {
                firstIndex = workingString.IndexOfAny(delimiters);
                if (firstIndex < 0)
                {
                    output.Add(workingString);
                }
                else
                {
                    output.Add(workingString.Substring(0, firstIndex));
                }
                workingString = workingString.Substring(firstIndex + 1);
            }
            while (firstIndex >= 0 && !string.IsNullOrEmpty(workingString));

            return output.ToArray();
        }

        public static string GetForeignKeyOrdinalPositionAnnotationName(string foreignKeyConstraintId)
        {
            return GetForeignKeyAnnotationName(AnnotationDescriptorForeignKeyOrdinal, foreignKeyConstraintId);
        }


        public static string GetForeignKeyTargetPropertyAnnotationName(string foreignKeyConstraintId)
        {
            return GetForeignKeyAnnotationName(AnnotationDescriptorForeignKeyTargetProperty, foreignKeyConstraintId);
        }

        public static string GetForeignKeyTargetEntityTypeAnnotationName(string foreignKeyConstraintId)
        {
            return GetForeignKeyAnnotationName(AnnotationDescriptorForeignKeyTargetEntityType, foreignKeyConstraintId);
        }

        public static string GetForeignKeyAnnotationName(string descriptor, string foreignKeyConstraintId)
        {
            return string.Format(AnnotationFormatForeignKey, foreignKeyConstraintId, descriptor);
        }

        public static void ApplyPropertyProperties(Property property, TableColumn tc)
        {
            property.IsNullable = tc.IsNullable;
            property.AddAnnotation(AnnotationNameIsNullable, tc.IsNullable.ToString());
            property.MaxLength = tc.MaxLength == -1 ? null : tc.MaxLength;
            if (property.MaxLength != null)
            {
                property.AddAnnotation(AnnotationNameMaxLength, property.MaxLength.Value.ToString());
            }
            if (tc.NumericPrecision.HasValue)
            {
                property.AddAnnotation(AnnotationNamePrecision, tc.NumericPrecision.Value.ToString());
            }
            if (tc.DateTimePrecision.HasValue)
            {
                property.AddAnnotation(AnnotationNamePrecision, tc.DateTimePrecision.Value.ToString());
            }
            if (tc.Scale.HasValue)
            {
                property.AddAnnotation(AnnotationNameScale, tc.Scale.Value.ToString());
            }
            if (tc.IsIdentity)
            {
                property.AddAnnotation(AnnotationNameIsIdentity, tc.IsIdentity.ToString());
            }
            property.IsStoreComputed = tc.IsStoreGenerated;
            if (tc.DefaultValue != null)
            {
                property.UseStoreDefault = true;
            }
        }

        public DbContextCodeGenerator GetContextModelCodeGenerator(ReverseEngineeringGenerator generator, DbContextGeneratorModel dbContextGeneratorModel)
        {
            return new SqlServerDbContextCodeGeneratorContext(
                generator
                , dbContextGeneratorModel.MetadataModel
                , dbContextGeneratorModel.Namespace
                , dbContextGeneratorModel.ClassName
                , dbContextGeneratorModel.ConnectionString);
        }
        public EntityTypeCodeGenerator GetEntityTypeModelCodeGenerator(
            ReverseEngineeringGenerator generator, EntityTypeGeneratorModel entityTypeGeneratorModel)
        {
            return new SqlServerEntityTypeCodeGeneratorContext(
                generator
                , entityTypeGeneratorModel.EntityType
                , entityTypeGeneratorModel.Namespace);
        }
    }
}