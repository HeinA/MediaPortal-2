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

using MediaPortal.Common.Logging;
using MediaPortal.Common.Runtime;
using MediaPortal.Common.Services.Runtime.Settings;
using MediaPortal.Common.Settings;
using MediaPortal.Utilities.SystemAPI;

namespace MediaPortal.Common.Services.Runtime
{
  public class SystemStateService : ISystemStateService
  {
    protected SystemState _state = SystemState.Starting;

    public void SwitchSystemState(SystemState newState, bool sendMessage)
    {
      _state = newState;
      if (sendMessage)
        SystemMessaging.SendSystemStateChangeMessage(_state);
    }

    #region ISystemStateService implementation

    public SystemState CurrentState
    {
      get { return _state; }
    }

    public void Suspend()
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Hibernating");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.Hibernating);
      SystemSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<SystemSettings>();
      WindowsAPI.SetSuspendState(settings.UseHibernation, false, false);
    }

    #endregion
  }
}