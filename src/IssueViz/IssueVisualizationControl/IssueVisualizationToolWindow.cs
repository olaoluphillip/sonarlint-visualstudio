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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Integration;
using SonarLint.VisualStudio.IssueVisualization.Editor;
using SonarLint.VisualStudio.IssueVisualization.IssueVisualizationControl.ViewModels;
using SonarLint.VisualStudio.IssueVisualization.Selection;

namespace SonarLint.VisualStudio.IssueVisualization.IssueVisualizationControl
{
    [Guid("bb3677d1-3b5c-45a3-8a3a-897108c3ba28")]
    public class IssueVisualizationToolWindow : ToolWindowPane
    {
        private readonly IssueVisualizationViewModel viewModel;

        public IssueVisualizationToolWindow(IServiceProvider serviceProvider) : base(null)
        {
            Caption = Resources.IssueVisualizationToolWindowCaption;

            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var selectionService = componentModel.GetService<IAnalysisIssueSelectionService>();
            var locationNavigator = componentModel.GetService<ILocationNavigator>();

            var imageService = serviceProvider.GetService(typeof(SVsImageService)) as IVsImageService2;
            var logger = componentModel.GetService<ILogger>();
            var fileNameLocationListItemCreator = new FileNameLocationListItemCreator(imageService, logger);

            viewModel = new IssueVisualizationViewModel(selectionService, new RuleHelpLinkProvider(), locationNavigator, fileNameLocationListItemCreator);
            Content = new IssueVisualizationControl(viewModel, locationNavigator);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                viewModel.Dispose();

                var vsWindowFrame = Frame as IVsWindowFrame;
                vsWindowFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
            }

            base.Dispose(disposing);
        }
    }
}
