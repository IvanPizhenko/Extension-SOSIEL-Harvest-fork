/// Name: Parameters.cs
/// Description: 
/// Authors: Multiple.
/// Last updated: July 10th, 2020.
/// Copyright: Garry Sotnik, Brooke A. Cassell, Robert M. Scheller.

// Can use classes from the listed namespaces.
using Landis.Utilities;

namespace Landis.Extension.SOSIELHuman
{
    public class Parameters
    {
        private int timestep;
        private string inputJasonFile;

        //---------------------------------------------------------------------

        public int Timestep
        {
            get
            {
                return timestep;
            }
            set
            {
                if (value < 0)
                    throw new InputValueException(value.ToString(),
                                                  "Timestep must be > or = 0");
                timestep = value;
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Template for pathnames for input maps of land use.
        /// </summary>
        public string InputJson
        {
            get
            {
                return inputJasonFile;
            }
            set
            {
                inputJasonFile = value;
            }
        }

    }
}
