﻿#region License
/*
Microsoft Public License (Ms-PL)
MonoGame - Copyright © 2009-2012 The MonoGame Team

All rights reserved.

This license governs use of the accompanying software. If you use the software,
you accept this license. If you do not accept the license, do not use the
software.

1. Definitions

The terms "reproduce," "reproduction," "derivative works," and "distribution"
have the same meaning here as under U.S. copyright law.

A "contribution" is the original software, or any additions or changes to the
software.

A "contributor" is any person that distributes its contribution under this
license.

"Licensed patents" are a contributor's patent claims that read directly on its
contribution.

2. Grant of Rights

(A) Copyright Grant- Subject to the terms of this license, including the
license conditions and limitations in section 3, each contributor grants you a
non-exclusive, worldwide, royalty-free copyright license to reproduce its
contribution, prepare derivative works of its contribution, and distribute its
contribution or any derivative works that you create.

(B) Patent Grant- Subject to the terms of this license, including the license
conditions and limitations in section 3, each contributor grants you a
non-exclusive, worldwide, royalty-free license under its licensed patents to
make, have made, use, sell, offer for sale, import, and/or otherwise dispose of
its contribution in the software or derivative works of the contribution in the
software.

3. Conditions and Limitations

(A) No Trademark License- This license does not grant you rights to use any
contributors' name, logo, or trademarks.

(B) If you bring a patent claim against any contributor over patents that you
claim are infringed by the software, your patent license from such contributor
to the software ends automatically.

(C) If you distribute any portion of the software, you must retain all
copyright, patent, trademark, and attribution notices that are present in the
software.

(D) If you distribute any portion of the software in source code form, you may
do so only under this license by including a complete copy of this license with
your distribution. If you distribute any portion of the software in compiled or
object code form, you may only do so under a license that complies with this
license.

(E) The software is licensed "as-is." You bear the risk of using it. The
contributors give no express warranties, guarantees or conditions. You may have
additional consumer rights under your local laws which this license cannot
change. To the extent permitted under your local laws, the contributors exclude
the implied warranties of merchantability, fitness for a particular purpose and
non-infringement.
*/
using Microsoft.Xna.Framework.Input;


#endregion License

using System;
using System.Drawing;
using System.Text;

using System.Collections.Generic;
using System.Linq;
using CoreAnimation;
using Foundation;
using ObjCRuntime;
using OpenGLES;
using UIKit;
using CoreGraphics;

using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using OpenTK.Platform.iPhoneOS;

