// Evaluate the Cholesky decomposition of a symmetric and positive-definite matrix.
float3x3 CholeskyCreate(in float3x3 A)
{
    float3x3 result = 0;
    [unroll(3)]
    for (uint i = 0; i < 3; i++)
    {
        [unroll(3)]
        for (uint j = 0; j <= i; j++)
        {
            float sum = 0.0;

            // 2 since k < j.
            [unroll(2)]
            for (uint k = 0; k < j; k++)
            {
                sum += result[i][k] * result[j][k];
            }

            if (j == i)
            {
                result[i][j] = sqrt(A[i][i] - sum);
            }
            else
            {
                result[i][j] = 1.0 / result[j][j] * (A[i][j] - sum);
            }
        }
    }

    return result;
}

// Solve L.y = b
float3 CholeskySolve(in float3x3 L, in float3 b, out float3 y)
{
    [unroll(3)]
    for(uint i = 0; i != 3; ++i)
    {
        float sum = b[i];
        [unroll(3)]
        for(uint j = 0; j < i; ++j)
        {
            sum -= L[i][j] * y[j];
        }
        y[i] = sum / L[i][i];
    }
}

// Returns the logarithm of the determinant of the matrix whose cholseky decomposition we pass.
float CholeskyLogDet(in float3x3 L)
{
    float sum = 0;
    [unroll(3)]
    for(uint i = 0; i != 3; ++i)
    {
        sum += log(L[i][i]);
    }
    return 2 * sum;
}

