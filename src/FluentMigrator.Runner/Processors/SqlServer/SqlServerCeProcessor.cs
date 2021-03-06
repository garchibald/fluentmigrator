#region License
// 
// Copyright (c) 2007-2009, Sean Chambers <schambers80@gmail.com>
// Copyright (c) 2010, Nathan Brown
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using FluentMigrator.Builders.Execute;

namespace FluentMigrator.Runner.Processors.SqlServer
{
   public class SqlServerCeProcessor : ProcessorBase, IDisposable
    {
        public virtual SqlCeConnection Connection { get; set; }
        public SqlCeTransaction Transaction { get; private set; }
        public bool WasCommitted { get; private set; }

        public SqlServerCeProcessor(SqlCeConnection connection, IMigrationGenerator generator, IAnnouncer announcer, IMigrationProcessorOptions options)
            : base(generator, announcer, options)
        {
            Connection = connection;
            connection.Open();
            BeginTransaction();
        }

        public override bool SchemaExists(string schemaName)
        {
            if (string.IsNullOrEmpty(schemaName) || schemaName.ToLower() == "dbo")
            {
              return true;
            }
            throw new NotSupportedException("Schemas not supported by SQL Compact");
        }

        public override bool TableExists(string schemaName, string tableName)
        {
            return Exists("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{0}'", FormatSqlEscape(tableName));
        }

        public override bool ColumnExists(string schemaName, string tableName, string columnName)
        {
            return Exists("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{0}' AND COLUMN_NAME = '{1}'", FormatSqlEscape(tableName), FormatSqlEscape(columnName));
        }

        public override bool ConstraintExists(string schemaName, string tableName, string constraintName)
        {
           // See more information http://arcanecode.com/2007/04/19/system-views-in-sql-server-compact-edition-constraints/
            return Exists("SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_NAME = '{0}' AND CONSTRAINT_NAME = '{1}'", FormatSqlEscape(tableName), FormatSqlEscape(constraintName));
        }

        public override bool IndexExists(string schemaName, string tableName, string indexName)
        {
           return Exists("SELECT NULL FROM INFORMATION_SCHEMA.INDEXES WHERE index_name = '{0}'", FormatSqlEscape(indexName));
        }

        public override void Execute(string template, params object[] args)
        {
            Process(String.Format(template, args));
        }

        public override bool Exists(string template, params object[] args)
        {
            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            using (var command = new SqlCeCommand(String.Format(template, args), Connection, Transaction))
            using (var reader = command.ExecuteReader())
            {
                return reader.Read();
            }
        }

        public override DataSet ReadTableData(string schemaName, string tableName)
        {
            return Read("SELECT * FROM {0}", tableName);
        }

        public DataTable GetTableSchema(string schemaName, string tableName)
        {
           try
           {
              if (Connection.State != ConnectionState.Open)
                 Connection.Open();

              using (var command = new SqlCeCommand(String.Format("SELECT * FROM {0} WHERE 1 = 2", tableName), Connection, Transaction))
              {
                 var rdr = command.ExecuteReader(CommandBehavior.Default);
                 // Get the schema table.
                 return rdr.GetSchemaTable();
              }
           }
           catch (Exception)
           {
              return null;
           }
        }


        public override DataSet Read(string template, params object[] args)
        {
            if (Connection.State != ConnectionState.Open) Connection.Open();

            var ds = new DataSet();
            using (var command = new SqlCeCommand(String.Format(template, args), Connection, Transaction))
            using (var adapter = new SqlCeDataAdapter(command))
            {
                adapter.Fill(ds);
                return ds;
            }
        }

        public override void BeginTransaction()
        {
            Announcer.Say("Beginning Transaction");
            Transaction = Connection.BeginTransaction();
        }

        public override void CommitTransaction()
        {
            Announcer.Say("Committing Transaction");
            
            if (Transaction != null)
            {
                Transaction.Commit();
                Transaction = null;    
            }

            WasCommitted = true;

            if (Connection.State != ConnectionState.Closed)
            {
                Connection.Close();
            }
        }

        public override void RollbackTransaction()
        {
            if (Transaction == null)
            {
                Announcer.Say("No transaction was available to rollback!");
                return;
            }

            Announcer.Say("Rolling back transaction");
            
            Transaction.Rollback();

            WasCommitted = true;
            if (Connection.State != ConnectionState.Closed)
            {
                Connection.Close();
            }
        }

        protected override void Process(string sql)
        {
            Announcer.Sql(sql);

            if (Options.PreviewOnly || string.IsNullOrEmpty(sql))
                return;

            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            if (Transaction == null)
                BeginTransaction();

            using (var command = new SqlCeCommand(sql, Connection, Transaction))
            {
                try
                {
                    command.CommandTimeout = 0; // SQL Server CE does not support non-zero command timeout values!! :/
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    using (var message = new StringWriter())
                    {
                        message.WriteLine("An error occurred executing the following sql:");
                        message.WriteLine(sql);
                        message.WriteLine("The error was {0}", ex.Message);

                        throw new Exception(message.ToString(), ex);
                    }
                }
            }
        }

        public override void Process(PerformDBOperationExpression expression)
        {
            if (Connection.State != ConnectionState.Open) Connection.Open();

            if (expression.Operation != null)
                expression.Operation(Connection, Transaction);
        }

        public override void Process(Expressions.InsertDataExpression expression)
        {
           // Always insert rows separately as SQL CE does not support combining multiple insert statements
           expression.InsertRowsSeparately = true;
           base.Process(expression);
        }

      protected string FormatSqlEscape(string sql)
        {
           return !string.IsNullOrEmpty(sql) ? sql.Replace("'", "''") : string.Empty;
        }

      ~SqlServerCeProcessor() {
         Dispose(false);
      }

      public void Dispose()
      {
         Dispose(true);
      }

      private void Dispose(bool diposing)
      {
         if ( diposing)
         {
            GC.SuppressFinalize(this);
         }

         if (Connection == null) return;

         Connection.Dispose();
         Connection = null;
      }
    }
}