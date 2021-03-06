// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2021 SOSIEL Inc. All rights reserved.

namespace Landis.Extension.SOSIELHarvest.Output
{
    public class FEValuesOutput
    {
        public int Iteration { get; set; }

        //public string Site { get; set; }

        public double AverageBiomass { get; set; }

        public double AverageReductionPercentage { get; set; }

        public double MinReductionPercentage { get; set; }

        public double MaxReductionPercentage { get; set; }

        public double BiomassReduction { get; set; }

        //public double Profit { get; set; }
    }
}
