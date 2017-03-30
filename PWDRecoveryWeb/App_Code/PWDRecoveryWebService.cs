using RecoveryManagerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

// NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "PWDRecoveryWebService" in code, svc and config file together.
public class PWDRecoveryWebService : IPWDRecoveryWebService, IRecoveryManagerGUICallback
{
    private NetTcpBinding tcpBinding;
    private String recovManagerURL = "net.tcp://localhost:5002/RecovManager";
    private ChannelFactory<IRecoveryManagerGUI> rmFactory;
    private IRecoveryManagerGUI manager;

    private Boolean connected = false;

    public PWDRecoveryWebService()
    {
        tcpBinding = new NetTcpBinding();
        IRecoveryManagerGUICallback callBackObject = this;
        try
        {
            rmFactory = new DuplexChannelFactory<IRecoveryManagerGUI>(callBackObject, tcpBinding, recovManagerURL);  //Sets up the channel factory with above created tcp binding and url. 
            manager = rmFactory.CreateChannel();                                                                     //Creates the channel for client-server communication.   

            connected = true;

            IClientChannel contextChannel = manager as IClientChannel;          //Need to change the timeout period.
            contextChannel.OperationTimeout = TimeSpan.FromMinutes(10000);
            
        }

        catch (CommunicationException ce)
        {
            connected = false;
            Console.WriteLine("ERROR: web service could not connect to the manager:\n" + ce.Message);
        }
        catch (TimeoutException te)
        {
            connected = false;
            Console.WriteLine("ERROR: web service could not connect to the manager:\n" + te.Message);
        }
        catch (Exception exc)
        {
            Console.WriteLine("ERROR: web service could not connect to the manager:\n" + exc.Message);
        }
    }


    //Gets a password 
    public String wsGenerateLength(int length)
    {
        String outcome = "ERROR: manager server unavailable.";
        if(connected)
        {
            outcome = "Error: Length must be greater than 0 and less than 10.";
            if (length > 0 && length <= 10)
            {
                String encryptedPW = "";


                try
                {
                    encryptedPW = manager.generateNewPassword(length);
                    outcome = encryptedPW;
                    rmFactory.Close();                                      //Close the connection!
                }
                catch (CommunicationException e)
                {
                    outcome = "Error: could not connect to the recovery server: " + e.Message;
                    Console.WriteLine("ERROR in wsGenerateLength:\n" + e.Message);
                }
                catch (TimeoutException e)
                {
                    outcome = "Error: recovery server timed out: " + e.Message;
                    Console.WriteLine("ERROR in wsGenerateLength:\n" + e.Message);
                }
                catch(Exception e)
                {
                    outcome = "Error: recovery server failed." + e.Message;
                    Console.WriteLine("ERROR in wsGenerateLength:\n" + e.Message);
                }
            }

        }
        
        return outcome;
    }

    public String wsBeginRecovery()
    {
        String password = "";
        try
        {
            //System.Threading.Thread pingThread = new Thread(pingLoop());                  //Begin the update tot he GUI
            //pingThread.Start();

            password = manager.beginViaWeb();
            password = "Password Found!: " + password;
            rmFactory.Close();
        }
        catch(CommunicationException e)
        {
            password = "Error: could not connect to the recovery server: " + e.Message;
        }
        catch(TimeoutException e)
        {
            password = "Error: connection to recovery server timed out." + e.Message;
        }
        catch(Exception e)
        {
            password = "ERROR: could not connect to the recovery server: " + e.Message;
        }

       
        return password;
    }

   
    /*************** Callback interface methods - blank for web service *******************/
    public void setNumConnected(int numConnected)
    {
        //GUI method, not web
    }

    public void passwordFound(String password)
    {
        //GUI method, not web
    }

    public void updateJobStats(int numConnected, long numTested, long rate, long elapsedSeconds)
    {
        //GUI method, not web
    }
}


/*

wsBeginRecovery SOAP request:
 * 
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Header>
    <Action s:mustUnderstand="1" xmlns="http://schemas.microsoft.com/ws/2005/05/addressing/none">http://tempuri.org/IPWDRecoveryWebService/wsBeginRecovery</Action>
  </s:Header>
  <s:Body>
    <wsBeginRecovery xmlns="http://tempuri.org/" />
  </s:Body>
</s:Envelope>

 * wsBeginRecovery response:
 
 <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Header />
  <s:Body>
    <wsBeginRecoveryResponse xmlns="http://tempuri.org/">
      <wsBeginRecoveryResult>foobar</wsBeginRecoveryResult>
    </wsBeginRecoveryResponse>
  </s:Body>
</s:Envelope>
 * 
 * * 
*/