using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Media;
using Android.Content.Res;

namespace GlVideo {
	[Activity(Label = "GlVideo", MainLauncher = true)]
	public class MainActivity : Activity, Android.Views.TextureView.ISurfaceTextureListener {
		private static readonly string LOG_TAG = "SurfaceTest";

		private TextureView surface;
		private MediaPlayer player;
		private VideoTextureRenderer renderer;

		private int surfaceWidth;
		private int surfaceHeight;

		protected override void OnCreate(Bundle bundle) {
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.main);

			surface = FindViewById<TextureView>(Resource.Id.surface);
			surface.SurfaceTextureListener = this;
		}

		protected override void OnResume() {
			base.OnResume();
			if (surface.IsAvailable)
				StartPlaying();
		}

		protected override void OnPause() {
			base.OnPause();
			if (player != null)
				player.Release();
			if (renderer != null)
				renderer.OnPause();
		}

		private void StartPlaying() {
			renderer = new VideoTextureRenderer(this, surface.SurfaceTexture, surfaceWidth, surfaceHeight);
			player = new MediaPlayer();

			try {
				AssetFileDescriptor afd = Assets.OpenFd("big_buck_bunny.mp4");
				player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
				player.SetSurface(new Surface(renderer.GetVideoTexture()));
				player.Looping = true;
				player.Prepare();
				renderer.SetVideoSize(player.VideoWidth, player.VideoHeight);
				player.Start();

			} catch (Java.IO.IOException e) {
				throw new Java.Lang.RuntimeException("Could not open input video!");
			}
		}

		public void OnSurfaceTextureAvailable(Android.Graphics.SurfaceTexture surface, int width, int height) {
			surfaceWidth = width;
			surfaceHeight = height;
			StartPlaying();
		}

		public bool OnSurfaceTextureDestroyed(Android.Graphics.SurfaceTexture surface) {
			return false;
		}

		public void OnSurfaceTextureSizeChanged(Android.Graphics.SurfaceTexture surface, int width, int height) {
			
		}

		public void OnSurfaceTextureUpdated(Android.Graphics.SurfaceTexture surface) {
			
		}
	}
}


