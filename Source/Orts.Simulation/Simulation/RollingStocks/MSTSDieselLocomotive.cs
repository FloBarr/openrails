// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

/* DIESEL LOCOMOTIVE CLASSES
 * 
 * The Locomotive is represented by two classes:
 *  MSTSDieselLocomotiveSimulator - defines the behaviour, ie physics, motion, power generated etc
 *  MSTSDieselLocomotiveViewer - defines the appearance in a 3D viewer.  The viewer doesn't
 *  get attached to the car until it comes into viewing range.
 *  
 * Both these classes derive from corresponding classes for a basic locomotive
 *  LocomotiveSimulator - provides for movement, basic controls etc
 *  LocomotiveViewer - provides basic animation for running gear, wipers, etc
 * 
 */

//#define ALLOW_ORTS_SPECIFIC_ENG_PARAMETERS


using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using Orts.Parsers.Msts;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks.SubSystems.Controllers;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions;
using ORTS.Common;
using System.Diagnostics;
using System;
using System.IO;
using System.Text;
using Event = Orts.Common.Event;

using System.Collections.Generic;

//** Ajout FB   **//
using ORTS.Scripting.Api;

namespace Orts.Simulation.RollingStocks
{
    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////

    /// <summary>
    /// Adds physics and control for a diesel locomotive
    /// </summary>
    public class MSTSDieselLocomotive : MSTSLocomotive
    {
        public float IdleRPM;
        public float HeatingRPM;
        public float HeatingVoltage;    //** Not the best place to put, would be better linked to a generator or alternator class
        public float HeatingAskedPower;
        public float HeatingAbsorbedPower;
        public bool HeatingStatus = false;
        public float MaxRPM;
        public float MaxRPMChangeRate;
        public float PercentChangePerSec = .2f;
        public float InitialExhaust;
        public float InitialMagnitude;
        public float MaxExhaust = 2.8f;
        public float MaxMagnitude = 1.5f;
        public float EngineRPMderivation;
        float EngineRPMold;
        float EngineRPMRatio; // used to compute Variable1 and Variable2
        public float MaximumDieselEnginePowerW;
        public float DieselUsablePower = 0;

        public MSTSNotchController FuelController = new MSTSNotchController(0, 1, 0.0025f);
        public float MaxDieselLevelL = 5000.0f;
        public float DieselLevelL
        {
            get { return FuelController.CurrentValue * MaxDieselLevelL; }
            set { FuelController.CurrentValue = value / MaxDieselLevelL; }
        }

        public float DieselUsedPerHourAtMaxPowerL = 1.0f;
        public float DieselUsedPerHourAtIdleL = 1.0f;
        public float DieselFlowLps;
        public float DieselWeightKgpL = 0.8508f; //per liter
        float InitialMassKg = 100000.0f;

        public float LocomotiveMaxRailOutputPowerW;

        public float EngineRPM;
        public SmoothedData ExhaustParticles = new SmoothedData(1);
        public SmoothedData ExhaustMagnitude = new SmoothedData(1);
        public SmoothedData ExhaustColorR = new SmoothedData(1);
        public SmoothedData ExhaustColorG = new SmoothedData(1);
        public SmoothedData ExhaustColorB = new SmoothedData(1);

        public float DieselOilPressurePSI = 0f;
        public float DieselMinOilPressurePSI;
        public float DieselMaxOilPressurePSI;
        public float DieselTemperatureDeg = 40f;
        public float DieselMaxTemperatureDeg;
        public DieselEngine.Cooling DieselEngineCooling = DieselEngine.Cooling.Proportional;

        public DieselEngines DieselEngines;

        public GearBox GearBox = new GearBox(); // this is the same instance present in the first engine of the locomotive; instead instances in other engines, if any, are copies

        /// <summary>
        /// Used to accumulate a quantity that is not lost because of lack of precision when added to the Fuel level
        /// </summary>        
        float partialFuelConsumption = 0;

        public bool DPUSet = false;
        public bool AESSEquiped = false;

        private const float GearBoxControllerBoost = 1; // Slow boost to enable easy single gear up/down commands

        //** New UpdateMotiveForce parameters   **//
        /// <summary>
        /// Voltage supplied by generator
        /// </summary> 
        public float Voltage = 0;
        /// <summary>
        /// Flow generated by current passing through field
        /// </summary> 
        public float InductFlow = 0;
        /// <summary>
        /// Force generated by flow, using AmpToFlow factor
        /// </summary> 
        public float InducedForce = 0;
        /// <summary>
        /// Wheel Force, defined bu Induced Force and Gearing Reduction
        /// </summary> 
        public float WheelForce = 0;
        /// <summary>
        /// Wheel speed in mps
        /// </summary> 
        public float WheelSpeed = 0;
        /// <summary>
        /// motor rotation speed in rad/s
        /// </summary> 
        public float RotSpeed = 0;
        /// <summary>
        /// Back EMF, in Volts, generated by flow and rotation speed
        /// </summary> 
        public float BackEMF = 0;
        /// <summary>
        /// Voltage usable after deducing Back EMF
        /// </summary> 
        public float UInductor = 0;
        /// <summary>
        /// Current passing through motor
        /// </summary> 
        public float IInductor = 0;
        /// <summary>
        /// Indicates the Diesel or generator is overload, and voltage flatten to keep balance
        /// </summary> 
        public bool OverLoad = false;
        /// <summary>
        /// The overload value, in W
        /// </summary> 
        private float OverLoadValue = 0;
        /// <summary>
        /// Indicates the motor eat more amperes that allowed by U=R*I
        /// </summary> 
        public bool OverAmp = false;
        /// <summary>
        /// Define if DCMotor code is used or not.
        /// </summary> 
        public bool HasDCMotor = false;
        /// <summary>
        /// Define if DCMotor Force is the force to use
        /// </summary> 
        public bool UseDCMotorForce = false;

        /// <summary>
        /// Lower voltage of generator (proportionnal to RPM for a generator, around 0V for an alternator.
        /// </summary> 
        public float GeneratorVoltage = 0;
        /// <summary>
        /// Higher voltage of generator.
        /// </summary> 
        public float GeneratorLowVoltage = 0;
        /// <summary>
        /// Voltage of generator when not supplying motors.
        /// </summary> 
        public float GeneratorUnloadedVoltage = 0;
       
        /// <summary>
        /// Number of field changes defined.
        /// </summary> 
        private int FieldChangeNumber = 0;
        /// <summary>
        /// Tab listing fields speed changes when train acceleates
        /// </summary> 
        private List<float> FieldChangeSpeedUp = new List<float>();
        /// <summary>
        /// Tab listing fields speed changes when train decelerates
        /// </summary> 
        private List<float> FieldChangeSpeedDown = new List<float>();
        /// <summary>
        /// Tab listing notches where field changes.
        /// </summary> 
        private List<float> FieldChangeNotch = new List<float>();
        /// <summary>
        /// Tab listing fields values
        /// </summary> 
        private List<float> FieldChangeValues = new List<float>();
        /// <summary>
        /// Set to True if a tab of notch is defined
        /// </summary> 
        /// 
        private DataMatrix2D FieldChangeSpeedUpMatrix;
        private DataMatrix2D FieldChangeSpeedDownMatrix;
        private DataMatrix FieldChangeNotchMatrix;

        private DataMatrix CouplingChangeNotchMatrix;
        public List<float> FieldChangeController = new List<float>();

        public bool FieldChangeByNotch = false;
        /// <summary>
        /// Gearing reduction beetwen motors and wheels
        /// </summary> 
        public float GearingReduction = 0;
        /// <summary>
        /// Armature resistance in ohm
        /// </summary> 
        public float DCMotorInternalR = 0.25f;
        /// <summary>
        /// Field resistance in ohm
        /// </summary> 
        public float DCMotorInductorR = 0.25f;
        /// <summary>
        /// Motor Inductance (time response of current)
        /// </summary> 
        public float DCMotorInductance = 1.5f;

        /// <summary>
        /// Factor generating Back EMF from Flow and Rotation speed
        /// </summary> 
        public float DCMotorBEMFFactor =0 ;
        /// <summary>
        /// Factor converting Amps to Flow
        /// </summary> 
        public float DCMotorAmpToFlowFactor = 0;

        /// <summary>
        /// Number of DC Motors to handle: this number is applied as a factor
        /// </summary> 
        public int DCMotorNumber = 1;

        /// <summary>
        /// Current displayed in game (for each motor)
        /// </summary> 
        public float DisplayedAmperage = 0;
        /// <summary>
        /// Force calculated with DC Motor code
        /// </summary> 
        private float NewMotiveForceN = 0;
        /// <summary>
        /// Original Force
        /// </summary> 
        private float OpenRailsMotiveForceN;

        /// <summary>
        /// LegacyMotiveForce
        /// </summary> 
        private float LegacyMotiveForceN = 0;

        /// <summary>
        /// LegacyMotiveForce
        /// </summary> 
        private int PrevSpeed = 0;
        
        /// <summary>
        /// Previous Noutch
        /// </summary> 
        private float PrevNotch = 0;

        /// <summary>
        /// Displayed Force
        /// </summary> 
        private float DisplayedMotiveForceN = 0;

        /// <summary>
        /// Demanded Voltage. Generator Voltage tries to reach this value
        /// </summary> 
        private float DemandedVoltage = 0;

        /// <summary>
        /// Time From Game Start, used as a timer
        /// </summary> 
        private float TimeFromStart;
        /// <summary>
        /// Previous Time From Game Start, used as a timer
        /// </summary> 
        private float PrevTimeFromStart;
        /// <summary>
        /// Name of Report file (used for tests)
        /// </summary> 
        private string path = @"ReportDCMotor.csv";
        /// <summary>
        /// File Stram for Report file (used for tests)
        /// </summary> 
        private FileStream fs;

        /// <summary>
        /// Heating Calls
        /// </summary> 
        public int HeatingRPMCalls=0;

        /// <summary>
        /// Heating Force Throttle to zero
        /// </summary> 
        public bool HeatingForceThrottleToZero = false;


        public MSTSDieselLocomotive(Simulator simulator, string wagFile)
            : base(simulator, wagFile)
        {
            // Delete the file if it exists.
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                fs = File.OpenWrite(path);
                string ExportString;
                if (IsMetric)
                {
                    ExportString = "SpeedKmh" + ";" + "Time" + ';' + "Throttle" + ";" + "Voltage" + ";" + "U Inductor" + ";" + "BackEMF" + ";" + "TotalR" + ";" + "Amperage" + ";" + "Flow" + ";" + "New Force" + ";" + "Current Force" + ";" + "Power/Motor" + ";" + "AvailPower/Motor" + ";" + "OverLoad" + "\r\n";
                }
                else
                {
                    ExportString = "Speed(mph)" + ";" + "Time" + ';' + "Throttle" + ";" + "Voltage" + ";" + "U Inductor" + ";" + "BackEMF" + ";" + "TotalR" + ";" + "Amperage" + ";" + "Flow" + ";" + "New Force(lbf)" + ";" + "Legacy Force(lbf)" + ";" + "Power/Motor" + ";" + "AvailPower/Motor" + ";" + "OverLoad" + "\r\n";
                }
                byte[] info = new UTF8Encoding(true).GetBytes(ExportString);
                fs.Write(info, 0, info.Length);
            }
            catch
            {
//                Trace.TraceInformation("Export File already used");
            }

