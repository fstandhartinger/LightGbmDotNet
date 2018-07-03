using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LightGbmDotNet
{
    public class Parameters
    {
        private readonly HashSet<Parameter> parameters = new HashSet<Parameter>(Parameter.IdComparer);

        /// <summary>
        ///     Add LightGBM parameters, you can look up the available parmeters here:
        ///     http://lightgbm.readthedocs.io/en/latest/Parameters.html
        ///     Thease parameters are automatically added and dont have to be provided by you: task=train/predict, device=cpu/gpu,
        ///     data=filename
        /// </summary>
        /// <param name="initialParameters"></param>
        public Parameters(params Parameter[] initialParameters)
        {
            foreach (var p in initialParameters)
                parameters.Add(p);
        }

        public static Parameters DefaultForBinaryClassification => new Parameters(CreateDefaultSet("binary")); //recreate on every access => immutable
        public static Parameters DefaultForMulticlassClassification => new Parameters(CreateDefaultSet("multiclass")); //recreate on every access => immutable
        public static Parameters DefaultForRegression => new Parameters(CreateDefaultSet("regression")); //recreate on every access => immutable

        private static Parameter[] CreateDefaultSet(string objective)
        {
            return new[]
            {
                new Parameter("boosting_type", "gbdt"),
                new Parameter("objective", objective),
                //new Parameter("metric", "binary_logloss,auc"),
                new Parameter("metric_freq", "1"),
                new Parameter("is_training_metric", "true"),
                new Parameter("max_bin", "63"),
                new Parameter("num_trees", "100"),
                new Parameter("min_data_in_leaf", "30"),
                new Parameter("learning_rate", "0.1"),
                //new Parameter("num_leaves", "10"),
                //new Parameter("feature_fraction", "0.8"),
                //new Parameter("bagging_freq", "5"),
                //new Parameter("bagging_fraction", "0.8"),
                //new Parameter("min_sum_hessian_in_leaf", "4.0"),
                new Parameter("is_enable_sparse", "true"),
                new Parameter("use_two_round_loading", "false"),
                new Parameter("output_model", "LightGBM_model.txt")
            };
        }

        public void AddOrReplace(Parameter p)
        {
            if (parameters.Contains(p))
                parameters.Remove(p);
            parameters.Add(p);
        }

        public void WriteToConfigFile(string filePath)
        {
            File.WriteAllLines(filePath, parameters.Select(p => p.ToString()).ToArray());
        }

        public Parameters Clone()
        {
            return new Parameters(parameters.ToArray());
        }

        public override string ToString()
        {
            return string.Join(", ", parameters);
        }
    }
}