using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework {

    public class KeyboardEntryEventArgs : EventArgs 
    {
        public object TextEtc;
		public int Start;
		public int Length;
		public int End;
        public KeyboardEntryEventArgs (string text)
        {
            TextEtc = text;
        }
        public KeyboardEntryEventArgs (object text)
        {
            TextEtc = text;
        }
		public KeyboardEntryEventArgs (string text, int start, int length, int end)
		{
			TextEtc = text;
			Start = start;
			Length = length;
			End = end;
		}
    }
    public enum Keypresses
    {
        backspaceDeleteKey,
		enterKey,
        whatelse
    }

	public class SelectionChangedEventArgs : EventArgs 
	{
		public NSRange Range;

		public SelectionChangedEventArgs(NSRange range)
		{
			Range = range;
		}
	}


    [Register("iOSGameView")]
	public partial class iOSGameView : UIView {
		private readonly iOSGamePlatform _platform;
		private int _colorbuffer;
		private int _depthbuffer;
		private int _framebuffer;

		public EditableCoreTextView textView;

		#region Construction/Destruction

		public iOSGameView (iOSGamePlatform platform, CGRect frame)
			: base(frame)
		{
			if (platform == null)
				throw new ArgumentNullException ("platform");
			_platform = platform;
			Initialize ();


			//***********************************

			// Create and set up our SimpleCoreTextView that will do the drawing
			textView = new EditableCoreTextView (Bounds.Inset (5, 5));
			textView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
			UserInteractionEnabled = true;
			AutosizesSubviews = true;
			AddSubview (textView);
			textView.UserInteractionEnabled = false;
		}

		private void Initialize ()
		{
			MultipleTouchEnabled = true;
			Opaque = true;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (__renderbuffergraphicsContext != null)
					DestroyContext();
			}

			base.Dispose (disposing);
			_isDisposed = true;

			//***********************


			textView = null;
		}

#endregion Construction/Destruction

		#region Properties

		private bool _isDisposed;

		public bool IsDisposed {
			get { return _isDisposed; }
		}

		#endregion Properties

		[Export ("layerClass")]
		public static Class GetLayerClass ()
		{
			return new Class (typeof (CAEAGLLayer));
		}

        public bool EnableKeyboard = false;
		public override bool CanBecomeFirstResponder {
            get { return EnableKeyboard; }
		}

		private new CAEAGLLayer Layer {
			get { return base.Layer as CAEAGLLayer; }
		}

		// FIXME: Someday, hopefully it will be possible to move
		//        GraphicsContext into an iOS-specific GraphicsDevice.
		//        Some level of cooperation with the UIView/Layer will
		//        probably always be necessary, unfortunately.
		private GraphicsContext __renderbuffergraphicsContext;
		private IOpenGLApi _glapi;
		private void CreateContext ()
		{
			AssertNotDisposed ();

            // RetainedBacking controls if the content of the colorbuffer should be preserved after being displayed
            // This is the XNA equivalent to set PreserveContent when initializing the GraphicsDevice
            // (should be false by default for better performance)
			Layer.DrawableProperties = NSDictionary.FromObjectsAndKeys (
				new NSObject [] {
					NSNumber.FromBoolean (false), 
					EAGLColorFormat.RGBA8
				},
				new NSObject [] {
					EAGLDrawableProperty.RetainedBacking,
					EAGLDrawableProperty.ColorFormat
				});

			Layer.ContentsScale = Window.Screen.Scale;

			//var strVersion = OpenTK.Graphics.ES11.GL.GetString (OpenTK.Graphics.ES11.All.Version);
			//strVersion = OpenTK.Graphics.ES20.GL.GetString (OpenTK.Graphics.ES20.All.Version);
			//var version = Version.Parse (strVersion);

			try {
				__renderbuffergraphicsContext = new GraphicsContext (null, null, 2, 0, GraphicsContextFlags.Embedded);
				_glapi = new Gles20Api ();
			} catch {
				__renderbuffergraphicsContext = new GraphicsContext (null, null, 1, 1, GraphicsContextFlags.Embedded);
				_glapi = new Gles11Api ();
			}

			__renderbuffergraphicsContext.MakeCurrent (null);
		}

		private void DestroyContext ()
		{
			AssertNotDisposed ();
			AssertValidContext ();

			__renderbuffergraphicsContext.Dispose ();
			__renderbuffergraphicsContext = null;
			_glapi = null;
		}

        [Export("doTick")]
        void DoTick()
        {
            _platform.Tick();
        }

		private void CreateFramebuffer ()
		{
			AssertNotDisposed ();
			AssertValidContext ();

			__renderbuffergraphicsContext.MakeCurrent (null);
			
			// HACK:  GraphicsDevice itself should be calling
			//        glViewport, so we shouldn't need to do it
			//        here and then force the state into
			//        GraphicsDevice.  However, that change is a
			//        ways off, yet.
            int viewportHeight = (int)Math.Round(Layer.Bounds.Size.Height * Layer.ContentsScale);
            int viewportWidth = (int)Math.Round(Layer.Bounds.Size.Width * Layer.ContentsScale);

			_glapi.GenFramebuffers (1, ref _framebuffer);
			_glapi.BindFramebuffer (All.Framebuffer, _framebuffer);

			// Create our Depth buffer. Color buffer must be the last one bound
			GL.GenRenderbuffers(1, out _depthbuffer);
			GL.BindRenderbuffer(All.Renderbuffer, _depthbuffer);
            GL.RenderbufferStorage (All.Renderbuffer, All.Depth24Stencil8Oes, viewportWidth, viewportHeight);
            GL.FramebufferRenderbuffer(All.Framebuffer, All.DepthAttachment, All.Renderbuffer, _depthbuffer);
            GL.FramebufferRenderbuffer(All.Framebuffer, All.StencilAttachment, All.Renderbuffer, _depthbuffer);

			_glapi.GenRenderbuffers(1, ref _colorbuffer);
			_glapi.BindRenderbuffer(All.Renderbuffer, _colorbuffer);

			var ctx = ((IGraphicsContextInternal) __renderbuffergraphicsContext).Implementation as iPhoneOSGraphicsContext;

			// TODO: EAGLContext.RenderBufferStorage returns false
			//       on all but the first call.  Nevertheless, it
			//       works.  Still, it would be nice to know why it
			//       claims to have failed.
			ctx.EAGLContext.RenderBufferStorage ((uint) All.Renderbuffer, Layer);
			
			_glapi.FramebufferRenderbuffer (All.Framebuffer, All.ColorAttachment0, All.Renderbuffer, _colorbuffer);
			
			var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
			if (status != FramebufferErrorCode.FramebufferComplete)
				throw new InvalidOperationException (
					"Framebuffer was not created correctly: " + status);

			_glapi.Viewport(0, 0, viewportWidth, viewportHeight);
            _glapi.Scissor(0, 0, viewportWidth, viewportHeight);

			var gds = _platform.Game.Services.GetService(
                typeof (IGraphicsDeviceService)) as IGraphicsDeviceService;

			if (gds != null && gds.GraphicsDevice != null)
			{
                var pp = gds.GraphicsDevice.PresentationParameters;
                int height = viewportHeight;
                int width = viewportWidth;

                if (this.NextResponder is iOSGameViewController)
                {
                    var displayOrientation = _platform.Game.Window.CurrentOrientation;
                    if (displayOrientation == DisplayOrientation.LandscapeLeft || displayOrientation == DisplayOrientation.LandscapeRight)
                    {
                        height = Math.Min(viewportHeight, viewportWidth);
                        width = Math.Max(viewportHeight, viewportWidth);
                    }
                    else
                    {
                        height = Math.Max(viewportHeight, viewportWidth);
                        width = Math.Min(viewportHeight, viewportWidth);
                    }
                }

                pp.BackBufferHeight = height;
                pp.BackBufferWidth = width;

				gds.GraphicsDevice.Viewport = new Viewport(
					0, 0,
					pp.BackBufferWidth,
					pp.BackBufferHeight);
				
				// FIXME: These static methods on GraphicsDevice need
				//        to go away someday.
				gds.GraphicsDevice.glFramebuffer = _framebuffer;
			}

            if (Threading.BackgroundContext == null)
                Threading.BackgroundContext = new OpenGLES.EAGLContext(ctx.EAGLContext.API, ctx.EAGLContext.ShareGroup);
		}

		private void DestroyFramebuffer ()
		{
			AssertNotDisposed ();
			AssertValidContext ();

			__renderbuffergraphicsContext.MakeCurrent (null);

			_glapi.DeleteFramebuffers (1, ref _framebuffer);
			_framebuffer = 0;

			_glapi.DeleteRenderbuffers (1, ref _colorbuffer);
			_colorbuffer = 0;
			
			_glapi.DeleteRenderbuffers (1, ref _depthbuffer);
			_depthbuffer = 0;
		}

		// FIXME: This logic belongs in GraphicsDevice.Present, not
		//        here.  If it can someday be moved there, then the
		//        normal call to Present in Game.Tick should cover
		//        this.  For now, iOSGamePlatform will call Present
		//        in the Draw/Update loop handler.
		[Obsolete("Remove iOSGameView.Present once GraphicsDevice.Present fully expresses this")]
		public void Present ()
		{
			AssertNotDisposed ();
			AssertValidContext ();

			GL.Ext.DiscardFramebuffer(All.Framebuffer, discard.Length, discard);

			__renderbuffergraphicsContext.MakeCurrent (null);
			GL.BindRenderbuffer (All.Renderbuffer, this._colorbuffer);
            __renderbuffergraphicsContext.SwapBuffers();
		}

		// FIXME: This functionality belongs iMakeCurrentn GraphicsDevice.
		[Obsolete("Move the functionality of iOSGameView.MakeCurrent into GraphicsDevice")]
		public void MakeCurrent ()
		{
			AssertNotDisposed ();
			AssertValidContext ();

			__renderbuffergraphicsContext.MakeCurrent (null);
		}

		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();

            var gds = _platform.Game.Services.GetService (
                typeof (IGraphicsDeviceService)) as IGraphicsDeviceService;

            if (gds == null || gds.GraphicsDevice == null)
                return;

			if (_framebuffer + _colorbuffer + _depthbuffer != 0)
				DestroyFramebuffer ();
			if (__renderbuffergraphicsContext == null)
				CreateContext();
			CreateFramebuffer ();
		}

		#region UIWindow Notifications

		[Export ("didMoveToWindow")]
		public virtual void DidMoveToWindow ()
		{

            if (Window != null) {
                
                if (__renderbuffergraphicsContext == null)
                    CreateContext ();
                if (_framebuffer * _colorbuffer * _depthbuffer == 0)
                    CreateFramebuffer ();
            }
		}

		#endregion UIWindow Notifications

		private void AssertNotDisposed ()
		{
			if (_isDisposed)
				throw new ObjectDisposedException (GetType ().Name);
		}

		private void AssertValidContext ()
		{
			if (__renderbuffergraphicsContext == null)
				throw new InvalidOperationException (
					"GraphicsContext must be created for this operation to succeed.");
		}



        //**********************************************
        //**********************************************


}
}
