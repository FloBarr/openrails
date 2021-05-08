using Orts.Common;
using Orts.Parsers.Msts;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using ORTS.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Orts.Simulation.RollingStocks.SubSystems.PowerTransmissions
{
    public class MSTSGearBoxParams
    {
        public int GearBoxNumberOfGears = 1;
        public int GearBoxDirectDriveGear = 1;
        public GearBoxOperation GearBoxOperation = GearBoxOperation.Manual;
        public GearBoxEngineBraking GearBoxEngineBraking = GearBoxEngineBraking.None;
        public List<float> GearBoxMaxSpeedForGearsMpS = new List<float>();
        public List<float> GearBoxMinSpeedForGearsMpS = new List<float>();
        public List<bool> GearBoxFreeWheelForGears = new List<bool>();
        public List<bool> GearBoxHydroIsConverter = new List<bool>();
        public List<float> GearBoxMaxTractiveForceForGearsN = new List<float>();
        public float GearBoxOverspeedPercentageForFailure = 150f;
        public float GearBoxBackLoadForceN = 1000;
        public float GearBoxCoastingForceN = 500;
        public float GearBoxUpGearProportion = 0.85f;
        public float GearBoxDownGearProportion = 0.35f;

        public float GearBoxTimeForSpeedChange = 0.01f;
        public bool GearBoxSpeedChanging = false;
        public float GearBoxElapsedTimeForSpeedChange = 0.0f;

        int initLevel;

        public bool IsInitialized { get { return initLevel >= 5; } }
        public bool AtLeastOneParamFound { get { return initLevel >= 1; } }

        public MSTSGearBoxParams()
        {

        }

        public MSTSGearBoxParams(MSTSGearBoxParams copy)
        {
            GearBoxNumberOfGears = copy.GearBoxNumberOfGears;
            GearBoxDirectDriveGear = copy.GearBoxDirectDriveGear;
            GearBoxOperation = copy.GearBoxOperation;
            GearBoxEngineBraking = copy.GearBoxEngineBraking;
            GearBoxMaxSpeedForGearsMpS = new List<float>(copy.GearBoxMaxSpeedForGearsMpS);
            GearBoxMinSpeedForGearsMpS = new List<float>(copy.GearBoxMinSpeedForGearsMpS);
            GearBoxFreeWheelForGears = new List<bool>(copy.GearBoxFreeWheelForGears);
            GearBoxHydroIsConverter = new List<bool>(copy.GearBoxHydroIsConverter);
            GearBoxMaxTractiveForceForGearsN = new List<float>(copy.GearBoxMaxTractiveForceForGearsN);
            GearBoxOverspeedPercentageForFailure = copy.GearBoxOverspeedPercentageForFailure;
            GearBoxBackLoadForceN = copy.GearBoxBackLoadForceN;
            GearBoxCoastingForceN = copy.GearBoxCoastingForceN;
            GearBoxUpGearProportion = copy.GearBoxUpGearProportion;
            GearBoxDownGearProportion = copy.GearBoxDownGearProportion;
            GearBoxSpeedChanging = copy.GearBoxSpeedChanging;
            GearBoxTimeForSpeedChange = copy.GearBoxTimeForSpeedChange;
            GearBoxElapsedTimeForSpeedChange = copy.GearBoxElapsedTimeForSpeedChange;

            initLevel = copy.initLevel;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            string temp = "";
            switch (lowercasetoken)
            {
                case "engine(gearboxnumberofgears": 
                    GearBoxNumberOfGears = stf.ReadIntBlock(1); initLevel++;
                    for (int i = 0; i < GearBoxNumberOfGears; i++)
                    {
                        bool BoolValue = false;
                        GearBoxFreeWheelForGears.Add(BoolValue);
                        GearBoxHydroIsConverter.Add(BoolValue);
                    }
                    break;
                case "engine(gearboxdirectdrivegear": GearBoxDirectDriveGear = stf.ReadIntBlock(1); break; // initLevel++; break;
                case "engine(gearboxoperation":
                    temp = stf.ReadStringBlock("manual");
                    switch (temp)
                    {
                        case "manual": GearBoxOperation = GearBoxOperation.Manual; break;
                        case "automatic": GearBoxOperation = GearBoxOperation.Automatic; break;
                        case "semiautomatic": GearBoxOperation = GearBoxOperation.Semiautomatic; break;
                    }
                    initLevel++;
                    break;
                case "engine(gearboxenginebraking":
                    temp = stf.ReadStringBlock("none");
                    switch (temp)
                    {
                        case "none": GearBoxEngineBraking = GearBoxEngineBraking.None; break;
                        case "all_gears": GearBoxEngineBraking = GearBoxEngineBraking.AllGears; break;
                        case "direct_drive": GearBoxEngineBraking = GearBoxEngineBraking.DirectDrive; break;
                    }
                    initLevel++;
                    break;
                case "engine(gearboxmaxspeedforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxMaxSpeedForGearsMpS.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                            GearBoxMaxSpeedForGearsMpS.Add(stf.ReadFloat(STFReader.UNITS.SpeedDefaultMPH, 10.0f));
                            if (GearBoxMinSpeedForGearsMpS.Count < GearBoxNumberOfGears)
                                GearBoxMinSpeedForGearsMpS.Add(GearBoxMaxSpeedForGearsMpS[i]);
                        }
                        stf.SkipRestOfBlock();
                        initLevel++;
                    }
                    break;
                case "engine(gearboxminspeedforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxMinSpeedForGearsMpS.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                             GearBoxMinSpeedForGearsMpS.Add(stf.ReadFloat(STFReader.UNITS.SpeedDefaultMPH, 10.0f));
                        }
                        Trace.TraceInformation("Min Speeds found");
                        stf.SkipRestOfBlock();
//                        initLevel++;
                    }
                    break;
                case "engine(gearboxfreewheelforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxFreeWheelForGears.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                            bool BoolValue = false;
                            int Value = stf.ReadInt(0);
                            if (Value == 1) BoolValue = true;
                            GearBoxFreeWheelForGears.Add(BoolValue);
                        }
                        stf.SkipRestOfBlock();