            PowerOn = true;
            RefillImmediately();
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public override void Parse(string lowercasetoken, STFReader stf)
        {
            string temp = "";
            switch (lowercasetoken)
            {
                case "engine(dieselengineidlerpm": IdleRPM = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(dieselenginemaxrpm": MaxRPM = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(dieselenginemaxrpmchangerate": MaxRPMChangeRate = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortsdieselenginemaxpower": MaximumDieselEnginePowerW = stf.ReadFloatBlock(STFReader.UNITS.Power, null); break;
                case "engine(effects(dieselspecialeffects": ParseEffects(lowercasetoken, stf); break;
                case "engine(dieselsmokeeffectinitialsmokerate": InitialExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(dieselsmokeeffectinitialmagnitude": InitialMagnitude = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(dieselsmokeeffectmaxsmokerate": MaxExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(dieselsmokeeffectmaxmagnitude": MaxMagnitude = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortsdieselengines": DieselEngines = new DieselEngines(this, stf); break;
                case "engine(maxdiesellevel": MaxDieselLevelL = stf.ReadFloatBlock(STFReader.UNITS.Volume, null); break;
                case "engine(dieselusedperhouratmaxpower": DieselUsedPerHourAtMaxPowerL = stf.ReadFloatBlock(STFReader.UNITS.Volume, null); break;
                case "engine(dieselusedperhouratidle": DieselUsedPerHourAtIdleL = stf.ReadFloatBlock(STFReader.UNITS.Volume, null); break;
                case "engine(maxoilpressure": DieselMaxOilPressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, 120f); break;
                case "engine(ortsminoilpressure": DieselMinOilPressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, 40f); break;
                case "engine(maxtemperature": DieselMaxTemperatureDeg = stf.ReadFloatBlock(STFReader.UNITS.TemperatureDifference, 0); break;
                case "engine(ortsdieselcooling": DieselEngineCooling = (DieselEngine.Cooling)stf.ReadInt((int)DieselEngine.Cooling.Proportional); break;

                //** For test, new UpdateMotiveForce. Would be better in MSTSLocomotive
                case "engine(ortsdcmotorcouplingchange": CouplingChangeNotchMatrix = new DataMatrix(stf); break;

                case "engine(ortsdcmotorinternalr": DCMotorInternalR = stf.ReadFloatBlock(STFReader.UNITS.None, 0.25f); break;
                case "engine(ortsdcmotorinductorr": DCMotorInductorR = stf.ReadFloatBlock(STFReader.UNITS.None, 0.25f); break;
                case "engine(ortsdcmotorinductance": DCMotorInductance = stf.ReadFloatBlock(STFReader.UNITS.None, 1.5f); break;
                case "engine(ortsdcmotorbemffactor": DCMotorBEMFFactor = stf.ReadFloatBlock(STFReader.UNITS.None, 0.005f); break;
                case "engine(ortsdcmotoramptoflowfactor": DCMotorAmpToFlowFactor = stf.ReadFloatBlock(STFReader.UNITS.None, 0.0f); break;
                case "engine(ortsdcmotornumber": DCMotorNumber = stf.ReadIntBlock(1); break;
                case "engine(ortsdcmotorgeneratorvoltage": GeneratorVoltage = stf.ReadFloatBlock(STFReader.UNITS.Voltage, 1500); break;
                case "engine(ortsdcmotorgeneratorlowvoltage": GeneratorLowVoltage = stf.ReadFloatBlock(STFReader.UNITS.Voltage, 0); break;
                case "engine(ortsdcmotorfieldchangenumber": FieldChangeNumber = stf.ReadIntBlock(0); break;
                case "engine(ortsdcmotorfieldspeedup": FieldChangeSpeedUpMatrix = new DataMatrix2D(stf, false); break;
                case "engine(ortsdcmotorfieldspeeddown": FieldChangeSpeedDownMatrix = new DataMatrix2D(stf, false); break;
                case "engine(ortsdcmotorfieldnotch": FieldChangeNotchMatrix = new DataMatrix(stf); FieldChangeByNotch = true; break;
                case "engine(ortsdcmotorusedcforce": UseDCMotorForce = stf.ReadBoolBlock(false); break;
                case "engine(ortsdcmotorgearingreduction": GearingReduction = stf.ReadFloatBlock(STFReader.UNITS.None, 1.0f); break;
                default:
                    GearBox.Parse(lowercasetoken, stf);
                    base.Parse(lowercasetoken, stf); break;
            }

            if (IdleRPM != 0 && MaxRPM != 0 && MaxRPMChangeRate != 0)
            {
                PercentChangePerSec = MaxRPMChangeRate / (MaxRPM - IdleRPM);
                EngineRPM = IdleRPM;
            }
        }

        public override void LoadFromWagFile(string wagFilePath)
        {
            base.LoadFromWagFile(wagFilePath);

            if (Simulator.Settings.VerboseConfigurationMessages)  // Display locomotivve name for verbose error messaging
            {
                Trace.TraceInformation("\n\n ================================================= {0} =================================================", LocomotiveName);
            }

            NormalizeParams();

            // Check to see if Speed of Max Tractive Force has been set - use ORTS value as first priority, if not use MSTS, last resort use an arbitary value.
            if (SpeedOfMaxContinuousForceMpS == 0)
            {
                if (MSTSSpeedOfMaxContinuousForceMpS != 0)
                {
                    SpeedOfMaxContinuousForceMpS = MSTSSpeedOfMaxContinuousForceMpS; // Use MSTS value if present

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to default value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));

                }
                else if (MaxPowerW != 0 && MaxContinuousForceN != 0)
                {
                    SpeedOfMaxContinuousForceMpS = MaxPowerW / MaxContinuousForceN;

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to 'calculated' value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));

                }
                else
                {
                    SpeedOfMaxContinuousForceMpS = 10.0f; // If not defined then set at an "arbitary" value of 22mph

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Speed Of Max Continuous Force: set to 'arbitary' value {0}", FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));

                }
            }

            if (DieselEngines == null)
                DieselEngines = new DieselEngines(this);

            // Create a diesel engine block if none exits, typically for a MSTS or BASIC configuration
            if (DieselEngines.Count == 0)
            {
                DieselEngines.Add(new DieselEngine());

                DieselEngines[0].InitFromMSTS(this);
                DieselEngines[0].Initialize(true);
            }


            // Check initialization of power values for diesel engines
            for (int i = 0; i < DieselEngines.Count; i++)
            {
                DieselEngines[i].InitDieselRailPowers(this);

            }

            if (GearBox != null && GearBox.IsInitialized)
            {
                GearBox.CopyFromMSTSParams(DieselEngines[0]);
                if (DieselEngines[0].GearBox == null)
                {
                    DieselEngines[0].GearBox = GearBox;
                    DieselEngines[0].GearBox.UseLocoGearBox(DieselEngines[0]);
                }
                for (int i = 1; i < DieselEngines.Count; i++)
                {
                    if (DieselEngines[i].GearBox == null)
                        DieselEngines[i].GearBox = new GearBox(GearBox, DieselEngines[i]);
                }

                if (GearBoxController == null)
                {
                    GearBoxController = new MSTSNotchController(GearBox.NumOfGears + 1);
                }
            }

            InitialMassKg = MassKG;

            // If traction force curves not set (BASIC configuration) then check that power values are set, otherwise locomotive will not move.
            if (TractiveForceCurves == null && LocomotiveMaxRailOutputPowerW == 0)
            {
                if (MaxPowerW != 0)
                {

                    LocomotiveMaxRailOutputPowerW = MaxPowerW;  // Set to default power value

                    if (Simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("MaxRailOutputPower (BASIC Config): set to default value = {0}", FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, IsMetric, false, false));
                    }
                }
                else
                {
                    LocomotiveMaxRailOutputPowerW = 2500000.0f; // If no default value then set to arbitary value

                    if (Simulator.Settings.VerboseConfigurationMessages)
                    {
                        Trace.TraceInformation("MaxRailOutputPower (BASIC Config): set at arbitary value = {0}", FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, IsMetric, false, false));
                    }

                }

                
                if (MaximumDieselEnginePowerW == 0)
                {
                    MaximumDieselEnginePowerW = LocomotiveMaxRailOutputPowerW;  // If no value set in ENG file, then set the Prime Mover power to same as RailOutputPower (typically the MaxPower value)

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Diesel Engine Prime Mover Power set the same as MaxRailOutputPower {0} value", FormatStrings.FormatPower(MaximumDieselEnginePowerW, IsMetric, false, false));

                }

            }

            // Check that maximum force value has been set
            if (MaxForceN == 0)
            {

                if (TractiveForceCurves == null)  // Basic configuration - ie no force and Power tables, etc
                {
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    MaxForceN = LocomotiveMaxRailOutputPowerW / StartingSpeedMpS;

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Force set to {0} value, calculated from Rail Power Value.", FormatStrings.FormatForce(MaxForceN, IsMetric));
                }
                else
                {
                    float ThrottleSetting = 1.0f; // Must be at full throttle for these calculations
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    float MaxForceN = TractiveForceCurves.Get(ThrottleSetting, StartingSpeedMpS);

                    if (Simulator.Settings.VerboseConfigurationMessages)
                        Trace.TraceInformation("Maximum Force set to {0} value, calcuated from Tractive Force Tables", FormatStrings.FormatForce(MaxForceN, IsMetric));
                }


            }

            //** Checking DC Motors parameters                                                      **//
            //** Checking DC Motors parameters                                                      **//
            if (TractionMotorType == TractionMotorTypes.DC)
            {
                HasDCMotor = true;
                //** Setting a Max Current if not defined or defined to 0                           **//
                if (MaxCurrentA == 0)
                {
                    MaxCurrentA = 1000;
                    Trace.TraceInformation("DC Motor: No Max Current set, set to " + MaxCurrentA);
                }
                //** Setting a Max Current if not defined or defined to 0                           **//
                if (DCMotorNumber == 0)
                {
                    DCMotorNumber = 1;
                    Trace.TraceInformation("DC Motor: No Motor Number set, set to 1");
                }
                //** Setting Gear Reduction if not defined or defined to 0                          **//
                if (GearingReduction == 0)
                {
                    GearingReduction = 1;
                    Trace.TraceInformation("DC Motor: Gearing Reduction not set, calculated to " + GearingReduction);
                }
                //** If Amp To Flow is not defined or set to 0, calculating one with known values   **//
                if (DCMotorAmpToFlowFactor == 0)
                {
                    DCMotorAmpToFlowFactor = 2 * (MaxForceN / GearingReduction) / (DCMotorNumber * (MaxCurrentA / DCMotorNumber) * (MaxCurrentA / DCMotorNumber));
                    Trace.TraceInformation("DC Motor: Amp To Flow parameter not set, calculated to " + DCMotorAmpToFlowFactor + " from known parameters");
                }
                if (DCMotorBEMFFactor == 0)
                {
                    DCMotorBEMFFactor = 0.005f;
                    Trace.TraceInformation("DC Motor: Back EMF parameter not set, forced to " + DCMotorBEMFFactor);
                }
            }

            // Check force assumptions set for diesel
            if (Simulator.Settings.VerboseConfigurationMessages)
            {

                float ThrottleSetting = 1.0f; // Must be at full throttle for these calculations
                if (TractiveForceCurves == null)  // Basic configuration - ie no force and Power tables, etc
                {
                    float CalculatedMaxContinuousForceN = ThrottleSetting * LocomotiveMaxRailOutputPowerW / SpeedOfMaxContinuousForceMpS;
                    Trace.TraceInformation("Diesel Force Settings (BASIC Config): Max Starting Force {0}, Calculated Max Continuous Force {1} @ speed of {2}", FormatStrings.FormatForce(MaxForceN, IsMetric), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, IsMetric), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));
                    Trace.TraceInformation("Diesel Power Settings (BASIC Config): Prime Mover {0}, Max Rail Output Power {1}", FormatStrings.FormatPower(MaximumDieselEnginePowerW, IsMetric, false, false), FormatStrings.FormatPower(LocomotiveMaxRailOutputPowerW, IsMetric, false, false));

                    if (MaxForceN < MaxContinuousForceN)
                    {
                        Trace.TraceInformation("!!!! Warning: Starting Tractive force {0} is less then Calculated Continuous force {1}, please check !!!!", FormatStrings.FormatForce(MaxForceN, IsMetric), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, IsMetric), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));
                    }

                }
                else // Advanced configuration - 
                {
                    float StartingSpeedMpS = 0.1f; // Assumed starting speed for diesel - can't be zero otherwise error will occurr
                    float StartingForceN = TractiveForceCurves.Get(ThrottleSetting, StartingSpeedMpS);
                    float CalculatedMaxContinuousForceN = TractiveForceCurves.Get(ThrottleSetting, SpeedOfMaxContinuousForceMpS);
                    Trace.TraceInformation("Diesel Force Settings (ADVANCED Config): Max Starting Force {0} Calculated Max Continuous Force {1}, @ speed of {2}", FormatStrings.FormatForce(StartingForceN, IsMetric), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, IsMetric), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));
                    Trace.TraceInformation("Diesel Power Settings (ADVANCED Config): Prime Mover {0}, Max Rail Output Power {1} @ {2} rpm", FormatStrings.FormatPower(DieselEngines.MaxPowerW, IsMetric, false, false), FormatStrings.FormatPower(DieselEngines.MaximumRailOutputPowerW, IsMetric, false, false), MaxRPM);

                    if (StartingForceN < MaxContinuousForceN)
                    {
                        Trace.TraceInformation("!!!! Warning: Calculated Starting Tractive force {0} is less then Calculated Continuous force {1}, please check !!!!", FormatStrings.FormatForce(StartingForceN, IsMetric), FormatStrings.FormatForce(CalculatedMaxContinuousForceN, IsMetric), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));
                    }
                }

                // Check that MaxPower value is realistic - Calculate power - metric - P = F x V
                float CalculatedContinuousPowerW = MaxContinuousForceN * SpeedOfMaxContinuousForceMpS;
                if (MaxPowerW < CalculatedContinuousPowerW)
                {
                    Trace.TraceInformation("!!!! Warning: MaxPower {0} is less then continuous force calculated power {1} @ speed of {2}, please check !!!!", FormatStrings.FormatPower(MaxPowerW, IsMetric, false, false), FormatStrings.FormatPower(CalculatedContinuousPowerW, IsMetric, false, false), FormatStrings.FormatSpeedDisplay(SpeedOfMaxContinuousForceMpS, IsMetric));
                }

                Trace.TraceInformation("===================================================================================================================\n\n");
            }

        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a locomotive already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// NOTE:  you must initialize all the same variables as you parsed above
        /// </summary>
        public override void Copy(MSTSWagon copy)
        {
            base.Copy(copy);  // each derived level initializes its own variables

            MSTSDieselLocomotive locoCopy = (MSTSDieselLocomotive)copy;
            EngineRPM = locoCopy.EngineRPM;
            IdleRPM = locoCopy.IdleRPM;
            MaxRPM = locoCopy.MaxRPM;
            MaxRPMChangeRate = locoCopy.MaxRPMChangeRate;
            MaximumDieselEnginePowerW = locoCopy.MaximumDieselEnginePowerW;
            PercentChangePerSec = locoCopy.PercentChangePerSec;
            LocomotiveMaxRailOutputPowerW = locoCopy.LocomotiveMaxRailOutputPowerW;

            EngineRPMderivation = locoCopy.EngineRPMderivation;
            EngineRPMold = locoCopy.EngineRPMold;

            MaxDieselLevelL = locoCopy.MaxDieselLevelL;
            DieselUsedPerHourAtMaxPowerL = locoCopy.DieselUsedPerHourAtMaxPowerL;
            DieselUsedPerHourAtIdleL = locoCopy.DieselUsedPerHourAtIdleL;

            DieselFlowLps = 0.0f;
            InitialMassKg = MassKG;

            if (this.CarID.StartsWith("0"))
                DieselLevelL = locoCopy.DieselLevelL;
            else
                DieselLevelL = locoCopy.MaxDieselLevelL;

            if (locoCopy.GearBoxController != null)
                GearBoxController = new MSTSNotchController(locoCopy.GearBoxController);

            DieselEngines = new DieselEngines(locoCopy.DieselEngines, this);
            if (DieselEngines[0].GearBox != null) GearBox = DieselEngines[0].GearBox;
            for (int i = 1; i < DieselEngines.Count; i++)
            {
                if (DieselEngines[i].GearBox == null && locoCopy.DieselEngines[i].GearBox != null)
                    DieselEngines[i].GearBox = new GearBox(GearBox, DieselEngines[i]);
            }
            foreach (DieselEngine de in DieselEngines)
            {
                de.Initialize(true);
            }

            HasDCMotor = locoCopy.HasDCMotor;
            GeneratorVoltage = locoCopy.GeneratorVoltage;
            GeneratorLowVoltage = locoCopy.GeneratorLowVoltage;
            GeneratorUnloadedVoltage = locoCopy.GeneratorUnloadedVoltage;
            FieldChangeNumber = locoCopy.FieldChangeNumber;
            FieldChangeSpeedUpMatrix = locoCopy.FieldChangeSpeedUpMatrix;
            FieldChangeSpeedDownMatrix = locoCopy.FieldChangeSpeedDownMatrix;
            FieldChangeNotchMatrix = locoCopy.FieldChangeNotchMatrix;
            FieldChangeSpeedUp = locoCopy.FieldChangeSpeedUp;
            FieldChangeSpeedDown = locoCopy.FieldChangeSpeedDown;
            FieldChangeNotch = locoCopy.FieldChangeNotch;
            FieldChangeValues = locoCopy.FieldChangeValues;
            FieldChangeByNotch = locoCopy.FieldChangeByNotch;
            GearingReduction = locoCopy.GearingReduction;
            DCMotorNumber = locoCopy.DCMotorNumber;
            DCMotorInternalR = locoCopy.DCMotorInternalR;
            DCMotorInductorR = locoCopy.DCMotorInductorR;
            DCMotorInductance = locoCopy.DCMotorInductance;
            DCMotorBEMFFactor = locoCopy.DCMotorBEMFFactor;
            DCMotorAmpToFlowFactor = locoCopy.DCMotorAmpToFlowFactor;
            UseDCMotorForce= locoCopy.UseDCMotorForce;
            CouplingChangeNotchMatrix = locoCopy.CouplingChangeNotchMatrix;

            SecondControllerActive = locoCopy.SecondControllerActive;

        }

        public override void Initialize()
        {
            if (GearBox != null && !GearBox.IsInitialized)
            {
                GearBox = null;
            }

            DieselEngines.Initialize(false);

            base.Initialize();

            // If DrvWheelWeight is not in ENG file, then calculate drivewheel weight freom FoA

            if (DrvWheelWeightKg == 0) // if DrvWheelWeightKg not in ENG file.
            {
                DrvWheelWeightKg = MassKG; // set Drive wheel weight to total wagon mass if not in ENG file
                InitialDrvWheelWeightKg = MassKG; // // set Initial Drive wheel weight as well, as it is used as a reference
            }

            // Initialise water level in steam heat boiler
            if (CurrentLocomotiveSteamHeatBoilerWaterCapacityL == 0 && IsSteamHeatFitted)
            {
                if (MaximumSteamHeatBoilerWaterTankCapacityL != 0)
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = MaximumSteamHeatBoilerWaterTankCapacityL;
                }
                else
                {
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL = L.FromGUK(800.0f);
                }
            }
        }

        /// <summary>
        /// We are saving the game.  Save anything that we'll need to restore the 
        /// status later.
        /// </summary>
        public override void Save(BinaryWriter outf)
        {
            // for example
            // outf.Write(Pan);
            base.Save(outf);
            outf.Write(DieselLevelL);
            outf.Write(CurrentLocomotiveSteamHeatBoilerWaterCapacityL);
            DieselEngines.Save(outf);
            ControllerFactory.Save(GearBoxController, outf);
        }

        /// <summary>
        /// We are restoring a saved game.  The TrainCar class has already
        /// been initialized.   Restore the game state.
        /// </summary>
        public override void Restore(BinaryReader inf)
        {
            base.Restore(inf);
            DieselLevelL = inf.ReadSingle();
            CurrentLocomotiveSteamHeatBoilerWaterCapacityL = inf.ReadSingle();
            DieselEngines.Restore(inf);
            ControllerFactory.Restore(GearBoxController, inf);
            
        }

        //================================================================================================//
        /// <summary>
        /// Set starting conditions  when initial speed > 0 
        /// 

        public override void InitializeMoving()
        {
            base.InitializeMoving();
            WheelSpeedMpS = SpeedMpS;
            DynamicBrakePercent = -1;
            if (DieselEngines[0].GearBox != null && GearBoxController != null)
            {
                DieselEngines[0].GearBox.InitializeMoving();
                DieselEngines[0].InitializeMoving();
                if (IsLeadLocomotive())
                {
                    Train.MUGearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                    Train.AITrainGearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                }
                GearBoxController.CurrentNotch = Train.MUGearboxGearIndex;
                GearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearBoxController.SetValue((float)GearBoxController.CurrentNotch);
            }
            ThrottleController.SetValue(Train.MUThrottlePercent / 100);
            SecondThrottleController.SetValue(Train.MUSecondThrottlePercent / 100);
        }


        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's subsystems.
        /// </summary>
        public override void Update(float elapsedClockSeconds)
        {
            if (TractionMotorType == TractionMotorTypes.DC)
            {
                UpdateDCMotorCurrent(elapsedClockSeconds);
            }
            base.Update(elapsedClockSeconds);

            // The following is not in the UpdateControllers function due to the fact that fuel level has to be calculated after the motive force calculation.
            FuelController.Update(elapsedClockSeconds);
            if (FuelController.UpdateValue > 0.0)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.DieselFuel, CabSetting.Increase, FuelController.CurrentValue * 100);

            // Update water controller for steam boiler heating tank
            if (this.IsLeadLocomotive() && IsSteamHeatFitted)
            {
                WaterController.Update(elapsedClockSeconds);
                if (WaterController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.SteamHeatBoilerWater, CabSetting.Increase, WaterController.CurrentValue * 100);
            }

        }


        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's power supply.
        /// </summary>
        protected override void UpdatePowerSupply(float elapsedClockSeconds)
        {
            DieselEngines.Update(elapsedClockSeconds);
            UpdateElectricalHeat(elapsedClockSeconds);

            ExhaustParticles.Update(elapsedClockSeconds, DieselEngines[0].ExhaustParticles);
            ExhaustMagnitude.Update(elapsedClockSeconds, DieselEngines[0].ExhaustMagnitude);
            ExhaustColorR.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.R);
            ExhaustColorG.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.G);
            ExhaustColorB.Update(elapsedClockSeconds, DieselEngines[0].ExhaustColor.B);

            PowerOn = DieselEngines.PowerOn;
            AuxPowerOn = DieselEngines.PowerOn;
        }

        /// <summary>
        /// This function updates periodically the states and physical variables of the locomotive's controllers.
        /// </summary>
        protected override void UpdateControllers(float elapsedClockSeconds)
        {
            base.UpdateControllers(elapsedClockSeconds);

            //Currently the ThrottlePercent is global to the entire train
            //So only the lead locomotive updates it, the others only updates the controller (actually useless)
            if (this.IsLeadLocomotive() || (!AcceptMUSignals))
            {
                if (GearBoxController != null)
                {
                    GearboxGearIndex = (int)GearBoxController.UpdateAndSetBoost(elapsedClockSeconds, GearBoxControllerBoost);
                }
            }
            else
            {
                if (GearBoxController != null)
                {
                    GearBoxController.UpdateAndSetBoost(elapsedClockSeconds, GearBoxControllerBoost);
                }
            }
        }


        /// <summary>
        /// Updates motor current in DC motors
        /// </summary>
        public void UpdateDCMotorCurrent(float elapsedClockSeconds)
        {
//            Trace.TraceInformation("UpdateDCMotor");
            float FullVoltage = GeneratorVoltage;       //** Max voltage given by alternator/generator, arbitrarily set  //750 for BB63500, 900 for 67300 (electric engine config)
            float R = DCMotorInternalR;                 //** Induced R, fixed, arbitrarily set
            float ShuntedR = DCMotorInductorR;          //** Inductor R, could be modified with field reduction
            float TotalR = R + ShuntedR;                //** Total Resistance (induced+inductor R serialy mounted )
            float Inductance = DCMotorInductance;       //** arbitrarily set, modify time responding of the current
            float TimeResponse = 1 / elapsedClockSeconds;       //** will impact Inductance, linked to time  **//

            float k4 = GearingReduction * 60 / (3.1416f * (WheelRadiusM * 2));   //** conversion factor of abs speed to rot speed

            //** Previous value, used for simulate derivative functions  **//
            float PrevVoltage = Voltage;
            float PrevBackEMF = BackEMF;
            float PrevUInductor = UInductor;
            float PrevIInductor = IInductor;

            float ActualFieldChangeFactor = 1;          //** Field diversion factor, initialy set to 1, modified later
            float WantedNotch = 0;                      //** Used to determine voltage, while Field Change by Notch is active (max voltage supplied before fields changes notches
            float VirtualPercent = 0f;                  //** Virtual notch used to get voltage if Change by Notch is active
            float NotchCount = this.ThrottleController.NotchCount();                        //** Total notch count defined in .eng
            float SerialMotorNumber = 1;

            string ExportString; //** Export to string to txt file

            HeatingVoltage = this.DieselEngines[0].HeatingVoltage;
            float generatorUsedLowVoltage = GeneratorLowVoltage;

            bool HTMode = false;

            try
            {
                SerialMotorNumber = CouplingChangeNotchMatrix.Get((ThrottlePercent / 100));
            }
            catch
            {
                SerialMotorNumber = 1;
            }

            //** Getting Voltage , different if field diversion by speed or notch       **//
            //** If field diverting is set for notches                                  **//
            if (FieldChangeByNotch == true)
            {
                float VoltPerNotch = GeneratorLowVoltage + ((FullVoltage - GeneratorLowVoltage))/(NotchCount - FieldChangeNumber);
                int HeatingNotch = (int)(this.DieselEngines[0].HeatingVoltage / VoltPerNotch);

                //** Demanded voltage is obtained before field diversion, at NotchCount-FieldChangeNumber

                VirtualPercent = (float)(NotchCount / (NotchCount - FieldChangeNumber));
                WantedNotch = VirtualPercent * (ThrottlePercent / 100);
                if (WantedNotch > 1) WantedNotch = 1;
                DemandedVoltage = GeneratorLowVoltage + ((FullVoltage - GeneratorLowVoltage) * WantedNotch);


                if ((this.DieselEngines[0].IsHeatingRPMCommand() == true) && (IsLeadLocomotive() == true)&&(WantedNotch!=0)&&(PrevNotch==0))
                {
                        Voltage = 0;
                }

//                Simulator.Confirmer.Information((HeatingNotch/NotchCount) + " / "+ (PrevNotch * ((NotchCount - FieldChangeNumber)) / (NotchCount)) + " - "+DemandedVoltage+" / "+ this.DieselEngines[0].HeatingVoltage);

                if ((DemandedVoltage < this.DieselEngines[0].HeatingVoltage)&&((PrevNotch*((NotchCount- FieldChangeNumber)) / (NotchCount) > (HeatingNotch/NotchCount))))
                {
//                    HeatingForceThrottleToZero=true;
                    ThrottleToZero();
                }
                    
                PrevNotch = WantedNotch;

            }
            else
            {
                //** Demanded voltage between heating voltage and max voltage   **//
                if ((this.DieselEngines[0].IsHeatingRPMCommand() == true) && (IsLeadLocomotive() == true))
                {
                    generatorUsedLowVoltage = HeatingVoltage;
                    DemandedVoltage = HeatingVoltage + ((FullVoltage - HeatingVoltage) * (ThrottlePercent / 100));
                    if (Voltage < DemandedVoltage)
                    {
                        Voltage += 120 * elapsedClockSeconds;
                    }
                }
                else
                {
                    //** Demanded voltage is proportional to throttle, between low and high voltage    **//
                    DemandedVoltage = GeneratorLowVoltage + ((FullVoltage - GeneratorLowVoltage) * (ThrottlePercent / 100));
                }
            }

            DieselUsablePower = 0;
            foreach (DieselEngine de in DieselEngines)
            {
                if (de.EngineStatus == DieselEngine.Status.Running)
                    DieselUsablePower += de.CurrentDieselOutputPowerW;
            }
            DieselUsablePower -= HeatingAbsorbedPower;

            if (ThrottlePercent > 0)
            {
                //                bool wCurrentPriority = true;


                //                if((wCurrentPriority==false)|| ((DemandedVoltage < (GeneratorLowVoltage + (AbsSpeedMpS / (MaxSpeedMpS / 5)) * (GeneratorLowVoltage + GeneratorVoltage)) * (ThrottlePercent / 100))&&(wCurrentPriority == true)))
                if ((DemandedVoltage < (generatorUsedLowVoltage + (AbsSpeedMpS / (MaxSpeedMpS / 5)) * (generatorUsedLowVoltage + GeneratorVoltage)) * (ThrottlePercent / 100))||(FieldChangeByNotch==true))
                {
                    //** Increasing Generator Voltage smoothly  **//
                    if (Voltage < DemandedVoltage)
                    {
                        //** Testing power after Voltage increase: if voltage*current > available power, voltage=power/current   **//
                        if ((IInductor * (Voltage + (40 * elapsedClockSeconds))) > ((DieselUsablePower) / DCMotorNumber))
                        {
                            Voltage = (DieselUsablePower / PrevIInductor) / DCMotorNumber;
                            OverLoad = true;
                        }
                        else
                        {
                            Voltage += 40 * elapsedClockSeconds; //40V seconds, would be set by parameter as depends on loco.
                        }

                    }
                    else if (Voltage > DemandedVoltage)
                    {
                        Voltage -= 40 * elapsedClockSeconds; //40V seconds, would be set by parameter as depends on loco.
                    }
                    else
                    {
                        if ((IInductor * Voltage) > ((this.DieselEngines.MaxOutputPowerW) / DCMotorNumber))
                        {
                            Voltage = ((DieselUsablePower / DCMotorNumber) / IInductor);
                        }
                    }
                }
                else
                {
                    Voltage = (generatorUsedLowVoltage + (AbsSpeedMpS / (MaxSpeedMpS/5)) * (GeneratorVoltage + generatorUsedLowVoltage)) * (ThrottlePercent / 100);
                    if ((IInductor * Voltage) > ((DieselUsablePower) / DCMotorNumber))
                    {
                        Voltage = ((DieselUsablePower / DCMotorNumber) / IInductor);
                    }
                    HTMode = true;
                }

                //** Near to demanded Voltage, setting it exactly, avoiding oscillations    **//
                if ((Math.Abs(DemandedVoltage - Voltage) * elapsedClockSeconds) < (2 * elapsedClockSeconds))
                    Voltage = DemandedVoltage;


                ShuntedR = DCMotorInductorR;

                //** Calculating Field change factor            **//
                //** Field change is linked to Speed            **//
                if (FieldChangeByNotch == false)
                {
                    if (Train.AccelerationMpSpS.Value > 0)
                    {
                        //** And accelerating                       **//
                        ActualFieldChangeFactor = FieldChangeSpeedUpMatrix.Get((ThrottlePercent / 100), AbsSpeedMpS);

                    }
                    else
                    {
                        //** Or decelerating                        **//
                        ActualFieldChangeFactor = FieldChangeSpeedDownMatrix.Get((ThrottlePercent / 100), AbsSpeedMpS); 
                    }
                }
                else  //** Linked to Notch                            **//
                {
                    //** If notch<=FieldChangeNotch, we use the value       **//
                    ActualFieldChangeFactor = FieldChangeNotchMatrix.Get((ThrottlePercent / 100));
                    ShuntedR = DCMotorInductorR * ActualFieldChangeFactor;

                }

                //** We now have Field R and Armature R                     **//
                TotalR = R + ShuntedR;

                //** Displaying infos, for tests                            **//
                if (IsLeadLocomotive() == true)
                {
                    if (IsMetric)
                    {
                        Simulator.Confirmer.Information("Heating : "+HeatingStatus+", Heating Power :"+HeatingAbsorbedPower+", Speed : " + (int)MpS.ToKpH(AbsSpeedMpS) + "km/h (Rot Speed:" + (int)RotSpeed + "rpm) , De power : " + (int)(DieselUsablePower / 1000) + "KW, Alt=" + (int)Voltage + "V,  Demanded = "+DemandedVoltage+"+V ,BEMF = " + (int)BackEMF + ", R=" + TotalR + " ohm, Field Factor: " + ActualFieldChangeFactor + ", I=" + (int)IInductor + " A, Flow = " + (int)InductFlow + " Wb, F=" + (int)(NewMotiveForceN / 1000) + " KN (total), Overload : " + OverLoad + "(" + (int)(OverLoadValue / 1000) + "Kw), OverAmp = " + OverAmp);
                    }
                    else
                    {
                        Simulator.Confirmer.Information("Heating : " + HeatingStatus + ", Heating Power :" + HeatingAbsorbedPower + ", Speed : " + (int)MpS.ToMpH(AbsSpeedMpS) + "mph (Rot Speed:" + (int)RotSpeed + "rpm) , De power : " + (int)W.ToBhp(DieselUsablePower) + "BHP, Alt=" + (int)Voltage + "V,  Demanded = " + DemandedVoltage+"+V ,BEMF = " + (int)BackEMF + ", R=" + TotalR + " ohm, Field Factor: " + ActualFieldChangeFactor + ", I=" + (int)IInductor + " A, Flow = " + (int)InductFlow + " Wb, F=" + (int)N.ToLbf(NewMotiveForceN) / 1000 + " klbf (total), Overload : " + OverLoad + "(" + (int)W.ToHp(OverLoadValue) + "hp), OverAmp = " + OverAmp);
                    }
                }

                //** Caluclating voltage used for following code, using Back EMF                                    **//
                UInductor = Voltage - PrevBackEMF;

                if (UInductor > Voltage)
                    UInductor = Voltage;   //** Impossible to have a UInductor > Voltage!      **//

            }
            else
            {
                //** Throttle =0, line contactors opened, no voltage                                                **//
                if((this.DieselEngines[0].IsHeatingRPMCommand() == true)&&(IsLeadLocomotive()==true))
                {
//                    Simulator.Confirmer.Information("Heating!");
                    DemandedVoltage = GeneratorVoltage;
                    if(Voltage<DemandedVoltage)
                    {
                        Voltage += 400*elapsedClockSeconds;
                    }
                    TotalR = R + ShuntedR;
                }
                else
                {
                    DemandedVoltage = 0;
                    Voltage = 0;
//                    if(Voltage>DemandedVoltage) Voltage -= 120 * elapsedClockSeconds;
                    TotalR = R + ShuntedR;
                }
            }

            //** Calculating amperage using voltage, prev voltage, Resistance and inductance
            if (AbsSpeedMpS > 0)
            {

                if ((this.DieselEngines[0].IsHeatingRPMCommand() == true) && (ThrottlePercent == 0))
                {
                    UInductor = 0;
                    IInductor = 0;
                }
                else
                {
                    UInductor = PrevVoltage - PrevBackEMF;

                    if (UInductor > Voltage)
                        UInductor = Voltage;   //** Impossible to have a UInductor > Voltage!      **//

                    //** New current is calculated                                                                      **//
                    IInductor = (PrevUInductor - (TotalR) * PrevIInductor) * (1 / (Inductance * TimeResponse)) + PrevIInductor;
                    //** And verified, if exceeding MaxCurrent, limited to this value                                   **//
                    //** In a perfect world, if exceeding value, should open line contactor or damage motors            **//
                    if (IInductor > (MaxCurrentA * SerialMotorNumber) / (DCMotorNumber))
                        IInductor = ((MaxCurrentA * SerialMotorNumber) / DCMotorNumber);
                }

            }
            else
            {
                if ((this.DieselEngines[0].IsHeatingRPMCommand() == true) && (ThrottlePercent == 0)) UInductor = 0;
                else UInductor = Voltage;
                IInductor = (PrevUInductor - (TotalR) * PrevIInductor) * (1 / (Inductance * TimeResponse)) + PrevIInductor;

                if (IInductor > (MaxCurrentA * SerialMotorNumber) / (DCMotorNumber))
                    IInductor = ((MaxCurrentA * SerialMotorNumber) / DCMotorNumber);

                //** Displaying information at speed = 0
                if (IsLeadLocomotive() == true)
                {
                    if (IsMetric)
                    {
                        Simulator.Confirmer.Information("Speed : " + (int)MpS.ToKpH(AbsSpeedMpS) + "km/h (Rot Speed:" + (int)RotSpeed + "rpm) , De power : " + (int)(DieselUsablePower / 1000) + "KW, Alt=" + (int)Voltage + "V (Demanded:" + DemandedVoltage + "V), Full Voltage = " + FullVoltage + "V), R=" + TotalR + " ohm, I=" + (int)IInductor + " A, Flow = " + (int)InductFlow + " Wb, F=" + (int)(NewMotiveForceN / 1000) + " KN (total)");

                    }
                    else
                    {
                        Simulator.Confirmer.Information("Speed : " + (int)MpS.ToMpH(AbsSpeedMpS) + "mph (Rot Speed:" + (int)RotSpeed + "rpm) , De power : " + (int)W.ToBhp(DieselUsablePower) + "BHP, Alt=" + (int)Voltage + "V (Demanded:" + DemandedVoltage + "V), Full Voltage = " + FullVoltage + "V), R=" + TotalR + " ohm, Field Factor: " + ActualFieldChangeFactor + ", I=" + (int)IInductor + " A, Flow = " + (int)InductFlow + " Wb, F=" + (int)N.ToLbf(NewMotiveForceN) / 1000 + " klbf (total), Overload : " + OverLoad + "(" + (int)W.ToHp(OverLoadValue) + "hp), OverAmp = " + OverAmp);
                    }
                }
            }

            //** Verifying overload and overamp.                                                                        **//
            //** If asked power exceed max usable power, overload is set to True, used to flatten generator voltage     **//
            OverLoad = false;

            if ((IInductor * (UInductor + PrevBackEMF)) > (DieselUsablePower / DCMotorNumber))
            {
                OverLoadValue = (IInductor * (UInductor + PrevBackEMF)) - (DieselUsablePower / DCMotorNumber);
                OverLoad = true;
            }

            DCMotorThrottleIncreaseForbidden = false;
            if ((IInductor * (UInductor + PrevBackEMF)) > ((DieselUsablePower / DCMotorNumber)*0.95))
            {
                if (FieldChangeByNotch == true)
                {
                    DCMotorThrottleIncreaseForbidden = true;
//                    Simulator.Confirmer.Information("Throttle up forbidden!");
                }
            }

            //** If Current > Voltage/R, limiting the current: in normal use, could not happen                          **//
            OverAmp = false;
            if (IInductor > (Voltage / (TotalR)))
            {
                IInductor = Voltage / (TotalR);
                OverAmp = true;
            }

            //** Negative Current, not handled, set to 0 (need to be seen for Dynamic braking)                          **//

            if (IInductor < 0)
                IInductor = 0;

            //** Using amperage to calculate flow generated                                                             **//
            if (DCMotorInductorR > 0)
                InductFlow = DCMotorAmpToFlowFactor * IInductor * ActualFieldChangeFactor;

            //** and induced Force
            InducedForce = InductFlow * IInductor;

            //** transmitted to wheels
            WheelForce = InducedForce * GearingReduction;

            //** wheelspeed in m/s converted to rpm
            RotSpeed = k4 * AbsWheelSpeedMpS;

            //** Back EMF, proportional to speed
            BackEMF = InductFlow * RotSpeed * DCMotorBEMFFactor;

            //** Verifying and correcting negative values                                                               **//
            if (BackEMF < 0)
                BackEMF = 0;

            if (BackEMF > Voltage)
                BackEMF = Voltage;

            if (WheelForce < 0)
                WheelForce = 0;

            if (WheelSpeed < 0)
                WheelSpeed = 0;

            if (RotSpeed < 0)
                RotSpeed = 0;

            // ** Motive force set with WheelForce multiplied by number of motors
            NewMotiveForceN = WheelForce * DCMotorNumber;
            if (NewMotiveForceN > MaxForceN)
                NewMotiveForceN = MaxForceN;

            //** Giving value to display in cab                                                                         **//
            DisplayedAmperage = IInductor;

            //** Report writing                                                                                         **//
            if (ThrottlePercent > 0)
            {
                TimeFromStart += elapsedClockSeconds;


                if (this.IsLeadLocomotive())
                {

                    //                    if ((TimeFromStart - PrevTimeFromStart) > 0.5)  //** Writing report every 0.5s
                    //                    {
                    //** Creating String                    **//
                    if (IsMetric)
                    {
                        //** Export every KpH   **//
                        if (PrevSpeed != (int)Math.Round(MpS.ToKpH(AbsSpeedMpS)))
                        {
                            PrevSpeed = (int)Math.Round(MpS.ToKpH(AbsSpeedMpS));
                            ExportString = MpS.ToKpH(AbsSpeedMpS) + ";" + TimeFromStart + ";" + ThrottlePercent + ";" + Voltage + ";" + UInductor + ";" + BackEMF + ";" + TotalR + ";" + IInductor + ";" + InductFlow + ";" + (NewMotiveForceN / 1000) + ";" + (LegacyMotiveForceN / 1000) + ";" + (Voltage * IInductor / 1000) + ";" + ((DieselUsablePower / DCMotorNumber) / 1000) + ";" + OverLoad + "\r\n";
                            //**Export to report file
                            byte[] info = new UTF8Encoding(true).GetBytes(ExportString);
                            fs.Write(info, 0, info.Length);
                            PrevTimeFromStart = TimeFromStart;
                        }

                    }
                    else
                    {
                        //** Export every MpH   **//
                        if (PrevSpeed != (int)Math.Round(MpS.ToKpH(AbsSpeedMpS)))
                        {
                            PrevSpeed = (int)Math.Round(MpS.ToKpH(AbsSpeedMpS));
                            ExportString = MpS.ToMpH(AbsSpeedMpS) + ";" + TimeFromStart + ";" + ThrottlePercent + ";" + Voltage + ";" + UInductor + ";" + BackEMF + ";" + TotalR + ";" + IInductor + ";" + InductFlow + ";" + N.ToLbf(NewMotiveForceN) + ";" + N.ToLbf(LegacyMotiveForceN) + ";" + (Voltage * IInductor / 1000) + ";" + W.ToHp(DieselUsablePower / DCMotorNumber) + ";" + OverLoad + "\r\n";
                            //**Export to report file
                            byte[] info = new UTF8Encoding(true).GetBytes(ExportString);
                            fs.Write(info, 0, info.Length);
                            PrevTimeFromStart = TimeFromStart;
                        }
                    }

                    //                    }
                }

            }

        }

        /// <summary>
        /// This function updates periodically the locomotive's motive force.
        /// </summary>
        protected override void UpdateTractiveForce(float elapsedClockSeconds, float t, float AbsSpeedMpS, float AbsWheelSpeedMpS)
        {
            // This section calculates the motive force of the locomotive as follows:
            // Basic configuration (no TF table) - uses P = F /speed  relationship - requires power and force parameters to be set in the ENG file. 
            // Advanced configuration (TF table) - use a user defined tractive force table
            // With Simple adhesion apart from correction for rail adhesion, there is no further variation to the motive force. 
            // With Advanced adhesion the raw motive force is fed into the advanced (axle) adhesion model, and is corrected for wheel slip and rail adhesion
            if (PowerOn)
            {
                // Appartent throttle setting is a reverse lookup of the throttletab vs rpm, hence motive force increase will be related to increase in rpm. The minimum of the two values
                // is checked to enable fast reduction in tractive force when decreasing the throttle. Typically it will take longer for the prime mover to decrease rpm then drop motive force.
                float LocomotiveApparentThrottleSetting = 0;

                if (IsPlayerTrain)
                {
                    LocomotiveApparentThrottleSetting = Math.Min(t, DieselEngines.ApparentThrottleSetting / 100.0f);
                }
                else // For AI trains, just use the throttle setting
                {
                    LocomotiveApparentThrottleSetting = t;
                }

                LocomotiveApparentThrottleSetting = MathHelper.Clamp(LocomotiveApparentThrottleSetting, 0.0f, 1.0f);  // Clamp decay within bounds

                // If there is more then one diesel engine, and one or more engines is stopped, then the Fraction Power will give a fraction less then 1 depending upon power definitions of engines.
                float DieselEngineFractionPower = 1.0f;

                if (DieselEngines.Count > 1)
                {
                    DieselEngineFractionPower = DieselEngines.RunningPowerFraction;
                }

                DieselEngineFractionPower = MathHelper.Clamp(DieselEngineFractionPower, 0.0f, 1.0f);  // Clamp decay within bounds


                // For the advanced adhesion model, a rudimentary form of slip control is incorporated by using the wheel speed to calculate tractive effort.
                // As wheel speed is increased tractive effort is decreased. Hence wheel slip is "controlled" to a certain extent.
                // This doesn't cover all types of locomotives, for eaxmple if DC traction motors and no slip control, then the tractive effort shouldn't be reduced. This won't eliminate slip, but limits
                // its impact. More modern locomotive have a more sophisticated system that eliminates slip in the majority (if not all circumstances).
                // Simple adhesion control does not have any slip control feature built into it.
                // TODO - a full review of slip/no slip control.
                if (WheelSlip && AdvancedAdhesionModel)
                {
                    AbsTractionSpeedMpS = AbsWheelSpeedMpS;
                }
                else
                {
                    AbsTractionSpeedMpS = AbsSpeedMpS;
                }

                if (TractiveForceCurves == null)
                {
                    // This sets the maximum force of the locomotive, it will be adjusted down if it exceeds the max power of the locomotive.
                    float maxForceN = Math.Min(t * MaxForceN * (1 - PowerReduction), AbsTractionSpeedMpS == 0.0f ? (t * MaxForceN * (1 - PowerReduction)) : (t * LocomotiveMaxRailOutputPowerW / AbsTractionSpeedMpS));

                    // Maximum rail power is reduced by apparent throttle factor and the number of engines running (power ratio)
                    float maxPowerW = LocomotiveMaxRailOutputPowerW * DieselEngineFractionPower * LocomotiveApparentThrottleSetting;

                    // If unloading speed is in ENG file, and locomotive speed is greater then unloading speed, and less then max speed, then apply a decay factor to the power/force
                    if (UnloadingSpeedMpS != 0 && AbsTractionSpeedMpS > UnloadingSpeedMpS && AbsTractionSpeedMpS < MaxSpeedMpS && !WheelSlip)
                    {
                        // use straight line curve to decay power to zero by 2 x unloading speed
                        float unloadingspeeddecay = 1.0f - (1.0f / UnloadingSpeedMpS) * (AbsTractionSpeedMpS - UnloadingSpeedMpS);
                        unloadingspeeddecay = MathHelper.Clamp(unloadingspeeddecay, 0.0f, 1.0f);  // Clamp decay within bounds
                        maxPowerW *= unloadingspeeddecay;
                    }

                    if (DieselEngines.HasGearBox)
                    {
                        TractiveForceN = DieselEngines.TractiveForceN;
                    }
                    else
                    {
                        if (maxForceN * AbsSpeedMpS > maxPowerW)
                            maxForceN = maxPowerW / AbsTractionSpeedMpS;

                        TractiveForceN = maxForceN;
                        // Motive force will be produced until power reaches zero, some locomotives had a overspeed monitor set at the maximum design speed
                    }

                }
                else
                {
                    // Tractive force is read from Table using the apparent throttle setting, and then reduced by the number of engines running (power ratio)

                    TractiveForceN = TractiveForceCurves.Get(LocomotiveApparentThrottleSetting, AbsTractionSpeedMpS) * DieselEngineFractionPower * (1 - PowerReduction);

                    if (TractiveForceN < 0 && !TractiveForceCurves.AcceptsNegativeValues())
                        TractiveForceN = 0;
                }

                DieselFlowLps = DieselEngines.DieselFlowLps;
                partialFuelConsumption += DieselEngines.DieselFlowLps * elapsedClockSeconds;
                if (partialFuelConsumption >= 0.1)
                {
                    DieselLevelL -= partialFuelConsumption;
                    partialFuelConsumption = 0;
                }
                if (DieselLevelL <= 0.0f)
                {
                    PowerOn = false;
                    SignalEvent(Event.EnginePowerOff);
                    foreach (DieselEngine de in DieselEngines)
                    {
                        if (de.EngineStatus != DieselEngine.Status.Stopping || de.EngineStatus != DieselEngine.Status.Stopped)
                            de.Stop();
                    }
                }
            }
            else
            {
                //** Preparing to use DC Motor update    **//
                if (MaxForceN > 0 && MaxContinuousForceN > 0 && PowerReduction < 1)
                {
                    NewMotiveForceN *= 1 - (MaxForceN - MaxContinuousForceN) / (MaxForceN * MaxContinuousForceN) * AverageForceN * (1 - PowerReduction);
                    float w = (ContinuousForceTimeFactor - elapsedClockSeconds) / ContinuousForceTimeFactor;
                    if (w < 0)
                        w = 0;
                    AverageForceN = w * AverageForceN + (1 - w) * NewMotiveForceN;
                    TractiveForceN = NewMotiveForceN;
                }
            }

            if (MaxForceN > 0 && MaxContinuousForceN > 0 && PowerReduction < 1)
            {
                MotiveForceN *= 1 - (MaxForceN - MaxContinuousForceN) / (MaxForceN * MaxContinuousForceN) * AverageForceN * (1 - PowerReduction);
                float w = (ContinuousForceTimeFactor - elapsedClockSeconds) / ContinuousForceTimeFactor;
                if (w < 0)
                    w = 0;
                AverageForceN = w * AverageForceN + (1 - w) * TractiveForceN;
                AverageForceN = w * AverageForceN + (1 - w) * OpenRailsMotiveForceN;
            }
            //** Forcing MotiveForceN or ORForce to be used          **//
            if(UseDCMotorForce==false) TractiveForceN = OpenRailsMotiveForceN;
            else TractiveForceN = NewMotiveForceN;

            //** Force to be displayed                              **//
            DisplayedMotiveForceN = TractiveForceN;
 //           if (IsLeadLocomotive() == true) Simulator.Confirmer.Information("Index : " + (this.GearBox.CurrentGearIndex));

        }

        /// <summary>
        /// This function updates periodically the locomotive's sound variables.
        /// </summary>
        protected override void UpdateSoundVariables(float elapsedClockSeconds)
        {
            EngineRPMRatio = (DieselEngines[0].RealRPM - DieselEngines[0].IdleRPMSave) / (DieselEngines[0].MaxRPM - DieselEngines[0].IdleRPMSave);

            Variable1 = ThrottlePercent / 100.0f;


            // else Variable1 = MotiveForceN / MaxForceN; // Gearbased, Variable1 proportional to motive force
            // allows for motor volume proportional to effort.

            // Refined Variable2 setting to graduate
            if (Variable2 != EngineRPMRatio)
            {
                // We must avoid Variable2 to run outside of [0, 1] range, even temporarily (because of multithreading)
                Variable2 = EngineRPMRatio < Variable2 ?
                    Math.Max(Math.Max(Variable2 - elapsedClockSeconds * PercentChangePerSec, EngineRPMRatio), 0) :
                    Math.Min(Math.Min(Variable2 + elapsedClockSeconds * PercentChangePerSec, EngineRPMRatio), 1);
            }

//            Trace.TraceInformation(EngineRPMRatio+" - "+ DieselEngines[0].RealRPM + " " + DieselEngines[0].IdleRPM+" "+ DieselEngines[0].IdleRPMSave + " " + DieselEngines[0].MaxRPM);


            //            if(Variable4!= (DieselEngines[0].DieselFlowLps * 3600) / DieselEngines[0].DieselConsumptionTab.MaxY())
            //            {
            //                if (Variable4 < (DieselEngines.DieselFlowLps*3600) / DieselEngines[0].DieselConsumptionTab.MaxY())
            //                    Variable4 = Variable4+elapsedClockSeconds*(DieselEngines[0].ChangeUpRPMpS/10000);
            //                else
            //                    Variable4 = Variable4-elapsedClockSeconds * (DieselEngines[0].ChangeDownRPMpS/10000);
            //            }

            //** Variable 4 : related to turbocharger state (ratio between throttle and force)
            float DieselTotalPower = 0;
            float MaxDieselUsedL = 0;
            float ChangeUpRPMpS = DieselEngines[0].ChangeUpRPMpS/60;
            float ChangeDownRPMpS = DieselEngines[0].ChangeDownRPMpS/60;

            foreach (DieselEngine de in DieselEngines)
            {
                if (de.EngineStatus == DieselEngine.Status.Running)
                {
                    DieselTotalPower += de.CurrentDieselOutputPowerW;
                    MaxDieselUsedL += (de.DieselConsumptionTab.MaxY()/3600);
                }
                    
            }

            

            //** Variable 5: related to necessary force / diesel force
            float var5Target = ((HeatingAbsorbedPower + ((MotiveForceN - DynamicBrakeForceN) * SpeedMpS)) / DieselTotalPower);
            if (Variable5 != var5Target)
            {
                if (Variable5 < var5Target)
//                    Variable5 = Variable5 + elapsedClockSeconds * (PercentChangePerSec);
                    Variable5 = Variable5 + elapsedClockSeconds * (ChangeUpRPMpS / 10);
                else
//                    Variable5 = Variable5 - elapsedClockSeconds * (PercentChangePerSec);
                    Variable5 = Variable5 - elapsedClockSeconds * (ChangeDownRPMpS / 10);
            }
            if (Math.Abs(Variable5 - var5Target) < 0.005) Variable5 = var5Target;
            if (Variable5 < 0) Variable5 = 0;
            if (Variable5 > 1) Variable5 = 1;

            //** variable4 : related to turbocharger: proportionnal to diesel flow.... But diesel flow not related to charge!
            float var4Target = ((DieselFlowLps/ MaxDieselUsedL) *0.5f)+((DieselFlowLps / MaxDieselUsedL) * 0.5f* Variable5);
            if(Variable4!= var4Target)
            {
                if (Variable4 < var4Target) Variable4 = Variable4 + elapsedClockSeconds * (ChangeUpRPMpS/100);
                else Variable4 = Variable4 - elapsedClockSeconds * (ChangeDownRPMpS/100);
            }

//            Trace.TraceInformation(var4Target+" -> "+Variable4+" / "+(ChangeDownRPMpS/10));

            if (Math.Abs(Variable4 - var4Target) < 0.005) Variable4 = var4Target;
            if (Variable4 < 0) Variable4 = 0;
            if (Variable4 > 1) Variable4 = 1;


//            Trace.TraceInformation((ChangeDownRPMpS/100) + " - " + (ChangeUpRPMpS / 100) + " / " + PercentChangePerSec + " / " + var4Target + " - " + var5Target);

            EngineRPM = Variable2 * (MaxRPM - IdleRPM) + IdleRPM;

            if (DynamicBrakePercent > 0)
            {
                if (MaxDynamicBrakeForceN == 0)
                    Variable3 = DynamicBrakePercent / 100f;
                else
                    Variable3 = DynamicBrakeForceN / MaxDynamicBrakeForceN;
            }
            else
                Variable3 = 0;

            if (elapsedClockSeconds > 0.0f)
            {
                EngineRPMderivation = (EngineRPM - EngineRPMold) / elapsedClockSeconds;
                EngineRPMold = EngineRPM;
            }
        }

        public override void ChangeGearUp()
        {
            if (DieselEngines[0].GearBox != null)
            {
                if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Semiautomatic)
                {
                    DieselEngines[0].GearBox.AutoGearUp();
                    GearBoxController.SetValue((float)DieselEngines[0].GearBox.NextGearIndex);
                }
            }
        }

        public override void ChangeGearDown()
        {

            if (DieselEngines[0].GearBox != null)
            {
                if (DieselEngines[0].GearBox.GearBoxOperation == GearBoxOperation.Semiautomatic)
                {
                    DieselEngines[0].GearBox.AutoGearDown();
                    GearBoxController.SetValue((float)DieselEngines[0].GearBox.NextGearIndex);
                }
            }
        }

        public override float GetDataOf(CabViewControl cvc)
        {
            float data = 0;

            switch (cvc.ControlType)
            {
                case CABViewControlTypes.GEARS:
                    if (DieselEngines.HasGearBox)
                        data = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                    break;
                case CABViewControlTypes.FUEL_GAUGE:
                    if (cvc.Units == CABViewControlUnits.GALLONS)
                        data = L.ToGUS(DieselLevelL);
                    else
                        data = DieselLevelL;
                    break;
                //** Recup d'infos electriques par une machine thermique    **//
                case CABViewControlTypes.LINE_VOLTAGE:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.PowerSupply.PantographVoltageV;
                            if (cvc.Units == CABViewControlUnits.KILOVOLTS)
                                data /= 1000;

                            break;
                        }
                    }

                    break;

                case CABViewControlTypes.PANTO_DISPLAY:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.Pantographs.State == PantographState.Up ? 1 : 0;
                            break;
                        }
                    }
                    break;

                case CABViewControlTypes.PANTOGRAPH:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.Pantographs[1].CommandUp ? 1 : 0;
                            break;
                        }
                    }
                    break;

                case CABViewControlTypes.PANTOGRAPH2:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.Pantographs[2].CommandUp ? 1 : 0;
                            break;
                        }
                    }
                    break;

                case CABViewControlTypes.PANTOGRAPHS_4:
                case CABViewControlTypes.PANTOGRAPHS_4C:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            if (electricLoco.Pantographs[1].CommandUp && electricLoco.Pantographs[2].CommandUp)
                                data = 2;
                            else if (electricLoco.Pantographs[1].CommandUp)
                                data = 1;
                            else if (electricLoco.Pantographs[2].CommandUp)
                                data = 3;
                            else
                                data = 0;
                            break;

                        }
                    }

                    break;

                case CABViewControlTypes.PANTOGRAPHS_5:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            if (electricLoco.Pantographs[1].CommandUp && electricLoco.Pantographs[2].CommandUp)
                                data = 0; // TODO: Should be 0 if the previous state was Pan2Up, and 4 if that was Pan1Up
                            else if (electricLoco.Pantographs[2].CommandUp)
                                data = 1;
                            else if (electricLoco.Pantographs[1].CommandUp)
                                data = 3;
                            else
                                data = 2;
                            break;

                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_DRIVER_CLOSING_ORDER:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.PowerSupply.CircuitBreaker.DriverClosingOrder ? 1 : 0;
                            break;

                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_DRIVER_OPENING_ORDER:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.PowerSupply.CircuitBreaker.DriverOpeningOrder ? 1 : 0;
                            break;
                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_DRIVER_CLOSING_AUTHORIZATION:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.PowerSupply.CircuitBreaker.DriverClosingAuthorization ? 1 : 0; break;
                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_STATE:
                    foreach (var car in Train.Cars)
                    {

                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            switch (electricLoco.PowerSupply.CircuitBreaker.State)
                            {
                                case CircuitBreakerState.Open:
                                    data = 0;
                                    break;
                                case CircuitBreakerState.Closing:
                                    data = 1;
                                    break;
                                case CircuitBreakerState.Closed:
                                    data = 2;
                                    break;
                            }
                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_CLOSED:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            switch (electricLoco.PowerSupply.CircuitBreaker.State)
                            {
                                case CircuitBreakerState.Open:
                                case CircuitBreakerState.Closing:
                                    data = 0;
                                    break;
                                case CircuitBreakerState.Closed:
                                    data = 1;
                                    break;
                            }
                        }
                    }
                    break;

                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_OPEN:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            switch (electricLoco.PowerSupply.CircuitBreaker.State)
                            {
                                case CircuitBreakerState.Open:
                                case CircuitBreakerState.Closing:
                                    data = 1;
                                    break;
                                case CircuitBreakerState.Closed:
                                    data = 0;
                                    break;
                            }
                        }
                    }
                    break;


                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_AUTHORIZED:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = electricLoco.PowerSupply.CircuitBreaker.ClosingAuthorization ? 1 : 0;
                            break;
                        }
                    }
                    break;


                case CABViewControlTypes.ORTS_CIRCUIT_BREAKER_OPEN_AND_AUTHORIZED:
                    foreach (var car in Train.Cars)
                    {
                        if (car is MSTSElectricLocomotive)
                        {
                            var electricLoco = car as MSTSElectricLocomotive;
                            data = (electricLoco.PowerSupply.CircuitBreaker.State < CircuitBreakerState.Closed && electricLoco.PowerSupply.CircuitBreaker.ClosingAuthorization) ? 1 : 0;
                            break;
                        }
                    }
                    break;


                default:
                    data = base.GetDataOf(cvc);
                    break;
            }

            return data;
        }

        public override string GetStatus()
        {
            var status = new StringBuilder();
            status.AppendFormat("{0} = {1}\n", Simulator.Catalog.GetString("Engine"),
                Simulator.Catalog.GetParticularString("Engine", GetStringAttribute.GetPrettyName(DieselEngines[0].EngineStatus)));

            if (DieselEngines.HasGearBox)
                status.AppendFormat("{0} = {1}\n", Simulator.Catalog.GetString("Gear"), DieselEngines[0].GearBox.CurrentGearIndex < 0
                    ? Simulator.Catalog.GetParticularString("Gear", "N")
                    : (DieselEngines[0].GearBox.CurrentGearIndex + 1).ToString());

            return status.ToString();
        }

        public override string GetDebugStatus()
        {
            var status = new StringBuilder(base.GetDebugStatus());

            if (DieselEngines.HasGearBox)
                status.AppendFormat("\t{0} {1}", Simulator.Catalog.GetString("Gear"), DieselEngines[0].GearBox.CurrentGearIndex);
            status.AppendFormat("\t{0} {1}\t\t{2}\n", 
                Simulator.Catalog.GetString("Fuel"), 
                FormatStrings.FormatFuelVolume(DieselLevelL, IsMetric, IsUK), DieselEngines.GetStatus());

            if (IsSteamHeatFitted && Train.PassengerCarsNumber > 0 && this.IsLeadLocomotive() && Train.CarSteamHeatOn)
            {
                // Only show steam heating HUD if fitted to locomotive and the train, has passenger cars attached, and is the lead locomotive
                // Display Steam Heat info
                status.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}/{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}\t{14}\t{15}\t{16}\t{17}\t{18:N0}\n",
                   Simulator.Catalog.GetString("StHeat:"),
                   Simulator.Catalog.GetString("Press"),
                   FormatStrings.FormatPressure(CurrentSteamHeatPressurePSI, PressureUnit.PSI, MainPressureUnit, true),
                   Simulator.Catalog.GetString("StTemp"),
                   FormatStrings.FormatTemperature(C.FromF(SteamHeatPressureToTemperaturePSItoF[CurrentSteamHeatPressurePSI]), IsMetric, false),
                   Simulator.Catalog.GetString("StUse"),
                   FormatStrings.FormatMass(pS.TopH(Kg.FromLb(CalculatedCarHeaterSteamUsageLBpS)), IsMetric),
                   FormatStrings.h,
                   Simulator.Catalog.GetString("WaterLvl"),
                   FormatStrings.FormatFuelVolume(CurrentLocomotiveSteamHeatBoilerWaterCapacityL, IsMetric, IsUK),
                   Simulator.Catalog.GetString("Last:"),
                   Simulator.Catalog.GetString("Press"),
                   FormatStrings.FormatPressure(Train.LastCar.CarSteamHeatMainPipeSteamPressurePSI, PressureUnit.PSI, MainPressureUnit, true),
                   Simulator.Catalog.GetString("Temp"),
                   FormatStrings.FormatTemperature(Train.LastCar.CarCurrentCarriageHeatTempC, IsMetric, false),
                   Simulator.Catalog.GetString("OutTemp"),
                   FormatStrings.FormatTemperature(Train.TrainOutsideTempC, IsMetric, false),
                   Simulator.Catalog.GetString("NetHt"),
                   Train.LastCar.DisplayTrainNetSteamHeatLossWpTime);
            }


            return status.ToString();
        }

        /// <summary>
        /// Catch the signal to start or stop the diesel
        /// </summary>
        public void StartStopDiesel()
        {
            if (!this.IsLeadLocomotive() && (this.ThrottlePercent == 0))
                PowerOn = !PowerOn;
        }

        public override void SetPower(bool ToState)
        {
            if (ToState)
            {
                foreach (DieselEngine engine in DieselEngines)
                    engine.Start();
                SignalEvent(Event.EnginePowerOn);
            }
            else
            {
                foreach (DieselEngine engine in DieselEngines)
                    engine.Stop();
                SignalEvent(Event.EnginePowerOff);
            }

            base.SetPower(ToState);
        }

        /// <summary>
        /// Returns the controller which refills from the matching pickup point.
        /// </summary>
        /// <param name="type">Pickup type</param>
        /// <returns>Matching controller or null</returns>
        public override MSTSNotchController GetRefillController(uint type)
        {
            MSTSNotchController controller = null;
            if (type == (uint)PickupType.FuelDiesel) return FuelController;
            if (type == (uint)PickupType.FuelWater) return WaterController;
            return controller;
        }

        /// <summary>
        /// Sets step size for the fuel controller basing on pickup feed rate and engine fuel capacity
        /// </summary>
        /// <param name="type">Pickup</param>

        public override void SetStepSize(PickupObj matchPickup)
        {
            if (MaxDieselLevelL != 0)
                FuelController.SetStepSize(matchPickup.PickupCapacity.FeedRateKGpS / MSTSNotchController.StandardBoost / (MaxDieselLevelL * DieselWeightKgpL));
            if (MaximumSteamHeatBoilerWaterTankCapacityL != 0)
                WaterController.SetStepSize(matchPickup.PickupCapacity.FeedRateKGpS / MSTSNotchController.StandardBoost / MaximumSteamHeatBoilerWaterTankCapacityL);
        }

        /// <summary>
        /// Sets coal and water supplies to full immediately.
        /// Provided in case route lacks pickup points for diesel oil.
        /// </summary>
        public override void RefillImmediately()
        {
            FuelController.CurrentValue = 1.0f;
            WaterController.CurrentValue = 1.0f;
        }

        /// <summary>
        /// Returns the fraction of diesel oil already in tank.
        /// </summary>
        /// <param name="pickupType">Pickup type</param>
        /// <returns>0.0 to 1.0. If type is unknown, returns 0.0</returns>
        public override float GetFilledFraction(uint pickupType)
        {
            if (pickupType == (uint)PickupType.FuelDiesel)
            {
                return FuelController.CurrentValue;
            }
            if (pickupType == (uint)PickupType.FuelWater)
            {
                return WaterController.CurrentValue;
            }
            return 0f;
        }

        /// <summary>
        /// Restores the type of gearbox, that was forced to
        /// automatic for AI trains
        /// </summary>
        public override void SwitchToPlayerControl()
        {
            foreach (DieselEngine de in DieselEngines)
            {
                if (de.GearBox != null)
                    de.GearBox.GearBoxOperation = de.GearBox.OriginalGearBoxOperation;
            }
            if (DieselEngines[0].GearBox != null && GearBoxController != null)
            {
                GearBoxController.CurrentNotch = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearboxGearIndex = DieselEngines[0].GearBox.CurrentGearIndex + 1;
                GearBoxController.SetValue((float)GearBoxController.CurrentNotch);
            }

        }

        public override void SwitchToAutopilotControl()
        {
            SetDirection(Direction.Forward);
            foreach (DieselEngine de in DieselEngines)
            {
                if (de.EngineStatus != DieselEngine.Status.Running)
                    de.Initialize(true);
                if (de.GearBox != null)
                    de.GearBox.GearBoxOperation = GearBoxOperation.Automatic;
            }
            base.SwitchToAutopilotControl();
        }

        private void UpdateElectricalHeat(float elapsedClockSeconds)
        {
            int CarCount = 0;
            bool ElectricHeating = Train.CarElectricHeatOn;

            if (ElectricHeating == true)
            {
                if (this.IsLeadLocomotive())
                {
                    foreach (var car in Train.Cars)
                    {
                        if ((car.WagonType != WagonTypes.Engine) && (car.WagonType != WagonTypes.Tender))
                        {
                            CarCount++;
                        }

                    }
                    //                   Console.WriteLine(CarCount+" cars to heat");
                    foreach (var de in this.DieselEngines)
                    {
                        //** RPM>Heating RPM & Voltage>Heating Voltage low value
                        if ((de.HeatingRPM != 0) && (de.RealRPM >= (de.HeatingRPM))&&(Voltage>de.HeatingVoltage)) 
                        {
                            HeatingStatus = true;
                            //                           Simulator.Confirmer.Information("Heating On, " + CarCount + " cars to heat, " + (CarCount * 40000) + "W taken on Diesel Max Power ("+ de.MaximumDieselPowerW + ")");
                            HeatingAskedPower = (CarCount * 40000);
                            if (HeatingAbsorbedPower < HeatingAskedPower) HeatingAbsorbedPower += CarCount*5000 * elapsedClockSeconds;
//                            de.CurrentDieselOutputPowerW -= HeatingAbsorbedPower;
                        }
                        //** Auto cut of heating if voltage lower than 0.95 heating voltage OR RPM < 0.95 heating RPM   **//
                        else if (((de.HeatingRPM != 0) && (de.RealRPM < (de.HeatingRPM*0.95)) || (Voltage < de.HeatingVoltage*0.95))&&(HeatingStatus==true))
                        {
                            HeatingAskedPower = 0;
                            HeatingAbsorbedPower = 0;
                            HeatingStatus = false;
                        }
                    }
                    

                }
            }
            else
            {
                HeatingAskedPower = 0;
                HeatingAbsorbedPower = 0;
                HeatingStatus = false;
            }

        }

        protected override void UpdateCarSteamHeat(float elapsedClockSeconds)
        {
            // Update Steam Heating System

            // TO DO - Add test to see if cars are coupled, if Light Engine, disable steam heating.

            if (IsSteamHeatFitted && this.IsLeadLocomotive())  // Only Update steam heating if train and locomotive fitted with steam heating
            {

                CurrentSteamHeatPressurePSI = SteamHeatController.CurrentValue * MaxSteamHeatPressurePSI;

                // Calculate steam boiler usage values
                // Don't turn steam heat on until pressure valve has been opened, water and fuel capacity also needs to be present, and steam boiler is not locked out
                if (CurrentSteamHeatPressurePSI > 0.1 && CurrentLocomotiveSteamHeatBoilerWaterCapacityL > 0 && DieselLevelL > 0 && !IsSteamHeatBoilerLockedOut)      
                {
                    // Set values for visible exhaust based upon setting of steam controller
                    HeatingSteamBoilerVolumeM3pS = 1.5f * SteamHeatController.CurrentValue;
                    HeatingSteamBoilerDurationS = 1.0f * SteamHeatController.CurrentValue;
                    Train.CarSteamHeatOn = true; // turn on steam effects on wagons

                    // Calculate fuel usage for steam heat boiler
                    float FuelUsageLpS = L.FromGUK(pS.FrompH(TrainHeatBoilerFuelUsageGalukpH[pS.TopH(CalculatedCarHeaterSteamUsageLBpS)]));
                    DieselLevelL -= FuelUsageLpS * elapsedClockSeconds; // Reduce Tank capacity as fuel used.

                    // Calculate water usage for steam heat boiler
                    float WaterUsageLpS = L.FromGUK(pS.FrompH(TrainHeatBoilerWaterUsageGalukpH[pS.TopH(CalculatedCarHeaterSteamUsageLBpS)]));
                    CurrentLocomotiveSteamHeatBoilerWaterCapacityL -= WaterUsageLpS * elapsedClockSeconds; // Reduce Tank capacity as water used.
                }
                else
                {
                    Train.CarSteamHeatOn = false; // turn on steam effects on wagons
                }
                

            }
        }
        public void ToggleElectricHeatingCommand()
        {
            bool IsHeatingCommand = this.DieselEngines.DEList[0].IsHeatingRPMCommand();
            HeatingRPMCalls++;

            if (IsHeatingCommand == false)
            {
                Trace.TraceInformation(HeatingRPMCalls + " : On");
                this.DieselEngines.DEList[0].HeatingRPMCommand(true);
                Train.CarElectricHeatOn = true;
            }
            else
            {
                Trace.TraceInformation(HeatingRPMCalls + " : Off");
                this.DieselEngines.DEList[0].HeatingRPMCommand(false);
                Train.CarElectricHeatOn = false;
            }
        }

        public void TogglePlayerEngine()
        {
            int DieselCount = 0;

            if (ThrottlePercent < 1)
            {
                foreach (var car in Train.Cars)
                {
                    var mstsDieselLocomotive = car as MSTSDieselLocomotive;
                    if (mstsDieselLocomotive != null && mstsDieselLocomotive.AcceptMUSignals)
                    {

                        foreach (DieselEngine de in mstsDieselLocomotive.DieselEngines)
                        {
                            if (((de.DieselSerie == 0) && (DieselCount == 0)) || (de.DieselSerie == 1))
                            {
                                if (de.DieselSerie == 0) DieselCount++;

                                //                    PowerOn = !PowerOn;
                                if (de.EngineStatus == DieselEngine.Status.Stopped)
                                {
                                    de.Start();
                                    SignalEvent(Event.EnginePowerOn); // power on sound hook
                                }
                                if (de.EngineStatus == DieselEngine.Status.Running)
                                {
                                    de.Stop();
                                    SignalEvent(Event.EnginePowerOff); // power off sound hook
                                }
                                Simulator.Confirmer.Confirm(CabControl.PlayerDiesel, DieselEngines.PowerOn ? CabSetting.On : CabSetting.Off);
                            }
                        }
                    }
                }
            }
            else
            {
                Simulator.Confirmer.Warning(CabControl.PlayerDiesel, CabSetting.Warn1);
            }
        }

        //used by remote diesels to update their exhaust
        public void RemoteUpdate(float exhPart, float exhMag, float exhColorR, float exhColorG, float exhColorB)
        {
            ExhaustParticles.ForceSmoothValue(exhPart);
            ExhaustMagnitude.ForceSmoothValue(exhMag);
            ExhaustColorR.ForceSmoothValue(exhColorR);
            ExhaustColorG.ForceSmoothValue(exhColorG);
            ExhaustColorB.ForceSmoothValue(exhColorB);
        }


        //================================================================================================//
        /// <summary>
        /// The method copes with the strange parameters that some british gear-based DMUs have: throttle 
        /// values arrive up to 1000%, and conversely GearBoxMaxTractiveForceForGears are divided by 10.
        /// Apparently MSTS works well with such values. This method recognizes such case and corrects such values.
        /// </summary>
        protected void NormalizeParams()
        {
            // check for wrong GearBoxMaxTractiveForceForGears parameters
            if (GearBox != null && GearBox.mstsParams != null && GearBox.mstsParams.GearBoxMaxTractiveForceForGearsN.Count > 0)
            {
                if (ThrottleController != null && ThrottleController.MaximumValue > 1 && MaxForceN / GearBox.mstsParams.GearBoxMaxTractiveForceForGearsN[0] > 3)
                    // Tricky things have been made with this .eng file, see e.g Cravens 105; let's correct them
                {
                    for (int i = 0; i < GearBox.mstsParams.GearBoxMaxTractiveForceForGearsN.Count; i++)
                        GearBox.mstsParams.GearBoxMaxTractiveForceForGearsN[i] *= ThrottleController.MaximumValue;
                }
                ThrottleController.Normalize(ThrottleController.MaximumValue);
                if(SecondControllerActive==true) SecondThrottleController.Normalize(SecondThrottleController.MaximumValue);
                // correct also .cvf files
                if (CabViewList.Count > 0)
                    foreach (var cabView in CabViewList)
                    {
                        if (cabView.CVFFile != null && cabView.CVFFile.CabViewControls != null && cabView.CVFFile.CabViewControls.Count > 0)
                        {
                            foreach ( var control in cabView.CVFFile.CabViewControls)
                            {
                                if (control is CVCDiscrete && (control.ControlType == (CABViewControlTypes.THROTTLE) || (control.ControlType == CABViewControlTypes.SECOND_THROTTLE)) && (control as CVCDiscrete).Values.Count > 0 && (control as CVCDiscrete).Values[(control as CVCDiscrete).Values.Count - 1] > 1)
                                {
                                    var discreteControl = (CVCDiscrete)control;
                                    for (var i = 0; i < discreteControl.Values.Count; i++)
                                        discreteControl.Values[i] /= ThrottleController.MaximumValue;
                                    if (discreteControl.MaxValue > 0) discreteControl.MaxValue = discreteControl.Values[discreteControl.Values.Count - 1];
                                }
                            }
                        }
                    }
                ThrottleController.MaximumValue = 1;
                if (SecondControllerActive == true) SecondThrottleController.MaximumValue = 1;
            }
            // Check also for very low DieselEngineIdleRPM
            if (IdleRPM < 10) IdleRPM = Math.Max(150, MaxRPM / 10);
        }
    } // class DieselLocomotive
}
