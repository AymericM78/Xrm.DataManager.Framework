using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class ConsoleHelper
    {
        private ILogger Logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public ConsoleHelper(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Display instance selection
        /// </summary>
        /// <param name="instances"></param>
        /// <returns></returns>
        public Instance InstanceSelect(List<Instance> instances)
        {
            Logger.LogDisplay($"Sélectionner l'instance cible : ");
            int i = 1;
            foreach (var instance in instances)
            {
                Logger.LogDisplay($" {i} : {instance.DisplayName}");
                i++;
            }
            Logger.LogDisplay($"Choisir entre 1 et {instances.Count}");
            var choice = Console.ReadLine();
            var index = int.Parse(choice);
            var selectedInstance = instances[index - 1];
            return selectedInstance;
        }

        /// <summary>
        /// Display job selection
        /// </summary>
        /// <param name="dataJobs"></param>
        /// <returns></returns>
        public DataJobBase DataJobSelect(List<DataJobBase> dataJobs)
        {
            IOrderedEnumerable<DataJobBase> orderedDataJobs = null;
            int index = 0;
            var valid = false;
            while (!valid)
            {
                Logger.LogDisplay($"Sélectionner le traitement à réaliser : ");
                int i = 1;
                orderedDataJobs = dataJobs.OrderBy(x => x.GetName().ToUpper().Contains("WEBJOB AZURE")).ThenBy(x => x.GetName());
                foreach (var dataJob in orderedDataJobs)
                {
                    Logger.LogDisplay($" {i} : {dataJob.GetName()}");
                    i++;
                }
                Logger.LogDisplay($"Choisir entre 1 et {dataJobs.Count}");
                var choice = Console.ReadLine();
                if (int.TryParse(choice, out index) && index <= dataJobs.Count)
                {
                    valid = true;
                }
                else
                {
                    Logger.LogDisplay($"Choix invalide");
                }
            }
            var selectedDataJob = orderedDataJobs.Skip(index - 1).First();
            return selectedDataJob;
        }
    }
}
