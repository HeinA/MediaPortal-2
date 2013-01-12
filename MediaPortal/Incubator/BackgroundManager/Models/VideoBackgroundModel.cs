﻿#region Copyright (C) 2007-2012 Team MediaPortal

/*
    Copyright (C) 2007-2012 Team MediaPortal
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

using System;
using System.Collections.Generic;
using System.IO;
using MediaPortal.Common;
using MediaPortal.Common.General;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Players.Video;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UiComponents.BackgroundManager.Helper;
using MediaPortal.UiComponents.BackgroundManager.Settings;

namespace MediaPortal.UiComponents.BackgroundManager.Models
{
  public class VideoBackgroundModel : IDisposable
  {
    public const string MODEL_ID_STR = "441288AC-F88D-4186-8993-6E259F7C75D8";
    public static readonly Guid[] NECESSARY_VIDEO_MIAS = new Guid[]
      {
          ProviderResourceAspect.ASPECT_ID,
          MediaAspect.ASPECT_ID,
          VideoAspect.ASPECT_ID,
      };

    protected string _videoFilename;
    protected VideoPlayer _videoPlayer;
    protected AbstractProperty _videoPlayerProperty;
    protected AbstractProperty _isEnabledProperty;
    protected AsynchronousMessageQueue _messageQueue;
    protected IPlayerSlotController _backgroundPsc = null;

    #region Protected fields

    #endregion

    public AbstractProperty VideoPlayerProperty
    {
      get { return _videoPlayerProperty; }
    }

    public ISlimDXVideoPlayer VideoPlayer
    {
      get { return (ISlimDXVideoPlayer) _videoPlayerProperty.GetValue(); }
      set { _videoPlayerProperty.SetValue(value); }
    }

    public AbstractProperty IsEnabledProperty
    {
      get { return _isEnabledProperty; }
    }

    public bool IsEnabled
    {
      get { return (bool) _isEnabledProperty.GetValue(); }
      set { _isEnabledProperty.SetValue(value); }
    }

    public VideoBackgroundModel()
    {
      _videoPlayerProperty = new WProperty(typeof(ISlimDXVideoPlayer), null);
      _isEnabledProperty = new WProperty(typeof(bool), false);
      _messageQueue = new AsynchronousMessageQueue(this, new[] { BackgroundManagerMessaging.CHANNEL });
      _messageQueue.MessageReceived += OnMessageReceived;
      _messageQueue.Start();
      RefreshSettings();
    }

    protected void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == BackgroundManagerMessaging.CHANNEL)
      {
        BackgroundManagerMessaging.MessageType messageType = (BackgroundManagerMessaging.MessageType) message.MessageType;
        if (messageType == BackgroundManagerMessaging.MessageType.SettingsChanged)
          RefreshSettings(true);
      }
    }

    /// <summary>
    /// Loads settings on startup or when changed inside configuration.
    /// </summary>
    protected void RefreshSettings(bool refresh = false)
    {
      EndBackgroundPlayback();
      BackgroundManagerSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<BackgroundManagerSettings>();
      _videoFilename = settings.VideoBackgroundFileName;
      IsEnabled = settings.EnableVideoBackground && !string.IsNullOrEmpty(_videoFilename) && File.Exists(_videoFilename);
      if (IsEnabled && refresh)
        StartBackgroundPlayback();
    }

    public void Dispose()
    {
      EndBackgroundPlayback();
      _messageQueue.Shutdown();
    }

    public void EndBackgroundPlayback()
    {
      if (_backgroundPsc != null)
      {
        IPlayerManager playerManager = ServiceRegistration.Get<IPlayerManager>();
        playerManager.CloseSlot(_backgroundPsc);
      }
    }

    public void StartBackgroundPlayback()
    {
      if (!IsEnabled)
        return;

      IPlayerManager playerManager = ServiceRegistration.Get<IPlayerManager>();
      if (_backgroundPsc == null)
        _backgroundPsc = playerManager.OpenSlot();

      if (_backgroundPsc == null)
        return;

      try
      {
        IMediaAccessor mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();
        IEnumerable<Guid> meIds = mediaAccessor.GetMetadataExtractorsForMIATypes(NECESSARY_VIDEO_MIAS);
        ResourceLocator resourceLocator = new ResourceLocator(LocalFsResourceProviderBase.ToResourcePath(_videoFilename));
        IResourceAccessor ra = resourceLocator.CreateAccessor();
        if (ra != null)
          using (ra)
          {
            MediaItem video = mediaAccessor.CreateLocalMediaItem(ra, meIds);
            _backgroundPsc.Play(video, StartTime.AtOnce);
            BaseDXPlayer player = _backgroundPsc.CurrentPlayer as BaseDXPlayer;
            if (player != null)
              player.AutoRepeat = true;

            VideoPlayer = player as ISlimDXVideoPlayer;
          }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("VideoBackgroundModel: Error opening MediaItem {0} for background playback!", ex, _videoFilename);
      }
    }
  }
}