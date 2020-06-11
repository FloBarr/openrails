using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Common;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using ORTS.Common;
using System;
using System.Collections.Generic;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;
using ORTS.Scripting.Api;
using System.Security.Cryptography;

namespace Orts.Viewer3D.Popups
{
    class LocomotivesOperationsLabel : Label
    {
        readonly Viewer Viewer;
        readonly int CarPosition;
        readonly int DieselNumber;

        public LocomotivesOperationsLabel(int x, int y, Viewer viewer, TrainCar car, int carPosition, int DieselCount, LabelAlignment alignment)
            : base(x, y, "", alignment)
        {
            Viewer = viewer;
            CarPosition = carPosition;
            DieselNumber = DieselCount;

            if (car is MSTSDieselLocomotive)
            {
                Text = "Engine " + DieselCount + " : " + (int)(car as MSTSDieselLocomotive).DieselEngines[DieselCount].RealRPM + " Rpm";
            }
            else if (car is MSTSElectricLocomotive)
            {
                if (DieselCount == 0) Text = "Engine Pantograph : " + (car as MSTSElectricLocomotive).Pantographs.State;
                else Text = "Engine Circuit Breaker : " + (car as MSTSElectricLocomotive).PowerSupply.CircuitBreaker.State;
            }
            else Text = car.CarID;

            Click += new Action<Control, Point>(EnginesInformationsLabel_Click);
        }

        void EnginesInformationsLabel_Click(Control arg1, Point arg2)
        {
            Viewer.LocomotiveOperationsWindow.CarPosition = CarPosition;
            Viewer.LocomotiveOperationsWindow.DieselNumber = DieselNumber;
            Viewer.LocomotiveOperationsWindow.Visible = true;
            //            Viewer.CarOperationsWindow.CarPosition = CarPosition;
            //            Viewer.CarOperationsWindow.Visible = true;
        }
    }

    public class LocomotiveOperationsWindow : Window
    {
        readonly Viewer Viewer;

        public int CarPosition
        {
            set;
            get;
        }
        public int DieselNumber;

        public LocomotiveOperationsWindow(WindowManager owner)
            : base(owner, Window.DecorationSize.X + owner.TextFontDefault.Height * 19, Window.DecorationSize.Y + owner.TextFontDefault.Height * 10 + ControlLayout.SeparatorSize * 9, Viewer.Catalog.GetString("Locomotive Operation Menu"))
        {
            Viewer = owner.Viewer;

        }

