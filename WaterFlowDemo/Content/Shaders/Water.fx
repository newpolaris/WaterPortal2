//Water effect shader that uses reflection and refraction maps projected onto the water.
//These maps are distorted based on the two scrolling normal maps.

float4x4 World;
float4x4 WorldViewProj;

float4  WaterColor;
float3	SunDirection;
float4  SunColor;
float	SunFactor; //the intensity of the sun specular term.
float   SunPower; //how shiny we want the sun specular term on the water to be.
float3  EyePos;

//Flow map offsets used to scroll the wave maps
float	FlowMapOffset0;
float	FlowMapOffset1;

//scale used on the wave maps
float TexScale;
float HalfCycle;

// Two normal maps and the reflection/refraction maps
texture FlowMap;
texture NoiseMap;
texture WaveMap0;
texture WaveMap1;
texture ReflectMap;
texture RefractMap;

static const float	  R0 = 0.02037f;

sampler FlowMapS = sampler_state
{
	Texture = <FlowMap>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = WRAP;
    AddressV  = WRAP;
};

sampler NoiseMapS = sampler_state
{
	Texture = <NoiseMap>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = WRAP;
    AddressV  = WRAP;
};
sampler WaveMapS0 = sampler_state
{
	Texture = <WaveMap0>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = WRAP;
    AddressV  = WRAP;
};

sampler WaveMapS1 = sampler_state
{
	Texture = <WaveMap1>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = WRAP;
    AddressV  = WRAP;
};

sampler ReflectMapS = sampler_state
{
	Texture = <ReflectMap>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = CLAMP;
    AddressV  = CLAMP;
};

sampler RefractMapS = sampler_state
{
	Texture = <RefractMap>;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	MipFilter = LINEAR;
	AddressU  = CLAMP;
    AddressV  = CLAMP;
};

struct OutputVS
{
    float4 posH			: POSITION0;
    float3 toEyeW		: TEXCOORD0;
    float2 texcoord		: TEXCOORD1;
    float4 projTexC		: TEXCOORD2;
    float4 pos			: TEXCOORD3;
};

OutputVS WaterVS( float3 posL	: POSITION0, 
                  float2 texC   : TEXCOORD0)
{
    // Zero out our output.
	OutputVS outVS = (OutputVS)0;
	
	// Transform vertex position to world space.
	float3 posW  = mul(float4(posL, 1.0f), World).xyz;
	outVS.pos.xyz = posW;
	outVS.pos.w = 1.0f;
	
	// Compute the unit vector from the vertex to the eye.
	outVS.toEyeW = posW - EyePos;
	
	// Transform to homogeneous clip space.
	outVS.posH = mul(float4(posL, 1.0f), WorldViewProj);
	
	// Scroll texture coordinates.
	outVS.texcoord = texC;

	// Generate projective texture coordinates from camera's perspective.
	outVS.projTexC = outVS.posH;
	
	// Done--return the output.
    return outVS;
}

