﻿#region License
// 
// Copyright (c) 2011, Grant Archibald
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
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentMigrator.Runner.Processors.Oracle;
using NUnit.Framework;

namespace FluentMigrator.Tests.Integration
{
   /// <summary>
   /// Provides a context from a which a unit test can create an empty Oracle user database for unit testing
   /// </summary>
   /// <remarks>For Oracle XE will need to run "ALTER SYSTEM SET PROCESSES=400 SCOPE=SPFILE;" and restart XE</remarks>
   public class OracleUnitTest
   {
      /// <summary>
      /// The identfier that will be used to open the oracle instance
      /// <remarks>
      /// This can be TNS-less connection or TNS name
      /// </remarks>
      /// </summary>
      private string Server;

      /// <summary>
      /// The connection string that is used to connect to Oracle and create the new user database
      /// </summary>
      private string MasterConnectionString;

      /// <summary>
      /// The connection string that the unit test can use within a test
      /// </summary>
      public string ConnectionString;

      /// <summary>
      /// The temporary test database that has been created for thr user test user
      /// </summary>
      protected internal string TestDbName = string.Empty;

      /// <summary>
      /// The temporary unit test user that has been created for the unit test 
      /// </summary>
      private string User = string.Empty;

      /// <summary>
      /// The password to connect to the <seealso cref="MasterConnectionString"/>
      /// </summary>
      private static string Password
      {
         get
         {
            var password = Environment.GetEnvironmentVariable("ORACLE_PASSWORD");
            return string.IsNullOrEmpty(password) ? "password" : password;
         }
      }

      [TestFixtureSetUp]
      public virtual void TestFixtureSetUp()
      {
         // Try get server name from environment first
         Server = Environment.GetEnvironmentVariable("ORACLE_SERVER");
         if ( String.IsNullOrEmpty(Server) )
         {
            // .... default server to be TNS-less connection to the local XE instance
            Server = "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=127.0.0.1)(PORT=1521))(CONNECT_DATA=(SID=XE)))";
         }


         MasterConnectionString = string.Format("user id=system;password={1};Data Source={0};", Server, Password);

         try
         {
            // Remove any test databases that exist from previous test runs
            // Done at test fixture setup time, as the databases cannot always be dropped after each test as they may be in use
            DropDatabases("FM_A");
         }
         catch (Exception)
         {
            Debug.WriteLine("Just making sure database doesn't exist error");
         }
      }

      [SetUp]
      public virtual void Setup()
      {
         TestDbName = GetUniqueDbName();
         User = TestDbName;

         
         ConnectionString = string.Format("user id={0};password={1};Data Source={2};Pooling=True", User, Password, Server);

         CreateDatabase(TestDbName.ToUpper());
      }

      private static string GetUniqueDbName()
      {
         //Oracle only allows 30 chars for databse name and a guid is 38 so we use this shortend form
         //Oracle requires name to start with a letter no longer a true short guid as we use substring addedtimestamp to ensure unique and remove illegal chars
         return string.Format(CultureInfo.InvariantCulture, "FM_A{0}{1}", Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 19).Replace("/", "_")
                                                                             .Replace("+", "_"), DateTime.Now.Minute.ToString(CultureInfo.CurrentCulture) + DateTime.Now.Second + DateTime.Now.Millisecond);
      }

      

      [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
      public void DropDatabases(string databaseName)
      {
         if ( string.IsNullOrEmpty(databaseName))
            throw new ArgumentException("Database name not specified");

         using (var con = OracleFactory.GetOpenConnection(MasterConnectionString))
         {
             using (var nameCom = con.CreateCommand())
            {
                nameCom.CommandText = string.Format("select UserName from DBA_USERS WHERE USERNAME Like '{0}%'",
                                                    databaseName);
               using (var reader = nameCom.ExecuteReader())
               {
                  while ( reader.Read())
                  {
                     var userName = reader["username"] as string;
                     //CloseDatabaseSessions(con, userName);
                     DropUser(con, userName);   
                  }
                  
               }
            }
         }
      }

      /// <summary>
      /// Drops a user schema
      /// </summary>
      /// <param name="con">The open connection to drop user from</param>
      /// <param name="userName">The user name to drop</param>
      private void DropUser(DbConnection con, string userName)
      {
         using (var com = con.CreateCommand())
         {
            com.CommandText = string.Format("DROP USER {0} CASCADE", userName);
            try
            {
               com.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
            }
         }
      }

      /// <summary>
      /// Creates a new user within the Oracle instance using the <seealso cref="MasterConnectionString"/>
      /// </summary>
      /// <param name="databaseName">The name of the database to be created</param>
      private void CreateDatabase(string databaseName)
      {
         Debug.WriteLine("Creating database " + databaseName);
         var query1 = @"CREATE USER " + databaseName + " IDENTIFIED BY " + Password;
         var query = @"GRANT CONNECT, RESOURCE, CREATE VIEW TO " + databaseName;

         using (var con = OracleFactory.GetOpenConnection(MasterConnectionString))
         {
            using (var com = con.CreateCommand())
            {
                com.CommandText = query1;
               com.ExecuteNonQuery();
               com.CommandText = query;
               com.ExecuteNonQuery();
            }
         }
      }

   }
}