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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.Dokan;
using MediaPortal.Common.Services.ResourceAccess.Settings;
using MediaPortal.Common.Settings;
using MediaPortal.Common.SystemResolver;

namespace MediaPortal.Common.Services.ResourceAccess
{
  public class ResourceMountingService : IResourceMountingService, IDisposable
  {
    #region Consts

    /// <summary>
    /// Volume label for the virtual drive if our mount point is a drive letter.
    /// </summary>
    public static string VOLUME_LABEL = "MediaPortal 2 resource access";

    protected const FileAttributes FILE_ATTRIBUTES = FileAttributes.ReadOnly;
    protected const FileAttributes DIRECTORY_ATTRIBUTES = FileAttributes.ReadOnly | FileAttributes.NotContentIndexed | FileAttributes.Directory;

    protected static readonly DateTime MIN_FILE_DATE = DateTime.FromFileTime(0); // If we use lower values, resources are not recognized by the Explorer/Windows API

    #endregion

    protected object _syncObj = new object();
    protected Dokan.Dokan _dokanExecutor = null;

    protected char? ReadDriveLetterFromSettings()
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      ResourceMountingSettings settings = settingsManager.Load<ResourceMountingSettings>();
      return settings.DriveLetter;
    }

    protected void SaveDefaultDriveSettings(char driveLetter)
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      ResourceMountingSettings settings = settingsManager.Load<ResourceMountingSettings>();
      settings.DriveLetter = driveLetter;
      settingsManager.Save(settings);
    }

    #region IDisposable implementation

    public void Dispose()
    {
      Shutdown(); // Make sure we're shut down
    }

    #endregion

    #region IResourceMountingService implementation

    public ResourcePath RootPath
    {
      get
      {
        lock (_syncObj)
          return _dokanExecutor == null ? null : LocalFsResourceProviderBase.ToResourcePath(_dokanExecutor.DriveLetter + ":\\");
      }
    }

    public ICollection<string> RootDirectories
    {
      get
      {
        lock (_syncObj)
          return new List<string>(_dokanExecutor.RootDirectory.ChildResources.Keys);
      }
    }

    public void Startup()
    {
      char? driveLetter = ReadDriveLetterFromSettings();
      if (!driveLetter.HasValue)
      {
        ISystemResolver systemResolver = ServiceRegistration.Get<ISystemResolver>();
        driveLetter = systemResolver.SystemType == SystemType.Server ? ResourceMountingSettings.DEFAULT_DRIVE_LETTER_SERVER :
            ResourceMountingSettings.DEFAULT_DRIVE_LETTER_CLIENT;
        // Save the current default setting to be able to change it in config file
        SaveDefaultDriveSettings(driveLetter.Value);
      }
      _dokanExecutor = Dokan.Dokan.Install(driveLetter.Value);
      if (_dokanExecutor == null)
        ServiceRegistration.Get<ILogger>().Warn("ResourceMountingService: Due to problems in DOKAN, resources cannot be mounted into the local filesystem");
      else
        // We share the same synchronization object to avoid multithreading issues between the two classes
        _syncObj = _dokanExecutor.SyncObj;
    }

    public void Shutdown()
    {
      if (_dokanExecutor == null)
        return;
      _dokanExecutor.Dispose();
      _dokanExecutor = null;
    }

    public bool IsVirtualResource(ResourcePath rp)
    {
      if (rp == null)
        return false;
      int numPathSegments = rp.Count();
      if (numPathSegments == 0)
        return false;
      ResourcePath firtsRPSegment = new ResourcePath(new ProviderPathSegment[] {rp[0]});
      String pathRoot = Path.GetPathRoot(LocalFsResourceProviderBase.ToDosPath(firtsRPSegment));
      if (string.IsNullOrEmpty(pathRoot))
        return false;
      return Dokan.Dokan.IsDokanDrive(pathRoot[0]);
    }

    public string CreateRootDirectory(string rootDirectoryName)
    {
      lock (_syncObj)
      {
        ResourcePath rootPath = RootPath;
        if (rootPath == null)
          return null;
        _dokanExecutor.RootDirectory.AddResource(rootDirectoryName, new VirtualRootDirectory(rootDirectoryName));
        return ResourcePathHelper.Combine(rootPath.Serialize(), rootDirectoryName);
      }
    }

    public void DisposeRootDirectory(string rootDirectoryName)
    {
      lock (_syncObj)
      {
        if (_dokanExecutor == null)
          return;
        VirtualRootDirectory rootDirectory = _dokanExecutor.GetRootDirectory(rootDirectoryName);
        if (rootDirectory == null)
          return;
        _dokanExecutor.RootDirectory.RemoveResource(rootDirectoryName);
        rootDirectory.Dispose();
      }
    }

    public ICollection<IFileSystemResourceAccessor> GetResources(string rootDirectoryName)
    {
      lock (_syncObj)
      {
        if (_dokanExecutor == null)
          return null;
        VirtualRootDirectory rootDirectory = _dokanExecutor.GetRootDirectory(rootDirectoryName);
        if (rootDirectory == null)
          return null;
        return rootDirectory.ChildResources.Values.Select(resource => resource.ResourceAccessor).ToList();
      }
    }

    public string AddResource(string rootDirectoryName, IFileSystemResourceAccessor resourceAccessor)
    {
      lock (_syncObj)
      {
        if (_dokanExecutor == null)
          return null;
        VirtualRootDirectory rootDirectory = _dokanExecutor.GetRootDirectory(rootDirectoryName);
        if (rootDirectory == null)
          return null;
        string resourceName = resourceAccessor.ResourceName;
        rootDirectory.AddResource(resourceName, !resourceAccessor.IsFile?
            (VirtualFileSystemResource) new VirtualDirectory(resourceName, resourceAccessor) :
            new VirtualFile(resourceName, resourceAccessor));
        char driveLetter = _dokanExecutor.DriveLetter;
        return Path.Combine(driveLetter + ":\\", rootDirectoryName + "\\" + resourceName);
      }
    }

    public void RemoveResource(string rootDirectoryName, IFileSystemResourceAccessor resourceAccessor)
    {
      lock (_syncObj)
      {
        if (_dokanExecutor == null)
          return;
        VirtualRootDirectory rootDirectory = _dokanExecutor.GetRootDirectory(rootDirectoryName);
        if (rootDirectory == null)
          return;
        string resourceName = resourceAccessor.ResourceName;
        VirtualFileSystemResource toRemove;
        if (!rootDirectory.ChildResources.TryGetValue(resourceName, out toRemove))
          return;
        rootDirectory.ChildResources.Remove(resourceName);
        toRemove.Dispose();
      }
    }

    #endregion
  }
}