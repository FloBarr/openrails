using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using ORTS.Common;
using Orts.Formats.Msts;

//** Flo    **//
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.ComponentModel;
//** Flo    **//


namespace Orts.Viewer3D.SharedMemoryFiles
{
    //** Flo    **//

    /// <summary>
    /// Access rights for file mapping objects
    /// http://msdn.microsoft.com/en-us/library/aa366559.aspx
    /// </summary>
    [Flags]
    public enum FileMapAccess
    {
        FILE_MAP_COPY = 0x0001,
        FILE_MAP_WRITE = 0x0002,
        FILE_MAP_READ = 0x0004,
        FILE_MAP_ALL_ACCESS = 0x000F001F
    }

    public class SharedMemory
    {
        // ===========================================================================================
        //      Viewer object from Viewer3D - needed for acces to Heads Up Display Data
        // ===========================================================================================
        public Viewer viewer;
        private bool Running = false;
        string MapPrefix = "";
        string MapName = "";
        string FullMapName = "";

        uint ViewOffset = 0;

        uint ViewSize = 1024;

        // Max size of the file mapping object.
        uint MapSize = 65536;
        public IntPtr pView = IntPtr.Zero;


        //private SharedMemory SharedMem = new SharedMemory();


        SafeFileMappingHandle hMapFile = null;


        // ===========================================================================================
        //  	SharedMemory constructor
        // ===========================================================================================
        public SharedMemory(string wPrefix = "Local\\", string wSample = "SampleMap")
        {
            // Unicode string message to be written to the mapped view. Its size in 
            // byte must be less than the view size (VIEW_SIZE). 
            const string Message = "Message d'initialisation emis depuis OpenRails.";

            MapPrefix = wPrefix;
            MapName = wSample;
            FullMapName = MapPrefix + MapName;
            ViewOffset = 0;
            ViewSize = 1024;
            MapSize = 65536;


            if (Running)
                return;
            while ((viewer = Program.Viewer) == null)
                Thread.Sleep(1000);


            //************************************************************************************//
            //** Mon bazar, ouverture de communication                                          **//
            //************************************************************************************//
            try
            {
                // Create the file mapping object.
                hMapFile = NativeMethod.CreateFileMapping(
                    INVALID_HANDLE_VALUE,   // Use paging file - shared memory
                    IntPtr.Zero,            // Default security attributes
                    FileProtection.PAGE_READWRITE, // Allow read and write access
                    0,                      // High-order DWORD of file mapping max size
                    MapSize,                // Low-order DWORD of file mapping max size
                    FullMapName             // Name of the file mapping object
                    );

                if (hMapFile.IsInvalid)
                {
                    Console.WriteLine("Error, invalid hmapfile ");
                    throw new Win32Exception();
                }

                Console.WriteLine("The file mapping ({0}) is created", FullMapName);

                // Map a view of the file mapping into the address space of the 
                // current process.
                pView = NativeMethod.MapViewOfFile(
                    hMapFile,                       // Handle of the map object
                    FileMapAccess.FILE_MAP_ALL_ACCESS, // Read and write access
                    0,                              // High-order DWORD of file offset 
                    ViewOffset,                     // Low-order DWORD of file offset
                    ViewSize                        // Byte# to map to the view
                    );

                if (pView == IntPtr.Zero)
                {
                    Console.WriteLine("Error, pView = zero");
                    throw new Win32Exception();
                }


                Console.WriteLine("The file view is mapped");

                // Prepare a message to be written to the view. Append '\0' to 
                // mark the end of the string when it is marshaled to the native 
                // memory.
                byte[] bMessage = Encoding.ASCII.GetBytes(Message + '\0');


                // Write the message to the view.
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);

                //                Console.WriteLine("This message is written to the view:\n\"{0}\"",Message);


                // Wait to clean up resources and stop the process.
                //                Console.Write("Press ENTER to clean up resources and quit");
                //                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("The process throws the error: {0}", ex.Message);
            }

            finally
            {
            }
            //************************************************************************************//
            //** Mon bazar, test de comm: fin d'init                                            **//
            //************************************************************************************//
        }

        public void Run()
        {


            while (true)
            {
                String MessageToSend = "";

                Running = true;
                //** not usefull to run quickly, 10 updates per second are ok                   **//
                Thread.Sleep(100);
                MessageToSend = GenerateMessage();
                Trace.WriteLine(MessageToSend);
                UpdateSharedMemory(MessageToSend);
            }
        }


