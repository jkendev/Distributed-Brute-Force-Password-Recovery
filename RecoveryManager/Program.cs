using RecoveryManagerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;


namespace RecoveryManager
{
    /// <summary>
    ///             Runs the main which sets up end point connection and creates the RecoveryManagerImpl object.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            
            RecoveryManagerImpl manager = new RecoveryManagerImpl();                                    //Create the Manager server object
            
            String recovManagerURL = "net.tcp://localhost:5002/RecovManager";                           //URL to find the manager on.
            ServiceHost host = new ServiceHost(manager); 
            NetTcpBinding tcpBinding = new NetTcpBinding();

           
            try
            {
                //This sets up an end point for the GUI and RecoveryServices to connect to this tier! 
                host.AddServiceEndpoint(typeof(IRecoveryManagerService), tcpBinding, recovManagerURL);  //Add end point for services to connect to
                host.AddServiceEndpoint(typeof(IRecoveryManagerGUI), tcpBinding, recovManagerURL);      //Add end point for GUI to connect to. 
                host.Open();
                Console.WriteLine("Host opened.");
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine("This service has somehow been closed. Could not initiate the server");  //It shouldn't be possible to occur!
                Console.WriteLine(e.Message);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error: this service host could not be opened. Could not initiate server");   //Again, impossible to occurr, I'm pretty sure. 
                Console.WriteLine(e.Message);
            }
            catch (CommunicationObjectFaultedException e)   
            {
                Console.WriteLine("Error: could not initiate server. ");
                Console.WriteLine(e.Message);
            }
            catch (TimeoutException e)
            {
                Console.WriteLine("Error: could not open host - time out.");      //Don't think this can happen either?
                Console.WriteLine(e.Message);
            }

            System.Console.WriteLine("Press Enter to Exit");
            System.Console.ReadLine();

            try
            {
                host.Close();
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine("Error could not close host : The communication object is in a Closing or Closed state and cannot be modified.");
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("Error could not close host : The communication object is not in a Opened or Opening state and cannot be modified.");
            }
            catch (CommunicationObjectFaultedException e)
            {
                Console.WriteLine("Error could not close host : the The communication object is in a Faulted state and cannot be modified.");
            }
            catch (TimeoutException e)
            {
                Console.WriteLine("Error could not close host : The default interval of time that was allotted for the operation was exceeded before the operation was completed.");
            }
            


            //Console.WriteLine("Press enter to exit");
            //Console.ReadLine();



        }
    }
}
