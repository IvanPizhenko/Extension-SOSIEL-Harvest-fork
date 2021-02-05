using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Landis.Core;
using Landis.Extension.SOSIELHarvest.Algorithm;
using Landis.Extension.SOSIELHarvest.Helpers;
using Landis.Extension.SOSIELHarvest.Services;
using Landis.Library.HarvestManagement;
using Landis.Utilities;
using HarvestManagement = Landis.Library.HarvestManagement;

namespace Landis.Extension.SOSIELHarvest.Models
{
    public class Mode2 : Mode
    {
        private readonly BiomassHarvest.PlugIn _biomassHarvest;
        private readonly ICore _core;
        private readonly LogService _logService;
        private readonly List<ExtendedPrescription> _extendedPrescriptions;

        public Mode2(BiomassHarvest.PlugIn biomassHarvest, ICore core, LogService logService)
        {
            _biomassHarvest = biomassHarvest;
            _core = core;
            _logService = logService;
            _extendedPrescriptions = new List<ExtendedPrescription>();
        }

        public override void Initialize()
        {
            _biomassHarvest.Initialize();

            var biomassHarvestPluginType = _biomassHarvest.GetType();
            var managementAreasField = biomassHarvestPluginType.GetField("managementAreas",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (managementAreasField == null)
                throw new Exception();

            var managementAreas =
                ((IManagementAreaDataset) managementAreasField.GetValue(_biomassHarvest)).ToDictionary(
                    area => area.MapCode.ToString(), area => area);

            Areas = ((IManagementAreaDataset) managementAreasField.GetValue(_biomassHarvest)).ToDictionary(
                area => area.MapCode.ToString(), managementArea =>
                {
                    var area = new Area();
                    area.Initialize(managementArea);
                    return area;
                });

            foreach (var biomassHarvestArea in Areas.Values)
            {
                foreach (var appliedPrescription in biomassHarvestArea.ManagementArea.Prescriptions)
                    _extendedPrescriptions.Add(
                        appliedPrescription.ToExtendedPrescription(biomassHarvestArea.ManagementArea));
            }
        }

        public override void Harvest(SosielData sosielData)
        {
            foreach (var decisionOptionModel in sosielData.NewDecisionOptions)
            {
                GenerateNewPrescription(decisionOptionModel.Name, decisionOptionModel.ConsequentVariable,
                    decisionOptionModel.ConsequentValue, decisionOptionModel.BasedOn,
                    decisionOptionModel.ManagementArea);
            }

            if (sosielData.NewDecisionOptions.Any())
            {
                _logService.WriteLine("\tSosiel generated new prescriptions:");
                _logService.WriteLine($"\t\t{"Area",-10}{"Name",-20}{"Based on",-20}{"Variable",-40}{"Value",10}");

                foreach (var decisionOption in sosielData.NewDecisionOptions)
                {
                    _logService.WriteLine(
                        $"\t\t{decisionOption.ManagementArea,-10}{decisionOption.Name,-20}{decisionOption.BasedOn,-20}{decisionOption.ConsequentVariable,-40}{decisionOption.ConsequentValue,10}");
                }
            }

            _logService.WriteLine($"\tSosiel selected the following prescriptions:");
            _logService.WriteLine($"\t\t{"Area",-10}Prescriptions");

            foreach (var selectedDecisionPair in sosielData.SelectedDecisions)
            {
                var managementArea = Areas[selectedDecisionPair.Key].ManagementArea;

                managementArea.Prescriptions.RemoveAll(prescription =>
                {
                    var decisionPattern = new Regex(@"(MM\d+-\d+_DO\d+)");
                    return decisionPattern.IsMatch(prescription.Prescription.Name);
                });

                if (selectedDecisionPair.Value.Count == 0)
                {
                    _logService.WriteLine($"\t\t{selectedDecisionPair.Key,-10}none");
                    continue;
                }

                foreach (var selectedDesignName in selectedDecisionPair.Value)
                {
                    var extendedPrescription =
                        _extendedPrescriptions.FirstOrDefault(ep =>
                            ep.ManagementArea.MapCode.Equals(managementArea.MapCode) &&
                            ep.Name.Equals(selectedDesignName));
                    if (extendedPrescription != null)
                        ApplyPrescription(managementArea, extendedPrescription);
                }

                var prescriptionsLog = selectedDecisionPair.Value.Aggregate((s1, s2) => $"{s1} {s2}");

                _logService.WriteLine($"\t\t{selectedDecisionPair.Key,-10}{prescriptionsLog}");
            }

            _biomassHarvest.Run();
        }

        public override HarvestResults AnalyzeHarvestingResult()
        {
            var results = new HarvestResults();

            foreach (var biomassHarvestArea in Areas.Values.Select(a => a.ManagementArea))
            {
                var managementAreaName = biomassHarvestArea.MapCode.ToString();

                results.ManageAreaBiomass[managementAreaName] = 0;
                results.ManageAreaHarvested[managementAreaName] = 0;
                results.ManageAreaMaturityPercent[managementAreaName] = 0;

                double manageAreaMaturityProportion = 0;

                foreach (var stand in biomassHarvestArea)
                {
                    double standMaturityProportion = 0;

                    foreach (var site in stand)
                    {
                        double siteBiomass = 0;
                        double siteMaturity = 0;
                        double siteMaturityProportion = 0;

                        foreach (var species in _core.Species)
                        {
                            var cohorts = BiomassHarvest.SiteVars.Cohorts[site][species];

                            if (cohorts == null)
                                continue;

                            double siteSpeciesMaturity = 0;

                            foreach (var cohort in cohorts)
                            {
                                siteBiomass += cohort.Biomass;

                                if (cohort.Age >= _core.Species[species.Name].Maturity)
                                    siteSpeciesMaturity += cohort.Biomass;
                            }

                            siteMaturity += siteSpeciesMaturity;
                        }

                        siteMaturityProportion = Math.Abs(siteBiomass) < 0.0001 ? 0 : siteMaturity / siteBiomass;
                        standMaturityProportion += siteMaturityProportion;

                        results.ManageAreaBiomass[managementAreaName] += siteBiomass;
                        results.ManageAreaHarvested[managementAreaName] +=
                            BiomassHarvest.SiteVars.BiomassRemoved[site];
                    }

                    standMaturityProportion /= stand.Count();
                    manageAreaMaturityProportion += standMaturityProportion;
                }

                manageAreaMaturityProportion /= biomassHarvestArea.StandCount;

                results.ManageAreaBiomass[managementAreaName] =
                    results.ManageAreaBiomass[managementAreaName] / 100 * _core.CellArea;

                results.ManageAreaHarvested[managementAreaName] =
                    results.ManageAreaHarvested[managementAreaName] / 100 * _core.CellArea;

                results.ManageAreaMaturityPercent[managementAreaName] = 100 * manageAreaMaturityProportion;
            }

            return results;
        }

        private void GenerateNewPrescription(string newName, string parameter, dynamic value, string basedOn,
            string managementAreaName)
        {
            HarvestManagement.Prescription newPrescription;

            var managementArea = Areas[managementAreaName].ManagementArea;

            var appliedPrescription = managementArea.Prescriptions.First(p => p.Prescription.Name.Equals(basedOn));

            var areaToHarvest = appliedPrescription.PercentageToHarvest;
            var standsToHarvest = appliedPrescription.PercentStandsToHarvest;
            var beginTime = appliedPrescription.BeginTime;
            var endTime = appliedPrescription.EndTime;

            switch (parameter)
            {
                case "PercentOfHarvestArea":
                    var newAreaToHarvest = new Percentage(value / 100);
                    double cuttingMultiplier = areaToHarvest.Value > 0 ? newAreaToHarvest.Value / areaToHarvest.Value : 1;
                    areaToHarvest = newAreaToHarvest;
                    newPrescription = appliedPrescription.Prescription.Copy(newName, cuttingMultiplier);
                    break;
                default:
                    throw new Exception();
            }

            _extendedPrescriptions.Add(new ExtendedPrescription(newPrescription, managementArea, areaToHarvest, standsToHarvest,
                beginTime, endTime));
        }

        private void ApplyPrescription(ManagementArea managementArea, ExtendedPrescription extendedPrescription)
        {
            managementArea.ApplyPrescription(extendedPrescription.Prescription,
                new Percentage(extendedPrescription.HarvestAreaPercent),
                new Percentage(extendedPrescription.HarvestStandsAreaPercent), extendedPrescription.StartTime,
                extendedPrescription.EndTime);
            managementArea.FinishInitialization();
        }
    }
}