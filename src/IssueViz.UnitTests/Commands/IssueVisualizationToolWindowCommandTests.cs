﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.ComponentModel.Design;
using FluentAssertions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using SonarLint.VisualStudio.Integration;
using SonarLint.VisualStudio.IssueVisualization.Commands;
using SonarLint.VisualStudio.IssueVisualization.IssueVisualizationControl;
using ThreadHelper = SonarLint.VisualStudio.Integration.UnitTests.ThreadHelper;

namespace SonarLint.VisualStudio.IssueVisualization.UnitTests.Commands
{
    [TestClass]
    public class IssueVisualizationToolWindowCommandTests
    {
        private Mock<AsyncPackage> package;
        private Mock<IMenuCommandService> commandService;
        private Mock<IVsMonitorSelection> monitorSelection;
        private Mock<ILogger> logger;
        private uint uiContextCookie = 999;

        [TestInitialize]
        public void TestInitialize()
        {
            package = new Mock<AsyncPackage>();
            commandService = new Mock<IMenuCommandService>();
            monitorSelection = new Mock<IVsMonitorSelection>();
            logger = new Mock<ILogger>();

            ThreadHelper.SetCurrentThreadAsUIThread();

            var uiContext = new Guid(IssueVisualization.Commands.Constants.UIContextGuid);
            monitorSelection.Setup(x => x.GetCmdUIContextCookie(ref uiContext, out uiContextCookie)).Returns(VSConstants.S_OK);
        }

        [TestMethod]
        public void Ctor_ArgsCheck()
        {
            Action act = () => new IssueVisualizationToolWindowCommand(null, commandService.Object, monitorSelection.Object, logger.Object);
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("package");

            act = () => new IssueVisualizationToolWindowCommand(package.Object, null, monitorSelection.Object, logger.Object);
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("commandService");

            act = () => new IssueVisualizationToolWindowCommand(package.Object, commandService.Object, null, logger.Object);
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("monitorSelection");

            act = () => new IssueVisualizationToolWindowCommand(package.Object, commandService.Object, monitorSelection.Object, null);
            act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Ctor_CommandAddedToMenu()
        {
            new IssueVisualizationToolWindowCommand(package.Object, commandService.Object, monitorSelection.Object, logger.Object);

            commandService.Verify(x =>
                    x.AddCommand(It.Is((MenuCommand c) =>
                        c.CommandID.Guid == IssueVisualizationToolWindowCommand.CommandSet &&
                        c.CommandID.ID == IssueVisualizationToolWindowCommand.ViewToolWindowCommandId)),
                Times.Once);

            commandService.Verify(x =>
                    x.AddCommand(It.Is((OleMenuCommand c) =>
                        c.CommandID.Guid == IssueVisualizationToolWindowCommand.CommandSet &&
                        c.CommandID.ID == IssueVisualizationToolWindowCommand.ErrorListCommandId)),
                Times.Once);

            commandService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void ErrorListQueryStatus_NonCriticalException_IsSuppressed()
        {
            var result = 1;
            monitorSelection
                .Setup(x => x.IsCmdUIContextActive(uiContextCookie, out result))
                .Throws(new NotImplementedException("this is a test"));

            var testSubject = CreateTestSubject();

            Action act = () => testSubject.ErrorListQueryStatus(null, EventArgs.Empty);
            act.Should().NotThrow();

            VerifyMessageLogged("this is a test");
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ErrorListQueryStatus_CommandVisibilityIsSetToContextActivation(bool isContextActive)
        {
            var isActive = isContextActive ? 1 : 0;
            monitorSelection.Setup(x => x.IsCmdUIContextActive(uiContextCookie, out isActive)).Returns(VSConstants.S_OK);

            var testSubject = CreateTestSubject();

            testSubject.ErrorListMenuItem.Visible = !isContextActive;

            testSubject.ErrorListQueryStatus(null, EventArgs.Empty);

            testSubject.ErrorListMenuItem.Visible.Should().Be(isContextActive);
        }

        [TestMethod]
        public void Execute_WindowNotCreated_NoException()
        {
            SetupWindowCreation(null);

            VerifyExecutionDoesNotThrow();

            VerifyMessageLogged("cannot create window frame");
        }

        [TestMethod]
        public void Execute_WindowCreatedWithoutFrame_NoException()
        {
            SetupWindowCreation(new ToolWindowPane());

            VerifyExecutionDoesNotThrow();

            VerifyMessageLogged("cannot create window frame");
        }

        [TestMethod]
        public void Execute_NonCriticalExceptionInCreatingWindow_IsSuppressed()
        {
            SetupWindowCreation(new ToolWindowPane { Frame = Mock.Of<IVsWindowFrame>() }, new NotImplementedException("this is a test"));

            VerifyExecutionDoesNotThrow();

            VerifyMessageLogged("this is a test");
        }

        [TestMethod]
        public void Execute_WindowCreatedWithIVsWindowFrame_WindowIsShown()
        {
            var frame = new Mock<IVsWindowFrame>();
            SetupWindowCreation(new ToolWindowPane { Frame = frame.Object });

            VerifyExecutionDoesNotThrow();

            frame.Verify(x => x.Show(), Times.Once);

            logger.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void Execute_WindowCreatedWithIVsWindowFrame_ExceptionInShowingWindow_NoException()
        {
            var frame = new Mock<IVsWindowFrame>();
            frame.Setup(x => x.Show()).Throws(new NotImplementedException("this is a test"));

            SetupWindowCreation(new ToolWindowPane { Frame = frame.Object });

            VerifyExecutionDoesNotThrow();

            frame.Verify(x => x.Show(), Times.Once);

            VerifyMessageLogged("this is a test");
        }

        private void SetupWindowCreation(WindowPane windowPane, Exception exceptionToThrow = null)
        {
            var setup = package.Protected().Setup<WindowPane>("CreateToolWindow", typeof(IssueVisualizationToolWindow), 0);

            if (exceptionToThrow == null)
            {
                setup.Returns(windowPane);
            }
            else
            {
                setup.Throws(exceptionToThrow);
            }
        }

        private void VerifyMessageLogged(string expectedMessage)
        {
            logger.Verify(x =>
                    x.WriteLine(It.Is((string message) => message.Contains(expectedMessage))),
                Times.Once);
        }

        private void VerifyExecutionDoesNotThrow()
        {
            var testSubject = CreateTestSubject();

            Action act = () => testSubject.Execute(this, EventArgs.Empty);
            act.Should().NotThrow();
        }

        private IssueVisualizationToolWindowCommand CreateTestSubject() => 
            new IssueVisualizationToolWindowCommand(package.Object, commandService.Object, monitorSelection.Object, logger.Object);
    }
}
