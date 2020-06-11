using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Common;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using ORTS.Common;
using Swan;
using System;
using System.Collections.Generic;


namespace Orts.Viewer3D.Popups
{
    class EnginesOperationsLabel : Label
    {
        readonly Viewer Viewer;
        readonly int CarPosition;

        public EnginesOperationsLabel(int x, int y, Viewer viewer, TrainCar car, int carPosition, LabelAlignment alignment)
            : base(x, y, "", alignment)
        {
            Viewer = viewer;
            CarPosition = carPosition;
            int wPlayers = 0;
            int wHelpers = 0;
            string wTemp = "";

            if (car is MSTSDieselLocomotive)
            {
                //** Counting each kind of diesel (player / helper) for display     **//
                foreach (var de in (car as MSTSDieselLocomotive).DieselEngines)
                {
                    if ((de.DieselSerie == 0) && (car.Train.IsPlayerDriven) || (de.DieselSerie == 1)) wPlayers++;
                    else wHelpers++;
                }
                if (wPlayers != 0) wTemp += " Pl: " + wPlayers;
                if (wHelpers != 0) wTemp += " He: " + wHelpers;
                Text = (car as MSTSLocomotive).LocomotiveName.SliceLength(0, 25) + " (" + (car as MSTSDieselLocomotive).DieselEngines.Count + " De : " + wTemp + ")";
            }
            else if (car is MSTSElectricLocomotive)
            {
                //** Display of current panto & circuit breaker state               **//
                Text = (car as MSTSLocomotive).LocomotiveName + "( Pto : " + (car as MSTSElectricLocomotive).Pantographs.State + " C B : " + (car as MSTSElectricLocomotive).PowerSupply.CircuitBreaker.State + ")";
            }
            Click += new Action<Control, Point>(EnginesOperationsLabel_Click);
        }

        void EnginesOperationsLabel_Click(Control arg1, Point arg2)
        {
            //** Positionning and displaying locomotive operations window           **//
            int xPos = Viewer.EnginesOperationsWindow.Location.Right;
            int yPos = arg1.Position.Y + Viewer.EnginesOperationsWindow.Location.Top;
            int OldPos = Viewer.LocomotiveOperationsWindow.CarPosition;
            Viewer.LocomotiveOperationsWindow.MoveTo(xPos, yPos);
            Viewer.LocomotiveOperationsWindow.CarPosition = CarPosition;
            Viewer.LocomotiveOperationsWindow.DieselNumber = 0;
            if (Viewer.LocomotiveOperationsWindow.Visible == false) Viewer.LocomotiveOperationsWindow.Visible = true;
            else if (CarPosition == OldPos) Viewer.LocomotiveOperationsWindow.Visible = false;


        }
    }

    public class EnginesOperationsWindow : Window
    {
        readonly Viewer Viewer;

        public int CarPosition
        {
            set;
            get;
        }

        public EnginesOperationsWindow(WindowManager owner)
            : base(owner, Window.DecorationSize.X + owner.TextFontDefault.Height * 19, Window.DecorationSize.Y + owner.TextFontDefault.Height * 10 + ControlLayout.SeparatorSize * 9, Viewer.Catalog.GetString("Engines Operation Menu"))
        {
            Viewer = owner.Viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            var vbox = base.Layout(layout).AddLayoutVertical();
            int Count = 0;
            int wDiesels = 0;

            int wHeight = Window.DecorationSize.Y + Owner.TextFontDefault.Height * 10 + ControlLayout.SeparatorSize * 9;
            int wSeparators = 0;
            int wLines = 0;
            bool PrecIsLoco = false;
            int NumberOfCars = 0;

            Label buttonClose, buttonToggleDPUSetup, buttonCarsInTrain;
            Dictionary<object, Label> d;
            d = new Dictionary<object, Label>();

            var hbox = base.Layout(layout).AddLayoutVertical();

            foreach (var car in Viewer.PlayerTrain.Cars)
            {


                string TempName = "";

                if (car is MSTSLocomotive)
                {
                    //** Displaying the number of cars between locos        **//
                    if (NumberOfCars != 0)
                    {
                        vbox.Add(buttonCarsInTrain = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, NumberOfCars + " cars", LabelAlignment.Center));
                        vbox.AddHorizontalSeparator();
                        
                        NumberOfCars = 0;
                        wLines++;
                        wSeparators++;
                    }
                    TempName = "Loco" + Count.ToString();
                    Label wLabel;

                    vbox.Add(wLabel = new EnginesOperationsLabel(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer, car, Count, LabelAlignment.Center));

                    wLabel.Color = Color.White;
                    vbox.AddHorizontalSeparator();
                    if (car is MSTSDieselLocomotive) wDiesels++;

                    wLines++;

                    wSeparators++;
                    PrecIsLoco = true;
                }
                else
                {
                    if (PrecIsLoco == true)
                                        {
                    PrecIsLoco = false;
                                        }
                    NumberOfCars++;

//                    vbox.AddHorizontalSeparator();
                }

                Count++;
        }

