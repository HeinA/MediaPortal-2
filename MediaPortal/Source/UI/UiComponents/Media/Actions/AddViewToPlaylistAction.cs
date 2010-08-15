#region Copyright (C) 2007-2010 Team MediaPortal

/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using MediaPortal.Core;
using MediaPortal.Core.Localization;
using MediaPortal.UI.Presentation.Workflow;
using MediaPortal.UiComponents.Media.Models;

namespace MediaPortal.UiComponents.Media.Actions
{
  public class AddViewToPlaylistAction : IWorkflowContributor
  {
    #region Consts

    public const string ADD_TO_PLAYLIST_RES = "[Media.AddAllToPlaylist]";

    #endregion

    #region IWorkflowContributor implementation

    public event ContributorStateChangeDelegate StateChanged;

    public bool IsActionVisible
    {
      get
      {
        IWorkflowManager workflowManager = ServiceRegistration.Get<IWorkflowManager>();
        NavigationContext currentNavigationContext = workflowManager.CurrentNavigationContext;
        NavigationData navigationData = MediaModel.GetNavigationData(currentNavigationContext);
        return navigationData != null;
      }
    }

    public bool IsActionEnabled
    {
      get { return true; }
    }

    public IResourceString DisplayTitle
    {
      get { return LocalizationHelper.CreateResourceString(ADD_TO_PLAYLIST_RES); }
    }

    public void Initialize()
    {
    }

    public void Uninitialize()
    {
    }

    public void Execute()
    {
      IWorkflowManager workflowManager = ServiceRegistration.Get<IWorkflowManager>();
      MediaModel model = (MediaModel) workflowManager.GetModel(MediaModel.MEDIA_MODEL_ID);
      model.AddCurrentViewToPlaylist();
    }

    #endregion
  }
}
