using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace OverlayFS
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] 
			{ 
				new Service1() 
			};
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                Service1.StartThread();
            }
        }
    }
}