//                        initLevel++;
                    }
                    break;
                case "engine(gearboxhydroisconverter":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxHydroIsConverter.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                        {
                            bool BoolValue = false;
                            int Value = stf.ReadInt(0);
                            if (Value == 1) BoolValue = true;
                            GearBoxHydroIsConverter.Add(BoolValue);
                        }
                        stf.SkipRestOfBlock();
//                        initLevel++;
                    }
                    break;
                    
                case "engine(gearboxmaxtractiveforceforgears":
                    temp = stf.ReadItem();
                    if (temp == ")")
                    {
                        stf.StepBackOneItem();
                    }
                    if (temp == "(")
                    {
                        GearBoxMaxTractiveForceForGearsN.Clear();
                        for (int i = 0; i < GearBoxNumberOfGears; i++)
                            GearBoxMaxTractiveForceForGearsN.Add(stf.ReadFloat(STFReader.UNITS.Force, 10000.0f));
                        stf.SkipRestOfBlock();
                        initLevel++;
                    }
                    break;
                case "engine(gearboxtimeforspeedchange": 
                    GearBoxTimeForSpeedChange = stf.ReadFloatBlock(STFReader.UNITS.Time, 0.0f);
 //                   Trace.TraceInformation("Time for Speed Change : " + GearBoxTimeForSpeedChange);
                    break;

                case "engine(gearboxoverspeedpercentageforfailure": GearBoxOverspeedPercentageForFailure = stf.ReadFloatBlock(STFReader.UNITS.None, 150f); break; // initLevel++; break;
                case "engine(gearboxbackloadforce": GearBoxBackLoadForceN = stf.ReadFloatBlock(STFReader.UNITS.Force, 0f); break;
                case "engine(gearboxcoastingforce": GearBoxCoastingForceN = stf.ReadFloatBlock(STFReader.UNITS.Force, 0f); break;
                case "engine(gearboxupgearproportion": GearBoxUpGearProportion = stf.ReadFloatBlock(STFReader.UNITS.None, 0.85f); break; // initLevel++; break;
                case "engine(gearboxdowngearproportion": GearBoxDownGearProportion = stf.ReadFloatBlock(STFReader.UNITS.None, 0.25f); break; // initLevel++; break;
                default: break;
            }
        }
    }

    public class GearBox
    {
        public MSTSGearBoxParams mstsParams = new MSTSGearBoxParams();
        DieselEngine DieselEngine;
        public List<Gear> Gears = new List<Gear>();

        public Gear CurrentGear
        {
            get
            {
                if ((currentGearIndex >= 0)&&(currentGearIndex < NumOfGears))
                    return Gears[currentGearIndex];
                else
                    return null;
            }
        }

        public int CurrentGearIndex { get { return currentGearIndex; } }
        public Gear NextGear 
        {
            get
            {
                if ((nextGearIndex >= 0)&&(nextGearIndex < NumOfGears))
                    return Gears[nextGearIndex];
                else
                    return null;
            }
            set
            {
                switch(GearBoxOperation)
                {
                    case GearBoxOperation.Manual:
                    case GearBoxOperation.Semiautomatic:
                        int temp = 0;
                        if(value == null)
                            nextGearIndex = -1;
                        else
                        {
                            foreach (Gear gear in Gears)
                            {
                                temp++;
                                if (gear == value)
                                {
                                    break;
                                }
                            }
                            nextGearIndex = temp - 1;
                        }
                        break;
                    case GearBoxOperation.Automatic:
                        break;
                }
            }
        }

        public int NextGearIndex { get { return nextGearIndex; } }

        private bool gearedUp;
        private bool gearedDown;
        public bool GearedUp { get { return gearedUp; } }
        public bool GearedDown { get { return gearedDown; } }

        public bool AutoGearUp()
        {
            if (clutch < 0.05f)
            {
                if (!gearedUp)
                {
                    if(++nextGearIndex >= Gears.Count)
                        nextGearIndex =  (Gears.Count - 1);
                    else
                    {
                        gearedUp = true;
//                        Trace.TraceInformation("Speed Up");
                        if (currentGearIndex>=0)
                            SpeedChanging = true;
                    }
                }
                else
                    gearedUp = false;
            }
            return gearedUp;
        }

        public bool AutoGearDown()
        {
            if ((clutch < 0.05f)||(CurrentGear.MaxSpeedMpS != CurrentGear.MinSpeedMpS))  // using clutch, or speed trigger if defined
            {
                if (!gearedDown)
                {
                    if (--nextGearIndex <= 0)
                    {
                        nextGearIndex = 0;
                    }
                    else
                    {
 //                       Trace.TraceInformation("GearedDown = true, speed = "+CurrentSpeedMpS+" mph");
                        gearedDown = true;
                        if (currentGearIndex>=0)
                        {
                            SpeedChanging = true;
                        }
                    }
                        
                }
                else
                {
                    gearedDown = false;
                }
                    
            }
            return gearedDown;
        }

        public void AutoAtGear()
        {
            gearedUp = false;
            gearedDown = false;
        }

        public bool clutchOn;
        public bool IsClutchOn
        {
            get
            {
                if (DieselEngine.locomotive.ThrottlePercent > 0)
                {
                    if (ShaftRPM >= (CurrentGear.DownGearProportion * DieselEngine.MaxRPM))
                        clutchOn = true;
                    //** If the current gear is a couple converter, the "clutch" is always off
                    if (CurrentGear.Converter == true) clutchOn = false;
                }
                if (ShaftRPM < DieselEngine.StartingRPM)
                    clutchOn = false;

                return clutchOn;
            }
        }

        public int NumOfGears { get { return Gears.Count; } }

        int currentGearIndex = -1;
        int nextGearIndex = -1;

        public float CurrentSpeedMpS 
        {
            get
            {
                if(DieselEngine.locomotive.Direction == Direction.Reverse)
                    return -(DieselEngine.locomotive.SpeedMpS);
                else
                    return (DieselEngine.locomotive.SpeedMpS);
            }
        }

        public float ShaftRPM 
        {
            get
            {
                if (CurrentGear == null)
                    return DieselEngine.RealRPM;
                else
                    return CurrentSpeedMpS / CurrentGear.Ratio; 
            }
        }

        public bool IsOverspeedError
        {
            get
            {
                if (CurrentGear == null)
                    return false;
                else
                    return ((DieselEngine.RealRPM / DieselEngine.MaxRPM * 100f) > CurrentGear.OverspeedPercentage); 
            } 
        }

        public bool IsOverspeedWarning 
        {
            get
            {
                if (CurrentGear == null)
                    return false;
                else
                    return ((DieselEngine.RealRPM / DieselEngine.MaxRPM * 100f) > 100f); 
            }
        }

        float clutch;
        public float ClutchPercent { set { clutch = (value > 100.0f ? 100f : (value < -100f ? -100f : value)) / 100f; }
            get { return clutch * 100f; } }

        public bool AutoClutch = true;

        public GearBoxOperation GearBoxOperation = GearBoxOperation.Manual;
        public GearBoxOperation OriginalGearBoxOperation = GearBoxOperation.Manual;

        public bool AdvancedGearBox = false;
        public float TimeForSpeedChange;
        public float ElapsedTimeForSpeedChanging;
        public bool SpeedChanging;

        public float TractiveForceN
        {
            get
            {
                if (CurrentGear != null)
                {
                    if (ClutchPercent >= -20)
                    {
                        //float motiveForceN = DieselEngine.DemandedThrottlePercent / 100f * CurrentGear.MaxTractiveForceN;
                        //if (CurrentSpeedMpS > 0)
                        //{
                        //    if (motiveForceN > (DieselEngine.MaxOutputPowerW / CurrentSpeedMpS))
                        //        motiveForceN = DieselEngine.MaxOutputPowerW / CurrentSpeedMpS;
                        //}

                        float tractiveForceN = DieselEngine.DieselTorqueTab[DieselEngine.RealRPM] * DieselEngine.DemandedThrottlePercent / DieselEngine.DieselTorqueTab.MaxY() * 0.01f * CurrentGear.MaxTractiveForceN;
                        if (CurrentSpeedMpS > 0)
                        {
                            if (tractiveForceN > (DieselEngine.CurrentDieselOutputPowerW/ CurrentSpeedMpS))
                                tractiveForceN = DieselEngine.CurrentDieselOutputPowerW / CurrentSpeedMpS;
                        }
                        return tractiveForceN;
                    }
                    else
                        return -CurrentGear.CoastingForceN * (100f + ClutchPercent) / 100f;
                }
                else
                    return 0;
            }
        }

        public GearBox() { }

        public GearBox(GearBox copy, DieselEngine de)
        {
            mstsParams = new MSTSGearBoxParams(copy.mstsParams);
            DieselEngine = de;

            CopyFromMSTSParams(DieselEngine);

        }      

        

        public void Parse(string lowercasetoken, STFReader stf)
        {
            mstsParams.Parse(lowercasetoken, stf);
        }

        public bool IsRestored;

        public void Restore(BinaryReader inf)
        {
            currentGearIndex = inf.ReadInt32();
            nextGearIndex = inf.ReadInt32();
            gearedUp = inf.ReadBoolean();
            gearedDown = inf.ReadBoolean();
            clutchOn = inf.ReadBoolean();
            clutch = inf.ReadSingle();
            IsRestored = true;
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write(currentGearIndex);
            outf.Write(nextGearIndex);
            outf.Write(gearedUp);
            outf.Write(gearedDown);
            outf.Write(clutchOn);
            outf.Write(clutch);
        }

        public void InitializeMoving ()
        {
            for (int iGear = 0; iGear < Gears.Count; iGear++)
            {
                if (Gears[iGear].MaxSpeedMpS < CurrentSpeedMpS) continue;
                else currentGearIndex = nextGearIndex = iGear;
                break;
            } 
            gearedUp = false;
            gearedDown = false;
            clutchOn = true;
            clutch = 0.4f;
            DieselEngine.RealRPM = ShaftRPM;
        }

        public bool IsInitialized { get { return mstsParams.IsInitialized; } }

        public void UseLocoGearBox (DieselEngine dieselEngine)
        {
            DieselEngine = dieselEngine;
        }

        public void CopyFromMSTSParams(DieselEngine dieselEngine)
        {
            if (mstsParams != null)
            {
                if ((!mstsParams.IsInitialized) && (mstsParams.AtLeastOneParamFound))
                    Trace.TraceWarning("Some of the gearbox parameters are missing! Default physics will be used.");
                for (int i = 0; i < mstsParams.GearBoxNumberOfGears; i++)
                {
                    Gears.Add(new Gear(this));
                    Gears[i].BackLoadForceN = mstsParams.GearBoxBackLoadForceN;
                    Gears[i].CoastingForceN = mstsParams.GearBoxCoastingForceN;
                    Gears[i].DownGearProportion = mstsParams.GearBoxDownGearProportion;
                    Gears[i].IsDirectDriveGear = (mstsParams.GearBoxDirectDriveGear == mstsParams.GearBoxNumberOfGears);
                    Gears[i].MaxSpeedMpS = mstsParams.GearBoxMaxSpeedForGearsMpS[i];
                    Gears[i].MinSpeedMpS = mstsParams.GearBoxMinSpeedForGearsMpS[i];
                    if (Gears[i].MinSpeedMpS != Gears[i].MaxSpeedMpS) AdvancedGearBox = true;
                    Gears[i].FreeWheel = mstsParams.GearBoxFreeWheelForGears[i];
                    Gears[i].Converter = mstsParams.GearBoxHydroIsConverter[i];
                    Gears[i].MaxTractiveForceN = mstsParams.GearBoxMaxTractiveForceForGearsN[i];
                    Gears[i].OverspeedPercentage = mstsParams.GearBoxOverspeedPercentageForFailure;
                    Gears[i].UpGearProportion = mstsParams.GearBoxUpGearProportion;
                    Gears[i].Ratio = mstsParams.GearBoxMaxSpeedForGearsMpS[i] / dieselEngine.MaxRPM;
                }
                GearBoxOperation = mstsParams.GearBoxOperation;
                OriginalGearBoxOperation = mstsParams.GearBoxOperation;

                SpeedChanging= mstsParams.GearBoxSpeedChanging;
                TimeForSpeedChange = mstsParams.GearBoxTimeForSpeedChange;
                ElapsedTimeForSpeedChanging = mstsParams.GearBoxElapsedTimeForSpeedChange;

            }
        }

        public void Update(float elapsedClockSeconds)
        {
            if((SpeedChanging==true)&&(TimeForSpeedChange>ElapsedTimeForSpeedChanging))
            {
                ElapsedTimeForSpeedChanging += elapsedClockSeconds;
 //               Trace.TraceInformation("Speed Change : next speed is " + currentGearIndex + " in " + (TimeForSpeedChange - ElapsedTimeForSpeedChanging));

                if (ElapsedTimeForSpeedChanging> TimeForSpeedChange)
                {
                    SpeedChanging = false;
                    ElapsedTimeForSpeedChanging = 0;
                }
            }
           
            if (((clutch <= 0.05) || (clutch >= 1f))||(AdvancedGearBox==true))
            {
                if (currentGearIndex < nextGearIndex)
                {
                    DieselEngine.locomotive.SignalEvent(Event.GearUp);
                    currentGearIndex = nextGearIndex;
                }
            }
            if (((clutch <= 0.05) || (clutch >= 0.5f))|| (AdvancedGearBox == true))
            {
                if (currentGearIndex > nextGearIndex)
                {
                    DieselEngine.locomotive.SignalEvent(Event.GearDown);
                    currentGearIndex = nextGearIndex;
                }
            }

            if (DieselEngine.EngineStatus == DieselEngine.Status.Running)
            {
                switch (GearBoxOperation)
                {
                    case GearBoxOperation.Manual:
                        if (DieselEngine.locomotive.ThrottlePercent == 0)
                        {
                            clutchOn = false;
                            ClutchPercent = 0f;
                        }
                        break;
                    case GearBoxOperation.Automatic:
                    case GearBoxOperation.Semiautomatic:
                        if ((CurrentGear != null))
                        {
                            if (AdvancedGearBox==true)
                            {
                                if ((CurrentSpeedMpS > (CurrentGear.MaxSpeedMpS*(0.75+(0.25* (DieselEngine.locomotive.ThrottlePercent/100))))))
                                    AutoGearUp();
                                else if ((CurrentSpeedMpS < (CurrentGear.MinSpeedMpS * (0.75 + (0.25 * (DieselEngine.locomotive.ThrottlePercent/100))))))
                                {
                                    AutoGearDown();
                                }
                                    
                                else
                                    AutoAtGear();
                            }
                            else
                            {
                                if ((CurrentSpeedMpS > (DieselEngine.MaxRPM * CurrentGear.UpGearProportion * CurrentGear.Ratio)))// && (!GearedUp) && (!GearedDown))
                                    AutoGearUp();
                                else
                                {
                                    if ((CurrentSpeedMpS < (DieselEngine.MaxRPM * CurrentGear.DownGearProportion * CurrentGear.Ratio)))// && (!GearedUp) && (!GearedDown))
                                        AutoGearDown();
                                    else
                                        AutoAtGear();
                                }
                            }

                            if (DieselEngine.locomotive.ThrottlePercent == 0)
                            {
                                if ((CurrentGear != null) || (NextGear == null))
                                {
                                    nextGearIndex = -1;
                                    currentGearIndex = -1;
                                    clutchOn = false;
                                    gearedDown = false;
                                    gearedUp = false;
                                }

                            }
                        }
                        else
                        {
                            if ((DieselEngine.locomotive.ThrottlePercent > 0))
                                AutoGearUp();
                            else
                            {
                                nextGearIndex = -1;
                                currentGearIndex = -1;
                                clutchOn = false;
                                gearedDown = false;
                                gearedUp = false;
                            }
                        }
                        break;
                }
            }
            else
            {
                nextGearIndex = -1;
                currentGearIndex = -1;
                clutchOn = false;
                gearedDown = false;
                gearedUp = false;
            }

        }

    }

    public enum GearBoxOperation
    {
        Manual,
        Automatic,
        Semiautomatic
    }

    public enum GearBoxEngineBraking
    {
        None,
        DirectDrive,
        AllGears
    }

    public class Gear
    {
        public bool IsDirectDriveGear;
        public float MaxSpeedMpS;
        public float MinSpeedMpS;
        public float MaxTractiveForceN;
        public float OverspeedPercentage;
        public float BackLoadForceN;
        public float CoastingForceN;
        public float UpGearProportion;
        public float DownGearProportion;
        public bool FreeWheel;
        public bool Converter;

        public float Ratio = 1f;

        public GearBox GearBox;

        public Gear(GearBox gb) { GearBox = gb; }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
