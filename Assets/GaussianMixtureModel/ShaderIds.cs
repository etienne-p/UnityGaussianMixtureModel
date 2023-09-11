using UnityEngine;

namespace GaussianMixtureModel
{
    // Grouping all Ids here allows us to eyeball them in one pass to look for spelling errors.
    static class ShaderIds
    {
        public static readonly int _NumClusters = Shader.PropertyToID("_NumClusters");
        public static readonly int _ColorBins = Shader.PropertyToID("_ColorBins");
        public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
        public static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
        public static readonly int _AppendSelectedColorBins = Shader.PropertyToID("_AppendSelectedColorBins");
        public static readonly int _IndirectArgsBuffer = Shader.PropertyToID("_IndirectArgsBuffer");
        public static readonly int _CovariancesIn = Shader.PropertyToID("_CovariancesIn");
        public static readonly int _CovariancesOut = Shader.PropertyToID("_CovariancesOut");
        public static readonly int _Weights = Shader.PropertyToID("_Weights");
        public static readonly int _CentroidsIn = Shader.PropertyToID("_CentroidsIn");
        public static readonly int _CentroidsOut = Shader.PropertyToID("_CentroidsOut");
        public static readonly int _SqrtDetReciprocals = Shader.PropertyToID("_SqrtDetReciprocals");
        public static readonly int _Precisions = Shader.PropertyToID("_Precisions");
        public static readonly int _SelectedColorBins = Shader.PropertyToID("_SelectedColorBins");
        public static readonly int _SumsIn = Shader.PropertyToID("_SumsIn");
        public static readonly int _SumsOut = Shader.PropertyToID("_SumsOut");
        public static readonly int _IndirectArgsOffset = Shader.PropertyToID("_IndirectArgsOffset");
        public static readonly int _ClusterIndex = Shader.PropertyToID("_ClusterIndex");
        public static readonly int _Centroids = Shader.PropertyToID("_Centroids");
        public static readonly int _Covariances = Shader.PropertyToID("_Covariances");
        public static readonly int _PrecisionsRW = Shader.PropertyToID("_PrecisionsRW");
        public static readonly int _SqrtDetReciprocalsRW = Shader.PropertyToID("_SqrtDetReciprocalsRW");
        public static readonly int _Cholesky = Shader.PropertyToID("_Cholesky");
        public static readonly int _ViewProjection = Shader.PropertyToID("_ViewProjection");
        public static readonly int _Opacity = Shader.PropertyToID("_Opacity");
        public static readonly int _MaxBinSize = Shader.PropertyToID("_MaxBinSize");
    }
}