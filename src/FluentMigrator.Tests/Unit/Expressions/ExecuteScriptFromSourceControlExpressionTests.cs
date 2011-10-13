#region License
// 
// Copyright (c) 2007-2009, Sean Chambers <schambers80@gmail.com>
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
using System.IO;
using FluentMigrator.Expressions;
using Moq;
using NUnit.Framework;
using NUnit.Should;

namespace FluentMigrator.Tests.Unit.Expressions
{
    [TestFixture]
    public class ExecuteScriptFromSourceControlExpressionTests
    {
        private string _testFolder;
        [SetUp]
        public void Setup()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testFolder);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_testFolder,true);
        }

        [Test]
        public void GetsFileFromIntoSubFolder()
        {
            TestGetFile(@"\Foo\test.sql", 1, @"\Foo\test_1.sql");
        }

        [Test]
        public void CreateSubFolderFoo()
        {
            TestGetFile(@"Foo\test.sql", 1, @"Foo\test_1.sql");

            Directory.Exists(Path.Combine(_testFolder,"Foo")).ShouldBeTrue();
        }

        [Test]
        public void CreateSubFolderFooWithLeadingSlash()
        {
            TestGetFile(@"\Foo\test.sql", 1, @"\Foo\test_1.sql");

            Directory.Exists(Path.Combine(_testFolder, "Foo")).ShouldBeTrue();
        }

        [Test]
        public void SvnClientReadBaseUrl()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testFolder,"sourcecontrol.txt"),"svn://test.com");
            var client = new SvnClient { GetWorkingDirectory = () => _testFolder };

            // Act
            var settings = client.Settings;

            // Assert
            settings.BaseUri.ShouldBe("svn://test.com");
        }

        [Test]
        public void SvnClientSettingsNullIfConfigFileNotExist()
        {
            // Arrange
            var client = new SvnClient { GetWorkingDirectory = () => _testFolder };

            // Act
            var settings = client.Settings;

            // Assert
            settings.ShouldBeNull();
        }

        private void TestGetFile(string scriptFile, int revision, string expectedEndScript)
        {
            // Arrange
            var mockSourceControl = new Mock<ISourceControlClient>();
            var settings = new SourceControlSettings();
            var scriptExpression = new ExecuteSqlScriptExpression { SqlScript = scriptFile };
            var expression = new ExecuteScriptFromSourceControlExpression(scriptExpression)
                                 {
                                     GetWorkingDirectory = () => _testFolder,
                                     SourceControlClient = mockSourceControl.Object
                                 };


            mockSourceControl.Setup(m => m.Settings).Returns(settings);
            mockSourceControl.Setup(m => m.GetFile(scriptFile.Replace(@"\","/"), revision, Path.Combine(_testFolder, scriptFile)));

            // Act
            expression.FromSourceControl(1);

            // Assert
            scriptExpression.SqlScript.ShouldBe(expectedEndScript);
        }
    }
}


