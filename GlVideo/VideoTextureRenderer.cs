using System;
using Android.Content;
using Java.Nio;
using Android.Graphics;
using Android.Opengl;
using Android.Util;

namespace GlVideo {
	public class VideoTextureRenderer : TextureSurfaceRenderer, Android.Graphics.SurfaceTexture.IOnFrameAvailableListener{
		private static readonly string vertexShaderCode =
			"attribute vec4 vPosition;" +
			"attribute vec4 vTexCoordinate;" +
			"uniform mat4 textureTransform;" +
			"varying vec2 v_TexCoordinate;" +
			"void main() {" +
			"   v_TexCoordinate = (textureTransform * vTexCoordinate).xy;" +
			"   gl_Position = vPosition;" +
			"}";

		private static readonly string fragmentShaderCode =
			"#extension GL_OES_EGL_image_external : require\n" +
			"precision mediump float;" +
			"uniform samplerExternalOES texture;" +
			"varying vec2 v_TexCoordinate;" +
			"void main () {" +
			"    vec4 color = texture2D(texture, v_TexCoordinate);" +
			"    gl_FragColor = color;" +
			"}";


		private static float squareSize = 1.0f;
		private static float[] squareCoords = { -squareSize,  squareSize, 0.0f,   // top left
			-squareSize, -squareSize, 0.0f,   // bottom left
			squareSize, -squareSize, 0.0f,   // bottom right
			squareSize,  squareSize, 0.0f }; // top right

		private static short[] drawOrder = { 0, 1, 2, 0, 2, 3};

		private Context ctx;

		// Texture to be shown in backgrund
		private FloatBuffer textureBuffer;
		private float[] textureCoords = { 0.0f, 1.0f, 0.0f, 1.0f,
			0.0f, 0.0f, 0.0f, 1.0f,
			1.0f, 0.0f, 0.0f, 1.0f,
			1.0f, 1.0f, 0.0f, 1.0f };
		private int[] textures = new int[1];

		private int vertexShaderHandle;
		private int fragmentShaderHandle;
		private int shaderProgram;
		private FloatBuffer vertexBuffer;
		private ShortBuffer drawListBuffer;

		private SurfaceTexture videoTexture;
		private float[] videoTextureTransform;
		private bool frameAvailable = false;

		private int videoWidth;
		private int videoHeight;
		private bool adjustViewport = false;

		public VideoTextureRenderer(Context context, SurfaceTexture texture, int width, int height) :base(texture, width, height){
			this.ctx = context;
			videoTextureTransform = new float[16];
		}

		private void LoadShaders()
		{
			vertexShaderHandle = GLES20.GlCreateShader(GLES20.GlVertexShader);
			GLES20.GlShaderSource(vertexShaderHandle, vertexShaderCode);
			GLES20.GlCompileShader(vertexShaderHandle);
			CheckGlError("Vertex shader compile");

			fragmentShaderHandle = GLES20.GlCreateShader(GLES20.GlFragmentShader);
			GLES20.GlShaderSource(fragmentShaderHandle, fragmentShaderCode);
			GLES20.GlCompileShader(fragmentShaderHandle);
			CheckGlError("Pixel shader compile");

			shaderProgram = GLES20.GlCreateProgram();
			GLES20.GlAttachShader(shaderProgram, vertexShaderHandle);
			GLES20.GlAttachShader(shaderProgram, fragmentShaderHandle);
			GLES20.GlLinkProgram(shaderProgram);
			CheckGlError("Shader program compile");

			int[] status = new int[1];
			GLES20.GlGetProgramiv(shaderProgram, GLES20.GlLinkStatus, status, 0);
			if (status[0] != GLES20.GlTrue) {
				String error = GLES20.GlGetProgramInfoLog(shaderProgram);
				Log.Error("SurfaceTest", "Error while linking program:\n" + error);
			}

		}
		private void SetupVertexBuffer()
		{
			// Draw list buffer
			ByteBuffer dlb = ByteBuffer.AllocateDirect(drawOrder.Length * 2);
			dlb.Order(ByteOrder.NativeOrder());
			drawListBuffer = dlb.AsShortBuffer();
			drawListBuffer.Put(drawOrder);
			drawListBuffer.Position(0);

			// Initialize the texture holder
			ByteBuffer bb = ByteBuffer.AllocateDirect(squareCoords.Length * 4);
			bb.Order(ByteOrder.NativeOrder());

			vertexBuffer = bb.AsFloatBuffer();
			vertexBuffer.Put(squareCoords);
			vertexBuffer.Position(0);
		}
		private void SetupTexture(Context context)
		{
			ByteBuffer texturebb = ByteBuffer.AllocateDirect(textureCoords.Length * 4);
			texturebb.Order(ByteOrder.NativeOrder());

			textureBuffer = texturebb.AsFloatBuffer();
			textureBuffer.Put(textureCoords);
			textureBuffer.Position(0);

			// Generate the actual texture
			GLES20.GlActiveTexture(GLES20.GlTexture0);
			GLES20.GlGenTextures(1, textures, 0);
			CheckGlError("Texture generate");

			GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, textures[0]);
			CheckGlError("Texture bind");

