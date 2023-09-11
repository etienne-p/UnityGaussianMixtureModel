#include "Utility.hlsl"

#define GRID_SIZE 32

StructuredBuffer<uint2> _ColorBins;

float4x4 _ViewProjection;
float _MaxBinSize;
float _Opacity;

struct Attributes
{
    float4 vertex : POSITION;
    uint instanceID : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
};

Varyings Vertex(Attributes input)
{
    uint2 colorBin = _ColorBins[input.instanceID];

    // Determine voxel center.
    uint3 id3d = To3DIndex(colorBin.x, GRID_SIZE);
    // The built-in cube mesh we use is centered at zero.
    float3 center = (id3d + (0.5).xxx) / (float)(GRID_SIZE).xxx;
    float scale1d = min(1, (float)colorBin.y / _MaxBinSize);
    // Note that we scale the volume, for an accurate perception.
    float scale3d = pow(scale1d, 1.0 / 3.0);
    float3 position = center + input.vertex.xyz * scale3d / (float)(GRID_SIZE).xxx;

    float4 clipPosition = mul(_ViewProjection, float4(position, 1));

    Varyings output;
    output.positionCS = clipPosition;
    output.color = float4(center, _Opacity);
    return output;
}

float4 Fragment(Varyings input) : SV_Target
{
    return input.color;
}