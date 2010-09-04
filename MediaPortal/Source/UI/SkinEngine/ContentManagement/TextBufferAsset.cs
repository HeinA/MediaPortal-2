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
using System.Collections.Generic;
using System.Linq;
using SlimDX;
using SlimDX.Direct3D9;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.SkinManagement;
using MediaPortal.UI.SkinEngine.Effects;
using Font=MediaPortal.UI.SkinEngine.Fonts.Font;

namespace MediaPortal.UI.SkinEngine.ContentManagement
{
  public enum TextScrollMode
  {
    /// <summary>
    /// Determine scroll direction based on text size, wrapping and available space.
    /// </summary>
    Auto,

    /// <summary>
    /// No scrolling.
    /// </summary>
    None,

    /// <summary>
    /// Force scrolling text to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Force scrolling text to the right.
    /// </summary>
    Right,

    /// <summary>
    /// Force scrolling text to the top.
    /// </summary>
    Up,

    /// <summary>
    /// Force scrolling text to the bottom.
    /// </summary>
    Down
  };

  public class TextBufferAsset : IAsset
  {
    #region Consts

    protected const string EFFECT_FONT = "font";

    protected const string PARAM_SCROLL_POSITION = "g_scrollpos";
    protected const string PARAM_TEXT_RECT = "g_textbox";
    protected const string PARAM_COLOR = "g_color";
    protected const string PARAM_ALIGNMENT = "g_alignment";

    #endregion

    #region Protected fields

    // Immutable properties
    protected readonly Font _font;
    protected readonly float _fontSize;
    // State
    protected string _text;
    protected bool _textChanged;
    protected SizeF _lastTextSize;
    protected float _lastTextBoxWidth;
    protected bool _lastWrap;
    protected bool _kerning;
    protected int[] _textLines;
    // Rendering
    EffectAsset _effect;
    // Scrolling
    protected Vector2 _scrollPos;
    protected DateTime _lastTimeUsed;
    // Vertex buffer
    protected VertexBuffer _vertexBuffer;
    protected int _vertexBufferLength;
    protected int _primitiveCount;
    
