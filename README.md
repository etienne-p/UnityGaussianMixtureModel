# Unity Gaussian Mixture Model
This project is an implementation of a Gaussian Mixture Model in Unity using Compute Shaders. It is an unsupervised machine learning algorithm. Here we implement and demonstrate it by classifying colors in an image. There are many uses for such algorithms in Visual Effects and Computer Vision, although these applications go beyond the scope of this project.

We base our implementation on the one provided in [Numerical Recipes (third edition)](http://numerical.recipes/book.html). The book's implementation is single threaded, in C++. A lot of the work done here relates to parallelizing the algorithm, on the GPU.

Our implementation is independent from render pipelines, so the demo project uses the built in render pipeline to save space and time, and the code should be compatible with any render pipeline.

![Animation](./Images/Animation.gif)

## Resources
* Gaussian Mixture Models as explained in [Numerical Recipes (third edition)](http://numerical.recipes/book.html), see "16.1 Gaussian Mixture Models and k-Means Clustering". Our explanations in this document are only complementary to those provided in the book.
* Presentation from Nvidia, [Optimizing Parallel Reduction in CUDA](https://developer.download.nvidia.com/assets/cuda/files/reduction.pdf
). This is important as reductions account for a lot of our GPU calls and the code can seem cryptic at first.

## Using The Demo
### Workflow
The demo runs both in Edit and Play modes. We provide a visualization of the algorithm in 3D (each dimension corresponding to a color channel, RGB) in a custom window. We execute the Expectation Maximization procedure that calculates the distribution in a coroutine so that the convergence is visually perceptible.

* Open `Assets/Scenes/SampleScene.unity`. 
* Open the `Window/Gaussian Mixture/Visualization` window. 
* Select the `GaussianMixtureModel` gameObject in the hierarchy. It holds a `GaussianMixtureModelComponent`.
* Assign a texture to the `Source` field of the component. Make sure that you have checked `Read/Write` in the texture import settings, so that it is readable from Compute Shaders.
* Press `Execute Expectation Maximization` button in the visualization window.

You should see a 3D visualization of the clusters progressively fitting the color distribution of the source image.

Note that the initial clusters' means are evaluated procedurally, we use evenly distributed hues. It is sufficient for this demo, but in a real scenario one would pay a lot of attention to choosing initialization values based on the input data, as it has a significant influence on the number of iterations required to obtain a good result.

### Gaussian Mixture Model Component
The `GaussianMixtureModelComponent` exposes the following properties:

* The compute shaders used, these should not be changed. A tooltip mentions the shader name in case the field needs to be restored.
* **Source**, the texture we use as input data.
* **Num Clusters**, the number of clusters in the Gaussian Mixture Model.
* **Iterations**, the number of steps of Expectation Maximization to execute during the convergence procedure. The more iterations the more refined the resulting distribution.
* **Delay**, the time delay between the execution of each step.
Note that a right click on the component will show a `DEBUG - Capture` entry. This schedules a Render Doc capture of the next execution. Remember to leave `#pragma enable_d3d11_debug_symbols` in shaders to enable debug symbols.

## Implementation
Here we will describe our implementation by comparison to the one suggested in the Numerical Recipes book.
### Color Quantization
We must calculate means and covariance for each sample, that is, each pixel. This huge amount of data is managed by quantizing colors. We quantize colors in a 32x32x32 grid. We count samples falling in each cell of the grid. This allows us to calculate means and covariances for each grid cell only. Note that we must use the number of samples per cell as a scaling factor in our calculations.
### Reductions
The original algorithm features many massive iterations over the entire dataset. As we work on the GPU we implement those using parallel reductions. These reductions require double buffers for means, covariances and weights. We generally tend to append "In" and "Out" to buffer names in our shaders to clarify this.
### Optimizations
* We filter out empty grid cells in the initialization step, as only non-empty grid cells are relevant for processing. Non-empty grid cells are stored as `uint2`, storing the index and number of samples respectively. These grid cells are stored in an Append Buffer. The count of items in that buffer determines how many thread groups will be dispatched for subsequent operations, including reductions. We use the `_IndirectArgsBuffer` buffer to store these thread groups count, and subsequently rely on indirect dispatches.
* Covariance is a symmetric matrix. Therefore only 6 of its 9 values need storage and we use a `float2x3` matrix. We provide utilities to manipulate these matrices, converting them back and forth to 3x3 matrices. Note that a similar optimization would be possible for Cholesky decomposition matrices, but we only use one of those per cluster.	
### Stability
It is possible for the covariance matrix to become singular as the procedure goes on. This can be related to rounding errors. In the book an error would be thrown as we attempt to calculate the Cholesky decomposition of such a matrix. We choose instead to bump the diagonal of covariance matrices, adding a small value, to guarantee a positive-definite matrix. A similar approach is taken by the [Scikit](https://scikit-learn.org/stable/modules/generated/sklearn.mixture.GaussianMixture.html#sklearn.mixture.GaussianMixture) toolkit, see the "reg_covarfloat" parameter.
### Visualization
We provide a 3D visualization of the algorithm in a custom window. It allows us to observe the color distribution of the image and the clusters fitting it. We provide orbit view controls using the mouse. The visualization uses Unity built-in Sphere and Cubes. Scaled cubes represent grid cells, deformed sphere represent clusters. An important shortcoming is that transparency is somewhat wrong and misleading, orbit controls alleviate the issue. We could fix out-of-order transparent samples using Order Independent Transparency or Raymarching.
