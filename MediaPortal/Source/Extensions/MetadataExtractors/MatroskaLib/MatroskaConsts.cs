#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System.Collections.Generic;

namespace MediaPortal.Extensions.MetadataExtractors.MatroskaLib
{
  public class MatroskaConsts
  {
    // Tags are constructed by using TargetTypeValue (i.e. 70) and the name of the <Simple> tag (i.e. TITLE).
    public const string TAG_SERIES_TITLE = "70.TITLE";
    public const string TAG_SERIES_GENRE = "70.GENRE";
    public const string TAG_SERIES_ACTORS = "70.ACTOR";
    public const string TAG_SEASON_YEAR = "60.DATE_RELEASE";
    public const string TAG_SEASON_TITLE = "60.TITLE";
    public const string TAG_EPISODE_TITLE = "50.TITLE";
    public const string TAG_EPISODE_SUMMARY = "50.SUMMARY";
    public const string TAG_ACTORS = "50.ACTOR";
    public const string TAG_SEASON_NUMBER = "60.PART_NUMBER";
    public const string TAG_EPISODE_YEAR = "50.DATE_RELEASED";
    public const string TAG_EPISODE_NUMBER = "50.PART_NUMBER";
    public const string TAG_SIMPLE_TITLE = "TITLE";

    public static Dictionary<string, IList<string>> DefaultTags
    {
      get
      {
        return new Dictionary<string, IList<string>>
          {
            {TAG_SERIES_TITLE, null}, // Series title
            {TAG_SERIES_GENRE, null}, // Series genre(s)
            {TAG_SERIES_ACTORS, null}, // Series actor(s)
            {TAG_SEASON_NUMBER, null}, // Season number
            {TAG_SEASON_YEAR, null}, // Season year
            {TAG_SEASON_TITLE, null}, // Season title
            {TAG_EPISODE_TITLE, null}, // Episode title
            {TAG_EPISODE_SUMMARY, null}, // Episode summary
            {TAG_EPISODE_YEAR, null}, // Episode year
            {TAG_EPISODE_NUMBER, null}, // Episode number
            {TAG_ACTORS, null}, // Actor(s)
            {TAG_SIMPLE_TITLE, null} // File title
          };
      }
    }
  }
}