float4 WaterFlowPS( float3 toEyeW		: TEXCOORD0,
					float2 texcoord		: TEXCOORD1,
					float4 projTexC		: TEXCOORD2,
					float4 pos			: TEXCOORD3) : COLOR
{
	//transform the projective texcoords to NDC space
	//and scale and offset xy to correctly sample a DX texture
	projTexC.xyz /= projTexC.w;            
	projTexC.x =  0.5f*projTexC.x + 0.5f; 
	projTexC.y = -0.5f*projTexC.y + 0.5f;
	projTexC.z = .1f / projTexC.z; //refract more based on distance from the camera
	
	toEyeW    = normalize(toEyeW);
	
	// Light vector is opposite the direction of the light.
	float3 lightVecW = -SunDirection;
	
	//get and uncompress the flow vector for this pixel
	float2 flowmap = tex2D( FlowMapS, texcoord ).rg * 2.0f - 1.0f;
	float cycleOffset = tex2D( NoiseMapS, texcoord ).r;

	float phase0 = cycleOffset * .5f + FlowMapOffset0;
	float phase1 = cycleOffset * .5f + FlowMapOffset1;
	
	// Sample normal map.
	float3 normalT0 = tex2D(WaveMapS0, ( texcoord * TexScale ) + flowmap * phase0 );
	float3 normalT1 = tex2D(WaveMapS1, ( texcoord * TexScale ) + flowmap * phase1 );

	float flowLerp = ( abs( HalfCycle - FlowMapOffset0 ) / HalfCycle );

	 //unroll the normals retrieved from the normalmaps
    normalT0.yz = normalT0.zy;	
	normalT1.yz = normalT1.zy;
	
	normalT0 = 2.0f*normalT0 - 1.0f;
    normalT1 = 2.0f*normalT1 - 1.0f;
    
	float3 normalT = lerp( normalT0, normalT1, flowLerp );
	
	//get the reflection vector from the eye
	float3 R = normalize(reflect(toEyeW,normalT));
	
	float4 finalColor;
	finalColor.a = 1;

	//compute the fresnel term to blend reflection and refraction maps
	float ang = saturate(dot(-toEyeW,normalT));
	float f = R0 + (1.0f-R0) * pow(1.0f-ang,5.0);	
	
	//also blend based on distance
	f = min(1.0f, f + 0.007f * EyePos.y);	
		
	//compute the reflection from sunlight
	float sunFactor = SunFactor;
	float sunPower = SunPower;
	
	if(EyePos.y < pos.y)
	{
		sunFactor = 7.0f; //these could also be sent to the shader
		sunPower = 55.0f;
	}
	float3 sunlight = sunFactor * pow(saturate(dot(R, lightVecW)), sunPower) * SunColor;

	float4 refl = tex2D(ReflectMapS, projTexC.xy + projTexC.z * normalT.xz);
	float4 refr = tex2D(RefractMapS, projTexC.xy - projTexC.z * normalT.xz);
	
	//only use the refraction map if we're under water
	if(EyePos.y < pos.y)
		f = 0.0f;
	
	//interpolate the reflection and refraction maps based on the fresnel term and add the sunlight
	finalColor.rgb = WaterColor * lerp( refr, refl, f) + sunlight;
	
	return finalColor;
}

float4 NormalFlowPS( float3 toEyeW		: TEXCOORD0,
					 float2 tex0		: TEXCOORD1,
					 float4 projTexC	: TEXCOORD2,
					 float4 pos			: TEXCOORD3 ) : COLOR
{
	//transform the projective texcoords to NDC space
	//and scale and offset xy to correctly sample a DX texture
	projTexC.xyz /= projTexC.w;            
	projTexC.x =  0.5f*projTexC.x + 0.5f; 
	projTexC.y = -0.5f*projTexC.y + 0.5f;
	projTexC.z = .1f / projTexC.z; //refract more based on distance from the camera
	
	toEyeW    = normalize(toEyeW);
	
	// Light vector is opposite the direction of the light.
	float3 lightVecW = -SunDirection;
	
	//get and uncompress the flow vector for this pixel
	float2 flowmap = tex2D( FlowMapS, tex0 ).rg * 2.0f - 1.0f;
	float cycleOffset = tex2D( NoiseMapS, tex0 ).r;

	float phase0 = cycleOffset * .5f + FlowMapOffset0;
	float phase1 = cycleOffset * .5f + FlowMapOffset1;
	
	// Sample normal map.
	float3 normalT0 = tex2D(WaveMapS0, ( tex0 * TexScale ) + flowmap * phase0 );
	float3 normalT1 = tex2D(WaveMapS1, ( tex0 * TexScale ) + flowmap * phase1 );

	float f = ( abs( HalfCycle - FlowMapOffset0 ) / HalfCycle );
    
	float3 normalT = lerp( normalT0, normalT1, f );
	return float4( normalT, 1.0f );
}

float4 FlowMapPS( float3 toEyeW		: TEXCOORD0,
				  float2 tex0		: TEXCOORD1,
				  float4 projTexC	: TEXCOORD2,
				  float4 pos		: TEXCOORD3 ) : COLOR
{	
	return float4( tex2D( FlowMapS, tex0 ).rg, 0.0f, 1.0f );
}

technique WaterTech
{
    pass Pass1
    {
        // Specify the vertex and pixel shader associated with this pass.
        vertexShader = compile vs_3_0 WaterVS();
        pixelShader  = compile ps_3_0 WaterFlowPS();
    }    
}

technique NormalFlowTech
{
    pass Pass1
    {
        // Specify the vertex and pixel shader associated with this pass.
        vertexShader = compile vs_3_0 WaterVS();
        pixelShader  = compile ps_3_0 NormalFlowPS();
    }    
}

technique FlowMapTech
{
    pass Pass1
    {
        // Specify the vertex and pixel shader associated with this pass.
        vertexShader = compile vs_3_0 WaterVS();
        pixelShader  = compile ps_3_0 FlowMapPS();
    }    
}
