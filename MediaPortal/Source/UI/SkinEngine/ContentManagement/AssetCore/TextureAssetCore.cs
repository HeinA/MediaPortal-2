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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Cache;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.SkinManagement;
using MediaPortal.UI.Thumbnails;
using SlimDX.Direct3D9;

namespace MediaPortal.UI.SkinEngine.ContentManagement.AssetCore
{
  // TODO: Tidy up
  public class TextureAssetCore : TemporaryAssetBase, IAssetCore
  {
    public event AssetAllocationHandler AllocationChanged = delegate { };

    #region Variables

    private enum State
    {
      Unknown,
      Creating,
      Created,
      DoesNotExist
    };

    private Texture _texture = null;
    private int _width;
    private int _height;
    private float _maxV;
    private float _maxU;
    private readonly string _textureName;
    private State _state = State.Unknown;
    private string _sourceFileName;
    private WebClient _webClient;
    private byte[] _buffer;
    private bool _useThumbnail = true;
    private int _allocationSize = 0;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureAsset"/> class.
    /// </summary>
    /// <param name="textureName">Name of the texture.</param>
    public TextureAssetCore(string textureName)
    {
      _textureName = textureName;
    }

    public TextureAssetCore(Texture texture, float maxU, float maxV)
    {
      _texture = texture;
      _maxU = maxU;
      _maxV = maxV;
    }

    public Texture Texture
    {
      get
      {
        KeepAlive();
        return _texture;
      }
    }

    public bool UseThumbnail
    {
      get { return _useThumbnail; }
      set { _useThumbnail = value; }
    }

    public string Name
    {
      get { return _textureName; }
    }

    public int Width
    {
      get { return _width; }
    }

    public int Height
    {
      get { return _height; }
    }

    public float MaxU
    {
      get { return _maxU; }
    }

    public float MaxV
    {
      get { return _maxV; }
    }

    /// <summary>
    /// Loads the specified texture from the file.
    /// </summary>
    public void Allocate()
    {
      KeepAlive();
      if (string.IsNullOrEmpty(_textureName))
      {
        _state = State.DoesNotExist;
        return;
      }
      if (_state == State.DoesNotExist)
      {
        return;
      }
      byte[] thumbData = null;
      ImageType imageType = ImageType.Unknown;
      ImageInformation info = new ImageInformation();

      IAsyncThumbnailGenerator generator = ServiceRegistration.Get<IAsyncThumbnailGenerator>();
      if (_state == State.Unknown)
      {
        string sourceFilePath = SkinContext.SkinResources.GetResourceFilePath(
            SkinResources.MEDIA_DIRECTORY + "\\" + _textureName);
        if (sourceFilePath != null && File.Exists(sourceFilePath))
        {
          _sourceFileName = sourceFilePath;
          _state = State.Created;
        }

        if (_state == State.Unknown)
        {
          Uri uri;
          if (!Uri.TryCreate(_textureName, UriKind.Absolute, out uri))
          {
            ServiceRegistration.Get<ILogger>().Error("Cannot open texture :{0}", _textureName);
            _state = State.DoesNotExist;
            return;
          }

          if (uri.IsFile)
          {
            _sourceFileName = uri.LocalPath;
            if (UseThumbnail)
            {
              if (generator.GetThumbnail(_sourceFileName, out thumbData, out imageType))
                _state = State.Created;
              else if (generator.IsCreating(_sourceFileName))
                _state = State.Creating;
              else
              {
                generator.CreateThumbnail(_sourceFileName);
                _state = State.Creating;
              }
            }
            else
            {
              _state = State.Created;
            }
          }
          else
          {
            if (_state == State.Unknown)
            {
              if (_webClient == null)
              {
                _state = State.Creating;
                _webClient = new WebClient
                  {
                      CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable)
                  };
                _webClient.DownloadDataCompleted += _webClient_DownloadDataCompleted;
                _webClient.DownloadDataAsync(uri);
                return;
              }
            }
          }
        }
      }

      if (_state == State.Creating && _webClient == null && !generator.IsCreating(_sourceFileName))
        _state = generator.GetThumbnail(_sourceFileName, out thumbData, out imageType) ? State.Created : State.DoesNotExist;

      if (_state == State.Creating)
        return;
      if (_state == State.DoesNotExist)
        return;
      if (_webClient != null)
      {
        thumbData = _buffer;
        _buffer = null;
        _webClient.Dispose();
        _webClient = null;
      }

