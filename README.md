# LightGbmDotNet

A .NET wrapper for the [LightGBM](https://github.com/Microsoft/LightGBM) machine learning library, including GPU support.


LightGBM is one of the leading Gradient Boosting solutions, provided open source by Microsoft.


### Usage (C#):

```C#
using(var lightGbm = new LightGbm())
{
   lightGbm.Train(trainingData); //optionally pass parameters
   var predictions = lightGbm.Predict(predictionData); //returns an array of predictions
   lightGbm.SaveModel("somefile.txt"); //for later reuse of the trained machine learning model without retraining
}
```

Where both `trainingData` and `predictionData` are of type `IEnumerable<IEnumerable<double>>` representing rows of double values.

First column in `trainingData` is the label column (the value that you want to predict). 
Omit this column in `predictionData`.


### License

MIT


### Notes

- You might need to install the [Visual Studio 2015 C++ Redistributable](https://www.microsoft.com/en-ca/download/details.aspx?id=48145) on the machine you want to run this on. 
- Please be aware this wrapper uses files to transfer data to LightGBM and may write a considerable amount of data on your disk
- Don't forget to dispose the LightGbm instance after use to make sure the created files are cleaned up
- To use GPU acceleration (only provided for NVIDIA cards) pass `true` to the constructor of LightGbm class. If it crashes you might need to install the [OpenCL implementation from NVIDIA (CUDA)](https://developer.nvidia.com/cuda-downloads).
- Works only on Windows, 64 Bit, .NET Framework 4.6.1 or higher (should also work with lower .NET Framework versions if compiled with it)


### Alternatives

Not tested myself so far:
- [XGBoost wrapper in SharpLearning](https://github.com/mdabros/SharpLearning/wiki/Using-SharpLearning.XGBoost)
- [LightGBMSharp](https://github.com/ttustonic/LightGBMSharp)
Both libraries work using in process / in memory data transfer to / from the native machine learning libraries, which should lead to superior performance compared to LightGbmDotNet