            //** Diesels are present in the train, displaying Players and Helpers toggle buttons        **//
            if (wDiesels != 0)
            {
                Label buttonTogglePlayerEngines, buttonToggleHelpersEngines;
                vbox.AddHorizontalSeparator();
                vbox.Add(buttonTogglePlayerEngines = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Toggle Player Engines"), LabelAlignment.Center));
                vbox.AddHorizontalSeparator();
                vbox.Add(buttonToggleHelpersEngines = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Toggle Helpers Engines"), LabelAlignment.Center));

                wLines += 2;
                wSeparators += 2;

                buttonTogglePlayerEngines.Click += new Action<Control, Point>(buttonTogglePlayerEngines_Click);
                buttonToggleHelpersEngines.Click += new Action<Control, Point>(buttonToggleHelpersEngines_Click);
            }

            vbox.AddHorizontalSeparator();
            vbox.Add(buttonToggleDPUSetup = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("DPU Automatic Setup"), LabelAlignment.Center));
            vbox.AddHorizontalSeparator();
            vbox.Add(buttonClose = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Close window"), LabelAlignment.Center));

            buttonClose.Click += new Action<Control, Point>(buttonClose_Click);
            buttonToggleDPUSetup.Click += new Action<Control, Point>(buttonToggleDPUSetup_Click);

            wLines += 2;
            wSeparators += 2;

            wHeight = Window.DecorationSize.Y + (Owner.TextFontDefault.Height * wLines) + (ControlLayout.SeparatorSize * wSeparators);
            Viewer.EnginesOperationsWindow.SizeTo(Viewer.EnginesOperationsWindow.Location.Width, wHeight);

            return vbox;
        }

        void buttonTest_Click(Control arg1, Point arg2)
        {

            //            Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Loco "+CarPosition+" "+arg1.Position.Y));
        }


        void buttonClose_Click(Control arg1, Point arg2)
        {
            Visible = false;
            if (Viewer.LocomotiveOperationsWindow.Visible == true) Viewer.LocomotiveOperationsWindow.Visible = false;
        }

        void buttonTogglePlayerEngines_Click(Control arg1, Point arg2)
        {
            foreach (var car in Viewer.PlayerTrain.Cars)
            {
                if (car is MSTSDieselLocomotive)
                {
                    (car as MSTSDieselLocomotive).TogglePlayerEngine();
                }
            }
        }

        void buttonToggleHelpersEngines_Click(Control arg1, Point arg2)
        {
            foreach (var car in Viewer.PlayerTrain.Cars)
            {
                if (car is MSTSDieselLocomotive)
                {
                    (car as MSTSDieselLocomotive).ToggleHelpersEngine();
                }
            }

        }


        void buttonToggleDPUSetup_Click(Control arg1, Point arg2)
        {
            //** Automatic setup of leading and helpers unit, if a ranking of diesel doesn't exist  **//
            bool IsALocomotive = false;
            bool FirstGroupLocomotive = false;
            int count = 0;
            int Leading = 0;

            foreach (var car in Viewer.PlayerTrain.Cars)
            {
                if (car is MSTSDieselLocomotive)
                {
                    MSTSDieselLocomotive Loco = car as MSTSDieselLocomotive;

                    if (IsALocomotive == false)
                    {
                        FirstGroupLocomotive = true;
                        IsALocomotive = true;
                        Leading++;
                    }
                    else
                    {
                        FirstGroupLocomotive = false;
                        IsALocomotive = true;
                    }
                    (car as MSTSDieselLocomotive).DPUSet = true;

                    count++;
                    foreach (var de in Loco.DieselEngines)
                    {
                        if (de.DieselSerie == 0)
                        {
                            if (FirstGroupLocomotive == true) de.DieselSerie = 1;
                            else de.DieselSerie = 2;
                        }
                    }

                }
                else
                {
                    FirstGroupLocomotive = false;
                    IsALocomotive = false;
                }

                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("DPU Set for " + count + " Locomotives : " + Leading + " Leading units, " + (count - Leading) + " MUed"));

            }
        }

