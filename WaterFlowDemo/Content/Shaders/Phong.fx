
//Phong shading

float4x4 World;
float4x4 WorldInvTrans;
float4x4 WorldViewProj;

float4	LightDiffuse0;
float4	LightAmbient0;
float3	LightDir0;

float4	LightDiffuse1;
float4	LightAmbient1;
float3	LightDir1;

float4	LightDiffuse2;
float4	LightAmbient2;
float3	LightDir2;

bool    ClipPlaneEnable;
float4  Clipplane;

texture  DiffuseTex;
float TexScale;

sampler TexS = sampler_state
{
	Texture = <DiffuseTex>;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = LINEAR;
	MaxAnisotropy = 8;
	AddressU  = WRAP;
    AddressV  = WRAP;
};
 
struct OutputVS
{
    float4 posH    : POSITION0;
    float3 normalW : TEXCOORD0;
    float2 tex0    : TEXCOORD1;
	float  height  : TEXCOORD2;
    float3 posW    : TEXCOORD3;
};


OutputVS PhongVS(float3 posL : POSITION0, float3 normalL : NORMAL0)
{
    // Zero out our output.
	OutputVS outVS = (OutputVS)0;
	
    outVS.posW = mul(float4(posL, 1.0f), World);
	// Transform to homogeneous clip space.
	outVS.posH = mul(float4(posL, 1.0f), WorldViewProj);
	
	// Transform normal to world space.
	outVS.normalW = mul(float4(normalL, 0.0f), WorldInvTrans).xyz;	
	
	// Pass on texture coordinates to be interpolated in rasterization.
	outVS.tex0 = 0.f;

	outVS.height = mul(float4(posL, 1.0f), World).y;

	// Done--return the output.
    return outVS;
}

float4 PhongPS(float3 positionW : TEXCOORD3, float3 normalW : TEXCOORD0, float2 tex0 : TEXCOORD1, float height : TEXCOORD2) : COLOR
{
    if (ClipPlaneEnable)
        clip(dot(Clipplane, float4(positionW, 1.0)));

	// Interpolated normals can become unnormal--so normalize.
	normalW = normalize(normalW);
	
	// Light vector is opposite the direction of the light.
	float3 lightVecW0 = -LightDir0;
	float3 lightVecW1 = -LightDir1;
	float3 lightVecW2 = -LightDir2;
	
	// Determine the diffuse light intensity that strikes the vertex.
	float s0 = saturate(dot(lightVecW0, normalW));
	float s1 = saturate(dot(lightVecW1, normalW));
	float s2 = saturate(dot(lightVecW2, normalW));
	
	// Compute the ambient, diffuse and specular terms separatly. 
	float3 diffuse0 = s0 * LightDiffuse0.rgb;
	float3 diffuse1 = s1 * LightDiffuse1.rgb;
	float3 diffuse2 = s2 * LightDiffuse2.rgb;
	
	// Get the texture color.
	float4 texColor = tex2D(TexS, tex0 * TexScale);
	
	// Combine the color from lighting with the texture color.
	float3 color = (diffuse0 + diffuse1 + diffuse2 + LightAmbient0) * texColor.rgb;
		
	// Sum all the terms together and copy over the diffuse alpha.
    return float4(color, ( 2.5f - height )/ 5.0f );
}

technique Phong
{
    pass P0
    {
        // Specify the vertex and pixel shader associated with this pass.
        vertexShader = compile vs_3_0 PhongVS();
        pixelShader  = compile ps_3_0 PhongPS();
    }
}