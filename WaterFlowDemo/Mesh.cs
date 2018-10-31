using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace WaterFlowDemo
{
    public struct Light
    {
        public Vector3 Direction;
        public Vector4 DiffuseColor;
        public Vector4 AmbientColor;
    }

    public class Mesh : DrawableGameComponent
    {
        #region Fields

        protected float mScale;
        protected float mRotation;
        protected Vector3 mPosition;
        protected Vector3 mRotAxis;
        protected Matrix mWorld;

        protected Matrix mView;
        protected Matrix mProj;
        protected Vector3 mViewPos;

        protected Effect mEffect;
        protected string mEffectAsset;

        protected Model mMesh;
        protected string mMeshAsset;
        protected BoundingBox mBoundBox;
        protected BoundingSphere mBoundSphere;

        protected Texture2D mTexture;
        protected string mTexAsset;

        protected TextureCube mEnvTexture;
        protected string mEnvTexAsset;

        protected List<Light> mLights;

        protected bool mLightingEnabled;
        protected bool mSpecularLighting;
        #endregion

        #region Properties

        public Vector3 Position
        {
            get { return mPosition; }
            set { mPosition = value; }
        }

        protected Vector3 Center
        {
            get
            {
                Vector3 min = Vector3.Transform(mBoundBox.Min, mWorld);
                Vector3 max = Vector3.Transform(mBoundBox.Max, mWorld);

                return (min + max) * .5f;
            }
        }

        public float Scale
        {
            get { return mScale; }
            set { mScale = value; }
        }

        public float Rotation
        {
            get { return mRotation; }
            set { mRotation = value; }
        }

        public Vector3 RotationAxis
        {
            get { return mRotAxis; }
            set { mRotAxis = value; }
        }

        public Matrix World
        {
            get { return mWorld; }
            set { mWorld = value; }
        }

        public string EffectAsset
        {
            get { return mEffectAsset; }
            set { mEffectAsset = value; }
        }

        public string MeshAsset
        {
            get { return mMeshAsset; }
            set { mMeshAsset = value; }
        }

        public string TextureAsset
        {
            get { return mTexAsset; }
            set { mTexAsset = value; }
        }

        public string EnvironmentTextureAsset
        {
            get { return mEnvTexAsset; }
            set { mEnvTexAsset = value; }
        }

        public List<Light> Lights
        {
            get { return mLights; }
            set { mLights = value; }
        }

        public bool EnableLighting
        {
            get { return mLightingEnabled; }
            set { mLightingEnabled = value; }
        }

        public bool SpecularLighting
        {
            get { return mSpecularLighting; }
            set { mSpecularLighting = value; }
        }

        public Matrix View
        {
            set { mView = value; }
        }
        public Matrix Projection
        {
            set { mProj = value; }
        }

        public Vector3 ViewPosition
        {
            set { mViewPos = value; }
        }
        #endregion

        public Mesh(Game game) : base(game)
        {
            mPosition = Vector3.Zero;
            mScale = 1.0f;
            mRotation = 0.0f;
            mRotAxis = Vector3.Up;

            mLights = new List<Light>(3);
            mLightingEnabled = true;
            mSpecularLighting = true;
        }

        public override void Initialize()
        {
            mWorld =  Matrix.CreateFromAxisAngle(mRotAxis, mRotation) *
                      Matrix.CreateScale(mScale) *
                      Matrix.CreateTranslation(mPosition);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            //create and load the effect
            if (mEffectAsset != null)
            {
                mEffect = Game.Content.Load<Effect>(mEffectAsset);

                int i = 0;
                foreach (Light light in mLights)
                {
                    mEffect.Parameters["LightDir" + i].SetValue(light.Direction);
                    mEffect.Parameters["LightDiffuse" + i].SetValue(light.DiffuseColor);
                    mEffect.Parameters["LightAmbient" + i].SetValue(light.AmbientColor);
                    i++;
                }
            }

            //create and load the mesh 
            if (mMeshAsset != null)
            {
                mMesh = Game.Content.Load<Model>(mMeshAsset);
                
                if (mMesh.Tag != null)
                    mBoundBox = (BoundingBox)mMesh.Tag;
                else
                    mBoundBox = new BoundingBox();

                computeBoundingSphere();
            }

            //create and load the texture
            if (mTexAsset != null)
            {
                mTexture = Game.Content.Load<Texture2D>(mTexAsset);

                if(mEffect != null && mEffect.Parameters["DiffuseTex"] != null)
                    mEffect.Parameters["DiffuseTex"].SetValue(mTexture);
            }

            //create and load the environment map
            if (mEnvTexAsset != null)
            {
                mEnvTexture = Game.Content.Load<TextureCube>(mEnvTexAsset);
                // TODO:
                // mEnvTexture.GenerateMipMaps(TextureFilter.Anisotropic);
            }

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {

        }

        public override void Draw(GameTime gameTime)
        {
            //draw with the basic effect
            if (mEffect == null)
            {
                DrawBasicEffect();
            }
            else
            {
                DrawCustomEffect();
            }
        }

        protected virtual void DrawBasicEffect()
        {
            Matrix[] boneTransforms = new Matrix[mMesh.Bones.Count];
            mMesh.CopyAbsoluteBoneTransformsTo(boneTransforms);
            
            foreach (ModelMesh mesh in mMesh.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = boneTransforms[mesh.ParentBone.Index] * mWorld;
                    effect.View = mView;
                    effect.Projection = mProj;

                    if (mTexture != null)
                    {
                        effect.Texture = mTexture;
                        effect.TextureEnabled = true;
                    }

                    if (mLightingEnabled)
                    {
                        effect.EnableDefaultLighting();
                        effect.PreferPerPixelLighting = true;

                        if (!mSpecularLighting)
                            effect.SpecularColor = Vector3.Zero;
                    }
                    else
                    {
                        effect.LightingEnabled = false;
                    }
                }
                
                mesh.Draw();
            }
        }

        protected virtual void DrawCustomEffect()
        {
            Matrix[] boneTransforms = new Matrix[mMesh.Bones.Count];
            mMesh.CopyAbsoluteBoneTransformsTo(boneTransforms);

            foreach (ModelMesh mesh in mMesh.Meshes)
            {
                Matrix world = boneTransforms[mesh.ParentBone.Index] * mWorld;

                Matrix worldInvTrans = Matrix.Invert(world);
                worldInvTrans = Matrix.Transpose(worldInvTrans);

                mEffect.Parameters["World"].SetValue( world );
                mEffect.Parameters["WorldInvTrans"].SetValue(worldInvTrans);
                mEffect.Parameters["WorldViewProj"].SetValue(world * mView * mProj);
                if (mTexture == null)
                    mEffect.Parameters["DiffuseTex"].SetValue(((BasicEffect)mesh.Effects[0]).Texture);
                else
                    mEffect.Parameters["DiffuseTex"].SetValue(mTexture);

                foreach (ModelMeshPart part in mesh.MeshParts)
                    part.Effect = mEffect;

                foreach (EffectPass pass in mEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    mesh.Draw();
                }
            }
        }

        protected void ComputeBoundingBoxFromPoints(Vector3[] points, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < points.Length; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }
        }

        public void SetCamera(Matrix view, Matrix proj, Vector3 pos)
        {

        }

        public void SetCamera(Matrix viewProj, Vector3 pos)
        {

        }

        public void SetCamera(Matrix viewProj)
        {

        }

        private void computeBoundingSphere()
        {
            mBoundSphere = new BoundingSphere();

            //accumulate all the bounding spheres to find the total
            foreach (ModelMesh mesh in mMesh.Meshes)
            {
                BoundingSphere meshBoundingSphere = mesh.BoundingSphere;

                BoundingSphere.CreateMerged(ref mBoundSphere,
                                            ref meshBoundingSphere,
                                            out mBoundSphere);
            }
        }
    }
}
