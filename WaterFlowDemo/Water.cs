using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace WaterFlowDemo
{
    //delegate that the water component calls to render the objects in the scene
    public delegate void RenderObjects( Matrix reflectionMatrix );

    /// <summary>
    /// Options that must be passed to the water component before Initialization
    /// </summary>
    public class WaterOptions
    {
        //the number of vertices in the x and z plane (must be of the form 2^n + 1)
        //and the amount of spacing between vertices
        public int Width = 257;
        public int Height = 257;
        public float CellSpacing = .5f;

        //how large to scale the wave map texture in the shader
        //higher than 1 and the texture will repeat providing finer detail normals
        public float WaveMapScale = 1.0f;

        //size of the reflection and refraction render targets' width and height
        public int RenderTargetSize = 512;

        //offsets for the flowmap updated every frame. These offsets are used to 
        //simulate the water flowing through the flow map.
        public float FlowMapOffset0;
        public float FlowMapOffset1;

        //asset names for the normal/wave maps
        public string FlowMapAsset;
        public string NoiseMapAsset;
        public string WaveMapAsset0;
        public string WaveMapAsset1;

        //water color and sun light properties
        public Vector4 WaterColor;
        public Vector4 SunColor;
        public Vector3 SunDirection;
        public float SunFactor; //the intensity of the sun specular term.
        public float SunPower;  //how shiny we want the sun specular term on the water to be.
    }

    /// <summary>
    /// Drawable game component for water rendering. Renders the scene to reflection and refraction
    /// maps that are projected onto the water plane and are distorted based on two scrolling normal
    /// maps.
    /// </summary>
    public class Water : DrawableGameComponent
    {
        #region Fields
        private const float Cycle = .15f;
        private const float HalfCycle = Cycle * .5f;
        private const float FlowSpeed = .05f;

        private RenderObjects mDrawFunc;

        //vertex and index buffers for the water plane
        private VertexBuffer mVertexBuffer;
        private IndexBuffer mIndexBuffer;
        private VertexDeclaration mDecl;

        //water shader
        private Effect mEffect;
        private string mEffectAsset;

        //camera properties
        private Vector3 mViewPos;
        private Matrix mViewProj;
        private Matrix mWorld;

        //maps to render the refraction/reflection to
        private RenderTarget2D mRefractionMap;
        private RenderTarget2D mReflectionMap;

        //scrolling normal maps that we will use as a
        //a normal for the water plane in the shader
        private Texture2D mFlowMap;
        private Texture2D mNoiseMap;
        private Texture2D mWaveMap0;
        private Texture2D mWaveMap1;

        //user specified options to configure the water object
        private WaterOptions mOptions;

        //tells the water object if it needs to update the refraction
        //map itself or not. Since refraction just needs the scene drawn
        //regularly, we can:
        // --Draw the objects we want refracted
        // --Resolve the back buffer and send it to the water
        // --Skip computing the refraction map in the water object
        // This is useful if you are already drawing the scene to a render target
        // Prevents from rendering the scene objects multiple times
        private bool mGrabRefractionFromFB = false;

        private int mNumVertices;
        private int mNumTris;
        #endregion

        #region Properties

        public RenderObjects RenderObjects
        {
            set { mDrawFunc = value; }
        }

        /// <summary>
        /// Shader of the water
        /// </summary>
        public Effect Effect { get { return mEffect; } }

        /// <summary>
        /// Name of the asset for the Effect.
        /// </summary>
        public string EffectAsset
        {
            get { return mEffectAsset; }
            set { mEffectAsset = value; }
        }

        /// <summary>
        /// The render target that the refraction is rendered to.
        /// </summary>
        public RenderTarget2D RefractionMap
        {
            get { return mRefractionMap; }
            set { mRefractionMap = value; }
        }

        /// <summary>
        /// The render target that the reflection is rendered to.
        /// </summary>
        public RenderTarget2D ReflectionMap
        {
            get { return mReflectionMap; }
            set { mReflectionMap = value; }
        }

        /// <summary>
        /// Options to configure the water. Must be set before
        /// the water is initialized. Should be set immediately
        /// following the instantiation of the object.
        /// </summary>
        public WaterOptions Options
        {
            get { return mOptions; }
            set { mOptions = value; }
        }

        /// <summary>
        /// The world matrix of the water.
        /// </summary>
        public Matrix World
        {
            get { return mWorld; }
            set { mWorld = value; }
        }

        #endregion

        public Water( Game game )
            : base( game )
        {

        }

        public override void Initialize()
        {
            base.Initialize();

            //build the water mesh
            mNumVertices = mOptions.Width * mOptions.Height;
            mNumTris = ( mOptions.Width - 1 ) * ( mOptions.Height - 1 ) * 2;
            VertexPositionTexture[] vertices = new VertexPositionTexture[ mNumVertices ];

            Vector3[] verts;
            int[] indices;

            //create the water vertex grid positions and indices
            GenTriGrid( mOptions.Height, mOptions.Width, mOptions.CellSpacing, mOptions.CellSpacing,
                        Vector3.Zero, out verts, out indices );

            //copy the verts into our PositionTextured array
            for ( int i = 0; i < mOptions.Width; ++i )
            {
                for ( int j = 0; j < mOptions.Height; ++j )
                {
                    int index = i * mOptions.Width + j;
                    vertices[ index ].Position = verts[ index ];
                    vertices[ index ].TextureCoordinate = new Vector2( (float)j / mOptions.Width, (float)i / mOptions.Height );
                }
            }

            mVertexBuffer = new VertexBuffer( Game.GraphicsDevice,
                                             VertexPositionTexture.SizeInBytes * mOptions.Width * mOptions.Height,
                                             BufferUsage.WriteOnly );
            mVertexBuffer.SetData( vertices );

            mIndexBuffer = new IndexBuffer( Game.GraphicsDevice, typeof( int ), indices.Length, BufferUsage.WriteOnly );
            mIndexBuffer.SetData( indices );

            mDecl = new VertexDeclaration( Game.GraphicsDevice, VertexPositionTexture.VertexElements );

            //normalzie the sun direction in case the user didn't
            mOptions.FlowMapOffset0 = 0.0f;
            mOptions.FlowMapOffset1 = HalfCycle;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            //load the wave maps
            mWaveMap0 = Game.Content.Load<Texture2D>( mOptions.WaveMapAsset0 );
            mWaveMap1 = Game.Content.Load<Texture2D>( mOptions.WaveMapAsset1 );
            mFlowMap = Game.Content.Load<Texture2D>( mOptions.FlowMapAsset );
            mNoiseMap = Game.Content.Load<Texture2D>( mOptions.NoiseMapAsset );

            //get the attributes of the back buffer
            PresentationParameters pp = Game.GraphicsDevice.PresentationParameters;
            SurfaceFormat format = pp.BackBufferFormat;
            MultiSampleType msType = pp.MultiSampleType;
            int msQuality = pp.MultiSampleQuality;

            //create the reflection and refraction render targets
            //using the backbuffer attributes
            mRefractionMap = new RenderTarget2D( Game.GraphicsDevice, mOptions.RenderTargetSize, mOptions.RenderTargetSize,
                                                1, format, msType, msQuality );
            mReflectionMap = new RenderTarget2D( Game.GraphicsDevice, mOptions.RenderTargetSize, mOptions.RenderTargetSize,
                                                1, format, msType, msQuality );

            mEffect = Game.Content.Load<Effect>( mEffectAsset );

            //set the parameters that shouldn't change.
            //Some of these might need to change every once in awhile,
            //move them to updateEffectParams function if you need that functionality.
            if ( mEffect != null )
            {
                mEffect.Parameters[ "FlowMap" ].SetValue( mFlowMap );
                mEffect.Parameters[ "NoiseMap" ].SetValue( mNoiseMap );
                mEffect.Parameters[ "WaveMap0" ].SetValue( mWaveMap0 );
                mEffect.Parameters[ "WaveMap1" ].SetValue( mWaveMap1 );

                mEffect.Parameters[ "HalfCycle" ].SetValue( HalfCycle );
                mEffect.Parameters[ "TexScale" ].SetValue( mOptions.WaveMapScale );

                mEffect.Parameters[ "WaterColor" ].SetValue( mOptions.WaterColor );
                mEffect.Parameters[ "SunColor" ].SetValue( mOptions.SunColor );
                mEffect.Parameters[ "SunDirection" ].SetValue( Vector3.Normalize( mOptions.SunDirection ) );
                mEffect.Parameters[ "SunFactor" ].SetValue( mOptions.SunFactor );
                mEffect.Parameters[ "SunPower" ].SetValue( mOptions.SunPower );

                mEffect.Parameters[ "World" ].SetValue( mWorld );

                mEffect.CurrentTechnique = mEffect.Techniques[ "WaterTech" ];
            }
        }

        public override void Update( GameTime gameTime )
        {
            float timeDelta = (float)gameTime.ElapsedGameTime.TotalSeconds;
       
            //update the flow map offsets for both layers
            mOptions.FlowMapOffset0 += FlowSpeed * timeDelta;
            mOptions.FlowMapOffset1 += FlowSpeed * timeDelta;
            if ( mOptions.FlowMapOffset0 >= Cycle )
                mOptions.FlowMapOffset0 = 0.0f;

            if ( mOptions.FlowMapOffset1 >= Cycle )
                mOptions.FlowMapOffset1 = 0.0f;
        }

        public override void Draw( GameTime gameTime )
        {
            //don't cull back facing triangles since we want the water to be visible
            //from beneath the water plane too
            Game.GraphicsDevice.RenderState.CullMode = CullMode.None;

            UpdateEffectParams();

            Game.GraphicsDevice.Indices = mIndexBuffer;
            Game.GraphicsDevice.Vertices[ 0 ].SetSource( mVertexBuffer, 0, VertexPositionTexture.SizeInBytes );
            Game.GraphicsDevice.VertexDeclaration = mDecl;

            mEffect.Begin( SaveStateMode.None );

            foreach ( EffectPass pass in mEffect.CurrentTechnique.Passes )
            {
                pass.Begin();
                Game.GraphicsDevice.DrawIndexedPrimitives( PrimitiveType.TriangleList, 0, 0, mNumVertices, 0, mNumTris );
                pass.End();
            }

            mEffect.End();

            Game.GraphicsDevice.RenderState.CullMode = CullMode.CullCounterClockwiseFace;
        }

        /// <summary>
        /// Set the ViewProjection matrix and position of the Camera.
        /// </summary>
        /// <param name="viewProj"></param>
        /// <param name="pos"></param>
        public void SetCamera( Matrix viewProj, Vector3 pos )
        {
            mViewProj = viewProj;
            mViewPos = pos;
        }

        /// <summary>
        /// Updates the reflection and refraction maps. Called
        /// on update.
        /// </summary>
        /// <param name="gameTime"></param>
        public void UpdateWaterMaps( GameTime gameTime )
        {
            /*------------------------------------------------------------------------------------------
             * Render to the Reflection Map
             */
            //clip objects below the water line, and render the scene upside down
            GraphicsDevice.RenderState.CullMode = CullMode.CullClockwiseFace;

            GraphicsDevice.SetRenderTarget( 0, mReflectionMap );
            GraphicsDevice.Clear( ClearOptions.Target | ClearOptions.DepthBuffer, mOptions.WaterColor, 1.0f, 0 );

            //reflection plane in local space
            //the w value can be used to raise or lower the plane to hide gaps between objects and their
            //reflection on the water.
            Vector4 waterPlaneL = new Vector4( 0.0f, -1.0f, 0.0f, 0.0f );

            Matrix wInvTrans = Matrix.Invert( mWorld );
            wInvTrans = Matrix.Transpose( wInvTrans );

            //reflection plane in world space
            Vector4 waterPlaneW = Vector4.Transform( waterPlaneL, wInvTrans );

            Matrix wvpInvTrans = Matrix.Invert( mWorld * mViewProj );
            wvpInvTrans = Matrix.Transpose( wvpInvTrans );

            //reflection plane in homogeneous space
            Vector4 waterPlaneH = Vector4.Transform( waterPlaneL, wvpInvTrans );

            GraphicsDevice.ClipPlanes[ 0 ].IsEnabled = true;
            GraphicsDevice.ClipPlanes[ 0 ].Plane = new Plane( waterPlaneH );

            Matrix reflectionMatrix = Matrix.CreateReflection( new Plane( waterPlaneW ) );

            if ( mDrawFunc != null )
                mDrawFunc( reflectionMatrix );

            GraphicsDevice.RenderState.CullMode = CullMode.CullCounterClockwiseFace;
            GraphicsDevice.ClipPlanes[ 0 ].IsEnabled = false;

            GraphicsDevice.SetRenderTarget( 0, null );


            /*------------------------------------------------------------------------------------------
             * Render to the Refraction Map
             */

            //if the application is going to send us the refraction map
            //exit early. The refraction map must be given to the water component
            //before it renders. 
            //***This option can be handy if you're already drawing your scene to a render target***
            if ( mGrabRefractionFromFB )
            {
                return;
            }

            //update the refraction map, clip objects above the water line
            //so we don't get artifacts
            GraphicsDevice.SetRenderTarget( 0, mRefractionMap );
            GraphicsDevice.Clear( ClearOptions.Target | ClearOptions.DepthBuffer, mOptions.WaterColor, 1.0f, 1 );

            //only clip if the camera is above the water plane
            if ( mViewPos.Y > World.Translation.Y )
            {
                //refrection plane in local space
                //here w=1.1f is a fudge factor so that we don't get gaps between objects and their refraction
                //on the water. It effective raises or lowers the height of the clip plane. w=0.0 will be the clip plane
                //at the water level. 1.1f raises the clip plane above the water level.
                waterPlaneL = new Vector4( 0.0f, -1.0f, 0.0f, 1.5f );

                //refrection plane in world space
                waterPlaneW = Vector4.Transform( waterPlaneL, wInvTrans );

                //refrection plane in homogeneous space
                waterPlaneH = Vector4.Transform( waterPlaneL, wvpInvTrans );

                GraphicsDevice.ClipPlanes[ 0 ].IsEnabled = true;
                GraphicsDevice.ClipPlanes[ 0 ].Plane = new Plane( waterPlaneH );
            }

            if ( mDrawFunc != null )
                mDrawFunc( Matrix.Identity );

            GraphicsDevice.ClipPlanes[ 0 ].IsEnabled = false;
            GraphicsDevice.SetRenderTarget( 0, null );
        }

        /// <summary>
        /// Updates effect parameters related to the water shader
        /// </summary>
        private void UpdateEffectParams()
        {
            //update the reflection and refraction textures
            mEffect.Parameters[ "ReflectMap" ].SetValue( mReflectionMap.GetTexture() );
            mEffect.Parameters[ "RefractMap" ].SetValue( mRefractionMap.GetTexture() );

            //normal map offsets
            mEffect.Parameters[ "FlowMapOffset0" ].SetValue( mOptions.FlowMapOffset0 );
            mEffect.Parameters[ "FlowMapOffset1" ].SetValue( mOptions.FlowMapOffset1 );

            mEffect.Parameters[ "WorldViewProj" ].SetValue( mWorld * mViewProj );

            //pass the position of the camera to the shader
            mEffect.Parameters[ "EyePos" ].SetValue( mViewPos );
        }

        /// <summary>
        /// Generates a grid of vertices to use for the water plane.
        /// </summary>
        /// <param name="numVertRows">Number of rows. Must be 2^n + 1. Ex. 129, 257, 513.</param>
        /// <param name="numVertCols">Number of columns. Must be 2^n + 1. Ex. 129, 257, 513.</param>
        /// <param name="dx">Cell spacing in the x dimension.</param>
        /// <param name="dz">Cell spacing in the y dimension.</param>
        /// <param name="center">Center of the plane.</param>
        /// <param name="verts">Outputs the constructed vertices for the plane.</param>
        /// <param name="indices">Outpus the constructed triangle indices for the plane.</param>
        private void GenTriGrid( int numVertRows, int numVertCols, float dx, float dz,
                                Vector3 center, out Vector3[] verts, out int[] indices )
        {
            int numVertices = numVertRows * numVertCols;
            int numCellRows = numVertRows - 1;
            int numCellCols = numVertCols - 1;

            int mNumTris = numCellRows * numCellCols * 2;

            float width = (float)numCellCols * dx;
            float depth = (float)numCellRows * dz;

            //===========================================
            // Build vertices.

            // We first build the grid geometry centered about the origin and on
            // the xz-plane, row-by-row and in a top-down fashion.  We then translate
            // the grid vertices so that they are centered about the specified 
            // parameter 'center'.

            //verts.resize(numVertices);
            verts = new Vector3[ numVertices ];

            // Offsets to translate grid from quadrant 4 to center of 
            // coordinate system.
            float xOffset = -width * 0.5f;
            float zOffset = depth * 0.5f;

            int k = 0;
            for ( float i = 0; i < numVertRows; ++i )
            {
                for ( float j = 0; j < numVertCols; ++j )
                {
                    // Negate the depth coordinate to put in quadrant four.  
                    // Then offset to center about coordinate system.
                    verts[ k ] = new Vector3( 0, 0, 0 );
                    verts[ k ].X = j * dx + xOffset;
                    verts[ k ].Z = -i * dz + zOffset;
                    verts[ k ].Y = 0.0f;

                    Matrix translation = Matrix.CreateTranslation( center );
                    verts[ k ] = Vector3.Transform( verts[ k ], translation );

                    ++k; // Next vertex
                }
            }

            //===========================================
            // Build indices.

            //indices.resize(mNumTris * 3);
            indices = new int[ mNumTris * 3 ];

            // Generate indices for each quad.
            k = 0;
            for ( int i = 0; i < numCellRows; ++i )
            {
                for ( int j = 0; j < numCellCols; ++j )
                {
                    indices[ k ] = i * numVertCols + j;
                    indices[ k + 1 ] = i * numVertCols + j + 1;
                    indices[ k + 2 ] = ( i + 1 ) * numVertCols + j;

                    indices[ k + 3 ] = ( i + 1 ) * numVertCols + j;
                    indices[ k + 4 ] = i * numVertCols + j + 1;
                    indices[ k + 5 ] = ( i + 1 ) * numVertCols + j + 1;

                    // next quad
                    k += 6;
                }
            }
        }
    }
}