      if (_texture != null)
        Free();
      if (thumbData != null)
      {
        using (MemoryStream stream = new MemoryStream(thumbData))
        {
          try
          {
            //ServiceRegistration.Get<ILogger>().Debug("TEXTURE alloc from thumbdata:{0}", _textureName);
            if (UseThumbnail)
            {
              info = ImageInformation.FromStream(stream);
              stream.Seek(0, SeekOrigin.Begin);
              _texture = Texture.FromStream(GraphicsDevice.Device, stream, 0, 0, 1, Usage.None, Format.A8R8G8B8,
                  Pool.Default, Filter.None, Filter.None, 0);
            }
            else
            {
              ImageInformation imgInfo = ImageInformation.FromStream(stream);
              stream.Seek(0, SeekOrigin.Begin);
              if (imgInfo.Width > GraphicsDevice.Width || imgInfo.Height > GraphicsDevice.Height)
              {
                using (Image imgSource = Image.FromStream(stream))
                {
                  info = Scale(imgSource, imgInfo);
                }
              }
              else
              {
                info = ImageInformation.FromStream(stream);
                stream.Seek(0, SeekOrigin.Begin);
                _texture = Texture.FromStream(GraphicsDevice.Device, stream, 0, 0, 1, Usage.None, Format.A8R8G8B8,
                    Pool.Default, Filter.None, Filter.None, 0);
              }
            }
          }
          catch (Exception)
          {
            _state = State.DoesNotExist;
          }
        }
      }
      else
      {
        //        ServiceRegistration.Get<ILogger>().Debug("TEXTURE alloc from file:{0}", _sourceFileName);
        try
        {
          if (UseThumbnail)
          {
            info = ImageInformation.FromFile(_sourceFileName);
            _texture = Texture.FromFile(GraphicsDevice.Device, _sourceFileName, 0, 0, 1, Usage.None, Format.A8R8G8B8,
                Pool.Default, Filter.None, Filter.None, 0);
          }
          else
          {
            ImageInformation imgInfo = ImageInformation.FromFile(_sourceFileName);
            if (imgInfo.Width > GraphicsDevice.Width || imgInfo.Height > GraphicsDevice.Height)
            {
              using (Image imgSource = Image.FromFile(_sourceFileName))
              {
                info = Scale(imgSource, imgInfo);
              }
            }
            else
            {
              info = ImageInformation.FromFile(_sourceFileName);
              _texture = Texture.FromFile(GraphicsDevice.Device, _sourceFileName, 0, 0, 1, Usage.None, Format.A8R8G8B8,
                  Pool.Default, Filter.None, Filter.None, 0);
            }
          }
        }
        catch (Exception)
        {
          _state = State.DoesNotExist;
        }
      }
      if (_texture != null)
      {
        SurfaceDescription desc = _texture.GetLevelDescription(0);
        _width = info.Width;
        _height = info.Height;
        _maxU = info.Width / ((float) desc.Width);
        _maxV = info.Height / ((float) desc.Height);
        _allocationSize = info.Width * info.Height * 4;
        AllocationChanged(AllocationSize);
      }
    }

    private void _webClient_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
    {
      if (e.Error != null)
      {
        ServiceRegistration.Get<ILogger>().Error("Contentmanager: Failed to download {0} - {1}", _textureName, e.Error.Message);
        _webClient.Dispose();
        _webClient = null;
        _state = State.DoesNotExist;
        return;
      }
      //Trace.WriteLine("downloaded " + _textureName);
      _buffer = e.Result;
      _state = State.Created;
    }

    #region IAssetCore implementation

    public bool IsAllocated
    {
      get { return (_texture != null); }
    }

    public int AllocationSize
    {
      get { return IsAllocated ? _allocationSize : 0; }
    }

    public void Free()
    {
      //      Trace.WriteLine(String.Format("  Dispose texture:{0}", _textureName));
      if (_texture != null)
      {
        lock (_texture)
        {
          //ServiceRegistration.Get<ILogger>().Debug("TEXTURE dispose from {0}", _textureName);
          AllocationChanged(-AllocationSize);
          _texture.Dispose();
          _texture = null;
        }
      }
      _state = State.Unknown;
    }

    #endregion

    ImageInformation Scale(Image imgSource, ImageInformation imgInfo)
    {
      ImageInformation info;
      Rectangle rDest = new Rectangle();
      if (imgInfo.Width >= imgInfo.Height)
      {
        float ar = imgInfo.Height / (float) imgInfo.Width;
        rDest.Width = GraphicsDevice.Width;
        rDest.Height = (int) (GraphicsDevice.Width * ar);
      }
      else
      {
        float ar = imgInfo.Width / (float) imgInfo.Height;
        rDest.Height = GraphicsDevice.Height;
        rDest.Width = (int) (GraphicsDevice.Height * ar);
      }
      using (Bitmap bmPhoto = new Bitmap(rDest.Width, rDest.Height, PixelFormat.Format24bppRgb))
      {
        using (Graphics grPhoto = Graphics.FromImage(bmPhoto))
        {
          grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
          grPhoto.DrawImage(imgSource,
              new Rectangle(0, 0, rDest.Width, rDest.Height),
              new Rectangle(0, 0, imgSource.Width, imgSource.Height),
              GraphicsUnit.Pixel);
        }

        using (MemoryStream stream = new MemoryStream())
        {
          bmPhoto.Save(stream, ImageFormat.Bmp);
          stream.Seek(0, SeekOrigin.Begin);
          info = ImageInformation.FromStream(stream);
          stream.Seek(0, SeekOrigin.Begin);
          _texture = Texture.FromStream(GraphicsDevice.Device, stream, 0, 0, 1, Usage.None, Format.A8R8G8B8,
              Pool.Default, Filter.None, Filter.None, 0);
        }
      }
      return info;
    }
  }
}