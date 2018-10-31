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

            mEnvTexture.GenerateMipMaps(TextureFilter.Anisotropic);
            mEffect.Parameters["EnvMap"].SetValue(mEnvTexture);
        }

        public override void Draw(GameTime gameTime)
        {
            CullMode cullmode = Game.GraphicsDevice.RenderState.CullMode;

            Game.GraphicsDevice.RenderState.CullMode = CullMode.None;

            Matrix world = mWorld * Matrix.CreateTranslation(mViewPos);
            mEffect.Parameters["WorldViewProj"].SetValue(world * mView * mProj);

            mEffect.Begin(SaveStateMode.None);

            foreach (ModelMesh mesh in mMesh.Meshes)
            {
                //set the index buffer 
                Game.GraphicsDevice.Indices = mesh.IndexBuffer;

                foreach (EffectPass pass in mEffect.CurrentTechnique.Passes)
                {
                    pass.Begin();
                    foreach (ModelMeshPart meshPart in mesh.MeshParts)
                    {
                        Game.GraphicsDevice.Vertices[0].SetSource(
                        mesh.VertexBuffer, meshPart.StreamOffset, meshPart.VertexStride);

                        Game.GraphicsDevice.VertexDeclaration = meshPart.VertexDeclaration;

                        Game.GraphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList, meshPart.BaseVertex, 0,
                            meshPart.NumVertices, meshPart.StartIndex, meshPart.PrimitiveCount);
                    }
                    pass.End();
                }
            }

            mEffect.End();

            Game.GraphicsDevice.RenderState.CullMode = cullmode;
        }
    }
}
