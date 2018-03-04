# LightGbmDotNet

A .NET wrapper for the LightGBM machine learning library, including GPU support.

Allows .NET developers to use the <https://github.com/Microsoft/LightGBM> machine learning library on Windows.


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

- Please be aware this wrapper uses files to transfer data to LightGBM and may write a considerable amount of data on your disk
- Don't forget to dispose the LightGbm instance after use to make sure the created files are cleaned up
- To use GPU acceleration (only provided for NVIDIA cards) pass `true` to the constructor of LightGbm class
