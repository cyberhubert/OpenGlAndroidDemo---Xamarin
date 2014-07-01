using System;
using Android.Graphics;
using Javax.Microedition.Khronos.Egl;
using Java.Lang;
using Android.Util;
using Java.Interop;
using Android.Opengl;

namespace GlVideo {
	public abstract class TextureSurfaceRenderer :Java.Lang.Object, IRunnable{
		private static readonly int EGL_OPENGL_ES2_BIT = 4;
		private static readonly int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
		private static readonly string LOG_TAG = "SurfaceTest.GL";
		protected readonly SurfaceTexture texture;
		private IEGL10 egl;
		private EGLDisplay eglDisplay;
		private EGLContext eglContext;
		private EGLSurface eglSurface;

		protected int width;
		protected int height;
		private bool running;

		public TextureSurfaceRenderer(SurfaceTexture texture, int width, int height) {
			this.texture = texture;
			this.width = width;
			this.height = height;
			this.running = true;
			Thread thrd = new Thread(this);
			thrd.Start();
		}

		public void Run(){
			InitGL();
			InitGLComponents();
			Log.Debug(LOG_TAG, "OpenGL init OK.");

			while (running)
			{
				long loopStart = Java.Lang.JavaSystem.CurrentTimeMillis();
				PingFps();

				if (Draw())
				{
					egl.EglSwapBuffers(eglDisplay, eglSurface);
				}

				long waitDelta = 16 - (JavaSystem.CurrentTimeMillis() - loopStart);    // Targeting 60 fps, no need for faster
				if (waitDelta > 0)
				{
					try
					{
						Thread.Sleep(waitDelta);
					}
					catch (InterruptedException e)
					{
						continue;
					}
				}
			}

			DeinitGLComponents();
			DeinitGL();
		}
		protected abstract bool Draw();
		protected abstract void InitGLComponents();
		protected abstract void DeinitGLComponents();

		private long lastFpsOutput = 0;
		private int frames;
		private void PingFps()
		{
			if (lastFpsOutput == 0)
				lastFpsOutput = JavaSystem.CurrentTimeMillis();

			frames ++;

			if (JavaSystem.CurrentTimeMillis() - lastFpsOutput > 1000)
			{
				Log.Debug(LOG_TAG, "FPS: " + frames);
				lastFpsOutput = JavaSystem.CurrentTimeMillis();
				frames = 0;
			}
		}

		public void OnPause()
		{
			running = false;
		}
		private void InitGL()
		{
			egl = EGLContext.EGL.JavaCast<IEGL10>();
			eglDisplay = egl.EglGetDisplay(EGL10.EglDefaultDisplay);

			int[] version = new int[2];
			egl.EglInitialize(eglDisplay, version);

			EGLConfig eglConfig = ChooseEglConfig();
			eglContext = CreateContext(egl, eglDisplay, eglConfig);

			eglSurface = egl.EglCreateWindowSurface(eglDisplay, eglConfig, texture, null);

			if (eglSurface == null || eglSurface == EGL10.EglNoSurface)
			{
				throw new RuntimeException("GL Error: " + GLUtils.GetEGLErrorString(egl.EglGetError()));
			}

			if (!egl.EglMakeCurrent(eglDisplay, eglSurface, eglSurface, eglContext))
			{
				throw new RuntimeException("GL Make current error: " + GLUtils.GetEGLErrorString(egl.EglGetError()));
			}
		}

		private void DeinitGL()
		{
			egl.EglMakeCurrent(eglDisplay, EGL10.EglNoSurface, EGL10.EglNoSurface, EGL10.EglNoContext);
			egl.EglDestroySurface(eglDisplay, eglSurface);
			egl.EglDestroyContext(eglDisplay, eglContext);
			egl.EglTerminate(eglDisplay);
			Log.Debug(LOG_TAG, "OpenGL deinit OK.");
		}

		private EGLContext CreateContext(IEGL10 egl, EGLDisplay eglDisplay, EGLConfig eglConfig)
		{
			int[] attribList = { EGL_CONTEXT_CLIENT_VERSION, 2, EGL10.EglNone};
			return egl.EglCreateContext(eglDisplay, eglConfig, EGL10.EglNoContext, attribList);
		}

		private EGLConfig ChooseEglConfig()
		{
			int[] configsCount = new int[1];
			EGLConfig[] configs = new EGLConfig[1];
			int[] configSpec = GetConfig();

			if (!egl.EglChooseConfig(eglDisplay, configSpec, configs, 1, configsCount))
			{
				throw new IllegalArgumentException("Failed to choose config: " + GLUtils.GetEGLErrorString(egl.EglGetError()));
			}
			else if (configsCount[0] > 0)
			{
				return configs[0];
			}

			return null;
		}
		private int[] GetConfig()
		{
			return new int[] {
				EGL10.EglRenderableType, EGL_OPENGL_ES2_BIT,
				EGL10.EglRedSize, 8,
				EGL10.EglGreenSize, 8,
				EGL10.EglBlueSize, 8,
				EGL10.EglAlphaSize, 8,
				EGL10.EglDepthSize, 0,
				EGL10.EglStencilSize, 0,
				EGL10.EglNone
			};
		}

		protected override void JavaFinalize() {
			base.JavaFinalize();
			running = false;
		}
	}
}

