using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace WaterFlowDemo
{
    public class Environment : Mesh
    {
        public Environment(Game game)
            : base(game)
        {

        }

        protected override void LoadContent()
        {
            base.LoadContent();

            // TODO:
            // mEnvTexture.GenerateMipMaps(TextureFilter.Anisotropic);
            mEffect.Parameters["EnvMap"].SetValue(mEnvTexture);
        }

        public override void Draw(GameTime gameTime)
        {
            Draw(gameTime, false, new Plane());
        }

        public override void Draw(GameTime gameTime, bool bclipplane, Plane clipplane)
        {
            var rasterstate = Game.GraphicsDevice.RasterizerState;

            Game.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            Matrix world = mWorld * Matrix.CreateTranslation(mViewPos);
            mEffect.Parameters["WorldViewProj"].SetValue(world * mView * mProj);

            Vector4 plane = new Vector4(clipplane.Normal, clipplane.D);
            mEffect.Parameters["ClipPlaneEnable"].SetValue(bclipplane);
            mEffect.Parameters["Clipplane"].SetValue(plane);
            mEffect.Parameters["World"].SetValue(world);

            foreach (ModelMesh mesh in mMesh.Meshes)
            {
                foreach (var part in mesh.MeshParts)
                    part.Effect = mEffect; 

                foreach (EffectPass pass in mEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    mesh.Draw();
                }
            }

            Game.GraphicsDevice.RasterizerState = rasterstate;
        }
    }
}
