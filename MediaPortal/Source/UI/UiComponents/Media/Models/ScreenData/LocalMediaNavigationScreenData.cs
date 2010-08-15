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

using System.Collections.Generic;
using MediaPortal.Core.MediaManagement;
using MediaPortal.UI.Views;
using MediaPortal.UiComponents.Media.General;

namespace MediaPortal.UiComponents.Media.Models.ScreenData
{
  public class LocalMediaNavigationScreenData : AbstractItemsScreenData
  {
    public LocalMediaNavigationScreenData(PlayableItemCreatorDelegate playableItemCreator) :
        base(Consts.LOCAL_MEDIA_NAVIGATION_SCREEN, null, Consts.LOCAL_MEDIA_NAVIGATION_NAVBAR_DISPLAY_LABEL_RES,
        playableItemCreator, true) { }

    public override IEnumerable<MediaItem> GetAllMediaItems()
    {
      return GetItemsRecursive(_navigationData.BaseViewSpecification.BuildView());
    }

    public override AbstractItemsScreenData Derive()
    {
      return new LocalMediaNavigationScreenData(PlayableItemCreator);
    }

    protected IEnumerable<MediaItem> GetItemsRecursive(View view)
    {
      foreach (MediaItem item in view.MediaItems)
        yield return item;
      foreach (View subView in view.SubViews)
        foreach (MediaItem item in GetItemsRecursive(subView))
          yield return item;
    }
  }
}