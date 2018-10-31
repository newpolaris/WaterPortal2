float4x4 WorldViewProj;

texture EnvMap;

sampler EnvTex = sampler_state
{
	Texture = <EnvMap>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = WRAP;
    AddressV  = WRAP;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float3 EnvTexC	: TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.Position = mul(input.Position, WorldViewProj);
    
    output.EnvTexC = input.Position.xyz;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    return float4( texCUBE(EnvTex, input.EnvTexC).rgb, 1.0f );
}

technique EnvrionmentMap
{
    pass Pass1
    {		
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
        
        //CullMode = None;
		//ZFunc = Always; // Always write sky to depth buffer
		//StencilEnable = true;
		//StencilFunc   = Always;
		//StencilPass   = Replace;
		//StencilRef    = 0; // clear to zero
    }
}
