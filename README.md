# LightGbmDotNet
A .NET wrapper for the LightGBM machine learning library

Allows .NET developers to use the https://github.com/Microsoft/LightGBM machine learning library on Windows.

LightGBM is one of the leading Gradient Boosting solutions, progvided open source by Microsoft.

Usage (C#):

using(var lightGbm = new LightGbm()) 
{
  lightGbm.Train(trainingData);
  var predictions = lightGbm.Predict(predictionData);  
}

Where both trainingData and predictionData are of type IEnumerable<IEnumerable<double>> representing rows of double values.
First column in trainingData is the label column (the value that you want to predict). 
Omit this column in predictionData.
  