        protected override ControlLayout Layout(ControlLayout layout)
        {

            int wHeight = 0;
            int wSeparators = 0;
            int wLines = 0;


            var vbox = base.Layout(layout).AddLayoutVertical();
            int Count = 0;

            Label buttonToggleMU, buttonHandbrake, buttonAESS, buttonToggleBleedOffValve, buttonClose;
            Dictionary<object, Label> d;
            d = new Dictionary<object, Label>();

            var Car = Viewer.PlayerTrain.Cars[CarPosition];


            if (Car is MSTSDieselLocomotive)
            {
                bool AESSEquiped = false;
                bool AESSEnabled = false;

                foreach (var De in (Car as MSTSDieselLocomotive).DieselEngines)
                {
                    string TempName = "";


                    TempName = "Diesel" + Count.ToString();
                    Label wLabel;

                    vbox.Add(wLabel = new LocomotivesOperationsLabel(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer, Car, CarPosition, Count, LabelAlignment.Center));

                    wLabel.Color = Color.Green;
                    vbox.AddHorizontalSeparator();


                    wLabel.Click += new Action<Control, Point>(buttonTogglePower_Click);
                    //                   wLabel.Click += new Action<Control, Point>(buttonTest_Click);
                    Count++;
                    wLines++;
                    wSeparators++;
                }

                foreach (var De in (Viewer.PlayerTrain.Cars[CarPosition] as MSTSDieselLocomotive).DieselEngines)
                {
                    if (De.AESSEquiped == true) AESSEquiped = true;
                    if (De.AESSEnabled == true) AESSEnabled = true;
                }
                if (AESSEquiped)
                {
                    if (AESSEnabled == true) vbox.Add(buttonAESS = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Set AESS Inactive"), LabelAlignment.Center));
                    else vbox.Add(buttonAESS = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Set AESS Active"), LabelAlignment.Center));

                    vbox.AddHorizontalSeparator();

                    buttonAESS.Click += new Action<Control, Point>(buttonAESS_Click);
                    wLines++;
                    wSeparators++;
                }

            }

            if (Car is MSTSElectricLocomotive)
            {

                Label wLabel;

                vbox.Add(wLabel = new LocomotivesOperationsLabel(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer, Car, CarPosition, 0, LabelAlignment.Center));

                wLabel.Color = Color.Green;
                vbox.AddHorizontalSeparator();

                wLabel.Click += new Action<Control, Point>(buttonTogglePanto_Click);


                vbox.Add(wLabel = new LocomotivesOperationsLabel(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer, Car, CarPosition, 1, LabelAlignment.Center));

                wLabel.Color = Color.Green;
                vbox.AddHorizontalSeparator();

                wLabel.Click += new Action<Control, Point>(buttonToggleCircuitBreaker_Click);

                //                   wLabel.Click += new Action<Control, Point>(buttonTest_Click);
                Count++;
                wLines += 2;
                wSeparators += 2;
            }


            vbox.Add(buttonHandbrake = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Toggle Handbrake"), LabelAlignment.Center));
            vbox.AddHorizontalSeparator();
            vbox.Add(buttonToggleMU = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Toggle MU Connection"), LabelAlignment.Center));
            vbox.AddHorizontalSeparator();
            vbox.Add(buttonToggleBleedOffValve = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Open/Close Bleed Off Valve"), LabelAlignment.Center));
            vbox.AddHorizontalSeparator();
            vbox.Add(buttonClose = new Label(vbox.RemainingWidth, Owner.TextFontDefault.Height, Viewer.Catalog.GetString("Close window"), LabelAlignment.Center));



            buttonClose.Click += new Action<Control, Point>(buttonClose_Click);
            buttonHandbrake.Click += new Action<Control, Point>(buttonHandbrake_Click);

            buttonToggleMU.Click += new Action<Control, Point>(buttonToggleMU_Click);
            buttonToggleBleedOffValve.Click += new Action<Control, Point>(buttonToggleBleedOffValve_Click);

            wLines += 4;
            wSeparators += 3;

            wHeight = Window.DecorationSize.Y + (Owner.TextFontDefault.Height * wLines) + (ControlLayout.SeparatorSize * wSeparators);

            Viewer.LocomotiveOperationsWindow.SizeTo(Viewer.LocomotiveOperationsWindow.Location.Width, wHeight);

            return vbox;
        }


        void buttonClose_Click(Control arg1, Point arg2)
        {
            Visible = false;
        }


        public override void PrepareFrame(ElapsedTime elapsedTime, bool updateFull)
        {
            if (updateFull)
            {
                Layout();
            }
            base.PrepareFrame(elapsedTime, updateFull);
        }

        void buttonTogglePanto_Click(Control arg1, Point arg2)
        {
            if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSElectricLocomotive)))
            {
                var car = Viewer.PlayerTrain.Cars[CarPosition] as MSTSElectricLocomotive;
                if (car.Pantographs.State == PantographState.Up)
                {
                    car.SignalEvent(PowerSupplyEvent.LowerPantograph);
                }
                else
                {
                    car.SignalEvent(PowerSupplyEvent.RaisePantograph);
                }
            }
        }

        void buttonToggleCircuitBreaker_Click(Control arg1, Point arg2)
        {
            if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSElectricLocomotive)))
            {
                var car = Viewer.PlayerTrain.Cars[CarPosition] as MSTSElectricLocomotive;
                if (car.PowerSupply.CircuitBreaker.State == CircuitBreakerState.Open)
                {
                    car.SignalEvent(PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization);
                    car.SignalEvent(PowerSupplyEvent.CloseCircuitBreakerButtonPressed);
                }
                else
                {
                    car.SignalEvent(PowerSupplyEvent.OpenCircuitBreaker);
                }
            }
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
            if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSDieselLocomotive)))
            {
                var car = Viewer.PlayerTrain.Cars[CarPosition] as MSTSDieselLocomotive;

                if (car.DieselEngines[DieselNumber].EngineStatus == DieselEngine.Status.Running)
                {
                    car.DieselEngines[DieselNumber].Stop();
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Power OFF command sent"));
                }
                else
                {
                    car.DieselEngines[DieselNumber].Start();
                    Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Power ON command sent"));
                }
            }
            else if ((Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSElectricLocomotive)))
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


        void buttonAESS_Click(Control arg1, Point arg2)
        {
            bool AESSActivated = false;

            Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("AESS Enabling/Disabling Command"));
            if (Viewer.PlayerTrain.Cars[CarPosition].GetType() == typeof(MSTSDieselLocomotive))
            {
                foreach (var De in (Viewer.PlayerTrain.Cars[CarPosition] as MSTSDieselLocomotive).DieselEngines)
                {
                    De.AESSEnabled = !De.AESSEnabled;
                    if (De.AESSEnabled == true) AESSActivated = true;
                    if ((De.AESSEnabled == false) && (De.EngineStatus != DieselEngine.Status.Running)) De.Start();
                }
                if (AESSActivated == true) Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("AESS Enabled"));
                else Viewer.Simulator.Confirmer.Information(Viewer.Catalog.GetString("AESS Disabled, running stopped engines"));
            }
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
