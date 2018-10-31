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
            var rasterstate = Game.GraphicsDevice.RasterizerState;

            Game.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            Matrix world = mWorld * Matrix.CreateTranslation(mViewPos);
            mEffect.Parameters["WorldViewProj"].SetValue(world * mView * mProj);

            foreach (ModelMesh mesh in mMesh.Meshes)
            {
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
