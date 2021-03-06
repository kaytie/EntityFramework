﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;

namespace Microsoft.Data.Entity.SqlServer.Design.ReverseEngineering.Model
{
    public class ForeignKeyColumnMapping
    {
        public static readonly string Query =
@"SELECT
    quotename(SCHEMA_NAME(fk.schema_id)) + quotename(fk.name) + quotename(SCHEMA_NAME(fromSchema.schema_id)) + quotename(OBJECT_NAME(fk.parent_object_id)) + quotename(fromCol.name) [Id]
  , quotename(SCHEMA_NAME(fk.schema_id)) + quotename(fk.name) [ConstraintId]
  , quotename(SCHEMA_NAME(fromSchema.schema_id)) + quotename(OBJECT_NAME(fk.parent_object_id)) + quotename(fromCol.name) [FromColumnId]
  , quotename(SCHEMA_NAME(toSchema.schema_id)) + quotename(OBJECT_NAME(fk.referenced_object_id)) + quotename(toCol.name) [ToColumnId]
  FROM
  sys.foreign_keys fk
  INNER JOIN
  sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
  INNER JOIN
  sys.columns toCol ON fkc.referenced_column_id = toCol.column_id AND fkc.referenced_object_id = toCol.object_id /* PRIMARY KEY COLS*/
  INNER JOIN
  sys.columns fromCol ON fkc.parent_column_id = fromCol.column_id AND fkc.parent_object_id = fromCol.object_id /* FOREIGN KEY COLS*/
  INNER JOIN
  sys.objects toSchema ON toSchema.object_id = fk.referenced_object_id
  INNER JOIN
  sys.objects fromSchema ON fromSchema.object_id = fk.parent_object_id
";
        public string Id { get; set; }
        public string ConstraintId { get; set; }
        public string FromColumnId { get; set; }
        public string ToColumnId { get; set; }

        public static ForeignKeyColumnMapping CreateFromReader(SqlDataReader reader)
        {
            var tableColumn = new ForeignKeyColumnMapping();
            tableColumn.Id = reader.IsDBNull(0) ? null : reader.GetString(0);
            tableColumn.ConstraintId = reader.IsDBNull(1) ? null : reader.GetString(1);
            tableColumn.FromColumnId = reader.IsDBNull(2) ? null : reader.GetString(2);
            tableColumn.ToColumnId = reader.IsDBNull(3) ? null : reader.GetString(3);

            return tableColumn;
        }

        public override string ToString()
        {
            return "FKCM[Id=" + Id
                + ", ConstraintId=" + ConstraintId
                + ", FromColumnId=" + FromColumnId
                + ", ToColumnId=" + ToColumnId
                + "]";
        }
    }
}