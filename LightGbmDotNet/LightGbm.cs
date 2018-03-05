using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;

namespace LightGbmDotNet
{
    public class LightGbm : CriticalFinalizerObject, IDisposable
    {
        private const string ModelFileName = "LightGBM_model.txt";
        private static readonly CultureInfo englishCulture = CultureInfo.GetCultureInfo("en");

        private static readonly StringBuilder log = new StringBuilder();
        private DirectoryInfo tempDirectory;
        private readonly bool useGpu;
        private readonly object lockObj = new object();
        private DataSet trainingDataSet;
        private Process runningProcess;

        /// <summary>
        /// An instance of the LightGBM machine learning 
        /// </summary>
        /// <param name="useGpu">If set to true, the LightGBM will try to use GPU accelleration. Only works on NVIDIA cards.</param>
        /// <param name="tempDir">Optional, allows to provide a path to run the LightGBM executable in. This directory must be empty. If not existing it will be created. 
        /// If not provided default temp will be used. 
        /// Attention: Large training or prediction data sets can lead to considerate amount of data on the hard disk (will be removed when the LightGbm instance is disposed).
        /// To changed the default temp dir to store LightGBM files in for all future LightGbm instances at once, assign a value to DirectoryManager.Instance.TempDir
        /// </param>
        public LightGbm(bool useGpu = false, string tempDir = null)
        {
            this.useGpu = useGpu;
            tempDirectory = DirectoryManager.Instance.CreateTempDirectory(useGpu, tempDir);
        }

        private string LightGbmExePath => Path.Combine(tempDirectory.FullName, "lightgbm.exe");
        private string TrainedModelPath => Path.Combine(tempDirectory.FullName, ModelFileName);
        public bool IsTrained => File.Exists(TrainedModelPath);
        public bool HasOwnTrainingDataSet => trainingDataSet != null && trainingDataSet.Exists;

        /// <summary>
        ///     Trains the LightGBM machine learning.
        /// </summary>
        /// <param name="rows">
        ///     An enumerable of rows. The inner enumerable represents the column values of the row. The first
        ///     double value in each row is the label column, that the algorithm is being trained to predict, the rest of the
        ///     column values are the observations uesd for predicting.
        /// </param>
        /// <param name="parameters">
        ///     The parameters used for training, if not provided will use
        ///     Parameters.DefaultForBinaryClassification
        /// </param>
        public DataSet Train(IEnumerable<IEnumerable<double>> rows, Parameters parameters = null)
        {
            lock (lockObj)
            {
                if (parameters == null)
                    parameters = Parameters.DefaultForBinaryClassification;

                trainingDataSet = CreateDataSet(rows);

                TrainInternal(parameters, trainingDataSet);

                return trainingDataSet;
            }
        }

        /// <summary>
        ///     Trains the LightGBM machine learning with a already prepared DataSet instance (performance).
        /// </summary>
        /// <param name="dataSet">
        ///     A DataSet that contains the training data. If you reuse a DataSet from another LightGbm instance here, this other instance must not be disposed yet.
        /// </param>
        /// <param name="parameters">
        ///     The parameters used for training, if not provided will use
        ///     Parameters.DefaultForBinaryClassification
        /// </param>
        public DataSet Train(DataSet dataSet, Parameters parameters = null)
        {
            if (!dataSet.Exists)
                throw new LightGbmException("The DataSet does not exist anymore, have you disposed the LightGbm instance you used to create it?", LogText);

            lock (lockObj)
            {
                if (parameters == null)
                    parameters = Parameters.DefaultForBinaryClassification;

                trainingDataSet = dataSet;

                TrainInternal(parameters, trainingDataSet);

                return dataSet;
            }
        }

        private void TrainInternal(Parameters parameters, DataSet dataSet)
        {
            try
            {
                dataSet.BeginUse();
                parameters = parameters.Clone();
                parameters.AddOrReplace(new Parameter("task", "train"));
                parameters.AddOrReplace(new Parameter("data", dataSet.FilePathShort));
                parameters.AddOrReplace(new Parameter("device", useGpu ? "GPU" : "CPU"));
                parameters.AddOrReplace(new Parameter("is_save_binary_file", "true"));
                var psi = CreateProcessStartInfo(parameters, "train.conf");
                RunProcess(psi);
            }
            catch
            {
                dataSet.RollbackUse();
                throw;
            }
            finally
            {
                dataSet.CompleteUse();
            }
        }

        /// <summary>
        /// Performs another training with changed parameters on the same training data set (faster than calling Train again). 
        /// Will block if other training or prediction is already running. For parallelization create multiple instances of the LightGbm class. 
        /// </summary>
        /// <param name="parameters"></param>
        public void Retrain(Parameters parameters)
        {
            lock (lockObj)
            {
                if (!HasOwnTrainingDataSet)
                    throw new LightGbmException("Training must be run at least once before calling Retrain()", LogText);
                TrainInternal(parameters, trainingDataSet);
            }
        }

        /// <summary>
        ///     Predicts values based on a trained LightGBM machine learning
        /// </summary>
        /// <param name="rows">
        ///     An enumerable of rows. The inner enumerable represents the column values of the row. The column
        ///     values are the observations used for predicting.
        /// </param>
        /// <returns></returns>
        public double[] Predict(IEnumerable<IEnumerable<double>> rows)
        {
            lock (lockObj)
            {
                var dataSet = CreateDataSet(rows);
                return Predict(dataSet);
            }
        }

