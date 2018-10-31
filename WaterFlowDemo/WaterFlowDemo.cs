using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace WaterFlowDemo
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class WaterFlowDemo : Microsoft.Xna.Framework.Game
    {
        #region Fields

        private GraphicsDeviceManager mGraphics;
        private SpriteBatch mSpriteBatch;
        private SpriteFont mSpriteFont;

        private Camera mCamera;

        private Water mWaterMesh;

        private float mMouseScale;
        private float mCameraVelocity;

        private KeyboardState mPrevKBState;

        private GameTime mGameTime;

        private int mWaterTechnique;

        private RasterizerState rsCullNone;

        #endregion

        public WaterFlowDemo()
        {
            mGraphics = new GraphicsDeviceManager( this );
            mGraphics.PreferredBackBufferWidth = 1024;
            mGraphics.PreferredBackBufferHeight = 768;
            mGraphics.PreferMultiSampling = true;
            mGraphics.SynchronizeWithVerticalRetrace = true;
            mGraphics.IsFullScreen = false;

            this.IsFixedTimeStep = false;
            this.IsMouseVisible = false;

            Content.RootDirectory = "Content";

            mMouseScale = .005f;
            mCameraVelocity = 50.0f;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            rsCullNone = new RasterizerState();
            rsCullNone.CullMode = CullMode.None;

            //Setup the camera used to render with
            mCamera = new Camera();
            mCamera.LookAt( new Vector3( 23.0f, 6.5f, -41.4f ), new Vector3( 11.0f, 6.7f, -32.0f ) );
            float aspect = (float)mGraphics.PreferredBackBufferWidth / (float)mGraphics.PreferredBackBufferHeight;
            mCamera.SetLens( MathHelper.ToRadians( 90.0f ), aspect, .1f, 1000.0f );
            mCamera.BuildView();

            //load the environment (i.e. the sky sphere)
            Mesh mesh = new Environment( this );
            mesh.MeshAsset = "Models/sphere";
            mesh.Scale = 500.0f;
            mesh.EffectAsset = "Shaders/EnvironmentMap";
            mesh.EnvironmentTextureAsset = "Textures/grassCUBE";

            Components.Add( mesh );

            //load the column scene
            mesh = new Mesh( this );
            mesh.MeshAsset = "Models/basiccolumnscene";
            mesh.SpecularLighting = false;
            mesh.Position = new Vector3( 0, 0, 0 );

            Components.Add( mesh );

            //load the centerpiece mesh
            mesh = new Mesh( this );
            mesh.MeshAsset = "Models/dragon";
            mesh.EffectAsset = "Shaders/Phong";
            mesh.TextureAsset = "Textures/whitetex";
            mesh.Lights = createLights();
            mesh.Scale = 55.5f;
            mesh.Position = new Vector3( 0.0f, 3.0f, 0.0f );

            Components.Add( mesh );

            //fill out the water options struct
            //note: width and height could potentially be only 1 cell wide/deep
            WaterOptions options = new WaterOptions();
            options.Width = 65;
            options.Height = 65;
            options.CellSpacing = 1.75f;
            options.FlowMapAsset = "Textures/flowmap";
            options.NoiseMapAsset = "Textures/noise";
            options.WaveMapAsset0 = "Textures/wave0";
            options.WaveMapAsset1 = "Textures/wave1";
            options.WaveMapScale = 2.5f;
            options.WaterColor = new Vector4( 0.5f, 0.79f, 0.75f, 1.0f );
            options.SunColor = new Vector4( 1.0f, 0.8f, 0.4f, 1.0f );
            options.SunDirection = new Vector3( 2.6f, -1.0f, -1.5f );
            options.SunFactor = 1.5f;
            options.SunPower = 100.0f;

            //create the water object and assign it a delegate function that will render the scene objects
            mWaterMesh = new Water( this );
            mWaterMesh.Options = options;
            mWaterMesh.EffectAsset = "Shaders/Water";
            mWaterMesh.World = Matrix.CreateTranslation( Vector3.UnitY * 2.5f );
            mWaterMesh.RenderObjects = DrawObjects;

            // Components.Add( mWaterMesh );

            base.Initialize();

            Mouse.SetPosition( Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2 );
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            // Create a new SpriteBatch, which can be used to draw textures.
            mSpriteBatch = new SpriteBatch( GraphicsDevice );

            //load the sprie font
            mSpriteFont = Content.Load<SpriteFont>( "Font" );
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            Content.Unload();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update( GameTime gameTime )
        {
            mGameTime = gameTime;
            float timeDelta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            updateInput( timeDelta );

            base.Update( gameTime );
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw( GameTime gameTime )
        {
            foreach ( DrawableGameComponent gc in Components )
            {
                if ( !( gc is Mesh ) )
                    continue;

                ( (Mesh)gc ).View = mCamera.View;
                ( (Mesh)gc ).Projection = mCamera.Projection;
            }

            // TODO:
            // mWaterMesh.SetCamera( mCamera.ViewProj, mCamera.Position );
            // mWaterMesh.UpdateWaterMaps( gameTime );

            GraphicsDevice.Clear( ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 1 );
            GraphicsDevice.RasterizerState = rsCullNone;

            base.Draw( gameTime );

            //mSpriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            //mSpriteBatch.Draw(mWaterMesh.ReflectionMap.GetTexture(), new Rectangle(0, 0, 128, 128), Color.White);
            //mSpriteBatch.Draw(mWaterMesh.RefractionMap.GetTexture(), new Rectangle(130, 0, 128, 128), Color.White);
            //mSpriteBatch.End();
        }

        /// <summary>
        /// Draws the objects in the Components list, excluding the water component.
        /// Combines the reflection matrix with the object's world matrix to flip the model.
        /// For the refraction pass, the reflMatrix will just be the Identity Matrix
        /// </summary>
        /// <param name="reflMatrix"></param>
        private void DrawObjects( Matrix reflMatrix )
        {
            Mesh model;
            foreach ( DrawableGameComponent mesh in Components )
            {
                if ( !( mesh is Mesh ) )
                    continue;

                model = mesh as Mesh;

                //save the old matrix
                Matrix oldWorld = model.World;

                //combine the old matrix with the refleciton matrix
                model.World = oldWorld * reflMatrix;

                model.Draw( mGameTime );

                //restore the old matrix for regular rendering
                model.World = oldWorld;
            }
        }

        private void updateInput( float timeDelta )
        {
            KeyboardState keyState = Keyboard.GetState();

            if ( GamePad.GetState( PlayerIndex.One ).Buttons.Back == ButtonState.Pressed )
                this.Exit();
            else if ( keyState.IsKeyDown( Keys.Escape ) )
                this.Exit();

            //update the current camera
            if ( keyState.IsKeyDown( Keys.W ) )
                mCamera.Walk( -mCameraVelocity * timeDelta );
            if ( keyState.IsKeyDown( Keys.S ) )
                mCamera.Walk( mCameraVelocity * timeDelta );
            if ( keyState.IsKeyDown( Keys.A ) )
                mCamera.Strafe( -mCameraVelocity * timeDelta );
            if ( keyState.IsKeyDown( Keys.D ) )
                mCamera.Strafe( mCameraVelocity * timeDelta );

            if ( keyState.IsKeyDown( Keys.D1 ) )
                mWaterTechnique = 0;
            else if ( keyState.IsKeyDown( Keys.D2 ) )
                mWaterTechnique = 1;
            else if ( keyState.IsKeyDown( Keys.D3 ) )
                mWaterTechnique = 2;

            // TODO:
            // mWaterMesh.Effect.CurrentTechnique = mWaterMesh.Effect.Techniques[ mWaterTechnique ];

            mCamera.UpdateMouse( Mouse.GetState(), mMouseScale );
            mCamera.BuildView();

            mPrevKBState = keyState;
        }

        /// <summary>
        /// Updates the reflection and refraction maps for the water plane
        /// </summary>


        /// <summary>
        /// Helper function to create the lights used for Phong shading
        /// </summary>
        /// <returns></returns>
        private List<Light> createLights()
        {
            List<Light> lights = new List<Light>( 3 );

            Light light;
            light.AmbientColor = new Vector4( .15f, .15f, .15f, 1.0f );
            light.DiffuseColor = new Vector4( 1.0f, 0.3f, 0.3f, 1.0f );
            light.Direction = new Vector3( 1, -1, -1 );
            light.Direction.Normalize();

            lights.Add( light );

            light.DiffuseColor = new Vector4( 0.15f, 0.15f, 0.5f, 1.0f );
            light.Direction = new Vector3( 0, 1, -1 );
            light.Direction.Normalize();

            lights.Add( light );

            light.DiffuseColor = new Vector4( 0.15f, 0.5f, 0.15f, 1.0f );
            light.Direction = new Vector3( -1, -1, 1 );
            light.Direction.Normalize();

            lights.Add( light );

            return lights;
        }
    }
}