    #endregion

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBufferAsset"/> class.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size (may be slightly different to the size of <paramref name="font"/>).</param>
    public TextBufferAsset(Font font, float size)
    {
      _font = font;
      _fontSize = size;
      _kerning = true;
      _lastTimeUsed = DateTime.MinValue;
      _lastTextSize = new SizeF();
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Gets the font.
    /// </summary>
    public Font Font
    {
      get { return _font; }
    }

    /// <summary>
    /// Gets the font size. This may be slightly different from the size stored in Font.
    /// </summary>
    public float FontSize
    {
      get { return _fontSize; }
    }

    /// <summary>
    /// Gets or sets the text to be rendered.
    /// </summary>
    public string Text
    {
      get { return _text; }
      set
      {
        if (_text == value)
          return;
        _textChanged = true;
        _scrollPos = new Vector2(0.0f, 0.0f);
        _text = value;
      }
    }

    /// <summary>
    /// Gets the current kerning setting.
    /// </summary>
    public bool Kerning
    {
      get { return _kerning; }
    }

    /// <summary>
    /// Gets the size of a string as if it was rendered by this asset.
    /// </summary>
    /// <param name="text">String to evaluate</param>
    /// <returns>The width of the text in graphics device units (pixels)</returns>
    public float TextWidth(string text)
    {
      return _font.TextWidth(text, _fontSize, _kerning);
    }

    /// <summary>
    /// Gets the height of text rendered with this asset.
    /// </summary>
    public float LineHeight
    {
      get { return _font.LineHeight(_fontSize); }
    }

    #endregion

    public void Allocate(float boxWidth, bool wrap)
    {
      if (String.IsNullOrEmpty(_text))
      {
        Free(true);
        return;
      }

      // Get text quads
      string[] lines = wrap ? WrapText(boxWidth) : _text.Split(Environment.NewLine.ToCharArray());
      PositionColored2Textured[] verts = _font.CreateText(lines, _fontSize, true, out _lastTextSize, out _textLines);
      int count = verts.Length;

      // Re-use existing buffer if possible
      if (_vertexBuffer != null && count > _vertexBufferLength)
        Free(true);
      if (_vertexBuffer == null)
      {
        _vertexBufferLength = Math.Min(count * 2, Math.Max(4096, count));
        _vertexBuffer = PositionColored2Textured.Create(_vertexBufferLength);
        ContentManager.VertexReferences++;
      }
      PositionColored2Textured.Set(_vertexBuffer, verts);
      _primitiveCount = count / 3;
      
      // Preserve state
      _lastTextBoxWidth = boxWidth;
      _lastWrap = wrap;
      _textChanged = false;

      _effect = ContentManager.GetEffect(EFFECT_FONT);
    }

    /// <summary>
    /// Wraps the text of this label to the specified <paramref name="maxWidth"/> and returns the wrapped
    /// text parts. Wrapping is performed on a word-by-word basis.
    /// </summary>
    /// <param name="maxWidth">Maximum available width before the text should be wrapped.</param>
    /// <returns>An array of strings holding the wrapped text lines.</returns>
    public string[] WrapText(float maxWidth)
    {
      if (string.IsNullOrEmpty(_text))
        return new string[0];

      IList<string> result = new List<string>();
      foreach (string para in _text.Split(Environment.NewLine.ToCharArray()))
      {
        int paraLength = para.Length;
        int nextIndex = 0;
        float lineWidth = 0;
        int lineIndex = 0;
        // Split paragraphs into lines that will fit into maxWidth
        while (nextIndex < paraLength)
        {
          int sectionIndex = nextIndex;
          // Iterate over leading spaces
          while (nextIndex < paraLength && char.IsWhiteSpace(para[nextIndex]))
            ++nextIndex;
          if (nextIndex < paraLength)
          {
            // Find length of next word
            int wordIndex = nextIndex;
            while (nextIndex < paraLength && !char.IsWhiteSpace(para[nextIndex]))
              ++nextIndex;
            // Does the word fit into the space?
            float cx = _font.TextWidth(para.Substring(sectionIndex, nextIndex - sectionIndex), _fontSize, Kerning);
            if (lineWidth + cx > maxWidth)
            {
              // Start new line						
              if (sectionIndex != lineIndex)
                result.Add(para.Substring(lineIndex, sectionIndex - lineIndex));
              lineIndex = wordIndex;
              lineWidth = _font.TextWidth(para.Substring(wordIndex, nextIndex - wordIndex), _fontSize, Kerning);
            }
            if (nextIndex >= paraLength)
            {
              // End of paragraphs
              result.Add(para.Substring(lineIndex, nextIndex - lineIndex));
              lineIndex = nextIndex;
            }
            lineWidth += cx;
          }
        }
        // If no words found add an empty line to preserve text formatting
        if (lineIndex == 0)
          result.Add("");
      }
      return result.ToArray();
    }

    /// <summary>
    /// Draws this text.
    /// </summary>
    /// <param name="textBox">The text box.</param>
    /// <param name="alignment">The alignment.</param>
    /// <param name="color">The color.</param>
    /// <param name="wrap">If <c>true</c> then text will be word-wrapped to fit the <paramref name="textBox"/>.</param>
    /// <param name="zOrder">A value indicating the depth (and thus position in the visual heirachy) that this element should be rendered at.</param>
    /// <param name="scrollMode">Text scrolling behaviour.</param>
    /// <param name="scrollSpeed">Text scrolling speed in units (pixels at original skin size) per second.</param>
    /// <param name="finalTransform">The final combined layout-/render-transform.</param>
    public void Render(RectangleF textBox, Font.Align alignment, Color4 color, bool wrap, float zOrder,
        TextScrollMode scrollMode, float scrollSpeed, Matrix finalTransform)
    {
      if (!IsAllocated || wrap != _lastWrap || _textChanged || (wrap && textBox.Width != _lastTextBoxWidth))
      {
        Allocate(textBox.Width, wrap);
        if (!IsAllocated)
          return;
      }

      // Prepare alignment info for shader. X is position offset, Y is multiplyer for line width.
      Vector4 alignParam;
      switch (alignment)
      {
        case Font.Align.Center:
          alignParam = new Vector4(textBox.Width / 2.0f, -0.5f, zOrder, 1.0f);
          break;
        case Font.Align.Right:
          alignParam = new Vector4(textBox.Width, -1.0f, zOrder, 1.0f);
          break;
        //case Font.Align.Left:
        default:
          alignParam = new Vector4(0.0f, 0.0f, zOrder, 1.0f);
          break;
      }

      if (scrollMode != TextScrollMode.None && _lastTimeUsed != DateTime.MinValue)
        UpdateScrollPosition(textBox, scrollMode, scrollSpeed);       

      _effect.Parameters[PARAM_COLOR] = color;
      _effect.Parameters[PARAM_ALIGNMENT] = alignParam;
      _effect.Parameters[PARAM_SCROLL_POSITION] = new Vector4(_scrollPos.X, _scrollPos.Y, 0.0f, 0.0f);
      _effect.Parameters[PARAM_TEXT_RECT] = new Vector4(textBox.Left, textBox.Top, textBox.Width, textBox.Height);

      // Render
      _effect.StartRender(_font.Texture, finalTransform);
      GraphicsDevice.Device.VertexFormat = PositionColored2Textured.Format;
      GraphicsDevice.Device.SetStreamSource(0, _vertexBuffer, 0, PositionColored2Textured.StrideSize);
      GraphicsDevice.Device.DrawPrimitives(PrimitiveType.TriangleList, 0, _primitiveCount);
      _effect.EndRender();
      
      _lastTimeUsed = SkinContext.FrameRenderingStartTime;
    }

    protected void UpdateScrollPosition(RectangleF textBox, TextScrollMode mode, float speed)
    {
      float dif = speed * (float) SkinContext.FrameRenderingStartTime.Subtract(_lastTimeUsed).TotalSeconds;

      if (mode == TextScrollMode.Auto)
      {
        if (_lastWrap && _lastTextSize.Height > textBox.Height)
          mode = TextScrollMode.Up;
        else if (_textLines.Length == 1 && _lastTextSize.Width > textBox.Width)
          mode = TextScrollMode.Left;
        else
          return;
      }

      switch (mode)
      {
        case TextScrollMode.Left:
          if (_scrollPos.X < -_lastTextSize.Width)
            _scrollPos.X =  textBox.Width + 4;
          _scrollPos.X -= dif;
          break;
        case TextScrollMode.Right:
          if (_scrollPos.X > textBox.Width)
            _scrollPos.X = -_lastTextSize.Width - 4;
          _scrollPos.X += dif;
          break;
        case TextScrollMode.Down:
          if (_scrollPos.Y > textBox.Width)
            _scrollPos.Y = -_lastTextSize.Width - 4;
          _scrollPos.Y += dif;
          break;        
        //case TextScrollMode.Up:
        default:
          if (_scrollPos.Y < -_lastTextSize.Height)
            _scrollPos.Y = textBox.Height + 4;
          _scrollPos.Y -= dif;
          break;
      }
    }

    #region IAsset Members

    public bool IsAllocated
    {
      get { return (_vertexBuffer != null); }
    }

    public bool CanBeDeleted
    {
      get
      {
        if (!IsAllocated)
          return false;
        TimeSpan ts = SkinContext.FrameRenderingStartTime - _lastTimeUsed;
        if (ts.TotalSeconds >= 5)
          return true;
        return false;
      }
    }

    public void Free(bool force)
    {
      if (_vertexBuffer == null)
        return;
      _vertexBuffer.Dispose();
      _vertexBuffer = null;
      ContentManager.VertexReferences--;
    }

    public override string ToString()
    {
      return Text;
    }
    #endregion
  }
}