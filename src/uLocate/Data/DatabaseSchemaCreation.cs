﻿namespace uLocate.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using uLocate.Models;

    using Umbraco.Core;
    using Umbraco.Core.Logging;
    using Umbraco.Core.Persistence;

    /// <summary>
    /// The database schema creation.
    /// </summary>
    internal class DatabaseSchemaCreation
    {
        /// <summary>
        /// Collection of tables to be added to the database
        /// </summary>
        private static readonly Dictionary<int, Type> OrderedTables = new Dictionary<int, Type>
        {
            { 0, typeof(LocationTypeDto) },
            { 1, typeof(LocationDto) },
            { 2, typeof(LocationTypePropertyDto) },
            { 3, typeof(LocationPropertyDataDto) }

            //{ 4, typeof(AllowedDataTypesDto) }
        };

        ///// <summary>
        ///// Collection of additional constraints which should be deleted on un-install 
        ///// (generally ones which connect to tables which won't be deleted)
        ///// </summary>
        //private static readonly Dictionary<string, string> ConnectedConstraints = new Dictionary<string, string>
        //{
        //    // {"Constraint Name", "Table Name"}
        //    { "FK_uLocateLocationTypeProperty_cmsDataType", "cmsDataType" }, 
        //    { "FK_uLocateAllowedDataTypes_cmsDataType", "cmsDataType" }
        //};

        /// <summary>
        /// The database.
        /// </summary>
        private readonly Database _database;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseSchemaCreation"/> class.
        /// </summary>
        /// <param name="database">
        /// The database.
        /// </param>
        public DatabaseSchemaCreation(Database database)
        {
            _database = database;
        }


        /// <summary>
        /// Creates the database tables
        /// </summary>
        public void InitializeDatabaseSchema()
        {
            foreach (var item in OrderedTables.OrderBy(x => x.Key))
            {
                var TableType = item.Value;              
                var TableAttrib = (TableNameAttribute) Attribute.GetCustomAttribute(TableType, typeof (TableNameAttribute));
                string TableName = TableAttrib.Value;

                if (!_database.TableExist(TableName))
                {
                    //Create DB table - and set overwrite to false
                    _database.CreateTable(false, TableType);
                }


                var message = string.Concat("uLocate.Data.DatabaseSchemaCreation.InitializeDatabaseSchema - Created Table '", TableName, "'");
                LogHelper.Info(typeof(DatabaseSchemaCreation), message);
            }
        }

        /// <summary>
        /// Deletes the database tables.  (Used in package un-install)
        /// </summary>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool UninstallDatabaseSchema()
        {
            bool Result = true;

            // Delete Tables
            foreach (var item in OrderedTables.OrderByDescending(x => x.Key))
            {
                var tableNameAttribute = item.Value.FirstAttribute<TableNameAttribute>();

                string TableName = tableNameAttribute == null ? item.Value.Name : tableNameAttribute.Value;

                try
                {
                    if (_database.TableExist(TableName))
                    {
                        _database.DropTable(TableName);
                        var message = string.Concat("uLocate.Data.DatabaseSchemaCreation.UninstallDatabaseSchema - Deleted Table '", TableName, "'");
                        LogHelper.Info(typeof(DatabaseSchemaCreation), message);
                    }
                }
                catch (Exception ex)
                {
                    var message = string.Concat("uLocate.Data.DatabaseSchemaCreation.UninstallDatabaseSchema - Delete Tables Error: ", ex);
                    LogHelper.Error(typeof(DatabaseSchemaCreation), message, ex);
                    Result = false;
                }
            }

            // Delete Extra Constraints
            //foreach (var item in ConnectedConstraints)
            //{
            //    string ConstraintName = item.Key;
            //    string TableName = item.Value;
            //    string sqlTest = string.Format("SELECT count(object_id) AS Match FROM sys.objects WHERE type_desc LIKE '%CONSTRAINT' AND OBJECT_NAME(parent_object_id)='{0}' AND OBJECT_NAME(object_id)='{1}' ", TableName, ConstraintName);
            //    string sqlDrop = string.Format("ALTER TABLE {0} DROP CONSTRAINT {1};", TableName, ConstraintName);

            //    try
            //    {
            //        var Matching = _database.ExecuteScalar<int>(sqlTest);
            //        if (Matching > 0)
            //        {
            //            _database.Execute(sqlDrop);
            //            var message = string.Concat("uLocate.Data.DatabaseSchemaCreation.UninstallDatabaseSchema - Deleted Constraint '", ConstraintName, "'");
            //            LogHelper.Info(typeof(DatabaseSchemaCreation), message);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        var message = string.Concat("uLocate.Data.DatabaseSchemaCreation.UninstallDatabaseSchema - Delete Extra Constraints Error: ", ex);
            //        LogHelper.Error(typeof(DatabaseSchemaCreation), message, ex);
            //        Result = false;
            //    }
            //}

            return Result;
        }
    }
}