        public void stop()
        {
            if (Running)
            {
                Running = false;
                // TODO:
                // Will Shutdown and close break out of any async waiting states??
                Trace.WriteLine("Arret de la mémoire partagée");
                //************************************************************************************//
                //** Mon bazar, test de comm                                                        **//
                //************************************************************************************//
                if (hMapFile != null)
                {
                    if (pView != IntPtr.Zero)
                    {
                        // Unmap the file view.
                        NativeMethod.UnmapViewOfFile(pView);
                        pView = IntPtr.Zero;
                    }
                    // Close the file mapping object.
                    hMapFile.Close();
                    hMapFile = null;
                    Console.WriteLine("Fermeture de la mémoire partagée");
                }
                //************************************************************************************//
                //** Mon bazar, test de comm: fermeture                                             **//
                //************************************************************************************//

            }
        }


        private string GenerateMessage()
        {
            //************************************************//
            //** MON BAZAR: recup et envoi des infos rames  **//
            //** Rame joueur en cours                       **//
            //** Variables locales diverses et variées  **//
            string MessageGenere = "";

            double wRPM = 0;
            string wRPMs = "";

            string wBoiteVs = "";
            string wEfforts = "";
            float wEffortLoc = 0;
            float wEffortMaxLoc = 0;
            string wEffortsMax = "";
            string wPuissances = "";
            string wPuissancesMax = "";
            double wPuissanceLoc = 0;

            double wPuissanceMax = 0;
            double wMaxSpeedKmH = ((viewer.PlayerTrain.LeadLocomotive as MSTSLocomotive).MaxSpeedMpS) * 3.6;

            double wMaxCurrent = 0;
            int wUrgence = 0;
            string wTypes = "";
            string wPantographes = "";
            //bool wDJAutorise = false;
            //bool wDJFerme = false;
            //bool wAlim = false;
            string wDJsAutorises = "";
            string wDJsFermes = "";
            string wAlims = "";
            double wAmperage = 0;
            string wAmperages = "";
            double wVoltage = 0;
            string wVoltages = "";

            string wNom = viewer.PlayerTrain.Name;
            string wLoco = "";
            string wPatinages = "";
            string wEnrayages = "";

            //************************************************//                
            string Message = "Test";

            //** Recup de toutes les machines du consist    **//
            for (int i = 0; i < (viewer.PlayerTrain.Cars.Count); i++)
            {
                if (viewer.PlayerTrain.Cars[i] is MSTSLocomotive)
                {
                    //** Infos sur la rame joueur           **//
                    if (viewer.PlayerTrain.Cars[i].IsDriveable)
                    {
                        //** Séparations pour les infos des différentes locs    **//
                        if (i > 0)
                        {
                            wLoco += '-';
                            wTypes += "-";
                            wPantographes += "-";
                            wDJsAutorises += "-";
                            wDJsFermes += "-";
                            wAlims += "-";
                            wRPMs += "-";
                            wBoiteVs += "-";
                            wEfforts += "-";
                            wEffortsMax += "-";
                            wAmperages += "-";
                            wVoltages += "-";
                            wPuissances += "-";
                            wPuissancesMax += "-";
                            wPatinages += '-';
                            wEnrayages += '-';
                        }

                        //** Conversion True/False en 1/0                       **//
                        if ((viewer.PlayerTrain.Cars[i] as MSTSLocomotive).WheelSlip == true) wPatinages += '1'; else wPatinages += '0';
                        if ((viewer.PlayerTrain.Cars[i] as MSTSLocomotive).WheelSkid == true) wEnrayages += '1'; else wEnrayages += '0';

                        //** Infos Mazouts                                      **//
                        if (viewer.PlayerTrain.Cars[i] is MSTSDieselLocomotive)
                        {
                            wTypes += "Diesel";

                            string wBoiteV = "";
                            if ((viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DieselEngines[0].HasGearBox)
                            {
                                wBoiteV = ((viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).GearBox.NextGearIndex + 1).ToString();

                            }
                            else
                            {
                                wBoiteV = "x";
                            }

                            wRPM = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DieselEngines[0].RealRPM;
                            wRPMs += Math.Round(wRPM, 0);

                            wBoiteVs += wBoiteV;

                            wAlims += (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DieselEngines.PowerOn;

                            //** Efforts    **//
                            wEffortLoc = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).FilteredMotiveForceN;
                            wEfforts += Math.Round(Math.Abs(wEffortLoc), 0);

                            wEffortMaxLoc = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).MaxForceN;    //.TractiveForceCurves.Get((MUThrottlePercent / 100f), Math.Abs(SpeedMpS)); //
                            wEffortsMax += Math.Round(Math.Abs(wEffortMaxLoc), 0);

                            //** Amperages  **//
                            wMaxCurrent = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).MaxCurrentA;
                            if((viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).HasDCMotor==true)
                            {
                                wAmperage = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DCMotorNumber* (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DisplayedAmperage;
                            }
                            else
                            {
                                wAmperage = Math.Abs(wEffortLoc / wEffortMaxLoc * wMaxCurrent);
                            }
                            wAmperages += Math.Round(Math.Abs(wAmperage), 0);

                            //** Voltages   **//
                            if ((viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).HasDCMotor == true)
                            {
                                wVoltage = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).Voltage;
                            }
                            wVoltages+= Math.Round(Math.Abs(wVoltage), 0);

                            //** Puissances **//
                            wPuissanceLoc = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DieselEngines[0].OutputPowerW / 1000;
                            wPuissances += Math.Round(Math.Abs(wPuissanceLoc), 0);

                            wPuissanceLoc = (viewer.PlayerTrain.Cars[i] as MSTSDieselLocomotive).DieselEngines[0].MaximumDieselPowerW / 1000;    //   .MaxOutputPowerW / 1000;
                            wPuissancesMax += Math.Round(Math.Abs(wPuissanceLoc), 0);



                        }
                        //** Et les Electriques                         **//
                        if (viewer.PlayerTrain.Cars[i] is MSTSElectricLocomotive)
                        {
                            wTypes += "Electric";

                            var status = new StringBuilder();
                            foreach (var pantograph in (viewer.PlayerTrain.Cars[i] as MSTSLocomotive).Pantographs.List)
                                status.AppendFormat("{0} ", Simulator.Catalog.GetParticularString("Pantograph", GetStringAttribute.GetPrettyName(pantograph.State)));
                            status.AppendLine();


                            wPantographes += status.ToString();
                            wDJsAutorises += (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).PowerSupply.CircuitBreaker.DriverClosingAuthorization.ToString();
                            wDJsFermes += (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).PowerSupply.CircuitBreaker.DriverClosingOrder.ToString();
                            wAlims += (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).PowerOn.ToString();

                            //** Efforts    **//
                            wEffortLoc = (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).FilteredMotiveForceN;
                            wEfforts += Math.Round(Math.Abs(wEffortLoc), 0);

                            wEffortMaxLoc = (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).MaxForceN;  //.TractiveForceCurves.Get((MUThrottlePercent/100f), Math.Abs(SpeedMpS)); //.MaxForceN;
                            wEffortsMax += Math.Round(Math.Abs(wEffortMaxLoc), 0);

                            //** Amperages  **//
                            wMaxCurrent = (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).MaxCurrentA;
                            wAmperage = Math.Abs((wEffortLoc / wEffortMaxLoc) * wMaxCurrent);
                            wAmperages += Math.Round(Math.Abs(wAmperage), 0);

                            //** Puissances **//
                            wPuissanceMax = (viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).MaxPowerW / 1000;
                            wPuissancesMax += Math.Round(Math.Abs(wPuissanceMax), 0);

                            wPuissanceLoc = ((viewer.PlayerTrain.Cars[i] as MSTSElectricLocomotive).MotiveForceN / wPuissanceMax) * viewer.PlayerTrain.SpeedMpS * 3; //** Coeff *3, pas tout à fait exact, pas compris l'origine...  **//
                            wPuissances += Math.Round(Math.Abs(wPuissanceLoc), 0);
                        }
                        //** Vapeurs à venir                            **//

                        //** Nom de la loc                              **//
                        wLoco += (viewer.PlayerTrain.Cars[i] as MSTSLocomotive).LocomotiveName;
                    }
                }
            }
            //** Fin Recup de toutes les machines  **//