        /// <summary>
        ///     Predicts values based on a trained LightGBM machine learning
        /// </summary>
        /// <param name="dataSet">
        ///     A DataSet that contains the training data. If you reuse a DataSet from another LightGbm instance here, this other instance must not be disposed yet.
        /// </param>
        /// <returns></returns>
        public double[] Predict(DataSet dataSet)
        {
            if (!dataSet.Exists)
                throw new LightGbmException("The DataSet does not exist anymore, have you disposed the LightGbm instance you used to create it?", LogText);

            lock (lockObj)
            {
                try
                {
                    dataSet.BeginUse();

                    var parameters = new Parameters(
                        new Parameter("task", "predict"),
                        new Parameter("data", dataSet.FilePathShort),
                        new Parameter("input_model", ModelFileName),
                        new Parameter("is_save_binary_file", "true")
                    );

                    var psi = CreateProcessStartInfo(parameters, "predict.conf");
                    RunProcess(psi);

                    var results =
                        (from l in File.ReadAllLines(Path.Combine(tempDirectory.FullName, "LightGBM_predict_result.txt"))
                         select double.Parse(l, englishCulture)).ToArray();

                    return results;
                }
                catch
                {
                    dataSet.RollbackUse();
                    throw;
                }
                finally
                {
                    dataSet.CompleteUse();
                }
            }
        }

        /// <summary>
        ///     Predicts a single row (warning: slow, use the other overload that takes multiple rows in a batch if possible)
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public double Predict(IEnumerable<double> row)
        {
            return Predict(new[] { row })[0];
        }

        /// <summary>
        /// Creates a DataSet for passing it to the Train or Predict methods. If you want to reuse this DataSet in other LightGbm instances, don't dispose the 
        /// LightGbm instance you created the DataSet with, until all other users are done with their work.
        /// </summary>
        /// <param name="rows">
        ///     An enumerable of rows. The inner enumerable represents the column values of the row. The first
        ///     double value in each row is the label column, that the algorithm is being trained to predict, the rest of the
        ///     column values are the observations uesd for predicting.
        /// </param>
        /// <returns></returns>
        public DataSet CreateDataSet(IEnumerable<IEnumerable<double>> rows)
        {
            var dataSet = DataSet.CreateNew(tempDirectory, rows);
            return dataSet;
        }

        public void SaveModel(string filePath)
        {
            using (var s = File.Create(filePath))
                SaveModel(s);
        }

        private void SaveModel(Stream s)
        {
            if (!File.Exists(TrainedModelPath))
                throw new Exception("No model found, have you performed training alredy?");
            using (var ms = File.OpenRead(TrainedModelPath))
                ms.CopyTo(s);
        }

        public void LoadModel(string filePath)
        {
            using (var s = File.OpenRead(filePath))
                LoadModel(s);
        }

        private void LoadModel(Stream s)
        {
            using (var ms = File.Create(TrainedModelPath))
                s.CopyTo(ms);
        }

        private ProcessStartInfo CreateProcessStartInfo(Parameters parameters, string configFileName)
        {
            var configFilePath = Path.Combine(tempDirectory.FullName, configFileName);
            parameters.WriteToConfigFile(configFilePath);
            var psi = new ProcessStartInfo(LightGbmExePath)
            {
                WorkingDirectory = tempDirectory.FullName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "config=" + configFileName,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return psi;
        }

        private void RunProcess(ProcessStartInfo psi)
        {
            Exception exceptionDuringRun = null;
            runningProcess = Process.Start(psi);
            if (runningProcess == null)
                throw new LightGbmException("LightGBM process could not be started, see property Log of this object for details", LogText);
            runningProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                exceptionDuringRun = new Exception(e.Data);
            };
            runningProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                Log(e.Data);
            };
            runningProcess.BeginErrorReadLine();
            runningProcess.BeginOutputReadLine();
            runningProcess.WaitForExit();
            if (exceptionDuringRun != null)
                throw new LightGbmException("Error during LightGBM run, see properties InnerException and Log of this object for details", LogText, exceptionDuringRun);
            if (runningProcess.ExitCode != 0)
                throw new LightGbmException("Error during LightGBM run, see property Log of this object for details", LogText);
            runningProcess = null;
        }

        private void Log(string text)
        {
            OnLogMessageReceived(new LogMessageEventArgs(text));
            lock (log)
            {
                log.AppendLine(text);
            }
        }

        public string LogText
        {
            get
            {
                lock (log)
                {
                    return log.ToString();
                }
            }
        }

        public void ClearLog()
        {
            lock (log)
                log.Clear();
        }

        public event EventHandler<LogMessageEventArgs> LogMessageReceived;

        protected virtual void OnLogMessageReceived(LogMessageEventArgs e)
        {
            LogMessageReceived?.Invoke(this, e);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                if (runningProcess != null && runningProcess.HasExited)
                    runningProcess.Kill();

                var d = tempDirectory;
                tempDirectory = null;
                DirectoryManager.Instance.CleanupDirectory(d);
            }
            catch
            {
                //dont throw in critical finalizer, give up, c'est la vie!
            }
        }

        ~LightGbm()
        {
            ReleaseUnmanagedResources();
        }

    }
}