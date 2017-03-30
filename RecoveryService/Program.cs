using PwdRecover;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;


namespace RecoveryService
{
    /// <summary>
    ///             Runs the main which creates the RecoveryServiceImpl server object.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            
            //Create the service object. Constructor connects automatically.
            RecoveryServiceImpl rs = new RecoveryServiceImpl();
           
            System.Console.WriteLine("Press Enter at any time to Exit");
            System.Console.ReadLine();

           
        }
    }
}
