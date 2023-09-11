StructuredBuffer<float3> _Centroids;
StructuredBuffer<float3x3> _Cholesky;

float4x4 _ViewProjection;
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
    float3 center = _Centroids[input.instanceID];
    float3x3 cholesky = _Cholesky[input.instanceID];

    // The built-in sphere mesh we use is sphere centered at zero with radius = 0.5
    float3 position = input.vertex.xyz * 2.0;

    position = mul(cholesky, position);
    position += center;

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