            //** Infos générales                    **//
            int wFanaux = (viewer.PlayerTrain.LeadLocomotive as MSTSLocomotive).Headlight;

            wUrgence = (viewer.PlayerTrain.LeadLocomotive as MSTSLocomotive).EmergencyButtonPressed ? 1 : 0;

            float wFrein = (viewer.PlayerTrain.LeadLocomotive as MSTSLocomotive).TrainBrakeController.CurrentValue;
            double wLongueur = viewer.PlayerTrain.Length;
            float wDistanceSignal = viewer.PlayerTrain.DistanceToSignal.Value;
            int wDirection = 0;
            MstsSignalAspect wAspect = viewer.PlayerTrain.GetNextSignalAspect(wDirection); //GetNextSignalAspect(wDirection);

            double wOdometrie = Math.Round(viewer.PlayerTrain.DistanceTravelledM, 0);



            //** Mise en forme                                          **//
            Message = "I=" +
                Math.Round((viewer.PlayerTrain.SpeedMpS * 3.6), 1) + ';' +                 //1
                Math.Round((viewer.PlayerTrain.TrainMaxSpeedMpS * 3.6), 0) + ';' +
                Math.Round((viewer.PlayerTrain.AllowedMaxSpeedMpS * 3.6), 0) + ';' +
                Math.Round(viewer.PlayerTrain.BrakeLine2PressurePSI, 1) + ';' +
                Math.Round(viewer.PlayerTrain.EqualReservoirPressurePSIorInHg, 1) + ';' +  //5
                Math.Round(viewer.PlayerTrain.LeadPipePressurePSI, 1) + ';' +
                Math.Round(viewer.PlayerTrain.HUDLocomotiveBrakeCylinderPSI, 1) + ';' +
                Math.Round(viewer.PlayerTrain.MUThrottlePercent, 0) + ';' +
                viewer.PlayerTrain.MUDirection + ';' +
                wUrgence + ';' +                                        //10
                wRPMs + ';' +
                wBoiteVs + ';' +
                wEfforts + ';' +
                wEffortsMax + ';' +
                wPuissances + ';' +                                      //15
                wPuissancesMax + ';' +
                wAmperages + ';' +
                wVoltages + ';' +
                wFrein + ';' +
                Math.Round(wLongueur, 0) + ';' +                        //20
                Math.Round(wDistanceSignal, 0) + ';' +                  
                wAspect + ';' +
                wOdometrie + ';' +
                wLoco + ';' +
                wNom + ';' +                                            //25
                wTypes + ';' +
                wPantographes + ';' +
                wDJsAutorises + ';' +
                wDJsFermes + ';' +
                wAlims + ';' +                                          //30
                viewer.PlayerTrain.Cars.Count + ';' +                       
                wFanaux + ';' +
                wPatinages + ';' +
                wEnrayages + ';';