			videoTexture = new SurfaceTexture(textures[0]);
			videoTexture.SetOnFrameAvailableListener(this);
		}
		object lck=new object();
		protected override bool Draw()
		{
			lock(lck)
			{
				if (frameAvailable)
				{
					videoTexture.UpdateTexImage();
					videoTexture.GetTransformMatrix(videoTextureTransform);
					frameAvailable = false;
				}
				else
				{
					return false;
				}

			}

			if (adjustViewport)
				AdjustViewport();

			GLES20.GlClearColor(1.0f, 0.0f, 0.0f, 0.0f);
			GLES20.GlClear(GLES20.GlColorBufferBit);

			// Draw texture
			GLES20.GlUseProgram(shaderProgram);
			int textureParamHandle = GLES20.GlGetUniformLocation(shaderProgram, "texture");
			int textureCoordinateHandle = GLES20.GlGetAttribLocation(shaderProgram, "vTexCoordinate");
			int positionHandle = GLES20.GlGetAttribLocation(shaderProgram, "vPosition");
			int textureTranformHandle = GLES20.GlGetUniformLocation(shaderProgram, "textureTransform");

			GLES20.GlEnableVertexAttribArray(positionHandle);
			GLES20.GlVertexAttribPointer(positionHandle, 3, GLES20.GlFloat, false, 4 * 3, vertexBuffer);

			GLES20.GlBindTexture(GLES20.GlTexture0, textures[0]);
			GLES20.GlActiveTexture(GLES20.GlTexture0);
			GLES20.GlUniform1i(textureParamHandle, 0);

			GLES20.GlEnableVertexAttribArray(textureCoordinateHandle);
			GLES20.GlVertexAttribPointer(textureCoordinateHandle, 4, GLES20.GlFloat, false, 0, textureBuffer);

			GLES20.GlUniformMatrix4fv(textureTranformHandle, 1, false, videoTextureTransform, 0);

			GLES20.GlDrawElements(GLES20.GlTriangles, drawOrder.Length, GLES20.GlUnsignedShort, drawListBuffer);
			GLES20.GlDisableVertexAttribArray(positionHandle);
			GLES20.GlDisableVertexAttribArray(textureCoordinateHandle);

			return true;
		}
		private void AdjustViewport()
		{
			float surfaceAspect = height / (float)width;
			float videoAspect = videoHeight / (float)videoWidth;

			if (surfaceAspect > videoAspect)
			{
				float heightRatio = height / (float)videoHeight;
				int newWidth = (int)(width * heightRatio);
				int xOffset = (newWidth - width) / 2;
				GLES20.GlViewport(-xOffset, 0, newWidth, height);
			}
			else
			{
				float widthRatio = width / (float)videoWidth;
				int newHeight = (int)(height * widthRatio);
				int yOffset = (newHeight - height) / 2;
				GLES20.GlViewport(0, -yOffset, width, newHeight);
			}

			adjustViewport = false;
		}

		protected override void InitGLComponents() {
			SetupVertexBuffer();
			SetupTexture(ctx);
			LoadShaders();
		}

		protected override void DeinitGLComponents() {
			GLES20.GlDeleteTextures(1, textures, 0);
			GLES20.GlDeleteProgram(shaderProgram);
			videoTexture.Release();
			videoTexture.SetOnFrameAvailableListener(null);
		}


		public void SetVideoSize(int width, int height)
		{
			this.videoWidth = width;
			this.videoHeight = height;
			adjustViewport = true;
		}

		public void CheckGlError(String op)
		{
			int error;
			while ((error = GLES20.GlGetError()) != GLES20.GlNoError) {
				Log.Error("SurfaceTest", op + ": glError " + GLUtils.GetEGLErrorString(error));
			}
		}

		public SurfaceTexture GetVideoTexture()
		{
			return videoTexture;
		}
		public void OnFrameAvailable(SurfaceTexture surfaceTexture)
		{
			lock (lck)
			{
				frameAvailable = true;
			}
		}
	}	
}