        public override void PrepareFrame(ElapsedTime elapsedTime, bool updateFull)
        {
            if (updateFull)
            {
                Layout();
            }
            base.PrepareFrame(elapsedTime, updateFull);
        }

        void buttonHandbrake_Click(Control arg1, Point arg2)
        {
            new WagonHandbrakeCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).GetTrainHandbrakeStatus());
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).GetTrainHandbrakeStatus())
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Handbrake set"));
            else
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Handbrake off"));
        }

        void buttonTogglePower_Click(Control arg1, Point arg2)
        {
            Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Asking Power Change status for " + Viewer.PlayerTrain.Cars[CarPosition].CarID + " / Pos : " + Viewer.PlayerTrain.Cars[CarPosition]));
            if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSLocomotive))
                ||
              (Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSElectricLocomotive))
                ||
              (Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSDieselLocomotive)))
            {
                new PowerCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive).PowerOn);
                if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive).PowerOn)
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Power OFF command sent"));
                else
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Power ON command sent"));
            }
            else
                Viewer.Simulator.Confirmer.Warning(Viewer.Catalog.GetString("No power command for this type of car!"));
        }

        void buttonToggleMU_Click(Control arg1, Point arg2)
        {

            if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSLocomotive))
                ||
              (Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSElectricLocomotive))
                ||
              (Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSDieselLocomotive)))
            {
                new ToggleMUCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive).AcceptMUSignals);
                if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSLocomotive).AcceptMUSignals)
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("MU signal connected"));
                else
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("MU signal disconnected"));
            }
            else
                Viewer.Simulator.Confirmer.Warning(Viewer.Catalog.GetString("No MU command for this type of car!"));
        }

        void buttonToggleBrakeHose_Click(Control arg1, Point arg2)
        {
            new WagonBrakeHoseConnectCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.FrontBrakeHoseConnected);
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.FrontBrakeHoseConnected)
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Front brake hose connected"));
            else
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Front brake hose disconnected"));
        }

        void buttonToggleAngleCockA_Click(Control arg1, Point arg2)
        {
            new ToggleAngleCockACommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.AngleCockAOpen);
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.AngleCockAOpen)
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Front angle cock opened"));
            else
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Front angle cock closed"));
        }

        void buttonToggleAngleCockB_Click(Control arg1, Point arg2)
        {
            new ToggleAngleCockBCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.AngleCockBOpen);
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.AngleCockBOpen)
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Rear angle cock opened"));
            else
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Rear angle cock closed"));
        }

        void buttonToggleBleedOffValve_Click(Control arg1, Point arg2)
        {
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem is SingleTransferPipe)
                return;

            new ToggleBleedOffValveCommand(Viewer.Log, (Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon), !(Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.BleedOffValveOpen);
            if ((Viewer.PlayerTrain.Cars[CarPosition] as MSTSWagon).BrakeSystem.BleedOffValveOpen)
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Bleed off valve opened"));
            else
                Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Bleed off valve closed"));
        }
    }
}