            MessageGenere = Message;
            //** En mise à dispo                              **//
            return MessageGenere;
            //**************************************************//
        }
        //** FIN DE BAZAR                                     **//


        public void UpdateSharedMemory(string Message)
        {
            if (pView == IntPtr.Zero)
            {
                throw new Win32Exception();
            }
            else
            {
                // Prepare a message to be written to the view. Append '\0' to 
                // mark the end of the string when it is marshaled to the native 
                // memory.
                byte[] bMessage = Encoding.ASCII.GetBytes(Message + '\0');


                // Write the message to the view.
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);
            }

        }
        #region Native API Signatures and Types

        /// <summary>
        /// Memory Protection Constants
        /// http://msdn.microsoft.com/en-us/library/aa366786.aspx
        /// </summary>
        [Flags]
        public enum FileProtection : uint
        {
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400,
            SEC_FILE = 0x800000,
            SEC_IMAGE = 0x1000000,
            SEC_RESERVE = 0x4000000,
            SEC_COMMIT = 0x8000000,
            SEC_NOCACHE = 0x10000000
        }


        /// <summary>
        /// Access rights for file mapping objects
        /// http://msdn.microsoft.com/en-us/library/aa366559.aspx
        /// </summary>
        [Flags]
        public enum FileMapAccess
        {
            FILE_MAP_COPY = 0x0001,
            FILE_MAP_WRITE = 0x0002,
            FILE_MAP_READ = 0x0004,
            FILE_MAP_ALL_ACCESS = 0x000F001F
        }


        /// <summary>
        /// Represents a wrapper class for a file mapping handle. 
        /// </summary>
        [SuppressUnmanagedCodeSecurity,
        HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            private SafeFileMappingHandle()
                : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            public SafeFileMappingHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                base.SetHandle(handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
            DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }
        }


        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);


        /// <summary>
        /// The class exposes Windows APIs used in this code sample.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class NativeMethod
        {
            /// <summary>
            /// Opens a named file mapping object.
            /// </summary>
            /// <param name="dwDesiredAccess">
            /// The access to the file mapping object. This access is checked against 
            /// any security descriptor on the target file mapping object.
            /// </param>
            /// <param name="bInheritHandle">
            /// If this parameter is TRUE, a process created by the CreateProcess 
            /// function can inherit the handle; otherwise, the handle cannot be 
            /// inherited.
            /// </param>
            /// <param name="lpName">
            /// The name of the file mapping object to be opened.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is an open handle to the 
            /// specified file mapping object.
            /// </returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafeFileMappingHandle OpenFileMapping(
                FileMapAccess dwDesiredAccess, bool bInheritHandle, string lpName);

            /// <summary>
            /// Creates or opens a named or unnamed file mapping object for a 
            /// specified file.
            /// </summary>
            /// <param name="hFile">
            /// A handle to the file from which to create a file mapping object.
            /// </param>
            /// <param name="lpAttributes">
            /// A pointer to a SECURITY_ATTRIBUTES structure that determines 
            /// whether a returned handle can be inherited by child processes.
            /// </param>
            /// <param name="flProtect">
            /// Specifies the page protection of the file mapping object. All 
            /// mapped views of the object must be compatible with this 
            /// protection.
            /// </param>
            /// <param name="dwMaximumSizeHigh">
            /// The high-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="dwMaximumSizeLow">
            /// The low-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="lpName">
            /// The name of the file mapping object.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is a handle to the 
            /// newly created file mapping object.
            /// </returns>
            [DllImport("Kernel32.dll", SetLastError = true)]
            public static extern SafeFileMappingHandle CreateFileMapping(
                IntPtr hFile,
                IntPtr lpAttributes,
                FileProtection flProtect,
                uint dwMaximumSizeHigh,
                uint dwMaximumSizeLow,
                string lpName);


            /// <summary>
            /// Maps a view of a file mapping into the address space of a calling
            /// process.
            /// </summary>
            /// <param name="hFileMappingObject">
            /// A handle to a file mapping object. The CreateFileMapping and 
            /// OpenFileMapping functions return this handle.
            /// </param>
            /// <param name="dwDesiredAccess">
            /// The type of access to a file mapping object, which determines the 
            /// protection of the pages.
            /// </param>
            /// <param name="dwFileOffsetHigh">
            /// A high-order DWORD of the file offset where the view begins.
            /// </param>
            /// <param name="dwFileOffsetLow">
            /// A low-order DWORD of the file offset where the view is to begin.
            /// </param>
            /// <param name="dwNumberOfBytesToMap">
            /// The number of bytes of a file mapping to map to the view. All bytes 
            /// must be within the maximum size specified by CreateFileMapping.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is the starting address 
            /// of the mapped view.
            /// </returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr MapViewOfFile(
                SafeFileMappingHandle hFileMappingObject,
                FileMapAccess dwDesiredAccess,
                uint dwFileOffsetHigh,
                uint dwFileOffsetLow,
                uint dwNumberOfBytesToMap);


            /// <summary>
            /// Unmaps a mapped view of a file from the calling process's address 
            /// space.
            /// </summary>
            /// <param name="lpBaseAddress">
            /// A pointer to the base address of the mapped view of a file that 
            /// is to be unmapped.
            /// </param>
            /// <returns></returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        }

        #endregion
    }

    /// <summary>
    /// Represents a wrapper class for a file mapping handle. 
    /// </summary>
    [SuppressUnmanagedCodeSecurity,
    HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
    internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private SafeFileMappingHandle()
            : base(true)
        {
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public SafeFileMappingHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            base.SetHandle(handle);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
        DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            return CloseHandle(base.handle);
        }
    }


    /// <summary>
    /// The class exposes Windows APIs used in this code sample.
    /// </summary>
}
