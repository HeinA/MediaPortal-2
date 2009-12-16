#region Copyright (C) 2007-2009 Team MediaPortal

/*
    Copyright (C) 2007-2009 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Core;
using MediaPortal.Core.Commands;
using MediaPortal.Core.MediaManagement;
using MediaPortal.Core.MediaManagement.DefaultItemAspects;
using MediaPortal.UI.Views;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Models;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.Presentation.Screens;
using MediaPortal.UI.Presentation.Workflow;

namespace UiComponents.Media.Models
{
  /// <summary>
  /// Model which holds the GUI state for the current navigation in the media views.
  /// </summary>
  public class MediaModel : IWorkflowModel
  {
    #region Consts

    public const string MEDIA_MODEL_ID_STR = "4CDD601F-E280-43b9-AD0A-6D7B2403C856";

    public const string MUSIC_MODULE_ID_STR = "53130C0E-D19C-4972-92F4-DB6665E51CBC";
    public const string VIDEO_MODULE_ID_STR = "BB5362E4-3723-4a11-A989-A4B01ECCEB14";
    public const string PICTURE_MODULE_ID_STR = "DE0A2E53-1898-4e50-B27F-8652C3D11EDF";

    public const string MEDIA_MAIN_SCREEN = "media";

    protected const string VIEW_KEY = "MediaModel: VIEW";

    // Keys for the ListItem's Labels in the ItemsLists
    public const string NAME_KEY = "Name";
    public const string MEDIA_ITEM_KEY = "MediaItem";

    public const string PLAY_AUDIO_ITEM_RESOURCE = "[Media.PlayAudioItem]";
    public const string ENQUEUE_AUDIO_ITEM_RESOURCE = "[Media.EnqueueAudioItem]";
    public const string MUTE_VIDEO_PLAY_AUDIO_ITEM_RESOURCE = "[Media.MuteVideoAndPlayAudioItem]";

    public const string PLAY_VIDEO_ITEM_RESOURCE = "[Media.PlayVideoItem]";
    public const string ENQUEUE_VIDEO_ITEM_RESOURCE = "[Media.EnqueueVideoItem]";
    public const string PLAY_VIDEO_ITEM_MUTED_CONCURRENT_AUDIO_RESOURCE = "[Media.PlayVideoItemMutedConcurrentAudio]";
    public const string PLAY_VIDEO_ITEM_PIP_RESOURCE = "[Media.PlayVideoItemPIP]";

    public const string VIDEO_PLAYER_CONTEXT_NAME_RESOURCE = "[Media.VideoPlayerContextName]";
    public const string PICTURE_PLAYER_CONTEXT_NAME_RESOURCE = "[Media.PicturePlayerContextName]";
    public const string AUDIO_PLAYER_CONTEXT_NAME_RESOURCE = "[Media.AudioPlayerContextName]";

    public const string SYSTEM_INFORMATION_RESOURCE = "[System.Information]";
    public const string CANNOT_PLAY_ITEM_RESOURCE = "[Media.CannotPlayItemDialogText]";

    public const string LOCAL_MEDIA_VIEW_NAME_RESOURCE = "[Media.LocalMediaViewName]";

    public const string PLAY_MENU_DIALOG_SCREEN = "DialogPlayMenu";

    public static Guid MUSIC_MODULE_ID = new Guid(MUSIC_MODULE_ID_STR);
    public static Guid VIDEO_MODULE_ID = new Guid(VIDEO_MODULE_ID_STR);
    public static Guid PICTURE_MODULE_ID = new Guid(PICTURE_MODULE_ID_STR);

    public static Guid CURRENTLY_PLAYING_VIDEO_WORKFLOW_STATE_ID = VideoPlayerModel.CURRENTLY_PLAYING_STATE_ID;
    public static Guid FULLSCREEN_VIDEO_WORKFLOW_STATE_ID = VideoPlayerModel.FULLSCREEN_CONTENT_STATE_ID;

    public static Guid CURRENTLY_PLAYING_AUDIO_WORKFLOW_STATE_ID = AudioPlayerModel.CURRENTLY_PLAYING_STATE_ID;
    public static Guid FULLSCREEN_AUDIO_WORKFLOW_STATE_ID = AudioPlayerModel.FULLSCREEN_CONTENT_STATE_ID;

    #endregion

    #region Protected fields

    protected ViewSpecification _rootViewSpecification;

    // Media screen
    protected ItemsList _mediaItems = null;
    protected View _currentView;
    protected bool _hasParentDirectory;

    // Play menu
    protected ItemsList _playMenuItems = null;

    #endregion

    public MediaModel()
    {
      _rootViewSpecification = new LocalSharesViewSpecification(LOCAL_MEDIA_VIEW_NAME_RESOURCE, new Guid[]
        {
          ProviderResourceAspect.ASPECT_ID,
          MediaAspect.ASPECT_ID,
        });
      _hasParentDirectory = false;
    }

    /// <summary>
    /// Provides a list with the sub views and media items of the current view.
    /// Note: This <see cref="MediaItems"/> list doesn't contain an item to navigate to the parent view.
    /// It is job of the skin to provide a means to navigate to the parent view.
    /// </summary>
    public ItemsList MediaItems
    {
      get { return _mediaItems; }
    }

    /// <summary>
    /// Gets the information whether the current view has a navigatable parent view.
    /// </summary>
    public bool HasParentDirectory
    {
      get { return _hasParentDirectory; }
      set { _hasParentDirectory = value; }
    }

    /// <summary>
    /// Provides the data of the view currently shown.
    /// </summary>
    public View CurrentView
    {
      get { return _currentView; }
      set { _currentView = value; }
    }

    /// <summary>
    /// Provides a list of items to be shown in the play menu.
    /// </summary>
    public ItemsList PlayMenuItems
    {
      get { return _playMenuItems; }
    }

    /// <summary>
    /// Provides a callable method for the skin to select an item.
    /// Depending on the item type, we will navigate to the choosen view or play the choosen item.
    /// </summary>
    /// <param name="item">The choosen item. This item should be one of the items in the
    /// <see cref="MediaItems"/> list.</param>
    public void Select(ListItem item)
    {
      if (item == null)
        return;
      NavigationItem navigationItem = item as NavigationItem;
      if (navigationItem != null)
      {
        NavigateToView(navigationItem.View);
        return;
      }
      PlayableItem playableItem = item as PlayableItem;
      if (playableItem != null)
      {
        CheckPlayMenu(playableItem.MediaItem);
        return;
      }
    }

    #region Protected methods

    /// <summary>
    /// Does the actual work of navigating to the specifield view. This will exchange our
    /// <see cref="CurrentView"/> to the specified <paramref name="view"/> and push a state onto
    /// the workflow manager's navigation stack.
    /// </summary>
    /// <param name="view">View to navigate to.</param>
    protected static void NavigateToView(View view)
    {
      WorkflowState newState = WorkflowState.CreateTransientState(
          "View: " + view.DisplayName, MEDIA_MAIN_SCREEN, true, WorkflowType.Workflow);
      IWorkflowManager workflowManager = ServiceScope.Get<IWorkflowManager>();
      IDictionary<string, object> variables = new Dictionary<string, object>
        {
            {VIEW_KEY, view}
        };
      workflowManager.NavigatePushTransient(newState, variables);
    }

    /// <summary>
    /// Checks if we need to show a menu for playing the specified <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item to play.</param>
    protected void CheckPlayMenu(MediaItem item)
    {
      IPlayerContextManager pcm = ServiceScope.Get<IPlayerContextManager>();
      int numOpen = pcm.NumActivePlayerContexts;
      if (numOpen == 0)
      {
        PlayItem(item);
        return;
      }
      _playMenuItems = new ItemsList();
      PlayerContextType mediaType = pcm.GetTypeOfMediaItem(item);
      int numAudio = pcm.NumPlayerContextsOfMediaType(PlayerContextType.Audio);
      int numVideo = pcm.NumPlayerContextsOfMediaType(PlayerContextType.Video);
      if (mediaType == PlayerContextType.Audio)
      {
        ListItem playItem = new ListItem(NAME_KEY, PLAY_AUDIO_ITEM_RESOURCE)
          {
              Command = new MethodDelegateCommand(() => PlayItem(item))
          };
        _playMenuItems.Add(playItem);
        if (numAudio > 0)
        {
          ListItem enqueueItem = new ListItem(NAME_KEY, ENQUEUE_AUDIO_ITEM_RESOURCE)
            {
                Command = new MethodDelegateCommand(() => PlayOrEnqueueItem(item, false, false, false))
            };
          _playMenuItems.Add(enqueueItem);
        }
        if (numVideo > 0)
        {
          ListItem playItemConcurrently = new ListItem(NAME_KEY, MUTE_VIDEO_PLAY_AUDIO_ITEM_RESOURCE)
            {
                Command = new MethodDelegateCommand(() => PlayOrEnqueueItem(item, true, true, false))
            };
          _playMenuItems.Add(playItemConcurrently);
        }
      }
      else if (mediaType == PlayerContextType.Video)
      {
        ListItem playItem = new ListItem(NAME_KEY, PLAY_VIDEO_ITEM_RESOURCE)
          {
              Command = new MethodDelegateCommand(() => PlayItem(item))
          };
        _playMenuItems.Add(playItem);
        if (numVideo > 0)
        {
          ListItem enqueueItem = new ListItem(NAME_KEY, ENQUEUE_VIDEO_ITEM_RESOURCE)
            {
                Command = new MethodDelegateCommand(() => PlayOrEnqueueItem(item, false, false, false))
            };
          _playMenuItems.Add(enqueueItem);
        }
        if (numAudio > 0)
        {
          ListItem playItem_A = new ListItem(NAME_KEY, PLAY_VIDEO_ITEM_MUTED_CONCURRENT_AUDIO_RESOURCE)
            {
                Command = new MethodDelegateCommand(() => PlayOrEnqueueItem(item, true, true, false))
            };
          _playMenuItems.Add(playItem_A);
        }
        if (numVideo > 0)
        {
          ListItem playItem_V = new ListItem(NAME_KEY, PLAY_VIDEO_ITEM_PIP_RESOURCE)
            {
                Command = new MethodDelegateCommand(() => PlayOrEnqueueItem(item, true, true, true))
            };
          _playMenuItems.Add(playItem_V);
        }
      }
      else
      {
        IDialogManager dialogManager = ServiceScope.Get<IDialogManager>();
        dialogManager.ShowDialog(SYSTEM_INFORMATION_RESOURCE, CANNOT_PLAY_ITEM_RESOURCE, DialogType.OkDialog, false,
            DialogButtonType.Ok);
      }
      IScreenManager screenManager = ServiceScope.Get<IScreenManager>();
      screenManager.ShowDialog(PLAY_MENU_DIALOG_SCREEN);
    }

    /// <summary>
    /// Provides a callable method for the play menu dialog to select one of the items representing
    /// a user choice of a player slot.
    /// </summary>
    /// <param name="item">The item which has an attached <see cref="ListItem.Command"/>.</param>
    public void ExecuteMenuItem(ListItem item)
    {
      if (item == null)
        return;
      ICommand command = item.Command;
      if (command != null)
        command.Execute();
    }

    /// <summary>
    /// Discards any currently playing items and plays the specified media <paramref name="item"/>.
    /// </summary>
    /// <param name="item">Media item to be played.</param>
    protected static void PlayItem(MediaItem item)
    {
      IPlayerManager playerManager = ServiceScope.Get<IPlayerManager>();
      playerManager.CloseSlot(PlayerManagerConsts.SECONDARY_SLOT);
      PlayOrEnqueueItem(item, true, false, false);
    }

    protected static IPlayerContext GetPlayerContextByMediaType(PlayerContextType mediaType, bool concurrent)
    {
      IPlayerContextManager pcm = ServiceScope.Get<IPlayerContextManager>();
      IPlayerContext result;
      if (mediaType == PlayerContextType.Video && concurrent || mediaType == PlayerContextType.Audio)
        // Concurrent video & audio - search in reverse order
        for (int i = 1; i >= 0; i--)
        {
          if ((result = pcm.GetPlayerContext(i)) != null && result.MediaType == mediaType)
            return result;
        }
      else // Non-concurrent video - search in normal order
        for (int i = 0; i < 2; i++)
        {
          if ((result = pcm.GetPlayerContext(i)) != null && result.MediaType == PlayerContextType.Video)
            return result;
        }
      return null;
    }

    public static bool GetModuleIdAndNameForMediaItem(MediaItem item, out Guid moduleId, out string contextName)
    {
      // No locking necessary
      if (item.Aspects.ContainsKey(MovieAspect.Metadata.AspectId))
      {
        moduleId = VIDEO_MODULE_ID;
        contextName = VIDEO_PLAYER_CONTEXT_NAME_RESOURCE;
        return true;
      }
      else if (item.Aspects.ContainsKey(PictureAspect.Metadata.AspectId))
      {
        moduleId = PICTURE_MODULE_ID;
        contextName = PICTURE_PLAYER_CONTEXT_NAME_RESOURCE;
        return true;
      }
      else if (item.Aspects.ContainsKey(MusicAspect.Metadata.AspectId))
      {
        moduleId = MUSIC_MODULE_ID;
        contextName = AUDIO_PLAYER_CONTEXT_NAME_RESOURCE;
        return true;
      }
      moduleId = new Guid();
      contextName = null;
      return false;
    }

    /// <summary>
    /// Depending on parameter <paramref name="play"/>, plays or enqueues the specified media <paramref name="item"/>.
    /// </summary>
    /// <param name="item">Media item to be played.</param>
    /// <param name="play">If <c>true</c>, plays the specified <paramref name="item"/>, else enqueues it.</param>
    /// <param name="concurrent">If set to <c>true</c>, the <paramref name="item"/> will be played concurrently to
    /// an already playing player. Else, all other players will be stopped first.</param>
    /// <param name="subordinatedVideo">If set to <c>true</c>, a video item will be played in PIP mode, if
    /// applicable.</param>
    protected static void PlayOrEnqueueItem(MediaItem item, bool play, bool concurrent, bool subordinatedVideo)
    {
      IPlayerContextManager pcm = ServiceScope.Get<IPlayerContextManager>();
      PlayerContextType mediaType = pcm.GetTypeOfMediaItem(item);
      Guid moduleId;
      string contextName;
      if (!GetModuleIdAndNameForMediaItem(item, out moduleId, out contextName))
        return;
      IPlayerContext pc = null;
      if (!play)
        // !play means enqueue - so find our first player context of the correct media type
        foreach (IPlayerContext mmPC in pcm.GetPlayerContextsByMediaModuleId(moduleId))
          if (mmPC.MediaType == mediaType)
          {
            pc = mmPC;
            break;
          }
      if (pc == null)
        // No player context to reuse - so open a new one
        if (mediaType == PlayerContextType.Video)
          pc = pcm.OpenVideoPlayerContext(moduleId, contextName, concurrent, subordinatedVideo,
              CURRENTLY_PLAYING_VIDEO_WORKFLOW_STATE_ID, FULLSCREEN_VIDEO_WORKFLOW_STATE_ID);
        else if (mediaType == PlayerContextType.Audio)
          pc = pcm.OpenAudioPlayerContext(moduleId, contextName, concurrent,
              CURRENTLY_PLAYING_AUDIO_WORKFLOW_STATE_ID, FULLSCREEN_AUDIO_WORKFLOW_STATE_ID);
      if (pc == null)
        return;
      if (play)
        pc.Playlist.Clear();
      if (mediaType == PlayerContextType.Video)
      {
        // We don't need a playlist when just playing a single video item
        pc.DoPlay(item);
        pc.CloseWhenFinished = true; // Has to be done after starting the media item, else the slot might be closed at once
        pcm.ShowFullscreenContent();
      }
      else
      {
        pc.Playlist.Add(item);
        pc.Play();
      }
    }

    protected void ReloadItems()
    {
      // We need to create a new items list because the reloading of items takes place while the old
      // screen still shows the old items
      _mediaItems = new ItemsList();
      // TODO: Add the items in a separate job while the UI already shows the new screen
      View currentView = CurrentView;
      HasParentDirectory = currentView.ParentView != null;
      if (currentView.IsValid)
      {
        // Add items for sub views
        foreach (View subView in currentView.SubViews)
          _mediaItems.Add(new NavigationItem(subView, null));
        foreach (MediaItem item in currentView.MediaItems)
          _mediaItems.Add(new PlayableItem(item));
      }
    }

    protected View GetViewFromContext(NavigationContext context)
    {
      View view = context.GetContextVariable(VIEW_KEY, true) as View;
      return view ?? _rootViewSpecification.BuildRootView();
    }

    #endregion

    #region IWorkflowModel implementation

    public Guid ModelId
    {
      get { return new Guid(MEDIA_MODEL_ID_STR); }
    }

    public bool CanEnterState(NavigationContext oldContext, NavigationContext newContext)
    {
      return true;
    }

    public void EnterModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
      CurrentView = GetViewFromContext(newContext);
      ReloadItems();
    }

    public void ExitModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
      // We could dispose some data here when exiting media navigation context
    }

    public void ChangeModelContext(NavigationContext oldContext, NavigationContext newContext, bool push)
    {
      CurrentView = GetViewFromContext(newContext);
      ReloadItems();
    }

    public void Deactivate(NavigationContext oldContext, NavigationContext newContext)
    {
    }

    public void ReActivate(NavigationContext oldContext, NavigationContext newContext)
    {
      View newView = GetViewFromContext(newContext);
      if (newView == CurrentView)
        return;
      CurrentView = newView;
      ReloadItems();
    }

    public void UpdateMenuActions(NavigationContext context, ICollection<WorkflowAction> actions)
    {
    }

    #endregion
  }
}